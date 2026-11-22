using System.Runtime.InteropServices;

namespace TeamsMCP.Services;

/// <summary>
/// Win32 Shell_NotifyIconW system tray host with a hidden message window and context menu.
/// Works identically in MSIX-packaged Store apps and unpackaged desktop runs.
/// </summary>
public sealed class TrayIconService : IDisposable
{
	private const uint WmUser = 0x0400;
	private const uint WmTrayIcon = WmUser + 1;
	private const uint WmLButtonUp = 0x0202;
	private const uint WmLButtonDblClk = 0x0203;
	private const uint WmRButtonUp = 0x0205;
	private const uint WmContextMenu = 0x007B;

	private const uint NimAdd = 0x00000000;
	private const uint NimModify = 0x00000001;
	private const uint NimDelete = 0x00000002;
	private const uint NifMessage = 0x00000001;
	private const uint NifIcon = 0x00000002;
	private const uint NifTip = 0x00000004;

	private const uint ImageIcon = 1;
	private const uint LrLoadFromFile = 0x00000010;
	private const uint LrDefaultSize = 0x00000040;

	private const uint MfString = 0x00000000;
	private const uint MfSeparator = 0x00000800;
	private const uint MfGrayed = 0x00000001;
	private const uint TpmRightButton = 0x0002;
	private const uint TpmReturnCmd = 0x0100;
	private const uint TpmNonotify = 0x0080;

	private const uint CmdOpen = 1001;
	private const uint CmdToggleServer = 1002;
	private const uint CmdCopyEndpoint = 1003;
	private const uint CmdRefreshToken = 1004;
	private const uint CmdExit = 1005;

	private const uint TrayIconId = 1;

	private readonly string windowClassName = $"TeamsMCP_TrayWindow_{Guid.NewGuid():N}";
	private readonly WndProcDelegate wndProcDelegate;
	private readonly uint taskbarCreatedMessage;
	private readonly IntPtr runningIconHandle;
	private readonly IntPtr stoppedIconHandle;
	private IntPtr messageWindowHandle;
	private bool isAdded;
	private bool isRunning;
	private string tooltip = "TeamsMCP — Stopped";
	private bool disposed;

	public event EventHandler? OpenRequested;
	public event EventHandler? ToggleServerRequested;
	public event EventHandler? CopyEndpointRequested;
	public event EventHandler? RefreshTokenRequested;
	public event EventHandler? ExitRequested;

	public TrayIconService()
	{
		wndProcDelegate = WndProc;
		taskbarCreatedMessage = RegisterWindowMessageW("TaskbarCreated");

		var assetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");
		runningIconHandle = LoadIconFromFile(Path.Combine(assetsDir, "TrayRunning.ico"));
		stoppedIconHandle = LoadIconFromFile(Path.Combine(assetsDir, "TrayStopped.ico"));

		CreateMessageWindow();
		AddOrUpdateIcon(NimAdd);
	}

	public void UpdateStatus(bool serverRunning, string tooltipText)
	{
		if (disposed)
			return;

		isRunning = serverRunning;
		tooltip = tooltipText.Length > 127 ? tooltipText[..127] : tooltipText;
		AddOrUpdateIcon(isAdded ? NimModify : NimAdd);
	}

	private void CreateMessageWindow()
	{
		var hInstance = GetModuleHandleW(null);
		var wndClass = new WNDCLASSEXW
		{
			cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
			lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProcDelegate),
			hInstance = hInstance,
			lpszClassName = windowClassName
		};

		if (RegisterClassExW(ref wndClass) == 0)
			throw new InvalidOperationException("Failed to register tray icon window class.");

		messageWindowHandle = CreateWindowExW(
			0,
			windowClassName,
			"TeamsMCP Tray Host",
			0,
			0, 0, 0, 0,
			IntPtr.Zero,
			IntPtr.Zero,
			hInstance,
			IntPtr.Zero);

