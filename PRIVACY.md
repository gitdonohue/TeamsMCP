# Privacy Policy for TeamsMCP

**Effective Date:** November 22, 2026  
**Publisher:** Charles Donohue

## Overview

TeamsMCP is a Windows desktop application that hosts a local Model Context Protocol (MCP) server for Microsoft Teams via Microsoft Graph.

TeamsMCP is designed to run **entirely locally on your Windows device**. The publisher does not operate any remote servers for TeamsMCP and does **not** collect, track, sell, share, or transmit your personal data, Microsoft Teams messages, credentials, or usage telemetry.

## Data Accessed by TeamsMCP

When you configure a Microsoft Entra ID Application (Client) ID and sign in with your Microsoft 365 account, TeamsMCP communicates directly between your device and Microsoft services (Microsoft Entra ID and Microsoft Graph) using the delegated permissions granted to your app registration, which may include:

- `User.Read` (your signed-in profile)
- `Chat.Read` (your 1:1, group, and meeting chats)
- `Team.ReadBasic.All` and `Channel.ReadBasic.All` (teams and channels you belong to)
- `ChannelMessage.Read.All` (channel posts and thread replies)
- `Files.Read.All` (inline image attachments)
- `ChannelMessage.Send` (posting plain-text messages to channels when channel posting is enabled)

The local MCP HTTP server binds exclusively to the loopback address (`127.0.0.1`) on your configured port and is not exposed to external networks. Data fetched from Microsoft Graph is returned only to local MCP clients you explicitly connect to `http://127.0.0.1:<port>/mcp`.

## Data Stored Locally on Your Device

TeamsMCP stores only the data required to operate locally under your Windows user profile:

- **App Settings:** Stored locally at `%LOCALAPPDATA%\TeamsMCP\settings.json` (Client ID, optional Tenant ID, loopback port, sign-in mode, channel-posting preference, image save directory, and startup/tray options).
- **Authentication Tokens:** When Device Code sign-in is used, OAuth tokens are cached locally using MSAL's token cache protected by Windows Data Protection API (DPAPI), scoped to your Windows user account. When Windows Broker sign-in is used, token management is handled by Windows Account Manager (WAM).
- **Saved Inline Images (Optional):** When inline image saving is enabled and invoked by a connected MCP client, images are written to the local folder you configure (by default, `%LOCALAPPDATA%\TeamsMCP\images`).

## Third-Party Services

- **Microsoft Services:** Sign-in and Microsoft Teams data access occur directly with Microsoft Entra ID and Microsoft Graph and are governed by the [Microsoft Privacy Statement](https://privacy.microsoft.com/privacystatement).
- **Connected Local MCP Clients:** Any local AI assistant or MCP client you point at TeamsMCP (such as Visual Studio Code, GitHub Copilot CLI, or Claude Desktop) processes the Teams data you ask it to retrieve under that client's and AI provider's own privacy policy.

## Data Retention and Deletion

Because all application data resides solely on your local machine:

- You can delete all local settings, cached tokens, and saved images at any time by removing `%LOCALAPPDATA%\TeamsMCP\` or uninstalling TeamsMCP.
- You can revoke Microsoft Graph access at any time through your Microsoft account app permissions or your organization's Microsoft Entra admin center.

## Children's Privacy

TeamsMCP is a developer tool intended for organizational Microsoft 365 accounts and is not directed to children under the age of 13.

## Changes to This Policy

If this privacy policy is updated, the revised version and effective date will be published at this URL.

## Contact

If you have questions about this privacy policy, please open an issue at [https://github.com/gitdonohue/TeamsMCP](https://github.com/gitdonohue/TeamsMCP).
