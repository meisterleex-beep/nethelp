using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NetHelp
{
    public class PingStat
    {
        public string Host = "";
        public string Ip = "";
        public int Sent = 0;
        public int Recv = 0;
        public int Fail = 0;
        public long Min = -1;
        public long Max = -1;
        public long Last = -1;
        public long Sum = 0;
        public int Ttl = 0;
        public string Status = "";
        public DataGridViewRow Row;

        public double Avg { get { return Recv > 0 ? (double)Sum / Recv : 0; } }
        public double Loss { get { return Sent > 0 ? (100.0 * (Sent - Recv) / Sent) : 0; } }

        public object[] ToRow()
        {
            return new object[]
            {
                Host, Ip, Sent.ToString(), Recv.ToString(),
                Sent > 0 ? Loss.ToString("0") + " %" : "—",
                Min >= 0 ? Min.ToString() : "—",
                Recv > 0 ? Avg.ToString("0") : "—",
                Max >= 0 ? Max.ToString() : "—",
                Last >= 0 ? Last.ToString() : "—",
                Ttl > 0 ? Ttl.ToString() : "—",
                Status
            };
        }
    }

    /// <summary>Пинг списка сайтов и адресов.</summary>
    public class TabPing : UserControl
    {
        private Core _core;
        private ListBox _hosts;
        private TextBox _newHost;
        private DataGridView _grid;
        private RichTextBox _log;
        private NumericUpDown _count;
        private NumericUpDown _timeout;
        private NumericUpDown _size;
        private NumericUpDown _interval;
        private CheckBox _cbLog;
        private CheckBox _cbDf;
        private CheckBox _cbContinuous;
        private Button _btnAll;
        private Button _btnSel;
        private Button _btnStop;
        private bool _running;
        private bool _stopFlag;
        private Dictionary<string, PingStat> _stats = new Dictionary<string, PingStat>(StringComparer.OrdinalIgnoreCase);

        public TabPing(Core core)
        {
            _core = core;
            Dock = DockStyle.Fill;
            BackColor = Ui.Bg;

            // ---------------- список хостов ----------------
            Panel left = new Panel();
            left.Dock = DockStyle.Left;
            left.Width = 250;
            left.Padding = new Padding(0, 4, 6, 0);

            Panel lTop = new Panel();
            lTop.Dock = DockStyle.Top;
            lTop.Height = 24;
            Label lh = Ui.Label("Список сайтов и адресов:", 2, 4);
            lh.Font = Ui.BoldFont;
            lTop.Controls.Add(lh);

            Panel lBottom = new Panel();
            lBottom.Dock = DockStyle.Bottom;
            lBottom.Height = 124;

            _newHost = Ui.Text(2, 4, 236);
            _newHost.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { AddHostFromBox(); e.SuppressKeyPress = true; }
            };
            lBottom.Controls.Add(_newHost);
            lBottom.Controls.Add(Ui.Button("Добавить", 2, 32, 116, delegate { AddHostFromBox(); }));
            lBottom.Controls.Add(Ui.Button("Удалить", 122, 32, 116, delegate { RemoveHosts(); }));
            lBottom.Controls.Add(Ui.Button("Загрузить из файла", 2, 60, 152, delegate { LoadHosts(); }));
            lBottom.Controls.Add(Ui.Button("Сохранить в файл", 158, 60, 80, delegate { SaveHosts(); }));
            lBottom.Controls.Add(Ui.Button("Восстановить список по умолчанию", 2, 88, 236, delegate
            {
                _hosts.Items.Clear();
                List<string> d = AppSettings.Defaults().PingHosts;
                for (int i = 0; i < d.Count; i++) _hosts.Items.Add(d[i]);
                SyncSettings();
            }));

            _hosts = new ListBox();
            _hosts.Dock = DockStyle.Fill;
            _hosts.Font = Ui.BaseFont;
            _hosts.SelectionMode = SelectionMode.MultiExtended;
            _hosts.IntegralHeight = false;

            left.Controls.Add(_hosts);
            left.Controls.Add(lBottom);
            left.Controls.Add(lTop);

            // ---------------- параметры ----------------
            // ---- панель параметров и кнопок запуска ----
            Panel top = new Panel();
            top.Dock = DockStyle.Top;
            top.Height = 112;

            FlowLayoutPanel flow = new FlowLayoutPanel();
            flow.Dock = DockStyle.Fill;
            flow.Padding = new Padding(2, 6, 0, 0);
            flow.WrapContents = true;

            flow.Controls.Add(Ui.Label("Пакетов:", 0, 4));
            _count = Num(1, 100, core.Settings.PingCount, 56);
            flow.Controls.Add(_count);
            flow.Controls.Add(Ui.Label("Таймаут, мс:", 0, 4));
            _timeout = Num(200, 30000, core.Settings.PingTimeout, 70);
            _timeout.Increment = 500;
            flow.Controls.Add(_timeout);
            flow.Controls.Add(Ui.Label("Размер, байт:", 0, 4));
            _size = Num(0, 65000, core.Settings.PingSize, 70);
            _size.Increment = 8;
            flow.Controls.Add(_size);
            flow.Controls.Add(Ui.Label("Пауза, мс:", 0, 4));
            _interval = Num(200, 10000, 1000, 70);
            _interval.Increment = 100;
            flow.Controls.Add(_interval);
            _cbDf = Ui.Check("не фрагментировать (DF)", 0, 6, false, null);
            flow.Controls.Add(_cbDf);
            _cbLog = Ui.Check("журнал ответов", 0, 6, core.Settings.PingLogReplies, null);
            flow.Controls.Add(_cbLog);
            _cbContinuous = Ui.Check("повторять непрерывно (до «Стоп»)", 0, 6, false, null);
            flow.Controls.Add(_cbContinuous);

            top.Controls.Add(flow);

            Panel topButtons = new Panel();
            topButtons.Dock = DockStyle.Bottom;
            topButtons.Height = 34;
            _btnAll = Ui.Button("Пинг всех", 2, 2, 130, delegate { StartPing(false); });
            _btnSel = Ui.Button("Только выбранные", 138, 2, 150, delegate { StartPing(true); });
            _btnStop = Ui.Button("Стоп", 294, 2, 80, delegate { _stopFlag = true; _log.AppendText(""); Ui.Log(_log, "Остановка...", Ui.WarnColor); });
            _btnStop.Enabled = false;
            topButtons.Controls.Add(_btnAll);
            topButtons.Controls.Add(_btnSel);
            topButtons.Controls.Add(_btnStop);
            topButtons.Controls.Add(Ui.Button("Очистить результаты", 380, 2, 150, delegate { ClearResults(); }));
            topButtons.Controls.Add(Ui.Button("Сохранить результаты", 536, 2, 150, delegate { SaveResults(); }));
            top.Controls.Add(topButtons);

            // ---------------- таблица и журнал ----------------
            _grid = Ui.Grid();
            _grid.Dock = DockStyle.Fill;
            Ui.Columns(_grid, new string[] { "Узел", "IP-адрес", "Отправлено", "Принято", "Потери", "Мин, мс", "Сред, мс", "Макс, мс", "Последний", "TTL", "Состояние" },
                new float[] { 130, 110, 80, 70, 60, 60, 65, 65, 70, 45, 140 });

            _log = Ui.LogBox();
            _log.Dock = DockStyle.Fill;

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            split.SplitterDistance = 280;
            split.Panel1.Controls.Add(_grid);
            split.Panel2.Controls.Add(_log);

            Panel right = new Panel();
            right.Dock = DockStyle.Fill;
            right.Padding = new Padding(6, 4, 0, 0);
            right.Controls.Add(split);
            right.Controls.Add(top);

            Controls.Add(right);
            Controls.Add(left);

            for (int i = 0; i < core.Settings.PingHosts.Count; i++) _hosts.Items.Add(core.Settings.PingHosts[i]);
        }

        /// <summary>Демонстрационные строки для снимков интерфейса (ничего не пингуется).</summary>
        public void LoadDemo()
        {
            _hosts.Items.Clear();
            string[] hosts = new string[] { "8.8.8.8", "1.1.1.1", "example.com", "ya.ru" };
            for (int i = 0; i < hosts.Length; i++) _hosts.Items.Add(hosts[i]);
            _grid.Rows.Clear();
            DemoRow("8.8.8.8", "8.8.8.8", 8, 8, 0, 11, 14, 19, 12, 117, "ответ получен");
            DemoRow("1.1.1.1", "1.1.1.1", 8, 8, 0, 9, 12, 17, 10, 116, "ответ получен");
            DemoRow("example.com", "93.184.216.34", 8, 7, 12.5, 24, 31, 58, -1, 55, "превышен интервал ожидания");
            DemoRow("ya.ru", "77.88.55.88", 8, 8, 0, 6, 9, 14, 7, 54, "ответ получен");
            Ui.Log(_log, "");
            Ui.Log(_log, "=== Пинг 4 узл(ов), пакетов: 8, таймаут: 2000 мс ===", Ui.Accent);
            Ui.Log(_log, "  8.8.8.8 — ответ от 8.8.8.8: время=12 мс, TTL=117", Color.FromArgb(180, 220, 180));
            Ui.Log(_log, "  example.com — превышен интервал ожидания", Color.FromArgb(255, 170, 170));
            Ui.Log(_log, "=== Пинг завершён ===", Ui.Accent);
        }

        private void DemoRow(string host, string ip, int sent, int recv, double loss, long min, long avg, long max, long last, int ttl, string status)
        {
            int idx = _grid.Rows.Add(new object[]
            {
                host, ip, sent.ToString(), recv.ToString(),
                sent > 0 ? loss.ToString("0") + " %" : "—",
                min.ToString(), avg.ToString(), max.ToString(),
                last >= 0 ? last.ToString() : "—", ttl.ToString(), status
            });
            DataGridViewRow r = _grid.Rows[idx];
            r.Cells[10].Style.ForeColor = recv == 0 ? Ui.ErrColor : (loss > 0 ? Ui.WarnColor : Ui.OkColor);
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

        // ------------------------------------------------------------- список

        private void AddHostFromBox()
        {
            string h = _newHost.Text.Trim();
            if (h.Length == 0) return;
            AddHost(h);
            _newHost.Clear();
        }

        public void AddHost(string host)
        {
            for (int i = 0; i < _hosts.Items.Count; i++)
                if (string.Equals(Convert.ToString(_hosts.Items[i]), host, StringComparison.OrdinalIgnoreCase)) return;
            _hosts.Items.Add(host);
            SyncSettings();
        }

        private void RemoveHosts()
        {
            List<object> sel = new List<object>();
            for (int i = 0; i < _hosts.SelectedItems.Count; i++) sel.Add(_hosts.SelectedItems[i]);
            for (int i = 0; i < sel.Count; i++) _hosts.Items.Remove(sel[i]);
            SyncSettings();
        }

        private void SyncSettings()
        {
            _core.Settings.PingHosts.Clear();
            for (int i = 0; i < _hosts.Items.Count; i++) _core.Settings.PingHosts.Add(Convert.ToString(_hosts.Items[i]));
            _core.Settings.PingCount = (int)_count.Value;
            _core.Settings.PingTimeout = (int)_timeout.Value;
            _core.Settings.PingSize = (int)_size.Value;
            _core.Settings.PingLogReplies = _cbLog.Checked;
        }

        private void LoadHosts()
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*";
            d.Title = "Загрузить список адресов";
            if (d.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                string[] lines = File.ReadAllLines(d.FileName);
                for (int i = 0; i < lines.Length; i++)
                {
                    string s = lines[i].Trim();
                    if (s.Length == 0 || s.StartsWith("#")) continue;
                    AddHost(s);
                }
            }
            catch (Exception ex) { Ui.Err(this, "Не удалось загрузить файл: " + ex.Message); }
        }

        private void SaveHosts()
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "Текстовые файлы (*.txt)|*.txt";
            d.FileName = "nethelp-hosts.txt";
            if (d.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < _hosts.Items.Count; i++) sb.AppendLine(Convert.ToString(_hosts.Items[i]));
                File.WriteAllText(d.FileName, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex) { Ui.Err(this, "Не удалось сохранить файл: " + ex.Message); }
        }

        // ------------------------------------------------------------- пинг

        public void AddAndStart(string host)
        {
            AddHost(host);
            for (int i = 0; i < _hosts.Items.Count; i++)
                _hosts.SetSelected(i, string.Equals(Convert.ToString(_hosts.Items[i]), host, StringComparison.OrdinalIgnoreCase));
            StartPing(true);
        }

        private List<string> SelectedHosts()
        {
            List<string> list = new List<string>();
            for (int i = 0; i < _hosts.SelectedItems.Count; i++) list.Add(Convert.ToString(_hosts.SelectedItems[i]));
            return list;
        }

        private List<string> AllHosts()
        {
            List<string> list = new List<string>();
            for (int i = 0; i < _hosts.Items.Count; i++) list.Add(Convert.ToString(_hosts.Items[i]));
            return list;
        }

        private class Probe
        {
            public bool Ok;
            public long Ms = -1;
            public string Ip = "";
            public int Ttl = 0;
            public string Status = "";
        }

        private async void StartPing(bool selectedOnly)
        {
            if (_running) { Ui.Msg(this, "Пинг уже выполняется. Нажмите «Стоп», чтобы прервать."); return; }
            SyncSettings();
            List<string> hosts = selectedOnly ? SelectedHosts() : AllHosts();
            if (hosts.Count == 0) { Ui.Warn(this, "Список адресов пуст. Добавьте сайт или IP-адрес слева."); return; }

            _running = true;
            _stopFlag = false;
            _btnAll.Enabled = false;
            _btnSel.Enabled = false;
            _btnStop.Enabled = true;

            _stats.Clear();
            _grid.Rows.Clear();
            for (int i = 0; i < hosts.Count; i++)
            {
                PingStat st = new PingStat();
                st.Host = hosts[i];
                st.Status = "ожидание";
                int index = _grid.Rows.Add(st.ToRow());
                st.Row = _grid.Rows[index];
                _stats[hosts[i]] = st;
            }

            Ui.Log(_log, "");
            Ui.Log(_log, "=== Пинг " + hosts.Count + " узл(ов), пакетов: " + _count.Value + ", таймаут: " + _timeout.Value + " мс ===", Ui.Accent);

            try
            {
                bool continuous = _cbContinuous.Checked;
                do
                {
                    List<Task> tasks = new List<Task>();
                    for (int i = 0; i < hosts.Count; i++) tasks.Add(PingHostAsync(hosts[i]));
                    await Task.WhenAll(tasks.ToArray());
                    if (_stopFlag || !continuous) break;
                    await Task.Delay((int)_interval.Value);
                }
                while (!_stopFlag);
            }
            catch (Exception ex)
            {
                Ui.Log(_log, "Ошибка: " + ex.Message, Ui.ErrColor);
            }
            finally
            {
                _running = false;
                _btnAll.Enabled = true;
                _btnSel.Enabled = true;
                _btnStop.Enabled = false;
                Ui.Log(_log, "=== Пинг завершён ===", Ui.Accent);
            }
        }

        private async Task PingHostAsync(string host)
        {
            PingStat st;
            if (!_stats.TryGetValue(host, out st)) return;
            try
            {
                using (Ping ping = new Ping())
                {
                    for (int i = 0; i < (int)_count.Value && !_stopFlag; i++)
                    {
                        Probe p = await ProbeAsync(ping, host);
                        st.Sent++;
                        if (p.Ok)
                        {
                            st.Recv++;
                            st.Last = p.Ms;
                            st.Sum += p.Ms;
                            if (st.Min < 0 || p.Ms < st.Min) st.Min = p.Ms;
                            if (p.Ms > st.Max) st.Max = p.Ms;
                            if (p.Ip.Length > 0) st.Ip = p.Ip;
                            st.Ttl = p.Ttl;
                            st.Status = "ответ получен";
                            if (_cbLog.Checked) Ui.Log(_log, "  " + host + " — ответ от " + p.Ip + ": время=" + p.Ms + " мс, TTL=" + p.Ttl, Color.FromArgb(180, 220, 180));
                        }
                        else
                        {
                            st.Fail++;
                            st.Last = -1;
                            st.Status = p.Status.Length > 0 ? p.Status : "нет ответа";
                            if (_cbLog.Checked) Ui.Log(_log, "  " + host + " — " + st.Status, Color.FromArgb(255, 170, 170));
                        }
                        UpdateRow(st);
                        if (i + 1 < (int)_count.Value && !_stopFlag) await Task.Delay(120);
                    }
                }
            }
            catch (Exception ex)
            {
                st.Status = ex.Message;
                UpdateRow(st);
            }
        }

        private async Task<Probe> ProbeAsync(Ping ping, string host)
        {
            Probe p = new Probe();
            try
            {
                byte[] buf = new byte[(int)_size.Value];
                PingOptions po = new PingOptions(128, _cbDf.Checked);
                PingReply r = await ping.SendPingAsync(host, (int)_timeout.Value, buf, po);
                if (r.Status == IPStatus.Success)
                {
                    p.Ok = true;
                    p.Ms = r.RoundtripTime;
                    p.Ip = r.Address != null ? r.Address.ToString() : "";
                    p.Ttl = r.Options != null ? r.Options.Ttl : 0;
                }
                else
                {
                    p.Status = StatusText(r.Status);
                    if (r.Address != null) p.Ip = r.Address.ToString();
                }
            }
            catch (Exception ex)
            {
                p.Status = ex.Message;
            }
            return p;
        }

        public static string StatusText(IPStatus s)
        {
            switch (s)
            {
                case IPStatus.Success: return "успешно";
                case IPStatus.TimedOut: return "превышен интервал ожидания";
                case IPStatus.DestinationHostUnreachable: return "узел недоступен";
                case IPStatus.DestinationNetworkUnreachable: return "сеть недоступна";
                case IPStatus.DestinationPortUnreachable: return "порт недоступен";
                case IPStatus.DestinationUnreachable: return "адрес недоступен";
                case IPStatus.TtlExpired: return "TTL истёк";
                case IPStatus.BadDestination: return "неверный адрес";
                case IPStatus.PacketTooBig: return "пакет слишком велик (нужен меньший MTU)";
                case IPStatus.BadOption: return "неверный параметр";
                default: return s.ToString();
            }
        }

        private void UpdateRow(PingStat st)
        {
            DataGridViewRow row = st.Row;
            if (row == null || row.DataGridView != _grid) return;
            object[] vals = st.ToRow();
            for (int i = 0; i < vals.Length && i < row.Cells.Count; i++) row.Cells[i].Value = vals[i];
            bool anyReply = st.Recv > 0;
            if (st.Sent > 0 && st.Recv == 0) row.Cells[10].Style.ForeColor = Ui.ErrColor;
            else if (st.Loss > 0) row.Cells[10].Style.ForeColor = Ui.WarnColor;
            else if (anyReply) row.Cells[10].Style.ForeColor = Ui.OkColor;
        }

        private void ClearResults()
        {
            if (_running) { Ui.Warn(this, "Сначала остановите пинг."); return; }
            _stats.Clear();
            _grid.Rows.Clear();
            _log.Clear();
        }

        private void SaveResults()
        {
            if (_grid.Rows.Count == 0) { Ui.Warn(this, "Нет результатов для сохранения."); return; }
            try
            {
                string dir = Path.Combine(AppSettings.DataDir, "reports");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "ping-" + Environment.MachineName + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(Report.Header());
                sb.AppendLine("--------------------------------------------------------------");
                sb.AppendLine("  РЕЗУЛЬТАТЫ ПИНГА");
                sb.AppendLine("--------------------------------------------------------------");
                sb.AppendLine("Узел\tIP\tОтправлено\tПринято\tПотери\tМин\tСред\tМакс");
                for (int i = 0; i < _grid.Rows.Count; i++)
                {
                    DataGridViewRow r = _grid.Rows[i];
                    sb.AppendLine(r.Cells[0].Value + "\t" + r.Cells[1].Value + "\t" + r.Cells[2].Value + "\t" + r.Cells[3].Value + "\t" +
                                  r.Cells[4].Value + "\t" + r.Cells[5].Value + "\t" + r.Cells[6].Value + "\t" + r.Cells[7].Value);
                }
                sb.AppendLine();
                File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
                if (Ui.Ask(this, "Результаты сохранены:" + Environment.NewLine + file + Environment.NewLine + Environment.NewLine + "Открыть файл?"))
                    System.Diagnostics.Process.Start("notepad.exe", file);
            }
            catch (Exception ex) { Ui.Err(this, "Не удалось сохранить: " + ex.Message); }
        }
    }
}
