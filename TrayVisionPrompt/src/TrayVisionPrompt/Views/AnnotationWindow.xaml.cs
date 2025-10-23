using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using TrayVisionPrompt.Models;
using TrayVisionPrompt.Services;

namespace TrayVisionPrompt.Views;

public partial class AnnotationWindow : Window
{
    private Point _startPoint;
    private bool _isSelecting;
    private readonly ScreenshotService _screenshotService;

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

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        KeyDown += OnKeyDown;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(this);
        _isSelecting = true;
        SelectionRectangle.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionRectangle, _startPoint.X);
        Canvas.SetTop(SelectionRectangle, _startPoint.Y);
        SelectionRectangle.Width = 0;
        SelectionRectangle.Height = 0;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isSelecting) return;
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

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSelecting) return;
        _isSelecting = false;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (SelectionRectangle.Visibility != Visibility.Visible || SelectionRectangle.Width < 2 || SelectionRectangle.Height < 2)
        {
            MessageBox.Show("Bitte einen Ausschnitt wählen.", "TrayVisionPrompt", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dpiScale = VisualTreeHelper.GetDpi(this);
        var bounds = new Rect(Canvas.GetLeft(SelectionRectangle), Canvas.GetTop(SelectionRectangle), SelectionRectangle.Width, SelectionRectangle.Height);
        var scaledBounds = new Rect(bounds.X * dpiScale.DpiScaleX, bounds.Y * dpiScale.DpiScaleY, bounds.Width * dpiScale.DpiScaleX, bounds.Height * dpiScale.DpiScaleY);

        var base64 = _screenshotService.CaptureRegion(scaledBounds, dpiScale.DpiScaleX);
        var width = Math.Max(1, (int)Math.Round(scaledBounds.Width));
        var height = Math.Max(1, (int)Math.Round(scaledBounds.Height));
        var annotatedBase64 = AnnotationRenderer.Render(base64, AnnotationInk.Strokes, width, height);

        CaptureResult = new CaptureResult
        {
            Bounds = scaledBounds,
            ImageBase64 = annotatedBase64,
            DisplayScaling = dpiScale.DpiScaleX
        };

        DialogResult = true;
    }

    private void OnReset(object sender, RoutedEventArgs e)
    {
        SelectionRectangle.Visibility = Visibility.Collapsed;
        AnnotationInk.Strokes.Clear();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnUndo(object sender, RoutedEventArgs e)
    {
        if (AnnotationInk.Strokes.Count > 0)
        {
            AnnotationInk.Strokes.RemoveAt(AnnotationInk.Strokes.Count - 1);
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
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
