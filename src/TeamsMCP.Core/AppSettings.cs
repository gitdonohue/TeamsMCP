namespace TeamsMCP.Core;

public sealed record AppSettings
{
	public const int MinPort = 1024;
	public const int MaxPort = 65535;
	public const int DefaultPort = 5123;
	public const string DefaultClientId = "508ef684-c0c8-4101-8e76-c4046af418fe";

	public string ClientId { get; init; } = DefaultClientId;
	public string TenantId { get; init; } = string.Empty;
	public int Port { get; init; } = DefaultPort;
	public bool UseDeviceCode { get; init; } = true;
	public bool AllowChannelPosting { get; init; } = true;
	public bool EnableImageSave { get; init; } = true;
	public string ImageSaveDirectory { get; init; } = GetDefaultImageSaveDirectory();
	public bool AutoStartServer { get; init; } = false;
	public bool StartMinimizedToTray { get; init; } = false;
	public bool MinimizeToTrayOnClose { get; init; } = true;

	public static string GetDefaultDataDirectory() =>
		Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TeamsMCP");

	public static string GetDefaultImageSaveDirectory() =>
		Path.Combine(GetDefaultDataDirectory(), "images");

	public AppSettings Normalize() => this with
	{
		ClientId = (ClientId ?? string.Empty).Trim(),
		TenantId = (TenantId ?? string.Empty).Trim(),
		Port = Port is >= MinPort and <= MaxPort ? Port : DefaultPort,
		ImageSaveDirectory = string.IsNullOrWhiteSpace(ImageSaveDirectory)
			? GetDefaultImageSaveDirectory()
			: ImageSaveDirectory.Trim()
	};

	public string? ValidateForStart()
	{
		if (string.IsNullOrWhiteSpace(ClientId))
			return "Enter a Microsoft Entra Application (Client) ID before starting the server.";

		if (Port < MinPort || Port > MaxPort)
			return $"MCP port must be between {MinPort} and {MaxPort}.";

		if (EnableImageSave && string.IsNullOrWhiteSpace(ImageSaveDirectory))
			return "Choose an image save folder or disable saving inline images.";

		return null;
	}
}
