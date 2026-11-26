using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;

namespace NetHelp
{
    /// <summary>Изменение настроек IP, DNS, MTU, метрики и IPv6 выбранного адаптера.</summary>
    public class TabIpDns : UserControl
    {
        private Core _core;
        private ComboBox _combo;
        private RadioButton _rbDhcp;
        private RadioButton _rbStatic;
        private TextBox _ip;
        private TextBox _mask;
        private TextBox _gw;
        private RadioButton _rbDnsDhcp;
        private RadioButton _rbDnsStatic;
        private TextBox _dns1;
        private TextBox _dns2;
        private TextBox _dns3;
        private TextBox _mtu;
        private TextBox _metric;
        private CheckBox _cbIpv6;
        private TextBox _dnsHost;
        private TextBox _dnsServer;
        private RichTextBox _out;
        private Label _lblInfo;
        private bool _loading;

        public TabIpDns(Core core)
        {
            _core = core;
            Dock = DockStyle.Fill;
            BackColor = Ui.Bg;

            // ---------------- левая колонка ----------------
            Panel left = new Panel();
            left.Dock = DockStyle.Left;
            left.Width = 620;
            left.AutoScroll = true;
            left.BackColor = Ui.Bg;

            left.Controls.Add(Ui.Label("Адаптер:", 6, 12));
            _combo = new ComboBox();
            _combo.Location = new Point(64, 8);
            _combo.Width = 430;
            _combo.DropDownStyle = ComboBoxStyle.DropDownList;
            _combo.Font = Ui.BaseFont;
            _combo.SelectedIndexChanged += delegate { LoadFromAdapter(); };
            left.Controls.Add(_combo);
            left.Controls.Add(Ui.Button("Сбросить", 500, 7, 90, delegate { LoadFromAdapter(); }));

            GroupBox gAddr = Ui.Group("Адрес IPv4", 6, 42, 584, 132);
            _rbDhcp = new RadioButton();
            _rbDhcp.Text = "Автоматически (DHCP) — адрес, маска и шлюз выдаются сервером";
            _rbDhcp.Location = new Point(12, 22);
            _rbDhcp.AutoSize = true;
            _rbDhcp.Font = Ui.BaseFont;
            _rbDhcp.CheckedChanged += delegate { UpdateEnabled(); };
            gAddr.Controls.Add(_rbDhcp);

            _rbStatic = new RadioButton();
            _rbStatic.Text = "Вручную (статический адрес)";
            _rbStatic.Location = new Point(12, 46);
            _rbStatic.AutoSize = true;
            _rbStatic.Font = Ui.BaseFont;
            _rbStatic.CheckedChanged += delegate { UpdateEnabled(); };
            gAddr.Controls.Add(_rbStatic);

            gAddr.Controls.Add(Ui.Label("IP-адрес:", 12, 80));
            _ip = Ui.Text(90, 77, 130);
            gAddr.Controls.Add(_ip);
            gAddr.Controls.Add(Ui.Label("Маска:", 232, 80));
            _mask = Ui.Text(285, 77, 120);
            _mask.Text = "255.255.255.0";
            gAddr.Controls.Add(_mask);
            gAddr.Controls.Add(Ui.Label("Шлюз:", 12, 106));
            _gw = Ui.Text(90, 103, 130);
            gAddr.Controls.Add(_gw);
            gAddr.Controls.Add(Ui.Button("Применить адрес", 420, 76, 155, delegate { ApplyAddress(); }));
            gAddr.Controls.Add(Ui.Button("Настройки IPv4 (netsh)", 420, 103, 155, delegate { ShowConfig(); }));
            left.Controls.Add(gAddr);

            GroupBox gDns = Ui.Group("DNS-серверы", 6, 180, 584, 112);
            _rbDnsDhcp = new RadioButton();
            _rbDnsDhcp.Text = "Автоматически (адреса DNS от DHCP-сервера)";
            _rbDnsDhcp.Location = new Point(12, 22);
            _rbDnsDhcp.AutoSize = true;
            _rbDnsDhcp.Font = Ui.BaseFont;
            _rbDnsDhcp.CheckedChanged += delegate { UpdateEnabled(); };
            gDns.Controls.Add(_rbDnsDhcp);

            _rbDnsStatic = new RadioButton();
            _rbDnsStatic.Text = "Вручную:";
            _rbDnsStatic.Location = new Point(12, 46);
            _rbDnsStatic.AutoSize = true;
            _rbDnsStatic.Font = Ui.BaseFont;
            _rbDnsStatic.CheckedChanged += delegate { UpdateEnabled(); };
            gDns.Controls.Add(_rbDnsStatic);

            gDns.Controls.Add(Ui.Label("DNS 1:", 100, 50));
            _dns1 = Ui.Text(150, 47, 110);
            gDns.Controls.Add(_dns1);
            gDns.Controls.Add(Ui.Label("DNS 2:", 268, 50));
            _dns2 = Ui.Text(318, 47, 110);
            gDns.Controls.Add(_dns2);
            gDns.Controls.Add(Ui.Label("DNS 3:", 436, 50));
            _dns3 = Ui.Text(486, 47, 90);
            gDns.Controls.Add(_dns3);
            gDns.Controls.Add(Ui.Button("Применить DNS", 12, 76, 155, delegate { ApplyDns(); }));
            gDns.Controls.Add(Ui.Button("DNS: Google", 175, 76, 125, delegate { SetDnsPreset("8.8.8.8", "8.8.4.4", ""); }));
            gDns.Controls.Add(Ui.Button("DNS: Cloudflare", 308, 76, 130, delegate { SetDnsPreset("1.1.1.1", "1.0.0.1", ""); }));
            gDns.Controls.Add(Ui.Button("DNS: Yandex", 446, 76, 130, delegate { SetDnsPreset("77.88.8.8", "77.88.8.1", ""); }));
            left.Controls.Add(gDns);

            GroupBox gMore = Ui.Group("Дополнительные параметры", 6, 298, 584, 128);
            gMore.Controls.Add(Ui.Label("MTU:", 12, 30));
            _mtu = Ui.Text(70, 27, 70);
            gMore.Controls.Add(_mtu);
            gMore.Controls.Add(Ui.Button("Применить MTU", 150, 26, 130, delegate { ApplyMtu(); }));
            gMore.Controls.Add(Ui.Button("Сбросить MTU (1500)", 286, 26, 150, delegate { _mtu.Text = "1500"; ApplyMtu(); }));
            gMore.Controls.Add(Ui.Label("Метрика:", 12, 62));
            _metric = Ui.Text(70, 59, 70);
            gMore.Controls.Add(_metric);
            gMore.Controls.Add(Ui.Button("Применить метрику", 150, 58, 130, delegate { ApplyMetric(); }));
            gMore.Controls.Add(Ui.Button("Автометрика", 286, 58, 110, delegate { SetAutoMetric(); }));
            _cbIpv6 = Ui.Check("IPv6 включён на этом адаптере", 12, 92, true, null);
            gMore.Controls.Add(_cbIpv6);
            gMore.Controls.Add(Ui.Button("Применить IPv6", 286, 88, 110, delegate { ApplyIpv6(); }));
            gMore.Controls.Add(Ui.Button("Перезапустить адаптер", 402, 88, 175, delegate { RestartAdapter(); }));
            left.Controls.Add(gMore);

            GroupBox gTest = Ui.Group("Проверка DNS", 6, 432, 584, 124);
            gTest.Controls.Add(Ui.Label("Имя для проверки:", 12, 30));
            _dnsHost = Ui.Text(140, 27, 140);
            _dnsHost.Text = core.Settings.DnsTestHost;
            gTest.Controls.Add(_dnsHost);
            gTest.Controls.Add(Ui.Label("DNS-сервер:", 292, 30));
            _dnsServer = Ui.Text(380, 27, 120);
            gTest.Controls.Add(_dnsServer);
            gTest.Controls.Add(Ui.Button("Проверить (nslookup)", 12, 58, 160, delegate { TestDnsOne(); }));
            gTest.Controls.Add(Ui.Button("Проверить все DNS адаптера", 178, 58, 190, delegate { TestDnsAll(); }));
            gTest.Controls.Add(Ui.Button("Сравнить с 8.8.8.8 и 1.1.1.1", 374, 58, 205, delegate
            {
                _dnsServer.Text = "8.8.8.8";
                TestDnsOne();
                _dnsServer.Text = "1.1.1.1";
                TestDnsOne();
            }));
            gTest.Controls.Add(Ui.Hint("Проверка покажет, отвечает ли DNS-сервер и за сколько миллисекунд.", 12, 88, 560));
            left.Controls.Add(gTest);

            // ---------------- правая колонка: вывод ----------------
            Panel right = new Panel();
            right.Dock = DockStyle.Fill;
            right.Padding = new Padding(6, 0, 0, 0);

            Panel rTop = new Panel();
            rTop.Dock = DockStyle.Top;
            rTop.Height = 38;
            _lblInfo = Ui.Label("", 4, 10);
            _lblInfo.Font = Ui.BoldFont;
            rTop.Controls.Add(_lblInfo);

            Button bcl = Ui.Button("Очистить", 0, 6, 90, delegate { _out.Clear(); });
            bcl.Dock = DockStyle.Right;
            bcl.Width = 90;
            rTop.Controls.Add(bcl);
            Button bc = Ui.Button("Копировать", 0, 6, 110, delegate
            {
                if (_out.TextLength > 0) Clipboard.SetText(_out.Text);
            });
            bc.Dock = DockStyle.Right;
            bc.Width = 110;
            rTop.Controls.Add(bc);

            _out = Ui.LogBox();
            _out.Dock = DockStyle.Fill;

            right.Controls.Add(_out);
            right.Controls.Add(rTop);

            Controls.Add(right);
            Controls.Add(left);

            core.InventoryChanged += delegate { ReloadCombo(); };
            core.SelectionChanged += delegate { SelectCurrent(); };
        }

