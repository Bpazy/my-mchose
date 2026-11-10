using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MyMchose
{
    // ------------------------------------------------------------------
    // Settings
    // ------------------------------------------------------------------
    internal sealed class AppSettings
    {
        public int RefreshMinutes = 60;
        public bool StartWithWindows = false;
        public bool ShowTextOnIcon = true;

        private static string Dir
        {
            get
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyMchose");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        private static string FilePath { get { return Path.Combine(Dir, "settings.json"); } }
        public static string LogPath { get { return Path.Combine(Dir, "app.log"); } }

        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new AppSettings();
                string json = File.ReadAllText(FilePath, Encoding.UTF8);
                var s = new AppSettings();
                s.RefreshMinutes = ReadInt(json, "RefreshMinutes", 60);
                s.StartWithWindows = ReadBool(json, "StartWithWindows", false);
                s.ShowTextOnIcon = ReadBool(json, "ShowTextOnIcon", true);
                if (s.RefreshMinutes < 1) s.RefreshMinutes = 1;
                return s;
            }
            catch (Exception ex) { Log.Write("settings load failed: " + ex.Message); return new AppSettings(); }
        }

        public void Save()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine("  \"RefreshMinutes\": " + RefreshMinutes + ",");
                sb.AppendLine("  \"StartWithWindows\": " + (StartWithWindows ? "true" : "false") + ",");
                sb.AppendLine("  \"ShowTextOnIcon\": " + (ShowTextOnIcon ? "true" : "false"));
                sb.AppendLine("}");
                File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex) { Log.Write("settings save failed: " + ex.Message); }
        }

        private static int ReadInt(string json, string key, int def)
        {
            var m = System.Text.RegularExpressions.Regex.Match(json, "\"" + key + "\"\\s*:\\s*(-?\\d+)");
            if (m.Success) { int v; if (int.TryParse(m.Groups[1].Value, out v)) return v; }
            return def;
        }

        private static bool ReadBool(string json, string key, bool def)
        {
            var m = System.Text.RegularExpressions.Regex.Match(json, "\"" + key + "\"\\s*:\\s*(true|false)");
            if (m.Success) return m.Groups[1].Value == "true";
            return def;
        }
    }

    // ------------------------------------------------------------------
    // Log
    // ------------------------------------------------------------------
    internal static class Log
    {
        public static void Write(string message)
        {
            try
            {
                File.AppendAllText(AppSettings.LogPath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }
    }

    // ------------------------------------------------------------------
    // HID native
    // ------------------------------------------------------------------
    internal static class HidNative
    {
        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint OPEN_EXISTING = 3;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr tmpl);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr h);
        public const uint FILE_FLAG_OVERLAPPED = 0x40000000;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadFile(IntPtr h, byte[] buffer, int toRead, out int read, ref Overlapped overlapped);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteFile(IntPtr h, byte[] buffer, int toWrite, out int written, ref Overlapped overlapped);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetOverlappedResult(IntPtr h, ref Overlapped overlapped, out int transferred, bool wait);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateEvent(IntPtr attributes, bool manualReset, bool initialState, IntPtr name);
        [DllImport("kernel32.dll")]
        public static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
        [DllImport("kernel32.dll")]
        public static extern bool CancelIo(IntPtr h);

        [StructLayout(LayoutKind.Sequential)]
        public struct Overlapped
        {
            public IntPtr Internal;
            public IntPtr InternalHigh;
            public uint Offset;
            public uint OffsetHigh;
            public IntPtr Event;
        }
        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetPreparsedData(IntPtr h, out IntPtr data);
        [DllImport("hid.dll")]
        public static extern bool HidD_FreePreparsedData(IntPtr data);
        [DllImport("hid.dll")]
        public static extern int HidP_GetCaps(IntPtr data, ref HidpCaps caps);
        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetFeature(IntPtr h, byte[] buffer, int length);
        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_SetFeature(IntPtr h, byte[] buffer, int length);

        [StructLayout(LayoutKind.Sequential)]
        public struct HidpCaps
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        private static readonly Guid HidInterfaceGuid = new Guid("4d1e55b2-f16f-11cf-88cb-001111000030");

        public static string[] FindPaths(string instanceIdFilter)
        {
            var found = new List<string>();
            string baseKey = @"SYSTEM\CurrentControlSet\Control\DeviceClasses\{" + HidInterfaceGuid + "}";
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(baseKey))
                {
                    if (k == null) return found.ToArray();
                    foreach (string name in k.GetSubKeyNames())
                    {
                        if (name.IndexOf(instanceIdFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        found.Add(name.StartsWith("##?#") ? "\\\\?\\" + name.Substring(4) : name);
                    }
                }
            }
            catch (Exception ex) { Log.Write("registry scan failed: " + ex.Message); }
            return found.ToArray();
        }
    }

    // ------------------------------------------------------------------
    // Device access
    // ------------------------------------------------------------------
    internal sealed class MouseReading
    {
        public bool Ok;
        public int Battery = -1;
        public bool Charging;
        public ushort VendorId;
        public ushort ProductId;
        public string Error = "";

        public override string ToString()
        {
            if (!Ok) return "读取失败: " + Error;
            return "电量 " + Battery + "%" + (Charging ? " (充电中)" : "");
        }
    }

    internal sealed class MouseDevice : IDisposable
    {
        private IntPtr _handle = IntPtr.Zero;
        private HidNative.HidpCaps _caps;
        private string _path = "";

        public ushort VendorId { get; private set; }
        public ushort ProductId { get; private set; }

        public static MouseDevice Open()
        {
            string[] paths = HidNative.FindPaths("VID_3837");

            // The configuration channel is the vendor-defined collection MI_02&Col02
            // (usage page 0xFF01, report id 0x4D). Other vendor collections either reject
            // every write or just echo zeros, so pick this one explicitly.
            foreach (string path in paths)
            {
                if (path.IndexOf("MI_02&Col02", StringComparison.OrdinalIgnoreCase) < 0) continue;
                var dev = TryOpen(path);
                if (dev != null) return dev;
            }
            foreach (string path in paths)
            {
                if (path.IndexOf("Col0", StringComparison.OrdinalIgnoreCase) < 0) continue;
                var fallback = TryOpen(path);
                if (fallback != null) return fallback;
            }
            return null;
        }

        private static MouseDevice TryOpen(string path)
        {
            IntPtr h = HidNative.CreateFile(path, HidNative.GENERIC_READ | HidNative.GENERIC_WRITE, 3,
                IntPtr.Zero, HidNative.OPEN_EXISTING, HidNative.FILE_FLAG_OVERLAPPED, IntPtr.Zero);
            if (h == new IntPtr(-1) || h == IntPtr.Zero) return null;

            IntPtr preparsed;
            if (!HidNative.HidD_GetPreparsedData(h, out preparsed)) { HidNative.CloseHandle(h); return null; }
            var caps = new HidNative.HidpCaps();
            HidNative.HidP_GetCaps(preparsed, ref caps);
            HidNative.HidD_FreePreparsedData(preparsed);

            var dev = new MouseDevice();
            dev._handle = h;
            dev._caps = caps;
            dev._path = path;
            dev.VendorId = 0x3837;
            return dev;
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero && _handle != new IntPtr(-1)) HidNative.CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }

        public MouseReading Read()
        {
            var r = new MouseReading();
            r.VendorId = VendorId;
            r.ProductId = ProductId;
            try
            {
                if (_caps.OutputReportByteLength < 4) return Fail("接口不支持输出报文");
                var res = ReadViaOutput();
                if (res != null && res.Ok) return res;
                if (res != null) r.Error = res.Error;
                if (string.IsNullOrEmpty(r.Error)) r.Error = "设备未响应";
            }
            catch (Exception ex) { r.Error = ex.Message; }
            return r;
        }

        /// <summary>
        /// Wireless path: write the request frame as an output report and watch the input reports.
        /// The receiver answers on the same report id; the battery arrives as a status packet.
        /// </summary>
        private MouseReading ReadViaOutput()
        {
            int len = _caps.OutputReportByteLength;
            byte[] cmd = BuildPingFrame(len);
            if (!WriteOverlapped(cmd, len, 1000)) return Fail("写输出报文失败");

            byte[] buf = new byte[_caps.InputReportByteLength];
            DateTime deadline = DateTime.UtcNow.AddSeconds(2.5);
            while (DateTime.UtcNow < deadline)
            {
                int read = ReadOverlapped(buf, buf.Length, 600);
                if (read <= 4) continue;
                MouseReading r = Parse(buf, read);
                if (r.Ok) return r;
            }
            return Fail("未收到电量报文");
        }

        private bool WriteOverlapped(byte[] data, int len, uint timeoutMs)
        {
            IntPtr ev = HidNative.CreateEvent(IntPtr.Zero, true, false, IntPtr.Zero);
            var ov = new HidNative.Overlapped();
            ov.Event = ev;
            int written;
            bool ok = HidNative.WriteFile(_handle, data, len, out written, ref ov);
            if (!ok && Marshal.GetLastWin32Error() != 997) { HidNative.CloseHandle(ev); return false; }
            if (!ok)
            {
                if (HidNative.WaitForSingleObject(ev, timeoutMs) != 0)
                {
                    HidNative.CancelIo(_handle);
                    HidNative.CloseHandle(ev);
                    return false;
                }
                HidNative.GetOverlappedResult(_handle, ref ov, out written, false);
            }
            HidNative.CloseHandle(ev);
            return true;
        }

        private int ReadOverlapped(byte[] buffer, int len, uint timeoutMs)
        {
            IntPtr ev = HidNative.CreateEvent(IntPtr.Zero, true, false, IntPtr.Zero);
            var ov = new HidNative.Overlapped();
            ov.Event = ev;
            int read;
            bool ok = HidNative.ReadFile(_handle, buffer, len, out read, ref ov);
            if (!ok && Marshal.GetLastWin32Error() != 997) { HidNative.CloseHandle(ev); return -1; }
            if (!ok)
            {
                if (HidNative.WaitForSingleObject(ev, timeoutMs) != 0)
                {
                    HidNative.CancelIo(_handle);
                    HidNative.CloseHandle(ev);
                    return 0;
                }
                HidNative.GetOverlappedResult(_handle, ref ov, out read, false);
            }
            HidNative.CloseHandle(ev);
            return read;
        }

        private static byte[] BuildPingFrame(int len)
        {
            // Report id 0x4D on the vendor collection; payload = 01 01 (header) 02 03 (read settings).
            // The answer doubles as a keep-alive and makes the receiver emit its status packets.
            var frame = new byte[len];
            frame[0] = 0x4D;
            frame[1] = 0x01;
            frame[2] = 0x01;
            frame[3] = 0x02;
            frame[4] = 0x03;
            return frame;
        }

        private MouseReading Parse(byte[] data, int length)
        {
            // Battery status packet: command 0x0A, type 0x05, level 0-100, then
            // high nibble = full status, low nibble = charge status.
            for (int off = 0; off + 4 <= length; off++)
            {
                if (data[off] != 0x0A || data[off + 1] != 0x05) continue;
                int level = data[off + 2];
                if (level > 100) continue;
                int flags = data[off + 3];
                var r = new MouseReading();
                r.Ok = true;
                r.Battery = level;
                r.Charging = (flags & 0x0F) == 3;   // 3 = charging, 4 = fully charged
                return r;
            }
            return new MouseReading { Ok = false, Error = "未识别电量报文" };
        }

        /// <summary>
        /// Diagnostic mode: keep the handle open, ping every few seconds and log every packet.
        /// Used to capture the battery status packet the receiver sends on wake / reconnect.
        /// </summary>
        public int Watch(int seconds)
        {
            int packets = 0;
            byte[] buf = new byte[_caps.InputReportByteLength];
            DateTime start = DateTime.UtcNow;
            DateTime lastPing = DateTime.MinValue;
            while ((DateTime.UtcNow - start).TotalSeconds < seconds)
            {
                if ((DateTime.UtcNow - lastPing).TotalSeconds > 5)
                {
                    WriteOverlapped(BuildPingFrame(_caps.OutputReportByteLength), _caps.OutputReportByteLength, 500);
                    lastPing = DateTime.UtcNow;
                }
                int read = ReadOverlapped(buf, buf.Length, 500);
                if (read <= 0) continue;
                packets++;
                Log.Write("[watch] " + Hex(buf, read));
                MouseReading r = Parse(buf, read);
                if (r.Ok) Log.Write("[watch] battery " + r.Battery + "% charging=" + r.Charging);
            }
            return packets;
        }

        private static MouseReading Fail(string error)
        {
            return new MouseReading { Ok = false, Error = error };
        }

        public static string Hex(byte[] data, int count)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < count && i < data.Length; i++) sb.Append(data[i].ToString("X2")).Append(' ');
            return sb.ToString().Trim();
        }
    }

    // ------------------------------------------------------------------
    // Tray icon painter
    // ------------------------------------------------------------------
    internal static class IconPainter
    {
        public static Icon Create(int percent, bool charging, bool showText, int size)
        {
            using (var bmp = new Bitmap(size, size))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.Clear(Color.Transparent);

                Color body = percent < 0 ? Color.Gray : percent <= 20 ? Color.FromArgb(220, 60, 60)
                    : percent <= 50 ? Color.FromArgb(230, 170, 40) : Color.FromArgb(60, 170, 90);

                // battery shell
                int w = size - 4, h = (int)(size * 0.52f);
                int x = 2, y = (size - h) / 2;
                using (var pen = new Pen(body, Math.Max(1f, size / 16f)))
                using (var fill = new SolidBrush(Color.FromArgb(60, body)))
                {
                    g.FillRectangle(fill, x, y, w, h);
                    g.DrawRectangle(pen, x, y, w, h);
                    g.FillRectangle(new SolidBrush(body), x + w, y + h / 3, Math.Max(1, size / 16), Math.Max(1, h / 2));

                    if (percent >= 0)
                    {
                        int innerW = (int)((w - 4) * Math.Min(100, Math.Max(0, percent)) / 100f);
                        g.FillRectangle(new SolidBrush(body), x + 2, y + 2, innerW, h - 4);
                    }
                }

                if (showText)
                {
                    string text = percent < 0 ? "--" : (percent >= 100 ? "100" : percent.ToString());
                    using (var font = new Font("Segoe UI", size * 0.34f, FontStyle.Bold, GraphicsUnit.Pixel))
                    {
                        var sz = g.MeasureString(text, font);
                        float tx = (size - sz.Width) / 2f;
                        float ty = size - sz.Height - (size * 0.02f);
                        using (var shadow = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
                        using (var white = new SolidBrush(Color.White))
                        {
                            g.DrawString(text, font, shadow, tx + 1, ty + 1);
                            g.DrawString(text, font, white, tx, ty);
                        }
                    }
                }

                IntPtr hIcon = bmp.GetHicon();
                Icon icon = Icon.FromHandle(hIcon);
                // Clone so the HICON can be destroyed independently.
                Icon clone = (Icon)icon.Clone();
                DestroyIcon(hIcon);
                return clone;
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }

    // ------------------------------------------------------------------
    // Settings form
    // ------------------------------------------------------------------
    internal sealed class SettingsForm : Form
    {
        private readonly AppSettings _settings;
        private readonly NumericUpDown _minutes;
        private readonly CheckBox _startup;
        private readonly CheckBox _showText;

        public SettingsForm(AppSettings settings)
        {
            _settings = settings;
            Text = "MyMchose 设置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(380, 210);
            Font = new Font("Microsoft YaHei UI", 9f);

            var label = new Label();
            label.Text = "刷新周期（分钟，默认 60）：";
            label.SetBounds(18, 22, 200, 22);
            Controls.Add(label);

            _minutes = new NumericUpDown();
            _minutes.Minimum = 1;
            _minutes.Maximum = 1440;
            _minutes.Value = Math.Min(1440, Math.Max(1, settings.RefreshMinutes));
            _minutes.SetBounds(220, 20, 120, 24);
            Controls.Add(_minutes);

            _showText = new CheckBox();
            _showText.Text = "在托盘图标上显示百分比";
            _showText.SetBounds(18, 58, 300, 24);
            _showText.Checked = settings.ShowTextOnIcon;
            Controls.Add(_showText);

            _startup = new CheckBox();
            _startup.Text = "开机自动启动";
            _startup.SetBounds(18, 88, 300, 24);
            _startup.Checked = settings.StartWithWindows;
            Controls.Add(_startup);

            var ok = new Button();
            ok.Text = "确定";
            ok.SetBounds(180, 150, 84, 30);
            ok.Click += delegate { Apply(); DialogResult = DialogResult.OK; Close(); };
            Controls.Add(ok);

            var cancel = new Button();
            cancel.Text = "取消";
            cancel.SetBounds(276, 150, 84, 30);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        private void Apply()
        {
            _settings.RefreshMinutes = (int)_minutes.Value;
            _settings.StartWithWindows = _startup.Checked;
            _settings.ShowTextOnIcon = _showText.Checked;
            _settings.Save();
            StartupRegistration.Apply(_settings.StartWithWindows);
        }
    }

    internal static class StartupRegistration
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "MyMchose";

        public static void Apply(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null) return;
                    if (enabled) key.SetValue(ValueName, "\"" + Assembly.GetEntryAssembly().Location + "\"");
                    else if (key.GetValue(ValueName) != null) key.DeleteValue(ValueName, false);
                }
            }
            catch (Exception ex) { Log.Write("startup registration failed: " + ex.Message); }
        }
    }

    // ------------------------------------------------------------------
    // Tray application
    // ------------------------------------------------------------------
    internal sealed class TrayContext : ApplicationContext
    {
        private readonly NotifyIcon _icon;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly AppSettings _settings;
        private readonly ToolStripMenuItem _statusItem;
        private readonly ToolStripMenuItem _refreshItem;
        private MouseReading _last;
        private DateTime _lastUpdated = DateTime.MinValue;

        public TrayContext()
        {
            _settings = AppSettings.Load();
            StartupRegistration.Apply(_settings.StartWithWindows);

            _statusItem = new ToolStripMenuItem("正在读取…");
            _statusItem.Enabled = false;

            _refreshItem = new ToolStripMenuItem("立即刷新");
            _refreshItem.Click += delegate { RefreshReading(true); };

            var settingsItem = new ToolStripMenuItem("设置…");
            settingsItem.Click += delegate { OpenSettings(); };

            var exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += delegate { ExitApplication(); };

            var menu = new ContextMenuStrip();
            menu.Items.Add(_statusItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_refreshItem);
            menu.Items.Add(settingsItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            _icon = new NotifyIcon();
            _icon.ContextMenuStrip = menu;
            _icon.Visible = true;
            _icon.Text = "MyMchose";
            _icon.DoubleClick += delegate { RefreshReading(true); };

            _timer = new System.Windows.Forms.Timer();
            _timer.Tick += delegate { RefreshReading(false); };
            ApplyInterval();

            UpdateIcon(-1, false);
            RefreshReading(true);
        }

        private void ApplyInterval()
        {
            int minutes = Math.Max(1, _settings.RefreshMinutes);
            _timer.Interval = minutes * 60 * 1000;
            _timer.Start();
        }

        private void OpenSettings()
        {
            using (var form = new SettingsForm(_settings))
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    ApplyInterval();
                    RefreshReading(true);
                }
            }
        }

        private void RefreshReading(bool manual)
        {
            try
            {
                MouseReading reading;
                using (MouseDevice dev = MouseDevice.Open())
                {
                    if (dev == null)
                    {
                        reading = new MouseReading { Ok = false, Error = "未找到 MCHOSE 设备" };
                    }
                    else
                    {
                        reading = dev.Read();
                    }
                }

                _last = reading;
                _lastUpdated = DateTime.Now;
                Log.Write((manual ? "[manual] " : "[auto] ") + reading);

                if (reading.Ok)
                {
                    UpdateIcon(reading.Battery, reading.Charging);
                    _statusItem.Text = "电量 " + reading.Battery + "%" + (reading.Charging ? "（充电中）" : "");
                    _icon.Text = Truncate("MCHOSE " + reading.Battery + "% · " + _lastUpdated.ToString("HH:mm"));
                }
                else
                {
                    UpdateIcon(-1, false);
                    _statusItem.Text = "读取失败：" + reading.Error;
                    _icon.Text = Truncate("MyMchose · 读取失败");
                }
            }
            catch (Exception ex)
            {
                Log.Write("refresh failed: " + ex);
                _statusItem.Text = "读取异常：" + ex.Message;
            }
        }

        private void UpdateIcon(int percent, bool charging)
        {
            Icon newIcon = IconPainter.Create(percent, charging, _settings.ShowTextOnIcon, 32);
            Icon old = _icon.Icon;
            _icon.Icon = newIcon;
            if (old != null) old.Dispose();
        }

        private static string Truncate(string text)
        {
            // NotifyIcon.Text is limited to 63 characters.
            return text.Length <= 63 ? text : text.Substring(0, 63);
        }

        private void ExitApplication()
        {
            _timer.Stop();
            _icon.Visible = false;
            _icon.Dispose();
            ExitThread();
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--watch")
            {
                int seconds = args.Length > 1 ? int.Parse(args[1]) : 600;
                Log.Write("--- watch " + seconds + "s ---");
                using (MouseDevice dev = MouseDevice.Open())
                {
                    if (dev == null) { Log.Write("watch: device not found"); return; }
                    int n = dev.Watch(seconds);
                    Log.Write("watch: done, packets=" + n);
                }
                return;
            }

            bool created;
            using (var mutex = new Mutex(true, "MyMchose.SingleInstance", out created))
            {
                if (!created) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Log.Write("--- start ---");
                Application.Run(new TrayContext());
            }
        }
    }
}
