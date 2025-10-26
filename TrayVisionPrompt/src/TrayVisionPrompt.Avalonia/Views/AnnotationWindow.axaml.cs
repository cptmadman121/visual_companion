using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Threading.Tasks;
using Shapes = Avalonia.Controls.Shapes;
using TrayVisionPrompt.Avalonia.Configuration;
using TrayVisionPrompt.Avalonia.Services;

namespace TrayVisionPrompt.Avalonia.Views;

public partial class AnnotationWindow : Window
{
    private enum Mode { Select, Draw }
    private Mode _mode = Mode.Select;
    private global::Avalonia.Point _start;
    private Shapes.Rectangle? _rect;
    private PolylineGeometry? _currentLine;
    private Shapes.Path? _currentPath;

    public string? CapturedImageBase64 { get; private set; }
    public PixelRect? SelectedRegion { get; private set; }

    public AnnotationWindow()
    {
        InitializeComponent();
        var store = new ConfigurationStore();
        Icon = IconProvider.LoadWindowIcon(store.Current.IconAsset);
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
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
            var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
            var winPos = this.Position; // pixel coordinates
            var sx = (int)Math.Round(left * scale) + winPos.X;
            var sy = (int)Math.Round(top * scale) + winPos.Y;
            var sw = (int)Math.Round(width * scale);
            var sh = (int)Math.Round(height * scale);

            var region = new PixelRect(sx, sy, sw, sh);
            SelectedRegion = region;

            // Hide overlay elements and window before screen copy so the green selection isn't captured
            rect.IsVisible = false;
            this.Hide();
            await Task.Delay(80); // allow compositor to update

            CapturedImageBase64 = CaptureRegionToBase64(region);
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
    }
    public void OnCancel(object? s, RoutedEventArgs e) => Close();

    private bool IsFromToolbar(PointerEventArgs e)
    {
        var toolbar = this.FindControl<Border>("Toolbar");
        if (toolbar is null) return false;
        var p = e.GetPosition(toolbar);
        return p.X >= 0 && p.Y >= 0 && p.X <= toolbar.Bounds.Width && p.Y <= toolbar.Bounds.Height;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsFromToolbar(e)) return;
        _start = e.GetPosition(this);
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
            _currentLine = new PolylineGeometry();
            _currentLine.Points.Add(_start);
            _currentPath = new Shapes.Path
            {
                Stroke = global::Avalonia.Media.Brushes.Lime,
                StrokeThickness = 2,
                Data = _currentLine
            };
            canvas.Children.Add(_currentPath);
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
        else if (_mode == Mode.Draw && _currentLine != null)
        {
            _currentLine.Points.Add(pos);
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!IsFromToolbar(e))
        {
            e.Pointer.Capture(null);
        }
    }
    private static string CaptureRegionToBase64(PixelRect region)
    {
        var width = Math.Max(1, region.Width);
        var height = Math.Max(1, region.Height);
        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(region.X, region.Y, 0, 0, new System.Drawing.Size(width, height), CopyPixelOperation.SourceCopy);
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return Convert.ToBase64String(ms.ToArray());
    }
}
