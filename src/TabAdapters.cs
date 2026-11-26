using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NetHelp
{
    /// <summary>Список сетевых адаптеров (включая скрытые) и их настройки.</summary>
    public class TabAdapters : UserControl
    {
        private Core _core;
        private DataGridView _grid;
        private RichTextBox _details;
        private TextBox _search;
        private CheckBox _cbHidden;
        private CheckBox _cbVirtual;
        private Label _lblCount;
        private List<AdapterInfo> _view = new List<AdapterInfo>();
        private bool _suppressSelect;

        public TabAdapters(Core core)
        {
            _core = core;
            Dock = DockStyle.Fill;
            BackColor = Ui.Bg;

            // ---------- Панель инструментов (переносится по строкам при узком окне) ----------
            FlowLayoutPanel top = new FlowLayoutPanel();
            top.Dock = DockStyle.Top;
            top.AutoSize = true;
            top.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            top.WrapContents = true;
            top.Padding = new Padding(4, 4, 4, 2);
            top.BackColor = Ui.Bg;

            Button bRefresh = Ui.Button("Обновить (F5)", 0, 0, 110, delegate { RefreshList(); });
            bRefresh.Margin = new Padding(0, 0, 6, 4);
            top.Controls.Add(bRefresh);

            Button bExport = Ui.Button("Экспорт списка…", 0, 0, 125, delegate { ExportList(); });
            bExport.Margin = new Padding(0, 0, 6, 4);
            top.Controls.Add(bExport);

            Button bReport = Ui.Button("Отчёт по сети…", 0, 0, 125, delegate { SaveReport(); });
            bReport.Margin = new Padding(0, 0, 10, 4);
            top.Controls.Add(bReport);

            Label lSearch = Ui.Label("Поиск:", 0, 0);
            lSearch.Margin = new Padding(4, 7, 2, 0);
            top.Controls.Add(lSearch);

            _search = Ui.Text(0, 0, 190);
            _search.Margin = new Padding(0, 1, 12, 4);
            _search.TextChanged += delegate { FillGrid(); };
            top.Controls.Add(_search);

            _cbHidden = Ui.Check("Показывать скрытые", 0, 0, core.Settings.ShowHidden, delegate
            {
                _core.Settings.ShowHidden = _cbHidden.Checked;
                FillGrid();
            });
            _cbHidden.Margin = new Padding(0, 6, 14, 4);
            top.Controls.Add(_cbHidden);

            _cbVirtual = Ui.Check("Показывать виртуальные и служебные", 0, 0, core.Settings.ShowVirtual, delegate
            {
                _core.Settings.ShowVirtual = _cbVirtual.Checked;
                FillGrid();
            });
            _cbVirtual.Margin = new Padding(0, 6, 6, 4);
            top.Controls.Add(_cbVirtual);

            // ---------- Таблица ----------
            Panel listPanel = new Panel();
            listPanel.Dock = DockStyle.Fill;
            listPanel.Padding = new Padding(0);

            _grid = Ui.Grid();
            _grid.Dock = DockStyle.Fill;
            Ui.Columns(_grid, AdapterInfo.ColumnNames(), new float[] { 100, 170, 60, 80, 80, 105, 105, 120, 80, 90 });
            _grid.SelectionChanged += delegate { OnGridSelection(); };
            _grid.CellDoubleClick += delegate(object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0) OpenNetworkConnections();
            };
            _grid.MouseDown += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Right)
                {
                    DataGridView.HitTestInfo hit = _grid.HitTest(e.X, e.Y);
                    if (hit.RowIndex >= 0) _grid.Rows[hit.RowIndex].Selected = true;
                }
            };

            ContextMenuStrip menu = BuildContextMenu();
            _grid.ContextMenuStrip = menu;

            listPanel.Controls.Add(_grid);

            // ---------- Сведения ----------
            Panel detailsBox = new Panel();
            detailsBox.Dock = DockStyle.Fill;

            Panel dButtons = new Panel();
            dButtons.Dock = DockStyle.Bottom;
            dButtons.Height = 38;
            dButtons.BackColor = Ui.Bg;
            int bx = 2;
            dButtons.Controls.Add(Ui.Button("Копировать сведения", bx, 6, 150, delegate
            {
                if (_core.Selected != null) Clipboard.SetText(_core.Selected.Details());
            })); bx += 156;
            dButtons.Controls.Add(Ui.Button("Сохранить карточку…", bx, 6, 150, delegate { SaveCard(); })); bx += 156;
            dButtons.Controls.Add(Ui.Button("Свойства в реестре", bx, 6, 150, delegate { ShowInRegistry(); })); bx += 156;
            dButtons.Controls.Add(Ui.Button("Диспетчер устройств", bx, 6, 150, delegate { OpenDeviceManager(); })); bx += 156;
            dButtons.Controls.Add(Ui.Button("Сетевые подключения", bx, 6, 160, delegate { OpenNetworkConnections(); }));

            _details = new RichTextBox();
            _details.Dock = DockStyle.Fill;
            _details.Font = Ui.MonoFont;
            _details.ReadOnly = true;
            _details.BackColor = Color.White;
            _details.BorderStyle = BorderStyle.None;
            _details.WordWrap = false;
            _details.HideSelection = false;

            detailsBox.Controls.Add(_details);
            detailsBox.Controls.Add(dButtons);

            _lblCount = Ui.Label("", 0, 0);
            _lblCount.Dock = DockStyle.Bottom;
            _lblCount.Height = 20;
            _lblCount.AutoSize = false;
            _lblCount.Padding = new Padding(6, 2, 0, 0);
            _lblCount.ForeColor = Ui.MutedColor;
            _lblCount.Font = Ui.SmallFont;

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            split.SplitterDistance = 330;
            split.BackColor = Ui.Bg;
            split.Panel1.Controls.Add(listPanel);
            split.Panel2.Controls.Add(detailsBox);

            Controls.Add(split);
            Controls.Add(_lblCount);
            Controls.Add(top);

            core.InventoryChanged += delegate { FillGrid(); };
            core.SelectionChanged += delegate { ShowDetails(_core.Selected); };
        }

        private void RefreshList()
        {
            if (_core.Busy) return;
            _details.Clear();
            Ui.Log(_details, "Сканирую сетевые адаптеры...", Ui.MutedColor);
            _core.ReloadAsync();
        }

        // ------------------------------------------------------------ таблица

        private void FillGrid()
        {
            if (_grid == null) return;
            AdapterInfo keep = _core.Selected;
            _view.Clear();
            string q = _search.Text.Trim().ToLowerInvariant();
            for (int i = 0; i < _core.Adapters.Count; i++)
            {
                AdapterInfo a = _core.Adapters[i];
                if (!_cbHidden.Checked && !a.Present) continue;
                if (!_cbVirtual.Checked && a.IsVirtual) continue;
                if (q.Length > 0)
                {
                    string hay = (a.Title + " " + a.Description + " " + a.Mac + " " + a.Ipv4Text + " " + a.Ipv6Text + " " +
                                  a.DnsText + " " + a.Guid + " " + a.InstanceId + " " + a.Service).ToLowerInvariant();
                    if (hay.IndexOf(q) < 0) continue;
                }
                _view.Add(a);
            }

            _suppressSelect = true;
            _grid.SuspendLayout();
            _grid.Rows.Clear();
            for (int i = 0; i < _view.Count; i++)
            {
                AdapterInfo a = _view[i];
                int idx = _grid.Rows.Add(a.ToRow());
                DataGridViewRow row = _grid.Rows[idx];
                row.Tag = a;
                if (!a.Present)
                {
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(140, 140, 145);
                    row.DefaultCellStyle.Font = new Font(Ui.BaseFont, FontStyle.Italic);
                }
                else if (a.StatusText == "Подключено")
                {
                    row.Cells[3].Style.ForeColor = Ui.OkColor;
                }
                else if (a.StatusText.StartsWith("Ошибка") || a.ErrorCode == 22)
                {
                    row.Cells[3].Style.ForeColor = Ui.ErrColor;
                }
                else if (a.StatusText == "Нет кабеля" || !a.NetEnabled)
                {
                    row.Cells[3].Style.ForeColor = Ui.WarnColor;
                }
                if (a.RegistryOnly && !a.Present) row.Cells[0].Style.ForeColor = Color.FromArgb(150, 90, 0);
            }
            _grid.ResumeLayout();

            int hidden = 0;
            for (int i = 0; i < _view.Count; i++) if (!_view[i].Present) hidden++;
            _lblCount.Text = "Показано адаптеров: " + _view.Count + "   •   из них скрытых (нет устройства): " + hidden +
                             "   •   всего найдено: " + _core.Adapters.Count;

            // восстановить выделение
            int sel = -1;
            if (keep != null)
                for (int i = 0; i < _view.Count; i++)
                    if (string.Equals(_view[i].Guid, keep.Guid, StringComparison.OrdinalIgnoreCase) && _view[i].Guid.Length > 0) { sel = i; break; }
            if (sel < 0 && _view.Count > 0) sel = 0;
            if (sel >= 0)
            {
                _grid.Rows[sel].Selected = true;
                _grid.CurrentCell = _grid.Rows[sel].Cells[0];
            }
            _suppressSelect = false;
            OnGridSelection();
        }

        private AdapterInfo Current()
        {
            if (_grid.SelectedRows.Count > 0)
            {
                AdapterInfo a = _grid.SelectedRows[0].Tag as AdapterInfo;
                if (a != null) return a;
            }
            return _core.Selected;
        }

        private void OnGridSelection()
        {
            if (_suppressSelect) return;
            AdapterInfo a = Current();
            _core.SelectAdapter(a);
        }

        private void ShowDetails(AdapterInfo a)
        {
            _details.Clear();
            if (a == null)
            {
                Ui.Log(_details, "Выберите адаптер в списке слева.", Ui.MutedColor);
                return;
            }
            string text = a.Details();
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                if (line.StartsWith("## "))
                {
                    Ui.Log(_details, "");
                    Ui.Log(_details, "  " + line.Substring(3), Ui.Accent);
                    Ui.Log(_details, "  " + new string('─', Math.Max(20, line.Length + 10)), Color.FromArgb(200, 208, 216));
                }
                else if (line.StartsWith("  > "))
                {
                    Ui.Log(_details, line, Color.FromArgb(30, 100, 30));
                }
                else
                {
                    Ui.Log(_details, line, Color.FromArgb(35, 35, 40));
                }
            }
        }

        // ------------------------------------------------------------ действия

        private ContextMenuStrip BuildContextMenu()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            m.Font = Ui.BaseFont;
            m.Items.Add(Menu("Получить адрес автоматически (DHCP)", delegate { Do(Cmds.SetDhcpAddress(Conn()), "DHCP", false, true); }));
            m.Items.Add(Menu("Сбросить DNS на автоматический", delegate { Do(Cmds.SetDhcpDns(Conn()), "DNS → DHCP", false, true); }));
            m.Items.Add(Menu("Перезапустить адаптер", delegate { Do(Cmds.Restart(Conn()), "Перезапуск адаптера", false, true); }));
            m.Items.Add(Menu("Включить адаптер", delegate { Do(Cmds.SetEnabled(Conn(), true), "Включение адаптера", false, true); }));
            m.Items.Add(Menu("Отключить адаптер", delegate { Do(Cmds.SetEnabled(Conn(), false), "Отключение адаптера", false, true); }));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Menu("Очистить кэш DNS", delegate
            {
                Runner.RunSingle(this, _core, "Очистка кэша DNS", "ipconfig /flushdns", false, false, true);
            }));
            m.Items.Add(Menu("Показать настройки IPv4 (netsh)", delegate { RunShow(Cmds.ShowConfig(Conn())); }));
            m.Items.Add(Menu("Показать DNS-серверы (netsh)", delegate { RunShow(Cmds.ShowDns(Conn())); }));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Menu("Пинг шлюза", delegate { PingGateway(); }));
            m.Items.Add(Menu("Трассировка до 8.8.8.8", delegate { _core.RequestTrace("8.8.8.8"); }));
            m.Items.Add(Menu("Проверить DNS-серверы адаптера", delegate { CheckDns(); }));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Menu("Копировать сведения", delegate { if (Current() != null) Clipboard.SetText(Current().Details()); }));
            m.Items.Add(Menu("Копировать имя подключения", delegate { if (Current() != null) Clipboard.SetText(Current().ConnectionName); }));
            m.Items.Add(Menu("Копировать IPv4", delegate { if (Current() != null) Clipboard.SetText(Current().Ipv4Text); }));
            m.Items.Add(Menu("Сохранить карточку в файл…", delegate { SaveCard(); }));
            m.Items.Add(Menu("Экспорт ветки реестра адаптера (.reg)…", delegate { ExportRegBranch(); }));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Menu("Удалить остаточную запись из реестра…", delegate { DeleteGhost(); }));
            return m;
        }

        private static ToolStripMenuItem Menu(string text, EventHandler h)
        {
            ToolStripMenuItem i = new ToolStripMenuItem(text);
            i.Click += h;
            return i;
        }

        private string Conn()
        {
            AdapterInfo a = Current();
            if (a == null) return "";
            return a.ConnectionName;
        }

        private async void Do(string[] commands, string title, bool danger, bool confirm)
        {
            AdapterInfo a = Current();
            if (a == null) { Ui.Warn(this, "Сначала выберите адаптер."); return; }
            if (!a.Manageable)
            {
                Ui.Warn(this, "Для этого адаптера нельзя применить команду: устройство отсутствует или неизвестно имя подключения." +
                    Environment.NewLine + Environment.NewLine +
                    "Имя подключения: " + (a.ConnectionName.Length > 0 ? a.ConnectionName : "(нет)") +
                    Environment.NewLine + "Устройство: " + (a.Present ? "присутствует" : "ОТСУТСТВУЕТ (скрытый)"));
                return;
            }
            RunOptions o = new RunOptions();
            o.Title = title + " — " + a.Title;
            o.Danger = danger;
            o.Confirm = confirm;
            o.NeedsAdmin = true;
            await Runner.Run(this, _core, commands, o);
        }

        private async void RunShow(string command)
        {
            RunOptions o = new RunOptions();
            o.Title = "Вывод команды";
            o.Confirm = false;
            o.NeedsAdmin = false;
            o.RefreshAfter = false;
            await Runner.Run(this, _core, new string[] { command }, o);
        }

        private void PingGateway()
        {
            AdapterInfo a = Current();
            if (a == null) return;
            if (a.Gateways.Count == 0) { Ui.Warn(this, "У адаптера не определён шлюз."); return; }
            _core.RequestPing(a.Gateways[0]);
        }

        private void CheckDns()
        {
            AdapterInfo a = Current();
            if (a == null) return;
            if (a.Dns.Count == 0) { Ui.Warn(this, "У адаптера не определён DNS-сервер."); return; }
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < a.Dns.Count; i++)
            {
                string server = a.Dns[i];
                if (server.IndexOf(':') >= 0) { sb.AppendLine(server + " — IPv6, пропущен"); continue; }
                Stopwatch sw = Stopwatch.StartNew();
                CmdResult r = Runner.RunLine("nslookup " + _core.Settings.DnsTestHost + " " + server, 15000);
                sw.Stop();
                bool ok = r.Output.IndexOf("Address", StringComparison.OrdinalIgnoreCase) >= 0 && r.Output.IndexOf("can't find", StringComparison.OrdinalIgnoreCase) < 0;
                sb.AppendLine((ok ? "OK   " : "ОШИБКА ") + server + "   " + sw.ElapsedMilliseconds + " мс");
                if (!ok) sb.AppendLine(Indent(r.Output));
            }
            _core.Log("info", "=== Проверка DNS адаптера " + a.Title + " ===");
            _core.Log("out", sb.ToString());
            Ui.Msg(this, "Результат проверки DNS:" + Environment.NewLine + Environment.NewLine + sb.ToString());
        }

        private static string Indent(string text)
        {
            if (string.IsNullOrEmpty(text)) return "    (нет вывода)";
            string[] lines = text.Split('\n');
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++) sb.AppendLine("    " + lines[i].TrimEnd('\r'));
            return sb.ToString();
        }

        private void ExportRegBranch()
        {
            AdapterInfo a = Current();
            if (a == null || a.RegClassPath.Length == 0) { Ui.Warn(this, "У адаптера не определён раздел реестра."); return; }
            string f = Runner.ExportRegistryKey(@"HKLM\" + Reg.NetClassPath + @"\" + a.RegKey, "Адаптер: " + a.Title);
            if (f != null) Ui.Msg(this, "Ветка реестра сохранена:" + Environment.NewLine + f);
            else Ui.Err(this, "Не удалось экспортировать ветку реестра.");
        }

        private void SaveCard()
        {
            AdapterInfo a = Current();
            if (a == null) { Ui.Warn(this, "Выберите адаптер."); return; }
            try
            {
                string f = Report.SaveAdapterCard(a);
                if (Ui.Ask(this, "Карточка сохранена:" + Environment.NewLine + f + Environment.NewLine + Environment.NewLine + "Открыть файл?"))
                    Process.Start("notepad.exe", f);
            }
            catch (Exception ex) { Ui.Err(this, "Ошибка: " + ex.Message); }
        }

        private void SaveReport()
        {
            try
            {
                string f = Report.SaveReport(_core, false);
                _core.Log("ok", "Отчёт сохранён: " + f);
                if (Ui.Ask(this, "Полный отчёт сохранён:" + Environment.NewLine + f + Environment.NewLine + Environment.NewLine + "Открыть файл?"))
                    Process.Start("notepad.exe", f);
            }
            catch (Exception ex) { Ui.Err(this, "Ошибка: " + ex.Message); }
        }

        private void ExportList()
        {
            try
            {
                string dir = Path.Combine(AppSettings.DataDir, "reports");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "adapters-" + Environment.MachineName + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv");
                StringBuilder sb = new StringBuilder();
                string[] cols = AdapterInfo.ColumnNames();
                for (int i = 0; i < cols.Length; i++) sb.Append((i > 0 ? ";" : "") + "\"" + cols[i] + "\"");
                sb.AppendLine(";\"Скрытый\";\"DHCP\";\"GUID\";\"Экземпляр\";\"Драйвер\"");
                for (int i = 0; i < _view.Count; i++)
                {
                    AdapterInfo a = _view[i];
                    object[] row = a.ToRow();
                    for (int c = 0; c < row.Length; c++)
                        sb.Append((c > 0 ? ";" : "") + "\"" + Convert.ToString(row[c]).Replace("\"", "\"\"") + "\"");
                    sb.AppendLine(";\"" + (a.Present ? "нет" : "да") + "\";\"" + a.DhcpText + "\";\"" + a.Guid + "\";\"" +
                                  a.InstanceId + "\";\"" + a.Service + " " + a.DriverVersion + "\"");
                }
                File.WriteAllText(file, sb.ToString(), new UTF8Encoding(true));
                if (Ui.Ask(this, "Список адаптеров сохранён:" + Environment.NewLine + file + Environment.NewLine + Environment.NewLine + "Открыть файл?"))
                    Process.Start("notepad.exe", file);
            }
            catch (Exception ex) { Ui.Err(this, "Ошибка экспорта: " + ex.Message); }
        }

        /// <summary>Удаление «призрачной» записи адаптера из реестра с предварительным бэкапом.</summary>
        private async void DeleteGhost()
        {
            AdapterInfo a = Current();
            if (a == null) { Ui.Warn(this, "Выберите адаптер."); return; }
            if (a.Present)
            {
                Ui.Warn(this, "Этот адаптер сейчас присутствует в системе. Удалять его запись из реестра не нужно — " +
                    "используйте «Диспетчер устройств» для удаления устройства.");
                return;
            }
            List<string> keys = new List<string>();
            if (a.RegKey.Length > 0) keys.Add(Reg.NetClassPath + @"\" + a.RegKey);
            if (a.Guid.Length > 0)
            {
                keys.Add(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\" + a.Guid);
                keys.Add(@"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters\Interfaces\" + a.Guid);
                keys.Add(@"SYSTEM\CurrentControlSet\Control\Network\{4d36e972-e325-11ce-bfc1-08002be10318}\" + a.Guid);
            }
            if (keys.Count == 0)
            {
                Ui.Warn(this, "У адаптера не найдены разделы реестра.");
                return;
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Будет удалена запись скрытого (отсутствующего) адаптера:");
            sb.AppendLine();
            sb.AppendLine("    " + a.Title);
            sb.AppendLine("    " + a.Description);
            sb.AppendLine();
            sb.AppendLine("Разделы реестра:");
            for (int i = 0; i < keys.Count; i++) sb.AppendLine(@"    HKLM\" + keys[i]);
            sb.AppendLine();
            sb.AppendLine("Перед удалением каждый существующий раздел будет сохранён в .reg-файл (папка backup).");
            sb.AppendLine();
            sb.AppendLine("Продолжить?");

            if (!Ui.AskDanger(this, "Удаление записи из реестра", sb.ToString())) return;
            RunOptions o = new RunOptions();
            o.Title = "Удаление записи адаптера из реестра";
            o.Danger = true;
            o.Confirm = false;
            string comment = "Резервная копия перед удалением: " + a.Title;
            await Runner.Run(this, _core, null, o, delegate
            {
                return Runner.PrepareRegistryDeletion(Registry.LocalMachine, keys, comment);
            });
        }

        private void OpenNetworkConnections()
        {
            try { Process.Start("ncpa.cpl"); }
            catch (Exception ex) { Ui.Err(this, "Не удалось открыть: " + ex.Message); }
        }

        private void OpenDeviceManager()
        {
            try
            {
                Process.Start("cmd.exe", "/c set devmgr_show_nonpresent_devices=1 & start devmgmt.msc");
                Ui.Msg(this, "Диспетчер устройств открыт. Чтобы увидеть скрытые (отсутствующие) устройства:" + Environment.NewLine +
                    "Вид → Показать скрытые устройства.");
            }
            catch (Exception ex) { Ui.Err(this, "Не удалось открыть: " + ex.Message); }
        }

        private void ShowInRegistry()
        {
            AdapterInfo a = Current();
            if (a == null)
            {
                try { Process.Start("regedit.exe"); }
                catch { }
                return;
            }
            try
            {
                Process.Start("regedit.exe");
                Ui.Msg(this, "В редакторе реестра откройте ветку:" + Environment.NewLine + Environment.NewLine +
                    (a.RegClassPath.Length > 0 ? a.RegClassPath : @"HKLM\" + Reg.NetClassPath) + Environment.NewLine +
                    (a.RegTcpip.Count > 0 ? a.RegTcpipPath : ""));
            }
            catch (Exception ex) { Ui.Err(this, "Не удалось открыть редактор реестра: " + ex.Message); }
        }
    }
}
