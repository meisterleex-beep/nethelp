using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NetHelp
{
    /// <summary>Общее состояние приложения, связь между вкладками и журнал действий.</summary>
    public class Core
    {
        public AppSettings Settings;
        public List<AdapterInfo> Adapters = new List<AdapterInfo>();
        public AdapterInfo Selected;
        public bool Busy;
        internal readonly SemaphoreSlim CommandGate = new SemaphoreSlim(1, 1);

        public event EventHandler InventoryChanged;
        public event EventHandler SelectionChanged;
        public event Action<string, string, string> JournalEntry; // kind, text, time
        public event Action<string> PingRequested;
        public event Action<string> TraceRequested;

        public Core()
        {
            Settings = AppSettings.Load();
        }

        public void Log(string kind, string text)
        {
            if (JournalEntry != null)
            {
                try { JournalEntry(kind, text, DateTime.Now.ToString("HH:mm:ss")); }
                catch { }
            }
            if (kind == "cmd" || kind == "err")
            {
                try
                {
                    File.AppendAllText(AppSettings.JournalPath,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text + Environment.NewLine, Encoding.UTF8);
                }
                catch { }
            }
        }

        public void LogCommand(CmdResult r)
        {
            Log("cmd", "> " + r.CommandLine);
            if (!string.IsNullOrEmpty(r.Output)) Log(r.Ok ? "out" : "err", r.Output);
            Log(r.Ok ? "ok" : "err", "код возврата: " + r.ExitCode + "   (" + r.Seconds.ToString("0.0") + " с)");
        }

        public Task ReloadAsync()
        {
            Busy = true;
            return Task.Factory.StartNew(delegate
            {
                return Inventory.Collect();
            }).ContinueWith(delegate(Task<List<AdapterInfo>> t)
            {
                Busy = false;
                if (t.Result != null)
                {
                    Adapters = t.Result;
                    if (Selected != null)
                    {
                        AdapterInfo again = null;
                        for (int i = 0; i < Adapters.Count; i++)
                            if (Adapters[i].Guid.Length > 0 && string.Equals(Adapters[i].Guid, Selected.Guid, StringComparison.OrdinalIgnoreCase)) { again = Adapters[i]; break; }
                        Selected = again;
                    }
                }
                if (InventoryChanged != null) InventoryChanged(this, EventArgs.Empty);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        public void SelectAdapter(AdapterInfo a)
        {
            Selected = a;
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }

        public void RequestPing(string host)
        {
            if (PingRequested != null) PingRequested(host);
        }

        public void RequestTrace(string host)
        {
            if (TraceRequested != null) TraceRequested(host);
        }

        public AdapterInfo FindByConnection(string conn)
        {
            for (int i = 0; i < Adapters.Count; i++)
                if (string.Equals(Adapters[i].ConnectionName, conn, StringComparison.OrdinalIgnoreCase)) return Adapters[i];
            return null;
        }
    }

    public class RunOptions
    {
        public string Title = "Выполнение команд";
        public bool NeedsAdmin = true;
        public bool Confirm = true;
        public bool Danger = false;
        public bool RefreshAfter = true;
        public bool ContinueOnError = true;
        public int TimeoutMs = 180000;
        public bool WriteJournal = true;
    }

    /// <summary>Выполнение последовательности команд с подтверждением, проверкой прав и журналом.</summary>
    public static class Runner
    {
        public static void Split(string line, out string file, out string args)
        {
            line = line.Trim();
            int sp = line.IndexOf(' ');
            if (sp < 0) { file = line; args = ""; return; }
            file = line.Substring(0, sp);
            args = line.Substring(sp + 1);
        }

        public static string ResolveExe(string name)
        {
            if (name.IndexOf('\\') >= 0) return name;
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                string p = Path.Combine(Shell.System32, name);
                if (File.Exists(p)) return p;
                return name;
            }
            string full = Path.Combine(Shell.System32, name + ".exe");
            if (File.Exists(full)) return full;
            return name;
        }

        public static CmdResult RunLine(string line, int timeoutMs)
        {
            string f, a;
            Split(line, out f, out a);
            a = a.Replace("%TEMP%", Path.GetTempPath().TrimEnd('\\'));
            return Shell.Run(ResolveExe(f), a, timeoutMs);
        }

        public static bool EnsureAdmin(IWin32Window owner, Core core)
        {
            if (Shell.IsAdmin) return true;
            bool yes = Ui.Ask(owner, "Эта операция изменяет системные настройки и требует прав администратора." +
                Environment.NewLine + Environment.NewLine +
                "Перезапустить NetHelp с правами администратора?" +
                Environment.NewLine + Environment.NewLine +
                "Текущее окно будет закрыто, программа откроется заново уже с полными правами.");
            if (yes)
            {
                if (Shell.RelaunchElevated("")) Application.Exit();
                else Ui.Err(owner, "Не удалось перезапустить программу с правами администратора.");
            }
            return false;
        }

        private static bool TryStart(IWin32Window owner, Core core)
        {
            if (core.CommandGate.Wait(0)) return true;

            const string message = "Дождитесь завершения текущей операции. Если она запущена в консоли, закройте её окно после завершения команд.";
            core.Log("info", message);
            if (owner != null) Ui.Warn(owner, message);
            return false;
        }

        /// <summary>Выполняет список команд по порядку. Поддерживает паузы вида "@wait:2500".</summary>
        public static async Task<bool> Run(IWin32Window owner, Core core, string[] commands, RunOptions opt,
            Func<string[]> prepareCommands = null)
        {
            if (opt == null) opt = new RunOptions();
            if ((commands == null || commands.Length == 0) && prepareCommands == null) return false;
            if (!TryStart(owner, core)) return false;

            try
            {
                if (opt.NeedsAdmin && !EnsureAdmin(owner, core)) return false;

                if (opt.Confirm && core.Settings.ConfirmCommands && commands != null)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("Будут выполнены команды:");
                    sb.AppendLine();
                    for (int i = 0; i < commands.Length; i++) sb.AppendLine("    " + commands[i]);
                    if (opt.Danger) sb.AppendLine(Environment.NewLine + "ВНИМАНИЕ: операция изменяет системные настройки и может потребовать перезагрузки.");
                    if (!Ui.AskDanger(owner, opt.Title, sb.ToString())) return false;
                }

                if (prepareCommands != null)
                {
                    commands = await Task.Run(prepareCommands);
                    if (commands.Length == 0)
                    {
                        const string message = "Разделы реестра уже отсутствуют.";
                        core.Log("info", message);
                        if (owner != null) Ui.Msg(owner, message);
                        return false;
                    }
                }

                core.Log("info", "=== " + opt.Title + " ===");
                bool hadError = false;
                for (int i = 0; i < commands.Length; i++)
                {
                    string line = commands[i];
                    if (line.StartsWith("@wait:"))
                    {
                        int ms;
                        if (int.TryParse(line.Substring(6), out ms) && ms > 0)
                        {
                            core.Log("info", "… ожидание " + (ms / 1000.0).ToString("0.#") + " с");
                            await Task.Delay(ms);
                        }
                        continue;
                    }
                    string f, a;
                    Split(line, out f, out a);
                    a = a.Replace("%TEMP%", Path.GetTempPath().TrimEnd('\\'));
                    string exe = ResolveExe(f);
                    core.Log("cmd", "> " + exe + (a.Length > 0 ? " " + a : ""));
                    CmdResult r = await Shell.RunAsync(exe, a, opt.TimeoutMs);
                    if (!string.IsNullOrEmpty(r.Output)) core.Log(r.Ok ? "out" : "err", r.Output);
                    core.Log(r.Ok ? "ok" : "err", "код возврата: " + r.ExitCode + "   (" + r.Seconds.ToString("0.0") + " с)");
                    if (!r.Ok)
                    {
                        hadError = true;
                        if (Shell.IsElevationError(r))
                        {
                            core.Log("err", "Операция требует прав администратора — последовательность остановлена.");
                            break;
                        }
                        if (!opt.ContinueOnError) break;
                    }
                }
                core.Log(hadError ? "err" : "ok", "=== " + opt.Title + ": завершено" + (hadError ? " с ошибками (см. выше)" : "") + " ===");

                if (opt.RefreshAfter && !core.Busy)
                {
                    try { await core.ReloadAsync(); }
                    catch { }
                }
                return true;
            }
            catch (Exception ex)
            {
                string message = opt.Title + " — операция остановлена." + Environment.NewLine + ex.Message;
                core.Log("err", message);
                if (owner != null) Ui.Err(owner, message);
                return false;
            }
            finally
            {
                core.CommandGate.Release();
            }
        }

        public static async void RunSingle(IWin32Window owner, Core core, string title, string command, bool needsAdmin, bool danger, bool confirm)
        {
            RunOptions o = new RunOptions();
            o.Title = title;
            o.NeedsAdmin = needsAdmin;
            o.Danger = danger;
            o.Confirm = confirm;
            await Run(owner, core, new string[] { command }, o);
        }

        /// <summary>Открывает окно консоли с командой — чтобы видеть живой вывод.</summary>
        public static void OpenConsole(string command)
        {
            OpenConsole(command, false);
        }

        public static void OpenConsole(string command, bool elevate)
        {
            try
            {
                using (Process process = StartConsole(command, elevate)) { }
            }
            catch (Exception ex)
            {
                Ui.Err(null, "Не удалось открыть консоль: " + ex.Message);
            }
        }

        private static Process StartConsole(string command, bool elevate)
        {
            ProcessStartInfo psi = new ProcessStartInfo(Shell.Tool("cmd.exe"), "/k " + command);
            psi.UseShellExecute = true;
            psi.WorkingDirectory = Shell.System32;
            if (elevate) psi.Verb = "runas";
            return Process.Start(psi);
        }

        public static async Task RunInConsole(IWin32Window owner, Core core, string title, string command, bool elevate)
        {
            if (!TryStart(owner, core)) return;

            try
            {
                using (Process process = StartConsole(command, elevate))
                {
                    core.Log("info", "Выполнение в консоли: " + title);
                    await Task.Run(delegate { process.WaitForExit(); });
                }
            }
            catch (Exception ex)
            {
                string message = "Не удалось выполнить команды в консоли: " + ex.Message;
                core.Log("err", message);
                Ui.Err(owner, message);
            }
            finally
            {
                core.CommandGate.Release();
            }
        }

        internal static string[] PrepareRegistryDeletion(RegistryKey hive, List<string> subKeys, string comment)
        {
            List<string> commands = new List<string>();
            foreach (string subKey in subKeys)
            {
                using (RegistryKey existing = hive.OpenSubKey(subKey))
                    if (existing == null) continue;

                string path = hive.Name + "\\" + subKey;
                if (ExportRegistryKey(path, comment) == null)
                    throw new IOException("Не удалось создать резервную копию раздела:" + Environment.NewLine + path);
                commands.Add("reg delete \"" + path + "\" /f");
            }
            return commands.ToArray();
        }

        public static string ExportRegistryKey(string regPath, string comment)
        {
            try
            {
                string dir = Path.Combine(AppSettings.DataDir, "backup");
                Directory.CreateDirectory(dir);
                string safe = regPath.Replace("\\", "_").Replace("{", "").Replace("}", "").Replace(":", "");
                if (safe.Length > 64) safe = safe.Substring(0, 64);
                string name = DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + safe + "-" + Guid.NewGuid().ToString("N") + ".reg";
                string file = Path.Combine(dir, name);
                CmdResult r = Shell.Run(ResolveExe("reg.exe"), "export \"" + regPath + "\" \"" + file + "\" /y", 30000);
                if (r.Ok && File.Exists(file))
                {
                    if (comment != null && comment.Length > 0)
                    {
                        string line = comment.Replace('\r', ' ').Replace('\n', ' ');
                        File.AppendAllText(file, Environment.NewLine + "; " + line + Environment.NewLine, Encoding.Unicode);
                    }
                    return file;
                }
                return null;
            }
            catch { return null; }
        }
    }
}
