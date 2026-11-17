using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using Windows.Devices.Enumeration;
using Windows.Media.Audio;

namespace AirBridge
{
    /// <summary>
    /// Makes this PC a Bluetooth speaker for paired phones (Android, and iPhone too) using the
    /// Windows 10 2004+ AudioPlaybackConnection API. The phone must be paired with Windows first.
    /// Enabled phones can then connect and play whenever they like.
    /// </summary>
    internal sealed class BluetoothReceiver : IDisposable
    {
        public sealed class Phone
        {
            public required string Id { get; init; }
            public required string Name { get; set; }
            public bool Enabled { get; set; }
            public AudioPlaybackConnectionState State { get; set; } = AudioPlaybackConnectionState.Closed;
            internal AudioPlaybackConnection? Connection { get; set; }
        }

        private readonly Settings settings;
        private readonly Dictionary<string, Phone> phones = new();
        private readonly object gate = new();
        private DeviceWatcher? watcher;

        /// <summary>Raised (on a background thread) when the phone list or a connection changes.</summary>
        public event Action? Changed;

        public BluetoothReceiver(Settings settings)
        {
            this.settings = settings;
        }

        public IReadOnlyList<Phone> Phones
        {
            get { lock (gate) return phones.Values.OrderBy(p => p.Name).ToList(); }
        }

        public void Start()
        {
            try
            {
                watcher = DeviceInformation.CreateWatcher(AudioPlaybackConnection.GetDeviceSelector());
                watcher.Added += (_, info) => OnAdded(info);
                watcher.Updated += (_, upd) =>
                {
                    lock (gate)
                        if (phones.TryGetValue(upd.Id, out var p) && upd.Properties.TryGetValue("System.ItemNameDisplay", out var n) && n is string s)
                            p.Name = s;
                    Changed?.Invoke();
                };
                watcher.Removed += (_, upd) =>
                {
                    Phone? p;
                    lock (gate)
                    {
                        if (!phones.Remove(upd.Id, out p)) return;
                    }
                    Close(p);
                    Changed?.Invoke();
                };
                watcher.Start();
                Log.Information("Bluetooth receiver: watching for paired phones");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Bluetooth receiving unavailable (needs Windows 10 2004+ and Bluetooth)");
            }
        }

        private void OnAdded(DeviceInformation info)
        {
            var phone = new Phone { Id = info.Id, Name = info.Name, Enabled = settings.BluetoothPhones.Contains(info.Id) };
            lock (gate) phones[info.Id] = phone;
            Log.Information("Bluetooth phone available: {Name}", info.Name);
            if (phone.Enabled) _ = EnableAsync(phone, connectNow: false);
            Changed?.Invoke();
        }

        /// <summary>Turn receiving on/off for a phone. On also connects right away.</summary>
        public async Task SetEnabledAsync(Phone phone, bool enabled)
        {
            phone.Enabled = enabled;
            if (enabled) settings.BluetoothPhones.Add(phone.Id);
            else settings.BluetoothPhones.Remove(phone.Id);
            settings.Save();

            if (enabled) await EnableAsync(phone, connectNow: true);
            else Close(phone);
            Changed?.Invoke();
        }

        /// <summary>Connect to a phone that is enabled but not currently connected.</summary>
        public Task ConnectAsync(Phone phone) => EnableAsync(phone, connectNow: true);

        private async Task EnableAsync(Phone phone, bool connectNow)
        {
            try
            {
                if (phone.Connection == null)
                {
                    var conn = AudioPlaybackConnection.TryCreateFromId(phone.Id);
                    if (conn == null)
                    {
                        Log.Warning("Bluetooth: could not create connection for {Name}", phone.Name);
                        return;
                    }
                    conn.StateChanged += (c, _) =>
                    {
                        phone.State = c.State;
                        Log.Information("Bluetooth {Name}: {State}", phone.Name, c.State);
                        Changed?.Invoke();
                    };
                    phone.Connection = conn;
                    // Start = the phone may now connect and play to this PC.
                    await conn.StartAsync();
                }

                if (connectNow && phone.Connection.State != AudioPlaybackConnectionState.Opened)
                {
                    var result = await phone.Connection.OpenAsync();
                    Log.Information("Bluetooth connect {Name}: {Status}", phone.Name, result.Status);
                    phone.State = phone.Connection.State;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Bluetooth: failed to enable {Name}", phone.Name);
            }
            Changed?.Invoke();
        }

        private static void Close(Phone phone)
        {
            try { phone.Connection?.Dispose(); } catch { /* already gone */ }
            phone.Connection = null;
            phone.State = AudioPlaybackConnectionState.Closed;
        }

        public void Dispose()
        {
            try { watcher?.Stop(); } catch { /* not started */ }
            lock (gate)
                foreach (var p in phones.Values) Close(p);
        }
    }
}
