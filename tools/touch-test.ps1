# Checks SmoothAnnotate's touch, palm and exit paths WITHOUT hands: drives the RUNNING
# app through its control pipe, with simulated finger strokes (InjectTouchInput), a
# simulated pen (CreateSyntheticPointerDevice) and simulated keys, then looks at the
# screen for red ink. It takes over the screen for ~30 s, so tell Omri before running it.
#
#   powershell -ExecutionPolicy Bypass -File tools\touch-test.ps1 [-Steps finger,palm,laser,esc,toolbar]
#
# Quirks it works around (CLAUDE.md -> "Testing touch without hands"):
# - WPF doesn't see the injected-touch device or the synthetic pen until its device list
#   refreshes, a few seconds after the synthetic pen is created. Until then every
#   injected touch is silently dropped, hence the warm-up that strokes until one draws.
# - Ink drawn over ink already there can't be measured, so the canvas is cleared before
#   every measured stroke.
# - Long, fast, diagonal injected strokes were sometimes lost before reaching WPF. Short
#   horizontal ones never were, so all strokes here are short and horizontal.
param([string]$Steps = 'finger,palm,laser,esc,toolbar')

$ErrorActionPreference = 'Stop'
$StepList = @($Steps -split ',' | ForEach-Object { $_.Trim() })   # a list; $Steps is typed [string]
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Threading;

