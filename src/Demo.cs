using System;
using System.Collections.Generic;

namespace NetHelp
{
    /// <summary>
    /// Демонстрационный набор адаптеров для снимков интерфейса и документации.
    /// Данные полностью вымышленные: обычные частные диапазоны (192.168.1.0/24, 10.0.0.0/8,
    /// 172.22.0.0/16, ULA fd00::/8) и ненастоящие MAC-адреса — ничего от реального компьютера.
    /// </summary>
    public static class Demo
    {
        private const string Domain = "example.local";

        public static List<AdapterInfo> Adapters()
        {
            List<AdapterInfo> list = new List<AdapterInfo>();

            // 1. Обычный Ethernet
            AdapterInfo eth = New("Ethernet", "Intel(R) Ethernet Connection I219-V", "Intel", "6",
                "e1i65x64", "12.19.2.1", "15.08.2024", "oem12.inf", "{11111111-2222-3333-4444-555555555555}",
                @"PCI\VEN_8086&DEV_15BE&SUBSYS_00000000&REV_00\3&11583659&0&FE", "001122334455", true);
            eth.RegKey = "0001";
            eth.MacPermanent = "00:11:22:33:44:55";
            eth.SpeedText = "1 Гбит/с";
            eth.NetStatus = 2;
            eth.NetEnabled = true;
            eth.Operational = "Up";
            eth.StatusText = "Подключено";
            eth.StatusDetail = "сеть доступна";
            eth.DhcpKnown = true;
            eth.DhcpEnabled = true;
            eth.DhcpServer = "192.168.1.1";
            eth.Domain = Domain;
            eth.Mtu = "1500";
            eth.Metric = "25";
            eth.Ipv4.Add("192.168.1.50");
            eth.Masks.Add("255.255.255.0");
            eth.Gateways.Add("192.168.1.1");
            eth.Ipv6.Add("fd00:1:2:3::150");
            eth.Dns.Add("192.168.1.1");
            eth.Dns.Add("192.168.1.53");
            eth.DnsSource = "получены от DHCP";
            eth.RegClass = RegClass(eth, "6", "0", "14");
            eth.RegTcpip = TcpipDhcp("192.168.1.50", "255.255.255.0", "192.168.1.1", "192.168.1.1", "192.168.1.1,192.168.1.53", "1500", "25");
            eth.RegTcpip6 = Tcpip6Static("fd00:1:2:3::150", "64", "192.168.1.1");
            list.Add(eth);

            // 2. Wi-Fi со статическими DNS
            AdapterInfo wifi = New("Wi-Fi", "Intel(R) Wi-Fi 6 AX201 160MHz", "Intel", "71",
                "Netwtw08", "22.120.0.3", "21.03.2024", "oem31.inf", "{22222222-3333-4444-5555-666666666666}",
                @"PCI\VEN_8086&DEV_06F0&SUBSYS_00000000&REV_00\3&11583659&0&A0", "3C58C2112233", true);
            wifi.RegKey = "0007";
            wifi.MacPermanent = "3C:58:C2:11:22:33";
            wifi.SpeedText = "866 Мбит/с";
            wifi.NetStatus = 2;
            wifi.NetEnabled = true;
            wifi.Operational = "Up";
            wifi.StatusText = "Подключено";
            wifi.StatusDetail = "сеть доступна";
            wifi.IsWireless = true;
            wifi.DhcpKnown = true;
            wifi.DhcpEnabled = true;
            wifi.DhcpServer = "192.168.1.1";
            wifi.Domain = Domain;
            wifi.Mtu = "1500";
            wifi.Metric = "35";
            wifi.Ipv4.Add("192.168.1.77");
            wifi.Masks.Add("255.255.255.0");
            wifi.Gateways.Add("192.168.1.1");
            wifi.Ipv6.Add("fd00:1:2:3::177");
            wifi.Dns.Add("1.1.1.1");
            wifi.Dns.Add("8.8.8.8");
            wifi.DnsSource = "заданы вручную";
            wifi.RegClass = RegClass(wifi, "71", "9", "9");
            wifi.RegTcpip = TcpipDhcp("192.168.1.77", "255.255.255.0", "192.168.1.1", "192.168.1.1", "1.1.1.1,8.8.8.8", "1500", "35");
            list.Add(wifi);

            // 3. Скрытый (отсутствующий) Ethernet — остаток в реестре
            AdapterInfo ghost = New("Ethernet 2", "Realtek PCIe GbE Family Controller", "Realtek", "6",
                "rt640x64", "10.68.512.2023", "12.05.2023", "oem7.inf", "{33333333-4444-5555-6666-777777777777}",
                @"PCI\VEN_10EC&DEV_8168&SUBSYS_00000000&REV_0C\4&2B4C0F1&0&00E0", "A1B2C3D4E5F6", false);
            ghost.RegKey = "0004";
            ghost.Present = false;
            ghost.Hidden = true;
            ghost.NetEnabledKnown = false;
            ghost.StatusText = "Скрыт";
            ghost.StatusDetail = "устройство отсутствует, запись осталась в реестре";
            ghost.DhcpKnown = true;
            ghost.DhcpEnabled = false;
            ghost.Mtu = "1500";
            ghost.Metric = "25";
            ghost.Ipv4.Add("192.168.1.90");
            ghost.Masks.Add("255.255.255.0");
            ghost.Gateways.Add("192.168.1.1");
            ghost.Dns.Add("192.168.1.1");
            ghost.DnsSource = "реестр: заданы вручную";
            ghost.RegClass = RegClass(ghost, "6", "0", "14");
            ghost.RegTcpip = TcpipStatic("192.168.1.90", "255.255.255.0", "192.168.1.1", "192.168.1.1", "1500", "25");
            ghost.Notes.Add("Данные взяты из реестра (устройство сейчас отсутствует).");
            list.Add(ghost);

            // 4. Виртуальный адаптер Hyper-V
            AdapterInfo veth = New("vEthernet (Default Switch)", "Hyper-V Virtual Ethernet Adapter", "Microsoft", "6",
                "VMSMP", "10.0.22621.1", "21.06.2006", "", "{44444444-5555-6666-7777-888888888888}",
                @"ROOT\VMS_MP\0000", "00155D0A1B2C", true);
            veth.RegKey = "0010";
            veth.MacPermanent = "00:15:5D:0A:1B:2C";
            veth.SpeedText = "10 Гбит/с";
            veth.NetStatus = 2;
            veth.NetEnabled = true;
            veth.Operational = "Up";
            veth.StatusText = "Подключено";
            veth.StatusDetail = "сеть доступна";
            veth.IsVirtual = true;
            veth.DhcpKnown = true;
            veth.DhcpEnabled = false;
            veth.Mtu = "1500";
            veth.Metric = "5";
            veth.Ipv4.Add("172.22.64.1");
            veth.Masks.Add("255.255.240.0");
            veth.Dns.Add("192.168.1.1");
            veth.DnsSource = "заданы вручную";
            veth.RegClass = RegClass(veth, "6", "0", "14");
            list.Add(veth);

            // 5. VPN-туннель
            AdapterInfo vpn = New("WireGuard Tunnel", "WireGuard Virtual Adapter", "WireGuard LLC", "53",
                "WireGuard", "0.5.3", "01.02.2024", "oem44.inf", "{55555555-6666-7777-8888-999999999999}",
                @"ROOT\NET\0001", "DEADBEEF0001", true);
            vpn.RegKey = "0012";
            vpn.MacPermanent = "DE:AD:BE:EF:00:01";
            vpn.SpeedText = "1 Гбит/с";
            vpn.NetStatus = 2;
            vpn.NetEnabled = true;
            vpn.Operational = "Up";
            vpn.StatusText = "Подключено";
            vpn.StatusDetail = "сеть доступна";
            vpn.IsVirtual = true;
            vpn.DhcpKnown = true;
            vpn.DhcpEnabled = false;
            vpn.Mtu = "1420";
            vpn.Metric = "10";
            vpn.Ipv4.Add("10.7.0.2");
            vpn.Masks.Add("255.255.255.255");
            vpn.Dns.Add("10.7.0.1");
            vpn.DnsSource = "заданы вручную";
            vpn.RegClass = RegClass(vpn, "53", "0", "0");
            vpn.RegTcpip = TcpipStatic("10.7.0.2", "255.255.255.255", "", "10.7.0.1", "1420", "10");
            list.Add(vpn);
            vpn.Notes.Add("Туннель: MTU уменьшен до 1420, чтобы пакеты не дробились.");

            // 6. Остаток настроек TCP/IP без устройства
            AdapterInfo orphan = new AdapterInfo();
            orphan.Guid = "{66666666-7777-8888-9999-AAAAAAAAAAAA}";
            orphan.RegistryOnly = true;
            orphan.Present = false;
            orphan.Hidden = true;
            orphan.Description = "Остаток настроек TCP/IP: устройства с таким GUID в системе нет";
            orphan.TitleOverride = "Скрытый остаток 66666666…";
            orphan.StatusText = "Скрыт";
            orphan.StatusDetail = "устройство отсутствует, запись осталась в реестре";
            orphan.DhcpKnown = true;
            orphan.DhcpEnabled = false;
            orphan.Ipv4.Add("10.0.0.5");
            orphan.Masks.Add("255.255.255.0");
            orphan.Dns.Add("10.0.0.1");
            orphan.DnsSource = "реестр: заданы вручную";
            orphan.Mtu = "1400";
            orphan.Metric = "30";
            orphan.RegTcpip = TcpipStatic("10.0.0.5", "255.255.255.0", "10.0.0.1", "10.0.0.1", "1400", "30");
            orphan.Notes.Add("Данные взяты из реестра (устройство сейчас отсутствует).");
            list.Add(orphan);

            // 7. Служебный Loopback
            AdapterInfo lo = New("Loopback Pseudo-Interface 1", "Software Loopback Interface 1", "Microsoft", "24",
                "", "", "", "", "{77777777-8888-9999-AAAA-BBBBBBBBBBBB}", "", "", true);
            lo.IsLoopback = true;
            lo.IsVirtual = true;
            lo.NetStatus = 2;
            lo.NetEnabled = true;
            lo.Operational = "Up";
            lo.StatusText = "Подключено";
            lo.StatusDetail = "служебный интерфейс";
            lo.DhcpKnown = true;
            lo.DhcpEnabled = false;
            lo.Mtu = "4294967295";
            lo.Ipv4.Add("127.0.0.1");
            lo.Masks.Add("255.0.0.0");
            lo.Ipv6.Add("::1");
            list.Add(lo);

            return list;
        }