        // ---------------------------------------------------------------- данные

        private void ReloadCombo()
        {
            _loading = true;
            _combo.Items.Clear();
            for (int i = 0; i < _core.Adapters.Count; i++)
            {
                AdapterInfo a = _core.Adapters[i];
                if (a.ConnectionName.Length == 0 && !a.Present) continue;
                _combo.Items.Add(new ComboItem(a));
            }
            _loading = false;
            SelectCurrent();
        }

        private void SelectCurrent()
        {
            if (_core.Selected == null) return;
            for (int i = 0; i < _combo.Items.Count; i++)
            {
                ComboItem ci = _combo.Items[i] as ComboItem;
                if (ci == null) continue;
                if (string.Equals(ci.Adapter.Guid, _core.Selected.Guid, StringComparison.OrdinalIgnoreCase) && ci.Adapter.Guid.Length > 0)
                {
                    if (_combo.SelectedIndex != i) _combo.SelectedIndex = i;
                    else LoadFromAdapter();
                    return;
                }
            }
        }

        private AdapterInfo Current()
        {
            ComboItem ci = _combo.SelectedItem as ComboItem;
            if (ci == null) return null;
            return ci.Adapter;
        }

        private class ComboItem
        {
            public AdapterInfo Adapter;
            public ComboItem(AdapterInfo a) { Adapter = a; }
            public override string ToString()
            {
                string s = Adapter.Title;
                if (!Adapter.Present) s += "  [скрытый — нет устройства]";
                else if (!Adapter.Manageable) s += "  [нет имени подключения]";
                return s;
            }
        }

