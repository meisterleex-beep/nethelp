using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace NetHelp
{
    /// <summary>Кнопки сброса и восстановления сетевых настроек.</summary>
    public class TabReset : UserControl
    {
        private Core _core;
        private ListView _list;
        private TextBox _desc;
        private RichTextBox _log;
        private CheckBox _cbConsole;
        private Label _lblWarn;
        private List<ResetAction> _actions = new List<ResetAction>();

        public TabReset(Core core)
        {
            _core = core;
            Dock = DockStyle.Fill;
            BackColor = Ui.Bg;

            // ---------- предупреждение ----------
            Panel warn = new Panel();
            warn.Dock = DockStyle.Top;
            warn.Height = 30;
            warn.BackColor = Shell.IsAdmin ? Color.FromArgb(232, 246, 232) : Color.FromArgb(255, 240, 240);
            _lblWarn = new Label();
            _lblWarn.Dock = DockStyle.Fill;
            _lblWarn.Padding = new Padding(8, 7, 0, 0);
            _lblWarn.Font = Ui.BaseFont;
            _lblWarn.ForeColor = Shell.IsAdmin ? Color.FromArgb(20, 90, 20) : Color.FromArgb(150, 30, 30);
            _lblWarn.Text = Shell.IsAdmin
                ? "Права администратора есть — все действия доступны."
                : "Нет прав администратора: команды сброса не выполнятся. Нажмите «Запустить с правами администратора» вверху окна.";
            warn.Controls.Add(_lblWarn);

            // ---------- список действий ----------
            Panel left = new Panel();
            left.Dock = DockStyle.Left;
            left.Width = 430;
            left.Padding = new Padding(0, 4, 6, 0);

            _list = new ListView();
            _list.Dock = DockStyle.Fill;
            _list.View = View.Details;
            _list.FullRowSelect = true;
            _list.MultiSelect = false;
            _list.HideSelection = false;
            _list.Font = Ui.BaseFont;
            _list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            _list.Columns.Add("Действие", 300);
            _list.Columns.Add("Признак", 110);
            _list.SelectedIndexChanged += delegate { ShowDescription(); };
            _list.DoubleClick += delegate { Execute(); };

            BuildList();
            left.Controls.Add(_list);

            // ---------- правая часть ----------
            Panel right = new Panel();
            right.Dock = DockStyle.Fill;
            right.Padding = new Padding(6, 4, 0, 0);

            // Кнопки сверху: при узком окне переносятся на вторую строку и не обрезаются.
            FlowLayoutPanel rTop = new FlowLayoutPanel();
            rTop.Dock = DockStyle.Top;
            rTop.AutoSize = true;
            rTop.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            rTop.WrapContents = true;
            rTop.Padding = new Padding(0, 4, 0, 2);

            Button bRun = Ui.Button("ВЫПОЛНИТЬ", 0, 0, 150, delegate { Execute(); });
            bRun.Margin = new Padding(0, 0, 6, 4);
            rTop.Controls.Add(bRun);
            Button bCopy = Ui.Button("Команды в буфер", 0, 0, 150, delegate
            {
                ResetAction a = CurrentAction();
                if (a == null) return;
                Clipboard.SetText(string.Join(Environment.NewLine, a.Commands));
            });
            bCopy.Margin = new Padding(0, 0, 6, 4);
            rTop.Controls.Add(bCopy);
            Button bLog = Ui.Button("Открыть файл журнала", 0, 0, 160, delegate
            {
                try { Runner.OpenConsole("type \"" + AppSettings.JournalPath + "\""); }
                catch { }
            });
            bLog.Margin = new Padding(0, 0, 12, 4);
            rTop.Controls.Add(bLog);
            _cbConsole = Ui.Check("Выполнить в окне консоли (видно вывод)", 0, 0, false, null);
            _cbConsole.Margin = new Padding(0, 6, 0, 4);
            rTop.Controls.Add(_cbConsole);

            _desc = Ui.MonoText();
            _desc.Dock = DockStyle.Top;
            _desc.Height = 170;
            _desc.ScrollBars = ScrollBars.Vertical;
            _desc.BackColor = Color.FromArgb(250, 250, 252);

            _log = Ui.LogBox();
            _log.Dock = DockStyle.Fill;

            right.Controls.Add(_log);
            right.Controls.Add(_desc);
            right.Controls.Add(rTop);

            Controls.Add(right);
            Controls.Add(left);
            Controls.Add(warn);

            core.JournalEntry += OnJournal;
            if (_list.Items.Count > 0) _list.Items[0].Selected = true;
        }

        private void BuildList()
        {
            _actions = ResetCatalog.All();
            Dictionary<string, ListViewGroup> groups = new Dictionary<string, ListViewGroup>();
            for (int i = 0; i < _actions.Count; i++)
            {
                ResetAction a = _actions[i];
                ListViewGroup g;
                if (!groups.TryGetValue(a.Group, out g))
                {
                    g = new ListViewGroup(a.Group);
                    groups[a.Group] = g;
                    _list.Groups.Add(g);
                }
                ListViewItem it = new ListViewItem(a.Title);
                it.SubItems.Add(Marks(a));
                it.Tag = a;
                it.Group = g;
                if (a.Danger) it.ForeColor = Ui.ErrColor;
                else if (a.NeedsReboot) it.ForeColor = Color.FromArgb(140, 80, 0);
                _list.Items.Add(it);
            }
        }

        private static string Marks(ResetAction a)
        {
            StringBuilder sb = new StringBuilder();
            if (a.NeedsAdmin) sb.Append("админ ");
            if (a.NeedsReboot) sb.Append("+перезагруз ");
            if (a.Danger) sb.Append("осторожно");
            string s = sb.ToString().Trim();
            return s.Length > 0 ? s : "безопасно";
        }

        private ResetAction CurrentAction()
        {
            if (_list.SelectedItems.Count == 0) return null;
            return _list.SelectedItems[0].Tag as ResetAction;
        }

        private void ShowDescription()
        {
            ResetAction a = CurrentAction();
            if (a == null) { _desc.Text = ""; return; }
            _desc.Text = a.FullDescription();
        }

        private async void Execute()
        {
            ResetAction a = CurrentAction();
            if (a == null) return;

            if (_cbConsole.Checked)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < a.Commands.Length; i++)
                {
                    if (a.Commands[i].StartsWith("@wait:")) continue;
                    if (sb.Length > 0) sb.Append(" & ");
                    sb.Append(a.Commands[i]);
                }
                await Runner.RunInConsole(this, _core, a.Title, sb.ToString(), a.NeedsAdmin);
                return;
            }

            RunOptions o = new RunOptions();
            o.Title = a.Title;
            o.NeedsAdmin = a.NeedsAdmin;
            o.Danger = a.Danger;
            o.Confirm = true;
            o.ContinueOnError = true;
            bool completed = await Runner.Run(this, _core, a.Commands, o);
            if (!completed || !a.NeedsReboot || IsDisposed) return;

            if (Ui.Ask(this, "Для полного применения изменений нужна перезагрузка Windows." +
                Environment.NewLine + Environment.NewLine + "Перезагрузить сейчас (через 15 секунд, можно отменить командой shutdown /a)?"))
            {
                RunOptions r = new RunOptions();
                r.Title = "Перезагрузка";
                r.Confirm = false;
                r.RefreshAfter = false;
                await Runner.Run(this, _core, new string[] { "shutdown /r /t 15 /c \"NetHelp: перезагрузка после сброса сети\"" }, r);
            }
        }

        private void OnJournal(string kind, string text, string time)
        {
            if (InvokeRequired)
            {
                try { BeginInvoke((MethodInvoker)delegate { OnJournal(kind, text, time); }); }
                catch { }
                return;
            }
            Color c = Ui.LogFore;
            if (kind == "cmd") c = Color.FromArgb(120, 200, 255);
            else if (kind == "ok") c = Color.FromArgb(120, 220, 120);
            else if (kind == "err") c = Color.FromArgb(255, 130, 130);
            else if (kind == "info") c = Color.FromArgb(230, 210, 120);
            Ui.Log(_log, text, c);
        }
    }
}
