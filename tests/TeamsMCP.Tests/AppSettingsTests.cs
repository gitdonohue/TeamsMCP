using TeamsMCP.Core;
using Xunit;

namespace TeamsMCP.Tests;

public sealed class AppSettingsTests
{
	[Fact]
	public void Defaults_EnableChannelPostingByDefault()
	{
		var settings = new AppSettings();
		Assert.True(settings.AllowChannelPosting);
		Assert.Equal(AppSettings.DefaultClientId, settings.ClientId);
	}

	[Fact]
	public void ValidateForStart_RequiresClientId()
	{
		var settings = new AppSettings { ClientId = "   " };
		Assert.NotNull(settings.ValidateForStart());
	}

	[Theory]
	[InlineData(80)]
	[InlineData(1023)]
	[InlineData(65536)]
	public void ValidateForStart_RejectsOutOfRangePorts(int port)
	{
		var settings = new AppSettings { ClientId = "00000000-0000-0000-0000-000000000001", Port = port };
		Assert.NotNull(settings.ValidateForStart());
	}

	[Fact]
	public void ValidateForStart_AllowsEmptyImageDirWhenImageSaveDisabled()
	{
		var settings = new AppSettings
		{
			ClientId = "00000000-0000-0000-0000-000000000001",
			Port = 5123,
			EnableImageSave = false,
			ImageSaveDirectory = ""
		};
		Assert.Null(settings.ValidateForStart());
	}

	[Fact]
	public void Normalize_TrimsStringsAndResetsInvalidPort()
	{
		var settings = new AppSettings
		{
			ClientId = "  abc  ",
			TenantId = "  contoso.onmicrosoft.com  ",
			Port = 12,
			ImageSaveDirectory = "   "
		}.Normalize();

		Assert.Equal("abc", settings.ClientId);
		Assert.Equal("contoso.onmicrosoft.com", settings.TenantId);
		Assert.Equal(AppSettings.DefaultPort, settings.Port);
		Assert.Equal(AppSettings.GetDefaultImageSaveDirectory(), settings.ImageSaveDirectory);
	}
}