        private void LoadFromAdapter()
        {
            if (_loading) return;
            AdapterInfo a = Current();
            _out.Clear();
            if (a == null)
            {
                _lblInfo.Text = "Адаптер не выбран";
                return;
            }
            _loading = true;
            bool staticAddr = a.DhcpKnown && !a.DhcpEnabled;
            _rbDhcp.Checked = !staticAddr;
            _rbStatic.Checked = staticAddr;
            _ip.Text = a.Ipv4.Count > 0 ? a.Ipv4[0] : "";
            _mask.Text = a.Masks.Count > 0 ? a.Masks[0] : "255.255.255.0";
            _gw.Text = a.Gateways.Count > 0 ? a.Gateways[0] : "";

            bool dnsAuto = a.DnsSource.IndexOf("DHCP", StringComparison.OrdinalIgnoreCase) >= 0;
            List<string> v4dns = new List<string>();
            for (int i = 0; i < a.Dns.Count; i++) if (a.Dns[i].IndexOf(':') < 0) v4dns.Add(a.Dns[i]);
            if (v4dns.Count == 0) dnsAuto = true;
            _rbDnsDhcp.Checked = dnsAuto;
            _rbDnsStatic.Checked = !dnsAuto;
            _dns1.Text = v4dns.Count > 0 ? v4dns[0] : "";
            _dns2.Text = v4dns.Count > 1 ? v4dns[1] : "";
            _dns3.Text = v4dns.Count > 2 ? v4dns[2] : "";
            _mtu.Text = a.Mtu.Length > 0 ? a.Mtu : "";
            _metric.Text = a.Metric.Length > 0 ? a.Metric : "";
            _cbIpv6.Checked = true;
            _loading = false;

            _lblInfo.Text = a.Title + (a.Present ? "" : "   (адаптер отсутствует — доступны только команды реестра)");
            Ui.Log(_out, "Адаптер: " + a.Title, Ui.Accent);
            Ui.Log(_out, "Описание: " + a.Description);
            Ui.Log(_out, "Состояние: " + a.StatusText + (a.StatusDetail.Length > 0 ? " — " + a.StatusDetail : ""));
            Ui.Log(_out, "Текущий IPv4: " + a.Ipv4Text + "   маска: " + (a.Masks.Count > 0 ? string.Join(", ", a.Masks.ToArray()) : "—"));
            Ui.Log(_out, "Шлюз: " + a.GatewayText);
            Ui.Log(_out, "DNS: " + a.DnsText + (a.DnsSource.Length > 0 ? "  [" + a.DnsSource + "]" : ""));
            Ui.Log(_out, "Режим адреса: " + a.DhcpText);
            Ui.Log(_out, "MTU: " + (a.Mtu.Length > 0 ? a.Mtu : "—") + "   метрика: " + (a.Metric.Length > 0 ? a.Metric : "автоматически"));
            Ui.Log(_out, "IPv6: " + a.Ipv6Text);
            if (a.RegClassPath.Length > 0) Ui.Log(_out, "Реестр: " + a.RegClassPath, Ui.MutedColor);

            if (!a.Manageable)
            {
                Ui.Log(_out, "");
                Ui.Log(_out, "ВНИМАНИЕ: изменить настройки этого адаптера командами нельзя " +
                    "(устройство отсутствует или неизвестно имя подключения). Запись существует только в реестре.", Ui.WarnColor);
            }
            UpdateEnabled();
        }

