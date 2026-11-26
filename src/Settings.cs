using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace NetHelp
{
    /// <summary>Настройки программы. Хранятся рядом с EXE — работает с флешки.</summary>
    public class AppSettings
    {
        public List<string> PingHosts = new List<string>();
        public int PingCount = 4;
        public int PingTimeout = 2000;
        public int PingSize = 32;
        public bool PingLogReplies = false;
        public string TraceHost = "8.8.8.8";
        public int TraceMaxHops = 30;
        public bool TraceResolve = true;
        public bool ShowHidden = true;
        public bool ShowVirtual = true;
        public bool ShowOnlyPhysical = false;
        public int LastTab = 0;
        public string PortHost = "";
        public string SpeedUrl = "https://speed.cloudflare.com/__down?bytes=50000000";
        public string DnsTestHost = "ya.ru";
        public string NslookupServer = "8.8.8.8";
        public bool ConfirmCommands = true;

        private static string _dataDir;

        public static string DataDir
        {
            get
            {
                if (_dataDir == null)
                {
                    string exeDir = "";
                    try { exeDir = Path.GetDirectoryName(Application.ExecutablePath); }
                    catch { }
                    if (!string.IsNullOrEmpty(exeDir) && IsWritable(exeDir)) _dataDir = exeDir;
                    else
                    {
                        string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NetHelp");
                        try { Directory.CreateDirectory(appData); }
                        catch { }
                        _dataDir = appData;
                    }
                }
                return _dataDir;
            }
        }

        public static string ConfigPath { get { return Path.Combine(DataDir, "nethelp.cfg"); } }
        public static string JournalPath { get { return Path.Combine(DataDir, "nethelp-journal.log"); } }

        public static bool IsPortable { get { return string.Equals(DataDir, SafeExeDir(), StringComparison.OrdinalIgnoreCase); } }

        private static string SafeExeDir()
        {
            try { return Path.GetDirectoryName(Application.ExecutablePath); }
            catch { return ""; }
        }

        private static bool IsWritable(string dir)
        {
            try
            {
                string probe = Path.Combine(dir, ".nethelp_probe");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch { return false; }
        }

        public static AppSettings Defaults()
        {
            AppSettings s = new AppSettings();
            s.PingHosts.Add("8.8.8.8");
            s.PingHosts.Add("1.1.1.1");
            s.PingHosts.Add("ya.ru");
            s.PingHosts.Add("google.com");
            return s;
        }

        public static AppSettings Load()
        {
            AppSettings s = Defaults();
            try
            {
                if (!File.Exists(ConfigPath)) return s;
                s.PingHosts.Clear();
                string[] lines = File.ReadAllLines(ConfigPath, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim();
                    string v = line.Substring(eq + 1).Trim();
                    switch (k)
                    {
                        case "ping": if (v.Length > 0) s.PingHosts.Add(v); break;
                        case "pingCount": s.PingCount = ParseInt(v, s.PingCount); break;
                        case "pingTimeout": s.PingTimeout = ParseInt(v, s.PingTimeout); break;
                        case "pingSize": s.PingSize = ParseInt(v, s.PingSize); break;
                        case "pingLog": s.PingLogReplies = ParseBool(v, s.PingLogReplies); break;
                        case "traceHost": s.TraceHost = v; break;
                        case "traceHops": s.TraceMaxHops = ParseInt(v, s.TraceMaxHops); break;
                        case "traceResolve": s.TraceResolve = ParseBool(v, s.TraceResolve); break;
                        case "showHidden": s.ShowHidden = ParseBool(v, s.ShowHidden); break;
                        case "showVirtual": s.ShowVirtual = ParseBool(v, s.ShowVirtual); break;
                        case "onlyPhysical": s.ShowOnlyPhysical = ParseBool(v, s.ShowOnlyPhysical); break;
                        case "lastTab": s.LastTab = ParseInt(v, s.LastTab); break;
                        case "portHost": s.PortHost = v; break;
                        case "speedUrl": s.SpeedUrl = v; break;
                        case "dnsTestHost": s.DnsTestHost = v; break;
                        case "nslookupServer": s.NslookupServer = v; break;
                        case "confirm": s.ConfirmCommands = ParseBool(v, s.ConfirmCommands); break;
                    }
                }
                if (s.PingHosts.Count == 0) s.PingHosts.AddRange(Defaults().PingHosts);
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# NetHelp — настройки (портативный режим). Файл можно править вручную.");
                sb.AppendLine("# " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                for (int i = 0; i < PingHosts.Count; i++) sb.AppendLine("ping=" + PingHosts[i]);
                sb.AppendLine("pingCount=" + PingCount);
                sb.AppendLine("pingTimeout=" + PingTimeout);
                sb.AppendLine("pingSize=" + PingSize);
                sb.AppendLine("pingLog=" + PingLogReplies);
                sb.AppendLine("traceHost=" + TraceHost);
                sb.AppendLine("traceHops=" + TraceMaxHops);
                sb.AppendLine("traceResolve=" + TraceResolve);
                sb.AppendLine("showHidden=" + ShowHidden);
                sb.AppendLine("showVirtual=" + ShowVirtual);
                sb.AppendLine("onlyPhysical=" + ShowOnlyPhysical);
                sb.AppendLine("lastTab=" + LastTab);
                sb.AppendLine("portHost=" + PortHost);
                sb.AppendLine("speedUrl=" + SpeedUrl);
                sb.AppendLine("dnsTestHost=" + DnsTestHost);
                sb.AppendLine("nslookupServer=" + NslookupServer);
                sb.AppendLine("confirm=" + ConfirmCommands);
                File.WriteAllText(ConfigPath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private static int ParseInt(string v, int def)
        {
            int r;
            if (int.TryParse(v, out r)) return r;
            return def;
        }

        private static bool ParseBool(string v, bool def)
        {
            if (string.IsNullOrEmpty(v)) return def;
            v = v.Trim().ToLowerInvariant();
            if (v == "1" || v == "true" || v == "yes" || v == "да") return true;
            if (v == "0" || v == "false" || v == "no" || v == "нет") return false;
            return def;
        }
    }
}
