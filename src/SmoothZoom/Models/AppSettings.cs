namespace SmoothZoom.Models;

public class AppSettings
{
    public int Version { get; set; } = 3;
    public float TargetZoomLevel { get; set; } = 2.0f;
    public int ZoomDurationMs { get; set; } = 300;
    public float CursorTrackingSpeed { get; set; } = 0.25f;
    public double HighlightRingSize { get; set; } = 70;
    public string HighlightColor { get; set; } = "#DCFFE632"; // ARGB yellow
    public double HighlightThickness { get; set; } = 4;
    public string HighlightFill { get; set; } = "#30FFE632"; // faint yellow inside the ring
    public bool ClickRipple { get; set; } = true;
    // true = the ring turns on by itself while OBS records (obs-websocket). Off since
    // settings v3: Omri switches it himself; with it off, SmoothZoom never talks to OBS.
    public bool AutoRingWhileRecording { get; set; } = false;
    public bool StartWithWindows { get; set; } = true;
}