        private void UpdateEnabled()
        {
            bool manual = _rbStatic.Checked;
            _ip.Enabled = manual; _mask.Enabled = manual; _gw.Enabled = manual;
            bool dnsManual = _rbDnsStatic.Checked;
            _dns1.Enabled = dnsManual; _dns2.Enabled = dnsManual; _dns3.Enabled = dnsManual;
        }

        // ---------------------------------------------------------------- команды

        private async void Run(string title, string[] commands, bool danger)
        {
            AdapterInfo a = Current();
            if (a == null) { Ui.Warn(this, "Выберите адаптер."); return; }
            if (!a.Manageable)
            {
                Ui.Warn(this, "Адаптер недоступен для изменения: устройство отсутствует или неизвестно имя подключения." +
                    Environment.NewLine + Environment.NewLine + "Используйте «Сброс сети» или работу с реестром.");
                return;
            }
            RunOptions o = new RunOptions();
            o.Title = title + " — " + a.Title;
            o.Danger = danger;
            await Runner.Run(this, _core, commands, o);
        }

        private void ApplyAddress()
        {
            AdapterInfo a = Current();
            if (a == null) return;
            if (_rbDhcp.Checked)
            {
                Run("Адрес автоматически (DHCP)", Cmds.SetDhcpAddress(a.ConnectionName), false);
                return;
            }
            if (!ValidIpv4(_ip.Text)) { Ui.Warn(this, "Введите корректный IP-адрес, например 192.168.1.50"); return; }
            if (!ValidMask(_mask.Text)) { Ui.Warn(this, "Введите непрерывную маску IPv4, например 255.255.255.0"); return; }
            if (_gw.Text.Trim().Length > 0 && !ValidIpv4(_gw.Text)) { Ui.Warn(this, "Введите корректный шлюз или оставьте поле пустым."); return; }
            Run("Статический IP-адрес", Cmds.SetStaticAddress(a.ConnectionName, _ip.Text.Trim(), _mask.Text.Trim(), _gw.Text.Trim()), false);
        }

