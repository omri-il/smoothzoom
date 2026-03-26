using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using SmoothAnnotate.Services;

namespace SmoothAnnotate.Views;

public partial class ToastWindow : Window
{
    public ToastWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            OverlayService.HideFromAltTab(hwnd);
            OverlayService.SetClickThrough(hwnd);
        };
    }

    public void ShowToast(string text)
    {
        ToastText.Text = text;

        // Position at center-top of primary monitor
        var screen = SystemParameters.WorkArea;
        UpdateLayout();
        Left = screen.Left + (screen.Width - ActualWidth) / 2;
        Top = screen.Top + 72;  // below the horizontal toolbar (~48px tall + 10px margin + buffer)

        ToastBorder.Opacity = 1.0;

        var fade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(1000))
        {
            BeginTime = TimeSpan.FromMilliseconds(500)
        };
        ToastBorder.BeginAnimation(OpacityProperty, fade);
    }
}
