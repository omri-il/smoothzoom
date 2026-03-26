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

    // Middle-click drag tracking
    private bool _middleDragging;
    private int _dragStartCursorX, _dragStartCursorY;
    private float _dragStartViewX, _dragStartViewY;

    // Settings
    public float TargetZoomLevel { get; set; } = 2.0f;
    public int ZoomDurationMs { get; set; } = 300;
    public float CursorTrackingSpeed { get; set; } = 0.25f;

    // Constants
    private const float MinZoom = 1.25f;
    private const float MaxZoom = 6.0f;
    private const float ZoomStep = 0.25f;
    private const int AdjustAnimationMs = 200;

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

    /// <summary>Ctrl+Alt+Z — toggle zoom to preset level</summary>
    public void Toggle()
    {
        if (_state == ZoomState.Idle)
            AnimateTo(TargetZoomLevel, ZoomDurationMs);
        else
            AnimateTo(1.0f, ZoomDurationMs);
    }

    /// <summary>Ctrl+Alt+Plus — zoom in one step</summary>
    public void ZoomInStep()
    {
        if (_state == ZoomState.Idle)
        {
            // First press: zoom to minimum level
            AnimateTo(MinZoom, AdjustAnimationMs);
            return;
        }

        float newTarget = _targetScale + ZoomStep;
        newTarget = MathF.Round(newTarget / ZoomStep) * ZoomStep;
        newTarget = Math.Clamp(newTarget, MinZoom, MaxZoom);

        _startScale = _currentScale;
        _targetScale = newTarget;
        _animationStart = DateTime.UtcNow;
        _animationDuration = TimeSpan.FromMilliseconds(AdjustAnimationMs);
        _state = ZoomState.Animating;
    }

    /// <summary>Ctrl+Alt+Minus — zoom out one step</summary>
    public void ZoomOutStep()
    {
        if (_state == ZoomState.Idle) return;

        float newTarget = _targetScale - ZoomStep;
        newTarget = MathF.Round(newTarget / ZoomStep) * ZoomStep;

        if (newTarget < MinZoom)
        {
            AnimateTo(1.0f, AdjustAnimationMs);
            return;
        }

        _startScale = _currentScale;
        _targetScale = newTarget;
        _animationStart = DateTime.UtcNow;
        _animationDuration = TimeSpan.FromMilliseconds(AdjustAnimationMs);
        _state = ZoomState.Animating;
    }

    public void ToggleViewLock()
    {
        if (_state == ZoomState.Idle) return;
        _viewLocked = !_viewLocked;
    }

    /// <summary>
    /// Middle-click drag: viewport tracks cursor 1:1 from drag start point.
    /// No initial jump — uses delta from where drag started.
    /// </summary>
    public void SetMiddleDragging(bool dragging)
    {
        if (dragging && !_middleDragging && _state != ZoomState.Idle)
        {
            // Capture start positions
            User32.GetCursorPos(out var cursor);
            _dragStartCursorX = cursor.X;
            _dragStartCursorY = cursor.Y;
            _dragStartViewX = _smoothX;
            _dragStartViewY = _smoothY;
        }
        _middleDragging = dragging;
    }

    public void PanicReset()
    {
        _state = ZoomState.Idle;
        _currentScale = 1.0f;
        _targetScale = 1.0f;
        _viewLocked = true;
        _middleDragging = false;
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

        // 2. Viewport movement
        User32.GetCursorPos(out var cursor);

        if (_middleDragging)
        {
            // 1:1 grab-and-drag: viewport moves by exact delta from drag start
            _smoothX = _dragStartViewX + (cursor.X - _dragStartCursorX);
            _smoothY = _dragStartViewY + (cursor.Y - _dragStartCursorY);
        }
        else if (!_viewLocked)
        {
            // Smooth cursor tracking
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
