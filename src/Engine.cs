using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.ServiceProcess;
using Microsoft.Win32;

namespace Defaults4Me
{
    class Row
    {
        public string Ext, Group, Now, Target;
        public bool Selected = true;
        public SetResult? Result;
    }

    static class Engine
    {
        public static List<Row> Plan(App app, ICollection<string> groups, List<App> apps)
        {
            return app.Types.OrderBy(t => t.Key)
                .Select(t => new Row { Ext = t.Key, Target = t.Value, Group = Groups.Of(t.Key) })
                .Where(r => groups == null || groups.Count == 0 || groups.Contains(r.Group))
                .Select(r => { r.Now = Apps.Describe(r.Ext, apps); r.Selected = !string.Equals(Native.EffectiveProgId(r.Ext), r.Target, StringComparison.OrdinalIgnoreCase); return r; })
                .ToList();
        }

        /// <summary>Applies the selected rows; the previous state is saved for undo first.</summary>
        public static void Apply(IEnumerable<Row> rows, string label)
        {
            var todo = rows.Where(r => r.Selected).ToList();
            if (todo.Count == 0) return;
            Store.SaveUndo(label, todo.Select(r => new KeyValuePair<string, string>(r.Ext, Assoc.UserChoice(r.Ext))));
            foreach (var r in todo) r.Result = Assoc.Set(r.Ext, r.Target);
        }

        public static List<Row> Undo()
        {
            var last = Store.LastUndo();
            if (last == null) return null;
            var rows = Store.ReadPairs(last).Select(p => new Row { Ext = p.Key, Target = p.Value, Group = Groups.Of(p.Key) }).ToList();
            foreach (var r in rows) r.Result = Assoc.Set(r.Ext, r.Target);
            File.Delete(last);
            return rows;
        }

        public static List<Row> ImportProfile(string file)
        {
            var rows = Store.ReadPairs(file).Where(p => p.Value.Length > 0)
                .Select(p => new Row { Ext = p.Key, Target = p.Value, Group = Groups.Of(p.Key) }).ToList();
            foreach (var r in rows) r.Selected = !string.Equals(Native.EffectiveProgId(r.Ext), r.Target, StringComparison.OrdinalIgnoreCase);
            return rows;
        }

        public static int ExportProfile(string file)
        {
            var pairs = Assoc.UserExtensions().Select(e => new KeyValuePair<string, string>(e, Assoc.UserChoice(e))).Where(p => p.Value.Length > 0).ToList();
            Store.WritePairs(file, "profile", pairs);
            return pairs.Count;
        }

        public static void OpenSettings(App app)
        {
            string uri = app == null ? "ms-settings:defaultapps" : "ms-settings:defaultapps?registeredAppUser=" + Uri.EscapeDataString(app.Key);
            try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); } catch { }
        }
    }

    /// <summary>The UserChoice Protection Driver (blocks http, https, .pdf).</summary>
    static class Ucpd
    {
        const string Svc = @"SYSTEM\CurrentControlSet\Services\UCPD";

        public static bool Running
        {
            get { try { return ServiceController.GetDevices().Any(d => d.ServiceName.Equals("UCPD", StringComparison.OrdinalIgnoreCase) && d.Status == ServiceControllerStatus.Running); } catch { return false; } }
        }

        public static int Start
        {
            get { using (var k = Registry.LocalMachine.OpenSubKey(Svc)) { object v = k == null ? null : k.GetValue("Start"); return v is int ? (int)v : -1; } }
        }

        public static bool IsAdmin { get { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); } }

        /// <summary>Runs this program elevated with one argument; true if it was started.</summary>
        public static bool RunElevated(string arg)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName, arg) { UseShellExecute = true, Verb = "runas" });
                p.WaitForExit();
                return p.ExitCode == 0;
            }
            catch { return false; }
        }

        /// <summary>Needs admin. on=false: Start=4 and the re-enable task off. on=true: back to Windows' default.</summary>
        public static int Switch(bool on)
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(Svc, true))
                {
                    if (k == null) return 2;
                    if (!on && k.GetValue("Defaults4MeOldStart") == null) k.SetValue("Defaults4MeOldStart", k.GetValue("Start") ?? 1, RegistryValueKind.DWord);
                    object old = k.GetValue("Defaults4MeOldStart");
                    k.SetValue("Start", on ? (old is int ? (int)old : 1) : 4, RegistryValueKind.DWord);
                    if (on) k.DeleteValue("Defaults4MeOldStart", false);
                }
                var t = Process.Start(new ProcessStartInfo("schtasks.exe", "/Change /TN \"\\Microsoft\\Windows\\AppxDeploymentClient\\UCPD velocity\" " + (on ? "/Enable" : "/Disable")) { UseShellExecute = false, CreateNoWindow = true });
                t.WaitForExit();
                return 0;
            }
            catch { return 1; }
        }
    }

    /// <summary>Right-click menu entry and Start menu shortcut.</summary>
    static class Shell
    {
        const string Menu = @"Software\Classes\*\shell\Defaults4Me";
        static string Exe { get { return Process.GetCurrentProcess().MainModule.FileName; } }
        static string Lnk { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Defaults4Me.lnk"); } }

        public static bool MenuOn { get { using (var k = Registry.CurrentUser.OpenSubKey(Menu)) return k != null; } }

        public static void SetMenu(bool on)
        {
            if (!on) { try { Registry.CurrentUser.DeleteSubKeyTree(Menu, false); } catch { } return; }
            using (var k = Registry.CurrentUser.CreateSubKey(Menu))
            {
                k.SetValue("MUIVerb", Lang.T("menu.verb"));
                k.SetValue("Icon", "\"" + Exe + "\",0");
            }
            using (var k = Registry.CurrentUser.CreateSubKey(Menu + @"\command"))
                k.SetValue("", "\"" + Exe + "\" --file \"%1\"");
        }

        public static bool StartMenuOn { get { return File.Exists(Lnk); } }

        public static void SetStartMenu(bool on)
        {
            if (!on) { try { File.Delete(Lnk); } catch { } return; }
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            dynamic sh = Activator.CreateInstance(t);
            dynamic l = sh.CreateShortcut(Lnk);
            l.TargetPath = Exe;
            l.WorkingDirectory = Path.GetDirectoryName(Exe);
            l.Description = Lang.T("app.desc");
            l.Save();
        }
    }
}
