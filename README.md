# Bloxstrap - Mr Exploit edition

A hardened fork of [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap) built for the Roblox exploit community.

## Why a fork?

Vanilla Bloxstrap lets Roblox's A/B routing sometimes push you onto test channels (`zlive`, `zintegration`) without warning. When that happens, every popular executor/external stops working until they update for that test build — or you have to wait for Roblox to roll you back to the LIVE channel.

**Bloxstrap - Mr Exploit edition forces the Roblox client to always run on the LIVE (`production`) channel.** Every launch rewrites the Roblox-side channel registry key with retry/verify logic, so you can't accidentally get routed onto a beta build your tools don't support yet.

## What it has that upstream Bloxstrap doesn't

- **LIVE channel lock.** Roblox's A/B test-channel routing is physically blocked. A "CHANNEL: LIVE (locked)" badge appears on every launch so you know it worked.
- **First-class downgrading UX.**
  - Auto-detects the current LIVE version hash from Roblox's own CDN on open — no third-party services required.
  - One-click "Pin this version" to lock to the current build.
  - Paste any version hash and **Verify** against the CDN — shows exact download size, package count, and the deployment date/time.
  - "Match your executor/exploit" dropdown (via weao.xyz) — if your executor is behind the latest Roblox, pick it from the list and the matching historical build is pinned automatically.
- **Richer loading screen.** Version hash + Roblox version number + live "X MB / Y MB" download progress + a `DOWNGRADED` badge when you're on a custom version + the place ID when joining a specific experience.
- **Analytics permanently disabled.** Nothing is ever sent to Bloxstrap upstream or anywhere else. Hardcoded off, not a toggle.
- **Toggleable post-launch "Channel: LIVE" tray notification** confirming the channel lock actually applied.
- **Cleaner surface.** Stripped bootstrapper theming / title overrides / translator lists, and the upstream wiki links that pointed to docs this fork doesn't share.

## Who made this

vibe pasted by **MrExploit** (aka **Sir Meme**):
- Active in the Roblox community since 2017.
- Formerly associated with **Synapse Softworks LLC**.
- Currently runs **[robloxscripts.com](https://robloxscripts.com)** and **[rsware.store](https://rsware.store)**.

## About the unsigned binary

Releases ship as an **unsigned `.exe`**. Windows SmartScreen (and some AV products) will flag it as "Publisher unknown" or similar on first run — that's just how Windows treats any unsigned binary. It's safe to run on any machine. Just click through the warning.

This is a legitimate fork, 100% for the Roblox exploit community. If you'd rather not take my word for it, **clone the repo and build it yourself** — it's a stock .NET 6 WPF project with no obfuscation:

```
git clone --recurse-submodules https://github.com/RealSlimShady2000/MrExLiveChannelForcer.git
cd MrExLiveChannelForcer
dotnet publish MrExStrap/MrExStrap.csproj -p:PublishSingleFile=true -r win-x64 -c Release --self-contained true
```

Output lands at `MrExStrap/bin/Release/net6.0-windows/win-x64/publish/MrExBloxstrap.exe`.

## Install

1. Download the latest `MrExBloxstrap.exe` from the [Releases](https://github.com/RealSlimShady2000/MrExLiveChannelForcer/releases) page.
2. Run it — the installer wizard handles the rest.

The release binary is self-contained — no .NET runtime install required. Install location is `%localappdata%\MrExBloxstrap`.

## Uninstall

Windows `Settings → Apps → Installed apps` (search for "Bloxstrap"), or run `MrExBloxstrap.exe --uninstall`.

## Development notes

vibe coded with claude.

## License

[MIT](./LICENSE), inherited from [upstream Bloxstrap](https://github.com/bloxstraplabs/bloxstrap) by pizzaboxer et al. This fork's changes are © 2026 RealSlimShady2000.
