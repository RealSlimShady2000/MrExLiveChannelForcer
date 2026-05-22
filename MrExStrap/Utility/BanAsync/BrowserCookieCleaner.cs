using Microsoft.Data.Sqlite;

namespace MrExStrap.Utility.BanAsync
{
    // Targeted cookie cleaner. Deletes ONLY the cookies whose host matches roblox.com
    // / rbxcdn.com (and a few related Roblox hostnames) from each installed browser's
    // cookie SQLite database. The user's other site cookies are untouched.
    //
    // Browsers lock their cookies DB while running, so we surface a friendly
    // "close the browser and try again" message instead of failing silently.
    public static class BrowserCookieCleaner
    {
        private const string LOG_IDENT = "BrowserCookieCleaner";

        // SQLite error codes for "database is busy/locked" — usually because the browser
        // has the cookies DB open with an exclusive lock.
        private const int SQLITE_BUSY = 5;
        private const int SQLITE_LOCKED = 6;

        // Chromium-family browsers store cookies under either
        //   %LocalAppData%\<Vendor>\<Product>\User Data\<Profile>\Network\Cookies   (Chrome-style)
        //   %AppData%\<Vendor>\<Product>\Network\Cookies                            (Opera-style)
        // The Root column picks the right AppData root and the discovery loop later checks
        // for cookies directly under the configured folder AND under profile subdirs, so both
        // layouts work.
        private enum AppDataRoot { Local, Roaming }

        private static readonly (string Name, AppDataRoot Root, string SubPath)[] ChromiumBrowsers =
        {
            ("Google Chrome",  AppDataRoot.Local,   @"Google\Chrome\User Data"),
            ("Microsoft Edge", AppDataRoot.Local,   @"Microsoft\Edge\User Data"),
            ("Brave",          AppDataRoot.Local,   @"BraveSoftware\Brave-Browser\User Data"),
            ("Opera",          AppDataRoot.Roaming, @"Opera Software\Opera Stable"),
            ("Opera GX",       AppDataRoot.Roaming, @"Opera Software\Opera GX Stable"),
            ("Vivaldi",        AppDataRoot.Local,   @"Vivaldi\User Data"),
            ("Chromium",       AppDataRoot.Local,   @"Chromium\User Data"),
        };

        public class Result
        {
            public int CookiesDeleted { get; set; }
            public int BrowsersScanned { get; set; }
            public int FilesScanned { get; set; }
            public List<string> Skipped { get; } = new();
        }

        public static Result ClearRobloxCookies(Action<string> log)
        {
            var result = new Result();
            string localAppData = Paths.LocalAppData;
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            foreach (var (name, root, subPath) in ChromiumBrowsers)
            {
                string rootDir = root == AppDataRoot.Local ? localAppData : appData;
                string userDataDir = Path.Combine(rootDir, subPath);
                if (!Directory.Exists(userDataDir))
                    continue;

                result.BrowsersScanned++;
                int browserTotal = 0;

                foreach (string cookieFile in EnumerateChromiumCookieFiles(userDataDir))
                {
                    result.FilesScanned++;
                    var (deleted, error) = TryDeleteFromDb(cookieFile, ChromiumDeleteSql);
                    if (error != null)
                    {
                        string msg = $"{name}: skipped {Path.GetFileName(Path.GetDirectoryName(cookieFile) ?? cookieFile)} — {error}";
                        result.Skipped.Add(msg);
                        log(msg);
                        continue;
                    }
                    browserTotal += deleted;
                }

                if (browserTotal > 0)
                    log($"{name}: cleared {browserTotal} Roblox cookie(s)");
                else if (!result.Skipped.Any(s => s.StartsWith(name)))
                    log($"{name}: no Roblox cookies to clear");

                result.CookiesDeleted += browserTotal;
            }

            // Firefox uses a different schema (moz_cookies, host column) and lives under
            // %AppData%\Mozilla\Firefox\Profiles\<id>\cookies.sqlite.
            string firefoxProfiles = Path.Combine(appData, @"Mozilla\Firefox\Profiles");
            if (Directory.Exists(firefoxProfiles))
            {
                result.BrowsersScanned++;
                int ffTotal = 0;

                foreach (string profileDir in Directory.GetDirectories(firefoxProfiles))
                {
                    string cookieFile = Path.Combine(profileDir, "cookies.sqlite");
                    if (!File.Exists(cookieFile))
                        continue;

                    result.FilesScanned++;
                    var (deleted, error) = TryDeleteFromDb(cookieFile, FirefoxDeleteSql);
                    if (error != null)
                    {
                        string msg = $"Firefox: skipped {Path.GetFileName(profileDir)} — {error}";
                        result.Skipped.Add(msg);
                        log(msg);
                        continue;
                    }
                    ffTotal += deleted;
                }

                if (ffTotal > 0)
                    log($"Firefox: cleared {ffTotal} Roblox cookie(s)");
                else if (!result.Skipped.Any(s => s.StartsWith("Firefox")))
                    log("Firefox: no Roblox cookies to clear");

                result.CookiesDeleted += ffTotal;
            }

            if (result.BrowsersScanned == 0)
                log("No supported browsers were found on this user account.");

            return result;
        }