        private static AdapterInfo New(string conn, string desc, string mfg, string ifType, string service,
            string ver, string date, string inf, string guid, string pnp, string mac, bool present)
        {
            AdapterInfo a = new AdapterInfo();
            a.ConnectionName = conn;
            a.Description = desc;
            a.Manufacturer = mfg;
            a.Provider = mfg;
            a.IfType = ifType;
            a.MediaType = ifType == "71" ? "9" : "0";
            a.PhysicalMediaType = ifType == "71" ? "9" : "14";
            a.Service = service;
            a.DriverVersion = ver;
            a.DriverDate = date;
            a.InfPath = inf;
            a.Guid = guid;
            a.InstanceId = pnp;
            a.Mac = FormatMac(mac);
            a.Present = present;
            a.Hidden = !present;
            a.SeenByWmi = true;
            a.SeenByDotNet = present;
            a.NetEnabledKnown = true;
            a.NetEnabled = present;
            a.StatusText = present ? "Есть" : "Скрыт";
            a.RegClass = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return a;
        }

        private static string FormatMac(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length != 12) return "";
            return hex.Substring(0, 2) + ":" + hex.Substring(2, 2) + ":" + hex.Substring(4, 2) + ":" +
                   hex.Substring(6, 2) + ":" + hex.Substring(8, 2) + ":" + hex.Substring(10, 2);
        }

