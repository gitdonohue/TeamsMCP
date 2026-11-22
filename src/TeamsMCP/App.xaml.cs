using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using TeamsMCP.Core;
using Windows.ApplicationModel.Activation;

namespace TeamsMCP;

public sealed partial class App : Application
{
	private MainWindow? mainWindow;

	public App()
	{
		InitializeComponent();
	}

	protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
	{
		var currentInstance = AppInstance.GetCurrent();
		var mainInstance = AppInstance.FindOrRegisterForKey("TeamsMCP-MainInstance");
		if (!mainInstance.IsCurrent)
		{
			await mainInstance.RedirectActivationToAsync(currentInstance.GetActivatedEventArgs());
			Exit();
			return;
		}

		mainInstance.Activated += OnMainInstanceActivated;

		var settings = new SettingsStore().Load();
		var launchedByStartupTask = currentInstance.GetActivatedEventArgs().Kind == ExtendedActivationKind.StartupTask;
		var startHidden = launchedByStartupTask || settings.StartMinimizedToTray;

		mainWindow = new MainWindow(startHidden);
		if (!startHidden)
			mainWindow.ShowWindow();
	}

	private void OnMainInstanceActivated(object? sender, AppActivationArguments e)
	{
		mainWindow?.DispatcherQueue.TryEnqueue(() => mainWindow?.ShowWindow());
	}
}
