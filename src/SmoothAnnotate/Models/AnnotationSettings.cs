namespace SmoothAnnotate.Models;

public class AnnotationSettings
{
    public double PenSize { get; set; } = 3.0;
    public string PenColor { get; set; } = "#FF0000";
    public double HighlighterSize { get; set; } = 20.0;
    public string HighlighterColor { get; set; } = "#FFFF00";
    public double EraserSize { get; set; } = 20.0;
    public double LaserSize { get; set; } = 8.0;
    public string LaserColor { get; set; } = "#FF0000";
    public int LaserFadeMs { get; set; } = 1500;
    public double TimerFontSize { get; set; } = 28.0;
}
