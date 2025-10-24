using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using TrayVisionPrompt.Models;
using TrayVisionPrompt.Services;
using MessageBox = System.Windows.MessageBox;

namespace TrayVisionPrompt.Views;

public partial class AnnotationWindow : Window
{
    private System.Windows.Point _startPoint;
    private bool _isSelecting;
    private readonly ScreenshotService _screenshotService;
    private enum AnnotationMode { Select, Draw }
    private AnnotationMode _mode = AnnotationMode.Select;

    public CaptureResult? CaptureResult { get; private set; }

    public AnnotationWindow(ScreenshotService screenshotService)
    {
        InitializeComponent();
        _screenshotService = screenshotService;
        AnnotationInk.DefaultDrawingAttributes = new DrawingAttributes
        {
            Color = Colors.OrangeRed,
            Height = 4,
            Width = 4,
            FitToCurve = true
        };

        PreviewMouseLeftButtonDown += OnMouseLeftButtonDown;
        PreviewMouseMove += OnMouseMove;
        PreviewMouseLeftButtonUp += OnMouseLeftButtonUp;
        KeyDown += OnKeyDown;
        SetSelectMode();
    }

    private void OnMouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
    {
        // Ignore clicks on toolbar
        if (IsFromToolbar(e.OriginalSource)) return;
        if (_mode != AnnotationMode.Select) return;
        _startPoint = e.GetPosition(this);
        _isSelecting = true;
        AnnotationInk.IsHitTestVisible = false;
        SelectionRectangle.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionRectangle, _startPoint.X);
        Canvas.SetTop(SelectionRectangle, _startPoint.Y);
        SelectionRectangle.Width = 0;
        SelectionRectangle.Height = 0;
    }

    private void OnMouseMove(object? sender, System.Windows.Input.MouseEventArgs e)
    {
        if (IsFromToolbar(e.OriginalSource)) return;
        if (!_isSelecting || _mode != AnnotationMode.Select) return;
        var position = e.GetPosition(this);
        var x = Math.Min(position.X, _startPoint.X);
        var y = Math.Min(position.Y, _startPoint.Y);
        var width = Math.Abs(position.X - _startPoint.X);
        var height = Math.Abs(position.Y - _startPoint.Y);

        Canvas.SetLeft(SelectionRectangle, x);
        Canvas.SetTop(SelectionRectangle, y);
        SelectionRectangle.Width = width;
        SelectionRectangle.Height = height;
    }

    private void OnMouseLeftButtonUp(object? sender, MouseButtonEventArgs e)
    {
        if (IsFromToolbar(e.OriginalSource)) return;
        if (!_isSelecting || _mode != AnnotationMode.Select) return;
        _isSelecting = false;
        AnnotationInk.IsHitTestVisible = true;
    }

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        var dpiScale = VisualTreeHelper.GetDpi(this);
        string base64;
        int width;
        int height;
        Rect resultBounds;

        if (SelectionRectangle.Visibility == Visibility.Visible && SelectionRectangle.Width >= 2 && SelectionRectangle.Height >= 2)
        {
            var bounds = new Rect(Canvas.GetLeft(SelectionRectangle), Canvas.GetTop(SelectionRectangle), SelectionRectangle.Width, SelectionRectangle.Height);
            var scaledBounds = new Rect(bounds.X * dpiScale.DpiScaleX, bounds.Y * dpiScale.DpiScaleY, bounds.Width * dpiScale.DpiScaleX, bounds.Height * dpiScale.DpiScaleY);
            base64 = _screenshotService.CaptureRegion(scaledBounds, dpiScale.DpiScaleX);
            width = Math.Max(1, (int)Math.Round(scaledBounds.Width));
            height = Math.Max(1, (int)Math.Round(scaledBounds.Height));
            resultBounds = scaledBounds;
            var annotatedBase64Sel = AnnotationRenderer.Render(base64, AnnotationInk.Strokes, width, height, scaledBounds.X, scaledBounds.Y);
            SetResultAndClose(resultBounds, annotatedBase64Sel, dpiScale.DpiScaleX);
            return;
        }
        else
        {
            var vs = System.Windows.Forms.SystemInformation.VirtualScreen;
            base64 = _screenshotService.CaptureFullScreen();
            width = Math.Max(1, vs.Width);
            height = Math.Max(1, vs.Height);
            resultBounds = new Rect(0, 0, width, height);
        }

        var annotatedBase64 = AnnotationRenderer.Render(base64, AnnotationInk.Strokes, width, height, 0, 0);
        SetResultAndClose(resultBounds, annotatedBase64, dpiScale.DpiScaleX);
    }

    private void SetResultAndClose(Rect bounds, string annotatedBase64, double dpiScaleX)
    {
        CaptureResult = new CaptureResult
        {
            Bounds = bounds,
            ImageBase64 = annotatedBase64,
            DisplayScaling = dpiScaleX
        };
        DialogResult = true;
    }

    private void OnReset(object? sender, RoutedEventArgs e)
    {
        SelectionRectangle.Visibility = Visibility.Collapsed;
        SelectionRectangle.Width = 0;
        SelectionRectangle.Height = 0;
        AnnotationInk.Strokes.Clear();
        SetSelectMode();
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnSelectMode(object? sender, RoutedEventArgs e) => SetSelectMode();
    private void OnDrawMode(object? sender, RoutedEventArgs e) => SetDrawMode();

    private void SetSelectMode()
    {
        _mode = AnnotationMode.Select;
        AnnotationInk.IsHitTestVisible = false;
        AnnotationInk.EditingMode = InkCanvasEditingMode.None;
    }

    private void SetDrawMode()
    {
        _mode = AnnotationMode.Draw;
        AnnotationInk.IsHitTestVisible = true;
        AnnotationInk.EditingMode = InkCanvasEditingMode.Ink;
    }

    private bool IsFromToolbar(object source)
    {
        if (Toolbar == null) return false;
        if (source is not DependencyObject dobj) return false;
        while (dobj != null)
        {
            if (ReferenceEquals(dobj, Toolbar)) return true;
            dobj = VisualTreeHelper.GetParent(dobj);
        }
        return false;
    }

    private void OnKeyDown(object? sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
        }
        else if (e.Key == Key.Enter)
        {
            OnConfirm(this, new RoutedEventArgs());
        }
    }
}
