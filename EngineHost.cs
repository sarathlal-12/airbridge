using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using Serilog;

namespace AirBridge
{
    public class EngineHost : IDisposable
    {
        private readonly object gate = new();
        private Process? process;
        private JobObject? job;
        private bool stopping;
        private bool disposed;
        private string? currentIface;
        private readonly Timer networkTimer;

        public string DeviceName { get; }
        public event Action<string>? StatusChanged;

        public EngineHost(string deviceName = "AirBridge")
        {
            DeviceName = deviceName;
            // Wi-Fi switched, cable plugged in, network came back: re-check a few seconds later
            // (addresses settle in bursts), then restart the engine on the new network.
            networkTimer = new Timer(_ => OnNetworkSettled(), null, Timeout.Infinite, Timeout.Infinite);
            NetworkChange.NetworkAddressChanged += OnNetworkChanged;
            NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
        }

        public void Start()
        {
            lock (gate)
            {
                if (disposed || process is { HasExited: false }) return;

                var iface = FindInterfaceGuid();
                currentIface = iface;
                if (iface == null)
                {
                    // No usable network yet; OnNetworkSettled starts the engine when one appears.
                    Log.Warning("No network connection; waiting before starting the engine");
                    StatusChanged?.Invoke("Waiting for Wi-Fi");
                    return;
                }

                string exe, args;
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
                args += $" -n \"{iface}\"";

                var psi = new ProcessStartInfo(exe, args)
                {
                    WorkingDirectory = AppPaths.EngineWorkDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                stopping = false;
                var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
                p.OutputDataReceived += (_, e) => OnLine(e.Data);
                p.ErrorDataReceived += (_, e) => OnLine(e.Data);
                p.Exited += (_, _) =>
                {
                    // Ignore the old engine exiting after a deliberate stop or a restart.
                    if (!stopping && ReferenceEquals(p, process))
                    {
                        Log.Warning("Engine exited with code {Code}", p.ExitCode);
                        StatusChanged?.Invoke("Engine stopped");
                    }
                };
                process = p;
                p.Start();
                try
                {
                    job ??= new JobObject();
                    job.Add(p);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Could not attach engine to job object; orphans possible if AirBridge is killed");
                }
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                Log.Information("Engine started (pid {Pid}) as '{Name}' on {Iface}", p.Id, DeviceName, iface);
                StatusChanged?.Invoke("Ready");
            }
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
            lock (gate)
            {
                stopping = true;
                try
                {
                    if (process is { HasExited: false })
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(3000); // free port 7000 before a restart
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error stopping engine");
                }
            }
        }

        private void OnNetworkChanged(object? sender, EventArgs e)
        {
            if (!disposed) networkTimer.Change(3000, Timeout.Infinite);
        }

        private void OnNetworkSettled()
        {
            try
            {
                lock (gate)
                {
                    if (disposed) return;
                    var iface = FindInterfaceGuid();
                    bool running = process is { HasExited: false };
                    if (iface == currentIface && (running || iface == null)) return;

                    Log.Information("Network changed ({Old} -> {New}); restarting engine",
                        currentIface ?? "none", iface ?? "none");
                    Stop();
                    Start();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not restart the engine after a network change");
                StatusChanged?.Invoke("Engine failed");
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

        /// <summary>The network the iPhone is most likely on, as an interface GUID; null if offline.</summary>
        internal static string? FindInterfaceGuid()
        {
            // 1. The interface Windows routes internet traffic through (UDP connect sends nothing).
            try
            {
                using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                s.Connect("8.8.8.8", 65530);
                var local = ((IPEndPoint)s.LocalEndPoint!).Address;
                var id = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.GetIPProperties().UnicastAddresses.Any(a => a.Address.Equals(local)))?.Id;
                if (id != null) return id;
            }
            catch
            {
                // No internet route (router without internet, phone hotspot, offline LAN): fall through.
            }

            // 2. Otherwise the best-looking local network: up, has a real IPv4 address,
            //    Wi-Fi/Ethernet before others, virtual adapters (Hyper-V, VPN, VMs) last.
            try
            {
                return NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up
                             && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                             && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel
                             && n.GetIPProperties().UnicastAddresses.Any(a => IsUsableIPv4(a.Address)))
                    .OrderBy(n => IsVirtual(n) ? 1 : 0)
                    .ThenBy(n => n.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet ? 0 : 1)
                    .ThenBy(n => n.GetIPProperties().GatewayAddresses.Any() ? 0 : 1)
                    .Select(n => n.Id)
                    .FirstOrDefault();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not list network interfaces");
                return null;
            }
        }

        private static bool IsUsableIPv4(IPAddress a) =>
            a.AddressFamily == AddressFamily.InterNetwork
            && !IPAddress.IsLoopback(a)
            && !a.ToString().StartsWith("169.254.", StringComparison.Ordinal); // no DHCP lease

        private static bool IsVirtual(NetworkInterface n)
        {
            var text = (n.Name + " " + n.Description).ToLowerInvariant();
            return new[] { "virtual", "vethernet", "hyper-v", "vmware", "virtualbox", "wsl", "vpn", "tap-", "wireguard", "tailscale", "zerotier", "bluetooth" }
                .Any(text.Contains);
        }

        public void Dispose()
        {
            disposed = true;
            NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
            networkTimer.Dispose();
            Stop();
            job?.Dispose();
            job = null;
        }
    }
}
