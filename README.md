# TeamsMCP

WinUI 3 desktop front end and system-tray host for the [`MsGraphMcpProvider`](https://www.nuget.org/packages/MsGraphMcpProvider) NuGet package, packaged as an MSIX app for the Microsoft Store.

## Features

- **System tray icon** with live running/stopped indicator, tooltip, click-to-open, and right-click context menu (Open, Start/Stop Server, Copy MCP URL, Refresh Token, Exit).
- **Configuration UI** for Microsoft Entra Client ID, optional Tenant ID, sign-in mode (Device Code with encrypted token cache or Windows Broker), loopback port (`127.0.0.1` only), channel posting (`send_channel_message`, enabled by default), and inline image saving folder.
- **Startup & tray behavior**: auto-start MCP server on launch, start minimized to tray, minimize to tray on window close, and Microsoft Store compliant launch-at-Windows-sign-in (`windows.startupTask`).
- **Single-instance enforcement**: relaunching TeamsMCP brings the existing configuration window to the foreground.
- **Ready-to-copy MCP client config** for VS Code, GitHub Copilot CLI, and Claude Desktop (`http://127.0.0.1:<port>/mcp`).

## Project Structure

- `TeamsMCP.slnx` — Solution file.
- `src/TeamsMCP.Core` — Core settings persistence, MCP server lifecycle manager (`McpServerManager`), and client config generator.
- `src/TeamsMCP` — WinUI 3 MSIX application (`Package.appxmanifest`, `MainWindow`, native Win32 `TrayIconService`, `StartupTaskService`, Store visual assets).
- `tests/TeamsMCP.Tests` — xUnit tests for `TeamsMCP.Core`.

## Building & Testing

```powershell
# Run unit tests
dotnet test tests\TeamsMCP.Tests\TeamsMCP.Tests.csproj

# Build the WinUI 3 MSIX app (x64)
dotnet build src\TeamsMCP\TeamsMCP.csproj -p:Platform=x64 -p:Configuration=Release
```

## Publishing to the Microsoft Store

1. Reserve your app name in **Microsoft Partner Center** and associate the project in Visual Studio (**Project → Publish → Associate App with the Store**), or update `<Identity Name="..." Publisher="..." />` and `<PublisherDisplayName>` in `src/TeamsMCP/Package.appxmanifest` to match your Partner Center identity.
2. Build an `.msixupload` bundle for `x64` and `ARM64` via Visual Studio (**Project → Publish → Create App Packages → Microsoft Store**) or 64-bit MSBuild:
   ```powershell
   msbuild src\TeamsMCP\TeamsMCP.csproj /restore /p:Configuration=Release /p:Platform=x64 /p:GenerateAppxPackageOnBuild=true /p:UapAppxPackageBuildMode=StoreUpload /p:AppxBundle=Always /p:AppxBundlePlatforms="x64|arm64"
   ```
3. Run the **Windows App Certification Kit (WACK)** locally before submitting to Partner Center.

## Microsoft Entra App Registration

1. Register an app in Microsoft Entra ID and copy the **Application (client) ID** (and optional **Directory (tenant) ID**).
2. Under **Authentication**, add a Mobile and desktop applications platform and enable **Allow public client flows** (required for device-code sign-in).
3. Add delegated Microsoft Graph permissions: `User.Read`, `Chat.Read`, `Team.ReadBasic.All`, `Channel.ReadBasic.All`, `ChannelMessage.Read.All`, `Files.Read.All`, and `ChannelMessage.Send` (used for channel posting, enabled by default).
