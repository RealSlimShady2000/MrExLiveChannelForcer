namespace MrExStrap
{
    static class Paths
    {
        // note that these are directories that aren't tethered to the basedirectory
        // so these can safely be called before initialization
        public static string Temp => Path.Combine(Path.GetTempPath(), App.ProjectName);
        public static string UserProfile => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        public static string Desktop => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        public static string WindowsStartMenu => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
        public static string System => Environment.GetFolderPath(Environment.SpecialFolder.System);

        public static string Process => Environment.ProcessPath!;

        public static string TempUpdates => Path.Combine(Temp, "Updates");
        public static string TempLogs => Path.Combine(Temp, "Logs");

        public static string Base { get; private set; } = "";
        public static string Downloads { get; private set; } = "";
        public static string Logs { get; private set; } = "";
        public static string Integrations { get; private set; } = "";
        public static string Versions { get; private set; } = "";
        public static string Modifications { get; private set; } = "";
        public static string CustomThemes { get; private set; } = "";

        public static string Application { get; private set; } = "";

        public static string CustomFont => Path.Combine(Modifications, "content\\fonts\\CustomFont.ttf");

        public static bool Initialized => !String.IsNullOrEmpty(Base);

        // When non-null, Versions + Downloads are stored under this directory instead of Base.
        // Used for fast-portable mode: heavy Roblox binaries cache locally on the host machine
        // while config (Settings/State/Logs/Modifications/CustomThemes) still travels with the
        // portable folder.
        public static string? CacheBase { get; private set; }

        public static void Initialize(string baseDirectory, string? cacheDirectory = null)
        {
            Base = baseDirectory;
            CacheBase = cacheDirectory;

            string heavyRoot = cacheDirectory ?? baseDirectory;

            Downloads = Path.Combine(heavyRoot, "Downloads");
            Versions = Path.Combine(heavyRoot, "Versions");

            Logs = Path.Combine(Base, "Logs");
            Integrations = Path.Combine(Base, "Integrations");
            Modifications = Path.Combine(Base, "Modifications");
            CustomThemes = Path.Combine(Base, "CustomThemes");

            Application = Path.Combine(Base, $"{App.ProjectName}.exe");
        }
    }
}
