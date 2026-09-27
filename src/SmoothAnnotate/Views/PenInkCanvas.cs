using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Input.StylusPlugIns;

namespace SmoothAnnotate.Views;

/// <summary>
/// An InkCanvas where a finger draws like the mouse, except while the pen is near the
/// screen. On a touch screen with a pen (the laptop, an HP OmniBook Ultra Flip) a palm
/// resting on the glass while you write then leaves no marks.
///
/// "Near" = the pen is in range over the overlay, or was less than PenGraceMs ago. The pen
/// is watched on the whole window, not just this canvas: a shape tool puts ShapeCanvas on
/// top, and the pen's events then never reach here.
///
/// Each touch is judged once, at touch-down, so a palm stays ignored for its whole
/// contact even if the pen moves away meanwhile. Two places make that call and must
/// agree: the routed stylus events (marking a touch handled stops the stroke from being
/// collected), and the DynamicRenderer, which draws live ink on WPF's pen thread BEFORE
/// those events. Without its filter a palm would still leave a trail while it moves,
/// which then vanishes.
/// </summary>
public class PenInkCanvas : InkCanvas
{
    // A palm lifts a moment after the pen does
    private const long PenGraceMs = 1000;

    private readonly TouchFilterRenderer _renderer;
    private volatile bool _ignoreTouchNearPen = true;
    private volatile bool _penInRange;
    private long _penSeenAt = long.MinValue / 2; // Environment.TickCount64; read on the pen thread too
    private bool _watchingPen;

    // UI thread: per touch contact (stylus device id), ignored or not
    private readonly Dictionary<int, bool> _ignored = new();

    public PenInkCanvas()
    {
        _renderer = new TouchFilterRenderer(this);
        DynamicRenderer = _renderer;
        Loaded += (_, _) =>
        {
            RefreshTouchDevices();
            WatchPen();
        };
    }

    /// <summary>False = a finger always draws, pen or no pen.</summary>
    public bool IgnoreTouchNearPen
    {
        get => _ignoreTouchNearPen;
        set => _ignoreTouchNearPen = value;
    }

    /// <summary>True while a touch should be ignored because the pen is near. Plain fields
    /// only, so the pen thread can ask too.</summary>
    public bool PenIsNear =>
        _ignoreTouchNearPen
        && (_penInRange || Environment.TickCount64 - Interlocked.Read(ref _penSeenAt) < PenGraceMs);

    /// <summary>The pen thread can't query tablet devices itself, so the ids of the
    /// touch digitizers are collected here, on the UI thread.</summary>
    public void RefreshTouchDevices()
    {
        var ids = new HashSet<int>();
        foreach (TabletDevice tablet in Tablet.TabletDevices)
            if (tablet.Type == TabletDeviceType.Touch) ids.Add(tablet.Id);
        _renderer.TouchTabletIds = ids;
    }

    // --- Where the pen is ---

    private void WatchPen()
    {
        if (_watchingPen || Window.GetWindow(this) is not { } window) return;
        _watchingPen = true;
        // handledEventsToo: InkCanvas handles the pen's own input
        window.AddHandler(Stylus.PreviewStylusInRangeEvent, new StylusEventHandler(OnPenHere), true);
        window.AddHandler(Stylus.PreviewStylusInAirMoveEvent, new StylusEventHandler(OnPenHere), true);
        window.AddHandler(Stylus.PreviewStylusMoveEvent, new StylusEventHandler(OnPenHere), true);
        window.AddHandler(Stylus.PreviewStylusDownEvent, new StylusDownEventHandler(OnPenHere), true);
        window.AddHandler(Stylus.PreviewStylusOutOfRangeEvent, new StylusEventHandler(OnPenGone), true);
        // Onto another window (the toolbar) its out-of-range goes there, so leaving counts as gone
        window.StylusEnter += OnPenHere;
        window.StylusLeave += OnPenGone;
    }

    private static bool IsPen(StylusEventArgs e) =>
        e.StylusDevice?.TabletDevice?.Type == TabletDeviceType.Stylus;

    private void OnPenHere(object sender, StylusEventArgs e)
    {
        if (!IsPen(e)) return;
        _penInRange = true;
        Interlocked.Exchange(ref _penSeenAt, Environment.TickCount64);
    }

    private void OnPenGone(object sender, StylusEventArgs e)
    {
        if (!IsPen(e)) return;
        _penInRange = false;
        Interlocked.Exchange(ref _penSeenAt, Environment.TickCount64);
    }

    // --- Routed events ---

    private bool Ignore(StylusEventArgs e, bool down = false)
    {
        if (e.StylusDevice is not { } device || device.TabletDevice?.Type != TabletDeviceType.Touch)
            return false;
        if (down)
        {
            _ignored[device.Id] = PenIsNear;
            // Only the ignored ones: "my finger doesn't draw" should be answerable from the log
            if (_ignored[device.Id])
                App.Log($"touch ignored, pen near (in range: {_penInRange}, "
                        + $"last seen {Environment.TickCount64 - Interlocked.Read(ref _penSeenAt)} ms ago)");
        }
        return _ignored.TryGetValue(device.Id, out var ignore) && ignore;
    }

    protected override void OnPreviewStylusDown(StylusDownEventArgs e)
    {
        if (Ignore(e, down: true)) { e.Handled = true; return; }
        base.OnPreviewStylusDown(e);
    }

    protected override void OnPreviewStylusMove(StylusEventArgs e)
    {
        if (Ignore(e)) { e.Handled = true; return; }
        base.OnPreviewStylusMove(e);
    }

    protected override void OnPreviewStylusUp(StylusEventArgs e)
    {
        if (Ignore(e)) { e.Handled = true; return; }
        base.OnPreviewStylusUp(e);
    }

    // --- Pen thread ---

    private sealed class TouchFilterRenderer : DynamicRenderer
    {
        private readonly PenInkCanvas _owner;
        public volatile HashSet<int> TouchTabletIds = new();
        // Pen thread only: per touch contact, ignored or not
        private readonly Dictionary<int, bool> _ignored = new();

        public TouchFilterRenderer(PenInkCanvas owner) => _owner = owner;

        private bool Ignore(RawStylusInput input, bool down = false)
        {
            if (!TouchTabletIds.Contains(input.TabletDeviceId)) return false;
            if (down) _ignored[input.StylusDeviceId] = _owner.PenIsNear;
            return _ignored.TryGetValue(input.StylusDeviceId, out var ignore) && ignore;
        }

        protected override void OnStylusDown(RawStylusInput rawStylusInput)
        {
            if (!Ignore(rawStylusInput, down: true)) base.OnStylusDown(rawStylusInput);
        }

        protected override void OnStylusMove(RawStylusInput rawStylusInput)
        {
            if (!Ignore(rawStylusInput)) base.OnStylusMove(rawStylusInput);
        }

        protected override void OnStylusUp(RawStylusInput rawStylusInput)
        {
            if (!Ignore(rawStylusInput)) base.OnStylusUp(rawStylusInput);
        }
    }
}
