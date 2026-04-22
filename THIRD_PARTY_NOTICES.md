# Third-party notices

This project is a fork of [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap) by pizzaboxer et al., MIT-licensed. See [LICENSE](./LICENSE) for the full upstream notice. Additional third-party code adapted into this fork is listed below.

## robloxmanager — privacy mode, multi-instance, window tiling

- **Files:** `MrExStrap/Utility/PrivacyMode.cs`, `MrExStrap/Utility/MultiInstance.cs`, `MrExStrap/Utility/WindowTiler.cs`
- **Source:** [gitlab.com/centerepic/robloxmanager](https://gitlab.com/centerepic/robloxmanager)
- **Author:** [sasha / centerepic](https://gitlab.com/centerepic/robloxmanager)
- **License:** MIT
- **What was ported:**
  - *Privacy mode:* truncating Roblox's `RobloxCookies.dat` to zero bytes before each launch so the client begins a fresh session. The C# port adds a best-effort sweep over this fork's versioned install directories in addition to the default `%LocalAppData%\Roblox\LocalStorage` location.
  - *Multi-instance:* closing the `ROBLOX_singletonEvent` in the running Roblox process after launch so a second Roblox client can start alongside the first. The C# port uses `NtQuerySystemInformation` + `DuplicateHandle(DUPLICATE_CLOSE_SOURCE)` against same-user processes (no admin required).
  - *Auto window tiling:* arranging visible Roblox windows in a grid on the primary monitor after launch via Win32 `EnumWindows` + `SetWindowPos`.
  - No other robloxmanager code was used.
