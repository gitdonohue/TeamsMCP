using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TeamsMCP.Core;
using TeamsMCP.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
using WinRT.Interop;

namespace TeamsMCP;

public sealed partial class MainWindow : Window
{
	private readonly SettingsStore settingsStore = new();
	private readonly McpServerManager serverManager = new();
	private readonly TrayIconService trayIcon = new();
	private readonly IntPtr windowHandle;
	private readonly AppWindow appWindow;

	private AppSettings currentSettings;
	private bool isInitializing = true;
	private bool isExiting;
	private bool startedWithPostingEnabled;
	private string? activeVerificationUrl;

	public MainWindow(bool startHidden)
	{
		InitializeComponent();

		SystemBackdrop = new MicaBackdrop();
		windowHandle = WindowNative.GetWindowHandle(this);
		appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(windowHandle));
		appWindow.Resize(new SizeInt32(680, 820));

		var appIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
		if (File.Exists(appIconPath))
			appWindow.SetIcon(appIconPath);

		appWindow.Closing += AppWindow_Closing;

		currentSettings = settingsStore.Load();
		ApplySettingsToUi(currentSettings);
		UpdateClientConfigSnippet();
		isInitializing = false;

		serverManager.StatusChanged += ServerManager_StatusChanged;
		serverManager.DeviceCodePrompted += ServerManager_DeviceCodePrompted;

		trayIcon.OpenRequested += (_, _) => DispatcherQueue.TryEnqueue(ShowWindow);
		trayIcon.ToggleServerRequested += (_, _) => DispatcherQueue.TryEnqueue(() => _ = ToggleServerAsync());
		trayIcon.CopyEndpointRequested += (_, _) => DispatcherQueue.TryEnqueue(CopyEndpointToClipboard);
		trayIcon.RefreshTokenRequested += (_, _) => DispatcherQueue.TryEnqueue(() => _ = RefreshTokenAsync());
		trayIcon.ExitRequested += (_, _) => DispatcherQueue.TryEnqueue(() => _ = ExitApplicationAsync());

		_ = LoadStartupTaskStatusAsync();

		if (startHidden)
			appWindow.Hide();

