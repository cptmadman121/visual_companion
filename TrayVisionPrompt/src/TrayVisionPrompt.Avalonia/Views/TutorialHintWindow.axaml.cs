using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Button = Avalonia.Controls.Button;
using Control = Avalonia.Controls.Control;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;

namespace TrayVisionPrompt.Avalonia.Views;

public partial class TutorialHintWindow : Window
{
    private readonly TextBlock _titleBlock;
    private readonly TextBlock _messageBlock;
    private readonly TextBlock _stepBlock;
    private readonly Button _nextButton;
    private Control? _placementTarget;
    private IDisposable? _ownerBoundsSubscription;

    public event EventHandler? NextRequested;
    public event EventHandler? SkipRequested;

    public TutorialHintWindow()
    {
        InitializeComponent();
        _titleBlock = this.FindControl<TextBlock>("TitleBlock") ?? throw new InvalidOperationException("Title block not found");
        _messageBlock = this.FindControl<TextBlock>("MessageBlock") ?? throw new InvalidOperationException("Message block not found");
        _stepBlock = this.FindControl<TextBlock>("StepTextBlock") ?? throw new InvalidOperationException("Step block not found");
        _nextButton = this.FindControl<Button>("NextButton") ?? throw new InvalidOperationException("Next button not found");
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public void SetContent(string title, string message, string step, string primaryButtonText)
    {
        _titleBlock.Text = title;
        _messageBlock.Text = message;
        _stepBlock.Text = step;
        _nextButton.Content = primaryButtonText;
    }

    public void AttachToTarget(Control? target)
    {
        if (_placementTarget is { } oldTarget)
        {
            oldTarget.LayoutUpdated -= OnTargetLayoutUpdated;
            oldTarget.DetachedFromVisualTree -= OnTargetDetached;
        }

        _placementTarget = target;

        if (target is { })
        {
            target.LayoutUpdated += OnTargetLayoutUpdated;
            target.DetachedFromVisualTree += OnTargetDetached;
        }

        UpdatePosition();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        UpdatePosition();
        if (Owner is Window owner)
        {
            owner.PositionChanged += OnOwnerPositionChanged;
            _ownerBoundsSubscription = owner.GetObservable(BoundsProperty)
                .Subscribe(new ActionObserver<Rect>(_ => UpdatePosition()));
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (Owner is Window owner)
        {
            owner.PositionChanged -= OnOwnerPositionChanged;
        }

        _ownerBoundsSubscription?.Dispose();
        _ownerBoundsSubscription = null;
        AttachToTarget(null);
    }

    private void OnOwnerPositionChanged(object? sender, PixelPointEventArgs e) => UpdatePosition();

    private void OnTargetLayoutUpdated(object? sender, EventArgs e) => UpdatePosition();

    private void OnTargetDetached(object? sender, VisualTreeAttachmentEventArgs e) => AttachToTarget(null);

    private void UpdatePosition()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var width = Bounds.Width > 0 ? Bounds.Width : ClientSize.Width;
            var height = Bounds.Height > 0 ? Bounds.Height : ClientSize.Height;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            PixelPoint desired;
            if (_placementTarget is Control target && target.IsEffectivelyVisible)
            {
                var bounds = target.Bounds;
                var screenPoint = target.PointToScreen(new global::Avalonia.Point(bounds.Width, bounds.Height / 2));
                desired = new PixelPoint(
                    (int)Math.Round(screenPoint.X + 24d),
                    (int)Math.Round(screenPoint.Y - height / 2));
            }
            else if (Owner is Window owner)
            {
                var ownerBounds = owner.Bounds;
                desired = new PixelPoint(
                    owner.Position.X + (int)Math.Round((ownerBounds.Width - width) / 2),
                    owner.Position.Y + (int)Math.Round((ownerBounds.Height - height) / 2));
            }
            else
            {
                desired = Position;
            }

            var screen = Screens.ScreenFromPoint(desired) ?? Screens.Primary;
            if (screen is not null)
            {
                var available = screen.WorkingArea;
                var maxX = available.Right - (int)Math.Round(width);
                var maxY = available.Bottom - (int)Math.Round(height);
                var clampedX = Math.Clamp(desired.X, available.X, maxX);
                var clampedY = Math.Clamp(desired.Y, available.Y, maxY);
                Position = new PixelPoint(clampedX, clampedY);
            }
            else
            {
                Position = desired;
            }
        }, DispatcherPriority.Background);
    }

    private void OnNext(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        NextRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSkip(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        SkipRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Enter || e.Key == Key.Space)
        {
            e.Handled = true;
            NextRequested?.Invoke(this, EventArgs.Empty);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            SkipRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class ActionObserver<T> : IObserver<T>
    {
        private readonly Action<T> _onNext;

        public ActionObserver(Action<T> onNext) => _onNext = onNext;

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(T value) => _onNext(value);
    }
}
