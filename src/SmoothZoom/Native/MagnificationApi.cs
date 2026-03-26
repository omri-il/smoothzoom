using System.Runtime.InteropServices;

namespace SmoothZoom.Native;

public static class MagnificationApi
{
    [DllImport("magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool MagInitialize();

    [DllImport("magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool MagUninitialize();

    [DllImport("magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool MagSetFullscreenTransform(float magLevel, int xOffset, int yOffset);

    [DllImport("magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool MagGetFullscreenTransform(out float magLevel, out int xOffset, out int yOffset);

    [DllImport("magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool MagShowSystemCursor([MarshalAs(UnmanagedType.Bool)] bool fShowCursor);
}
