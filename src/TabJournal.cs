using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace NetHelp
{
    /// <summary>Общий журнал: все выполненные команды и их вывод.</summary>
    public class TabJournal : UserControl
    {
        private Core _core;
        private RichTextBox _box;
        private CheckBox _autoScroll;

        public TabJournal(Core core)
        {
            _core = core;
            Dock = DockStyle.Fill;
            BackColor = Ui.Bg;

            Panel top = new Panel();
            top.Dock = DockStyle.Top;
            top.Height = 36;
            top.BackColor = Ui.Bg;

            int x = 8;
            top.Controls.Add(Ui.Button("Очистить", x, 4, 90, delegate { _box.Clear(); })); x += 96;
            top.Controls.Add(Ui.Button("Копировать всё", x, 4, 120, delegate
            {
                if (_box.TextLength > 0) Clipboard.SetText(_box.Text);
            })); x += 126;
            top.Controls.Add(Ui.Button("Сохранить в файл", x, 4, 130, delegate { SaveLog(); })); x += 136;
            top.Controls.Add(Ui.Button("Открыть журнал-файл", x, 4, 150, delegate
            {
                try { Runner.OpenConsole("type \"" + AppSettings.JournalPath + "\""); }
                catch { }
            })); x += 156;
            _autoScroll = Ui.Check("Прокручивать вниз", x, 8, true, null);
            top.Controls.Add(_autoScroll);

            _box = Ui.LogBox();
            _box.Dock = DockStyle.Fill;

            Controls.Add(_box);
            Controls.Add(top);

            core.JournalEntry += OnJournal;
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
            string prefix = "      ";
            if (kind == "cmd") { c = Color.FromArgb(120, 200, 255); prefix = time + " "; }
            else if (kind == "ok") { c = Color.FromArgb(120, 220, 120); prefix = time + " "; }
            else if (kind == "err") { c = Color.FromArgb(255, 130, 130); prefix = time + " "; }
            else if (kind == "info") { c = Color.FromArgb(230, 210, 120); prefix = time + " "; }

            if (!_autoScroll.Checked)
            {
                int sel = _box.SelectionStart;
                _box.SelectionStart = _box.TextLength;
                _box.SelectionLength = 0;
                _box.SelectionColor = c;
                _box.AppendText(prefix + text + Environment.NewLine);
                _box.SelectionColor = _box.ForeColor;
                _box.SelectionStart = sel;
                return;
            }
            Ui.Log(_box, prefix + text, c);
        }

        private void SaveLog()
        {
            try
            {
                string dir = Path.Combine(AppSettings.DataDir, "reports");
                Directory.CreateDirectory(dir);
                string f = Path.Combine(dir, "journal-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
                File.WriteAllText(f, _box.Text, Encoding.UTF8);
                Ui.Msg(this, "Журнал сохранён:" + Environment.NewLine + f);
            }
            catch (Exception ex)
            {
                Ui.Err(this, "Не удалось сохранить журнал: " + ex.Message);
            }
        }
    }
}
