using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.VisualTree;
using Shapes = Avalonia.Controls.Shapes;
using DeskLLM.Avalonia.Configuration;
using DeskLLM.Avalonia.Services;
using AvScreen = Avalonia.Platform.Screen;
using AvButton = Avalonia.Controls.Button;
using AvRoutedEventHandler = System.EventHandler<Avalonia.Interactivity.RoutedEventArgs>;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingSize = System.Drawing.Size;
using DrawingPen = System.Drawing.Pen;
using DrawingColor = System.Drawing.Color;
using Drawing2D = System.Drawing.Drawing2D;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingImageFormat = System.Drawing.Imaging.ImageFormat;

namespace DeskLLM.Avalonia.Views;

public partial class AnnotationWindow : Window
{
    private enum Mode { Select, Draw }
    private Mode _mode = Mode.Select;
    private global::Avalonia.Point _start;
    private Shapes.Rectangle? _rect;
    private Shapes.Polyline? _currentPolyline;
    private List<global::Avalonia.Point>? _currentStroke;
    private readonly List<List<global::Avalonia.Point>> _strokes = new();
    private readonly List<Border> _toolbars = new();
    private Canvas? _toolbarCanvas;
    private PixelPoint _virtualOrigin = new PixelPoint(0, 0);
    private double _renderScale = 1.0;

    public string? CapturedImageBase64 { get; private set; }
    public PixelRect? SelectedRegion { get; private set; }

    public AnnotationWindow()
    {
        InitializeComponent();
        var store = new ConfigurationStore();
        Icon = IconProvider.LoadWindowIcon(store.Current.IconAsset);
        _toolbarCanvas = this.FindControl<Canvas>("ToolbarCanvas");
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
        Opened += (_, _) => ExpandToAllScreens();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public void OnSelectMode(object? s, RoutedEventArgs e) => _mode = Mode.Select;
    public void OnDrawMode(object? s, RoutedEventArgs e) => _mode = Mode.Draw;
    public async void OnConfirm(object? s, RoutedEventArgs e)
    {
        try
        {
            var rect = this.FindControl<Shapes.Rectangle>("SelectionRectangle");
            if (rect is null) { Close(); return; }
            var left = Canvas.GetLeft(rect);
            var top = Canvas.GetTop(rect);
            var width = rect.Width;
            var height = rect.Height;

            if (double.IsNaN(left) || double.IsNaN(top) || width <= 0 || height <= 0)
            {
                Close();
                return;
            }

            // Convert DIP to physical pixels and offset by window position
            var topLevel = TopLevel.GetTopLevel(this);
            var scale = topLevel?.RenderScaling ?? 1.0;
            var winPos = this.Position; // pixel coordinates
            var sx = (int)Math.Round(left * scale) + winPos.X;
            var sy = (int)Math.Round(top * scale) + winPos.Y;
            var sw = (int)Math.Round(width * scale);
            var sh = (int)Math.Round(height * scale);

            var region = new PixelRect(sx, sy, sw, sh);
            SelectedRegion = region;
            var strokes = SnapshotStrokes();

            // Hide overlay elements and window before screen copy so the green selection isn't captured
            rect.IsVisible = false;
            this.Hide();
            await Task.Delay(80); // allow compositor to update

            CapturedImageBase64 = CaptureRegionToBase64(region, strokes, scale, winPos);
        }
        catch
        {
            CapturedImageBase64 = null;
            SelectedRegion = null;
        }
        finally
        {
            Close();
        }
    }
    public void OnReset(object? s, RoutedEventArgs e)
    {
        var rect = this.FindControl<Shapes.Rectangle>("SelectionRectangle");
        if (rect is null) return;
        rect.IsVisible = false;
        var canvas = this.FindControl<Canvas>("SelectionCanvas");
        if (canvas is null) return;
        canvas.Children.Clear();
        canvas.Children.Add(rect);
        _strokes.Clear();
        _currentStroke = null;
        _currentPolyline = null;
    }
    public void OnCancel(object? s, RoutedEventArgs e) => Close();

    private bool IsFromToolbar(PointerEventArgs e)
    {
        foreach (var toolbar in _toolbars)
        {
            var p = e.GetPosition(toolbar);
            if (p.X >= 0 && p.Y >= 0 && p.X <= toolbar.Bounds.Width && p.Y <= toolbar.Bounds.Height)
            {
                return true;
            }
        }
        return false;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsFromToolbar(e)) return;
        _start = e.GetPosition(this);
        _currentStroke = null;
        if (_mode == Mode.Select)
        {
            _rect = this.FindControl<Shapes.Rectangle>("SelectionRectangle");
            if (_rect is null) return;
            _rect.IsVisible = true;
            Canvas.SetLeft(_rect, _start.X);
            Canvas.SetTop(_rect, _start.Y);
            _rect.Width = 0; _rect.Height = 0;
        }
        else
        {
            var canvas = this.FindControl<Canvas>("SelectionCanvas");
            if (canvas is null) return;
            _currentPolyline = new Shapes.Polyline
            {
                Stroke = global::Avalonia.Media.Brushes.Lime,
                StrokeThickness = 2
            };
            _currentPolyline.Points.Add(_start);
            _currentStroke = new List<global::Avalonia.Point> { _start };
            _strokes.Add(_currentStroke);
            canvas.Children.Add(_currentPolyline);
        }
        e.Pointer.Capture(this);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (IsFromToolbar(e)) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var pos = e.GetPosition(this);
        if (_mode == Mode.Select && _rect != null)
        {
            var x = System.Math.Min(pos.X, _start.X);
            var y = System.Math.Min(pos.Y, _start.Y);
            var w = System.Math.Abs(pos.X - _start.X);
            var h = System.Math.Abs(pos.Y - _start.Y);
            Canvas.SetLeft(_rect, x);
            Canvas.SetTop(_rect, y);
            _rect.Width = w;
            _rect.Height = h;
        }
        else if (_mode == Mode.Draw && _currentPolyline != null)
        {
            _currentPolyline.Points.Add(pos);
            _currentStroke?.Add(pos);
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!IsFromToolbar(e))
        {
            e.Pointer.Capture(null);
        }
        _currentStroke = null;
        _currentPolyline = null;
    }

