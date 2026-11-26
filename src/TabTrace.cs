using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NetHelp
{
    /// <summary>Трассировка маршрута, pathping, nslookup и подбор MTU.</summary>
    public class TabTrace : UserControl
    {
        private Core _core;
        private TextBox _host;
        private NumericUpDown _hops;
        private NumericUpDown _timeout;
        private CheckBox _cbResolve;
        private DataGridView _grid;
        private RichTextBox _out;
        private TextBox _dnsServer;
        private Button _btnGo;
        private Button _btnStop;
        private bool _running;
        private bool _stopFlag;

        public TabTrace(Core core)
        {
            _core = core;
            Dock = DockStyle.Fill;
            BackColor = Ui.Bg;

            // ---------------- параметры ----------------
            FlowLayoutPanel top = new FlowLayoutPanel();
            top.Dock = DockStyle.Top;
            top.AutoSize = true;
            top.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            top.Padding = new Padding(2, 6, 0, 0);
            top.WrapContents = true;

            top.Controls.Add(Ui.Label("Узел (имя или IP):", 0, 4));
            _host = Ui.Text(0, 0, 200);
            _host.Text = core.Settings.TraceHost;
            _host.Margin = new Padding(2, 1, 12, 0);
            _host.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { StartWith(_host.Text.Trim()); e.SuppressKeyPress = true; }
            };
            top.Controls.Add(_host);

            top.Controls.Add(Ui.Label("Макс. прыжков:", 0, 4));
            _hops = Num(1, 64, core.Settings.TraceMaxHops, 56);
            top.Controls.Add(_hops);
            top.Controls.Add(Ui.Label("Таймаут, мс:", 0, 4));
            _timeout = Num(500, 10000, 3000, 66);
            _timeout.Increment = 500;
            top.Controls.Add(_timeout);
            _cbResolve = Ui.Check("определять имена узлов", 0, 6, core.Settings.TraceResolve, null);
            top.Controls.Add(_cbResolve);

            _btnGo = Ui.Button("Трассировка", 0, 1, 120, delegate { StartWith(_host.Text.Trim()); });
            top.Controls.Add(_btnGo);
            _btnStop = Ui.Button("Стоп", 0, 1, 70, delegate { _stopFlag = true; });
            _btnStop.Enabled = false;
            top.Controls.Add(_btnStop);
            top.Controls.Add(Ui.Button("Очистить", 0, 1, 90, delegate { _grid.Rows.Clear(); _out.Clear(); }));

            // Инструменты трассировки: переносятся по строкам, ничего не обрезается.
            FlowLayoutPanel tools = new FlowLayoutPanel();
            tools.Dock = DockStyle.Top;
            tools.AutoSize = true;
            tools.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tools.WrapContents = true;
            tools.Padding = new Padding(2, 2, 0, 4);

            Button bSysTrace = Ui.Button("Системный tracert", 0, 0, 140, delegate { RawTrace(); });
            bSysTrace.Margin = new Padding(0, 0, 6, 4);
            tools.Controls.Add(bSysTrace);
            Button bPathPing = Ui.Button("pathping (медленно, 1-3 мин)", 0, 0, 200, delegate { PathPing(); });
            bPathPing.Margin = new Padding(0, 0, 6, 4);
            tools.Controls.Add(bPathPing);
            Button bNslookup = Ui.Button("nslookup", 0, 0, 90, delegate { Nslookup(); });
            bNslookup.Margin = new Padding(0, 0, 8, 4);
            tools.Controls.Add(bNslookup);

            Label lDnsServer = Ui.Label("DNS-сервер:", 0, 0);
            lDnsServer.Margin = new Padding(2, 7, 2, 0);
            tools.Controls.Add(lDnsServer);
            _dnsServer = Ui.Text(0, 0, 110);
            _dnsServer.Text = core.Settings.NslookupServer;
            _dnsServer.Margin = new Padding(0, 1, 12, 4);
            tools.Controls.Add(_dnsServer);

            Button bMtu = Ui.Button("Подобрать MTU до узла", 0, 0, 170, delegate { FindMtuClick(); });
            bMtu.Margin = new Padding(0, 0, 6, 4);
            tools.Controls.Add(bMtu);
            Button bScan = Ui.Button("Ping-скан подсети (кто в сети)", 0, 0, 210, delegate { ScanSubnet(); });
            bScan.Margin = new Padding(0, 0, 6, 4);
            tools.Controls.Add(bScan);

            // ---------------- таблица ----------------
            _grid = Ui.Grid();
            _grid.Dock = DockStyle.Fill;
            Ui.Columns(_grid, new string[] { "№", "Узел", "Имя узла", "Попытка 1, мс", "Попытка 2, мс", "Попытка 3, мс", "Статус" },
                new float[] { 35, 130, 220, 90, 90, 90, 200 });

            _out = Ui.LogBox();
            _out.Dock = DockStyle.Fill;

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            split.SplitterDistance = 300;
            split.Panel1.Controls.Add(_grid);
            split.Panel2.Controls.Add(_out);

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 32;
            bottom.Controls.Add(Ui.Button("Копировать таблицу", 2, 3, 150, delegate { CopyTable(); }));
            bottom.Controls.Add(Ui.Button("Сохранить отчёт трассировки", 158, 3, 200, delegate { SaveTrace(); }));

            Panel center = new Panel();
            center.Dock = DockStyle.Fill;
            center.Padding = new Padding(2, 0, 0, 0);
            center.Controls.Add(split);
            center.Controls.Add(bottom);

            Controls.Add(center);
            Controls.Add(tools);
            Controls.Add(top);
        }

        private static NumericUpDown Num(int min, int max, int value, int width)
        {
            NumericUpDown n = new NumericUpDown();
            n.Minimum = min;
            n.Maximum = max;
            n.Value = Math.Max(min, Math.Min(max, value));
            n.Width = width;
            n.Font = Ui.BaseFont;
            n.Margin = new Padding(2, 4, 10, 0);
            return n;
        }

        public void StartWith(string host)
        {
            if (string.IsNullOrEmpty(host)) { Ui.Warn(this, "Укажите узел для трассировки."); return; }
            _host.Text = host;
            _core.Settings.TraceHost = host;
            TraceAsync(host);
        }

        // ------------------------------------------------------ своя трассировка

        private async void TraceAsync(string host)
        {
            if (_running) { Ui.Msg(this, "Трассировка уже выполняется."); return; }
            _running = true;
            _stopFlag = false;
            _btnGo.Enabled = false;
            _btnStop.Enabled = true;
            _grid.Rows.Clear();
            Ui.Log(_out, "");
            Ui.Log(_out, "=== Трассировка до " + host + " (не более " + _hops.Value + " прыжков) ===", Ui.Accent);

            string cur = "";
            try
            {
                using (Ping ping = new Ping())
                {
                    for (int ttl = 1; ttl <= (int)_hops.Value && !_stopFlag; ttl++)
                    {
                        long[] times = new long[] { -1, -1, -1 };
                        string addr = "";
                        string name = "";
                        string status = "";
                        bool reached = false;

                        for (int probe = 0; probe < 3; probe++)
                        {
                            if (_stopFlag) break;
                            try
                            {
                                byte[] buf = new byte[32];
                                PingOptions po = new PingOptions(ttl, true);
                                PingReply r = await ping.SendPingAsync(host, (int)_timeout.Value, buf, po);
                                if (r.Status == IPStatus.Success || r.Status == IPStatus.TtlExpired)
                                {
                                    if (r.Address != null) addr = r.Address.ToString();
                                    times[probe] = r.RoundtripTime;
                                    if (r.Status == IPStatus.Success) reached = true;
                                }
                                else
                                {
                                    status = TabPing.StatusText(r.Status);
                                }
                            }
                            catch (Exception ex)
                            {
                                status = ex.Message;
                            }
                        }

                        if (addr.Length > 0 && _cbResolve.Checked) name = await ResolveAsync(addr, 2500);
                        if (name.Length == 0 && addr.Length > 0) name = "";
                        if (reached) status = "узел назначения достигнут";
                        else if (addr.Length == 0) status = status.Length > 0 ? status : "нет ответа (превышен таймаут)";
                        else status = status.Length > 0 ? status : "промежуточный узел";

                        int idx = _grid.Rows.Add(new object[]
                        {
                            ttl.ToString(),
                            addr.Length > 0 ? addr : "*",
                            name.Length > 0 ? name : (addr.Length > 0 ? "(имя не определено)" : "—"),
                            Ms(times[0]), Ms(times[1]), Ms(times[2]),
                            status
                        });
                        if (addr.Length > 0)
                        {
                            _grid.Rows[idx].Cells[1].Style.ForeColor = reached ? Ui.OkColor : Color.FromArgb(40, 40, 45);
                        }
                        else
                        {
                            _grid.Rows[idx].DefaultCellStyle.ForeColor = Color.FromArgb(160, 160, 165);
                        }
                        _grid.FirstDisplayedScrollingRowIndex = idx;

                        if (addr.Length > 0) cur = addr;
                        Ui.Log(_out, "  " + ttl.ToString().PadLeft(2) + "  " + (addr.Length > 0 ? addr.PadRight(16) : "*".PadRight(16)) + " " +
                            name.PadRight(30) + " " + Ms(times[0]).PadLeft(8) + " " + Ms(times[1]).PadLeft(8) + " " + Ms(times[2]).PadLeft(8) + "   " + status,
                            addr.Length > 0 ? Ui.LogFore : Color.FromArgb(150, 150, 155));

                        if (reached) break;
                    }
                }
            }
            catch (Exception ex)
            {
                Ui.Log(_out, "Ошибка трассировки: " + ex.Message, Ui.ErrColor);
            }
            finally
            {
                _running = false;
                _btnGo.Enabled = true;
                _btnStop.Enabled = false;
                Ui.Log(_out, "=== Готово ===", Ui.Accent);
            }
        }

        private static string Ms(long v)
        {
            return v >= 0 ? v.ToString() : "*";
        }

        private static async Task<string> ResolveAsync(string ip, int timeoutMs)
        {
            try
            {
                Task<string> t = Task.Factory.StartNew<string>(delegate
                {
                    try { return Dns.GetHostEntry(ip).HostName; }
                    catch { return ""; }
                });
                Task done = await Task.WhenAny(t, Task.Delay(timeoutMs));
                if (done == t && t.Result != null) return t.Result;
                return "";
            }
            catch { return ""; }
        }

        // ------------------------------------------------------ системные утилиты

        private void RawTrace()
        {
            string host = _host.Text.Trim();
            if (host.Length == 0) { Ui.Warn(this, "Укажите узел."); return; }
            string cmd = "tracert -d -w " + _timeout.Value + " -h " + _hops.Value + " " + host;
            if (_cbResolve.Checked) cmd = "tracert -w " + _timeout.Value + " -h " + _hops.Value + " " + host;
            RunRaw("Системный tracert: " + host, cmd, 300000);
        }

        private void PathPing()
        {
            string host = _host.Text.Trim();
            if (host.Length == 0) { Ui.Warn(this, "Укажите узел."); return; }
            if (!Ui.Ask(this, "pathping собирает статистику потерь по каждому узлу и может работать 1–3 минуты." +
                Environment.NewLine + Environment.NewLine + "Запустить?")) return;
            RunRaw("pathping: " + host, "pathping -q 20 -p 100 -h " + Math.Min(20, (int)_hops.Value) + " " + host, 600000);
        }

        private void Nslookup()
        {
            string host = _host.Text.Trim();
            if (host.Length == 0) { Ui.Warn(this, "Укажите узел (имя или IP)."); return; }
            string server = _dnsServer.Text.Trim();
            _core.Settings.NslookupServer = server;
            RunRaw("nslookup " + host, "nslookup " + host + (server.Length > 0 ? " " + server : ""), 30000);
        }

        private async void RunRaw(string title, string command, int timeoutMs)
        {
            Ui.Log(_out, "");
            Ui.Log(_out, "> " + command, Ui.Accent);
            try
            {
                string f, a;
                Runner.Split(command, out f, out a);
                CmdResult r = await Shell.RunAsync(Runner.ResolveExe(f), a, timeoutMs);
                Ui.Log(_out, r.Output.Length > 0 ? r.Output : "(нет вывода)");
            }
            catch (Exception ex)
            {
                Ui.Log(_out, "Ошибка: " + ex.Message, Ui.ErrColor);
            }
        }

        // ------------------------------------------------------ подбор MTU

        private async void FindMtuClick()
        {
            string host = _host.Text.Trim();
            if (host.Length == 0) { Ui.Warn(this, "Укажите узел."); return; }
            Ui.Log(_out, "");
            Ui.Log(_out, "=== Подбор MTU до " + host + " (пакеты с флагом «не фрагментировать») ===", Ui.Accent);
            int lo = 1200;
            int hi = 1500;
            try
            {
                using (Ping ping = new Ping())
                {
                    while (lo < hi)
                    {
                        int mid = (lo + hi + 1) / 2;
                        bool ok = await PingDf(ping, host, mid - 28);
                        Ui.Log(_out, "  MTU " + mid + " : " + (ok ? "проходит" : "не проходит"),
                            ok ? Ui.OkColor : Color.FromArgb(230, 170, 120));
                        if (ok) lo = mid; else hi = mid - 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Ui.Log(_out, "Ошибка: " + ex.Message, Ui.ErrColor);
                return;
            }
            Ui.Log(_out, "");
            Ui.Log(_out, "Максимальный MTU до " + host + ": " + lo, Ui.Accent);
            if (lo >= 1500) Ui.Log(_out, "Значение максимальное — ограничений на пути нет (обычная Ethernet-сеть).");
            else if (lo > 1450) Ui.Log(_out, "Похоже на PPPoE/VPN-туннель. Рекомендуется установить MTU = " + lo + " на проблемном адаптере (вкладка «IP / DNS»).");
            else Ui.Log(_out, "Сильное ограничение MTU (VPN, PPPoE или проблемный канал). Рекомендуется MTU = " + lo + ".");
            Ui.Log(_out, "Подсказка: MTU = размер полезной нагрузки ICMP + 28 байт заголовков.");
            _core.Log("info", "Подбор MTU до " + host + ": " + lo);
        }

        private async Task<bool> PingDf(Ping ping, string host, int payload)
        {
            try
            {
                if (payload < 0) payload = 0;
                byte[] buf = new byte[payload];
                PingOptions po = new PingOptions(64, true);
                PingReply r = await ping.SendPingAsync(host, 4000, buf, po);
                return r.Status == IPStatus.Success;
            }
            catch { return false; }
        }

        // ------------------------------------------------------ скан подсети

        private async void ScanSubnet()
        {
            AdapterInfo a = _core.Selected;
            if (a == null || a.Ipv4.Count == 0 || a.Masks.Count == 0)
            {
                Ui.Msg(this, "Выберите на вкладке «Адаптеры» адаптер с настроенным IPv4-адресом.");
                return;
            }
            string ip = a.Ipv4[0];
            string mask = a.Masks[0];
            byte[] ipb, mb;
            if (!TryBytes(ip, out ipb) || !TryBytes(mask, out mb)) { Ui.Warn(this, "Не удалось разобрать адрес/маску."); return; }

            int count = 0;
            for (int i = 0; i < 4; i++)
            {
                int hostBits = 8 - BitsIn(mb[i]);
                count += (int)Math.Pow(2, hostBits);
            }
            if (count > 512)
            {
                if (!Ui.Ask(this, "В подсети около " + count + " адресов — проверка займёт продолжительное время." +
                    Environment.NewLine + Environment.NewLine + "Проверить только первые 254 адреса?")) return;
                count = 256;
            }

            Ui.Log(_out, "");
            Ui.Log(_out, "=== Проверка доступности узлов в " + ip + " / " + mask + " ===", Ui.Accent);
            List<string> targets = new List<string>();
            byte[] baseIp = (byte[])ipb.Clone();
            for (int i = 0; i < 4; i++) baseIp[i] = (byte)(ipb[i] & mb[i]);
            for (int n = 1; n < count && targets.Count < 254; n++)
            {
                byte[] cand = (byte[])baseIp.Clone();
                cand[3] = (byte)(baseIp[3] + n);
                if (cand[3] == ipb[3]) continue;
                if (cand[3] == 0 || cand[3] == 255) continue;
                if (baseIp[3] + n > 255)
                {
                    cand[2] = (byte)(baseIp[2] + ((baseIp[3] + n) / 256));
                    cand[3] = (byte)((baseIp[3] + n) % 256);
                    if (cand[3] == 0 || cand[3] == 255) continue;
                }
                targets.Add(cand[0] + "." + cand[1] + "." + cand[2] + "." + cand[3]);
            }

            int found = 0;
            List<Task> tasks = new List<Task>();
            object gate = new object();
            for (int i = 0; i < targets.Count; i++)
            {
                string t = targets[i];
                tasks.Add(Task.Factory.StartNew(delegate
                {
                    try
                    {
                        using (Ping p = new Ping())
                        {
                            PingReply r = p.Send(t, 700);
                            if (r.Status == IPStatus.Success)
                            {
                                string name = "";
                                try { name = Dns.GetHostEntry(t).HostName; }
                                catch { }
                                lock (gate)
                                {
                                    found++;
                                    BeginInvoke((MethodInvoker)delegate
                                    {
                                        Ui.Log(_out, "  " + t.PadRight(16) + name, Ui.OkColor);
                                    });
                                }
                            }
                        }
                    }
                    catch { }
                }));
            }
            await Task.WhenAll(tasks.ToArray());
            Ui.Log(_out, "Проверено адресов: " + targets.Count + ", ответили: " + found, Ui.Accent);
        }

        private static bool TryBytes(string ip, out byte[] b)
        {
            b = null;
            IPAddress a;
            if (!IPAddress.TryParse(ip, out a)) return false;
            if (a.AddressFamily != AddressFamily.InterNetwork) return false;
            b = a.GetAddressBytes();
            return true;
        }

        private static int BitsIn(byte v)
        {
            int c = 0;
            for (int i = 0; i < 8; i++) if ((v & (1 << i)) != 0) c++;
            return c;
        }

        // ------------------------------------------------------ прочее

        private void CopyTable()
        {
            StringBuilder sb = new StringBuilder();
            for (int c = 0; c < _grid.Columns.Count; c++) sb.Append((c > 0 ? "\t" : "") + _grid.Columns[c].HeaderText);
            sb.AppendLine();
            for (int r = 0; r < _grid.Rows.Count; r++)
            {
                for (int c = 0; c < _grid.Columns.Count; c++)
                    sb.Append((c > 0 ? "\t" : "") + Convert.ToString(_grid.Rows[r].Cells[c].Value));
                sb.AppendLine();
            }
            if (sb.Length > 0) Clipboard.SetText(sb.ToString());
        }

        private void SaveTrace()
        {
            try
            {
                string dir = System.IO.Path.Combine(AppSettings.DataDir, "reports");
                System.IO.Directory.CreateDirectory(dir);
                string file = System.IO.Path.Combine(dir, "traceroute-" + Environment.MachineName + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(Report.Header());
                sb.AppendLine("Трассировка до " + _host.Text);
                sb.AppendLine();
                for (int r = 0; r < _grid.Rows.Count; r++)
                {
                    for (int c = 0; c < _grid.Columns.Count; c++)
                        sb.Append(Convert.ToString(_grid.Rows[r].Cells[c].Value).PadRight(22));
                    sb.AppendLine();
                }
                sb.AppendLine();
                sb.AppendLine(_out.Text);
                System.IO.File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
                if (Ui.Ask(this, "Отчёт сохранён:" + Environment.NewLine + file + Environment.NewLine + Environment.NewLine + "Открыть файл?"))
                    Process.Start("notepad.exe", file);
            }
            catch (Exception ex) { Ui.Err(this, "Не удалось сохранить: " + ex.Message); }
        }
    }
}
