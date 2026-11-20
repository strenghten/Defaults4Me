using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Defaults4Me
{
    class MainForm : Form
    {
        readonly ComboBox appBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 340 };
        readonly FlowLayoutPanel groupPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Dock = DockStyle.Fill };
        readonly Dictionary<string, CheckBox> groupBoxes = new Dictionary<string, CheckBox>();
        readonly ListView list = new ListView { View = View.Details, CheckBoxes = true, FullRowSelect = true, Dock = DockStyle.Fill, HideSelection = false };
        readonly Button applyBtn = new Button { AutoSize = true }, undoBtn = new Button { AutoSize = true }, settingsBtn = new Button { AutoSize = true }, moreBtn = new Button { AutoSize = true };
        readonly Label status = new Label { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 0) };
        readonly ContextMenuStrip more = new ContextMenuStrip();
        List<App> apps;
        List<Row> rows = new List<Row>();
        readonly string fileExt;
        string problem, importLabel;
        int sortCol = 0; bool sortDesc;

        public MainForm(string file)
        {
            if (!string.IsNullOrEmpty(file)) { fileExt = Path.GetExtension(file).ToLowerInvariant(); if (fileExt.Length < 2) fileExt = null; }
            Text = "Defaults4Me";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(860, 600);
            MinimumSize = new Size(640, 420);
            StartPosition = FormStartPosition.CenterScreen;

            var top = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(10, 10, 10, 4) };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.Controls.Add(new Label { Text = Lang.T("ui.app"), AutoSize = true, Padding = new Padding(0, 6, 6, 0) }, 0, 0);
            top.Controls.Add(appBox, 1, 0);
            top.Controls.Add(new Label { Text = Lang.T("ui.groups"), AutoSize = true, Padding = new Padding(0, 6, 6, 0) }, 0, 1);
            top.Controls.Add(groupPanel, 1, 1);
            foreach (var g in Groups.All)
            {
                var cb = new CheckBox { Text = Lang.T("group." + g), AutoSize = true, Checked = true };
                cb.CheckedChanged += (s, e) => Rebuild();
                groupBoxes[g] = cb;
                groupPanel.Controls.Add(cb);
            }

            list.Columns.Add(Lang.T("col.ext"), 90);
            list.Columns.Add(Lang.T("col.group"), 100);
            list.Columns.Add(Lang.T("col.now"), 230);
            list.Columns.Add(Lang.T("col.new"), 220);
            list.Columns.Add(Lang.T("col.status"), 170);
            list.ColumnClick += (s, e) => { sortDesc = sortCol == e.Column && !sortDesc; sortCol = e.Column; Fill(); };
            list.ItemChecked += (s, e) => { var r = e.Item.Tag as Row; if (r != null) r.Selected = e.Item.Checked; UpdateButtons(); };

            var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 5, Padding = new Padding(10, 4, 10, 10) };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 4; i++) bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottom.Controls.Add(status, 0, 0);
            bottom.Controls.Add(moreBtn, 1, 0);
            bottom.Controls.Add(settingsBtn, 2, 0);
            bottom.Controls.Add(undoBtn, 3, 0);
            bottom.Controls.Add(applyBtn, 4, 0);
            applyBtn.Text = Lang.T("ui.apply");
            undoBtn.Text = Lang.T("ui.undo");
            settingsBtn.Text = Lang.T("ui.settings");
            moreBtn.Text = Lang.T("ui.more") + " ▾";
            applyBtn.Click += (s, e) => DoApply();
            undoBtn.Click += (s, e) => DoUndo();
            settingsBtn.Click += (s, e) => Engine.OpenSettings(appBox.SelectedItem as App);
            moreBtn.Click += (s, e) => { BuildMore(); more.Show(moreBtn, new Point(0, moreBtn.Height)); };
            AcceptButton = applyBtn;

            var mid = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 0, 10, 0) };
            mid.Controls.Add(list);
            Controls.Add(mid);
            Controls.Add(bottom);
            Controls.Add(top);

            appBox.SelectedIndexChanged += (s, e) => { importLabel = null; Rebuild(); };
            Load += (s, e) => Init();
        }

        void Init()
        {
            Cursor = Cursors.WaitCursor;
            apps = Apps.Load();
            problem = Assoc.Problem();
            var shown = fileExt == null ? apps : apps.Where(a => a.Types.ContainsKey(fileExt)).ToList();
            if (fileExt != null)
            {
                string g = Groups.Of(fileExt);
                foreach (var kv in groupBoxes) kv.Value.Checked = kv.Key == g;
                Text = "Defaults4Me – " + string.Format(Lang.T("ui.fortype"), Lang.T("group." + g), fileExt);
            }
            appBox.Items.AddRange(shown.Cast<object>().ToArray());
            Cursor = Cursors.Default;
            if (appBox.Items.Count > 0) appBox.SelectedIndex = 0; else Rebuild();
            if (shown.Any(a => a.IsNew)) status.Text = Lang.T("ui.newapps");
        }

        HashSet<string> CheckedGroups() { return new HashSet<string>(groupBoxes.Where(kv => kv.Value.Checked).Select(kv => kv.Key)); }

        void Rebuild()
        {
            var app = appBox.SelectedItem as App;
            if (app == null || apps == null) { rows = new List<Row>(); Fill(); return; }
            var groups = CheckedGroups();
            rows = groups.Count == 0 ? new List<Row>() : Engine.Plan(app, groups, apps);
            Fill();
        }

        static string StatusText(Row r)
        {
            if (r.Result != null) return Lang.T("result." + r.Result.ToString().ToLowerInvariant());
            return r.Selected ? "" : Lang.T("state.same");
        }

        void Fill()
        {
            Func<Row, string>[] keys = { r => r.Ext, r => Lang.T("group." + r.Group), r => r.Now, r => r.Target, StatusText };
            var ordered = sortDesc ? rows.OrderByDescending(keys[sortCol], StringComparer.CurrentCultureIgnoreCase) : rows.OrderBy(keys[sortCol], StringComparer.CurrentCultureIgnoreCase);
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var r in ordered)
            {
                var it = new ListViewItem(new[] { r.Ext, Lang.T("group." + r.Group), r.Now, r.Target, StatusText(r) }) { Tag = r, Checked = r.Selected };
                if (r.Result == SetResult.Ok) it.ForeColor = Color.ForestGreen;
                else if (r.Result != null) it.ForeColor = Color.Firebrick;
                else if (!r.Selected) it.ForeColor = SystemColors.GrayText;
                list.Items.Add(it);
            }
            list.EndUpdate();
            UpdateButtons();
        }

        void UpdateButtons()
        {
            int n = rows.Count(r => r.Selected);
            applyBtn.Text = Lang.T("ui.apply") + (n > 0 ? " (" + n + ")" : "");
            applyBtn.Enabled = n > 0 && problem == null;
            var last = Store.LastUndo();
            undoBtn.Enabled = last != null;
            if (problem != null) status.Text = problem;
            else if (fileExt == null && !(status.Text ?? "").StartsWith("★")) status.Text = string.Format(Lang.T("ui.count"), rows.Count, n);
        }

        void ShowResult(List<Row> done)
        {
            int ok = done.Count(r => r.Result == SetResult.Ok);
            var bad = done.Where(r => r.Result != null && r.Result != SetResult.Ok).ToList();
            Fill();
            status.Text = string.Format(Lang.T("ui.done"), ok, bad.Count);
            if (bad.Count == 0) return;
            string msg = string.Format(Lang.T("ui.failed"), string.Join(", ", bad.Select(r => r.Ext)));
            if (bad.Any(r => r.Result == SetResult.Blocked)) msg += "\n\n" + Lang.T("ui.blockedhint");
            if (MessageBox.Show(this, msg, "Defaults4Me", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                Engine.OpenSettings(appBox.SelectedItem as App);
        }

        void DoApply()
        {
            var app = appBox.SelectedItem as App;
            string label = importLabel ?? (app == null ? "?" : app.Name);
            Cursor = Cursors.WaitCursor;
            var todo = rows.Where(r => r.Selected).ToList();
            Engine.Apply(todo, label);
            foreach (var r in todo) r.Now = Apps.Describe(r.Ext, apps);
            Cursor = Cursors.Default;
            ShowResult(todo);
        }

        void DoUndo()
        {
            var last = Store.LastUndo();
            if (last == null) return;
            if (MessageBox.Show(this, string.Format(Lang.T("undo.ask"), Store.UndoLabel(last)), "Defaults4Me", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            Cursor = Cursors.WaitCursor;
            var done = Engine.Undo();
            Cursor = Cursors.Default;
            importLabel = null;
            Rebuild();
            if (done != null && done.Any(r => r.Result != SetResult.Ok)) ShowResult(done);
            else status.Text = Lang.T("undo.done");
        }

        void BuildMore()
        {
            more.Items.Clear();
            more.Items.Add(Lang.T("more.overview"), null, (s, e) => new OverviewForm(apps).Show(this));
            more.Items.Add(new ToolStripSeparator());
            more.Items.Add(Lang.T("more.export"), null, (s, e) => Export());
            more.Items.Add(Lang.T("more.import"), null, (s, e) => Import());
            more.Items.Add(new ToolStripSeparator());
            more.Items.Add(new ToolStripMenuItem(Lang.T("more.menu"), null, (s, e) => Shell.SetMenu(!Shell.MenuOn)) { Checked = Shell.MenuOn });
            more.Items.Add(new ToolStripMenuItem(Lang.T("more.startmenu"), null, (s, e) => { try { Shell.SetStartMenu(!Shell.StartMenuOn); } catch (Exception ex) { MessageBox.Show(this, ex.Message); } }) { Checked = Shell.StartMenuOn });
            more.Items.Add(new ToolStripSeparator());
            bool on = Ucpd.Running || Ucpd.Start != 4;
            more.Items.Add(on ? Lang.T("more.ucpdoff") : Lang.T("more.ucpdon"), null, (s, e) => ToggleUcpd(on));
            more.Items.Add(new ToolStripSeparator());
            more.Items.Add(Lang.T("more.about"), null, (s, e) => MessageBox.Show(this, Lang.T("about").Replace("{v}", Program.Version), "Defaults4Me"));
        }

        void ToggleUcpd(bool turnOff)
        {
            if (turnOff)
            {
                using (var f = new ExpertDialog()) if (f.ShowDialog(this) != DialogResult.OK) return;
            }
            bool ok = Ucpd.IsAdmin ? Ucpd.Switch(!turnOff) == 0 : Ucpd.RunElevated(turnOff ? "--ucpd-off" : "--ucpd-on");
            MessageBox.Show(this, ok ? Lang.T("ucpd.restart") : Lang.T("ucpd.fail"), "Defaults4Me", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }

        void Export()
        {
            using (var d = new SaveFileDialog { Filter = Lang.T("file.filter"), FileName = "Defaults4Me-" + Environment.MachineName + ".txt" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                status.Text = string.Format(Lang.T("cli.exported"), Engine.ExportProfile(d.FileName), Path.GetFileName(d.FileName));
            }
        }

        void Import()
        {
            using (var d = new OpenFileDialog { Filter = Lang.T("file.filter") })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                List<Row> r;
                try { r = Engine.ImportProfile(d.FileName); } catch (Exception ex) { MessageBox.Show(this, ex.Message); return; }
                foreach (var x in r) x.Now = Apps.Describe(x.Ext, apps);
                rows = r;
                importLabel = Path.GetFileName(d.FileName);
                Text = "Defaults4Me – " + importLabel;
                Fill();
                status.Text = Lang.T("ui.importhint");
            }
        }
    }

    /// <summary>"I know what I'm doing" confirmation for switching off the protection driver.</summary>
    class ExpertDialog : Form
    {
        public ExpertDialog()
        {
            Text = Lang.T("expert.title");
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 260);
            var text = new Label { Text = Lang.T("expert.text"), Dock = DockStyle.Top, Height = 160, Padding = new Padding(12) };
            var cb = new CheckBox { Text = Lang.T("expert.check"), AutoSize = true, Location = new Point(14, 170) };
            var ok = new Button { Text = Lang.T("expert.ok"), DialogResult = DialogResult.OK, Enabled = false, AutoSize = true, Location = new Point(300, 215) };
            var cancel = new Button { Text = Lang.T("expert.cancel"), DialogResult = DialogResult.Cancel, AutoSize = true, Location = new Point(420, 215) };
            cb.CheckedChanged += (s, e) => ok.Enabled = cb.Checked;
            Controls.AddRange(new Control[] { text, cb, ok, cancel });
            CancelButton = cancel;
        }
    }

    /// <summary>All extensions with the app that opens them now; sortable and filterable.</summary>
    class OverviewForm : Form
    {
        readonly ListView list = new ListView { View = View.Details, FullRowSelect = true, Dock = DockStyle.Fill };
        readonly TextBox filter = new TextBox { Dock = DockStyle.Top };
        readonly List<string[]> data;
        int sortCol; bool desc;

        public OverviewForm(List<App> apps)
        {
            Text = "Defaults4Me – " + Lang.T("more.overview");
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(700, 560);
            list.Columns.Add(Lang.T("col.ext"), 100);
            list.Columns.Add(Lang.T("col.group"), 110);
            list.Columns.Add(Lang.T("col.now"), 440);
            var exts = new HashSet<string>(Assoc.UserExtensions().Select(e => e.ToLowerInvariant()));
            foreach (var a in apps) exts.UnionWith(a.Types.Keys);
            data = exts.Select(e => new[] { e, Lang.T("group." + Groups.Of(e)), Apps.Describe(e, apps) }).Where(r => r[2] != "–" || true).ToList();
            filter.TextChanged += (s, e) => Fill();
            list.ColumnClick += (s, e) => { desc = sortCol == e.Column && !desc; sortCol = e.Column; Fill(); };
            var p = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            p.Controls.Add(list);
            p.Controls.Add(filter);
            Controls.Add(p);
            Fill();
        }

        void Fill()
        {
            string f = filter.Text.Trim();
            var rows = data.Where(r => f.Length == 0 || r.Any(c => c.IndexOf(f, StringComparison.CurrentCultureIgnoreCase) >= 0));
            rows = desc ? rows.OrderByDescending(r => r[sortCol], StringComparer.CurrentCultureIgnoreCase) : rows.OrderBy(r => r[sortCol], StringComparer.CurrentCultureIgnoreCase);
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var r in rows) list.Items.Add(new ListViewItem(r));
            list.EndUpdate();
        }
    }
}
