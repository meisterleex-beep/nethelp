using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;

namespace NetHelp
{
    /// <summary>Один сетевой адаптер: данные PnP, реестра, WMI и живой конфигурации.</summary>
    public class AdapterInfo
    {
        public string RegKey = "";
        public string Guid = "";
        public string Description = "";
        public string ConnectionName = "";
        public string InstanceId = "";
        public string Service = "";
        public string Provider = "";
        public string DriverVersion = "";
        public string DriverDate = "";
        public string InfPath = "";
        public string Manufacturer = "";
        public string Mac = "";
        public string MacPermanent = "";

        public bool Present = true;
        public bool Hidden = false;
        public bool RegistryOnly = false;
        public bool SeenByWmi = false;
        public bool SeenByDotNet = false;
        public bool WmiPhysical = false;
        public int WmiIndex = -1;

        public bool NetEnabled = false;
        public bool NetEnabledKnown = false;
        public int NetStatus = -1;
        public int ErrorCode = 0;
        public string Operational = "";
        public string SpeedText = "";

        public bool DhcpEnabled = false;
        public bool DhcpKnown = false;
        public string DhcpServer = "";
        public string Domain = "";
        public string Mtu = "";
        public string Metric = "";
        public string IfType = "";
        public string MediaType = "";
        public string PhysicalMediaType = "";
        public string StatusText = "—";
        public string StatusDetail = "";
        public bool IsVirtual = false;
        public bool IsLoopback = false;
        public bool IsWireless = false;

        public List<string> Ipv4 = new List<string>();
        public List<string> Ipv6 = new List<string>();
        public List<string> Masks = new List<string>();
        public List<string> Gateways = new List<string>();
        public List<string> Dns = new List<string>();
        public string DnsSource = "";
        public List<string> Notes = new List<string>();
        public string TitleOverride = "";

