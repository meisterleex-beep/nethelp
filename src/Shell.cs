using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NetHelp
{
    /// <summary>Результат запуска внешней команды.</summary>
    public class CmdResult
    {
        public string FileName = "";
        public string Arguments = "";
        public int ExitCode = -1;
        public string Output = "";
        public double Seconds = 0;
        public bool TimedOut = false;
        public bool Started = false;

        public string CommandLine { get { return FileName + " " + Arguments; } }
        public bool Ok { get { return Started && !TimedOut && ExitCode == 0; } }
    }

    /// <summary>Запуск консольных утилит Windows со скрытым окном и перехватом вывода.</summary>
    public static class Shell
    {
        private static Encoding _oem;
        private static bool? _admin;

        public static Encoding Oem
        {
            get
            {
                if (_oem == null)
                {
                    int cp = 866;
                    try { cp = CultureInfo.CurrentCulture.TextInfo.OEMCodePage; }
                    catch { }
                    try { _oem = Encoding.GetEncoding(cp); }
                    catch { try { _oem = Encoding.GetEncoding(866); } catch { _oem = Encoding.Default; } }
                }
                return _oem;
            }
        }

        public static bool IsAdmin
        {
            get
            {
                if (!_admin.HasValue)
                {
                    bool v = false;
                    try
                    {
                        using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                        {
                            WindowsPrincipal p = new WindowsPrincipal(id);
                            v = p.IsInRole(WindowsBuiltInRole.Administrator);
                        }
                    }
                    catch { }
                    _admin = v;
                }
                return _admin.Value;
            }
        }

        public static string System32
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.System); }
        }

        public static string Tool(string name)
        {
            string p = Path.Combine(System32, name);
            if (File.Exists(p)) return p;
            return name;
        }

        public static CmdResult Run(string fileName, string arguments, int timeoutMs)
        {
            CmdResult r = new CmdResult();
            r.FileName = fileName;
            r.Arguments = arguments;
            StringBuilder sb = new StringBuilder();
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(fileName, arguments);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Oem;
                psi.StandardErrorEncoding = Oem;
                psi.WorkingDirectory = System32;

                using (Process p = new Process())
                {
                    p.StartInfo = psi;
                    p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                    {
                        if (e.Data != null) { lock (sb) sb.AppendLine(e.Data); }
                    };
                    p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                    {
                        if (e.Data != null) { lock (sb) sb.AppendLine(e.Data); }
                    };
                    p.Start();
                    r.Started = true;
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(timeoutMs))
                    {
                        r.TimedOut = true;
                        try { p.Kill(); } catch { }
                    }
                    else
                    {
                        p.WaitForExit();
                        r.ExitCode = p.ExitCode;
                    }
                }
            }
            catch (Exception ex)
            {
                lock (sb) sb.AppendLine("Не удалось выполнить команду: " + ex.Message);
                r.ExitCode = -1;
            }
            sw.Stop();
            r.Seconds = sw.Elapsed.TotalSeconds;
            lock (sb) r.Output = sb.ToString().TrimEnd();
            if (r.TimedOut) r.Output += Environment.NewLine + "(прервано по таймауту " + (timeoutMs / 1000) + " с)";
            return r;
        }

        public static CmdResult Run(string fileName, string arguments)
        {
            return Run(fileName, arguments, 180000);
        }

        public static Task<CmdResult> RunAsync(string fileName, string arguments, int timeoutMs)
        {
            return Task.Factory.StartNew<CmdResult>(delegate { return Run(fileName, arguments, timeoutMs); });
        }

        public static Task<CmdResult> RunAsync(string fileName, string arguments)
        {
            return RunAsync(fileName, arguments, 180000);
        }

        /// <summary>Перезапуск программы с запросом UAC.</summary>
        public static bool RelaunchElevated(string arguments)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath, arguments == null ? "" : arguments);
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                psi.WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath);
                Process.Start(psi);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsElevationError(CmdResult r)
        {
            if (r == null) return false;
            if (r.ExitCode == 5) return true;
            string o = (r.Output ?? "").ToLowerInvariant();
            return o.Contains("отказано в доступе") || o.Contains("access is denied") ||
                   o.Contains("requires elevation") || o.Contains("требуются права") ||
                   o.Contains("requested operation requires elevation");
        }
    }
}