    private List<List<global::Avalonia.Point>> SnapshotStrokes()
    {
        if (_strokes.Count == 0)
        {
            return new List<List<global::Avalonia.Point>>();
        }

        return _strokes
            .Where(stroke => stroke.Count > 0)
            .Select(stroke => new List<global::Avalonia.Point>(stroke))
            .ToList();
    }

    private void ExpandToAllScreens()
    {
        var allScreens = Screens?.All;
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        _renderScale = scaling <= 0 ? 1.0 : scaling;

        if (allScreens is null || allScreens.Count == 0)
        {
            _virtualOrigin = new PixelPoint(Position.X, Position.Y);
            RenderToolbarsForScreens(null);
            return;
        }

        var minX = allScreens.Min(s => s.Bounds.X);
        var minY = allScreens.Min(s => s.Bounds.Y);
        var maxRight = allScreens.Max(s => s.Bounds.Right);
        var maxBottom = allScreens.Max(s => s.Bounds.Bottom);

        var widthPx = Math.Max(1, maxRight - minX);
        var heightPx = Math.Max(1, maxBottom - minY);

        Position = new PixelPoint(minX, minY);
        _virtualOrigin = new PixelPoint(minX, minY);

        Width = widthPx / _renderScale;
        Height = heightPx / _renderScale;

        RenderToolbarsForScreens(allScreens);
    }

