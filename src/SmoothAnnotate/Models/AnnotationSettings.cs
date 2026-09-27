namespace SmoothAnnotate.Models;

public class AnnotationSettings
{
    public double PenSize { get; set; } = 5.0;
    public string PenColor { get; set; } = "#FF0000";
    public double HighlighterSize { get; set; } = 20.0;
    public string HighlighterColor { get; set; } = "#FFFF00";
    public double EraserSize { get; set; } = 20.0;
    public double LaserSize { get; set; } = 8.0;
    public string LaserColor { get; set; } = "#FF0000";
    public int LaserFadeMs { get; set; } = 1500;
    public double TimerFontSize { get; set; } = 28.0;
    // Hide the toolbar (not just collapse it to a dot) whenever you aren't drawing,
    // so it never appears in OBS recordings. F8 / Ctrl+1-8 bring it back.
    public bool HideToolbarWhenIdle { get; set; } = true;
    // On a touch screen with a pen (the laptop): fingers and palms never draw.
    public bool IgnoreTouch { get; set; } = true;
    // While drawing, clicks over this window go to it, not to the canvas — the OBS
    // dashboard's remote, so its "stop drawing" button stays pressable.
    public string PassThroughWindowTitle { get; set; } = "מרכז השליטה של OBS";
}