        private void ApplyDns()
        {
            AdapterInfo a = Current();
            if (a == null) return;
            if (_rbDnsDhcp.Checked)
            {
                Run("DNS автоматически (от DHCP)", Cmds.SetDhcpDns(a.ConnectionName), false);
                return;
            }
            List<string> servers = new List<string>();
            string[] raw = new string[] { _dns1.Text, _dns2.Text, _dns3.Text };
            for (int i = 0; i < raw.Length; i++)
            {
                string s = raw[i].Trim();
                if (s.Length == 0) continue;
                if (!ValidIpv4(s)) { Ui.Warn(this, "Некорректный IPv4-адрес DNS-сервера: " + s); return; }
                servers.Add(s);
            }
            if (servers.Count == 0) { Ui.Warn(this, "Укажите хотя бы один DNS-сервер."); return; }
            _core.Settings.DnsTestHost = _dnsHost.Text.Trim().Length > 0 ? _dnsHost.Text.Trim() : _core.Settings.DnsTestHost;
            Run("DNS вручную", Cmds.SetStaticDns(a.ConnectionName, servers.ToArray()), false);
        }

        private void SetDnsPreset(string d1, string d2, string d3)
        {
            _rbDnsStatic.Checked = true;
            _dns1.Text = d1;
            _dns2.Text = d2;
            _dns3.Text = d3;
            Ui.Log(_out, "Выбраны DNS: " + d1 + (d2.Length > 0 ? ", " + d2 : ""), Ui.Accent);
            ApplyDns();
        }

        private void ApplyMtu()
        {
            AdapterInfo a = Current();
            if (a == null) return;
            int mtu;
            if (!int.TryParse(_mtu.Text.Trim(), out mtu) || mtu < 576 || mtu > 9000)
            {
                Ui.Warn(this, "MTU должен быть числом от 576 до 9000 (обычно 1500, для PPPoE 1472, для VPN 1400).");
                return;
            }
            Run("MTU = " + mtu, Cmds.SetMtu(a.ConnectionName, mtu), false);
        }

        private void ApplyMetric()
        {
            AdapterInfo a = Current();
            if (a == null) return;
            int m;
            if (!int.TryParse(_metric.Text.Trim(), out m) || m < 1 || m > 9999)
            {
                Ui.Warn(this, "Метрика должна быть числом от 1 до 9999. Меньше значение — выше приоритет адаптера.");
                return;
            }
            Run("Метрика = " + m, Cmds.SetMetric(a.ConnectionName, m), false);
        }

        private void SetAutoMetric()
        {
            AdapterInfo a = Current();
            if (a == null) return;
            Run("Автоматическая метрика", new string[]
            {
                "netsh interface ipv4 set interface \"" + a.ConnectionName + "\" metric=automatic",
                "netsh interface ipv6 set interface \"" + a.ConnectionName + "\" metric=automatic"
            }, false);
        }

        private void ApplyIpv6()
        {
            AdapterInfo a = Current();
            if (a == null) return;
            Run(_cbIpv6.Checked ? "IPv6 включён" : "IPv6 отключён", Cmds.SetIpv6(a.ConnectionName, _cbIpv6.Checked), false);
        }

        private void RestartAdapter()
        {
            AdapterInfo a = Current();
            if (a == null) return;
            if (!Ui.Ask(this, "Перезапустить адаптер «" + a.Title + "»?" + Environment.NewLine +
                "Соединение прервётся на несколько секунд.")) return;
            Run("Перезапуск адаптера", Cmds.Restart(a.ConnectionName), false);
        }

