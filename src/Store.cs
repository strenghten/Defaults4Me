using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Defaults4Me
{
    /// <summary>Files under %LOCALAPPDATA%\Defaults4Me: known apps, undo steps. Profiles are plain text (ext TAB ProgId).</summary>
    static class Store
    {
        public static string Dir
        {
            get
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Defaults4Me");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        static string UndoDir { get { string d = Path.Combine(Dir, "undo"); Directory.CreateDirectory(d); return d; } }
        static string KnownFile { get { return Path.Combine(Dir, "known-apps.txt"); } }

        public static HashSet<string> KnownApps()
        {
            try { return new HashSet<string>(File.ReadAllLines(KnownFile, Encoding.UTF8).Where(l => l.Length > 0), StringComparer.OrdinalIgnoreCase); }
            catch { return new HashSet<string>(StringComparer.OrdinalIgnoreCase); }
        }

        public static void SaveKnownApps(IEnumerable<string> keys)
        {
            try { var all = KnownApps(); all.UnionWith(keys); File.WriteAllLines(KnownFile, all.OrderBy(k => k), Encoding.UTF8); } catch { }
        }

        public static void SaveUndo(string label, IEnumerable<KeyValuePair<string, string>> before)
        {
            WritePairs(Path.Combine(UndoDir, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".txt"), label, before);
            foreach (var old in Directory.GetFiles(UndoDir, "*.txt").OrderByDescending(f => f).Skip(20)) File.Delete(old);
        }

        public static string LastUndo() { return Directory.GetFiles(UndoDir, "*.txt").OrderByDescending(f => f).FirstOrDefault(); }

        public static string UndoLabel(string file)
        {
            try { var l = File.ReadLines(file, Encoding.UTF8).FirstOrDefault() ?? ""; return l.StartsWith("# ") ? l.Substring(2) : ""; } catch { return ""; }
        }

        public static void WritePairs(string file, string label, IEnumerable<KeyValuePair<string, string>> pairs)
        {
            var sb = new StringBuilder();
            sb.Append("# ").Append(label).Append("\r\n# Defaults4Me 1 - ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append(" - .ext<TAB>ProgId (empty = Windows default)\r\n");
            foreach (var p in pairs) sb.Append(p.Key).Append('\t').Append(p.Value).Append("\r\n");
            File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
        }

        public static List<KeyValuePair<string, string>> ReadPairs(string file)
        {
            return File.ReadAllLines(file, Encoding.UTF8)
                .Where(l => l.StartsWith("."))
                .Select(l => { var p = l.Split('\t'); return new KeyValuePair<string, string>(p[0].Trim(), p.Length > 1 ? p[1].Trim() : ""); })
                .ToList();
        }
    }
}