        public Dictionary<string, string> RegClass = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> RegTcpip = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> RegTcpip6 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string RegClassPath
        {
            get { return RegKey.Length > 0 ? @"HKLM\" + Reg.NetClassPath + @"\" + RegKey : ""; }
        }

        public string RegTcpipPath
        {
            get { return Guid.Length > 0 ? @"HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\" + Guid : ""; }
        }

        public string Title
        {
            get
            {
                if (TitleOverride.Length > 0) return TitleOverride;
                if (ConnectionName.Length > 0) return ConnectionName;
                if (Description.Length > 0) return Description;
                if (Guid.Length > 0) return Guid;
                return "(без имени)";
            }
        }

        /// <summary>Можно ли управлять адаптером командами netsh (нужно имя подключения и наличие устройства).</summary>
        public bool Manageable
        {
            get { return ConnectionName.Length > 0 && Present; }
        }

        public string TypeName
        {
            get
            {
                int t = 0;
                int.TryParse(IfType, out t);
                switch (t)
                {
                    case 6: return "Ethernet";
                    case 71: return "Wi-Fi";
                    case 23: return "PPP (PPPoE)";
                    case 24: return "Loopback";
                    case 131: return "Туннель";
                    case 237: return "Wi-Fi Direct";
                    case 243: return "Мобильная (WWAN)";
                    case 53: return "Виртуальный";
                    case 1: return "Другое";
                    default:
                        if (MediaType == "9") return "Wi-Fi";
                        if (MediaType == "0") return "Ethernet";
                        return t > 0 ? ("Тип " + t) : "—";
                }
            }
        }

        public string Ipv4Text { get { return Ipv4.Count > 0 ? string.Join(", ", Ipv4.ToArray()) : "—"; } }
        public string Ipv6Text { get { return Ipv6.Count > 0 ? string.Join(", ", Ipv6.ToArray()) : "—"; } }
        public string GatewayText { get { return Gateways.Count > 0 ? string.Join(", ", Gateways.ToArray()) : "—"; } }
        public string DnsText { get { return Dns.Count > 0 ? string.Join(", ", Dns.ToArray()) : "—"; } }

        public string DhcpText
        {
            get
            {
                if (!DhcpKnown) return "нет данных";
                if (!DhcpEnabled) return "вручную";
                return DhcpServer.Length > 0 ? ("DHCP " + DhcpServer) : "DHCP";
            }
        }

        public string VisibilityText
        {
            get
            {
                if (!Present) return "скрыт";
                if (!NetEnabledKnown) return "есть";
                return NetEnabled ? "включён" : "отключён";
            }
        }

        public object[] ToRow()
        {
            return new object[]
            {
                Title,
                Description,
                TypeName,
                StatusText,
                VisibilityText,
                Mac,
                Ipv4Text,
                DnsText,
                DhcpText,
                GatewayText
            };
        }

        public static string[] ColumnNames()
        {
            return new string[]
            {
                "Подключение", "Описание", "Тип", "Состояние", "Устройство",
                "MAC", "IPv4", "DNS", "Адрес", "Шлюз"
            };
        }

        /// <summary>Подробная карточка адаптера для панели сведений.</summary>
        public string Details()
        {
            StringBuilder sb = new StringBuilder();
            Section(sb, "ОБЩИЕ СВЕДЕНИЯ");
            Row(sb, "Имя подключения", ConnectionName.Length > 0 ? ConnectionName : "(нет)");
            Row(sb, "Описание", Description);
            Row(sb, "Производитель", Manufacturer);
            Row(sb, "Тип интерфейса", TypeName + (IfType.Length > 0 ? "  (*IfType=" + IfType + ")" : ""));
            Row(sb, "Состояние", StatusText + (StatusDetail.Length > 0 ? "  — " + StatusDetail : ""));
            Row(sb, "Устройство", Present ? "присутствует" : "НЕТ (остаток в реестре / скрытый)");
            Row(sb, "Служба (драйвер)", Service);
            Row(sb, "Версия драйвера", DriverVersion);
            Row(sb, "Дата драйвера", DriverDate);
            Row(sb, "INF-файл", InfPath);
            Row(sb, "Экземпляр устройства", InstanceId);
            Row(sb, "GUID (NetCfgInstanceId)", Guid);
            Row(sb, "MAC (текущий)", Mac);
            Row(sb, "MAC (заводской)", MacPermanent);
            Row(sb, "Источники данных", "PnP=" + (InstanceId.Length > 0 ? "да" : "нет") + ", WMI=" + (SeenByWmi ? "да" : "нет") + ", Windows=" + (SeenByDotNet ? "да" : "нет"));
            if (IsVirtual) Row(sb, "Примечание", "Виртуальный / программный адаптер");
            if (IsLoopback) Row(sb, "Примечание", "Loopback (служебный)");

            Section(sb, "СЕТЕВЫЕ ПАРАМЕТРЫ");
            Row(sb, "IPv4", Ipv4Text);
            Row(sb, "Маска", Masks.Count > 0 ? string.Join(", ", Masks.ToArray()) : "—");
            Row(sb, "Шлюз", GatewayText);
            Row(sb, "IPv6", Ipv6Text);
            Row(sb, "DNS-серверы", DnsText + (DnsSource.Length > 0 ? "   [" + DnsSource + "]" : ""));
            Row(sb, "DHCP", DhcpKnown ? (DhcpEnabled ? "включён" : "выключен (адрес задан вручную)") : "нет данных");
            Row(sb, "Сервер DHCP", DhcpServer.Length > 0 ? DhcpServer : "—");
            Row(sb, "DNS-суффикс", Domain.Length > 0 ? Domain : "—");
            Row(sb, "MTU", Mtu.Length > 0 ? Mtu : "—");
            Row(sb, "Метрика интерфейса", Metric.Length > 0 ? Metric : "—");
            Row(sb, "Скорость канала", SpeedText.Length > 0 ? SpeedText : "—");

            if (Guid.Length > 0)
            {
                Section(sb, "ГОТОВЫЕ КОМАНДЫ (netsh)");
                string n = "\"" + (ConnectionName.Length > 0 ? ConnectionName : "Имя подключения") + "\"";
                Command(sb, "netsh interface ipv4 set address name=" + n + " source=dhcp");
                Command(sb, "netsh interface ipv4 set dnsservers name=" + n + " source=dhcp");
                Command(sb, "netsh interface ipv4 set dnsservers name=" + n + " static 1.1.1.1 primary");
                Command(sb, "netsh interface ipv4 set subinterface " + n + " mtu=1400 store=persistent");
                Command(sb, "netsh interface set interface name=" + n + " admin=disabled");
                Command(sb, "netsh interface ipv4 show config name=" + n);
            }

            DumpRegistry(sb, "РЕЕСТР: КЛАСС СЕТЕВЫХ АДАПТЕРОВ", RegClassPath, RegClass);
            DumpRegistry(sb, "РЕЕСТР: TCP/IP v4 (Services\\Tcpip\\Parameters\\Interfaces)", RegTcpipPath, RegTcpip);
            if (RegTcpip6.Count > 0)
                DumpRegistry(sb, "РЕЕСТР: TCP/IP v6 (Services\\Tcpip6\\Parameters\\Interfaces)",
                    Guid.Length > 0 ? @"HKLM\SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters\Interfaces\" + Guid : "", RegTcpip6);

            if (Notes.Count > 0)
            {
                Section(sb, "ЗАМЕЧАНИЯ");
                for (int i = 0; i < Notes.Count; i++) sb.AppendLine("  • " + Notes[i]);
            }
            return sb.ToString();
        }

        private static void DumpRegistry(StringBuilder sb, string title, string path, Dictionary<string, string> vals)
        {
            Section(sb, title);
            if (path.Length > 0) sb.AppendLine("  Путь: " + path);
            if (vals == null || vals.Count == 0)
            {
                sb.AppendLine("  (раздел отсутствует)");
                return;
            }
            List<string> keys = new List<string>(vals.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < keys.Count; i++)
            {
                string v = vals[keys[i]];
                if (v != null && v.Length > 400) v = v.Substring(0, 400) + "…";
                sb.AppendLine("  " + keys[i].PadRight(26) + " = " + v);
            }
        }

        private static void Section(StringBuilder sb, string title)
        {
            sb.AppendLine();
            sb.AppendLine("## " + title);
        }

        private static void Row(StringBuilder sb, string name, string value)
        {
            sb.AppendLine("  " + name.PadRight(24) + ": " + (value == null ? "" : value));
        }

        private static void Command(StringBuilder sb, string cmd)
        {
            sb.AppendLine("  > " + cmd);
        }
    }

    /// <summary>Сбор полного списка сетевых адаптеров из всех доступных источников.</summary>
    public static class Inventory
    {
        private static readonly string[] WmiAdapterProps = new string[]
        {
            "Index", "GUID", "NetConnectionID", "Name", "Description", "MACAddress", "NetEnabled",
            "NetConnectionStatus", "Speed", "ConfigManagerErrorCode", "PNPDeviceID", "ServiceName",
            "AdapterType", "PhysicalAdapter", "Installed", "SystemName"
        };

        public static List<AdapterInfo> Collect()
        {
            List<AdapterInfo> list = new List<AdapterInfo>();
            Dictionary<string, AdapterInfo> byGuid = new Dictionary<string, AdapterInfo>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, AdapterInfo> byRegKey = new Dictionary<string, AdapterInfo>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, AdapterInfo> byInstance = new Dictionary<string, AdapterInfo>(StringComparer.OrdinalIgnoreCase);

            // ---------- 1. Реестр: класс сетевых адаптеров (все установленные, включая остатки) ----------
            Dictionary<string, Dictionary<string, string>> classEntries = Reg.ReadClassEntries();
            Dictionary<string, string> connNames = Reg.ReadConnectionNames();
            foreach (KeyValuePair<string, Dictionary<string, string>> kv in classEntries)
            {
                Dictionary<string, string> v = kv.Value;
                string desc = Reg.V(v, "DriverDesc");
                string guid = NormGuid(Reg.V(v, "NetCfgInstanceId"));
                if (desc.Length == 0 && guid.Length == 0) continue;

                AdapterInfo a = new AdapterInfo();
                a.RegKey = kv.Key;
                a.RegClass = v;
                a.Description = desc;
                a.Guid = guid;
                a.Service = Reg.V(v, "Driver");
                a.Provider = Reg.V(v, "ProviderName");
                if (a.Provider.Length == 0) a.Provider = Reg.V(v, "Provider");
                a.DriverVersion = Reg.V(v, "DriverVersion");
                a.DriverDate = FormatDriverDate(Reg.V(v, "DriverDate"));
                a.InfPath = Reg.V(v, "InfPath");
                a.IfType = Reg.V(v, "*IfType");
                a.MediaType = Reg.V(v, "*MediaType");
                a.PhysicalMediaType = Reg.V(v, "*PhysicalMediaType");
                a.MacPermanent = Reg.V(v, "PermanentAddress");
                if (a.MacPermanent.Length == 0) a.MacPermanent = Reg.V(v, "NetworkAddress");
                string netAddr = Reg.V(v, "NetworkAddress");
                if (netAddr.Length > 0) a.Notes.Add("MAC переопределён вручную (NetworkAddress=" + netAddr + ")");
                if (a.Description.Length == 0) a.Description = Reg.V(v, "DeviceInstanceID");

                list.Add(a);
                byRegKey[kv.Key] = a;
                if (guid.Length > 0) byGuid[guid] = a;
                a.Present = false; // уточняется ниже по PnP/WMI
            }

            // ---------- 2. PnP (SetupAPI): присутствие устройства и его экземпляр ----------
            bool setupApiOk = false;
            try
            {
                List<DevNode> nodes = SetupApi.Enumerate();
                setupApiOk = nodes.Count > 0;
                for (int i = 0; i < nodes.Count; i++)
                {
                    DevNode dn = nodes[i];
                    string rk = LastSegment(dn.DriverKey);
                    AdapterInfo a = null;
                    if (rk.Length > 0) byRegKey.TryGetValue(rk, out a);
                    if (a == null)
                    {
                        // устройство класса «сеть», для которого нет раздела в Class — создаём отдельную запись
                        a = new AdapterInfo();
                        a.RegKey = rk;
                        a.InstanceId = dn.InstanceId;
                        a.Description = dn.Desc.Length > 0 ? dn.Desc : dn.FriendlyName;
                        a.Manufacturer = dn.Manufacturer;
                        a.RegistryOnly = rk.Length == 0;
                        a.Present = dn.Present;
                        a.Hidden = !dn.Present;
                        list.Add(a);
                    }
                    a.InstanceId = dn.InstanceId;
                    a.Present = dn.Present;
                    a.Hidden = !dn.Present;
                    if (a.Description.Length == 0) a.Description = dn.Desc.Length > 0 ? dn.Desc : dn.FriendlyName;
                    if (a.Manufacturer.Length == 0) a.Manufacturer = dn.Manufacturer;
                    if (dn.InstanceId.Length > 0) byInstance[dn.InstanceId] = a;
                }
            }
            catch { }

            // ---------- 3. WMI: NetConnectionID, состояние, ошибки, скорость ----------
            List<Dictionary<string, object>> wmiAdapters = Wmi.Query("Win32_NetworkAdapter", WmiAdapterProps);
            for (int i = 0; i < wmiAdapters.Count; i++)
            {
                Dictionary<string, object> w = wmiAdapters[i];
                string guid = NormGuid(Wmi.S(w, "GUID"));
                string pnp = Wmi.S(w, "PNPDeviceID");
                AdapterInfo a = null;
                if (guid.Length > 0) byGuid.TryGetValue(guid, out a);
                if (a == null && pnp.Length > 0) byInstance.TryGetValue(pnp, out a);
                if (a == null)
                {
                    string name = Wmi.S(w, "NetConnectionID");
                    string desc = Wmi.S(w, "Name");
                    if (desc.Length == 0 && name.Length == 0) continue;
                    if (guid.Length == 0 && pnp.Length == 0 && name.Length == 0) continue;
                    a = new AdapterInfo();
                    a.Guid = guid;
                    a.InstanceId = pnp;
                    a.Description = desc.Length > 0 ? desc : name;
                    a.RegistryOnly = guid.Length == 0;
                    list.Add(a);
                    if (guid.Length > 0) byGuid[guid] = a;
                }

                a.SeenByWmi = true;
                a.WmiIndex = Wmi.I(w, "Index", -1);
                string conn = Wmi.S(w, "NetConnectionID");
                if (conn.Length > 0) a.ConnectionName = conn;
                string mac = Wmi.S(w, "MACAddress");
                if (mac.Length > 0) a.Mac = mac;
                if (a.Description.Length == 0) a.Description = Wmi.S(w, "Name");
                if (a.Service.Length == 0) a.Service = Wmi.S(w, "ServiceName");
                a.NetEnabledKnown = true;
                a.NetEnabled = Wmi.B(w, "NetEnabled", false);
                a.NetStatus = Wmi.I(w, "NetConnectionStatus", -1);
                a.ErrorCode = Wmi.I(w, "ConfigManagerErrorCode", 0);
                a.WmiPhysical = Wmi.B(w, "PhysicalAdapter", false);
                if (pnp.Length > 0 && a.InstanceId.Length == 0) a.InstanceId = pnp;
                object sp = null;
                w.TryGetValue("Speed", out sp);
                if (sp != null) a.SpeedText = FormatSpeed(sp);
                if (a.ErrorCode == 45) { a.Present = false; a.Hidden = true; }
                else if (a.ErrorCode != 45 && a.ErrorCode != 0) { a.Present = true; }
            }

            // ---------- 4. Имена подключений из реестра (работает и для скрытых) ----------
            foreach (KeyValuePair<string, string> kv in connNames)
            {
                AdapterInfo a;
                if (byGuid.TryGetValue(NormGuid(kv.Key), out a) && a.ConnectionName.Length == 0)
                    a.ConnectionName = kv.Value;
            }

            // ---------- 5. Живая конфигурация Windows (NetworkInterface) ----------
            try
            {
                NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
                for (int i = 0; i < nics.Length; i++)
                {
                    NetworkInterface ni = nics[i];
                    string guid = NormGuid(ni.Id);
                    AdapterInfo a = null;
                    if (guid.Length > 0) byGuid.TryGetValue(guid, out a);
                    if (a == null)
                    {
                        for (int j = 0; j < list.Count; j++)
                            if (string.Equals(list[j].ConnectionName, ni.Name, StringComparison.OrdinalIgnoreCase)) { a = list[j]; break; }
                    }
                    if (a == null)
                    {
                        a = new AdapterInfo();
                        a.Guid = guid;
                        a.RegistryOnly = true;
                        list.Add(a);
                        if (guid.Length > 0) byGuid[guid] = a;
                    }
                    a.SeenByDotNet = true;
                    a.Present = true;
                    a.Hidden = false;
                    if (ni.Name.Length > 0) a.ConnectionName = ni.Name;
                    if (a.Description.Length == 0) a.Description = ni.Description;
                    if (a.Description.Length == 0) a.Description = ni.Name;
                    a.Mac = ni.GetPhysicalAddress().ToString();
                    a.Operational = ni.OperationalStatus.ToString();
                    if (ni.Speed > 0) a.SpeedText = FormatSpeed(ni.Speed);

                    try
                    {
                        IPInterfaceProperties p = ni.GetIPProperties();
                        foreach (UnicastIPAddressInformation u in p.UnicastAddresses)
                        {
                            if (u.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                a.Ipv4.Add(u.Address.ToString());
                                a.Masks.Add(u.IPv4Mask != null ? u.IPv4Mask.ToString() : "");
                            }
                            else if (u.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                            {
                                if (u.Address.IsIPv6LinkLocal) continue;
                                a.Ipv6.Add(u.Address.ToString());
                            }
                        }
                        for (int k = a.Masks.Count - 1; k >= 0; k--) if (a.Masks[k].Length == 0) a.Masks.RemoveAt(k);

                        foreach (GatewayIPAddressInformation g in p.GatewayAddresses)
                        {
                            if (g.Address != null && g.Address.ToString() != "0.0.0.0") a.Gateways.Add(g.Address.ToString());
                        }
                        foreach (System.Net.IPAddress d in p.DnsAddresses)
                        {
                            if (d.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) a.Dns.Add(d.ToString());
                            else a.Dns.Add(d.ToString());
                        }
                        foreach (System.Net.IPAddress d in p.DhcpServerAddresses)
                        {
                            string ds = d.ToString();
                            if (ds == "255.255.255.255" || ds == "0.0.0.0") continue;
                            a.DhcpServer = ds;
                            break;
                        }
                        if (p.DnsSuffix != null && p.DnsSuffix.Length > 0) a.Domain = p.DnsSuffix;
                        try
                        {
                            IPv4InterfaceProperties v4 = p.GetIPv4Properties();
                            if (v4 != null)
                            {
                                a.DhcpKnown = true;
                                a.DhcpEnabled = v4.IsDhcpEnabled;
                                a.Mtu = v4.Mtu.ToString();
                            }
                        }
                        catch { }
                        if (!a.DhcpKnown)
                        {
                            try
                            {
                                IPv6InterfaceProperties v6 = p.GetIPv6Properties();
                                if (v6 != null && a.Mtu.Length == 0) a.Mtu = v6.Mtu.ToString();
                            }
                            catch { }
                        }
                    }
                    catch { }
                    if (a.Dns.Count > 0) a.DnsSource = a.DhcpEnabled ? "получены от DHCP" : "заданы вручную";
                }
            }
            catch { }

            // ---------- 6. Реестр TCP/IP: настройки, в том числе у скрытых адаптеров ----------
            Dictionary<string, Dictionary<string, string>> t4 = Reg.ReadInterfaceKeys(Reg.Tcpip4Path);
            foreach (KeyValuePair<string, Dictionary<string, string>> kv in t4)
            {
                string guid = NormGuid(kv.Key);
                AdapterInfo a;
                if (!byGuid.TryGetValue(guid, out a))
                {
                    a = new AdapterInfo();
                    a.Guid = guid;
                    a.RegistryOnly = true;
                    a.Present = false;
                    a.Hidden = true;
                    a.Description = "Остаток настроек TCP/IP: устройства с таким GUID в системе нет";
                    a.TitleOverride = "Скрытый остаток " + ShortGuid(guid);
                    list.Add(a);
                    byGuid[guid] = a;
                }
                a.RegTcpip = kv.Value;
            }
            Dictionary<string, Dictionary<string, string>> t6 = Reg.ReadInterfaceKeys(Reg.Tcpip6Path);
            foreach (KeyValuePair<string, Dictionary<string, string>> kv in t6)
            {
                string guid = NormGuid(kv.Key);
                AdapterInfo a;
                if (byGuid.TryGetValue(guid, out a)) a.RegTcpip6 = kv.Value;
            }

            // ---------- 7. Дополняем данными из реестра (для скрытых адаптеров это единственный источник) ----------
            for (int i = 0; i < list.Count; i++)
            {
                AdapterInfo a = list[i];
                Dictionary<string, string> r = a.RegTcpip;
                if (r.Count > 0)
                {
                    int enableDhcp = Reg.VI(r, "EnableDHCP", -1);
                    bool fromRegistry = false;
                    if (!a.DhcpKnown && enableDhcp >= 0)
                    {
                        a.DhcpKnown = true;
                        a.DhcpEnabled = enableDhcp == 1;
                    }
                    if (a.DhcpServer.Length == 0)
                    {
                        string ds = Reg.V(r, "DhcpServer");
                        if (ds == "255.255.255.255" || ds == "0.0.0.0" || ds == "::") ds = "";
                        a.DhcpServer = ds;
                    }

                    if (a.Ipv4.Count == 0)
                    {
                        List<string> ips = Reg.VList(r, a.DhcpEnabled ? "DhcpIPAddress" : "IPAddress");
                        if (ips.Count == 0) ips = Reg.VList(r, "IPAddress");
                        if (ips.Count == 0) ips = Reg.VList(r, "DhcpIPAddress");
                        if (ips.Count > 0) { a.Ipv4.AddRange(Clean(ips)); fromRegistry = true; }
                        List<string> masks = Reg.VList(r, a.DhcpEnabled ? "DhcpSubnetMask" : "SubnetMask");
                        if (masks.Count == 0) masks = Reg.VList(r, "SubnetMask");
                        if (masks.Count == 0) masks = Reg.VList(r, "DhcpSubnetMask");
                        a.Masks.AddRange(Clean(masks));
                    }
                    if (a.Gateways.Count == 0)
                    {
                        List<string> gw = Reg.VList(r, a.DhcpEnabled ? "DhcpDefaultGateway" : "DefaultGateway");
                        if (gw.Count == 0) gw = Reg.VList(r, "DefaultGateway");
                        if (gw.Count == 0) gw = Reg.VList(r, "DhcpDefaultGateway");
                        gw = Clean(gw);
                        if (gw.Count > 0) { a.Gateways.AddRange(gw); fromRegistry = true; }
                    }
                    if (a.Dns.Count == 0)
                    {
                        List<string> dns = Reg.VList(r, "NameServer");
                        string src = "реестр: заданы вручную";
                        if (dns.Count == 0) { dns = Reg.VList(r, "DhcpNameServer"); src = "реестр: получены от DHCP"; }
                        if (dns.Count > 0) { a.Dns.AddRange(dns); a.DnsSource = src; fromRegistry = true; }
                    }
                    if (a.Domain.Length == 0)
                    {
                        string dom = Reg.V(r, "DhcpDomain");
                        if (dom.Length == 0) dom = Reg.V(r, "Domain");
                        a.Domain = dom;
                    }
                    if (a.Mtu.Length == 0) a.Mtu = Reg.V(r, "MTU");
                    a.Metric = Reg.V(r, "InterfaceMetric");
                    if (fromRegistry) a.Notes.Add("Данные взяты из реестра (устройство сейчас отсутствует).");
                }
                // Классификация
                string probe = (a.Description + " " + a.Service + " " + a.Manufacturer).ToLowerInvariant();
                a.IsLoopback = probe.Contains("loopback");
                string[] virtualMarks = new string[]
                {
                    "vmware", "virtualbox", "vbox", "hyper-v", "hyperv", "tap-", "tap adapter", "openvpn",
                    "wireguard", "tailscale", "zerotier", "npcap", "wan miniport", "microsoft kernel debug",
                    "pseudo", "virtual", "vmnet", "teredo", "isatap", "6to4", "radmin", "softether", "bluetooth"
                };
                for (int m = 0; m < virtualMarks.Length; m++)
                    if (probe.Contains(virtualMarks[m])) { a.IsVirtual = true; break; }
                a.IsWireless = a.TypeName == "Wi-Fi";
                if (a.IfType == "24") a.IsVirtual = true;

                // Состояние
                if (!a.Present)
                {
                    a.StatusText = "Скрыт";
                    a.StatusDetail = "устройство отсутствует, запись осталась в реестре";
                }
                else if (a.ErrorCode == 22)
                {
                    a.StatusText = "Отключён";
                    a.StatusDetail = "устройство отключено в диспетчере устройств (код 22)";
                }
                else if (a.ErrorCode != 0)
                {
                    a.StatusText = "Ошибка " + a.ErrorCode;
                    a.StatusDetail = ErrorText(a.ErrorCode);
                }
                else if (a.NetEnabledKnown && !a.NetEnabled)
                {
                    a.StatusText = "Отключён";
                    a.StatusDetail = "адаптер выключен";
                }
                else if (a.NetStatus == 2 || a.Operational == "Up")
                {
                    a.StatusText = "Подключено";
                    a.StatusDetail = "сеть доступна";
                }
                else if (a.NetStatus == 7)
                {
                    a.StatusText = "Нет кабеля";
                    a.StatusDetail = "среда передачи отключена";
                }
                else if (a.NetStatus == 0 || a.NetStatus == 1 || a.NetStatus == 3 || a.NetStatus == 4 || a.NetStatus == 5)
                {
                    a.StatusText = "Не подключено";
                    a.StatusDetail = "нет связи";
                }
                else if (a.Operational == "Down")
                {
                    a.StatusText = "Не подключено";
                }
                else if (!setupApiOk && a.SeenByWmi)
                {
                    a.StatusText = a.NetEnabled ? "Включён" : "Отключён";
                }
                else
                {
                    a.StatusText = a.Present ? "Есть" : "Скрыт";
                }

                if (a.Mac.Length == 0) a.Mac = a.MacPermanent;
                a.Mac = FormatMac(a.Mac);
                a.MacPermanent = FormatMac(a.MacPermanent);
                if (a.IsLoopback) a.StatusDetail = "служебный интерфейс";
            }

            // ---------- 8. Сортировка: сначала работающие, потом остальные ----------
            list.Sort(delegate(AdapterInfo x, AdapterInfo y)
            {
                int sx = Rank(x);
                int sy = Rank(y);
                if (sx != sy) return sx.CompareTo(sy);
                return string.Compare(x.Title, y.Title, StringComparison.CurrentCultureIgnoreCase);
            });
            return list;
        }

        private static int Rank(AdapterInfo a)
        {
            if (a.IsLoopback) return 4;
            if (!a.Present) return 3;
            if (a.StatusText == "Подключено") return 0;
            if (a.StatusText == "Нет кабеля") return 1;
            return 2;
        }

        private static List<string> Clean(List<string> list)
        {
            List<string> res = new List<string>();
            for (int i = 0; i < list.Count; i++)
            {
                string s = list[i].Trim();
                if (s.Length == 0) continue;
                if (s == "0.0.0.0" || s == "255.255.255.255" || s == "::") continue;
                res.Add(s);
            }
            return res;
        }

        private static string ShortGuid(string guid)
        {
            string s = guid.Trim('{', '}');
            if (s.Length > 8) return s.Substring(0, 8) + "…";
            return s;
        }

        /// <summary>Приводит MAC к виду AA:BB:CC:DD:EE:FF.</summary>
        public static string FormatMac(string mac)
        {
            if (string.IsNullOrEmpty(mac)) return "";
            string hex = mac.Replace(":", "").Replace("-", "").Replace(".", "").Trim();
            if (hex.Length == 12)
            {
                bool ok = true;
                for (int i = 0; i < hex.Length; i++)
                {
                    char c = hex[i];
                    bool isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                    if (!isHex) { ok = false; break; }
                }
                if (ok)
                {
                    StringBuilder sb = new StringBuilder(17);
                    for (int i = 0; i < 12; i += 2)
                    {
                        if (i > 0) sb.Append(':');
                        sb.Append(char.ToUpperInvariant(hex[i]));
                        sb.Append(char.ToUpperInvariant(hex[i + 1]));
                    }
                    return sb.ToString();
                }
            }
            return mac;
        }

        public static string NormGuid(string g)        {
            if (g == null) return "";
            g = g.Trim();
            if (g.Length == 0) return "";
            if (g[0] != '{') g = "{" + g;
            if (g[g.Length - 1] != '}') g = g + "}";
            return g.ToUpperInvariant();
        }

        private static string LastSegment(string driverKey)
        {
            if (string.IsNullOrEmpty(driverKey)) return "";
            int i = driverKey.LastIndexOf('\\');
            if (i < 0) return driverKey;
            return driverKey.Substring(i + 1);
        }

        private static string FormatSpeed(object speed)
        {
            try
            {
                decimal bits = Convert.ToDecimal(speed);
                if (bits <= 0) return "";
                decimal gb = bits / 1000000000m;
                if (gb >= 1m) return decimal.Round(gb, gb < 10m ? 1 : 0) + " Гбит/с";
                decimal mb = bits / 1000000m;
                if (mb >= 1m) return decimal.Round(mb, 0) + " Мбит/с";
                return decimal.Round(bits / 1000m, 0) + " Кбит/с";
            }
            catch { return ""; }
        }

        public static string ErrorText(int code)
        {
            switch (code)
            {
                case 0: return "устройство работает нормально";
                case 1: return "устройство не настроено правильно";
                case 3: return "повреждён драйвер или в системе мало памяти";
                case 10: return "устройство не может быть запущено";
                case 12: return "недостаточно свободных ресурсов";
                case 14: return "требуется перезагрузка";
                case 18: return "требуется переустановка драйверов";
                case 19: return "неправильная запись в реестре";
                case 21: return "система находится в процессе удаления устройства";
                case 22: return "устройство отключено";
                case 24: return "устройство отсутствует, работает или не установлено";
                case 28: return "драйверы не установлены";
                case 31: return "Windows не удалось загрузить драйверы";
                case 32: return "драйвер отключён";
                case 37: return "драйверу не удалось инициализироваться";
                case 39: return "драйвер повреждён или отсутствует";
                case 43: return "драйвер остановлен из-за ошибок";
                case 45: return "устройство в данный момент не подключено к компьютеру";
                case 52: return "не удалось проверить подпись драйвера";
                default: return "код ошибки " + code;
            }
        }

        private static string FormatDriverDate(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            // вид: 6-21-2006 или 06/21/2006
            try
            {
                string[] p = raw.Split(new char[] { '-', '/', '.' });
                if (p.Length == 3)
                {
                    int mon = int.Parse(p[0]);
                    int day = int.Parse(p[1]);
                    int year = int.Parse(p[2]);
                    if (year < 100) year += 2000;
                    return new DateTime(year, mon, day).ToString("dd.MM.yyyy");
                }
            }
            catch { }
            return raw;
        }
    }
}
