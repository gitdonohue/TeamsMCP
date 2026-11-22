using System.Text.Json;

namespace TeamsMCP.Core;

public sealed class SettingsStore
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		PropertyNameCaseInsensitive = true
	};

	public string FilePath { get; }

	public SettingsStore(string? filePath = null)
	{
		FilePath = string.IsNullOrWhiteSpace(filePath)
			? Path.Combine(AppSettings.GetDefaultDataDirectory(), "settings.json")
			: filePath;
	}

	public AppSettings Load()
	{
		try
		{
			if (!File.Exists(FilePath))
				return new AppSettings();

			var json = File.ReadAllText(FilePath);
			var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
			return (loaded ?? new AppSettings()).Normalize();
		}
		catch (Exception)
		{
			return new AppSettings();
		}
	}

	public void Save(AppSettings settings)
	{
		var normalized = settings.Normalize();
		var directory = Path.GetDirectoryName(Path.GetFullPath(FilePath));
		if (!string.IsNullOrEmpty(directory))
			Directory.CreateDirectory(directory);

		var json = JsonSerializer.Serialize(normalized, JsonOptions);
		File.WriteAllText(FilePath, json);
	}
}
