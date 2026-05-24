using MrExStrap.AppData;
using MrExStrap.Integrations;
using MrExStrap.Models;

namespace MrExStrap
{
    public class Watcher : IDisposable
    {
        private readonly InterProcessLock _lock = new("Watcher");

        private readonly WatcherData? _watcherData;
        
        private readonly NotifyIconWrapper? _notifyIcon;

        public readonly ActivityWatcher? ActivityWatcher;

        public readonly DiscordRichPresence? RichPresence;

        public Watcher()
        {
            const string LOG_IDENT = "Watcher";

            if (!_lock.IsAcquired)
            {
                App.Logger.WriteLine(LOG_IDENT, "Watcher instance already exists");
                return;
            }

            string? watcherDataArg = App.LaunchSettings.WatcherFlag.Data;

            if (String.IsNullOrEmpty(watcherDataArg))
            {
#if DEBUG
                string path = new RobloxPlayerData().ExecutablePath;
                if (!File.Exists(path))
                    throw new ApplicationException("Roblox player is not been installed");

                using var gameClientProcess = Process.Start(path);

                _watcherData = new() { ProcessId = gameClientProcess.Id };
#else
                throw new Exception("Watcher data not specified");
#endif
            }
            else
            {
                _watcherData = JsonSerializer.Deserialize<WatcherData>(Encoding.UTF8.GetString(Convert.FromBase64String(watcherDataArg)));
            }

            if (_watcherData is null)
                throw new Exception("Watcher data is invalid");

            if (App.Settings.Prop.EnableActivityTracking)
            {
                ActivityWatcher = new(_watcherData.LogFile);

                if (App.Settings.Prop.UseDisableAppPatch)
                {
                    ActivityWatcher.OnAppClose += delegate
                    {
                        App.Logger.WriteLine(LOG_IDENT, "Received desktop app exit, closing Roblox");
                        using var process = Process.GetProcessById(_watcherData.ProcessId);
                        process.CloseMainWindow();
                    };
                }

                if (App.Settings.Prop.UseDiscordRichPresence)
                    RichPresence = new(ActivityWatcher);
            }

            _notifyIcon = new(this);
        }

        public void KillRobloxProcess() => CloseProcess(_watcherData!.ProcessId, true);

        public void CloseProcess(int pid, bool force = false)
        {
            const string LOG_IDENT = "Watcher::CloseProcess";

            try
            {
                using var process = Process.GetProcessById(pid);

                App.Logger.WriteLine(LOG_IDENT, $"Killing process '{process.ProcessName}' (pid={pid}, force={force})");

                if (process.HasExited)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"PID {pid} has already exited");
                    return;
                }

                if (force)
                    process.Kill();
                else
                    process.CloseMainWindow();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"PID {pid} could not be closed");
                App.Logger.WriteException(LOG_IDENT, ex);
            }
        }

        public async Task Run()
        {
            const string LOG_IDENT = "Watcher::Run";

            if (!_lock.IsAcquired || _watcherData is null)
                return;

            ActivityWatcher?.Start();

            // v420.23: Roblox sometimes hangs around in Task Manager after the user
            // closes its window (window destroyed, but the process is wedged on a
            // network handle / dying renderer / injected DLL refusing to unload).
            // Pre-v420.23 we just polled the PID forever, which left the zombie
            // process around indefinitely. Now: once we've seen Roblox's main
            // window appear, if the window goes away but the process doesn't exit
            // within a 6s grace period, hard-kill it (process tree included).
            bool windowAppeared = false;
            DateTime? windowGoneSince = null;
            bool forceKilled = false;

            while (Utilities.GetProcessesSafe().Any(x => x.Id == _watcherData.ProcessId))
            {
                if (!forceKilled)
                {
                    try
                    {
                        using var process = Process.GetProcessById(_watcherData.ProcessId);
                        process.Refresh();
                        IntPtr hwnd = process.MainWindowHandle;

                        if (!windowAppeared && hwnd != IntPtr.Zero)
                            windowAppeared = true;

                        if (windowAppeared)
                        {
                            if (hwnd == IntPtr.Zero)
                            {
                                if (windowGoneSince is null)
                                {
                                    windowGoneSince = DateTime.UtcNow;
                                    App.Logger.WriteLine(LOG_IDENT, $"PID {_watcherData.ProcessId}: Roblox window closed but process still alive — 6s grace period before force-kill.");
                                }
                                else if ((DateTime.UtcNow - windowGoneSince.Value).TotalSeconds >= 6)
                                {
                                    App.Logger.WriteLine(LOG_IDENT, $"PID {_watcherData.ProcessId}: still alive after 6s — force-killing process tree.");
                                    try
                                    {
                                        process.Kill(entireProcessTree: true);
                                    }
                                    catch (Exception killEx)
                                    {
                                        App.Logger.WriteException(LOG_IDENT, killEx);
                                    }
                                    forceKilled = true;
                                }
                            }
                            else if (windowGoneSince is not null)
                            {
                                // Window came back (rare — splash screens, alt-tab quirks). Reset.
                                windowGoneSince = null;
                            }
                        }
                    }
                    catch (ArgumentException)
                    {
                        // PID disappeared between the Any() check above and GetProcessById here —
                        // next loop iteration will exit naturally.
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteException(LOG_IDENT, ex);
                    }
                }

                await Task.Delay(1000);
            }

            if (_watcherData.AutoclosePids is not null)
            {
                foreach (int pid in _watcherData.AutoclosePids)
                    CloseProcess(pid);
            }

            if (App.LaunchSettings.TestModeFlag.Active)
                Process.Start(Paths.Process, "-settings -testmode");
        }

        public void Dispose()
        {
            App.Logger.WriteLine("Watcher::Dispose", "Disposing Watcher");

            _notifyIcon?.Dispose();
            RichPresence?.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