		if (currentSettings.AutoStartServer && currentSettings.ValidateForStart() == null)
			_ = StartServerAsync();
	}

	public void ShowWindow()
	{
		appWindow.Show();
		Activate();
	}

	private void ApplySettingsToUi(AppSettings settings)
	{
		ClientIdBox.Text = settings.ClientId;
		TenantIdBox.Text = settings.TenantId;
		PortBox.Value = settings.Port;
		DeviceCodeSwitch.IsOn = settings.UseDeviceCode;
		AllowPostingSwitch.IsOn = settings.AllowChannelPosting;
		EnableImageSaveSwitch.IsOn = settings.EnableImageSave;
		ImageSaveDirBox.Text = settings.ImageSaveDirectory;
		ImageSaveDirBox.IsEnabled = settings.EnableImageSave;
		BrowseImageDirButton.IsEnabled = settings.EnableImageSave;
		AutoStartServerCheckBox.IsChecked = settings.AutoStartServer;
		StartMinimizedCheckBox.IsChecked = settings.StartMinimizedToTray;
		MinimizeOnCloseCheckBox.IsChecked = settings.MinimizeToTrayOnClose;
	}

	private AppSettings ReadSettingsFromUi() => new AppSettings
	{
		ClientId = ClientIdBox.Text,
		TenantId = TenantIdBox.Text,
		Port = double.IsNaN(PortBox.Value) ? AppSettings.DefaultPort : (int)PortBox.Value,
		UseDeviceCode = DeviceCodeSwitch.IsOn,
		AllowChannelPosting = AllowPostingSwitch.IsOn,
		EnableImageSave = EnableImageSaveSwitch.IsOn,
		ImageSaveDirectory = ImageSaveDirBox.Text,
		AutoStartServer = AutoStartServerCheckBox.IsChecked == true,
		StartMinimizedToTray = StartMinimizedCheckBox.IsChecked == true,
		MinimizeToTrayOnClose = MinimizeOnCloseCheckBox.IsChecked == true
	}.Normalize();

	private void SaveCurrentSettings()
	{
		if (isInitializing)
			return;

		currentSettings = ReadSettingsFromUi();
		settingsStore.Save(currentSettings);
		UpdateClientConfigSnippet();
	}

	private void UpdateClientConfigSnippet()
	{
		var port = double.IsNaN(PortBox.Value) ? AppSettings.DefaultPort : (int)PortBox.Value;
		if (port is >= AppSettings.MinPort and <= AppSettings.MaxPort)
			ClientConfigBox.Text = McpClientConfigBuilder.BuildServerConfigJson(port);
	}

	private async Task LoadStartupTaskStatusAsync()
	{
		var status = await StartupTaskService.GetStatusAsync();
		ApplyStartupTaskStatus(status);
	}

	private async void LaunchAtSignInCheckBox_Click(object sender, RoutedEventArgs e)
	{
		if (isInitializing)
			return;

		LaunchAtSignInCheckBox.IsEnabled = false;
		var status = await StartupTaskService.SetEnabledAsync(LaunchAtSignInCheckBox.IsChecked == true);
		ApplyStartupTaskStatus(status);
	}

	private void ApplyStartupTaskStatus(StartupTaskStatus status)
	{
		isInitializing = true;
		LaunchAtSignInCheckBox.IsChecked = status.IsEnabled;
		LaunchAtSignInCheckBox.IsEnabled = status.IsSupported;
		StartupTaskNoteText.Text = status.StatusNote;
		StartupTaskNoteText.Visibility = !status.IsSupported && !string.IsNullOrWhiteSpace(status.StatusNote)
			? Visibility.Visible
			: Visibility.Collapsed;
		isInitializing = false;
	}

	private void SettingControl_Changed(object sender, object e)
	{
		if (isInitializing)
			return;

		var isRunning = serverManager.Snapshot.State == ServerState.Running;
		ImageSaveDirBox.IsEnabled = !isRunning && EnableImageSaveSwitch.IsOn;
		BrowseImageDirButton.IsEnabled = !isRunning && EnableImageSaveSwitch.IsOn;
		SaveCurrentSettings();
	}

	private void PortBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
	{
		if (isInitializing)
			return;

		SaveCurrentSettings();
	}

	private void AllowPostingSwitch_Toggled(object sender, RoutedEventArgs e)
	{
		if (isInitializing)
			return;

		SaveCurrentSettings();

		if (serverManager.Snapshot.State == ServerState.Running)
		{
			if (startedWithPostingEnabled)
			{
				serverManager.TrySetChannelPostingEnabled(AllowPostingSwitch.IsOn);
			}
			else if (AllowPostingSwitch.IsOn)
			{
				StatusDetailText.Text = "Restart the server to register the send_channel_message tool.";
			}
		}
	}

	private async void BrowseImageDirButton_Click(object sender, RoutedEventArgs e)
	{
		var picker = new FolderPicker
		{
			SuggestedStartLocation = PickerLocationId.PicturesLibrary
		};
		picker.FileTypeFilter.Add("*");
		InitializeWithWindow.Initialize(picker, windowHandle);

		var folder = await picker.PickSingleFolderAsync();
		if (folder != null)
		{
			ImageSaveDirBox.Text = folder.Path;
			SaveCurrentSettings();
		}
	}

	private async void ToggleServerButton_Click(object sender, RoutedEventArgs e) => await ToggleServerAsync();

	private async Task ToggleServerAsync()
	{
		if (serverManager.Snapshot.State == ServerState.Running || serverManager.Snapshot.State == ServerState.Starting)
			await StopServerAsync();
		else
			await StartServerAsync();
	}

	private async Task StartServerAsync()
	{
		SaveCurrentSettings();
		ToggleServerButton.IsEnabled = false;
		try
		{
			startedWithPostingEnabled = currentSettings.AllowChannelPosting;
			await serverManager.StartAsync(currentSettings, windowHandle);
		}
		catch (Exception)
		{
			// StatusChanged already updates the UI with the error message.
		}
		finally
		{
			ToggleServerButton.IsEnabled = true;
		}
	}

	private async Task StopServerAsync()
	{
		ToggleServerButton.IsEnabled = false;
		try
		{
			await serverManager.StopAsync();
			startedWithPostingEnabled = false;
		}
		finally
		{
			ToggleServerButton.IsEnabled = true;
		}
	}

	private async void RefreshTokenButton_Click(object sender, RoutedEventArgs e) => await RefreshTokenAsync();

	private async Task RefreshTokenAsync()
	{
		if (serverManager.Snapshot.State != ServerState.Running)
			return;

		RefreshTokenButton.IsEnabled = false;
		try
		{
			await serverManager.RefreshTokenAsync();
		}
		catch (Exception ex)
		{
			StatusDetailText.Text = $"Token refresh failed: {ex.Message}";
		}
		finally
		{
			RefreshTokenButton.IsEnabled = serverManager.Snapshot.State == ServerState.Running;
		}
	}

	private async void NewTokenButton_Click(object sender, RoutedEventArgs e)
	{
		if (serverManager.Snapshot.State != ServerState.Running)
			return;

		NewTokenButton.IsEnabled = false;
		try
		{
			await serverManager.AcquireNewTokenAsync();
		}
		catch (Exception)
		{
			// StatusChanged already displays the error.
		}
		finally
		{
			NewTokenButton.IsEnabled = serverManager.Snapshot.State == ServerState.Running;
		}
	}

	private void ServerManager_StatusChanged(object? sender, ServerStatusSnapshot snapshot)
	{
		DispatcherQueue.TryEnqueue(() => ApplySnapshotToUi(snapshot));
	}

	private void ServerManager_DeviceCodePrompted(object? sender, DeviceCodePrompt prompt)
	{
		DispatcherQueue.TryEnqueue(async () =>
		{
			ShowWindow();
			CopyText(prompt.UserCode);
			if (Uri.TryCreate(prompt.VerificationUrl, UriKind.Absolute, out var uri))
				await Launcher.LaunchUriAsync(uri);
		});
	}

	private void ApplySnapshotToUi(ServerStatusSnapshot snapshot)
	{
		var isRunning = snapshot.State == ServerState.Running;
		var isBusy = snapshot.State == ServerState.Starting;

		StatusDot.Fill = new SolidColorBrush(snapshot.State switch
		{
			ServerState.Running => Color.FromArgb(255, 22, 198, 12),
			ServerState.Starting => Color.FromArgb(255, 245, 166, 35),
			ServerState.Error => Color.FromArgb(255, 232, 17, 35),
			_ => Color.FromArgb(255, 154, 160, 166)
		});

		StatusTitleText.Text = snapshot.State switch
		{
			ServerState.Running => "Running",
			ServerState.Starting => "Starting...",
			ServerState.Error => "Error",
			_ => "Stopped"
		};

		StatusDetailText.Text = snapshot.StatusMessage;
		ToggleServerButton.Content = isRunning ? "Stop Server" : "Start Server";
		ToggleServerButton.IsEnabled = !isBusy;

		EndpointRow.Visibility = snapshot.EndpointUrl != null ? Visibility.Visible : Visibility.Collapsed;
		EndpointText.Text = snapshot.EndpointUrl ?? string.Empty;

		if (!string.IsNullOrWhiteSpace(snapshot.SignedInUser))
		{
			var expiry = snapshot.TokenExpiresOn.HasValue
				? $" (expires {snapshot.TokenExpiresOn.Value.LocalDateTime:t})"
				: string.Empty;
			AccountStatusText.Text = $"Signed in as {snapshot.SignedInUser}{expiry}";
			AccountStatusText.Visibility = Visibility.Visible;
		}
		else
		{
			AccountStatusText.Visibility = Visibility.Collapsed;
		}

		if (snapshot.ActiveDeviceCode is { } code)
		{
			activeVerificationUrl = code.VerificationUrl;
			DeviceCodeMessageText.Text = code.Message;
			DeviceCodeValueText.Text = code.UserCode;
			DeviceCodeCard.Visibility = Visibility.Visible;
		}
		else
		{
			activeVerificationUrl = null;
			DeviceCodeCard.Visibility = Visibility.Collapsed;
		}

		var editable = !isRunning && !isBusy;
		ClientIdBox.IsEnabled = editable;
		TenantIdBox.IsEnabled = editable;
		DeviceCodeSwitch.IsEnabled = editable;
		PortBox.IsEnabled = editable;
		EnableImageSaveSwitch.IsEnabled = editable;
		ImageSaveDirBox.IsEnabled = editable && EnableImageSaveSwitch.IsOn;
		BrowseImageDirButton.IsEnabled = editable && EnableImageSaveSwitch.IsOn;

		RefreshTokenButton.IsEnabled = isRunning;
		NewTokenButton.IsEnabled = isRunning;

		var tooltip = isRunning && snapshot.EndpointUrl != null
			? $"TeamsMCP — Running ({snapshot.EndpointUrl})"
			: "TeamsMCP — Stopped";
		trayIcon.UpdateStatus(isRunning, tooltip);
	}

	private void CopyEndpointButton_Click(object sender, RoutedEventArgs e) => CopyEndpointToClipboard();

	private void CopyEndpointToClipboard()
	{
		if (serverManager.Snapshot.EndpointUrl is { } url)
			CopyText(url);
	}

	private void CopyCodeButton_Click(object sender, RoutedEventArgs e) => CopyText(DeviceCodeValueText.Text);

	private async void OpenSignInButton_Click(object sender, RoutedEventArgs e)
	{
		if (activeVerificationUrl != null && Uri.TryCreate(activeVerificationUrl, UriKind.Absolute, out var uri))
			await Launcher.LaunchUriAsync(uri);
	}

	private void CopyConfigButton_Click(object sender, RoutedEventArgs e) => CopyText(ClientConfigBox.Text);

	private static void CopyText(string text)
	{
		if (string.IsNullOrEmpty(text))
			return;
		var package = new DataPackage();
		package.SetText(text);
		Clipboard.SetContent(package);
	}

	private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
	{
		if (!isExiting && currentSettings.MinimizeToTrayOnClose)
		{
			args.Cancel = true;
			appWindow.Hide();
			return;
		}

		if (!isExiting)
		{
			args.Cancel = true;
			_ = ExitApplicationAsync();
		}
	}

	private async Task ExitApplicationAsync()
	{
		if (isExiting)
			return;
		isExiting = true;

		try
		{
			await serverManager.DisposeAsync();
		}
		finally
		{
			trayIcon.Dispose();
			Close();
			Application.Current.Exit();
		}
	}
}
