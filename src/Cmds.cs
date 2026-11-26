using System;
using System.Collections.Generic;

namespace NetHelp
{
    /// <summary>Готовые команды Windows для управления сетью.</summary>
    public static class Cmds
    {
        public static string Q(string s)
        {
            return "\"" + s + "\"";
        }

        public static string[] SetDhcpAddress(string conn)
        {
            return new string[] { "netsh interface ipv4 set address name=" + Q(conn) + " source=dhcp" };
        }

        public static string[] SetStaticAddress(string conn, string ip, string mask, string gw)
        {
            string cmd = "netsh interface ipv4 set address name=" + Q(conn) + " source=static address=" + ip + " mask=" + mask;
            if (gw != null && gw.Length > 0) cmd += " gateway=" + gw + " gwmetric=1";
            else cmd += " gateway=none";
            return new string[] { cmd };
        }

        public static string[] SetDhcpDns(string conn)
        {
            return new string[] { "netsh interface ipv4 set dnsservers name=" + Q(conn) + " source=dhcp" };
        }

        public static string[] SetStaticDns(string conn, string[] servers)
        {
            List<string> list = new List<string>();
            if (servers.Length == 0)
            {
                list.Add("netsh interface ipv4 set dnsservers name=" + Q(conn) + " source=static address=none");
                return list.ToArray();
            }
            list.Add("netsh interface ipv4 set dnsservers name=" + Q(conn) + " source=static address=" + servers[0] + " register=primary validate=no");
            for (int i = 1; i < servers.Length; i++)
                list.Add("netsh interface ipv4 add dnsservers name=" + Q(conn) + " address=" + servers[i] + " index=" + (i + 1) + " validate=no");
            return list.ToArray();
        }

        public static string[] SetMtu(string conn, int mtu)
        {
            return new string[]
            {
                "netsh interface ipv4 set subinterface " + Q(conn) + " mtu=" + mtu + " store=persistent",
                "netsh interface ipv6 set subinterface " + Q(conn) + " mtu=" + mtu + " store=persistent"
            };
        }

        public static string[] SetMetric(string conn, int metric)
        {
            return new string[]
            {
                "netsh interface ipv4 set interface " + Q(conn) + " metric=" + metric,
                "netsh interface ipv6 set interface " + Q(conn) + " metric=" + metric
            };
        }

        public static string[] SetIpv6(string conn, bool enable)
        {
            return new string[]
            {
                "netsh interface ipv6 set interface " + Q(conn) + " disabled=" + (enable ? "false" : "true")
            };
        }

        public static string[] Restart(string conn)
        {
            return new string[]
            {
                "netsh interface set interface name=" + Q(conn) + " admin=disabled",
                "@wait:2500",
                "netsh interface set interface name=" + Q(conn) + " admin=enabled"
            };
        }

        public static string[] SetEnabled(string conn, bool enable)
        {
            return new string[] { "netsh interface set interface name=" + Q(conn) + " admin=" + (enable ? "enabled" : "disabled") };
        }

        public static string ShowConfig(string conn)
        {
            return "netsh interface ipv4 show config name=" + Q(conn);
        }

        public static string ShowDns(string conn)
        {
            return "netsh interface ipv4 show dnsservers name=" + Q(conn);
        }
    }

    /// <summary>Одно действие по сбросу/восстановлению сети.</summary>
    public class ResetAction
    {
        public string Id = "";
        public string Title = "";
        public string Group = "";
        public string Description = "";
        public string[] Commands = new string[0];
        public bool NeedsAdmin = true;
        public bool NeedsReboot = false;
        public bool Danger = false;

        public ResetAction(string id, string group, string title, string description, string[] commands, bool needsAdmin, bool needsReboot, bool danger)
        {
            Id = id; Group = group; Title = title; Description = description;
            Commands = commands; NeedsAdmin = needsAdmin; NeedsReboot = needsReboot; Danger = danger;
        }

        public string FullDescription()
        {
            string s = Title + Environment.NewLine + Environment.NewLine + Description + Environment.NewLine + Environment.NewLine + "Будут выполнены команды:" + Environment.NewLine;
            for (int i = 0; i < Commands.Length; i++) s += "    " + Commands[i] + Environment.NewLine;
            if (NeedsAdmin) s += Environment.NewLine + "Требуются права администратора.";
            if (NeedsReboot) s += Environment.NewLine + "Потребуется перезагрузка Windows.";
            return s;
        }
    }

    /// <summary>Каталог всех действий по сбросу сетевых настроек.</summary>
    public static class ResetCatalog
    {
        public const string G_Fast = "Быстрые действия";
        public const string G_Stack = "Сброс стека и протоколов";
        public const string G_Services = "Службы, прокси, брандмауэр";
        public const string G_Full = "Полное восстановление";

