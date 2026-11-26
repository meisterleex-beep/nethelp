using System;
using System.Drawing;
using System.Windows.Forms;

namespace NetHelp
{
    /// <summary>Мелкие помощники для единообразного вида интерфейса.</summary>
    public static class Ui
    {
        public static readonly Font BaseFont = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font SmallFont = new Font("Segoe UI", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font BoldFont = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
        public static readonly Font MonoFont = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font TitleFont = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point);

        public static readonly Color Bg = Color.FromArgb(250, 250, 252);
        public static readonly Color Accent = Color.FromArgb(0, 102, 184);
        public static readonly Color OkColor = Color.FromArgb(16, 124, 16);
        public static readonly Color WarnColor = Color.FromArgb(170, 100, 0);
        public static readonly Color ErrColor = Color.FromArgb(190, 40, 40);
        public static readonly Color MutedColor = Color.FromArgb(110, 110, 118);
        public static readonly Color LogBack = Color.FromArgb(28, 30, 34);
        public static readonly Color LogFore = Color.FromArgb(222, 226, 230);

        public static Label Label(string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.Location = new Point(x, y);
            l.AutoSize = true;
            l.Font = BaseFont;
            return l;
        }

        public static Label Label(string text, int x, int y, int w)
        {
            Label l = Label(text, x, y);
            l.AutoSize = false;
            l.Width = w;
            l.Height = 18;
            return l;
        }

        public static Label Hint(string text, int x, int y, int w)
        {
            Label l = new Label();
            l.Text = text;
            l.Location = new Point(x, y);
            l.Size = new Size(w, 32);
            l.Font = SmallFont;
            l.ForeColor = MutedColor;
            return l;
        }

        public static TextBox Text(int x, int y, int w)
        {
            TextBox t = new TextBox();
            t.Location = new Point(x, y);
            t.Width = w;
            t.Font = BaseFont;
            return t;
        }

        public static Button Button(string text, int x, int y, int w, EventHandler onClick)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(w, 27);
            b.Font = BaseFont;
            b.FlatStyle = FlatStyle.System;
            b.UseVisualStyleBackColor = true;
            if (onClick != null) b.Click += onClick;
            return b;
        }

        public static CheckBox Check(string text, int x, int y, bool value, EventHandler onChanged)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.Location = new Point(x, y);
            c.AutoSize = true;
            c.Font = BaseFont;
            c.Checked = value;
            if (onChanged != null) c.CheckedChanged += onChanged;
            return c;
        }

        public static GroupBox Group(string text, int x, int y, int w, int h)
        {
            GroupBox g = new GroupBox();
            g.Text = text;
            g.Location = new Point(x, y);
            g.Size = new Size(w, h);
            g.Font = BaseFont;
            g.ForeColor = Color.FromArgb(50, 50, 55);
            return g;
        }

        public static ComboBox Combo(int x, int y, int w, string[] items, int selected)
        {
            ComboBox c = new ComboBox();
            c.Location = new Point(x, y);
            c.Width = w;
            c.Font = BaseFont;
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            if (items != null) c.Items.AddRange(items);
            if (selected >= 0 && selected < c.Items.Count) c.SelectedIndex = selected;
            return c;
        }

        public static DataGridView Grid()
        {
            DataGridView g = new DataGridView();
            g.Font = BaseFont;
            g.BackgroundColor = Color.White;
            g.BorderStyle = BorderStyle.FixedSingle;
            g.GridColor = Color.FromArgb(228, 230, 234);
            g.RowHeadersVisible = false;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.ReadOnly = true;
            g.MultiSelect = true;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ColumnHeadersHeight = 28;
            g.RowTemplate.Height = 22;
            g.EnableHeadersVisualStyles = false;
            g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(238, 241, 245);
            g.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(40, 40, 45);
            g.ColumnHeadersDefaultCellStyle.Font = BoldFont;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(238, 241, 245);
            g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(204, 226, 248);
            g.DefaultCellStyle.SelectionForeColor = Color.Black;
            g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 251, 253);
            g.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
            return g;
        }

        public static void Columns(DataGridView g, string[] names, float[] weights)
        {
            g.Columns.Clear();
            for (int i = 0; i < names.Length; i++)
            {
                DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn();
                c.HeaderText = names[i];
                c.Name = names[i];
                c.SortMode = DataGridViewColumnSortMode.Automatic;
                if (weights != null && i < weights.Length) c.FillWeight = weights[i];
                g.Columns.Add(c);
            }
        }

        public static RichTextBox LogBox()
        {
            RichTextBox r = new RichTextBox();
            r.Font = MonoFont;
            r.BackColor = LogBack;
            r.ForeColor = LogFore;
            r.BorderStyle = BorderStyle.None;
            r.ReadOnly = true;
            r.WordWrap = false;
            r.DetectUrls = false;
            r.HideSelection = false;
            return r;
        }

        public static TextBox MonoText()
        {
            TextBox t = new TextBox();
            t.Font = MonoFont;
            t.Multiline = true;
            t.ReadOnly = true;
            t.ScrollBars = ScrollBars.Both;
            t.WordWrap = false;
            t.BackColor = Color.White;
            t.BorderStyle = BorderStyle.FixedSingle;
            return t;
        }

        public static void Log(RichTextBox box, string text, Color color)
        {
            if (box == null) return;
            bool atEnd = true;
            try { atEnd = (box.SelectionStart >= box.TextLength - 1); } catch { }
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;
            box.SelectionColor = color;
            box.AppendText(text + Environment.NewLine);
            box.SelectionColor = box.ForeColor;
            if (atEnd) box.ScrollToCaret();
        }

        public static void Log(RichTextBox box, string text)
        {
            Log(box, text, LogFore);
        }

        public static void Msg(IWin32Window owner, string text)
        {
            MessageBox.Show(owner, text, "NetHelp", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static void Warn(IWin32Window owner, string text)
        {
            MessageBox.Show(owner, text, "NetHelp", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public static void Err(IWin32Window owner, string text)
        {
            MessageBox.Show(owner, text, "NetHelp", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static bool Ask(IWin32Window owner, string text)
        {
            return MessageBox.Show(owner, text, "NetHelp", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        public static bool AskDanger(IWin32Window owner, string title, string text)
        {
            return MessageBox.Show(owner, text, title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }
    }
}
