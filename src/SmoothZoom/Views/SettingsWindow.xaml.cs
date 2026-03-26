using System.Windows;
using SmoothZoom.Models;

namespace SmoothZoom.Views;

public partial class SettingsWindow : Window
{
    public AppSettings Settings { get; private set; }
    public bool Saved { get; private set; }

    public SettingsWindow(AppSettings currentSettings)
    {
        InitializeComponent();

        Settings = currentSettings;

        // Load current values into UI
        ZoomLevelSlider.Value = currentSettings.TargetZoomLevel;
        ZoomSpeedSlider.Value = currentSettings.ZoomDurationMs;
        TrackingSlider.Value = currentSettings.CursorTrackingSpeed;
        RingSizeSlider.Value = currentSettings.HighlightRingSize;
        SetColorRadio(currentSettings.HighlightColor);
        StartWithWindowsCheckBox.IsChecked = currentSettings.StartWithWindows;
    }

    private void ZoomLevelSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ZoomLevelLabel != null)
            ZoomLevelLabel.Text = $"{e.NewValue:F1}x";
    }

    private void ZoomSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ZoomSpeedLabel != null)
            ZoomSpeedLabel.Text = $"{(int)e.NewValue}ms";
    }

    private void TrackingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TrackingLabel == null) return;

        TrackingLabel.Text = e.NewValue switch
        {
            <= 0.1f => "Tight",
            <= 0.2f => "Medium",
            <= 0.35f => "Loose",
            _ => "Very Loose"
        };
    }

    private void RingSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (RingSizeLabel != null)
            RingSizeLabel.Text = $"{(int)e.NewValue}px";
    }

    private void SetColorRadio(string color)
    {
        switch (color)
        {
            case "#DCFFE632": ColorYellow.IsChecked = true; break;
            case "#DCFF4050": ColorRed.IsChecked = true; break;
            case "#DC40C040": ColorGreen.IsChecked = true; break;
            case "#DCFFFFFF": ColorWhite.IsChecked = true; break;
            default: ColorBlue.IsChecked = true; break;
        }
    }

    private string GetSelectedColor()
    {
        if (ColorYellow.IsChecked == true) return "#DCFFE632";
        if (ColorRed.IsChecked == true) return "#DCFF4050";
        if (ColorGreen.IsChecked == true) return "#DC40C040";
        if (ColorWhite.IsChecked == true) return "#DCFFFFFF";
        return "#DC4182DC"; // Blue default
    }

    private void SaveAndClose_Click(object sender, RoutedEventArgs e)
    {
        Settings = new AppSettings
        {
            TargetZoomLevel = (float)ZoomLevelSlider.Value,
            ZoomDurationMs = (int)ZoomSpeedSlider.Value,
            CursorTrackingSpeed = (float)TrackingSlider.Value,
            HighlightRingSize = RingSizeSlider.Value,
            HighlightColor = GetSelectedColor(),
            StartWithWindows = StartWithWindowsCheckBox.IsChecked ?? true
        };
        Saved = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Saved = false;
        Close();
    }
}
