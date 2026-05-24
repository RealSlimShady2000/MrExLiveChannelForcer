namespace MrExStrap.Utility
{
    // Manages Windows directory junctions used to expose per-profile Roblox
    // install dirs under the standard "Versions\version-<hash>\" name. This is
    // the v420.24 fix for flippi's two reports on v420.23:
    //   1. Same-hash profiles shared one install folder, so an executor (syn z)
    //      installed under one profile would leak into another (wave). With
    //      junctions, each profile has its own real folder at
    //      Versions\profile-<id>\ and only the active profile's junction
    //      Versions\version-<active-hash>\ -> Versions\profile-<active-id>\
    //      exists at any moment.
    //   2. Profile switches re-extracted Roblox packages even when nothing had
    //      changed, because the InstalledVersionGuid empty-string fallback was
    //      broken. Fixed in Bootstrapper.cs; this helper enables the layout
    //      that makes that fix matter.
    //
    // Junctions (mklink /J) do not require admin/Developer-Mode like symbolic
    // links do, so this works for every user. Most executors that parse the
    // install-dir name see the junction path verbatim — they get the
    // version-<hash> name they expect.
    public static class VersionJunctionManager
    {
        private const string LOG_IDENT = "VersionJunctionManager";

        // True when the directory at `path` is a reparse point (junction or
        // symlink). Either is safe to remove via Directory.Delete(path, false).
        public static bool IsJunction(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                    return false;
                var attrs = File.GetAttributes(path);
                return (attrs & FileAttributes.ReparsePoint) != 0;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::IsJunction", ex);
                return false;
            }
        }

        // Create a directory junction at `junctionPath` that resolves to
        // `targetDir`. `junctionPath` must not exist already. `targetDir`
        // should exist (mklink /J accepts a missing target but the resulting
        // junction is broken).
        public static bool CreateJunction(string junctionPath, string targetDir)
        {
            try
            {
                Directory.CreateDirectory(targetDir);

                var psi = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{junctionPath}\" \"{targetDir}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                using var proc = Process.Start(psi);
                if (proc is null)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to start cmd.exe for mklink /J {junctionPath} -> {targetDir}");
                    return false;
                }

                if (!proc.WaitForExit(5000))
                {
                    try { proc.Kill(true); } catch { }
                    App.Logger.WriteLine(LOG_IDENT, $"mklink /J timed out for {junctionPath} -> {targetDir}");
                    return false;
                }

                string stdout = proc.StandardOutput.ReadToEnd().Trim();
                string stderr = proc.StandardError.ReadToEnd().Trim();

                if (proc.ExitCode != 0 || !Directory.Exists(junctionPath) || !IsJunction(junctionPath))
                {
                    string msg = !string.IsNullOrEmpty(stderr) ? stderr
                                 : !string.IsNullOrEmpty(stdout) ? stdout
                                 : $"exit code {proc.ExitCode}";
                    App.Logger.WriteLine(LOG_IDENT, $"mklink /J failed for {junctionPath} -> {targetDir}: {msg}");
                    return false;
                }

                App.Logger.WriteLine(LOG_IDENT, $"Created junction {junctionPath} -> {targetDir}");
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::CreateJunction", ex);
                return false;
            }
        }

        // Remove a junction without affecting its target. Directory.Delete with
        // recursive=false treats reparse points as unlink-only, so the target
        // dir's contents are safe.
        public static bool DeleteJunction(string junctionPath)
        {
            try
            {
                if (!Directory.Exists(junctionPath))
                    return true;
                if (!IsJunction(junctionPath))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Refusing to DeleteJunction on non-junction {junctionPath} — caller must handle real dirs explicitly.");
                    return false;
                }
                Directory.Delete(junctionPath, recursive: false);
                App.Logger.WriteLine(LOG_IDENT, $"Removed junction {junctionPath}");
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::DeleteJunction", ex);
                return false;
            }
        }
    }
}
