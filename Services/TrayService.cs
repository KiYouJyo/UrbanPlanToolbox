using System.Runtime.InteropServices;

namespace UrbanPlanToolbox.Services;

/// <summary>Native notification-area icon hosted by a message-only window.</summary>
public sealed class TrayService : IDisposable
{
    private const uint Callback = 0x8001, Command = 0x0111, RButtonUp = 0x0205, LButtonDblClk = 0x0203;
    private const uint Add = 0, Modify = 1, Delete = 2, Message = 1, Icon = 2, Tip = 4, String = 0, Separator = 0x800, Checked = 8, RightButton = 2;
    private const int Open = 1001, Recorder = 1002, Settings = 1003, Exit = 1004;
    private static readonly Proc Procedure = WindowProc; private static readonly Dictionary<nint, TrayService> Owners = [];
    private nint _window, _icon; private bool _created, _recorderVisible;
    public event EventHandler? OpenRequested, RecorderRequested, SettingsRequested, ExitRequested;
    public void Initialize()
    {
        if (_created) return;
        var name = "UrbanPlanToolbox.Tray." + Guid.NewGuid().ToString("N"); var wc = new Class { Size = (uint)Marshal.SizeOf<Class>(), Proc = Marshal.GetFunctionPointerForDelegate(Procedure), Name = name };
        if (RegisterClassEx(ref wc) == 0) throw new InvalidOperationException("Cannot register the tray message window.");
        _window = CreateWindowEx(0, name, name, 0, 0, 0, 0, 0, new nint(-3), 0, 0, 0); if (_window == 0) throw new InvalidOperationException("Cannot create the tray message window."); Owners[_window] = this;
        _icon = LoadImage(0, Path.Combine(AppContext.BaseDirectory, "Assets", "UrbanPlanToolbox.ico"), 1, 0, 0, 0x10); var data = Data(Message | Icon | Tip); if (!ShellNotifyIcon(Add, ref data)) throw new InvalidOperationException("Cannot create the tray icon."); _created = true;
    }
    public void SetRecorderVisible(bool visible) { _recorderVisible = visible; if (_created) { var data = Data(Tip); ShellNotifyIcon(Modify, ref data); } }
    private DataStruct Data(uint flags) => new() { Size = (uint)Marshal.SizeOf<DataStruct>(), Window = _window, Id = 1, Flags = flags, Callback = Callback, Icon = _icon, Tip = "UrbanPlanToolbox" };
    private void Menu()
    {
        var menu = CreatePopupMenu(); AppendMenu(menu, String, Open, "Open UrbanPlanToolbox"); AppendMenu(menu, Separator, 0, null); AppendMenu(menu, String | (_recorderVisible ? Checked : 0), Recorder, "Inspiration Recorder"); AppendMenu(menu, Separator, 0, null); AppendMenu(menu, String, Settings, "Settings"); AppendMenu(menu, String, Exit, "Exit UrbanPlanToolbox"); GetCursorPos(out var p); SetForegroundWindow(_window); TrackPopupMenu(menu, RightButton, p.X, p.Y, 0, _window, 0); DestroyMenu(menu);
    }
    private void Invoke(int id) { if (id == Open) OpenRequested?.Invoke(this, EventArgs.Empty); else if (id == Recorder) RecorderRequested?.Invoke(this, EventArgs.Empty); else if (id == Settings) SettingsRequested?.Invoke(this, EventArgs.Empty); else if (id == Exit) ExitRequested?.Invoke(this, EventArgs.Empty); }
    private static nint WindowProc(nint hwnd, uint msg, nuint wp, nint lp)
    {
        if (Owners.TryGetValue(hwnd, out var owner)) { if (msg == Callback) { var action = unchecked((uint)lp.ToInt64()); if (action == RButtonUp) owner.Menu(); else if (action == LButtonDblClk) owner.OpenRequested?.Invoke(owner, EventArgs.Empty); return 0; } if (msg == Command) { owner.Invoke((int)(wp & 0xffff)); return 0; } }
        return DefWindowProc(hwnd, msg, wp, lp);
    }
    public void Dispose() { if (_window == 0) return; var data = Data(0); ShellNotifyIcon(Delete, ref data); Owners.Remove(_window); DestroyWindow(_window); if (_icon != 0) DestroyIcon(_icon); _window = 0; _created = false; }
    private delegate nint Proc(nint h, uint m, nuint w, nint l);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct Class { public uint Size, Style; public nint Proc; public int Extra, WindowExtra; public nint Instance, Icon, Cursor, Background; public string? MenuName, Name; public nint IconSmall; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct DataStruct { public uint Size; public nint Window; public uint Id, Flags, Callback; public nint Icon; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip; }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [DllImport("user32", CharSet = CharSet.Unicode)] private static extern ushort RegisterClassEx(ref Class c); [DllImport("user32", CharSet = CharSet.Unicode)] private static extern nint CreateWindowEx(uint x, string c, string n, uint s, int a, int b, int d, int e, nint p, nint m, nint i, nint q); [DllImport("user32")] private static extern nint DefWindowProc(nint h, uint m, nuint w, nint l); [DllImport("user32")] private static extern bool DestroyWindow(nint h); [DllImport("user32")] private static extern bool DestroyIcon(nint h); [DllImport("user32", CharSet = CharSet.Unicode)] private static extern nint LoadImage(nint i, string n, int t, int x, int y, int f); [DllImport("shell32", CharSet = CharSet.Unicode)] private static extern bool ShellNotifyIcon(uint m, ref DataStruct d); [DllImport("user32")] private static extern nint CreatePopupMenu(); [DllImport("user32", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(nint m, uint f, int i, string? t); [DllImport("user32")] private static extern bool DestroyMenu(nint m); [DllImport("user32")] private static extern bool GetCursorPos(out Point p); [DllImport("user32")] private static extern bool SetForegroundWindow(nint h); [DllImport("user32")] private static extern bool TrackPopupMenu(nint m, uint f, int x, int y, int r, nint h, nint z);
}