        private async void ShowConfig()
        {
            AdapterInfo a = Current();
            if (a == null || !a.Manageable) { Ui.Warn(this, "Недоступно для этого адаптера."); return; }
            RunOptions o = new RunOptions();
            o.Title = "Настройки IPv4";
            o.Confirm = false;
            o.NeedsAdmin = false;
            o.RefreshAfter = false;
            await Runner.Run(this, _core, new string[] { Cmds.ShowConfig(a.ConnectionName), Cmds.ShowDns(a.ConnectionName) }, o);
        }

        // ---------------------------------------------------------------- DNS-тесты

        private void TestDnsOne()
        {
            string host = _dnsHost.Text.Trim();
            string server = _dnsServer.Text.Trim();
            if (host.Length == 0) { Ui.Warn(this, "Укажите имя для проверки, например ya.ru"); return; }
            if (server.Length > 0 && !ValidIp(server)) { Ui.Warn(this, "Некорректный адрес DNS-сервера."); return; }

            string cmd = "nslookup " + host + (server.Length > 0 ? " " + server : "");
            Ui.Log(_out, "");
            Ui.Log(_out, "> " + cmd, Ui.Accent);
            Stopwatch sw = Stopwatch.StartNew();
            CmdResult r = Runner.RunLine(cmd, 20000);
            sw.Stop();
            Ui.Log(_out, r.Output.Length > 0 ? r.Output : "(нет вывода)", r.Ok ? Color.FromArgb(200, 230, 200) : Color.FromArgb(255, 160, 160));
            bool failed = r.Output.IndexOf("can't find", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          r.Output.IndexOf("не удается найти", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          r.Output.IndexOf("Request to", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          r.Output.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0;
            if (failed) Ui.Log(_out, "РЕЗУЛЬТАТ: " + (server.Length > 0 ? server : "DNS по умолчанию") + " — ОШИБКА  (" + sw.ElapsedMilliseconds + " мс)", Ui.ErrColor);
            else Ui.Log(_out, "РЕЗУЛЬТАТ: " + (server.Length > 0 ? server : "DNS по умолчанию") + " — OK  (" + sw.ElapsedMilliseconds + " мс)", Ui.OkColor);
        }

        private void TestDnsAll()
        {
            AdapterInfo a = Current();
            if (a == null) { Ui.Warn(this, "Выберите адаптер."); return; }
            List<string> servers = new List<string>();
            for (int i = 0; i < a.Dns.Count; i++) if (a.Dns[i].IndexOf(':') < 0) servers.Add(a.Dns[i]);
            if (servers.Count == 0) { Ui.Warn(this, "У адаптера не определены IPv4 DNS-серверы."); return; }
            string host = _dnsHost.Text.Trim().Length > 0 ? _dnsHost.Text.Trim() : "ya.ru";
            Ui.Log(_out, "");
            Ui.Log(_out, "=== Проверка DNS-серверов адаптера «" + a.Title + "» для имени " + host + " ===", Ui.Accent);
            for (int i = 0; i < servers.Count; i++)
            {
                Stopwatch sw = Stopwatch.StartNew();
                CmdResult r = Runner.RunLine("nslookup " + host + " " + servers[i], 15000);
                sw.Stop();
                bool failed = r.Output.IndexOf("can't find", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              r.Output.IndexOf("не удается найти", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              r.Output.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0;
                Ui.Log(_out, (failed ? "ОШИБКА " : "OK     ") + servers[i].PadRight(18) + sw.ElapsedMilliseconds + " мс",
                    failed ? Ui.ErrColor : Ui.OkColor);
            }
        }

        private static bool ValidIpv4(string value)
        {
            IPAddress address;
            return TryParseIpv4(value, out address);
        }

        private static bool ValidMask(string value)
        {
            IPAddress mask;
            if (!TryParseIpv4(value, out mask)) return false;

            byte[] bytes = mask.GetAddressBytes();
            uint bits = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
            ulong hostBits = ~bits;
            return (hostBits & (hostBits + 1)) == 0;
        }

        private static bool TryParseIpv4(string value, out IPAddress address)
        {
            value = value.Trim();
            return IPAddress.TryParse(value, out address)
                && address.AddressFamily == AddressFamily.InterNetwork
                && address.ToString() == value;
        }

        private static bool ValidIp(string s)
        {
            IPAddress ip;
            return IPAddress.TryParse(s.Trim(), out ip);
        }
    }
}
