using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace NetHelp
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
            }
            catch { }

            bool dump = false;
            bool full = false;
            bool uiTest = false;
            bool demoData = false;
            string outFile = null;
            string photosDir = null;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].ToLowerInvariant();
                if (a == "--dump" || a == "/dump" || a == "-d") dump = true;
                else if (a == "--report" || a == "-r") { dump = true; full = true; }
                else if (a == "--uitest" || a == "/uitest") { uiTest = true; dump = true; full = false; }
                else if (a == "--demo" || a == "/demo") { demoData = true; uiTest = true; }
                else if ((a == "--photos" || a == "--shots") && i + 1 < args.Length)
                {
                    photosDir = args[++i];
                    uiTest = true;
                    if (a == "--shots") demoData = true; // снимки для документации — только на вымышленных данных
                }
                else if (a == "--out" && i + 1 < args.Length) outFile = args[++i];
                else if (!a.StartsWith("-") && outFile == null && (a.EndsWith(".txt") || a.EndsWith(".log"))) outFile = args[i];
            }

            if (demoData) Core.DemoMode = true;

            if (uiTest)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                string text = UiAudit.Run(photosDir, demoData);
                return WriteText(outFile, text) ? 0 : 2;
            }

            if (dump) return DumpMode(full, outFile);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e)
            {
                Ui.Err(null, "Непредвиденная ошибка:" + Environment.NewLine + e.Exception.Message);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                Exception ex = e.ExceptionObject as Exception;
                try
                {
                    File.AppendAllText(Path.Combine(AppSettings.DataDir, "nethelp-errors.log"),
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + (ex == null ? "unknown" : ex.ToString()) + Environment.NewLine, Encoding.UTF8);
                }
                catch { }
            };

            Application.Run(new MainForm());
            return 0;
        }

        private static bool WriteText(string outFile, string text)
        {
            bool console = false;
            try
            {
                console = NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
                if (!console) console = NativeMethods.AllocConsole();
            }
            catch { }
            try { Console.OutputEncoding = Shell.Oem; }
            catch { }
            if (!string.IsNullOrEmpty(outFile))
            {
                try
                {
                    string dir = Path.GetDirectoryName(Path.GetFullPath(outFile));
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(outFile, text, Encoding.UTF8);
                    if (console) Console.WriteLine("NetHelp: результат записан в " + Path.GetFullPath(outFile));
                    return true;
                }
                catch (Exception ex)
                {
                    if (console)
                    {
                        Console.Error.WriteLine("NetHelp: ошибка записи: " + ex.Message);
                        Console.WriteLine(text);
                    }
                    return false;
                }
            }
            if (console) Console.WriteLine(text);
            return console;
        }

        /// <summary>Режим без интерфейса: --dump [файл] — сведения об адаптерах, --report — полный отчёт.</summary>
        private static int DumpMode(bool full, string outFile)
        {
            bool console = false;
            try
            {
                console = NativeMethods.AttachConsole(NativeMethods.ATTACH_PARENT_PROCESS);
                if (!console) console = NativeMethods.AllocConsole();
            }
            catch { }
            try { Console.OutputEncoding = Shell.Oem; }
            catch { }

            StringBuilder log = new StringBuilder();
            log.AppendLine("NetHelp — сбор сведений...");
            if (console) Console.WriteLine("NetHelp: сбор сведений о сети...");

            Core core = new Core();
            System.Collections.Generic.List<AdapterInfo> list = Inventory.Collect();
            core.Adapters = list;

            string text;
            if (full) text = Report.BuildFull(core, false);
            else text = Report.Header() + Report.AdaptersList(list);

            if (!string.IsNullOrEmpty(outFile))
            {
                try
                {
                    string dir = Path.GetDirectoryName(Path.GetFullPath(outFile));
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(outFile, text, Encoding.UTF8);
                    log.AppendLine("Отчёт сохранён: " + Path.GetFullPath(outFile));
                    if (console) Console.WriteLine("NetHelp: отчёт сохранён -> " + Path.GetFullPath(outFile));
                    log.AppendLine("Строк: " + text.Split('\n').Length + ", символов: " + text.Length);
                    if (console) Console.WriteLine("NetHelp: строк " + text.Split('\n').Length + ", символов " + text.Length);
                }
                catch (Exception ex)
                {
                    if (console) Console.WriteLine("NetHelp: ошибка записи: " + ex.Message);
                    return 2;
                }
            }
            else if (console)
            {
                Console.WriteLine(text);
            }
            return 0;
        }
    }
}
