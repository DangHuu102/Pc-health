using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace PCHealthDashboard.Helpers;

public class HotkeyHelper : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;

    private readonly IntPtr _hwnd;
    private readonly int _id = 9000;
    private Action _onHotKeyPressed;

    public HotkeyHelper(IntPtr hwnd, Action onHotKeyPressed)
    {
        _hwnd = hwnd;
        _onHotKeyPressed = onHotKeyPressed;
        
        HwndSource source = HwndSource.FromHwnd(_hwnd);
        source.AddHook(HwndHook);

        // Register Ctrl + Shift + Space
        uint modifiers = MOD_CONTROL | MOD_SHIFT;
        uint key = 0x20; // Space
        
        RegisterHotKey(_hwnd, _id, modifiers, key);
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg == WM_HOTKEY && wParam.ToInt32() == _id)
        {
            _onHotKeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterHotKey(_hwnd, _id);
    }
}
