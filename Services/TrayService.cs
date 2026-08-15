using System.Runtime.InteropServices;
using UrbanPlanToolbox.Views;
using Windows.Graphics;

namespace UrbanPlanToolbox.Services;

/// <summary>Native notification-area icon hosted by a message-only window with a WinUI 3 menu surface.</summary>
public sealed class TrayService : IDisposable
{
    private const uint Callback = 0x8001;
    private const uint RButtonUp = 0x0205;
    private const uint LButtonDblClk = 0x0203;
    private const uint Add = 0;
    private const uint Modify = 1;
    private const uint Delete = 2;
    private const uint Message = 1;
    private const uint Icon = 2;
    private const uint Tip = 4;

    private static readonly Proc Procedure = WindowProc;
    private static readonly Dictionary<nint, TrayService> Owners = [];

    private readonly Windows.UI.ViewManagement.UISettings _uiSettings = new();
    private nint _window;
    private nint _icon;
    private bool _created;
    private bool _iconVisible;
    private bool _recorderVisible;
    private TrayMenuWindow? _menuWindow;

    public event EventHandler? OpenRequested;
    public event EventHandler? RecorderRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;

    public void Initialize(bool iconVisible = true)
    {
        if (_created) return;

        var name = "UrbanPlanToolbox.Tray." + Guid.NewGuid().ToString("N");
        var windowClass = new Class
        {
            Size = (uint)Marshal.SizeOf<Class>(),
            Proc = Marshal.GetFunctionPointerForDelegate(Procedure),
            Name = name
        };

        if (RegisterClassEx(ref windowClass) == 0)
            throw new InvalidOperationException("Cannot register the tray message window.");

        _window = CreateWindowEx(0, name, name, 0, 0, 0, 0, 0, new nint(-3), 0, 0, 0);
        if (_window == 0)
            throw new InvalidOperationException("Cannot create the tray message window.");

        Owners[_window] = this;
        _icon = LoadThemeAwareIcon();
        _created = true;
        _uiSettings.ColorValuesChanged += OnSystemColorsChanged;

        if (iconVisible)
            SetIconVisible(true);
    }

    public void SetIconVisible(bool visible)
    {
        if (!_created || visible == _iconVisible) return;

        if (visible)
        {
            var data = Data(Message | Icon | Tip);
            if (!ShellNotifyIcon(Add, ref data))
            {
                AppLogger.Default.Warning("Tray", "TrayIconShowFailed", "Shell_NotifyIcon(NIM_ADD) returned false.");
                return;
            }

            _iconVisible = true;
            return;
        }

        var deleteData = Data(0);
        ShellNotifyIcon(Delete, ref deleteData);
        _iconVisible = false;
        _menuWindow?.HideMenu();
    }

    public void SetRecorderVisible(bool visible)
    {
        _recorderVisible = visible;
        if (_created && _iconVisible)
        {
            var data = Data(Tip);
            ShellNotifyIcon(Modify, ref data);
        }
    }

    private void OnSystemColorsChanged(Windows.UI.ViewManagement.UISettings sender, object args) => RefreshIcon();

    private nint LoadThemeAwareIcon()
    {
        var background = _uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
        var isLight = (0.2126 * background.R) + (0.7152 * background.G) + (0.0722 * background.B) >= 128;
        var path = Path.Combine(AppContext.BaseDirectory, WindowIconTheme.GetIconRelativePath(isLight ? AppTheme.Light : AppTheme.Dark));
        var size = GetSystemMetrics(49); // SM_CXSMICON; ICO selects its nearest embedded size for DPI.
        return LoadImage(0, path, 1, size, size, 0x10);
    }

    private void RefreshIcon()
    {
        var replacement = LoadThemeAwareIcon();
        if (replacement == 0) return;

        var previous = _icon;
        _icon = replacement;
        if (_created && _iconVisible)
        {
            var data = Data(Icon);
            ShellNotifyIcon(Modify, ref data);
        }

        if (previous != 0) DestroyIcon(previous);
    }

    private DataStruct Data(uint flags) => new()
    {
        Size = (uint)Marshal.SizeOf<DataStruct>(),
        Window = _window,
        Id = 1,
        Flags = flags,
        Callback = Callback,
        Icon = _icon,
        Tip = "UrbanPlanToolbox"
    };

    private void ShowMenu()
    {
        if (!_iconVisible || !GetCursorPos(out var cursor)) return;

        if (_menuWindow is null)
        {
            _menuWindow = new TrayMenuWindow();
            _menuWindow.OpenRequested += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
            _menuWindow.RecorderRequested += (_, _) => RecorderRequested?.Invoke(this, EventArgs.Empty);
            _menuWindow.SettingsRequested += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
            _menuWindow.ExitRequested += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        }

        _menuWindow.ShowAt(new PointInt32(cursor.X, cursor.Y), _recorderVisible);
    }

    private static nint WindowProc(nint hwnd, uint msg, nuint wp, nint lp)
    {
        if (Owners.TryGetValue(hwnd, out var owner) && msg == Callback)
        {
            var action = unchecked((uint)lp.ToInt64());
            if (action == RButtonUp)
                owner.ShowMenu();
            else if (action == LButtonDblClk)
                owner.OpenRequested?.Invoke(owner, EventArgs.Empty);
            return 0;
        }

        return DefWindowProc(hwnd, msg, wp, lp);
    }

    public void Dispose()
    {
        _menuWindow?.CloseForExit();
        _menuWindow = null;

        if (_window == 0) return;
        _uiSettings.ColorValuesChanged -= OnSystemColorsChanged;
        if (_iconVisible)
        {
            var data = Data(0);
            ShellNotifyIcon(Delete, ref data);
        }

        _iconVisible = false;
        Owners.Remove(_window);
        DestroyWindow(_window);
        if (_icon != 0) DestroyIcon(_icon);
        _icon = 0;
        _window = 0;
        _created = false;
    }

    private delegate nint Proc(nint h, uint m, nuint w, nint l);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Class
    {
        public uint Size, Style;
        public nint Proc;
        public int Extra, WindowExtra;
        public nint Instance, Icon, Cursor, Background;
        public string? MenuName, Name;
        public nint IconSmall;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DataStruct
    {
        public uint Size;
        public nint Window;
        public uint Id, Flags, Callback;
        public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X, Y;
    }

    [DllImport("user32", CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref Class c);

    [DllImport("user32", CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowEx(uint x, string c, string n, uint s, int a, int b, int d, int e, nint p, nint m, nint i, nint q);

    [DllImport("user32")]
    private static extern nint DefWindowProc(nint h, uint m, nuint w, nint l);

    [DllImport("user32")]
    private static extern bool DestroyWindow(nint h);

    [DllImport("user32")]
    private static extern bool DestroyIcon(nint h);

    [DllImport("user32", CharSet = CharSet.Unicode)]
    private static extern nint LoadImage(nint i, string n, int t, int x, int y, int f);

    [DllImport("user32")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("shell32", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode)]
    private static extern bool ShellNotifyIcon(uint m, ref DataStruct d);

    [DllImport("user32")]
    private static extern bool GetCursorPos(out Point p);
}