public static class Inj {
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    public struct POINTER_INFO {
        public uint pointerType, pointerId, frameId, pointerFlags;
        public IntPtr sourceDevice, hwndTarget;
        public POINT ptPixelLocation, ptHimetricLocation, ptPixelLocationRaw, ptHimetricLocationRaw;
        public uint dwTime, historyCount; public int InputData; public uint dwKeyStates;
        public ulong PerformanceCount; public int ButtonChangeType;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct POINTER_TOUCH_INFO {
        public POINTER_INFO pointerInfo; public uint touchFlags, touchMask;
        public RECT rcContact, rcContactRaw; public uint orientation, pressure;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct POINTER_PEN_INFO {
        public POINTER_INFO pointerInfo; public uint penFlags, penMask, pressure, rotation;
        public int tiltX, tiltY;
    }
    [StructLayout(LayoutKind.Explicit)]
    public struct POINTER_TYPE_INFO {
        [FieldOffset(0)] public uint type;
        [FieldOffset(8)] public POINTER_TOUCH_INFO touchInfo;
        [FieldOffset(8)] public POINTER_PEN_INFO penInfo;
    }

    [DllImport("user32.dll", SetLastError = true)] static extern bool InitializeTouchInjection(uint max, uint mode);
    [DllImport("user32.dll", SetLastError = true)] static extern bool InjectTouchInput(uint count, POINTER_TOUCH_INFO[] c);
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr CreateSyntheticPointerDevice(uint type, uint max, uint mode);
    [DllImport("user32.dll", SetLastError = true)] static extern bool InjectSyntheticPointerInput(IntPtr dev, POINTER_TYPE_INFO[] info, uint count);
    [DllImport("user32.dll")] static extern void DestroySyntheticPointerDevice(IntPtr dev);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

    const uint DOWN = 0x10000, UPDATE = 0x20000, UP = 0x40000, INRANGE = 0x2, INCONTACT = 0x4, NEW = 0x1;
    static bool touchReady;
    static IntPtr pen;

    static void Touch(int x, int y, uint flags) {
        if (!touchReady) {
            if (!InitializeTouchInjection(1, 3)) throw new Exception("InitializeTouchInjection " + Marshal.GetLastWin32Error());
            touchReady = true;
        }
        var c = new POINTER_TOUCH_INFO();
        c.pointerInfo.pointerType = 2; // PT_TOUCH
        c.pointerInfo.pointerFlags = flags;
        c.pointerInfo.ptPixelLocation.X = x; c.pointerInfo.ptPixelLocation.Y = y;
        c.touchMask = 7; // contact area | orientation | pressure
        c.rcContact.Left = x - 3; c.rcContact.Right = x + 3; c.rcContact.Top = y - 3; c.rcContact.Bottom = y + 3;
        c.orientation = 90; c.pressure = 32000;
        if (!InjectTouchInput(1, new[] { c })) throw new Exception("InjectTouchInput " + Marshal.GetLastWin32Error());
    }

    public static void Drag(int x1, int y1, int x2, int y2, int steps) {
        Touch(x1, y1, DOWN | INRANGE | INCONTACT);
        for (int i = 1; i <= steps; i++) {
            Thread.Sleep(12);
            Touch(x1 + (x2 - x1) * i / steps, y1 + (y2 - y1) * i / steps, UPDATE | INRANGE | INCONTACT);
        }
        Thread.Sleep(12);
        Touch(x2, y2, UP);
    }

    public static void Tap(int x, int y) {
        Touch(x, y, DOWN | INRANGE | INCONTACT);
        Thread.Sleep(60);
        Touch(x, y, UP);
    }

    static POINTER_TYPE_INFO PenFrame(int x, int y, uint flags) {
        var p = new POINTER_TYPE_INFO();
        p.type = 3; // PT_PEN
        p.penInfo.pointerInfo.pointerType = 3;
        p.penInfo.pointerInfo.pointerFlags = flags;
        p.penInfo.pointerInfo.ptPixelLocation.X = x; p.penInfo.pointerInfo.ptPixelLocation.Y = y;
        return p;
    }

    // The pen hovering (in range, not touching) at (x, y) for ms
    public static void PenHover(int x, int y, int ms) {
        if (pen == IntPtr.Zero) {
            pen = CreateSyntheticPointerDevice(3, 1, 1);
            if (pen == IntPtr.Zero) throw new Exception("CreateSyntheticPointerDevice " + Marshal.GetLastWin32Error());
        }
        var end = DateTime.UtcNow.AddMilliseconds(ms);
        bool first = true;
        while (DateTime.UtcNow < end) {
            if (!InjectSyntheticPointerInput(pen, new[] { PenFrame(x, y, INRANGE | (first ? NEW : UPDATE)) }, 1))
                throw new Exception("InjectSyntheticPointerInput " + Marshal.GetLastWin32Error());
            first = false; x += 1;
            Thread.Sleep(15);
        }
    }
    // An exception on this thread would kill the whole PowerShell process, results and all
    public static string PenError;
    public static Thread PenHoverAsync(int x, int y, int ms) {
        PenError = null;
        var t = new Thread(() => { try { PenHover(x, y, ms); } catch (Exception e) { PenError = e.Message; } });
        t.IsBackground = true; t.Start(); return t;
    }
    public static void PenAway(int x, int y) {
        if (pen != IntPtr.Zero) InjectSyntheticPointerInput(pen, new[] { PenFrame(x, y, UPDATE) }, 1); // no INRANGE = left
    }
    public static void PenDestroy() { if (pen != IntPtr.Zero) { DestroySyntheticPointerDevice(pen); pen = IntPtr.Zero; } }

    public static void Key(byte vk) {
        keybd_event(vk, 0, 0, UIntPtr.Zero);
        Thread.Sleep(30);
        keybd_event(vk, 0, 2, UIntPtr.Zero);
    }
}
'@

[void][Inj]::SetProcessDpiAwarenessContext([IntPtr](-4))   # per-monitor v2: real pixels
$SW = [Inj]::GetSystemMetrics(0); $SH = [Inj]::GetSystemMetrics(1)

function Pipe([string]$cmd) {
    $p = New-Object System.IO.Pipes.NamedPipeClientStream('.', 'SmoothAnnotate.control', [System.IO.Pipes.PipeDirection]::InOut)
    try {
        $p.Connect(2000)
        $sw = New-Object System.IO.StreamWriter($p); $sw.AutoFlush = $true
        $sr = New-Object System.IO.StreamReader($p)
        $sw.WriteLine($cmd)
        return ($sr.ReadLine() | ConvertFrom-Json)
    } finally { $p.Dispose() }
}

function DrawOn { [void](Pipe 'draw off'); $s = Pipe 'draw toggle'; Start-Sleep -Milliseconds 500; return $s }

function RedPixels([int]$px, [int]$py, [int]$pw, [int]$ph) {
    $bmp = New-Object System.Drawing.Bitmap $pw, $ph
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($px, $py, 0, 0, $bmp.Size)
    $g.Dispose()
    $n = 0
    for ($i = 0; $i -lt $pw; $i += 2) { for ($j = 0; $j -lt $ph; $j += 2) {
        $c = $bmp.GetPixel($i, $j)
        if ($c.R -gt 180 -and $c.G -lt 90 -and $c.B -lt 90) { $n++ }
    } }
    $bmp.Dispose()
    return $n
}

# The OBS remote is a hole in the drawing layer: a stroke there would press its buttons.
# (Title from code points so this file stays ASCII: Windows PowerShell reads a BOM-less
# .ps1 as ANSI.) The remote is excluded from screen capture, so screenshots never show it.
$remoteTitle = -join ([char[]](0x05DE,0x05E8,0x05DB,0x05D6,0x20,0x05D4,0x05E9,0x05DC,0x05D9,0x05D8,0x05D4,0x20,0x05E9,0x05DC,0x20)) + 'OBS'
$remote = $null
$h = [Inj]::FindWindow([NullString]::Value, $remoteTitle)   # [NullString]: PowerShell turns $null into ""
if ($h -ne [IntPtr]::Zero -and [Inj]::IsWindowVisible($h)) {
    $rr = New-Object Inj+RECT; [void][Inj]::GetWindowRect($h, [ref]$rr); $remote = $rr
}
function ClearOfRemote([int]$px, [int]$py) {
    if ($null -eq $remote) { return $true }
    return ($px + 220 -lt $remote.Left -or $px - 20 -gt $remote.Right -or $py + 60 -lt $remote.Top -or $py - 60 -gt $remote.Bottom)
}

# Spots for strokes, upper middle of the screen (the toolbar is at the very top)
$spots = New-Object System.Collections.Queue
foreach ($fy in 0.30, 0.40, 0.50, 0.60) { foreach ($fx in 0.35, 0.50, 0.65) {
    $px = [int]($SW * $fx); $py = [int]($SH * $fy)
    if (ClearOfRemote $px $py) { $spots.Enqueue(@($px, $py)) }
} }
function NextSpot { $s = $spots.Dequeue(); $spots.Enqueue($s); return $s }

# One short horizontal stroke on a cleared canvas: did it leave red ink?
function Stroke {
    $s = NextSpot; $px = $s[0]; $py = $s[1]
    [void](Pipe 'clear'); Start-Sleep -Milliseconds 100
    $before = RedPixels $px ($py - 40) 200 80
    [Inj]::Drag($px + 10, $py, $px + 180, $py, 12)
    Start-Sleep -Milliseconds 350
    return ((RedPixels $px ($py - 40) 200 80) -gt $before + 10)
}

# "touch ignored" lines in the app's log: tells a palm the app rejected from one that never
# reached it (dropped by the injection, or by Windows' own touch-off-while-pen-is-near)
$log = Join-Path $env:LOCALAPPDATA 'SmoothAnnotate\debug.log'
function IgnoredCount { @(Select-String -Path $log -Pattern 'touch ignored' -ErrorAction SilentlyContinue).Count }

$penX = [int]($SW * 0.5); $penY = [int]($SH * 0.8)
if (-not (ClearOfRemote $penX $penY)) { $penX = [int]($SW * 0.85) }

$results = @()
function Result([string]$name, [bool]$ok, [string]$detail) {
    $script:results += "{0,-9} {1}  {2}" -f $name, $(if ($ok) { 'PASS' } else { 'FAIL' }), $detail
}

try {
    "screen ${SW}x${SH}; remote: $(if ($remote) { "$($remote.Left),$($remote.Top)-$($remote.Right),$($remote.Bottom)" } else { 'not open' })"
    [void](Pipe 'clear')
    [void](DrawOn)

    # Warm-up (see the top of the file). The pen was just "near", so wait out the 1 s grace.
    [Inj]::PenHover($penX, $penY, 200); [Inj]::PenAway($penX, $penY)
    Start-Sleep -Milliseconds 1300
    $ready = $false
    for ($i = 1; $i -le 15 -and -not $ready; $i++) { $ready = Stroke; if (-not $ready) { Start-Sleep -Milliseconds 700 } }
    if (-not $ready) { throw 'injected touches never arrived (warm-up failed)' }
    "warm-up: touches arrive after $($i - 1) stroke(s)"

    if ($StepList -contains 'finger') {
        Result 'finger' (Stroke) 'a finger stroke with no pen around draws'
    }

    if ($StepList -contains 'palm') {
        $t = [Inj]::PenHoverAsync($penX, $penY, 1500)
        Start-Sleep -Milliseconds 400
        $n = IgnoredCount
        $drew = Stroke
        $by = if ((IgnoredCount) -gt $n) { 'the app ignored it' } else { 'it never reached the app' }
        $t.Join()
        if ([Inj]::PenError) { throw "simulated pen: $([Inj]::PenError)" }
        Result 'palm' (-not $drew) "a touch while the pen hovers leaves no ink ($by)"
        [Inj]::PenAway($penX, $penY)
        Start-Sleep -Milliseconds 250
        $n = IgnoredCount
        $drew = Stroke
        $by = if ((IgnoredCount) -gt $n) { 'the app ignored it' } else { 'it never reached the app' }
        Result 'grace' (-not $drew) "a touch ~0.3 s after the pen left leaves no ink ($by)"
        Start-Sleep -Milliseconds 1200
        Result 'pen-gone' (Stroke) 'a finger ~1.5 s after the pen left draws'
    }

    if ($StepList -contains 'laser') {
        [void](Pipe 'draw off')
        $a = Pipe 'laser toggle'; $b = Pipe 'laser toggle'
        Result 'laser' ($a.tool -eq 'Laser' -and -not $b.drawing) "F11 / laser toggle: on -> $($a.tool), again -> drawing=$($b.drawing)"
    }

    if ($StepList -contains 'esc') {
        # Esc is swallowed while drawing; Notepad holds focus in case it isn't
        $np = Start-Process notepad -PassThru
        Start-Sleep -Milliseconds 1200
        $s = DrawOn
        [Inj]::Key(0x1B)
        Start-Sleep -Milliseconds 300
        $t = Pipe 'status'
        Result 'esc' ($s.drawing -and -not $t.drawing) "drawing=$($s.drawing), after Esc drawing=$($t.drawing)"
        Stop-Process -Id $np.Id -ErrorAction SilentlyContinue
    }

    if ($StepList -contains 'toolbar') {
        $s = DrawOn
        [void][Inj]::SetCursorPos([int]($SW / 2), [int]($SH * 0.8))   # mouse far away: no hover help
        Start-Sleep -Milliseconds 300
        $tb = [Inj]::FindWindow([NullString]::Value, 'SmoothAnnotate Toolbar')
        $cond = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::HelpTextProperty, 'Mouse (Esc)')
        $btn = [System.Windows.Automation.AutomationElement]::FromHandle($tb).FindFirst(
            [System.Windows.Automation.TreeScope]::Descendants, $cond)
        if ($null -eq $btn) { Result 'toolbar' $false 'toolbar Mouse button not found' }
        else {
            $b = $btn.Current.BoundingRectangle
            [Inj]::Tap([int]($b.X + $b.Width / 2), [int]($b.Y + $b.Height / 2))
            Start-Sleep -Milliseconds 400
            $t = Pipe 'status'
            Result 'toolbar' ($s.drawing -and -not $t.drawing) "finger tap on the toolbar's Mouse button -> drawing=$($t.drawing)"
        }
    }
} finally {
    [Inj]::PenDestroy()
    try { [void](Pipe 'clear'); [void](Pipe 'draw off') } catch {}
    [void][Inj]::SetCursorPos([int]($SW / 2), [int]($SH / 2))
    ''
    $results
}