		if (messageWindowHandle == IntPtr.Zero)
			throw new InvalidOperationException("Failed to create tray icon message window.");
	}

	private void AddOrUpdateIcon(uint message)
	{
		if (messageWindowHandle == IntPtr.Zero)
			return;

		var data = new NOTIFYICONDATAW
		{
			cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
			hWnd = messageWindowHandle,
			uID = TrayIconId,
			uFlags = NifMessage | NifIcon | NifTip,
			uCallbackMessage = WmTrayIcon,
			hIcon = isRunning ? runningIconHandle : stoppedIconHandle,
			szTip = tooltip
		};

		if (Shell_NotifyIconW(message, ref data) && message == NimAdd)
			isAdded = true;
	}

	private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
	{
		if (msg == taskbarCreatedMessage && taskbarCreatedMessage != 0)
		{
			isAdded = false;
			AddOrUpdateIcon(NimAdd);
			return IntPtr.Zero;
		}

		if (msg == WmTrayIcon)
		{
			var mouseMessage = (uint)(lParam.ToInt64() & 0xFFFF);
			switch (mouseMessage)
			{
				case WmLButtonUp:
				case WmLButtonDblClk:
					OpenRequested?.Invoke(this, EventArgs.Empty);
					break;
				case WmRButtonUp:
				case WmContextMenu:
					ShowContextMenu();
					break;
			}
			return IntPtr.Zero;
		}

		return DefWindowProcW(hWnd, msg, wParam, lParam);
	}

	private void ShowContextMenu()
	{
		var menu = CreatePopupMenu();
		if (menu == IntPtr.Zero)
			return;

		try
		{
			AppendMenuW(menu, MfString, CmdOpen, "Open TeamsMCP");
			SetMenuDefaultItem(menu, CmdOpen, false);
			AppendMenuW(menu, MfSeparator, 0, null);
			AppendMenuW(menu, MfString, CmdToggleServer, isRunning ? "Stop MCP Server" : "Start MCP Server");
			AppendMenuW(menu, MfString | (isRunning ? 0u : MfGrayed), CmdCopyEndpoint, "Copy MCP Endpoint URL");
			AppendMenuW(menu, MfString | (isRunning ? 0u : MfGrayed), CmdRefreshToken, "Refresh Token");
			AppendMenuW(menu, MfSeparator, 0, null);
			AppendMenuW(menu, MfString, CmdExit, "Exit");

			GetCursorPos(out var pt);
			SetForegroundWindow(messageWindowHandle);
			var cmd = TrackPopupMenuEx(menu, TpmRightButton | TpmReturnCmd | TpmNonotify, pt.X, pt.Y, messageWindowHandle, IntPtr.Zero);
			PostMessageW(messageWindowHandle, 0, IntPtr.Zero, IntPtr.Zero);

			switch (cmd)
			{
				case CmdOpen:
					OpenRequested?.Invoke(this, EventArgs.Empty);
					break;
				case CmdToggleServer:
					ToggleServerRequested?.Invoke(this, EventArgs.Empty);
					break;
				case CmdCopyEndpoint:
					CopyEndpointRequested?.Invoke(this, EventArgs.Empty);
					break;
				case CmdRefreshToken:
					RefreshTokenRequested?.Invoke(this, EventArgs.Empty);
					break;
				case CmdExit:
					ExitRequested?.Invoke(this, EventArgs.Empty);
					break;
			}
		}
		finally
		{
			DestroyMenu(menu);
		}
	}

	private static IntPtr LoadIconFromFile(string path)
	{
		if (File.Exists(path))
		{
			var handle = LoadImageW(IntPtr.Zero, path, ImageIcon, 16, 16, LrLoadFromFile | LrDefaultSize);
			if (handle != IntPtr.Zero)
				return handle;
		}
		return LoadIconW(IntPtr.Zero, new IntPtr(32512)); // IDI_APPLICATION fallback
	}

	public void Dispose()
	{
		if (disposed)
			return;
		disposed = true;

		if (isAdded && messageWindowHandle != IntPtr.Zero)
		{
			var data = new NOTIFYICONDATAW
			{
				cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
				hWnd = messageWindowHandle,
				uID = TrayIconId
			};
			Shell_NotifyIconW(NimDelete, ref data);
			isAdded = false;
		}

		if (messageWindowHandle != IntPtr.Zero)
		{
			DestroyWindow(messageWindowHandle);
			messageWindowHandle = IntPtr.Zero;
		}

		UnregisterClassW(windowClassName, GetModuleHandleW(null));

		if (runningIconHandle != IntPtr.Zero)
			DestroyIcon(runningIconHandle);
		if (stoppedIconHandle != IntPtr.Zero)
			DestroyIcon(stoppedIconHandle);
	}

	private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

	[StructLayout(LayoutKind.Sequential)]
	private struct POINT
	{
		public int X;
		public int Y;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct WNDCLASSEXW
	{
		public uint cbSize;
		public uint style;
		public IntPtr lpfnWndProc;
		public int cbClsExtra;
		public int cbWndExtra;
		public IntPtr hInstance;
		public IntPtr hIcon;
		public IntPtr hCursor;
		public IntPtr hbrBackground;
		[MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
		[MarshalAs(UnmanagedType.LPWStr)] public string lpszClassName;
		public IntPtr hIconSm;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct NOTIFYICONDATAW
	{
		public uint cbSize;
		public IntPtr hWnd;
		public uint uID;
		public uint uFlags;
		public uint uCallbackMessage;
		public IntPtr hIcon;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
		public uint dwState;
		public uint dwStateMask;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
		public uint uTimeoutOrVersion;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
		public uint dwInfoFlags;
		public Guid guidItem;
		public IntPtr hBalloonIcon;
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern IntPtr GetModuleHandleW(string? lpModuleName);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern bool UnregisterClassW(string lpClassName, IntPtr hInstance);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern IntPtr CreateWindowExW(
		uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
		int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu,
		IntPtr hInstance, IntPtr lpParam);

	[DllImport("user32.dll")]
	private static extern bool DestroyWindow(IntPtr hWnd);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern uint RegisterWindowMessageW(string lpString);

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	private static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpData);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern IntPtr LoadImageW(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

	[DllImport("user32.dll")]
	private static extern IntPtr LoadIconW(IntPtr hInstance, IntPtr lpIconName);

	[DllImport("user32.dll")]
	private static extern bool DestroyIcon(IntPtr hIcon);

	[DllImport("user32.dll")]
	private static extern IntPtr CreatePopupMenu();

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, uint uIDNewItem, string? lpNewItem);

	[DllImport("user32.dll")]
	private static extern bool SetMenuDefaultItem(IntPtr hMenu, uint uItem, bool fByPos);

	[DllImport("user32.dll")]
	private static extern bool DestroyMenu(IntPtr hMenu);

	[DllImport("user32.dll")]
	private static extern bool GetCursorPos(out POINT lpPoint);

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern uint TrackPopupMenuEx(IntPtr hmenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

	[DllImport("user32.dll")]
	private static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
