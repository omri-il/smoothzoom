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

    private void SaveAndClose_Click(object sender, RoutedEventArgs e)
    {
        Settings = new AppSettings
        {
            TargetZoomLevel = (float)ZoomLevelSlider.Value,
            ZoomDurationMs = (int)ZoomSpeedSlider.Value,
            CursorTrackingSpeed = (float)TrackingSlider.Value,
            HighlightRingSize = RingSizeSlider.Value,
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
