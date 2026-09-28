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
    // Not drawing: false = the toolbar shrinks to a small dot (click to draw, drag to move);
    // true = it goes away completely and only F8 / the Start menu / the pen bring it back.
    // Either way no recording shows it: the toolbar window is hidden from screen capture.
    public bool HideToolbarWhenIdle { get; set; } = false;
    // On a touch screen with a pen (the laptop): a finger draws, but not while the pen is
    // near the screen, so a resting palm leaves no marks. False = a finger always draws.
    public bool IgnoreTouchNearPen { get; set; } = true;
    // While drawing, clicks over this window go to it, not to the canvas — the OBS
    // dashboard's remote, so REC / stop stay pressable while you draw.
    public string PassThroughWindowTitle { get; set; } = "מרכז השליטה של OBS";
}
