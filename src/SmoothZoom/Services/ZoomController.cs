using System.Windows.Threading;
using SmoothZoom.Native;

namespace SmoothZoom.Services;

public class ZoomController : IDisposable
{
    private readonly MagnificationService _magnification;
    private readonly DispatcherTimer _timer;
    private readonly Action<bool> _onZoomStateChanged;

    // State machine
    private enum ZoomState { Idle, Animating, Zoomed }
    private ZoomState _state = ZoomState.Idle;

    // Animation parameters
    private float _currentScale = 1.0f;
    private float _startScale;
    private float _targetScale = 1.0f;
    private DateTime _animationStart;
    private TimeSpan _animationDuration;

    // Cursor tracking (smoothed)
    private float _smoothX;
    private float _smoothY;
    private User32.RECT _activeMonitorBounds;
    private bool _monitorCaptured;

    // View lock
    private bool _viewLocked;

    // Configurable settings
    public float TargetZoomLevel { get; set; } = 2.0f;
    public int ZoomDurationMs { get; set; } = 300;
    public float CursorTrackingSpeed { get; set; } = 0.25f;

    // Zoom limits
    private const float MinZoom = 1.0f;
    private const float MaxZoom = 6.0f;
    private const float ZoomStep = 0.25f;
    private const int ScrollAnimationMs = 150;

    public bool IsZoomed => _state != ZoomState.Idle;

    public ZoomController(MagnificationService magnification, Action<bool> onZoomStateChanged)
    {
        _magnification = magnification;
        _onZoomStateChanged = onZoomStateChanged;

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(8) // ~120fps for smoother panning
        };
        _timer.Tick += OnTimerTick;
    }

    /// <summary>
    /// Toggle zoom: if idle, zoom to TargetZoomLevel. If zoomed, zoom back to 1x.
    /// </summary>
    public void Toggle()
    {
        if (_state == ZoomState.Idle)
            AnimateTo(TargetZoomLevel, ZoomDurationMs);
        else
            AnimateTo(1.0f, ZoomDurationMs);
    }

    /// <summary>
    /// Scroll to zoom: each notch adds/removes ZoomStep.
    /// Scrolling up from idle starts the zoom. Scrolling down to 1.0x exits zoom.
    /// </summary>
    public void ScrollZoom(int direction)
    {
        float newTarget = _targetScale + (direction * ZoomStep);
        newTarget = MathF.Round(newTarget / ZoomStep) * ZoomStep;
        newTarget = Math.Clamp(newTarget, MinZoom, MaxZoom);

        if (newTarget <= 1.0f)
        {
            AnimateTo(1.0f, ScrollAnimationMs);
            return;
        }

        // If already zoomed/animating, just update the target — don't restart animation
        if (_state == ZoomState.Zoomed || _state == ZoomState.Animating)
        {
            _targetScale = newTarget;
            if (_state == ZoomState.Zoomed)
            {
                // Kick into animating mode with current scale as start
                _startScale = _currentScale;
                _animationStart = DateTime.UtcNow;
                _animationDuration = TimeSpan.FromMilliseconds(ScrollAnimationMs);
                _state = ZoomState.Animating;
            }
            // If already animating, just changing _targetScale is enough —
            // the tick loop will interpolate toward the new target
            return;
        }

        // First scroll from idle — full activation
        AnimateTo(newTarget, ScrollAnimationMs);
    }

    public void ToggleViewLock()
    {
        if (_state == ZoomState.Idle) return;
        _viewLocked = !_viewLocked;
    }

    public void PanicReset()
    {
        _state = ZoomState.Idle;
        _currentScale = 1.0f;
        _targetScale = 1.0f;
        _viewLocked = false;
        _monitorCaptured = false;
        _timer.Stop();
        _magnification.Reset();
        _onZoomStateChanged(false);
    }

    private void AnimateTo(float target, int durationMs)
    {
        // Capture monitor on first zoom activation
        if (!_monitorCaptured)
        {
            User32.GetCursorPos(out var cursor);
            _activeMonitorBounds = MagnificationService.GetMonitorBounds(cursor.X, cursor.Y);
            _smoothX = cursor.X;
            _smoothY = cursor.Y;
            _monitorCaptured = true;
        }

        _startScale = _currentScale;
        _targetScale = target;
        _animationStart = DateTime.UtcNow;
        _animationDuration = TimeSpan.FromMilliseconds(durationMs);
        _state = ZoomState.Animating;
        _viewLocked = false;

        _timer.Start();
        _onZoomStateChanged(target > 1.0f);
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        // 1. Update zoom animation
        if (_state == ZoomState.Animating)
        {
            float elapsed = (float)(DateTime.UtcNow - _animationStart).TotalMilliseconds;
            float t = Math.Clamp(elapsed / (float)_animationDuration.TotalMilliseconds, 0f, 1f);
            float eased = CubicEaseInOut(t);
            _currentScale = _startScale + (_targetScale - _startScale) * eased;

            if (t >= 1.0f)
            {
                _currentScale = _targetScale;
                if (_targetScale <= 1.0f)
                {
                    // Fully zoomed out — go idle
                    _state = ZoomState.Idle;
                    _monitorCaptured = false;
                    _magnification.Reset();
                    _timer.Stop();
                    return;
                }
                else
                {
                    _state = ZoomState.Zoomed;
                }
            }
        }

        // 2. Smooth cursor tracking (unless view is locked)
        User32.GetCursorPos(out var cursor);
        if (!_viewLocked)
        {
            float dx = cursor.X - _smoothX;
            float dy = cursor.Y - _smoothY;
            float distance = MathF.Sqrt(dx * dx + dy * dy);

            if (distance < 1.5f)
            {
                _smoothX = cursor.X;
                _smoothY = cursor.Y;
            }
            else
            {
                _smoothX += dx * CursorTrackingSpeed;
                _smoothY += dy * CursorTrackingSpeed;
            }
        }

        // 3. Apply transform
        _magnification.SetZoom(_currentScale,
            (int)MathF.Round(_smoothX),
            (int)MathF.Round(_smoothY),
            _activeMonitorBounds);
    }

    private static float CubicEaseInOut(float t)
    {
        if (t < 0.5f)
            return 4f * t * t * t;
        else
        {
            float f = (2f * t) - 2f;
            return 0.5f * f * f * f + 1f;
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _magnification.Reset();
    }
}
