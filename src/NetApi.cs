using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace NetHelp
{
    public static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AllocConsole();

        public const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVINFO_DATA
    {
        public int cbSize;
        public Guid ClassGuid;
        public int DevInst;
        public IntPtr Reserved;
    }

    /// <summary>Устройство класса «Сетевые адаптеры» по данным PnP (включая отсутствующие/призрачные).</summary>
    public class DevNode
    {
        public string InstanceId = "";
        public string Desc = "";
        public string FriendlyName = "";
        public string DriverKey = "";
        public string Manufacturer = "";
        public string HardwareIds = "";
        public bool Present = false;
    }

    /// <summary>Перечисление сетевых устройств через SetupAPI: видны и удалённые (скрытые) устройства.</summary>
    public static class SetupApi
    {
        public const uint DIGCF_PRESENT = 0x02;
        private const uint SPDRP_DEVICEDESC = 0x00;
        private const uint SPDRP_HARDWAREID = 0x01;
        private const uint SPDRP_DRIVER = 0x09;
        private const uint SPDRP_MFG = 0x0B;
        private const uint SPDRP_FRIENDLYNAME = 0x0C;

        public static readonly Guid NetClass = new Guid("4d36e972-e325-11ce-bfc1-08002be10318");

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, IntPtr Enumerator, IntPtr hwndParent, uint Flags);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(IntPtr DeviceInfoSet, uint MemberIndex, ref SP_DEVINFO_DATA DeviceInfoData);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceRegistryProperty(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, uint Property, out uint PropertyRegDataType, byte[] PropertyBuffer, uint PropertyBufferSize, out uint RequiredSize);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInstanceId(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, StringBuilder DeviceInstanceId, uint DeviceInstanceIdSize, out uint RequiredSize);
        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

        private static string Prop(IntPtr set, ref SP_DEVINFO_DATA did, uint prop)
        {
            uint type;
            uint need;
            byte[] buf = new byte[4096];
            if (!SetupDiGetDeviceRegistryProperty(set, ref did, prop, out type, buf, (uint)buf.Length, out need)) return "";
            if (type == 7) // REG_MULTI_SZ
            {
                string all = Encoding.Unicode.GetString(buf, 0, (int)Math.Min(need, (uint)buf.Length));
                string[] parts = all.Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
                return string.Join("; ", parts);
            }
            return Encoding.Unicode.GetString(buf, 0, (int)Math.Min(need, (uint)buf.Length)).TrimEnd('\0');
        }

        private static HashSet<string> PresentIds()
        {
            HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Guid g = NetClass;
            IntPtr h = SetupDiGetClassDevs(ref g, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT);
            if (h == new IntPtr(-1) || h == IntPtr.Zero) return set;
            try
            {
                for (uint i = 0; ; i++)
                {
                    SP_DEVINFO_DATA did = new SP_DEVINFO_DATA();
                    did.cbSize = Marshal.SizeOf(typeof(SP_DEVINFO_DATA));
                    if (!SetupDiEnumDeviceInfo(h, i, ref did)) break;
                    StringBuilder sb = new StringBuilder(1024);
                    uint need;
                    if (SetupDiGetDeviceInstanceId(h, ref did, sb, (uint)sb.Capacity, out need))
                        set.Add(sb.ToString());
                }
            }
            finally { SetupDiDestroyDeviceInfoList(h); }
            return set;
        }

        public static List<DevNode> Enumerate()
        {
            List<DevNode> list = new List<DevNode>();
            HashSet<string> present = PresentIds();
            Guid g = NetClass;
            IntPtr h = SetupDiGetClassDevs(ref g, IntPtr.Zero, IntPtr.Zero, 0);
            if (h == new IntPtr(-1) || h == IntPtr.Zero) return list;
            try
            {
                for (uint i = 0; ; i++)
                {
                    SP_DEVINFO_DATA did = new SP_DEVINFO_DATA();
                    did.cbSize = Marshal.SizeOf(typeof(SP_DEVINFO_DATA));
                    if (!SetupDiEnumDeviceInfo(h, i, ref did)) break;

                    DevNode d = new DevNode();
                    StringBuilder sb = new StringBuilder(1024);
                    uint need;
                    if (SetupDiGetDeviceInstanceId(h, ref did, sb, (uint)sb.Capacity, out need))
                        d.InstanceId = sb.ToString();
                    d.Desc = Prop(h, ref did, SPDRP_DEVICEDESC);
                    d.FriendlyName = Prop(h, ref did, SPDRP_FRIENDLYNAME);
                    d.DriverKey = Prop(h, ref did, SPDRP_DRIVER);
                    d.Manufacturer = Prop(h, ref did, SPDRP_MFG);
                    d.HardwareIds = Prop(h, ref did, SPDRP_HARDWAREID);
                    d.Present = present.Contains(d.InstanceId);
                    list.Add(d);
                }
            }
            finally { SetupDiDestroyDeviceInfoList(h); }
            return list;
        }
    }

    /// <summary>Чтение ветвей реестра, где Windows хранит сетевые адаптеры и их настройки.</summary>
    public static class Reg
    {
        public const string NetClassPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";
        public const string Tcpip4Path = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
        public const string Tcpip6Path = @"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters\Interfaces";

        public static RegistryKey Machine()
        {
            return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        }

        public static string AsString(object v)
        {
            if (v == null) return "";
            string[] arr = v as string[];
            if (arr != null) return string.Join("; ", arr);
            byte[] b = v as byte[];
            if (b != null)
            {
                StringBuilder sb = new StringBuilder(b.Length * 2);
                for (int i = 0; i < b.Length; i++) sb.Append(b[i].ToString("x2"));
                return sb.ToString();
            }
            return Convert.ToString(v);
        }

        /// <summary>Все подразделы класса сетевых адаптеров (0000, 0001, ...), включая остатки удалённых.</summary>
        public static Dictionary<string, Dictionary<string, string>> ReadClassEntries()
        {
            Dictionary<string, Dictionary<string, string>> result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (RegistryKey baseKey = Machine())
                using (RegistryKey k = baseKey.OpenSubKey(NetClassPath))
                {
                    if (k == null) return result;
                    string[] subs = k.GetSubKeyNames();
                    for (int i = 0; i < subs.Length; i++)
                    {
                        string name = subs[i];
                        if (name.Length != 4) continue;
                        int n;
                        if (!int.TryParse(name, out n)) continue;
                        using (RegistryKey sub = k.OpenSubKey(name))
                        {
                            if (sub == null) continue;
                            Dictionary<string, string> vals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            string[] names = sub.GetValueNames();
                            for (int j = 0; j < names.Length; j++)
                            {
                                try { vals[names[j]] = AsString(sub.GetValue(names[j])); }
                                catch { }
                            }
                            result[name] = vals;
                        }
                    }
                }
            }
            catch { }
            return result;
        }

        /// <summary>Имена подключений (в том числе у скрытых адаптеров) из ветки Network.</summary>
        public static Dictionary<string, string> ReadConnectionNames()
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            const string path = @"SYSTEM\CurrentControlSet\Control\Network\{4d36e972-e325-11ce-bfc1-08002be10318}";
            try
            {
                using (RegistryKey baseKey = Machine())
                using (RegistryKey k = baseKey.OpenSubKey(path))
                {
                    if (k == null) return result;
                    string[] subs = k.GetSubKeyNames();
                    for (int i = 0; i < subs.Length; i++)
                    {
                        using (RegistryKey sub = k.OpenSubKey(subs[i] + @"\Connection"))
                        {
                            if (sub == null) continue;
                            object v = sub.GetValue("Name");
                            if (v != null) result[subs[i]] = Convert.ToString(v);
                        }
                    }
                }
            }
            catch { }
            return result;
        }

        public static Dictionary<string, Dictionary<string, string>> ReadInterfaceKeys(string path)
        {
            Dictionary<string, Dictionary<string, string>> result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (RegistryKey baseKey = Machine())
                using (RegistryKey k = baseKey.OpenSubKey(path))
                {
                    if (k == null) return result;
                    string[] subs = k.GetSubKeyNames();
                    for (int i = 0; i < subs.Length; i++)
                    {
                        using (RegistryKey sub = k.OpenSubKey(subs[i]))
                        {
                            if (sub == null) continue;
                            Dictionary<string, string> vals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            string[] names = sub.GetValueNames();
                            for (int j = 0; j < names.Length; j++)
                            {
                                try { vals[names[j]] = AsString(sub.GetValue(names[j])); }
                                catch { }
                            }
                            result[subs[i]] = vals;
                        }
                    }
                }
            }
            catch { }
            return result;
        }

        public static string V(Dictionary<string, string> d, string key)
        {
            if (d == null) return "";
            string v;
            if (d.TryGetValue(key, out v)) return v == null ? "" : v.Trim();
            return "";
        }

        public static int VI(Dictionary<string, string> d, string key, int def)
        {
            int r;
            if (int.TryParse(V(d, key), out r)) return r;
            return def;
        }

        public static List<string> VList(Dictionary<string, string> d, string key)
        {
            List<string> list = new List<string>();
            string v = V(d, key);
            if (v.Length == 0) return list;
            string[] parts = v.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length > 0) list.Add(p);
            }
            return list;
        }

        public static bool SetDword(string path, string name, int value)
        {
            try
            {
                using (RegistryKey baseKey = Machine())
                using (RegistryKey k = baseKey.OpenSubKey(path, true))
                {
                    if (k == null) return false;
                    k.SetValue(name, value, RegistryValueKind.DWord);
                    return true;
                }
            }
            catch { return false; }
        }

        public static bool SetString(string path, string name, string value)
        {
            try
            {
                using (RegistryKey baseKey = Machine())
                using (RegistryKey k = baseKey.OpenSubKey(path, true))
                {
                    if (k == null) return false;
                    if (value == null) k.DeleteValue(name, false);
                    else k.SetValue(name, value, RegistryValueKind.String);
                    return true;
                }
            }
            catch { return false; }
        }
    }

    /// <summary>Тонкая обёртка над WMI.</summary>
    public static class Wmi
    {
        private static bool? _ok;

        public static bool Available
        {
            get
            {
                if (!_ok.HasValue)
                {
                    List<Dictionary<string, object>> t = Query("Win32_NetworkAdapter", new string[] { "Index" });
                    _ok = t.Count > 0;
                }
                return _ok.Value;
            }
        }

        public static List<Dictionary<string, object>> Query(string className, string[] props)
        {
            List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
            try
            {
                ManagementScope scope = new ManagementScope(@"\\.\root\cimv2");
                scope.Options.Timeout = TimeSpan.FromSeconds(20);
                scope.Connect();
                ObjectQuery q = new ObjectQuery("SELECT " + string.Join(",", props) + " FROM " + className);
                EnumerationOptions opt = new EnumerationOptions();
                opt.ReturnImmediately = false;
                opt.Rewindable = false;
                opt.DirectRead = false;
                opt.Timeout = TimeSpan.FromSeconds(20);
                using (ManagementObjectSearcher s = new ManagementObjectSearcher(scope, q, opt))
                using (ManagementObjectCollection col = s.Get())
                {
                    foreach (ManagementBaseObject mo in col)
                    {
                        Dictionary<string, object> d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        foreach (PropertyData pd in mo.Properties) d[pd.Name] = pd.Value;
                        list.Add(d);
                        mo.Dispose();
                    }
                }
            }
            catch { }
            return list;
        }

        public static string S(Dictionary<string, object> d, string key)
        {
            if (d == null) return "";
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return "";
            return Convert.ToString(v).Trim();
        }

        public static int I(Dictionary<string, object> d, string key, int def)
        {
            if (d == null) return def;
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return def;
            try { return Convert.ToInt32(v); }
            catch { return def; }
        }

        public static bool B(Dictionary<string, object> d, string key, bool def)
        {
            if (d == null) return def;
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return def;
            try { return Convert.ToBoolean(v); }
            catch { return def; }
        }

        public static string[] A(Dictionary<string, object> d, string key)
        {
            if (d == null) return new string[0];
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return new string[0];
            string[] arr = v as string[];
            if (arr != null) return arr;
            return new string[] { Convert.ToString(v) };
        }
    }
}
