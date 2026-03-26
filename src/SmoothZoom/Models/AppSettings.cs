namespace SmoothZoom.Models;

public class AppSettings
{
    public float TargetZoomLevel { get; set; } = 2.0f;
    public int ZoomDurationMs { get; set; } = 300;
    public float CursorTrackingSpeed { get; set; } = 0.15f;
    public double HighlightRingSize { get; set; } = 40;
    public bool StartWithWindows { get; set; } = true;
}
