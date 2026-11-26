using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace NetHelp
{
    /// <summary>
    /// Автоматическая проверка вёрстки (режим --uitest): ищет элементы,
    /// которые выходят за границы контейнера или перекрывают друг друга.
    /// </summary>
    public static class UiAudit
    {
        public static string Run()
        {
            return Run(null);
        }

        /// <summary>Проверка вёрстки. Если задан photosDir — сохраняет снимок каждой вкладки в PNG.</summary>
        public static string Run(string photosDir)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Проверка вёрстки интерфейса NetHelp");
            sb.AppendLine("Разрешение / DPI: " + SystemInformation.PrimaryMonitorSize + ", шрифт системы: " + SystemInformation.MenuFont);
            sb.AppendLine();

            MainForm f = new MainForm();
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(-32000, -32000); // за пределами экрана, но с реальной разметкой
            f.ShowInTaskbar = false;
            f.Show();
            Application.DoEvents();

            TabControl tc = FindTab(f);
            if (tc == null)
            {
                sb.AppendLine("Не найден TabControl!");
                f.Close();
                return sb.ToString();
            }

            int problems = 0;

            MenuStrip menu = f.MainMenuStrip;
            if (menu != null)
            {
                StringBuilder mb = new StringBuilder();
                for (int i = 0; i < menu.Items.Count; i++)
                {
                    if (mb.Length > 0) mb.Append(", ");
                    mb.Append(menu.Items[i].Text);
                }
                sb.AppendLine("Меню верхнего уровня: " + mb);
                for (int i = 0; i < menu.Items.Count; i++)
                    if (menu.Items[i].Text == "Вид")
                        sb.AppendLine("  ЗАМЕЧАНИЕ: осталось меню «Вид» — переключение разделов должно быть только вкладками");
            }
            sb.AppendLine("Разделы (вкладки вверху окна): " + tc.TabCount);
            for (int i = 0; i < tc.TabCount; i++)
                sb.AppendLine("  " + (i + 1) + ") " + tc.TabPages[i].Text + "   Ctrl+" + (i + 1) +
                              (tc.TabPages[i].ToolTipText.Length > 0 ? "   — " + tc.TabPages[i].ToolTipText : ""));
            sb.AppendLine();

            // Прогоняем вёрстку в нескольких размерах окна и в обоих режимах прав:
            // так ловятся дефекты, которые видны не в «идеальном» окне.
            Size[] sizes = new Size[]
            {
                new Size(1240, 780),   // обычный запуск
                new Size(1000, 640),   // минимально допустимый размер окна
                new Size(1600, 900)    // развёрнутое окно
            };
            for (int s = 0; s < sizes.Length; s++)
            {
                for (int admin = 0; admin < 2; admin++)
                {
                    bool banner = (admin == 1);
                    f.Size = sizes[s];
                    if (banner) SetBanner(f, true);
                    Application.DoEvents();
                    Thread.Sleep(120);
                    Application.DoEvents();

                    string config = "окно " + sizes[s].Width + "x" + sizes[s].Height +
                                    (banner ? ", запуск без прав администратора (баннер включён)" : ", права администратора есть");
                    sb.AppendLine("############ " + config + " ############");
                    int before = problems;
                    problems += AuditTopLevel(f, tc, sb);

                    for (int i = 0; i < tc.TabPages.Count; i++)
                    {
                        tc.SelectedIndex = i;
                        Application.DoEvents();
                        Thread.Sleep(60);
                        Application.DoEvents();
                        StringBuilder inner = new StringBuilder();
                        Audit(tc.TabPages[i], inner, 0);
                        problems += AuditClickable(f, inner);
                        if (inner.Length > 0)
                        {
                            sb.AppendLine("--- вкладка " + (i + 1) + ": " + tc.TabPages[i].Text + " ---");
                            sb.Append(inner);
                            problems += CountLines(inner.ToString());
                        }
                        if (photosDir != null && !banner && s == 0) SavePhoto(f, i, tc.TabPages[i].Text, photosDir, sb);
                    }
                    sb.AppendLine(before == problems ? "  замечаний нет" : "  замечаний в этой конфигурации: " + (problems - before));
                    sb.AppendLine();
                    SetBanner(f, false);
                }
            }
            if (photosDir != null) sb.AppendLine("Снимки вкладок сохранены в: " + photosDir);

            sb.AppendLine("Всего замечаний: " + problems);
            f.Close();
            return sb.ToString();
        }

        /// <summary>Показывает/скрывает баннер о правах администратора.</summary>
        private static void SetBanner(Form f, bool visible)
        {
            for (int i = 0; i < f.Controls.Count; i++)
            {
                Panel p = f.Controls[i] as Panel;
                if (p != null) p.Visible = visible;
            }
        }

        /// <summary>
        /// Главная проверка «видно ли элемент»: берём центр каждого управляющего элемента
        /// и выясняем, что реально находится в этой точке экрана. Если там другой элемент —
        /// значит кнопка/поле скрыты под ним (как было с кнопками запуска в «Пинге»).
        /// </summary>
        private static int AuditClickable(Form f, StringBuilder sb)
        {
            List<Control> all = new List<Control>();
            CollectControls(f, all);
            int problems = 0;
            for (int i = 0; i < all.Count; i++)
            {
                Control c = all[i];
                if (!IsInteractive(c)) continue;
                if (!ReallyVisible(c)) continue;
                if (c.Width < 6 || c.Height < 6) continue;
                if (!CenterInsideAllAncestors(c)) continue; // обрезано контейнером или требует прокрутки — ловит проверка границ

                Point center = c.PointToScreen(new Point(c.Width / 2, c.Height / 2));
                Control hit = DeepHit(f, center);
                if (hit == null || hit == c || IsDescendant(hit, c)) continue;
                sb.AppendLine("  ЗАКРЫТ ДРУГИМ ЭЛЕМЕНТОМ: " + Describe(c) + "  перекрыт  " + Describe(hit) +
                              "  (центр " + center + ")");
                problems++;
            }
            return problems;
        }

        private static void CollectControls(Control parent, List<Control> list)
        {
            foreach (Control c in parent.Controls)
            {
                list.Add(c);
                CollectControls(c, list);
            }
        }

        private static bool IsInteractive(Control c)
        {
            return c is Button || c is CheckBox || c is RadioButton || c is TextBox || c is ComboBox ||
                   c is NumericUpDown || c is ListBox || c is DataGridView || c is ListView ||
                   c is RichTextBox || c is TabControl || c is ProgressBar;
        }

        private static bool ReallyVisible(Control c)
        {
            Control p = c;
            while (p != null)
            {
                if (!p.Visible) return false;
                p = p.Parent;
            }
            return true;
        }

        private static bool IsDescendant(Control maybeChild, Control parent)
        {
            Control p = maybeChild;
            while (p != null)
            {
                if (p == parent) return true;
                p = p.Parent;
            }
            return false;
        }

        /// <summary>Центр элемента лежит внутри клиентской области всех родителей (значит, элемент не обрезан).</summary>
        private static bool CenterInsideAllAncestors(Control c)
        {
            Point center = c.PointToScreen(new Point(c.Width / 2, c.Height / 2));
            Control p = c.Parent;
            while (p != null)
            {
                Point inParent = p.PointToClient(center);
                if (!p.ClientRectangle.Contains(inParent)) return false;
                p = p.Parent;
            }
            return true;
        }

        private static Control DeepHit(Control parent, Point screenPoint)
        {
            Point p = parent.PointToClient(screenPoint);
            if (!parent.ClientRectangle.Contains(p)) return parent;
            Control child = parent.GetChildAtPoint(p, GetChildAtPointSkip.Invisible);
            if (child == null) return parent;
            return DeepHit(child, screenPoint);
        }

        /// <summary>Снимок текущей вкладки средствами самой формы (без снимков экрана).</summary>
        private static void SavePhoto(Form f, int index, string title, string dir, StringBuilder sb)
        {
            try
            {
                System.IO.Directory.CreateDirectory(dir);
                string safe = title.Replace("/", "-").Replace("\\", "-").Replace(":", "").Trim();
                string file = System.IO.Path.Combine(dir, (index + 1).ToString("00") + "-" + safe + ".png");
                using (Bitmap bmp = new Bitmap(f.Width, f.Height))
                {
                    f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                    bmp.Save(file, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("  не удалось сохранить снимок вкладки: " + ex.Message);
            }
        }

        /// <summary>Проверка разметки верхнего уровня: не перекрывает ли меню/панель полосу вкладок.</summary>
        private static int AuditTopLevel(Form f, TabControl tc, StringBuilder sb)
        {
            int problems = 0;
            sb.AppendLine("Разметка верхнего уровня (координаты в клиентской области формы):");
            for (int i = 0; i < f.Controls.Count; i++)
            {
                Control c = f.Controls[i];
                Rectangle r = f.RectangleToClient(c.RectangleToScreen(c.ClientRectangle));
                sb.AppendLine("  " + (c.Visible ? " " : "*") + Describe(c) + "  Dock=" + c.Dock + "  ->  " + r +
                              (c.Visible ? "" : "   (скрыт)"));
            }

            Rectangle tabsClient = f.RectangleToClient(tc.RectangleToScreen(tc.ClientRectangle));
            int stripH = tc.ItemSize.Height + 8;
            Rectangle strip = new Rectangle(tabsClient.Left, tabsClient.Top, tabsClient.Width, stripH);
            sb.AppendLine("  Полоса вкладок (оценка): " + strip + "  ItemSize=" + tc.ItemSize);
            if (tabsClient.Top <= 0 && tabsClient.Height > 0)
            {
                sb.AppendLine("  ЗАМЕЧАНИЕ: TabControl начинается от самой кромки клиентской области (Top=" + tabsClient.Top + ")");
                problems++;
            }
            for (int i = 0; i < f.Controls.Count; i++)
            {
                Control c = f.Controls[i];
                if (c == tc || !c.Visible) continue;
                Rectangle r = f.RectangleToClient(c.RectangleToScreen(c.ClientRectangle));
                if (!r.IntersectsWith(strip)) continue;
                Rectangle inter = Rectangle.Intersect(r, strip);
                sb.AppendLine("  ЗАМЕЧАНИЕ: полосу вкладок перекрывает " + Describe(c) + " — по высоте " + inter.Height +
                              " из " + stripH + " px (видно только " + Math.Max(0, stripH - inter.Height) + " px вкладок)");
                problems++;
            }
            sb.AppendLine();
            return problems;
        }

        private static int CountLines(string s)
        {
            int n = 0;
            for (int i = 0; i < s.Length; i++) if (s[i] == '\n') n++;
            return n;
        }

        private static TabControl FindTab(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                TabControl tc = c as TabControl;
                if (tc != null) return tc;
                TabControl r = FindTab(c);
                if (r != null) return r;
            }
            return null;
        }

        private static bool IsContainer(Control c)
        {
            if (c is Panel || c is GroupBox || c is UserControl || c is TabControl || c is TabPage) return true;
            if (c is SplitContainer) return true;
            return false;
        }

        private static string Describe(Control c)
        {
            string t = c.Text;
            if (t != null && t.Length > 28) t = t.Substring(0, 28) + "…";
            t = (t == null ? "" : t).Replace("\r", " ").Replace("\n", " ");
            return c.GetType().Name + (t.Length > 0 ? " \"" + t + "\"" : "");
        }

        private static void Audit(Control parent, StringBuilder sb, int depth)
        {
            string pad = new string(' ', 2 + depth * 2);
            List<Control> kids = new List<Control>();
            foreach (Control c in parent.Controls) if (c.Visible) kids.Add(c);
            if (kids.Count == 0) return;

            Size client = parent.ClientSize;
            ScrollableControl scrollable = parent as ScrollableControl;
            bool skipBounds = (scrollable != null && scrollable.AutoScroll) || client.Width < 20 || client.Height < 10;

            // Не хватает места для пристыкованных панелей: суммарная высота (или ширина) больше контейнера,
            // поэтому часть элементов уезжает под соседнюю панель и становится невидимой.
            int needH = 0;
            int needW = 0;
            for (int i = 0; i < kids.Count; i++)
            {
                Control c = kids[i];
                if (c.Dock == DockStyle.Top || c.Dock == DockStyle.Bottom) needH += c.Height;
                if (c.Dock == DockStyle.Left || c.Dock == DockStyle.Right) needW += c.Width;
            }
            if (needH > client.Height)
            {
                sb.AppendLine(pad + "НЕ ХВАТАЕТ МЕСТА ПО ВЫСОТЕ в " + Describe(parent) + ": пристыкованные панели требуют " +
                              needH + " px, доступно " + client.Height + " px — часть кнопок/полей скрыта");
            }
            if (needW > client.Width)
            {
                sb.AppendLine(pad + "НЕ ХВАТАЕТ МЕСТА ПО ШИРИНЕ в " + Describe(parent) + ": пристыкованные панели требуют " +
                              needW + " px, доступно " + client.Width + " px");
            }

            for (int i = 0; i < kids.Count; i++)
            {
                Control c = kids[i];
                if (skipBounds) break;
                if (c.Dock != DockStyle.None) continue;
                if (c.Right > client.Width + 2 || c.Bottom > client.Height + 2 || c.Left < -2 || c.Top < -2)
                {
                    sb.AppendLine(pad + "ВЫХОДИТ ЗА ГРАНИЦЫ: " + Describe(c) + "  " + c.Bounds +
                                  "   в контейнере " + Describe(parent) + " (" + client.Width + "x" + client.Height + ")");
                }
            }

            for (int i = 0; i < kids.Count; i++)
            {
                for (int j = i + 1; j < kids.Count; j++)
                {
                    Control a = kids[i];
                    Control b = kids[j];
                    Rectangle ra = a.Bounds;
                    Rectangle rb = b.Bounds;
                    if (!ra.IntersectsWith(rb)) continue;
                    Rectangle inter = Rectangle.Intersect(ra, rb);
                    if (inter.Width <= 3 || inter.Height <= 3) continue;
                    sb.AppendLine(pad + "ПЕРЕКРЫТИЕ (" + inter.Width + "x" + inter.Height + "): " +
                                  Describe(a) + " " + ra + "  и  " + Describe(b) + " " + rb);
                }
            }

            for (int i = 0; i < kids.Count; i++)
            {
                Control c = kids[i];
                if (c is SplitContainer)
                {
                    SplitContainer sc = (SplitContainer)c;
                    Audit(sc.Panel1, sb, depth + 1);
                    Audit(sc.Panel2, sb, depth + 1);
                    continue;
                }
                if (IsContainer(c)) Audit(c, sb, depth + 1);
            }
        }
    }
}