        private static IEnumerable<string> EnumerateChromiumCookieFiles(string userDataDir)
        {
            // Opera-style layout: cookies sit directly under the configured folder, no
            // "Default" subdir. Check this first.
            string rootNew = Path.Combine(userDataDir, "Network", "Cookies");
            if (File.Exists(rootNew))
                yield return rootNew;
            else
            {
                string rootOld = Path.Combine(userDataDir, "Cookies");
                if (File.Exists(rootOld))
                    yield return rootOld;
            }

            // Chrome-style layout: cookies under each profile subdir (Default, Profile 1, …).
            string[] profileDirs;
            try
            {
                profileDirs = Directory.GetDirectories(userDataDir);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::EnumProfiles", ex);
                yield break;
            }

            foreach (string subdir in profileDirs)
            {
                string name = Path.GetFileName(subdir);
                bool looksLikeProfile =
                    name.Equals("Default", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Guest Profile", StringComparison.OrdinalIgnoreCase);
                if (!looksLikeProfile)
                    continue;

                // Newer Chromium puts cookies under Network\Cookies. Fall back to the
                // older flat layout if that file isn't there.
                string networkCookies = Path.Combine(subdir, "Network", "Cookies");
                if (File.Exists(networkCookies))
                {
                    yield return networkCookies;
                    continue;
                }

                string flatCookies = Path.Combine(subdir, "Cookies");
                if (File.Exists(flatCookies))
                    yield return flatCookies;
            }
        }

        // Chromium-family schema: table is `cookies`, hostname column is `host_key`.
        private const string ChromiumDeleteSql =
            "DELETE FROM cookies WHERE " +
            "host_key LIKE '%roblox.com%' OR " +
            "host_key LIKE '%rbxcdn.com%' OR " +
            "host_key LIKE '%robloxlabs.com%';";

        // Firefox schema: table is `moz_cookies`, hostname column is `host`.
        private const string FirefoxDeleteSql =
            "DELETE FROM moz_cookies WHERE " +
            "host LIKE '%roblox.com%' OR " +
            "host LIKE '%rbxcdn.com%' OR " +
            "host LIKE '%robloxlabs.com%';";

        private static (int Deleted, string? Error) TryDeleteFromDb(string dbPath, string sql)
        {
            try
            {
                var csb = new SqliteConnectionStringBuilder
                {
                    DataSource = dbPath,
                    Mode = SqliteOpenMode.ReadWrite,
                    Cache = SqliteCacheMode.Private
                };

                using var connection = new SqliteConnection(csb.ConnectionString);
                connection.Open();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = sql;
                int deleted = cmd.ExecuteNonQuery();
                return (deleted, null);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == SQLITE_BUSY || ex.SqliteErrorCode == SQLITE_LOCKED)
            {
                return (0, "browser is open, cookie file is locked. Close the browser and run again.");
            }
            catch (SqliteException ex) when (ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
            {
                // Profile dir exists but cookies table isn't there — likely a fresh/empty profile.
                return (0, null);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::Delete", ex);
                return (0, ex.Message);
            }
        }
    }
}