        private static Dictionary<string, string> RegClass(AdapterInfo a, string ifType, string mediaType, string physMedia)
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            d["DriverDesc"] = a.Description;
            d["NetCfgInstanceId"] = a.Guid;
            d["Driver"] = a.Service;
            d["ProviderName"] = a.Provider;
            d["DriverVersion"] = a.DriverVersion;
            d["DriverDate"] = a.DriverDate;
            d["InfPath"] = a.InfPath;
            d["InfSection"] = a.Service + ".ndi";
            d["MatchingDeviceId"] = a.InstanceId.Length > 15 ? a.InstanceId.Substring(0, 15) : a.InstanceId;
            d["DeviceInstanceID"] = a.InstanceId;
            d["*IfType"] = ifType;
            d["*MediaType"] = mediaType;
            d["*PhysicalMediaType"] = physMedia;
            return d;
        }

        private static Dictionary<string, string> TcpipDhcp(string ip, string mask, string gw, string server, string dns, string mtu, string metric)
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            d["EnableDHCP"] = "1";
            d["DhcpIPAddress"] = ip;
            d["DhcpSubnetMask"] = mask;
            d["DhcpDefaultGateway"] = gw;
            d["DhcpServer"] = server;
            d["DhcpNameServer"] = dns.Replace(",", " ");
            d["DhcpDomain"] = Domain;
            d["LeaseObtainedTime"] = "1772000000";
            d["LeaseTerminatesTime"] = "1772086400";
            d["Domain"] = Domain;
            d["MTU"] = mtu;
            d["InterfaceMetric"] = metric;
            d["RegistrationEnabled"] = "1";
            return d;
        }

        private static Dictionary<string, string> TcpipStatic(string ip, string mask, string gw, string dns, string mtu, string metric)
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            d["EnableDHCP"] = "0";
            d["IPAddress"] = ip;
            d["SubnetMask"] = mask;
            if (!string.IsNullOrEmpty(gw)) d["DefaultGateway"] = gw;
            d["NameServer"] = dns.Replace(",", " ");
            d["Domain"] = Domain;
            d["MTU"] = mtu;
            d["InterfaceMetric"] = metric;
            d["RegistrationEnabled"] = "1";
            return d;
        }

        private static Dictionary<string, string> Tcpip6Static(string ip, string prefix, string gw)
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            d["EnableDHCP"] = "0";
            d["IPAddress"] = ip;
            d["PrefixLength"] = prefix;
            d["DefaultGateway"] = gw;
            return d;
        }
    }
}
