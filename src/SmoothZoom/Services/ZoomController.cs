using System.Windows.Threading;
using SmoothZoom.Native;

namespace SmoothZoom.Services;

public class ZoomController : IDisposable
{
    private readonly MagnificationService _magnification;
    private readonly DispatcherTimer _timer;
    private readonly Action<bool> _onZoomStateChanged;

    // State machine
    private enum ZoomState { Idle, ZoomingIn, Zoomed, ZoomingOut }
    private ZoomState _state = ZoomState.Idle;

    // Animation parameters
    private float _currentScale = 1.0f;
    private float _startScale;
    private float _targetScale;
    private DateTime _animationStart;
    private TimeSpan _animationDuration;

    // Cursor tracking (smoothed)
    private float _smoothX;
    private float _smoothY;
    private User32.RECT _activeMonitorBounds;

    // View lock
    private bool _viewLocked;

    // Configurable settings
    public float TargetZoomLevel { get; set; } = 2.0f;
    public int ZoomDurationMs { get; set; } = 300;
    public float CursorTrackingSpeed { get; set; } = 0.15f;

    // Zoom adjustment limits
    private const float MinZoom = 1.1f;
    private const float MaxZoom = 6.0f;
    private const float ZoomStep = 0.25f;

    public bool IsZoomed => _state != ZoomState.Idle;

    public ZoomController(MagnificationService magnification, Action<bool> onZoomStateChanged)
    {
        _magnification = magnification;
        _onZoomStateChanged = onZoomStateChanged;

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60fps
        };
        _timer.Tick += OnTimerTick;
    }

    public void Toggle()
    {
        if (_state == ZoomState.Idle || _state == ZoomState.ZoomingOut)
            StartZoomIn();
        else
            StartZoomOut();
    }

    public void AdjustZoom(int direction)
    {
        // Only adjust while zoomed or zooming in
        if (_state == ZoomState.Idle) return;

        float newTarget = TargetZoomLevel + (direction * ZoomStep);
        newTarget = Math.Clamp(newTarget, MinZoom, MaxZoom);
        // Round to nearest 0.25 step
        newTarget = MathF.Round(newTarget / ZoomStep) * ZoomStep;

        TargetZoomLevel = newTarget;

        // If fully zoomed, smoothly animate to the new level
        if (_state == ZoomState.Zoomed)
        {
            _startScale = _currentScale;
            _targetScale = newTarget;
            _animationStart = DateTime.UtcNow;
            _animationDuration = TimeSpan.FromMilliseconds(150); // Quick adjustment
            _state = ZoomState.ZoomingIn;
        }
        else if (_state == ZoomState.ZoomingIn)
        {
            // Update target mid-animation
            _targetScale = newTarget;
        }
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
        _viewLocked = false;
        _timer.Stop();
        _magnification.Reset();
        _onZoomStateChanged(false);
    }

    private void StartZoomIn()
    {
        User32.GetCursorPos(out var cursor);
        _activeMonitorBounds = MagnificationService.GetMonitorBounds(cursor.X, cursor.Y);
        _smoothX = cursor.X;
        _smoothY = cursor.Y;
        _viewLocked = false;

        _startScale = _currentScale;
        _targetScale = TargetZoomLevel;
        _animationStart = DateTime.UtcNow;
        _animationDuration = TimeSpan.FromMilliseconds(ZoomDurationMs);
        _state = ZoomState.ZoomingIn;

        _timer.Start();
        _onZoomStateChanged(true);
    }

    private void StartZoomOut()
    {
        _startScale = _currentScale;
        _targetScale = 1.0f;
        _animationStart = DateTime.UtcNow;
        _animationDuration = TimeSpan.FromMilliseconds(ZoomDurationMs);
        _state = ZoomState.ZoomingOut;
        _viewLocked = false;

        _onZoomStateChanged(false);
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        // 1. Update zoom animation
        if (_state == ZoomState.ZoomingIn || _state == ZoomState.ZoomingOut)
        {
            float elapsed = (float)(DateTime.UtcNow - _animationStart).TotalMilliseconds;
            float t = Math.Clamp(elapsed / (float)_animationDuration.TotalMilliseconds, 0f, 1f);
            float eased = CubicEaseInOut(t);
            _currentScale = _startScale + (_targetScale - _startScale) * eased;

            if (t >= 1.0f)
            {
                _currentScale = _targetScale;
                if (_targetScale > 1.0f)
                {
                    _state = ZoomState.Zoomed;
                }
                else
                {
                    _state = ZoomState.Idle;
                    _magnification.Reset();
                    _timer.Stop();
                    return;
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

            // Adaptive tracking: snap to exact position when cursor is nearly still
            // This eliminates sub-pixel jitter that causes blur
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

        // 3. Apply transform with rounded coordinates
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
