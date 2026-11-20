using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Defaults4Me
{
    static class Program
    {
        public const string Version = "1.0.1";

        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length == 1 && args[0] == "--ucpd-off") return Ucpd.Switch(false);
            if (args.Length == 1 && args[0] == "--ucpd-on") return Ucpd.Switch(true);
            if (args.Length == 0 || (args.Length == 2 && args[0] == "--file"))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm(args.Length == 2 ? args[1] : null));
                return 0;
            }
            Native.AttachConsole(-1);
            try { return Cli(args); }
            catch (Exception e) { Console.Error.WriteLine("Error: " + e.Message); return 1; }
        }

        static readonly Dictionary<string, string> GroupAlias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "images", "images" }, { "bilder", "images" }, { "image", "images" }, { "bild", "images" },
            { "videos", "videos" }, { "video", "videos" },
            { "music", "music" }, { "musik", "music" }, { "audio", "music" },
            { "documents", "documents" }, { "dokumente", "documents" }, { "docs", "documents" },
            { "archives", "archives" }, { "archive", "archives" }, { "archiv", "archives" },
            { "text", "text" }, { "code", "text" },
            { "other", "other" }, { "sonstige", "other" },
        };

        static void Print(IEnumerable<Row> rows)
        {
            foreach (var r in rows)
            {
                string res = r.Result == null ? (r.Already ? "  " + Lang.T("state.same") : "") : "  -> " + Lang.T("result." + r.Result.ToString().ToLowerInvariant());
                Console.WriteLine("  {0,-8} {1,-10} {2,-28} {3}{4}", r.Ext, Lang.T("group." + (r.Group ?? "other")), Trunc(r.Now ?? "", 28), r.Target, res);
            }
        }

        static string Trunc(string s, int n) { return s.Length <= n ? s : s.Substring(0, n - 1) + "…"; }

        static int Summary(List<Row> rows)
        {
            int ok = rows.Count(r => r.Result == SetResult.Ok), bad = rows.Count(r => r.Result != null && r.Result != SetResult.Ok);
            Console.WriteLine(Lang.T("cli.summary"), ok, bad);
            if (rows.Any(r => r.Result == SetResult.Blocked)) Console.WriteLine(Lang.T("cli.blocked"));
            return bad == 0 ? 0 : 2;
        }

        static int Cli(string[] args)
        {
            string cmd = args[0].TrimStart('-', '/').ToLowerInvariant();
            var rest = args.Skip(1).ToList();
            switch (cmd)
            {
                case "list": case "liste":
                    foreach (var a in Apps.Load()) Console.WriteLine("  {0}{1,-34} {2,4}  [{3}]", a.IsNew ? "* " : "  ", a.Name, a.Types.Count, a.Key);
                    return 0;

                case "check": case "pruefe": case "prüfe":
                {
                    int ok, total; Assoc.SelfCheck(out ok, out total);
                    Console.WriteLine("Defaults4Me " + Version);
                    Console.WriteLine(Lang.T("cli.scheme"), Assoc.SchemeVersion);
                    Console.WriteLine(Lang.T("cli.selfcheck"), ok, total);
                    Console.WriteLine(Lang.T("cli.ucpd"), Ucpd.Running ? Lang.T("ucpd.on") : Lang.T("ucpd.off"));
                    string p = Assoc.Problem();
                    Console.WriteLine(p ?? Lang.T("cli.ready"));
                    return p == null ? 0 : 3;
                }

                case "show": case "zeige": case "set": case "setze":
                {
                    if (rest.Count == 0) { Help(); return 1; }
                    bool apply = cmd.StartsWith("set");
                    var apps = Apps.Load();
                    var app = Apps.Find(apps, rest[0]);
                    if (app == null) { Console.Error.WriteLine(Lang.T("cli.noapp"), rest[0]); return 1; }
                    var groups = new HashSet<string>();
                    foreach (var g in rest.Skip(1).SelectMany(x => x.Split(',')).Where(x => x.Length > 0 && !x.Equals("all", StringComparison.OrdinalIgnoreCase) && !x.Equals("alle", StringComparison.OrdinalIgnoreCase)))
                    {
                        string n; if (!GroupAlias.TryGetValue(g, out n)) { Console.Error.WriteLine(Lang.T("cli.nogroup"), g); return 1; }
                        groups.Add(n);
                    }
                    var rows = Engine.Plan(app, groups, apps);
                    Console.WriteLine(app.Name + ": " + rows.Count + " " + Lang.T("cli.types"));
                    if (apply)
                    {
                        string problem = Assoc.Problem();
                        if (problem != null) { Console.Error.WriteLine(problem); return 3; }
                        Engine.Apply(rows, app.Name);
                    }
                    Print(rows);
                    return apply ? Summary(rows) : 0;
                }

                case "undo": case "zurueck": case "zurück":
                {
                    var rows = Engine.Undo();
                    if (rows == null) { Console.WriteLine(Lang.T("undo.none")); return 0; }
                    Print(rows);
                    return Summary(rows);
                }

                case "export":
                    if (rest.Count == 0) { Help(); return 1; }
                    Console.WriteLine(Lang.T("cli.exported"), Engine.ExportProfile(rest[0]), rest[0]);
                    return 0;

                case "import":
                {
                    if (rest.Count == 0) { Help(); return 1; }
                    string problem = Assoc.Problem();
                    if (problem != null) { Console.Error.WriteLine(problem); return 3; }
                    var rows = Engine.ImportProfile(rest[0]);
                    if (rest.Contains("--all") || rest.Contains("--alle")) rows.ForEach(r => { r.Selected = true; r.Already = false; });
                    Engine.Apply(rows, Path.GetFileName(rest[0]));
                    Print(rows);
                    return Summary(rows);
                }

                default:
                    Help();
                    return cmd == "help" || cmd == "hilfe" || cmd == "?" || cmd == "h" ? 0 : 1;
            }
        }

        static void Help() { Console.WriteLine(Lang.T("cli.help").Replace("{v}", Version)); }
    }
}
