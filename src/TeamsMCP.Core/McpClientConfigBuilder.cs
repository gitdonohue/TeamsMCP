using System.Text.Json;

namespace TeamsMCP.Core;

public static class McpClientConfigBuilder
{
	private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

	public static string GetEndpointUrl(int port)
	{
		if (port < AppSettings.MinPort || port > AppSettings.MaxPort)
			throw new ArgumentOutOfRangeException(nameof(port), $"Port must be between {AppSettings.MinPort} and {AppSettings.MaxPort}.");

		return $"http://127.0.0.1:{port}/mcp";
	}

	public static string BuildServerConfigJson(int port, string serverName = "teams-mcp")
	{
		var endpointUrl = GetEndpointUrl(port);
		var payload = new Dictionary<string, object>
		{
			["mcpServers"] = new Dictionary<string, object>
			{
				[serverName] = new Dictionary<string, object>
				{
					["type"] = "http",
					["url"] = endpointUrl
				}
			}
		};
		return JsonSerializer.Serialize(payload, IndentedJson);
	}
}
