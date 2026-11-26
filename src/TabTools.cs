using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NetHelp
{
    /// <summary>Дополнительные инструменты: порты, netstat, WoL, скорость, внешний IP, отчёты.</summary>
    public class TabTools : UserControl
    {
        private Core _core;
        private RichTextBox _out;
        private TextBox _portHost;
        private TextBox _portPort;
        private NumericUpDown _portTimeout;
        private Label _portResult;
        private TextBox _mac;
        private TextBox _broadcast;
        private TextBox _speedUrl;
        private Label _speedResult;
        private ProgressBar _speedBar;
        private TextBox _consoleCmd;
        private Button _btnSpeedGo;
        private Button _btnStopSpeed;

        public TabTools(Core core)
        {
            _core = core;
            Dock = DockStyle.Fill;
            BackColor = Ui.Bg;

            // ================= инструменты: два раздела, чтобы всё было видно без прокрутки =================
            TabControl leftTabs = new TabControl();
            leftTabs.Dock = DockStyle.Left;
            leftTabs.Width = 680;
            leftTabs.Font = Ui.BaseFont;
            leftTabs.Padding = new Point(12, 4);

            TabPage pageNet = new TabPage("Порты, скорость, интернет");
            pageNet.UseVisualStyleBackColor = true;
            pageNet.BackColor = Ui.Bg;
            pageNet.AutoScroll = true;
            pageNet.Padding = new Padding(6);

            TabPage pageCmd = new TabPage("Команды, отчёты, файлы");
            pageCmd.UseVisualStyleBackColor = true;
            pageCmd.BackColor = Ui.Bg;
            pageCmd.AutoScroll = true;
            pageCmd.Padding = new Padding(6);

            leftTabs.TabPages.Add(pageNet);
            leftTabs.TabPages.Add(pageCmd);

            // ---- 1. Проверка порта ----
            GroupBox gPort = Ui.Group("Проверка TCP-порта (доступность службы)", 4, 4, 650, 116);
            gPort.Controls.Add(Ui.Label("Узел:", 10, 26));
            _portHost = Ui.Text(60, 23, 170);
            _portHost.Text = core.Settings.PortHost;
            gPort.Controls.Add(_portHost);
            gPort.Controls.Add(Ui.Label("Порт:", 244, 26));
            _portPort = Ui.Text(292, 23, 60);
            _portPort.Text = "443";
            gPort.Controls.Add(_portPort);
            gPort.Controls.Add(Ui.Label("Таймаут, мс:", 366, 26));
            _portTimeout = new NumericUpDown();
            _portTimeout.Minimum = 100;
            _portTimeout.Maximum = 30000;
            _portTimeout.Value = 3000;
            _portTimeout.Increment = 500;
            _portTimeout.Width = 70;
            _portTimeout.Location = new Point(456, 23);
            _portTimeout.Font = Ui.BaseFont;
            gPort.Controls.Add(_portTimeout);
            gPort.Controls.Add(Ui.Button("Проверить порт", 10, 54, 140, delegate { CheckPort(); }));
            gPort.Controls.Add(Ui.Button("Популярные порты", 156, 54, 150, delegate { CheckCommonPorts(); }));
            gPort.Controls.Add(Ui.Button("Взять адрес выбранного адаптера", 312, 54, 240, delegate
            {
                AdapterInfo a = _core.Selected;
                if (a == null) { Ui.Warn(this, "Выберите адаптер на вкладке «Адаптеры»."); return; }
                _portHost.Text = a.Gateways.Count > 0 ? a.Gateways[0] : a.Ipv4Text.Split(',')[0].Trim();
            }));
            _portResult = Ui.Label("", 10, 88, 620);
            _portResult.Font = Ui.BoldFont;
            gPort.Controls.Add(_portResult);
            pageNet.Controls.Add(gPort);

            // ---- 2. Wake-on-LAN ----
            GroupBox gWol = Ui.Group("Wake-on-LAN (включить компьютер по сети)", 4, 126, 650, 110);
            gWol.Controls.Add(Ui.Label("MAC-адрес:", 10, 26));
            _mac = Ui.Text(90, 23, 180);
            gWol.Controls.Add(_mac);
            gWol.Controls.Add(Ui.Label("Широковещательный адрес:", 286, 26));
            _broadcast = Ui.Text(452, 23, 130);
            _broadcast.Text = "255.255.255.255";
            gWol.Controls.Add(_broadcast);
            gWol.Controls.Add(Ui.Button("Взять MAC выбранного адаптера", 10, 54, 230, delegate
            {
                AdapterInfo a = _core.Selected;
                if (a == null || a.Mac.Length == 0) { Ui.Warn(this, "Выберите адаптер с известным MAC-адресом."); return; }
                _mac.Text = a.Mac;
            }));
            gWol.Controls.Add(Ui.Button("Разбудить", 246, 54, 120, delegate { WakeOnLan(); }));
            gWol.Controls.Add(Ui.Hint("Работает, если на целевом ПК включён Wake-on-LAN в BIOS и в свойствах сетевого адаптера. " +
                "MAC-адрес можно вводить как AA:BB:CC:DD:EE:FF, так и AABBCCDDEEFF.", 372, 46, 270));
            pageNet.Controls.Add(gWol);

            // ---- 3. Тест скорости ----
            GroupBox gSpeed = Ui.Group("Тест скорости загрузки", 4, 242, 650, 116);
            gSpeed.Controls.Add(Ui.Label("URL файла:", 10, 26));
            _speedUrl = Ui.Text(90, 23, 420);
            _speedUrl.Text = core.Settings.SpeedUrl;
            gSpeed.Controls.Add(_speedUrl);
            _btnSpeedGo = Ui.Button("Начать тест", 516, 22, 120, delegate { SpeedTest(); });
            gSpeed.Controls.Add(_btnSpeedGo);
            _btnStopSpeed = Ui.Button("Стоп", 516, 22, 120, delegate { _speedStop = true; });
            _btnStopSpeed.Visible = false;
            gSpeed.Controls.Add(_btnStopSpeed);
            _speedBar = new ProgressBar();
            _speedBar.Location = new Point(10, 56);
            _speedBar.Size = new Size(626, 18);
            _speedBar.Style = ProgressBarStyle.Marquee;
            _speedBar.MarqueeAnimationSpeed = 0;
            gSpeed.Controls.Add(_speedBar);
            _speedResult = Ui.Label("Тест измеряет реальную скорость скачивания за 10 секунд.", 10, 82, 620);
            gSpeed.Controls.Add(_speedResult);
            pageNet.Controls.Add(gSpeed);

            // ---- 4. Интернет и внешний IP ----
            GroupBox gNet = Ui.Group("Интернет и внешний IP", 4, 364, 650, 112);
            gNet.Controls.Add(Ui.Button("Проверить доступ в интернет", 10, 24, 210, delegate { CheckInternet(); }));
            gNet.Controls.Add(Ui.Button("Узнать внешний IP", 226, 24, 160, delegate { ExternalIp(); }));
            gNet.Controls.Add(Ui.Button("Обратное имя внешнего IP (PTR)", 392, 24, 240, delegate { ExternalPtr(); }));
            gNet.Controls.Add(Ui.Button("Проверить IPv6", 10, 56, 160, delegate { CheckIpv6(); }));
            gNet.Controls.Add(Ui.Button("Показать сетевые подключения Windows", 176, 56, 280, delegate
            {
                try { Process.Start("ncpa.cpl"); } catch { }
            }));
            pageNet.Controls.Add(gNet);

            // ---- 5. Быстрые команды ----
            GroupBox gCmd = Ui.Group("Быстрые команды (вывод — в окне справа)", 4, 4, 650, 236);
            Add(gCmd, "netsh interface show interface", 10, 24, 205, delegate { RunToOutput("Список интерфейсов", "netsh interface show interface"); });
            Add(gCmd, "netsh interface ipv4 show config", 220, 24, 205, delegate { RunToOutput("Настройки IPv4", "netsh interface ipv4 show config"); });
            Add(gCmd, "netsh interface ipv4 show subinterfaces", 430, 24, 210, delegate { RunToOutput("MTU интерфейсов", "netsh interface ipv4 show subinterfaces"); });
            Add(gCmd, "ipconfig /all", 10, 54, 205, delegate { RunToOutput("Полная конфигурация", "ipconfig /all"); });
            Add(gCmd, "ipconfig /displaydns", 220, 54, 205, delegate { RunToOutput("Кэш DNS", "ipconfig /displaydns"); });
            Add(gCmd, "netsh wlan show interfaces", 430, 54, 210, delegate { RunToOutput("Wi-Fi интерфейсы", "netsh wlan show interfaces"); });
            Add(gCmd, "route print -4", 10, 84, 205, delegate { RunToOutput("Таблица маршрутов", "route print -4"); });
            Add(gCmd, "arp -a", 220, 84, 205, delegate { RunToOutput("Таблица ARP", "arp -a"); });
            Add(gCmd, "getmac /v /fo list", 430, 84, 210, delegate { RunToOutput("MAC-адреса", "getmac /v /fo list"); });
            Add(gCmd, "netsh winhttp show proxy", 10, 114, 205, delegate { RunToOutput("Прокси WinHTTP", "netsh winhttp show proxy"); });
            Add(gCmd, "netsh advfirewall show allprofiles", 220, 114, 205, delegate { RunToOutput("Брандмауэр", "netsh advfirewall show allprofiles"); });
            Add(gCmd, "netstat -ano (с процессами)", 430, 114, 210, delegate { NetstatProcesses(); });
            Add(gCmd, "netstat -e (статистика)", 10, 144, 205, delegate { RunToOutput("Статистика интерфейсов", "netstat -e"); });
            Add(gCmd, "nslookup", 220, 144, 205, delegate { RunToOutput("nslookup", "nslookup"); });
            Add(gCmd, "службы DHCP/DNS", 430, 144, 210, delegate
            {
                RunToOutput("Сетевые службы", "cmd /c \"sc query Dhcp & sc query Dnscache & sc query Nsi & sc query Netprofm & sc query NlaSvc\"");
            });
            Add(gCmd, "кэш ARP + маршруты", 10, 174, 205, delegate { RunToOutput("Маршруты и ARP", "cmd /c \"route print -4 & echo. & arp -a\""); });
            Add(gCmd, "настройки DNS всех интерфейсов", 220, 174, 205, delegate { RunToOutput("DNS-серверы", "netsh interface ipv4 show config"); });
            Add(gCmd, "состояние адаптеров Windows", 430, 174, 210, delegate { RunToOutput("Состояние адаптеров", "netsh interface show interface"); });
            pageCmd.Controls.Add(gCmd);

            // ---- 6. Консоль ----
            GroupBox gCon = Ui.Group("Командная строка", 4, 246, 650, 94);
            gCon.Controls.Add(Ui.Label("Команда:", 10, 26));
            _consoleCmd = Ui.Text(80, 23, 400);
            _consoleCmd.Text = "ipconfig /all";
            gCon.Controls.Add(_consoleCmd);
            gCon.Controls.Add(Ui.Button("Выполнить здесь", 490, 22, 146, delegate { RunToOutput("Команда", _consoleCmd.Text.Trim()); }));
            gCon.Controls.Add(Ui.Button("Открыть в консоли", 10, 54, 160, delegate
            {
                if (_consoleCmd.Text.Trim().Length > 0) Runner.OpenConsole(_consoleCmd.Text.Trim(), false);
            }));
            gCon.Controls.Add(Ui.Button("Консоль от администратора", 176, 54, 210, delegate
            {
                Runner.OpenConsole(_consoleCmd.Text.Trim().Length > 0 ? _consoleCmd.Text.Trim() : "echo NetHelp", true);
            }));
            gCon.Controls.Add(Ui.Button("Просто cmd", 392, 54, 110, delegate { Runner.OpenConsole("", false); }));
            pageCmd.Controls.Add(gCon);

            // ---- 7. hosts и отчёты ----
            GroupBox gFiles = Ui.Group("Файл hosts и отчёты", 4, 346, 650, 124);
            gFiles.Controls.Add(Ui.Button("Показать hosts", 10, 24, 140, delegate { ShowHosts(); }));
            gFiles.Controls.Add(Ui.Button("Изменить hosts (админ)", 156, 24, 180, delegate
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo("notepad.exe", "\"" + Report.HostsFilePath() + "\"");
                    psi.UseShellExecute = true;
                    psi.Verb = "runas";
                    Process.Start(psi);
                }
                catch (Exception ex) { Ui.Err(this, "Не удалось открыть файл hosts: " + ex.Message); }
            }));
            gFiles.Controls.Add(Ui.Button("Открыть папку hosts", 342, 24, 160, delegate
            {
                try { Process.Start("explorer.exe", "/select,\"" + Report.HostsFilePath() + "\""); }
                catch { }
            }));
            gFiles.Controls.Add(Ui.Button("Сохранить полный отчёт (TXT)", 10, 56, 220, delegate { SaveReportTo(false, false); }));
            gFiles.Controls.Add(Ui.Button("Отчёт с netstat", 236, 56, 160, delegate { SaveReportTo(true, false); }));
            gFiles.Controls.Add(Ui.Button("Копировать отчёт в буфер", 402, 56, 200, delegate { SaveReportTo(true, true); }));
            gFiles.Controls.Add(Ui.Hint("Отчёты сохраняются в папку reports рядом с программой (или в профиль пользователя, если флешка защищена от записи).",
                10, 86, 620));
            pageCmd.Controls.Add(gFiles);

            // ================= правая колонка: вывод =================
            Panel right = new Panel();
            right.Dock = DockStyle.Fill;
            right.Padding = new Padding(6, 4, 0, 0);

            // Кнопки вывода: при узком окне переносятся на вторую строку и не исчезают.
            FlowLayoutPanel rTop = new FlowLayoutPanel();
            rTop.Dock = DockStyle.Top;
            rTop.AutoSize = true;
            rTop.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            rTop.WrapContents = true;
            rTop.FlowDirection = FlowDirection.RightToLeft;
            rTop.Padding = new Padding(0, 4, 0, 2);

            Button bSave = Ui.Button("Сохранить вывод", 0, 0, 140, delegate { SaveOutput(); });
            bSave.Margin = new Padding(4, 0, 0, 4);
            rTop.Controls.Add(bSave);
            Button bClear = Ui.Button("Очистить", 0, 0, 100, delegate { _out.Clear(); });
            bClear.Margin = new Padding(4, 0, 0, 4);
            rTop.Controls.Add(bClear);
            Button bCopy = Ui.Button("Копировать", 0, 0, 110, delegate { if (_out.TextLength > 0) Clipboard.SetText(_out.Text); });
            bCopy.Margin = new Padding(4, 0, 0, 4);
            rTop.Controls.Add(bCopy);

            _out = Ui.LogBox();
            _out.Dock = DockStyle.Fill;

            right.Controls.Add(_out);
            right.Controls.Add(rTop);

            Controls.Add(right);
            Controls.Add(leftTabs);
        }

        private static void Add(GroupBox g, string text, int x, int y, int w, EventHandler h)
        {
            Button b = Ui.Button(text, x, y, w, h);
            g.Controls.Add(b);
        }

        // -------------------------------------------------------------- вывод

        private void Append(string text)
        {
            Append(text, Ui.LogFore);
        }

        private void Append(string text, Color color)
        {
            if (InvokeRequired)
            {
                try { BeginInvoke((MethodInvoker)delegate { Append(text, color); }); }
                catch { }
                return;
            }
            Ui.Log(_out, text, color);
        }

        private async void RunToOutput(string title, string command)
        {
            Append("", Ui.LogFore);
            Append("=== " + title + " ===", Ui.Accent);
            Append("> " + command, Color.FromArgb(120, 200, 255));
            _core.Log("cmd", "> " + command);
            try
            {
                string f, a;
                Runner.Split(command, out f, out a);
                CmdResult r = await Shell.RunAsync(Runner.ResolveExe(f), a, 300000);
                Append(string.IsNullOrEmpty(r.Output) ? "(нет вывода)" : r.Output, r.Ok ? Ui.LogFore : Color.FromArgb(255, 170, 170));
                _core.Log("out", r.Output);
            }
            catch (Exception ex) { Append("Ошибка: " + ex.Message, Ui.ErrColor); }
        }

        private void SaveOutput()
        {
            try
            {
                string dir = Path.Combine(AppSettings.DataDir, "reports");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "tools-" + Environment.MachineName + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
                File.WriteAllText(file, _out.Text, System.Text.Encoding.UTF8);
                Append("Сохранено: " + file, Ui.OkColor);
            }
            catch (Exception ex) { Ui.Err(this, "Не удалось сохранить: " + ex.Message); }
        }

        // -------------------------------------------------------------- порты

        private async void CheckPort()
        {
            string host = _portHost.Text.Trim();
            int port;
            if (host.Length == 0) { Ui.Warn(this, "Укажите узел (имя или IP)."); return; }
            if (!int.TryParse(_portPort.Text.Trim(), out port) || port < 1 || port > 65535) { Ui.Warn(this, "Порт должен быть числом от 1 до 65535."); return; }
            _core.Settings.PortHost = host;
            _portResult.ForeColor = Ui.MutedColor;
            _portResult.Text = "Проверяю " + host + ":" + port + " ...";
            bool ok = await TestPortAsync(host, port, (int)_portTimeout.Value);
            _portResult.ForeColor = ok ? Ui.OkColor : Ui.ErrColor;
            _portResult.Text = ok
                ? ("ОТКРЫТ: " + host + ":" + port + " — служба отвечает.")
                : ("ЗАКРЫТ или недоступен: " + host + ":" + port + " — ответа нет (порт закрыт, служба не работает или блокирует брандмауэр).");
            Append("Проверка " + host + ":" + port + " — " + (ok ? "ОТКРЫТ" : "закрыт/нет ответа"), ok ? Ui.OkColor : Ui.ErrColor);
        }

        private async void CheckCommonPorts()
        {
            string host = _portHost.Text.Trim();
            if (host.Length == 0) { Ui.Warn(this, "Укажите узел (имя или IP)."); return; }
            int[] ports = new int[] { 21, 22, 23, 25, 53, 80, 110, 135, 139, 143, 443, 445, 993, 995, 1433, 1521, 3306, 3389, 5432, 5900, 8080, 8443 };
            Append("", Ui.LogFore);
            Append("=== Проверка популярных портов на " + host + " ===", Ui.Accent);
            for (int i = 0; i < ports.Length; i++)
            {
                bool ok = await TestPortAsync(host, ports[i], (int)_portTimeout.Value);
                Append("  " + ports[i].ToString().PadRight(6) + (ok ? "открыт" : "—"), ok ? Ui.OkColor : Color.FromArgb(140, 140, 145));
            }
            Append("Готово. Проверка TCP-портов не гарантирует работу UDP-служб.", Ui.MutedColor);
        }

        public static async Task<bool> TestPortAsync(string host, int port, int timeoutMs)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    Task t = client.ConnectAsync(host, port);
                    Task done = await Task.WhenAny(t, Task.Delay(timeoutMs));
                    if (done != t) return false;
                    try { await t; }
                    catch { return false; }
                    return client.Connected;
                }
            }
            catch { return false; }
        }

        // -------------------------------------------------------------- WoL

        private void WakeOnLan()
        {
            try
            {
                byte[] mac = ParseMac(_mac.Text);
                if (mac == null) { Ui.Warn(this, "Некорректный MAC-адрес. Пример: AA:BB:CC:DD:EE:FF"); return; }
                string bcast = _broadcast.Text.Trim();
                IPAddress addr;
                if (!IPAddress.TryParse(bcast, out addr)) { Ui.Warn(this, "Некорректный широковещательный адрес."); return; }

                byte[] packet = new byte[102];
                for (int i = 0; i < 6; i++) packet[i] = 0xFF;
                for (int i = 0; i < 16; i++) Array.Copy(mac, 0, packet, 6 + i * 6, 6);

                using (UdpClient udp = new UdpClient())
                {
                    udp.EnableBroadcast = true;
                    udp.Connect(addr, 9);
                    udp.Send(packet, packet.Length);
                    udp.Send(packet, packet.Length);
                }
                Append("Пакет Wake-on-LAN отправлен на " + bcast + " для " + _mac.Text, Ui.OkColor);
                _core.Log("info", "Wake-on-LAN: " + _mac.Text + " -> " + bcast);
            }
            catch (Exception ex) { Ui.Err(this, "Не удалось отправить пакет: " + ex.Message); }
        }

        private static byte[] ParseMac(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            string hex = s.Replace(":", "").Replace("-", "").Replace(".", "").Replace(" ", "").Trim();
            if (hex.Length != 12) return null;
            byte[] b = new byte[6];
            for (int i = 0; i < 6; i++)
            {
                int v;
                if (!int.TryParse(hex.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber, null, out v)) return null;
                b[i] = (byte)v;
            }
            return b;
        }

        // -------------------------------------------------------------- скорость

        private volatile bool _speedStop = false;

        private async void SpeedTest()
        {
            string url = _speedUrl.Text.Trim();
            if (url.Length == 0) { Ui.Warn(this, "Укажите URL файла для скачивания."); return; }
            _core.Settings.SpeedUrl = url;
            _speedStop = false;
            _btnSpeedGo.Visible = false;
            _btnStopSpeed.Visible = true;
            _speedBar.MarqueeAnimationSpeed = 30;
            _speedResult.ForeColor = Ui.MutedColor;
            _speedResult.Text = "Подключение...";
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) NetHelp";
                req.Timeout = 15000;
                req.ReadWriteTimeout = 30000;
                Stopwatch firstByte = Stopwatch.StartNew();
                using (WebResponse resp = await req.GetResponseAsync())
                using (Stream s = resp.GetResponseStream())
                {
                    byte[] buf = new byte[65536];
                    long total = 0;
                    long ttfb = firstByte.ElapsedMilliseconds;
                    Stopwatch sw = Stopwatch.StartNew();
                    while (!_speedStop && sw.Elapsed.TotalSeconds < 10)
                    {
                        Task<int> read = s.ReadAsync(buf, 0, buf.Length);
                        Task done = await Task.WhenAny(read, Task.Delay(15000));
                        if (done != read) break;
                        int n = await read;
                        if (n <= 0) break;
                        total += n;
                        _speedBar.Style = ProgressBarStyle.Continuous;
                        _speedBar.Maximum = 100;
                        _speedBar.Value = Math.Min(100, (int)(sw.Elapsed.TotalSeconds / 10 * 100));
                        _speedResult.Text = "Скачано " + Mb(total) + " МБ за " + sw.Elapsed.TotalSeconds.ToString("0.0") + " с ...";
                    }
                    sw.Stop();
                    double seconds = Math.Max(0.1, sw.Elapsed.TotalSeconds);
                    double mbps = total * 8.0 / seconds / 1000000.0;
                    double mbytes = total / 1048576.0 / seconds;
                    _speedBar.Style = ProgressBarStyle.Continuous;
                    _speedBar.Value = 100;
                    _speedResult.ForeColor = Ui.OkColor;
                    _speedResult.Text = _speedStop
                        ? ("Остановлено. Успели скачать " + Mb(total) + " МБ — " + mbps.ToString("0.0") + " Мбит/с")
                        : ("Скорость: " + mbps.ToString("0.0") + " Мбит/с (" + mbytes.ToString("0.0") + " МБ/с). Скачано " + Mb(total) +
                           " МБ за " + seconds.ToString("0.0") + " с, задержка до первого байта " + ttfb + " мс.");
                    Append("Тест скорости (" + url + "): " + mbps.ToString("0.00") + " Мбит/с, TTFB " + ttfb + " мс", Ui.OkColor);
                    _core.Log("info", "Тест скорости: " + mbps.ToString("0.00") + " Мбит/с");
                }
            }
            catch (Exception ex)
            {
                _speedResult.ForeColor = Ui.ErrColor;
                _speedResult.Text = "Ошибка теста: " + ex.Message;
                Append("Ошибка теста скорости: " + ex.Message, Ui.ErrColor);
            }
            finally
            {
                _speedBar.Style = ProgressBarStyle.Marquee;
                _speedBar.MarqueeAnimationSpeed = 0;
                _btnSpeedGo.Visible = true;
                _btnStopSpeed.Visible = false;
            }
        }

        private static string Mb(long bytes)
        {
            return (bytes / 1048576.0).ToString("0.0");
        }

        // -------------------------------------------------------------- интернет

        private async void CheckInternet()
        {
            Append("", Ui.LogFore);
            Append("=== Проверка доступа в интернет ===", Ui.Accent);
            bool any = false;

            try
            {
                IPAddress[] ips = await Task.Factory.StartNew<IPAddress[]>(delegate
                {
                    try { return Dns.GetHostAddresses("www.msftconnecttest.com"); }
                    catch { return new IPAddress[0]; }
                });
                if (ips.Length > 0)
                {
                    Append("  DNS-разрешение имён: работает (" + ips[0] + ")", Ui.OkColor);
                    any = true;
                }
                else Append("  DNS-разрешение имён: НЕ работает", Ui.ErrColor);
            }
            catch (Exception ex) { Append("  DNS: ошибка " + ex.Message, Ui.ErrColor); }

            using (Ping ping = new Ping())
            {
                string[] targets = new string[] { "8.8.8.8", "1.1.1.1" };
                for (int i = 0; i < targets.Length; i++)
                {
                    try
                    {
                        PingReply r = await ping.SendPingAsync(targets[i], 2000);
                        if (r.Status == IPStatus.Success) { Append("  ICMP " + targets[i] + ": ответ " + r.RoundtripTime + " мс", Ui.OkColor); any = true; }
                        else Append("  ICMP " + targets[i] + ": " + TabPing.StatusText(r.Status), Ui.WarnColor);
                    }
                    catch (Exception ex) { Append("  ICMP " + targets[i] + ": " + ex.Message, Ui.WarnColor); }
                }
            }

            bool tcp443 = await TestPortAsync("1.1.1.1", 443, 3000);
            Append("  TCP 1.1.1.1:443 — " + (tcp443 ? "доступен" : "недоступен"), tcp443 ? Ui.OkColor : Ui.WarnColor);
            if (tcp443) any = true;

            HttpResult http = await HttpGet("http://www.msftconnecttest.com/connecttest.txt");
            if (http.Body != null)
            {
                bool ok = http.Body.IndexOf("Microsoft Connect Test", StringComparison.OrdinalIgnoreCase) >= 0;
                Append("  HTTP-проверка: " + (ok ? "интернет работает (ответ сервера Microsoft получен)" : "получен неожиданный ответ: " + Short(http.Body)),
                    ok ? Ui.OkColor : Ui.WarnColor);
                if (ok) any = true;
            }
            else
            {
                Append("  HTTP-проверка: ошибка — " + Short(http.Error), Ui.WarnColor);
                HttpResult fb = await HttpGet("http://captive.apple.com/hotspot-detect.html");
                if (fb.Body != null)
                {
                    Append("  Резервная HTTP-проверка: ответ получен (" + fb.Body.Length +
                        " байт) — похоже, доступ есть, но требуется вход в сеть (captive portal).", Ui.WarnColor);
                    any = true;
                }
            }

            Append(any ? "ИТОГ: сеть доступна." : "ИТОГ: доступа в интернет нет — начните со вкладки «Сброс сети».", any ? Ui.OkColor : Ui.ErrColor);
        }

        private class HttpResult
        {
            public string Body;
            public string Error = "";
        }

        private static async Task<HttpResult> HttpGet(string url)
        {
            HttpResult r = new HttpResult();
            try
            {
                using (WebClient wc = new WebClient())
                {
                    wc.Headers["User-Agent"] = "NetHelp";
                    r.Body = await wc.DownloadStringTaskAsync(url);
                }
            }
            catch (Exception ex)
            {
                r.Error = ex.Message;
            }
            return r;
        }

        private async void ExternalIp()
        {
            Append("", Ui.LogFore);
            Append("=== Внешний IP-адрес ===", Ui.Accent);
            string[] urls = new string[] { "https://api.ipify.org", "https://ifconfig.me/ip", "https://icanhazip.com" };
            for (int i = 0; i < urls.Length; i++)
            {
                try
                {
                    using (WebClient wc = new WebClient())
                    {
                        wc.Headers["User-Agent"] = "NetHelp";
                        wc.Encoding = System.Text.Encoding.UTF8;
                        string s = await wc.DownloadStringTaskAsync(urls[i]);
                        s = s.Trim();
                        if (s.Length > 0 && s.Length < 80)
                        {
                            Append("  Внешний IP: " + s + "   (источник: " + urls[i] + ")", Ui.OkColor);
                            _core.Log("info", "Внешний IP: " + s);
                            return;
                        }
                    }
                }
                catch (Exception ex) { Append("  " + urls[i] + " — " + Short(ex.Message), Ui.MutedColor); }
            }
            Append("  Не удалось определить внешний IP (нет доступа или сервис недоступен).", Ui.ErrColor);
        }

        private async void ExternalPtr()
        {
            Append("", Ui.LogFore);
            Append("=== Обратное имя внешнего IP ===", Ui.Accent);
            string ip = "";
            try
            {
                using (WebClient wc = new WebClient())
                {
                    wc.Headers["User-Agent"] = "NetHelp";
                    ip = (await wc.DownloadStringTaskAsync("https://api.ipify.org")).Trim();
                }
            }
            catch { }
            if (ip.Length == 0) { Append("  Не удалось определить внешний IP.", Ui.ErrColor); return; }
            Append("  Внешний IP: " + ip);
            string name = await TabTrace_Resolve(ip);
            Append("  Обратное имя (PTR): " + (name.Length > 0 ? name : "(не найдено)"), name.Length > 0 ? Ui.OkColor : Ui.WarnColor);
            Append("  Примечание: PTR обычно скрывает провайдер, это нормально.", Ui.MutedColor);
        }

        private static async Task<string> TabTrace_Resolve(string ip)
        {
            try
            {
                Task<string> t = Task.Factory.StartNew<string>(delegate
                {
                    try { return Dns.GetHostEntry(ip).HostName; }
                    catch { return ""; }
                });
                Task done = await Task.WhenAny(t, Task.Delay(3000));
                if (done == t) return t.Result == null ? "" : t.Result;
                return "";
            }
            catch { return ""; }
        }

        private async void CheckIpv6()
        {
            Append("", Ui.LogFore);
            Append("=== Проверка IPv6 ===", Ui.Accent);
            try
            {
                bool has6 = false;
                NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
                for (int i = 0; i < nics.Length; i++)
                {
                    if (nics[i].OperationalStatus != OperationalStatus.Up) continue;
                    IPInterfaceProperties p = nics[i].GetIPProperties();
                    foreach (UnicastIPAddressInformation u in p.UnicastAddresses)
                    {
                        if (u.Address.AddressFamily != AddressFamily.InterNetworkV6) continue;
                        if (u.Address.IsIPv6LinkLocal || u.Address.IsIPv6SiteLocal) continue;
                        Append("  " + nics[i].Name + ": " + u.Address, Ui.OkColor);
                        has6 = true;
                    }
                }
                if (!has6) Append("  Глобальных адресов IPv6 нет — провайдер или сеть не используют IPv6.", Ui.WarnColor);
            }
            catch (Exception ex) { Append("  Ошибка: " + ex.Message, Ui.ErrColor); }

            try
            {
                Task<IPAddress[]> t = Task.Factory.StartNew<IPAddress[]>(delegate
                {
                    try { return Dns.GetHostAddresses("ipv6.google.com"); }
                    catch { return new IPAddress[0]; }
                });
                Task done = await Task.WhenAny(t, Task.Delay(5000));
                if (done == t && t.Result != null)
                {
                    bool v6 = false;
                    for (int i = 0; i < t.Result.Length; i++)
                        if (t.Result[i].AddressFamily == AddressFamily.InterNetworkV6 && !t.Result[i].IsIPv6LinkLocal) v6 = true;
                    Append("  Разрешение имён AAAA: " + (v6 ? "есть адреса IPv6" : "адресов IPv6 нет"), v6 ? Ui.OkColor : Ui.WarnColor);
                }
                else Append("  Разрешение имён AAAA: нет ответа (возможно, DNS не поддерживает IPv6)", Ui.WarnColor);
            }
            catch (Exception ex) { Append("  Ошибка: " + ex.Message, Ui.WarnColor); }

            Append("  Если IPv6 включён, но интернета нет — отключите IPv6 на адаптере (вкладка «IP / DNS») или выполните сброс IPv6.", Ui.MutedColor);
        }

        private static string Short(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length > 160 ? s.Substring(0, 160) + "…" : s;
        }

        // -------------------------------------------------------------- netstat

        private async void NetstatProcesses()
        {
            Append("", Ui.LogFore);
            Append("=== Сетевые соединения (netstat -ano с именами процессов) ===", Ui.Accent);
            try
            {
                CmdResult r = await Shell.RunAsync(Runner.ResolveExe("netstat.exe"), "-ano", 120000);
                string[] lines = r.Output.Split('\n');
                Dictionary<int, string> names = new Dictionary<int, string>();
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].TrimEnd('\r');
                    Match m = Regex.Match(line, @"^\s*(TCP|UDP)\s+(\S+)\s+(\S+)\s+(\S*)\s*(\d+)\s*$");
                    if (!m.Success)
                    {
                        sb.AppendLine(line);
                        continue;
                    }
                    int pid = 0;
                    int.TryParse(m.Groups[5].Value, out pid);
                    string pname = ProcessName(pid, names);
                    sb.AppendLine(m.Groups[1].Value.PadRight(6) + m.Groups[2].Value.PadRight(28) + m.Groups[3].Value.PadRight(28) +
                                  m.Groups[4].Value.PadRight(14) + m.Groups[5].Value.PadRight(8) + pname);
                }
                Append(sb.ToString(), Ui.LogFore);
                _core.Log("out", "netstat -ano с процессами: " + lines.Length + " строк");
            }
            catch (Exception ex) { Append("Ошибка: " + ex.Message, Ui.ErrColor); }
        }

        private static string ProcessName(int pid, Dictionary<int, string> cache)
        {
            if (pid == 0) return "System Idle";
            if (pid == 4) return "System";
            string v;
            if (cache.TryGetValue(pid, out v)) return v;
            try
            {
                Process p = Process.GetProcessById(pid);
                v = p.ProcessName + " (" + pid + ")";
            }
            catch { v = "(процесс " + pid + " не найден)"; }
            cache[pid] = v;
            return v;
        }

        // -------------------------------------------------------------- hosts и отчёты

        private void ShowHosts()
        {
            Append("", Ui.LogFore);
            Append("=== Файл hosts: " + Report.HostsFilePath() + " ===", Ui.Accent);
            try
            {
                string[] lines = File.ReadAllLines(Report.HostsFilePath());
                for (int i = 0; i < lines.Length; i++)
                {
                    string l = lines[i];
                    Append(l, l.TrimStart().StartsWith("#") || l.Trim().Length == 0 ? Ui.MutedColor : Ui.LogFore);
                }
                if (lines.Length == 0) Append("(файл пуст)");
            }
            catch (Exception ex) { Append("Не удалось прочитать файл hosts: " + ex.Message, Ui.ErrColor); }
        }

        private async void SaveReportTo(bool withNetstat, bool toClipboard)
        {
            await SaveReport(withNetstat, toClipboard);
        }

        private async Task SaveReport(bool withNetstat, bool toClipboard)
        {
            Append("", Ui.LogFore);
            Append("=== Формирование отчёта" + (withNetstat ? " (с netstat)" : "") + " ===", Ui.Accent);
            Append("Собираю сведения: адаптеры и системные команды. Это займёт несколько секунд...", Ui.WarnColor);
            try
            {
                Core core = _core;
                bool conn = withNetstat;
                string text = await Task.Factory.StartNew<string>(delegate { return Report.BuildFull(core, conn); });
                if (toClipboard)
                {
                    Clipboard.SetText(text);
                    Append("Отчёт (" + text.Length + " символов) скопирован в буфер обмена.", Ui.OkColor);
                    return;
                }
                string dir = Path.Combine(AppSettings.DataDir, "reports");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "nethelp-" + Environment.MachineName + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
                File.WriteAllText(file, text, System.Text.Encoding.UTF8);
                Append("Отчёт сохранён: " + file + "  (" + text.Length + " символов)", Ui.OkColor);
                _core.Log("ok", "Отчёт сохранён: " + file);
                if (Ui.Ask(this, "Отчёт сохранён:" + Environment.NewLine + file + Environment.NewLine + Environment.NewLine + "Открыть файл?"))
                    Process.Start("notepad.exe", file);
            }
            catch (Exception ex) { Append("Ошибка формирования отчёта: " + ex.Message, Ui.ErrColor); }
        }
    }
}