        public static List<ResetAction> All()
        {
            List<ResetAction> l = new List<ResetAction>();

            // ---------- Быстрые ----------
            l.Add(new ResetAction("flushdns", G_Fast, "Очистить кэш DNS",
                "Сбрасывает локальный кэш DNS-имён. Помогает, когда сайт не открывается или открывается старый адрес после смены DNS.",
                new string[] { "ipconfig /flushdns" }, false, false, false));

            l.Add(new ResetAction("registerdns", G_Fast, "Обновить регистрацию в DNS",
                "Заново регистрирует имена компьютера на DNS-сервере и обновляет записи DHCP. Помогает в домене и при проблемах с доступом к ПК по имени.",
                new string[] { "ipconfig /registerdns" }, true, false, false));

            l.Add(new ResetAction("renew", G_Fast, "Обновить адрес DHCP",
                "Запрашивает у DHCP-сервера новый IP-адрес, маску, шлюз и DNS. Основное средство при «нет доступа к интернету».",
                new string[] { "ipconfig /renew" }, true, false, false));

            l.Add(new ResetAction("release", G_Fast, "Освободить адрес DHCP",
                "Отказывается от текущего адреса DHCP. Обычно выполняется в паре с «Обновить адрес DHCP».",
                new string[] { "ipconfig /release" }, true, false, false));

            l.Add(new ResetAction("rerenew", G_Fast, "Переполучить адрес (release + renew)",
                "Полный цикл: освободить адрес, очистить кэш DNS и получить настройки заново.",
                new string[] { "ipconfig /release", "ipconfig /flushdns", "ipconfig /renew", "ipconfig /registerdns" }, true, false, false));

            l.Add(new ResetAction("arp", G_Fast, "Очистить кэш ARP",
                "Удаляет таблицу соответствия IP-адресов и MAC-адресов. Помогает после замены роутера или при конфликте адресов.",
                new string[] { "netsh interface ip delete arpcache", "arp -d *" }, true, false, false));

            l.Add(new ResetAction("netbios", G_Fast, "Очистить кэш NetBIOS",
                "Сбрасывает кэш имён NetBIOS и перерегистрирует их — лечит проблемы с сетевыми папками и принтерами.",
                new string[] { "nbtstat -R", "nbtstat -RR" }, true, false, false));

            l.Add(new ResetAction("routcache", G_Fast, "Очистить кэш маршрутов",
                "Удаляет динамические маршруты. Осторожно: если в системе есть нестандартная маршрутизация, сеть может кратко прерваться.",
                new string[] { "route -f" }, true, false, true));

            l.Add(new ResetAction("dnsreport", G_Fast, "Показать содержимое кэша DNS (диагностика)",
                "Не сбрасывает ничего, только показывает, какие имена уже разложены в кэш.",
                new string[] { "ipconfig /displaydns" }, false, false, false));

            // ---------- Стек ----------
            l.Add(new ResetAction("winsock", G_Stack, "Сбросить Winsock",
                "Восстанавливает каталог Winsock до исходного состояния. Лечит ошибки «Не удалось выполнить очистку сокета», проблемы с VPN, прокси и браузерами. Требуется перезагрузка.",
                new string[] { "netsh winsock reset catalog", "netsh winsock reset" }, true, true, false));

            l.Add(new ResetAction("ipreset", G_Stack, "Сбросить стек TCP/IP",
                "Заново записывает параметры протокола TCP/IP: адреса, шлюзы, DNS, маршруты. Требуется перезагрузка.",
                new string[] { "netsh int ip reset \"%TEMP%\\nethelp-ipreset.log\"" }, true, true, false));

            l.Add(new ResetAction("ipv4reset", G_Stack, "Сбросить параметры IPv4",
                "Восстанавливает параметры интерфейсов и глобальные настройки IPv4 по умолчанию.",
                new string[] { "netsh int ipv4 reset" }, true, true, false));

            l.Add(new ResetAction("ipv6reset", G_Stack, "Сбросить параметры IPv6",
                "Восстанавливает параметры IPv6. Помогает, когда IPv6 мешает работать сети (нет доступа при включённом IPv6).",
                new string[] { "netsh int ipv6 reset" }, true, true, false));

            l.Add(new ResetAction("tcpreset", G_Stack, "Сбросить настройки TCP",
                "Возвращает параметры стека TCP (окна, масштабирование, автоподстройка, RSS) к значениям по умолчанию.",
                new string[] { "netsh int tcp reset" }, true, true, false));

            l.Add(new ResetAction("httpreset", G_Stack, "Сбросить прокси-настройки WinHTTP",
                "Убирает системный прокси, который используют службы Windows, Windows Update и часть программ.",
                new string[] { "netsh winhttp reset proxy" }, true, false, false));

            l.Add(new ResetAction("winsockcatalog", G_Stack, "Показать каталог Winsock (диагностика)",
                "Показывает установленные в системе LSP-провайдеры. Полезно перед сбросом — видно, что устанавливалось (антивирус, VPN).",
                new string[] { "netsh winsock show catalog" }, true, false, false));

            l.Add(new ResetAction("neighbors", G_Stack, "Очистить кэш соседей IPv6",
                "Удаляет таблицу соседей IPv6. Полезно при конфликтах адресов в локальной сети.",
                new string[] { "netsh interface ipv6 delete destinationcache", "netsh interface ipv6 delete neighbors" }, true, false, false));

            // ---------- Службы/прокси/брандмауэр ----------
            l.Add(new ResetAction("winservices", G_Services, "Проверить и запустить сетевые службы",
                "Проверяет режим запуска и запускает ключевые службы: DHCP-клиент, DNS-клиент, NetBIOS, сетевые списки, диспетчер подключений.",
                new string[]
                {
                    "sc config Dhcp start= auto",  "sc start Dhcp",
                    "sc config Dnscache start= auto", "sc start Dnscache",
                    "sc config Nsi start= auto", "sc start Nsi",
                    "sc config nsi start= auto",
                    "sc config Netprofm start= auto", "sc start Netprofm",
                    "sc config NlaSvc start= auto", "sc start NlaSvc",
                    "sc config lmhosts start= auto", "sc start lmhosts",
                    "sc config BITS start= demand", "sc start BITS"
                }, true, false, false));

            l.Add(new ResetAction("restartdns", G_Services, "Перезапустить службу «DNS-клиент»",
                "Быстрый способ применить сброс DNS без перезагрузки. Может сообщить об ошибке зависимостей — это нормально.",
                new string[] { "net stop dnscache", "net start dnscache" }, true, false, false));

            l.Add(new ResetAction("wininetproxy", G_Services, "Отключить прокси в системе (Internet Explorer)",
                "Снимает галочку «Использовать прокси-сервер» в параметрах интернета. Лечит «нет доступа к сети», когда прокси остался от вируса или VPN.",
                new string[]
                {
                    "reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings\" /v ProxyEnable /t REG_DWORD /d 0 /f",
                    "reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings\" /v ProxyServer /t REG_SZ /d \"\" /f",
                    "reg delete \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings\" /v AutoConfigURL /f"
                }, false, false, false));

            l.Add(new ResetAction("firewall", G_Services, "Сбросить брандмауэр Windows",
                "Возвращает правила брандмауэра к состоянию по умолчанию. ВНИМАНИЕ: все созданные вручную правила будут удалены.",
                new string[] { "netsh advfirewall reset" }, true, false, true));

            l.Add(new ResetAction("firewallshow", G_Services, "Показать состояние брандмауэра (диагностика)",
                "Показывает профили брандмауэра и их состояние.",
                new string[] { "netsh advfirewall show allprofiles" }, true, false, false));

            // ---------- Полное восстановление ----------
            l.Add(new ResetAction("full", G_Full, "ПОЛНЫЙ СБРОС СЕТИ (рекомендуется)",
                "Последовательность всех основных сбросов: кэш DNS и ARP, освобождение адреса, Winsock, TCP/IP (IPv4 и IPv6), настройки TCP, прокси WinHTTP, службы сети, затем получение адреса заново. ПОСЛЕ ВЫПОЛНЕНИЯ НУЖНА ПЕРЕЗАГРУЗКА.",
                new string[]
                {
                    "ipconfig /flushdns",
                    "netsh interface ip delete arpcache",
                    "ipconfig /release",
                    "netsh winsock reset catalog",
                    "netsh winsock reset",
                    "netsh int ip reset \"%TEMP%\\nethelp-ipreset.log\"",
                    "netsh int ipv6 reset",
                    "netsh int tcp reset",
                    "netsh winhttp reset proxy",
                    "nbtstat -R",
                    "sc config Dhcp start= auto", "sc start Dhcp",
                    "sc config Dnscache start= auto", "sc start Dnscache",
                    "sc config Nsi start= auto", "sc start Nsi",
                    "ipconfig /renew",
                    "ipconfig /registerdns"
                }, true, true, true));

            l.Add(new ResetAction("fullstack", G_Full, "Только стек: Winsock + TCP/IP (+ перезагрузка)",
                "Самый частый «тяжёлый» сброс: Winsock и стек TCP/IP по IPv4 и IPv6. После него Windows переустановит сетевые протоколы.",
                new string[]
                {
                    "netsh winsock reset catalog",
                    "netsh winsock reset",
                    "netsh int ip reset \"%TEMP%\\nethelp-ipreset.log\"",
                    "netsh int ipv6 reset",
                    "ipconfig /flushdns"
                }, true, true, true));

            l.Add(new ResetAction("reboot", G_Full, "Перезагрузить компьютер",
                "Перезагрузка через 15 секунд с возможностью отмены.",
                new string[] { "shutdown /r /t 15 /c \"NetHelp: перезагрузка после сброса сети\"" }, true, false, true));

            l.Add(new ResetAction("rebootcancel", G_Full, "Отменить запланированную перезагрузку",
                "Отменяет ранее запланированную перезагрузку.",
                new string[] { "shutdown /a" }, true, false, false));

            return l;
        }

        public static ResetAction Find(string id)
        {
            List<ResetAction> all = All();
            for (int i = 0; i < all.Count; i++) if (all[i].Id == id) return all[i];
            return null;
        }
    }
}
