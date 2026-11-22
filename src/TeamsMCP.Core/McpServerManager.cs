using Microsoft.Identity.Client;
using MsGraphMcpProvider;

namespace TeamsMCP.Core;

public enum ServerState
{
	Stopped,
	Starting,
	Running,
	Error
}

public sealed record DeviceCodePrompt(string UserCode, string VerificationUrl, string Message);

public sealed record ServerStatusSnapshot(
	ServerState State,
	string StatusMessage,
	string? EndpointUrl,
	string? SignedInUser,
	DateTimeOffset? TokenExpiresOn,
	bool ChannelPostingEnabled,
	DeviceCodePrompt? ActiveDeviceCode);

public sealed class McpServerManager : IAsyncDisposable
{
	private readonly SemaphoreSlim lifecycleLock = new(1, 1);
	private MsGraphMcpProvider.MsGraphMcpProvider? provider;
	private MsalGraphClient? graphClient;
	private TeamsChannelWriteAccess? writeAccess;
	private CancellationTokenSource? signInCts;
	private ServerStatusSnapshot snapshot = new(
		ServerState.Stopped,
		"Server stopped.",
		null,
		null,
		null,
		false,
		null);

	public ServerStatusSnapshot Snapshot => snapshot;

	public event EventHandler<ServerStatusSnapshot>? StatusChanged;
	public event EventHandler<DeviceCodePrompt>? DeviceCodePrompted;

	public async Task StartAsync(AppSettings settings, IntPtr parentWindowHandle = default, CancellationToken cancellationToken = default)
	{
		var normalized = settings.Normalize();
		var validationError = normalized.ValidateForStart();
		if (validationError != null)
		{
			UpdateSnapshot(snapshot with
			{
				State = ServerState.Error,
				StatusMessage = validationError,
				ActiveDeviceCode = null
			});
			throw new InvalidOperationException(validationError);
		}

		await lifecycleLock.WaitAsync(cancellationToken);
		try
		{
			if (provider != null)
				await StopInternalAsync();

			UpdateSnapshot(new ServerStatusSnapshot(
				ServerState.Starting,
				$"Starting MCP server on port {normalized.Port}...",
				null,
				null,
				null,
				normalized.AllowChannelPosting,
				null));

			var client = new MsalGraphClient(
				normalized.ClientId,
				normalized.TenantId,
				useDeviceCode: normalized.UseDeviceCode,
				hwnd: parentWindowHandle,
				persistTokenCache: true);

			var channelWrite = new TeamsChannelWriteAccess { Enabled = normalized.AllowChannelPosting };
			var mcpProvider = new MsGraphMcpProvider.MsGraphMcpProvider();
			if (normalized.EnableImageSave)
			{
				mcpProvider.ImageSave = new TeamsImageSaveOptions
				{
					Directory = normalized.ImageSaveDirectory
				};
			}

			await mcpProvider.StartAsync(normalized.Port, client, cancellationToken, writeAccess: channelWrite);

			provider = mcpProvider;
			graphClient = client;
			writeAccess = channelWrite;

			var endpoint = McpClientConfigBuilder.GetEndpointUrl(normalized.Port);
			UpdateSnapshot(new ServerStatusSnapshot(
				ServerState.Running,
				$"MCP server running at {endpoint}",
				endpoint,
				null,
				null,
				channelWrite.Enabled,
				null));

			if (normalized.UseDeviceCode)
			{
				signInCts = new CancellationTokenSource();
				_ = RunDeviceCodeSignInAsync(client, signInCts.Token);
			}
		}
		catch (Exception ex)
		{
			await StopInternalAsync();
			UpdateSnapshot(new ServerStatusSnapshot(
				ServerState.Error,
				$"Failed to start server: {ex.Message}",
				null,
				null,
				null,
				false,
				null));
			throw;
		}
		finally
		{
			lifecycleLock.Release();
		}
	}

	public async Task StopAsync()
	{
		await lifecycleLock.WaitAsync();
		try
		{
			await StopInternalAsync();
			UpdateSnapshot(new ServerStatusSnapshot(
				ServerState.Stopped,
				"Server stopped.",
				null,
				null,
				null,
				false,
				null));
		}
		finally
		{
			lifecycleLock.Release();
		}
	}

	public bool TrySetChannelPostingEnabled(bool enabled)
	{
		if (writeAccess == null)
			return false;

		writeAccess.Enabled = enabled;
		UpdateSnapshot(snapshot with { ChannelPostingEnabled = enabled });
		return true;
	}

