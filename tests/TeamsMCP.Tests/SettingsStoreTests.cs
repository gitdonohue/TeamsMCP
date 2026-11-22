using TeamsMCP.Core;
using Xunit;

namespace TeamsMCP.Tests;

public sealed class SettingsStoreTests : IDisposable
{
	private readonly string tempDir = Path.Combine(Path.GetTempPath(), $"TeamsMCP_Tests_{Guid.NewGuid():N}");

	[Fact]
	public void SaveAndLoad_RoundTripsAllProperties()
	{
		var path = Path.Combine(tempDir, "settings.json");
		var store = new SettingsStore(path);
		var expected = new AppSettings
		{
			ClientId = "11111111-2222-3333-4444-555555555555",
			TenantId = "contoso.com",
			Port = 6123,
			UseDeviceCode = false,
			AllowChannelPosting = false,
			EnableImageSave = true,
			ImageSaveDirectory = Path.Combine(tempDir, "imgs"),
			AutoStartServer = true,
			StartMinimizedToTray = true,
			MinimizeToTrayOnClose = false
		};

		store.Save(expected);
		var loaded = store.Load();

		Assert.Equal(expected, loaded);
	}

	[Fact]
	public void Load_ReturnsDefaultsWhenFileIsCorrupt()
	{
		Directory.CreateDirectory(tempDir);
		var path = Path.Combine(tempDir, "settings.json");
		File.WriteAllText(path, "{ not valid json");

		var loaded = new SettingsStore(path).Load();

		Assert.Equal(AppSettings.DefaultPort, loaded.Port);
		Assert.Equal(string.Empty, loaded.ClientId);
		Assert.True(loaded.AllowChannelPosting);
	}

	public void Dispose()
	{
		if (Directory.Exists(tempDir))
			Directory.Delete(tempDir, recursive: true);
	}
}
