using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Serilog;
using Windows.Media.Audio;

namespace AirBridge
{
    public class MainForm : Form
    {
        private readonly NotifyIcon trayIcon = new();
        private readonly ContextMenuStrip trayMenu = new();
        private readonly ToolStripMenuItem statusItem = new("iPhone (AirPlay): Starting...") { Enabled = false };
        private readonly ToolStripMenuItem bluetoothMenu = new("Android / Bluetooth phones");
        private readonly ToolStripMenuItem startupItem = new("Start with Windows") { CheckOnClick = true };
        private readonly Settings settings = Settings.Load();
        private EngineHost? engine;
        private BluetoothReceiver? bluetooth;

        public MainForm()
        {
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            Opacity = 0;

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            trayIcon.Icon = LoadIcon();
            trayIcon.Text = "AirBridge";
            trayMenu.Items.Add(new ToolStripMenuItem($"AirBridge {version?.ToString(3)}") { Enabled = false, Font = new Font(trayMenu.Font, FontStyle.Bold) });
            trayMenu.Items.Add(statusItem);
            trayMenu.Items.Add(bluetoothMenu);
            trayMenu.Items.Add(new ToolStripSeparator());

            startupItem.Checked = Settings.StartWithWindows;
            startupItem.CheckedChanged += (_, _) =>
            {
                try { Settings.StartWithWindows = startupItem.Checked; }
                catch (Exception ex) { Log.Warning(ex, "Could not change start with Windows"); }
            };
            trayMenu.Items.Add(startupItem);
            trayMenu.Items.Add("Open Windows sound settings", null, (_, _) => Open("ms-settings:sound"));
            trayMenu.Items.Add("Open logs folder", null, (_, _) => Open(AppPaths.LogsDir));
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Exit", null, (_, _) => Close());
            trayMenu.Opening += (_, _) => RebuildBluetoothMenu();
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.Visible = true;

            _ = Handle;
            RebuildBluetoothMenu();
            StartEngine();
            StartBluetooth();
        }

        protected override void SetVisibleCore(bool value) => base.SetVisibleCore(false);

        private static Icon LoadIcon()
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("app_icon.ico");
            return s != null ? new Icon(s) : SystemIcons.Application;
        }

        private static void Open(string target)
        {
            try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
            catch (Exception ex) { Log.Warning(ex, "Could not open {Target}", target); }
        }

        private void OnUi(Action action)
        {
            if (!IsHandleCreated || IsDisposed) return;
            BeginInvoke(action);
        }

        private void StartEngine()
        {
            try
            {
                engine = new EngineHost("AirBridge");
                engine.StatusChanged += s => OnUi(() =>
                {
                    statusItem.Text = $"iPhone (AirPlay): {s}";
                    trayIcon.Text = $"AirBridge - {s}";
                });
                engine.Start();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start AirPlay engine");
                statusItem.Text = "iPhone (AirPlay): Engine failed";
                MessageBox.Show(ex.Message, "AirBridge", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StartBluetooth()
        {
            bluetooth = new BluetoothReceiver(settings);
            bluetooth.Changed += () => OnUi(RebuildBluetoothMenu);
            bluetooth.Start();
        }

        private void RebuildBluetoothMenu()
        {
            bluetoothMenu.DropDownItems.Clear();
            var phones = bluetooth?.Phones ?? Array.Empty<BluetoothReceiver.Phone>();

            if (phones.Count == 0)
                bluetoothMenu.DropDownItems.Add(new ToolStripMenuItem("No paired phones yet") { Enabled = false });

            foreach (var phone in phones)
            {
                var item = new ToolStripMenuItem($"{phone.Name}  ({Describe(phone)})");
                var allow = new ToolStripMenuItem("Let this phone play here") { Checked = phone.Enabled };
                allow.Click += async (_, _) => await bluetooth!.SetEnabledAsync(phone, !phone.Enabled);
                item.DropDownItems.Add(allow);
                if (phone.Enabled && phone.State != AudioPlaybackConnectionState.Opened)
                {
                    var connect = new ToolStripMenuItem("Connect now");
                    connect.Click += async (_, _) => await bluetooth!.ConnectAsync(phone);
                    item.DropDownItems.Add(connect);
                }
                bluetoothMenu.DropDownItems.Add(item);
            }

            bluetoothMenu.DropDownItems.Add(new ToolStripSeparator());
            bluetoothMenu.DropDownItems.Add("Pair a new phone...", null, (_, _) => Open("ms-settings:bluetooth"));

            int connected = 0;
            foreach (var p in phones) if (p.State == AudioPlaybackConnectionState.Opened) connected++;
            bluetoothMenu.Text = connected > 0
                ? $"Android / Bluetooth phones ({connected} connected)"
                : "Android / Bluetooth phones";
        }

        private static string Describe(BluetoothReceiver.Phone phone) =>
            !phone.Enabled ? "off" :
            phone.State == AudioPlaybackConnectionState.Opened ? "connected" : "ready";

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            bluetooth?.Dispose();
            engine?.Dispose();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            base.OnFormClosing(e);
        }
    }
}