    private void RenderToolbarsForScreens(IReadOnlyList<AvScreen>? screens)
    {
        var toolbarCanvas = _toolbarCanvas ??= this.FindControl<Canvas>("ToolbarCanvas");
        if (toolbarCanvas is null)
        {
            return;
        }

        toolbarCanvas.Children.Clear();
        _toolbars.Clear();

        if (screens is null || screens.Count == 0)
        {
            var toolbar = BuildToolbar();
            toolbarCanvas.Children.Add(toolbar);
            _toolbars.Add(toolbar);
            toolbar.Measure(global::Avalonia.Size.Infinity);
            var placeholderBounds = new PixelRect(_virtualOrigin, new PixelSize(
                Math.Max(1, (int)Math.Round(Math.Max(1, Bounds.Width) * _renderScale)),
                Math.Max(1, (int)Math.Round(Math.Max(1, Bounds.Height) * _renderScale))));
            PositionToolbar(toolbar, placeholderBounds);
            return;
        }

        foreach (var screen in screens)
        {
            var toolbar = BuildToolbar();
            toolbarCanvas.Children.Add(toolbar);
            _toolbars.Add(toolbar);
            toolbar.Measure(global::Avalonia.Size.Infinity);
            PositionToolbar(toolbar, screen.Bounds);
        }
    }

    private Border BuildToolbar()
    {
        var border = new Border
        {
            Background = new SolidColorBrush(global::Avalonia.Media.Color.Parse("#88000000")),
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(8)
        };

        var stack = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8
        };

        stack.Children.Add(CreateToolbarButton("Select", OnSelectMode));
        stack.Children.Add(CreateToolbarButton("Draw", OnDrawMode));
        stack.Children.Add(CreateToolbarButton("Confirm", OnConfirm, isPrimary: true));
        stack.Children.Add(CreateToolbarButton("Reset", OnReset));
        stack.Children.Add(CreateToolbarButton("Cancel", OnCancel));

        border.Child = stack;
        return border;
    }

    private AvButton CreateToolbarButton(string content, AvRoutedEventHandler handler, bool isPrimary = false)
    {
        var button = new AvButton { Content = content };
        button.Click += handler;
        if (isPrimary)
        {
            button.Classes.Add("primary");
        }
        return button;
    }

    private void PositionToolbar(Border toolbar, PixelRect screenBounds)
    {
        var widthDip = screenBounds.Width / _renderScale;
        var heightDip = screenBounds.Height / _renderScale;
        var offsetX = (screenBounds.X - _virtualOrigin.X) / _renderScale;
        var offsetY = (screenBounds.Y - _virtualOrigin.Y) / _renderScale;

        var desired = toolbar.DesiredSize;
        var left = offsetX + (widthDip - desired.Width) / 2;
        const double bottomMargin = 40;
        var top = offsetY + heightDip - desired.Height - bottomMargin;

        Canvas.SetLeft(toolbar, Math.Max(0, left));
        Canvas.SetTop(toolbar, Math.Max(0, top));
    }

    private static string CaptureRegionToBase64(
        PixelRect region,
        IReadOnlyList<IReadOnlyList<global::Avalonia.Point>> strokes,
        double scale,
        PixelPoint windowOrigin)
    {
        var width = Math.Max(1, region.Width);
        var height = Math.Max(1, region.Height);
        using var bmp = new DrawingBitmap(width, height, DrawingPixelFormat.Format32bppArgb);
        using var g = DrawingGraphics.FromImage(bmp);
        g.CopyFromScreen(region.X, region.Y, 0, 0, new DrawingSize(width, height), System.Drawing.CopyPixelOperation.SourceCopy);

        if (strokes.Count > 0)
        {
            g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new DrawingPen(DrawingColor.Lime, 4)
            {
                StartCap = Drawing2D.LineCap.Round,
                EndCap = Drawing2D.LineCap.Round,
                LineJoin = Drawing2D.LineJoin.Round
            };

            foreach (var stroke in strokes)
            {
                if (stroke.Count < 2)
                {
                    continue;
                }

                var drawingPoints = stroke
                    .Select(pt => new System.Drawing.PointF(
                        (float)((pt.X * scale) + windowOrigin.X - region.X),
                        (float)((pt.Y * scale) + windowOrigin.Y - region.Y)))
                    .ToArray();

                g.DrawLines(pen, drawingPoints);
            }
        }

        using var ms = new MemoryStream();
        bmp.Save(ms, DrawingImageFormat.Png);
        return Convert.ToBase64String(ms.ToArray());
    }
}
