using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SwtorDailyTool;

public sealed class Win32MouseHook : IDisposable
{
    public event Action<Point>? LeftButtonDown;
    public event Action<Point>? LeftButtonUp;

    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;

    private LowLevelMouseProc? _proc;
    private IntPtr _hookId = IntPtr.Zero;

    public bool IsInstalled => _hookId != IntPtr.Zero;

    public void Install()
    {
        if (_hookId != IntPtr.Zero)
        {
            return;
        }

        _proc = HookCallback;
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _hookId = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(module.ModuleName), 0);
        if (_hookId == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SetWindowsHookEx failed (error {Marshal.GetLastWin32Error()})");
        }
    }

    public void Uninstall()
    {
        if (_hookId == IntPtr.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
        _proc = null;
    }

    public void Dispose() => Uninstall();

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = wParam.ToInt32();
            if (msg is WM_LBUTTONDOWN or WM_LBUTTONUP)
            {
                var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                var point = new Point(data.pt.x, data.pt.y);
                try
                {
                    if (msg == WM_LBUTTONDOWN)
                    {
                        LeftButtonDown?.Invoke(point);
                    }
                    else
                    {
                        LeftButtonUp?.Invoke(point);
                    }
                }
                catch
                {
                    // Hook handlers must never throw out — Windows will silently disable the hook.
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
}
