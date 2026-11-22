using System.Text.Json;
using TeamsMCP.Core;
using Xunit;

namespace TeamsMCP.Tests;

public sealed class McpClientConfigBuilderTests
{
	[Fact]
	public void GetEndpointUrl_FormatsLoopbackUrl()
	{
		Assert.Equal("http://127.0.0.1:5123/mcp", McpClientConfigBuilder.GetEndpointUrl(5123));
	}

	[Fact]
	public void GetEndpointUrl_ThrowsForInvalidPort()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => McpClientConfigBuilder.GetEndpointUrl(80));
	}

	[Fact]
	public void BuildServerConfigJson_ProducesValidHttpMcpConfig()
	{
		var json = McpClientConfigBuilder.BuildServerConfigJson(5432);
		using var doc = JsonDocument.Parse(json);
		var server = doc.RootElement.GetProperty("mcpServers").GetProperty("teams-mcp");

		Assert.Equal("http", server.GetProperty("type").GetString());
		Assert.Equal("http://127.0.0.1:5432/mcp", server.GetProperty("url").GetString());
	}

	[Fact]
	public async Task McpServerManager_StartWithMissingClientId_SetsErrorState()
	{
		await using var manager = new McpServerManager();
		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			manager.StartAsync(new AppSettings { ClientId = "" }));

		Assert.Equal(ServerState.Error, manager.Snapshot.State);
		Assert.Null(manager.Snapshot.EndpointUrl);
	}
}
