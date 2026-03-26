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
    private float _lockedX;
    private float _lockedY;

    // Configurable settings
    public float TargetZoomLevel { get; set; } = 2.0f;
    public int ZoomDurationMs { get; set; } = 300;
    public float CursorTrackingSpeed { get; set; } = 0.15f;

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

    public void ToggleViewLock()
    {
        if (_state == ZoomState.Idle) return;

        _viewLocked = !_viewLocked;
        if (_viewLocked)
        {
            _lockedX = _smoothX;
            _lockedY = _smoothY;
        }
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
        // Capture cursor position and monitor at the moment of activation
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
            _smoothX += (cursor.X - _smoothX) * CursorTrackingSpeed;
            _smoothY += (cursor.Y - _smoothY) * CursorTrackingSpeed;
        }
        // When locked, _smoothX/_smoothY stay at _lockedX/_lockedY

        // 3. Apply transform
        _magnification.SetZoom(_currentScale, (int)_smoothX, (int)_smoothY, _activeMonitorBounds);
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
