using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using Serilog;

namespace AirBridge
{
    /// <summary>User settings, saved as JSON in the data folder.</summary>
    internal sealed class Settings
    {
        /// <summary>Bluetooth device IDs allowed to play through this PC.</summary>
        public HashSet<string> BluetoothPhones { get; set; } = new();

        public static Settings Load()
        {
            try
            {
                if (File.Exists(AppPaths.SettingsFile))
                    return JsonSerializer.Deserialize<Settings>(File.ReadAllText(AppPaths.SettingsFile)) ?? new Settings();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not read settings; using defaults");
            }
            return new Settings();
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not save settings");
            }
        }

        // "Start with Windows" lives in the registry Run key, not the JSON file.
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool StartWithWindows
        {
            get
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue("AirBridge") != null;
            }
            set
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey);
                if (value) key.SetValue("AirBridge", $"\"{Environment.ProcessPath}\"");
                else key.DeleteValue("AirBridge", throwOnMissingValue: false);
            }
        }
    }
}
