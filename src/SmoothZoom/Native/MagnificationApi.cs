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

    // Fullscreen (kept for crash recovery fallback)
    [DllImport("magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool MagSetFullscreenTransform(float magLevel, int xOffset, int yOffset);

    [DllImport("magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool MagGetFullscreenTransform(out float magLevel, out int xOffset, out int yOffset);

    [DllImport("magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool MagShowSystemCursor([MarshalAs(UnmanagedType.Bool)] bool fShowCursor);

    // Windowed magnifier
    [DllImport("magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool MagSetWindowTransform(IntPtr hwnd, ref MAGTRANSFORM pTransform);

    [DllImport("magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool MagSetWindowSource(IntPtr hwnd, User32.RECT rect);

    [DllImport("magnification.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool MagSetWindowFilterList(IntPtr hwnd, uint dwFilterMode, int count, IntPtr pHWND);

    public const uint MW_FILTERMODE_EXCLUDE = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct MAGTRANSFORM
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
        public float[] v;

        public static MAGTRANSFORM CreateScale(float scale)
        {
            var t = new MAGTRANSFORM { v = new float[9] };
            t.v[0] = scale;  // m[0][0]
            t.v[4] = scale;  // m[1][1]
            t.v[8] = 1.0f;   // m[2][2]
            return t;
        }
    }
}
