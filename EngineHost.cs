using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Serilog;

namespace AirBridge
{
    public class EngineHost : IDisposable
    {
        private Process? process;
        private JobObject? job;
        private bool stopping;

        public string DeviceName { get; }
        public event Action<string>? StatusChanged;

        public EngineHost(string deviceName = "AirBridge")
        {
            DeviceName = deviceName;
        }

        public void Start()
        {
            if (process is { HasExited: false }) return;

            string exe, args;
            var iface = FindActiveInterfaceGuid();
            if (AppPaths.IsInstalled)
            {
                // Installed: frozen engine, normal log level.
                exe = AppPaths.BundledEngineExe;
                args = $"-m \"{DeviceName}\"";
            }
            else
            {
                // Dev: Python venv, verbose logs.
                var engineDir = FindEngineDir();
                exe = Path.Combine(engineDir, "venv", "Scripts", "python.exe");
                var script = Path.Combine(engineDir, "airplay2", "ap2-receiver.py");
                if (!File.Exists(exe) || !File.Exists(script))
                    throw new FileNotFoundException($"AirPlay engine not found in {engineDir}");
                args = $"-u \"{script}\" -m \"{DeviceName}\" --debug";
            }
            if (iface != null) args += $" -n \"{iface}\"";

            var psi = new ProcessStartInfo(exe, args)
            {
                WorkingDirectory = AppPaths.EngineWorkDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            stopping = false;
            process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) => OnLine(e.Data);
            process.ErrorDataReceived += (_, e) => OnLine(e.Data);
            process.Exited += (_, _) =>
            {
                if (!stopping)
                {
                    Log.Warning("Engine exited with code {Code}", process?.ExitCode);
                    StatusChanged?.Invoke("Engine stopped");
                }
            };
            process.Start();
            try
            {
                job ??= new JobObject();
                job.Add(process);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not attach engine to job object; orphans possible if AirBridge is killed");
            }
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            Log.Information("Engine started (pid {Pid}) as '{Name}' on {Iface}", process.Id, DeviceName, iface ?? "default");
            StatusChanged?.Invoke("Ready");
        }

        private void OnLine(string? line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            Log.Debug("[engine] {Line}", line);
            if (line.Contains("SETUP", StringComparison.OrdinalIgnoreCase)) StatusChanged?.Invoke("Streaming");
            else if (line.Contains("TEARDOWN", StringComparison.OrdinalIgnoreCase)) StatusChanged?.Invoke("Ready");
        }

        public void Stop()
        {
            stopping = true;
            try
            {
                if (process is { HasExited: false })
                    process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error stopping engine");
            }
        }

        internal static string FindEngineDir()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "engine");
                if (Directory.Exists(Path.Combine(candidate, "airplay2"))) return candidate;
                dir = dir.Parent;
            }
            return Path.Combine(AppContext.BaseDirectory, "engine");
        }

        private static string? FindActiveInterfaceGuid()
        {
            try
            {
                using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                s.Connect("8.8.8.8", 65530);
                var local = ((IPEndPoint)s.LocalEndPoint!).Address;
                return NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.GetIPProperties().UnicastAddresses.Any(a => a.Address.Equals(local)))?.Id;
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            Stop();
            job?.Dispose();
            job = null;
        }
    }
}
