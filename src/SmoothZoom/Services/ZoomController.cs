using System.Windows.Threading;
using SmoothZoom.Native;

namespace SmoothZoom.Services;

public class ZoomController : IDisposable
{
    private readonly MagnificationService _magnification;
    private readonly DispatcherTimer _timer;
    private readonly Action<bool, User32.RECT> _onZoomStateChanged;

    private enum ZoomState { Idle, Animating, Zoomed }
    private ZoomState _state = ZoomState.Idle;

    // Animation
    private float _currentScale = 1.0f;
    private float _startScale;
    private float _targetScale = 1.0f;
    private DateTime _animationStart;
    private TimeSpan _animationDuration;

    // Viewport position
    private float _smoothX;
    private float _smoothY;
    private User32.RECT _activeMonitorBounds;
    private bool _monitorCaptured;

    // View lock (default ON)
    private bool _viewLocked = true;
    private bool _middleDragging; // Temporary cursor tracking while middle button held

    // Settings
    public float TargetZoomLevel { get; set; } = 2.0f;
    public int ZoomDurationMs { get; set; } = 300;
    public float CursorTrackingSpeed { get; set; } = 0.25f;

    // Constants
    private const float MinZoom = 1.0f;
    private const float MaxZoom = 6.0f;
    private const float ZoomStep = 0.25f;
    private const int ScrollAnimationMs = 200;

    public bool IsZoomed => _state != ZoomState.Idle;

    public ZoomController(MagnificationService magnification, Action<bool, User32.RECT> onZoomStateChanged)
    {
        _magnification = magnification;
        _onZoomStateChanged = onZoomStateChanged;

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(8)
        };
        _timer.Tick += OnTimerTick;
    }

    public void Toggle()
    {
        if (_state == ZoomState.Idle)
            AnimateTo(TargetZoomLevel, ZoomDurationMs);
        else
            AnimateTo(1.0f, ZoomDurationMs);
    }

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

        if (!_monitorCaptured)
        {
            User32.GetCursorPos(out var cursor);
            _activeMonitorBounds = MagnificationService.GetMonitorBounds(cursor.X, cursor.Y);
            _smoothX = cursor.X;
            _smoothY = cursor.Y;
            _monitorCaptured = true;
            _viewLocked = true;
        }

        _startScale = _currentScale;
        _targetScale = newTarget;
        _animationStart = DateTime.UtcNow;
        _animationDuration = TimeSpan.FromMilliseconds(ScrollAnimationMs);
        _state = ZoomState.Animating;
        _timer.Start();
        _onZoomStateChanged(true, _activeMonitorBounds);
    }

    public void ToggleViewLock()
    {
        if (_state == ZoomState.Idle) return;
        _viewLocked = !_viewLocked;
    }

    /// <summary>
    /// Middle-click drag: while held, viewport follows cursor (temporary tracking).
    /// On release, viewport locks at current position.
    /// </summary>
    public void SetMiddleDragging(bool dragging)
    {
        _middleDragging = dragging;
    }

    public void PanicReset()
    {
        _state = ZoomState.Idle;
        _currentScale = 1.0f;
        _targetScale = 1.0f;
        _viewLocked = true;
        _monitorCaptured = false;
        _timer.Stop();
        _magnification.Reset();
        _onZoomStateChanged(false, _activeMonitorBounds);
    }

    private void AnimateTo(float target, int durationMs)
    {
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
        _viewLocked = true;

        _timer.Start();
        _onZoomStateChanged(target > 1.0f, _activeMonitorBounds);
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

        // 2. Cursor tracking — active when view unlocked OR middle-dragging
        User32.GetCursorPos(out var cursor);
        if (!_viewLocked || _middleDragging)
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
