using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Defaults4Me
{
    static class Native
    {
        static readonly UIntPtr HKCU = (UIntPtr)0x80000001UL;

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        static extern int RegOpenKeyExW(UIntPtr hKey, string sub, int opt, int sam, out UIntPtr res);
        [DllImport("advapi32.dll")]
        static extern int RegQueryInfoKeyW(UIntPtr h, IntPtr c, IntPtr cc, IntPtr r, IntPtr sk,
            IntPtr msk, IntPtr mc, IntPtr v, IntPtr mvn, IntPtr mvl, IntPtr sd, out long ft);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        static extern int RegDeleteKeyW(UIntPtr hKey, string sub);
        [DllImport("advapi32.dll")]
        static extern int RegCloseKey(UIntPtr h);
        [DllImport("shell32.dll")]
        static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        static extern int AssocQueryStringW(int flags, int str, string assoc, string extra, StringBuilder outBuf, ref uint outLen);
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        static extern int SHLoadIndirectString(string src, StringBuilder outBuf, int cap, IntPtr reserved);
        [DllImport("kernel32.dll")]
        public static extern bool AttachConsole(int pid);

        /// <summary>FILETIME (100 ns ticks) of an HKCU key, or -1.</summary>
        public static long LastWriteTime(string subKey)
        {
            UIntPtr h;
            if (RegOpenKeyExW(HKCU, subKey, 0, 0x20019, out h) != 0) return -1;
            long ft;
            int rc = RegQueryInfoKeyW(h, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, out ft);
            RegCloseKey(h);
            return rc == 0 ? ft : -1;
        }

        /// <summary>Deletes an HKCU key without subkeys; returns the Win32 result (0 = ok, 2 = missing, 5 = denied).</summary>
        public static int DeleteKey(string subKey) { return RegDeleteKeyW(HKCU, subKey); }

        public static void NotifyAssocChanged() { SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); }

        /// <summary>The ProgId Windows really uses for an extension (honours the hash check).</summary>
        public static string EffectiveProgId(string ext)
        {
            const int ASSOCSTR_PROGID = 20;
            uint len = 512;
            var sb = new StringBuilder((int)len);
            return AssocQueryStringW(0, ASSOCSTR_PROGID, ext, null, sb, ref len) == 0 ? sb.ToString() : "";
        }

        /// <summary>The executable that opens an extension (for display).</summary>
        public static string EffectiveExe(string ext)
        {
            const int ASSOCSTR_EXECUTABLE = 2;
            uint len = 1024;
            var sb = new StringBuilder((int)len);
            return AssocQueryStringW(0, ASSOCSTR_EXECUTABLE, ext, null, sb, ref len) == 0 ? sb.ToString() : "";
        }

        public static string Indirect(string s)
        {
            if (string.IsNullOrEmpty(s) || s[0] != '@') return s;
            var sb = new StringBuilder(1024);
            return SHLoadIndirectString(s, sb, sb.Capacity, IntPtr.Zero) == 0 ? sb.ToString() : s;
        }
    }
}
