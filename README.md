# MrExBloxstrap

**The Roblox launcher built for executor and exploit users.**

A fork of [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap) hardened against the things that usually break executors — surprise channel routing, version updates that ship before your tool catches up, and ban traces left on your machine — plus a load of quality-of-life extras.

---

## At a glance

✅ **LIVE channel lock** — forces Roblox onto production every launch. Fixes most "my executor broke after a Roblox update" cases on its own. If it doesn't, the downgrade tab below handles it.
✅ **One-click downgrading** with CDN verification + auto-match-your-executor dropdown (weao.xyz)
✅ **BanAsync tab** — clean Roblox traces, spoof MAC, randomize MachineGuid, optional Roblox-only browser cookie wipe (Chrome / Edge / Firefox / Brave / Opera / Vivaldi)
✅ **Multi-instance + auto window tiling** — run several Roblox clients at once in a tidy grid
✅ **VIP server picker** before launch (rbxservers.xyz)
✅ **Auto-update with a real progress bar** — fires both on Roblox launch and on menu open
✅ **One-click diagnostic snapshot** zip for bug reports
✅ **Privacy first** — Roblox tracking cookies wiped before every launch, analytics hardcoded off
✅ **Detailed error messages** — reasons shown inline, not buried in logs

---

## How it compares

### vs vanilla Bloxstrap (the upstream project)

**Pros of MrExBloxstrap**

✅ LIVE channel lock — vanilla Bloxstrap leaves channel routing alone
✅ Built-in BanAsync tab (trace cleanup, MAC spoofing, MachineGuid, cookie cleanup) — not in vanilla
✅ Match-your-executor dropdown driven by weao.xyz — not in vanilla
✅ Per-version pin with CDN verification + deployment timestamp — vanilla has version downgrading but the UX is less detailed
✅ Auto-update prompt also fires when opening the menu directly — vanilla only checks on Roblox launch
✅ Diagnostic-snapshot zip for bug reports — not in vanilla
✅ Analytics permanently off (no toggle, hardcoded)

**Cons of MrExBloxstrap**

- Smaller user base, less battle-tested than upstream
- Releases are unsigned, so Windows SmartScreen warns on first run
- Stripped some of vanilla's polish (custom translator credits, broader theming options) to keep the surface focused
- Built specifically for exploit/executor users — if you only play vanilla Roblox, you don't need most of this

### vs Fishstrap (another popular Bloxstrap fork)

**Pros of MrExBloxstrap**

✅ Exploit-first focus — channel lock and executor version matching are the headline features
✅ BanAsync tab combines trace cleanup, MAC spoofing, MachineGuid randomize, and selective cookie wiping in one place
✅ Diagnostic snapshot built in for easier troubleshooting
✅ Auto-update on menu open with a determinate progress bar

**Cons of MrExBloxstrap**

- Fishstrap is aimed at the broader Roblox community and ships polish for general players (themes, custom assets) that MrExBloxstrap doesn't bother with
- Fishstrap has a larger user base and faster issue feedback
- If you're not running an executor, Fishstrap is probably a better fit

### TL;DR

| Pick this if you… | Use |
| --- | --- |
| Run executors/externals and want them to keep working | **MrExBloxstrap** |
| Want a polished player launcher with broad theme support | Fishstrap |
| Want the official upstream with the largest user base | Bloxstrap |

---

## Features in detail

### LIVE channel lock
Roblox sometimes A/B-routes your account onto a test channel like `zlive` or `zintegration` without warning. When that happens, every popular executor stops working until they catch up to that build, or until Roblox rolls you back. MrExBloxstrap rewrites the Roblox-side channel registry key on every launch and verifies the write took. A `CHANNEL: LIVE (locked)` badge appears on the bootstrapper so you know it worked.

### Downgrading
Pin Roblox to any historical build by version hash. The Downgrading tab:
- Auto-detects the current LIVE hash from Roblox's CDN on open (no third-party services)
- Lets you paste any hash and verify it still exists on the CDN
- Shows exact download size, package count, and deployment date
- Has a **Match your executor** dropdown powered by weao.xyz — if your executor is behind, pick it and we pin the right historical Roblox build for you

### BanAsync tab
Optional cleanup + spoofing tools inspired by Technitium MAC Address Changer and similar utilities. Everything is opt-in via toggles, and the Activity log shows you exactly what got changed:
- Clean Roblox traces (caches, logs, prefetch, HKCU registry)
- Optional clearing of `roblox.com` cookies from Chrome, Edge, Firefox, Brave, Opera, Vivaldi (other site cookies are NEVER touched — surgical SQL DELETE by host)
- MAC address spoofing across all detected adapters with OUI mirror, DHCP refresh, and per-adapter Revert
- MachineGuid randomize, gated behind an "I understand the risk" toggle

### Debug mode
Toggle in Settings → Bloxstrap → Debug mode. Exposes:
- Run health check (sanity test of every subsystem)
- Open log folder (jump straight to the log directory)
- **Save diagnostic snapshot** — builds a timestamped zip containing your settings, every log file, environment info, detected network adapters, running Roblox processes, a health check, and a fresh GitHub update probe. Hand the one zip to whoever's helping you debug.
- Open debug folder (where snapshots land)

### Auto-update
Checks GitHub for new releases when you launch Roblox AND when you open the menu directly. Shows a determinate progress bar during the download, classifies failures so the dialog tells you *why* it failed (DNS, TLS, rate limit, disk full, etc.) instead of a generic "something went wrong".

---

## Install

1. Download the latest `MrExBloxstrap-vX.Y.exe` from the [Releases page](https://github.com/RealSlimShady2000/MrExLiveChannelForcer/releases).
2. Run it. The installer handles the rest.

The release binary is self-contained — no .NET runtime install required. Install location is `%localappdata%\MrExBloxstrap`.

To uninstall: Windows **Settings → Apps → Installed apps**, search for "Bloxstrap", or run `MrExBloxstrap.exe --uninstall`.

---

## About the unsigned binary

Releases ship as an **unsigned `.exe`**. Windows SmartScreen (and some antivirus) flag unsigned binaries as "Publisher unknown" on first run — that's just how Windows treats any unsigned program. Click through the warning. The binary is safe.

Don't want to take my word for it? **Build it yourself** — it's a stock .NET 6 WPF project with no obfuscation:

```
git clone --recurse-submodules https://github.com/RealSlimShady2000/MrExLiveChannelForcer.git
cd MrExLiveChannelForcer
dotnet publish MrExStrap/MrExStrap.csproj -p:PublishSingleFile=true -r win-x64 -c Release --self-contained true
```

Output lands at `MrExStrap/bin/Release/net6.0-windows/win-x64/publish/MrExBloxstrap.exe`.

You can also compare the `SHA256SUMS` attached to every release against the exe you build yourself.

---

## Who made this

vibe pasted by **MrExploit** (aka **Sir Meme**):
- Active in the Roblox community since 2017
- Formerly associated with **Synapse Softworks LLC**
- Currently runs **[robloxscripts.com](https://robloxscripts.com)** and **[rsware.store](https://rsware.store)**

---

## Development notes

vibe coded with claude.

## License

[MIT](./LICENSE), inherited from [upstream Bloxstrap](https://github.com/bloxstraplabs/bloxstrap) by pizzaboxer et al. This fork's changes are © 2026 RealSlimShady2000.
