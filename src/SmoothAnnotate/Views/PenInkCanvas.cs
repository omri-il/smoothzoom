using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Input.StylusPlugIns;

namespace SmoothAnnotate.Views;

/// <summary>
/// An InkCanvas that can ignore finger and palm touches, so on a touch screen with a
/// pen (the laptop, an HP OmniBook Ultra Flip) only the pen — and the mouse — draw.
///
/// Two places have to agree: the routed stylus events (marking a touch handled stops
/// the stroke from being collected), and the DynamicRenderer, which draws live ink on
/// WPF's pen thread before those events arrive — without the filter a finger would
/// still leave a trail while it moves, which then vanishes.
/// </summary>
public class PenInkCanvas : InkCanvas
{
    private readonly TouchFilterRenderer _renderer = new();

    public PenInkCanvas()
    {
        DynamicRenderer = _renderer;
        Loaded += (_, _) => RefreshTouchDevices();
    }

    public bool IgnoreTouch
    {
        get => _renderer.IgnoreTouch;
        set => _renderer.IgnoreTouch = value;
    }

    /// <summary>The pen thread can't query tablet devices itself, so the ids of the
    /// touch digitizers are collected here, on the UI thread.</summary>
    public void RefreshTouchDevices()
    {
        var ids = new HashSet<int>();
        foreach (TabletDevice tablet in Tablet.TabletDevices)
            if (tablet.Type == TabletDeviceType.Touch) ids.Add(tablet.Id);
        _renderer.TouchTabletIds = ids;
    }

    private bool IsTouch(StylusEventArgs e) =>
        IgnoreTouch && e.StylusDevice?.TabletDevice?.Type == TabletDeviceType.Touch;

    protected override void OnPreviewStylusDown(StylusDownEventArgs e)
    {
        if (IsTouch(e)) { e.Handled = true; return; }
        base.OnPreviewStylusDown(e);
    }

    protected override void OnPreviewStylusMove(StylusEventArgs e)
    {
        if (IsTouch(e)) { e.Handled = true; return; }
        base.OnPreviewStylusMove(e);
    }

    protected override void OnPreviewStylusUp(StylusEventArgs e)
    {
        if (IsTouch(e)) { e.Handled = true; return; }
        base.OnPreviewStylusUp(e);
    }

    private sealed class TouchFilterRenderer : DynamicRenderer
    {
        public volatile bool IgnoreTouch = true;
        public volatile HashSet<int> TouchTabletIds = new();

        private bool Skip(RawStylusInput input) =>
            IgnoreTouch && TouchTabletIds.Contains(input.TabletDeviceId);

        protected override void OnStylusDown(RawStylusInput rawStylusInput)
        {
            if (!Skip(rawStylusInput)) base.OnStylusDown(rawStylusInput);
        }

        protected override void OnStylusMove(RawStylusInput rawStylusInput)
        {
            if (!Skip(rawStylusInput)) base.OnStylusMove(rawStylusInput);
        }

        protected override void OnStylusUp(RawStylusInput rawStylusInput)
        {
            if (!Skip(rawStylusInput)) base.OnStylusUp(rawStylusInput);
        }
    }
}
