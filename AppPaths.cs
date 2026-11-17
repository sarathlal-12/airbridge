using System;
using System.IO;

namespace AirBridge
{
    /// <summary>
    /// Where things live. Installed build: engine\AirBridgeEngine.exe next to AirBridge.exe, data in
    /// %LOCALAPPDATA%\AirBridge (Program Files isn't writable). Dev build: Python venv engine and
    /// data in the project folder.
    /// </summary>
    internal static class AppPaths
    {
        public static string BundledEngineExe { get; } =
            Path.Combine(AppContext.BaseDirectory, "engine", "AirBridgeEngine.exe");

        public static bool IsInstalled => File.Exists(BundledEngineExe);

        public static string DataDir
        {
            get
            {
                var dir = IsInstalled
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AirBridge")
                    : Path.GetDirectoryName(EngineHost.FindEngineDir())!;
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string LogsDir => Path.Combine(DataDir, "logs");

        /// <summary>Engine working folder: its logs and pairing keys go here.</summary>
        public static string EngineWorkDir
        {
            get
            {
                if (!IsInstalled) return Path.Combine(EngineHost.FindEngineDir(), "airplay2");
                var dir = Path.Combine(DataDir, "engine");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string SettingsFile => Path.Combine(DataDir, "settings.json");
    }
}
