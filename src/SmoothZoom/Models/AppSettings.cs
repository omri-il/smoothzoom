namespace SmoothZoom.Models;

public class AppSettings
{
    public int Version { get; set; } = 2;
    public float TargetZoomLevel { get; set; } = 2.0f;
    public int ZoomDurationMs { get; set; } = 300;
    public float CursorTrackingSpeed { get; set; } = 0.25f;
    public double HighlightRingSize { get; set; } = 80;
    public string HighlightColor { get; set; } = "#DC4182DC"; // ARGB blue
    public bool StartWithWindows { get; set; } = true;
}
