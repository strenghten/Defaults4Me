using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace Defaults4Me
{
    enum SetResult { Ok, Blocked, Rejected, Failed }

    /// <summary>Reads and writes the per-user default app (UserChoice + UserChoiceLatest).</summary>
    static class Assoc
    {
        public const string FileExts = @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts";

        // Field order per machine class: 0 assoc, 1 sid, 2 progid, 3 timestamp, 4 salt, 5 machineId
        // (scheme v1, as read from Windows.Internal.OpenWithHost.dll by PS-SFTA).
        static readonly int[][] Order = { new[] { 4, 0, 3, 5, 2, 1 }, new[] { 4, 3, 0, 1, 5, 2 }, new[] { 1, 3, 4, 0, 5, 2 } };
        static readonly string[] SaltGuid = {
            "{3822B7CA-C2F4-4889-B8CC-4CE39A8FB81C}", "{D185E0A1-E265-4724-AA21-3A17B038D72E}", "{97B6BCF4-C367-4577-95BE-73BD3053A5E0}" };
        const string LegacyFallback = "User Choice set via Windows User Experience {D18B6DD5-6124-4341-9318-804003BAFA0B}";

        static string _sid, _mid, _salt, _legacySalt;
        static bool _tables;

        public static string Sid { get { return _sid ?? (_sid = WindowsIdentity.GetCurrent().User.Value); } }

        public static string MachineId
        {
            get
            {
                if (_mid != null) return _mid;
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\SQMClient"))
                    _mid = ((k == null ? null : k.GetValue("MachineId") as string) ?? "").Trim('{', '}');
                return _mid;
            }
        }

        /// <summary>0 = only legacy UserChoice counts, 1 = UserChoiceLatest v1, &gt;1 = unknown newer scheme.</summary>
        public static int SchemeVersion
        {
            get
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\SystemProtectedUserData\" + Sid + @"\AnyoneRead\AppDefaults"))
                {
                    object v = k == null ? null : k.GetValue("HashVersion");
                    return v is int ? (int)v : 0;
                }
            }
        }

        static string FindInDll(string needle, bool fromStart)
        {
            string sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
            foreach (var dll in new[] { "windows.storage.dll", "shell32.dll" })
            {
                try
                {
                    byte[] b = File.ReadAllBytes(Path.Combine(sys, dll));
                    for (int shift = 0; shift < 2; shift++)
                    {
                        string t = Encoding.Unicode.GetString(b, shift, b.Length - shift);
                        int i = t.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
                        if (i < 0) continue;
                        if (fromStart) { int e = t.IndexOf('}', i); if (e > i) return t.Substring(i, e - i + 1); }
                        int s = t.LastIndexOf('\0', i) + 1, end = t.IndexOf('\0', i);
                        if (end > s) return t.Substring(s, end - s);
                    }
                }
                catch { }
            }
            return null;
        }

        static string Salt
        {
            get
            {
                if (_salt == null && MachineId.Length > 0)
                    _salt = FindInDll(SaltGuid[MachineId[MachineId.Length - 1] % 3], false);
                return _salt;
            }
        }

        static string LegacySalt { get { return _legacySalt ?? (_legacySalt = FindInDll("User Choice set via Windows User Experience", true) ?? LegacyFallback); } }

        static void Tables() { if (!_tables) { UclHash.LoadTables(Defaults4Me.Tables.Blob); _tables = true; } }

        static string Hex(long ft) { return ((uint)(ft >> 32)).ToString("x8") + ((uint)ft).ToString("x8"); }

        public static string LatestHash(string ext, string progId, long ticks)
        {
            Tables();
            string salt = Salt;
            if (salt == null) throw new InvalidOperationException("salt");
            long ft = ticks - ticks % 10000L;
            string[] f = { ext, Sid, progId, Hex(ft), salt, MachineId };
            return UclHash.ComputeHash(string.Concat(Order[MachineId[MachineId.Length - 1] % 3].Select(i => f[i])));
        }

        static string LegacyHash(string ext, string progId, long ticks)
        {
            Tables();
            long minute = ticks - ticks % 600000000L;
            return UclHash.LegacyHash((ext + Sid + progId + Hex(minute) + LegacySalt).ToLowerInvariant());
        }

        /// <summary>Can this program write a valid association on this machine? Reason in German/English via Lang.</summary>
        public static string Problem()
        {
            int v = SchemeVersion;
            if (v > 1) return Lang.T("problem.scheme");
            if (v == 1 && (MachineId.Length == 0 || Salt == null)) return Lang.T("problem.salt");
            if (v == 1 && SelfCheck() == "mismatch") return Lang.T("problem.mismatch");
            return null;
        }

        /// <summary>Recomputes Windows' own existing UserChoiceLatest hashes: "ok", "mismatch" or "none".</summary>
        public static string SelfCheck() { int ok, total; SelfCheck(out ok, out total); return total == 0 ? "none" : ok > 0 ? "ok" : "mismatch"; }

        public static void SelfCheck(out int ok, out int total)
        {
            ok = 0; total = 0;
            if (MachineId.Length == 0 || Salt == null) return;
            using (var root = Registry.CurrentUser.OpenSubKey(FileExts))
            {
                if (root == null) return;
                foreach (var ext in root.GetSubKeyNames())
                {
                    string basePath = FileExts + "\\" + ext + @"\UserChoiceLatest";
                    using (var k = Registry.CurrentUser.OpenSubKey(basePath))
                    using (var p = Registry.CurrentUser.OpenSubKey(basePath + @"\ProgId"))
                    {
                        string hash = k == null ? null : k.GetValue("Hash") as string;
                        string pid = p == null ? null : p.GetValue("ProgId") as string;
                        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(pid)) continue;
                        long t = Native.LastWriteTime(basePath + @"\ProgId");
                        if (t < 0) continue;
                        total++;
                        if (LatestHash(ext, pid, t) == hash) ok++;
                    }
                }
            }
        }

        /// <summary>The user's stored choice (what Settings shows), "" if none.</summary>
        public static string UserChoice(string ext)
        {
            using (var k = Registry.CurrentUser.OpenSubKey(FileExts + "\\" + ext + @"\UserChoiceLatest\ProgId"))
            {
                var v = k == null ? null : k.GetValue("ProgId") as string;
                if (!string.IsNullOrEmpty(v) && SchemeVersion >= 1) return v;
            }
            using (var k = Registry.CurrentUser.OpenSubKey(FileExts + "\\" + ext + @"\UserChoice"))
                return (k == null ? null : k.GetValue("ProgId") as string) ?? "";
        }

        public static IEnumerable<string> UserExtensions()
        {
            using (var root = Registry.CurrentUser.OpenSubKey(FileExts))
                return root == null ? new string[0] : root.GetSubKeyNames().Where(n => n.StartsWith(".")).ToArray();
        }

        /// <summary>Sets ProgId as default for ext. Empty ProgId = remove the user's choice.</summary>
        public static SetResult Set(string ext, string progId)
        {
            string basePath = FileExts + "\\" + ext;
            try
            {
                if (string.IsNullOrEmpty(progId))
                {
                    int a = Native.DeleteKey(basePath + @"\UserChoiceLatest\ProgId");
                    int b = Native.DeleteKey(basePath + @"\UserChoiceLatest");
                    int c = Native.DeleteKey(basePath + @"\UserChoice");
                    Native.NotifyAssocChanged();
                    return (a == 5 || b == 5 || c == 5) ? SetResult.Blocked : SetResult.Ok;
                }
                if (SchemeVersion >= 1)
                {
                    string latest = basePath + @"\UserChoiceLatest";
                    using (var k = Registry.CurrentUser.CreateSubKey(latest + @"\ProgId"))
                        k.SetValue("ProgId", progId, RegistryValueKind.String);
                    long t = Native.LastWriteTime(latest + @"\ProgId");
                    if (t < 0) return SetResult.Failed;
                    string h = LatestHash(ext, progId, t);
                    using (var k = Registry.CurrentUser.CreateSubKey(latest))
                        k.SetValue("Hash", h, RegistryValueKind.String);
                }
                WriteLegacy(basePath + @"\UserChoice", ext, progId);
                using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\ApplicationAssociationToasts"))
                    k.SetValue(progId + "_" + ext, 0, RegistryValueKind.DWord);
            }
            catch (UnauthorizedAccessException) { return SetResult.Blocked; }
            catch (System.Security.SecurityException) { return SetResult.Blocked; }
            catch (Exception) { return SetResult.Failed; }
            Native.NotifyAssocChanged();
            return string.Equals(Native.EffectiveProgId(ext), progId, StringComparison.OrdinalIgnoreCase) ? SetResult.Ok : SetResult.Rejected;
        }

        static void WriteLegacy(string keyPath, string ext, string progId)
        {
            long ft = DateTime.Now.ToFileTime();
            for (int i = 0; i < 3; i++)
            {
                string h = LegacyHash(ext, progId, ft);
                int rc = Native.DeleteKey(keyPath);
                if (rc == 5) { if (SchemeVersion >= 1) return; throw new UnauthorizedAccessException(); }
                try
                {
                    using (var k = Registry.CurrentUser.CreateSubKey(keyPath))
                    { k.SetValue("Hash", h); k.SetValue("ProgId", progId); }
                }
                catch (UnauthorizedAccessException) { if (SchemeVersion >= 1) return; throw; }
                long w = Native.LastWriteTime(keyPath);
                if (w < 0 || w - w % 600000000L == ft - ft % 600000000L) return;
                ft = w;
            }
        }
    }
}
