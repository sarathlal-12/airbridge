using System;
using System.Windows.Forms;
using Serilog;

namespace AirBridge
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // Only one instance: two engines would advertise the same receiver and fight over ports.
            using var mutex = new System.Threading.Mutex(true, @"Local\AirBridge.SingleInstance", out bool isFirst);
            if (!isFirst) return;

            // Installed: %LOCALAPPDATA%\AirBridge\logs. Dev: <project>\logs. Keep a week.
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(System.IO.Path.Combine(AppPaths.LogsDir, "airbridge-.txt"),
                    rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7,
                    fileSizeLimitBytes: 20_000_000, rollOnFileSizeLimit: true)
                .CreateLogger();

            try
            {
                Log.Information("AirBridge starting...");

                ApplicationConfiguration.Initialize();
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
