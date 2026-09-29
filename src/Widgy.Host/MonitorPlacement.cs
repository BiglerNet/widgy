using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Widgy.Core.Config;

namespace Widgy.Host;

/// <summary>A display in physical pixels (the process is per-monitor DPI aware, see app.manifest).</summary>
internal sealed record MonitorInfo(string DeviceName, Int32Rect Bounds, Int32Rect WorkArea, bool IsPrimary)
{
    public override string ToString() =>
        $"{DeviceName} {Bounds.Width}x{Bounds.Height} at {Bounds.X},{Bounds.Y}{(IsPrimary ? " (primary)" : "")}";
}

internal static class MonitorPlacement
{
    public static IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var monitors = new List<MonitorInfo>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, _, _, _) =>
        {
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(hMonitor, ref info))
            {
                monitors.Add(new MonitorInfo(
                    info.szDevice,
                    info.rcMonitor.ToInt32Rect(),
                    info.rcWork.ToInt32Rect(),
                    (info.dwFlags & MONITORINFOF_PRIMARY) != 0));
            }
            return true;
        }, IntPtr.Zero);
        return monitors;
    }

    /// <summary>
    /// Picks the target monitor from config: <c>monitorName</c> ("primary", "tallest", "widest", "largest",
    /// or a device name like "DISPLAY1"), falling back to the 1-based <c>monitor</c> index, then primary.
    /// </summary>
    public static MonitorInfo Select(WidgyConfig config, IReadOnlyList<MonitorInfo> monitors)
    {
        var primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
        string? name = config.MonitorName?.Trim();

        MonitorInfo? chosen = name?.ToLowerInvariant() switch
        {
            null or "" => null,
            "primary" => primary,
            "tallest" => monitors.Where(m => m.Bounds.Height > m.Bounds.Width).MaxBy(m => m.Bounds.Height),
            "widest" => monitors.MaxBy(m => m.Bounds.Width),
            "largest" => monitors.MaxBy(m => (long)m.Bounds.Width * m.Bounds.Height),
            _ => monitors.FirstOrDefault(m => m.DeviceName.EndsWith(name!, StringComparison.OrdinalIgnoreCase)),
        };

        if (chosen == null && config.Monitor >= 1 && config.Monitor <= monitors.Count)
            chosen = monitors[config.Monitor - 1];

        return chosen ?? primary;
    }

    /// <summary>Moves/resizes the window to exactly cover <paramref name="bounds"/> (physical pixels).</summary>
    public static void Cover(Window window, Int32Rect bounds)
    {
        nint hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return;
        SetWindowPos(hwnd, IntPtr.Zero, bounds.X, bounds.Y, bounds.Width, bounds.Height, SWP_NOZORDER | SWP_NOACTIVATE);
    }

    public static Int32Rect GetWindowBounds(Window window)
    {
        nint hwnd = new WindowInteropHelper(window).Handle;
        return GetWindowRect(hwnd, out var r) ? r.ToInt32Rect() : Int32Rect.Empty;
    }

    private const int MONITORINFOF_PRIMARY = 1;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
        public Int32Rect ToInt32Rect() => new(Left, Top, Right - Left, Bottom - Top);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX info);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
}
