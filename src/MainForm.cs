using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace NetHelp
{
    public class MainForm : Form
    {
        public const string Version = "1.0";

        private Core _core = new Core();
        private TabControl _tabs;
        private TabAdapters _tabAdapters;
        private TabIpDns _tabIpDns;
        private TabReset _tabReset;
        private TabPing _tabPing;
        private TabTrace _tabTrace;
        private TabTools _tabTools;
        private TabJournal _tabJournal;
        private ToolStripStatusLabel _stAdmin;
        private ToolStripStatusLabel _stCount;
        private ToolStripStatusLabel _stData;
        private ToolStripStatusLabel _stState;

        public MainForm()
        {
            Text = "NetHelp " + Version + " — сетевые адаптеры, сброс сети, диагностика";
            BackColor = Ui.Bg;
            Font = Ui.BaseFont;
            AutoScaleMode = AutoScaleMode.Font;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1240, 780);
            MinimumSize = new Size(1000, 640);
            using (Stream icon = typeof(MainForm).Assembly.GetManifestResourceStream("NetHelp.NetHelp.ico"))
                Icon = new Icon(icon);

            _tabs = new TabControl();
            _tabs.Dock = DockStyle.Fill;
            _tabs.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            _tabs.Padding = new Point(20, 7);
            _tabs.ShowToolTips = true;
            _tabs.SelectedIndexChanged += delegate
            {
                _core.Settings.LastTab = _tabs.SelectedIndex;
            };

            _tabAdapters = new TabAdapters(_core);
            _tabIpDns = new TabIpDns(_core);
            _tabReset = new TabReset(_core);
            _tabPing = new TabPing(_core);
            _tabTrace = new TabTrace(_core);
            _tabTools = new TabTools(_core);
            _tabJournal = new TabJournal(_core);

            AddPage("Адаптеры", "Все сетевые адаптеры, включая скрытые в реестре, и их настройки   (Ctrl+1)", _tabAdapters);
            AddPage("IP / DNS", "IP-адрес, DNS, MTU, метрика, IPv6 выбранного адаптера   (Ctrl+2)", _tabIpDns);
            AddPage("Сброс сети", "Кнопки сброса DNS, DHCP, Winsock, TCP/IP, служб и брандмауэра   (Ctrl+3)", _tabReset);
            AddPage("Пинг", "Пинг списка сайтов и адресов, потери пакетов   (Ctrl+4)", _tabPing);
            AddPage("Трассировка", "Трассировка, pathping, nslookup, подбор MTU, скан подсети   (Ctrl+5)", _tabTrace);
            AddPage("Инструменты", "Порты, Wake-on-LAN, скорость, внешний IP, быстрые команды, отчёты   (Ctrl+6)", _tabTools);
            AddPage("Журнал", "Все выполненные команды и их вывод   (Ctrl+7)", _tabJournal);

            StatusStrip status = new StatusStrip();
            status.Font = Ui.BaseFont;
            ToolStripStatusLabel stHint = new ToolStripStatusLabel();
            stHint.Text = "Разделы — вкладки вверху окна: Ctrl+1…7 или Ctrl+Tab";
            stHint.ForeColor = Ui.MutedColor;
            _stAdmin = new ToolStripStatusLabel();
            _stCount = new ToolStripStatusLabel();
            _stData = new ToolStripStatusLabel();
            _stState = new ToolStripStatusLabel();
            _stState.Spring = true;
            _stState.TextAlign = ContentAlignment.MiddleRight;
            status.Items.Add(stHint);
            status.Items.Add(new ToolStripSeparator());
            status.Items.Add(_stAdmin);
            status.Items.Add(new ToolStripSeparator());
            status.Items.Add(_stCount);
            status.Items.Add(new ToolStripSeparator());
            status.Items.Add(_stData);
            status.Items.Add(_stState);
            _stState.Text = "Готово";
            _stAdmin.Text = Shell.IsAdmin ? "Права: администратор" : "Права: ограниченные";
            _stAdmin.ForeColor = Shell.IsAdmin ? Ui.OkColor : Ui.ErrColor;
            _stData.Text = "Данные: " + AppSettings.DataDir + (AppSettings.IsPortable ? " (портативно)" : "");

            Panel warn = BuildWarnPanel();
            MenuStrip menu = BuildMenu();

            // Порядок добавления задаёт порядок пристыковки: сначала заполняющая область,
            // затем нижняя и верхние панели — меню получает кромку окна, вкладки идут под ним.
            Controls.Add(_tabs);
            Controls.Add(status);
            Controls.Add(warn);
            Controls.Add(menu);
            MainMenuStrip = menu;

            _core.InventoryChanged += delegate
            {
                _tabs.Invalidate(true);
                int hidden = 0;
                for (int i = 0; i < _core.Adapters.Count; i++) if (!_core.Adapters[i].Present) hidden++;
                _stCount.Text = "Адаптеров: " + _core.Adapters.Count + "  (скрытых: " + hidden + ")";
                _stState.Text = "Обновлено в " + DateTime.Now.ToString("HH:mm:ss");
            };
            _core.PingRequested += delegate(string host)
            {
                _tabs.SelectedTab = _tabPing.Parent as TabPage;
                _tabPing.AddAndStart(host);
            };
            _core.TraceRequested += delegate(string host)
            {
                _tabs.SelectedTab = _tabTrace.Parent as TabPage;
                _tabTrace.StartWith(host);
            };

            KeyPreview = true;
            KeyDown += OnKeyDown;
            FormClosing += delegate { try { _core.Settings.Save(); } catch { } };
            Shown += async delegate
            {
                if (_tabs.TabCount > _core.Settings.LastTab && _core.Settings.LastTab >= 0)
                    _tabs.SelectedIndex = _core.Settings.LastTab;
                _stState.Text = "Сканирую сетевые адаптеры...";
                await _core.ReloadAsync();
            };
            if (!Shell.IsAdmin)
            {
                _core.Log("info", "Программа запущена без прав администратора — доступен только просмотр. Для сброса сети перезапустите с правами администратора.");
            }
        }

        private void AddPage(string title, string tooltip, UserControl c)
        {
            TabPage p = new TabPage(title);
            p.UseVisualStyleBackColor = true;
            p.BackColor = Ui.Bg;
            p.Padding = new Padding(6);
            p.ToolTipText = tooltip;
            c.Dock = DockStyle.Fill;
            p.Controls.Add(c);
            _tabs.TabPages.Add(p);
        }

        private Panel BuildWarnPanel()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Top;
            p.Height = 36;
            p.BackColor = Color.FromArgb(255, 246, 214);
            p.Visible = !Shell.IsAdmin;

            Label l = new Label();
            l.AutoSize = false;
            l.Dock = DockStyle.Fill;
            l.Padding = new Padding(10, 9, 0, 0);
            l.Font = Ui.BaseFont;
            l.ForeColor = Color.FromArgb(120, 80, 0);
            l.Text = "Программа запущена без прав администратора: доступен просмотр и диагностика. " +
                     "Сброс сети, DHCP, DNS и изменение настроек требуют повышения прав.";
            p.Controls.Add(l);

            Button b = Ui.Button("Запустить с правами администратора", 0, 4, 240, delegate
            {
                if (Shell.RelaunchElevated("")) Application.Exit();
                else Ui.Err(this, "Не удалось получить права администратора.");
            });
            b.Dock = DockStyle.Right;
            b.Width = 240;
            b.Margin = new Padding(4);
            p.Controls.Add(b);
            return p;
        }

        private MenuStrip BuildMenu()
        {
            MenuStrip m = new MenuStrip();
            m.Font = Ui.BaseFont;

            ToolStripMenuItem file = new ToolStripMenuItem("Файл");
            file.DropDownItems.Add(Item("Сохранить полный отчёт в файл…", delegate { SaveReport(); }));
            file.DropDownItems.Add(Item("Открыть папку с отчётами", delegate { OpenFolder(Path.Combine(AppSettings.DataDir, "reports")); }));
            file.DropDownItems.Add(Item("Открыть файл настроек (nethelp.cfg)", delegate
            {
                try { Process.Start("notepad.exe", AppSettings.ConfigPath); }
                catch { }
            }));
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(Item("Выход", delegate { Close(); }));

            ToolStripMenuItem act = new ToolStripMenuItem("Действия");
            act.DropDownItems.Add(Item("Обновить сведения (F5)", async delegate
            {
                _stState.Text = "Обновляю…";
                await _core.ReloadAsync();
            }));
            act.DropDownItems.Add(new ToolStripSeparator());
            act.DropDownItems.Add(Item("Перезапустить с правами администратора", delegate
            {
                if (Shell.RelaunchElevated("")) Application.Exit();
            }));
            act.DropDownItems.Add(new ToolStripSeparator());
            act.DropDownItems.Add(Item("Очистить кэш DNS", delegate
            {
                Runner.RunSingle(this, _core, "Очистка кэша DNS", "ipconfig /flushdns", false, false, true);
            }));
            act.DropDownItems.Add(Item("Сетевые подключения (ncpa.cpl)", delegate { OpenShell("ncpa.cpl"); }));
            act.DropDownItems.Add(Item("Диспетчер устройств (devmgmt.msc)", delegate { OpenShell("devmgmt.msc"); }));
            act.DropDownItems.Add(Item("Просмотр событий (eventvwr.msc)", delegate { OpenShell("eventvwr.msc"); }));
            act.DropDownItems.Add(Item("Монитор ресурсов (resmon.exe)", delegate { OpenShell("resmon.exe"); }));
            act.DropDownItems.Add(Item("Реестр: класс сетевых адаптеров", delegate
            {
                OpenShell("regedit.exe");
                Ui.Msg(this, "Откройте в редакторе реестра ветку:" + Environment.NewLine + Environment.NewLine +
                    @"HKEY_LOCAL_MACHINE\" + Reg.NetClassPath);
            }));

            ToolStripMenuItem help = new ToolStripMenuItem("Справка");
            help.DropDownItems.Add(Item("О программе", delegate { About(); }));
            help.DropDownItems.Add(Item("Открыть README", delegate
            {
                try
                {
                    string p = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "README.md");
                    if (File.Exists(p)) Process.Start("notepad.exe", p);
                    else Ui.Msg(this, "Файл README.md не найден рядом с программой.");
                }
                catch { }
            }));

            m.Items.Add(file);
            m.Items.Add(act);
            m.Items.Add(help);
            return m;
        }

        private static ToolStripMenuItem Item(string text, EventHandler h)
        {
            ToolStripMenuItem i = new ToolStripMenuItem(text);
            i.Click += h;
            return i;
        }

        private static void OpenShell(string what)
        {
            try { Process.Start(what); }
            catch (Exception ex) { Ui.Err(null, "Не удалось открыть: " + ex.Message); }
        }

        private static void OpenFolder(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                Process.Start("explorer.exe", "\"" + path + "\"");
            }
            catch (Exception ex) { Ui.Err(null, "Не удалось открыть папку: " + ex.Message); }
        }

        private void SaveReport()
        {
            try
            {
                string f = Report.SaveReport(_core, false);
                _core.Log("ok", "Отчёт сохранён: " + f);
                if (Ui.Ask(this, "Отчёт сохранён:" + Environment.NewLine + f + Environment.NewLine + Environment.NewLine + "Открыть файл?"))
                    Process.Start("notepad.exe", f);
            }
            catch (Exception ex) { Ui.Err(this, "Не удалось сохранить отчёт: " + ex.Message); }
        }

        private void About()
        {
            string text =
                "NetHelp " + Version + " — утилита для работы с сетевыми адаптерами" + Environment.NewLine +
                "Windows 7/8/10/11, .NET Framework 4.8, работает без установки (с флешки)." + Environment.NewLine + Environment.NewLine +
                "Возможности:" + Environment.NewLine +
                "  • полный список сетевых адаптеров, включая скрытые (остатки в реестре);" + Environment.NewLine +
                "  • настройки IP, DNS, DHCP, MTU, метрики, IPv6 для каждого адаптера;" + Environment.NewLine +
                "  • сброс DNS, DHCP, Winsock, TCP/IP, брандмауэра, прокси;" + Environment.NewLine +
                "  • пинг списка сайтов, трассировка, pathping, проверка портов;" + Environment.NewLine +
                "  • Wake-on-LAN, тест скорости, внешний IP, отчёты." + Environment.NewLine + Environment.NewLine +
                "Каталог данных: " + AppSettings.DataDir + Environment.NewLine +
                "Права: " + (Shell.IsAdmin ? "администратор" : "ограниченные") + Environment.NewLine +
                "WMI: " + (Wmi.Available ? "доступен" : "недоступен");
            MessageBox.Show(this, text, "О программе NetHelp", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5)
            {
                _stState.Text = "Обновляю…";
                _core.ReloadAsync();
                e.Handled = true;
                return;
            }
            if (e.Control && (e.KeyCode == Keys.Tab || e.KeyCode == Keys.PageDown || e.KeyCode == Keys.PageUp))
            {
                int dir = (e.KeyCode == Keys.PageUp || e.Shift) ? -1 : 1;
                int n = _tabs.TabCount;
                if (n > 0) _tabs.SelectedIndex = ((_tabs.SelectedIndex + dir) % n + n) % n;
                e.Handled = true;
                return;
            }
            if (e.Control)
            {
                int idx = -1;
                if (e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D7) idx = (int)e.KeyCode - (int)Keys.D1;
                if (idx >= 0 && idx < _tabs.TabCount)
                {
                    _tabs.SelectedIndex = idx;
                    e.Handled = true;
                }
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _stCount.Text = "Адаптеров: —";
        }
    }
}
