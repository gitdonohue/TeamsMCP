using Windows.ApplicationModel;

namespace TeamsMCP.Services;

public sealed record StartupTaskStatus(bool IsSupported, bool IsEnabled, string StatusNote);

/// <summary>
/// Manages the MSIX StartupTask extension for Microsoft Store compliant launch-at-login.
/// Gracefully reports unsupported when running unpackaged.
/// </summary>
public static class StartupTaskService
{
	public const string TaskId = "TeamsMCPStartupTask";

	public static async Task<StartupTaskStatus> GetStatusAsync()
	{
		try
		{
			var task = await StartupTask.GetAsync(TaskId);
			return FromState(task.State);
		}
		catch (Exception)
		{
			return new StartupTaskStatus(false, false, "Available when installed as an MSIX / Microsoft Store package.");
		}
	}

	public static async Task<StartupTaskStatus> SetEnabledAsync(bool enable)
	{
		try
		{
			var task = await StartupTask.GetAsync(TaskId);
			if (enable)
			{
				var state = await task.RequestEnableAsync();
				return FromState(state);
			}

			task.Disable();
			return FromState(task.State);
		}
		catch (Exception)
		{
			return new StartupTaskStatus(false, false, "Available when installed as an MSIX / Microsoft Store package.");
		}
	}

	private static StartupTaskStatus FromState(StartupTaskState state) => state switch
	{
		StartupTaskState.Enabled => new StartupTaskStatus(true, true, "Enabled at Windows sign-in."),
		StartupTaskState.EnabledByPolicy => new StartupTaskStatus(false, true, "Enabled by system policy."),
		StartupTaskState.Disabled => new StartupTaskStatus(true, false, "Disabled."),
		StartupTaskState.DisabledByUser => new StartupTaskStatus(false, false, "Disabled in Windows Task Manager / Startup Apps."),
		StartupTaskState.DisabledByPolicy => new StartupTaskStatus(false, false, "Disabled by system policy."),
		_ => new StartupTaskStatus(false, false, "Unavailable.")
	};
}
