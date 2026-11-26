using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NetHelp
{
    /// <summary>Формирование текстовых отчётов о состоянии сети.</summary>
    public static class Report
    {
        public static string Header()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("==============================================================");
            sb.AppendLine("  NetHelp — отчёт о состоянии сети");
            sb.AppendLine("==============================================================");
            sb.AppendLine("Компьютер       : " + Safe(delegate { return Environment.MachineName; }));
            sb.AppendLine("Пользователь    : " + Safe(delegate { return Environment.UserDomainName + "\\" + Environment.UserName; }));
            sb.AppendLine("Дата и время    : " + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"));
            sb.AppendLine("ОС              : " + Safe(delegate { return Environment.OSVersion.VersionString + " (" + (Environment.Is64BitOperatingSystem ? "64-бит" : "32-бит") + ")"; }));
            sb.AppendLine("Права           : " + (Shell.IsAdmin ? "администратор" : "обычный пользователь"));
            sb.AppendLine("WMI             : " + (Wmi.Available ? "доступен" : "НЕ доступен (часть сведений отсутствует)"));
            sb.AppendLine("Режим           : " + (AppSettings.IsPortable ? "портативный (флешка)" : "настройки в профиле пользователя"));
            sb.AppendLine("Каталог данных  : " + AppSettings.DataDir);
            sb.AppendLine("Файл программы  : " + Safe(delegate { return System.Windows.Forms.Application.ExecutablePath; }));
            sb.AppendLine();
            return sb.ToString();
        }

        private static string Safe(Func<string> f)
        {
            try { return f(); }
            catch { return "(недоступно)"; }
        }

        public static string AdaptersList(List<AdapterInfo> list)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("--------------------------------------------------------------");
            sb.AppendLine("  СЕТЕВЫЕ АДАПТЕРЫ (всего: " + list.Count + ")");
            sb.AppendLine("--------------------------------------------------------------");
            int shown = 0, hidden = 0;
            for (int i = 0; i < list.Count; i++)
            {
                AdapterInfo a = list[i];
                if (!a.Present) hidden++;
                sb.AppendLine("[" + (i + 1) + "] " + a.Title);
                sb.AppendLine("    Описание      : " + a.Description);
                sb.AppendLine("    Тип           : " + a.TypeName + (a.IsVirtual ? " (виртуальный)" : "") + (a.IsLoopback ? " (служебный)" : ""));
                sb.AppendLine("    Состояние     : " + a.StatusText + (a.StatusDetail.Length > 0 ? " — " + a.StatusDetail : ""));
                sb.AppendLine("    Устройство    : " + (a.Present ? "присутствует" : "ОТСУТСТВУЕТ (скрытый, остаток в реестре)"));
                sb.AppendLine("    IPv4          : " + a.Ipv4Text + (a.Masks.Count > 0 ? "   маска: " + string.Join(", ", a.Masks.ToArray()) : ""));
                sb.AppendLine("    Шлюз          : " + a.GatewayText);
                sb.AppendLine("    IPv6          : " + a.Ipv6Text);
                sb.AppendLine("    DNS           : " + a.DnsText + (a.DnsSource.Length > 0 ? "   [" + a.DnsSource + "]" : ""));
                sb.AppendLine("    Адрес         : " + a.DhcpText + (a.DhcpServer.Length > 0 ? "   сервер: " + a.DhcpServer : ""));
                sb.AppendLine("    MAC           : " + a.Mac + (a.MacPermanent.Length > 0 && a.MacPermanent != a.Mac ? "   заводской: " + a.MacPermanent : ""));
                string driver = (a.Service + " " + a.DriverVersion + " " + a.DriverDate).Trim();
                sb.AppendLine("    Драйвер       : " + (driver.Length > 0 ? driver : "—"));
                sb.AppendLine("    GUID          : " + a.Guid);
                if (a.InstanceId.Length > 0) sb.AppendLine("    Устройство PnP: " + a.InstanceId);
                if (a.RegClassPath.Length > 0) sb.AppendLine("    Реестр (класс): " + a.RegClassPath);
                if (a.RegTcpipPath.Length > 0) sb.AppendLine("    Реестр TCP/IP : " + (a.RegTcpip.Count > 0 ? a.RegTcpipPath : "нет раздела"));
                for (int n = 0; n < a.Notes.Count; n++) sb.AppendLine("    ! " + a.Notes[n]);
                sb.AppendLine();
                shown++;
            }
            sb.AppendLine("Итого: показано " + shown + ", из них без устройства (скрытых): " + hidden);
            sb.AppendLine();
            return sb.ToString();
        }

        public static string CommandSection(string title, string commandLine)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("--------------------------------------------------------------");
            sb.AppendLine("  " + title);
            sb.AppendLine("  > " + commandLine);
            sb.AppendLine("--------------------------------------------------------------");
            CmdResult r = Runner.RunLine(commandLine, 120000);
            sb.AppendLine(string.IsNullOrEmpty(r.Output) ? "(нет вывода)" : r.Output);
            sb.AppendLine();
            return sb.ToString();
        }

        public static string BuildFull(Core core, bool includeConnections)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Header());
            sb.Append(AdaptersList(core.Adapters));

            sb.Append(CommandSection("IP-КОНФИГУРАЦИЯ (ipconfig /all)", "ipconfig /all"));
            sb.Append(CommandSection("ИНТЕРФЕЙСЫ (netsh interface show interface)", "netsh interface show interface"));
            sb.Append(CommandSection("НАСТРОЙКИ IPv4 (netsh interface ipv4 show config)", "netsh interface ipv4 show config"));
            sb.Append(CommandSection("НАСТРОЙКИ IPv6 (netsh interface ipv6 show config)", "netsh interface ipv6 show config"));
            sb.Append(CommandSection("МАРШРУТЫ (route print -4)", "route print -4"));
            sb.Append(CommandSection("ТАБЛИЦА ARP (arp -a)", "arp -a"));
            sb.Append(CommandSection("MAC-АДРЕСА (getmac /v /fo list)", "getmac /v /fo list"));
            sb.Append(CommandSection("ПРОКСИ WinHTTP (netsh winhttp show proxy)", "netsh winhttp show proxy"));
            if (Shell.IsAdmin)
            {
                sb.Append(CommandSection("БРАНДМАУЭР (netsh advfirewall show allprofiles)", "netsh advfirewall show allprofiles"));
                sb.Append(CommandSection("СЕТЕВЫЕ СЛУЖБЫ", "cmd /c \"sc query Dhcp & sc query Dnscache & sc query Nsi & sc query Netprofm & sc query NlaSvc\""));
            }
            if (includeConnections)
            {
                sb.Append(CommandSection("СЕТЕВЫЕ СОЕДИНЕНИЯ (netstat -ano)", "netstat -ano"));
            }
            sb.AppendLine("==============================================================");
            sb.AppendLine("  Конец отчёта NetHelp");
            sb.AppendLine("==============================================================");
            return sb.ToString();
        }

        public static string SaveReport(Core core, bool includeConnections)
        {
            string text = BuildFull(core, includeConnections);
            string dir = Path.Combine(AppSettings.DataDir, "reports");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "nethelp-" + Environment.MachineName + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            File.WriteAllText(file, text, Encoding.UTF8);
            return file;
        }

        public static string SaveAdapterCard(AdapterInfo a)
        {
            string dir = Path.Combine(AppSettings.DataDir, "reports");
            Directory.CreateDirectory(dir);
            string safe = a.Title;
            char[] bad = Path.GetInvalidFileNameChars();
            for (int i = 0; i < bad.Length; i++) safe = safe.Replace(bad[i], '_');
            string file = Path.Combine(dir, "adapter-" + safe + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            File.WriteAllText(file, Header() + a.Details(), Encoding.UTF8);
            return file;
        }

        public static string HostsFilePath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
        }
    }
}