	public async Task<AuthenticationResult?> RefreshTokenAsync(CancellationToken cancellationToken = default)
	{
		var client = graphClient;
		if (client == null)
			return null;

		var result = await client.RefreshTokenAsync(cancellationToken: cancellationToken);
		if (graphClient == client)
		{
			if (result == null)
			{
				UpdateSnapshot(snapshot with
				{
					StatusMessage = "Token refresh failed. Acquire a new token to sign in again."
				});
			}
			else
			{
				UpdateSnapshot(snapshot with
				{
					SignedInUser = result.Account.Username,
					TokenExpiresOn = result.ExpiresOn,
					StatusMessage = $"Token refreshed for {result.Account.Username}. Expires {result.ExpiresOn.LocalDateTime:t}."
				});
			}
		}
		return result;
	}

	public async Task<AuthenticationResult?> AcquireNewTokenAsync(CancellationToken cancellationToken = default)
	{
		var client = graphClient;
		if (client == null)
			return null;

		UpdateSnapshot(snapshot with { ActiveDeviceCode = null });
		try
		{
			var result = await client.AcquireNewTokenAsync(
				showCode: code => HandleDeviceCode(client, code),
				cancellationToken: cancellationToken);

			if (graphClient == client)
			{
				UpdateSnapshot(snapshot with
				{
					SignedInUser = result.Account.Username,
					TokenExpiresOn = result.ExpiresOn,
					ActiveDeviceCode = null,
					StatusMessage = $"Signed in as {result.Account.Username}. Token expires {result.ExpiresOn.LocalDateTime:t}."
				});
			}
			return result;
		}
		catch (OperationCanceledException)
		{
			if (graphClient == client)
				UpdateSnapshot(snapshot with { ActiveDeviceCode = null });
			throw;
		}
		catch (Exception ex)
		{
			if (graphClient == client)
			{
				UpdateSnapshot(snapshot with
				{
					ActiveDeviceCode = null,
					StatusMessage = $"Sign-in failed: {ex.Message}"
				});
			}
			throw;
		}
	}

	private async Task RunDeviceCodeSignInAsync(MsalGraphClient client, CancellationToken cancellationToken)
	{
		try
		{
			var cached = await client.TrySignInSilentAsync(cancellationToken: cancellationToken);
			if (cached != null)
			{
				if (graphClient == client)
				{
					UpdateSnapshot(snapshot with
					{
						SignedInUser = cached.Account.Username,
						TokenExpiresOn = cached.ExpiresOn,
						ActiveDeviceCode = null,
						StatusMessage = $"Signed in as {cached.Account.Username} (saved session). MCP tools are ready."
					});
				}
				return;
			}

			var result = await client.SignInWithDeviceCodeAsync(
				code => HandleDeviceCode(client, code),
				cancellationToken: cancellationToken);

			if (graphClient == client)
			{
				UpdateSnapshot(snapshot with
				{
					SignedInUser = result.Account.Username,
					TokenExpiresOn = result.ExpiresOn,
					ActiveDeviceCode = null,
					StatusMessage = $"Signed in as {result.Account.Username}. MCP tools are ready."
				});
			}
		}
		catch (OperationCanceledException)
		{
			// Server stopped while waiting for sign-in.
		}
		catch (Exception ex)
		{
			if (graphClient == client)
			{
				UpdateSnapshot(snapshot with
				{
					ActiveDeviceCode = null,
					StatusMessage = $"Sign-in failed: {ex.Message}"
				});
			}
		}
	}

	private void HandleDeviceCode(MsalGraphClient client, DeviceCodeResult code)
	{
		if (graphClient != client)
			return;

		var prompt = new DeviceCodePrompt(code.UserCode, code.VerificationUrl.ToString(), code.Message);
		UpdateSnapshot(snapshot with
		{
			StatusMessage = code.Message,
			ActiveDeviceCode = prompt
		});
		DeviceCodePrompted?.Invoke(this, prompt);
	}

	private async Task StopInternalAsync()
	{
		var cts = signInCts;
		signInCts = null;
		if (cts != null)
		{
			try { await cts.CancelAsync(); } catch (ObjectDisposedException) { }
			cts.Dispose();
		}

		var currentProvider = provider;
		provider = null;
		graphClient = null;
		writeAccess = null;

		if (currentProvider != null)
			await currentProvider.DisposeAsync();
	}

	private void UpdateSnapshot(ServerStatusSnapshot next)
	{
		snapshot = next;
		StatusChanged?.Invoke(this, next);
	}

	public async ValueTask DisposeAsync()
	{
		await StopAsync();
		lifecycleLock.Dispose();
	}
}
