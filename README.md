# MrExLiveChannelForcer

A personal fork of [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap) that forces the Roblox client channel to **LIVE** (`production`) on every launch.

Roblox occasionally A/B-tests users onto alternate client channels (`zlive`, `zintegration`, etc.), which can change engine behavior and break tooling that assumes the LIVE build. This fork guarantees you always run the LIVE build, regardless of what Roblox's servers or any registry state would otherwise select.

## Differences from upstream Bloxstrap

- Channel is hardcoded to `production`. CLI `--channel` flags, registry state, and any `channel:...` URI argument are ignored.
- On every launch, the Roblox-side channel registry key is overwritten with an empty string (Roblox interprets empty as LIVE), so external tools can't flip it mid-session.
- Analytics calls are no-ops — nothing is sent anywhere.
- Rebranded installer title, launch menu, icon, install directory (`%localappdata%\MrExploitLiveChannelForcer`), and uninstall key.
- Auto-updater points at this repo's GitHub Releases.

Everything else — the bootstrapper UX, Roblox launch flow, mod manager, FastFlag editor, Discord rich presence, settings UI — is inherited as-is from upstream.

## Install

1. Download the latest `MrExploitLiveChannelForcer-vX.Y.Z.exe` from the [Releases](https://github.com/RealSlimShady2000/MrExLiveChannelForcer/releases) page.
2. Run it. The installer wizard walks you through the rest.

The release binary is self-contained — no .NET runtime install required.

### Uninstall

Via Windows `Settings > Apps > Installed apps` (search for "MrExploit"), or run `MrExploitLiveChannelForcer.exe --uninstall`.

## Verifying the channel lock

After launching Roblox once through this app, open `regedit` and check:

```
HKEY_CURRENT_USER\Software\ROBLOX Corporation\Environments\RobloxPlayer\Channel
```

The `www.roblox.com` value should be an empty string. If you manually set it to `zlive` or anything else, the next launch will overwrite it back to empty.

## Building from source

Prerequisites: .NET 6 SDK, Git.

```bash
git clone --recurse-submodules https://github.com/RealSlimShady2000/MrExLiveChannelForcer.git
cd MrExLiveChannelForcer
dotnet publish Bloxstrap/Bloxstrap.csproj -p:PublishSingleFile=true -r win-x64 -c Release --self-contained true
```

Output: `Bloxstrap/bin/Release/net6.0-windows/win-x64/publish/MrExploitLiveChannelForcer.exe`.

## License

MIT. See [LICENSE](LICENSE).

This is a fork of [Bloxstrap by pizzaboxer](https://github.com/bloxstraplabs/bloxstrap). The original Bloxstrap copyright is preserved; credit for everything except the channel-lock changes belongs upstream.
