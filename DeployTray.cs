using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

public sealed class DeployTray : ApplicationContext
{
    public sealed class Config {
        public string Repositories = "";
        public string WorkflowContains = "deploy";
        public int IntervalSeconds = 15;
    }
    public sealed class RunInfo {
        public string name, display_title, html_url, status, head_branch;
        public long id;
        public string repo;
    }
    public sealed class Page { public RunInfo[] workflow_runs; }
    sealed class Indicator : IDisposable {
        public NotifyIcon Tray;
        public Icon Icon;
        public void Dispose() { Tray.Visible = false; Tray.Dispose(); Icon.Dispose(); }
    }
    readonly string configPath;
    readonly Dictionary<string, Indicator> icons = new Dictionary<string, Indicator>();
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer activationTimer = new System.Windows.Forms.Timer();
    readonly EventWaitHandle activation;
    readonly Control dispatcher = new Control();
    readonly NotifyIcon control = new NotifyIcon();
    Config config;
    Icon controlIcon;
    bool busy, closing, settingsOpen;
    int generation;
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);

    public static void Run(string folder) {
        using (var activation = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\DeployTray-gh-activate"))
        using (var mutex = new Mutex(false, "Local\\DeployTray-gh")) {
            if (!mutex.WaitOne(0)) { activation.Set(); return; }
            try { Application.EnableVisualStyles(); Application.Run(new DeployTray(folder, activation)); }
            finally { mutex.ReleaseMutex(); }
        }
    }
    DeployTray(string folder, EventWaitHandle activation) {
        this.activation = activation;
        configPath = Path.Combine(folder, "config.json");
        var handle = dispatcher.Handle;
        config = File.Exists(configPath) ? new JavaScriptSerializer().Deserialize<Config>(File.ReadAllText(configPath)) : new Config();
        if (config == null) config = new Config();
        SetControl("GH", Color.FromArgb(160, 170, 185));
        control.Text = "DeployTray: checking GitHub";
        control.Visible = true;
        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings", null, delegate { Settings(); });
        menu.Items.Add("Refresh", null, delegate { Poll(); });
        menu.Items.Add("Exit", null, delegate { ExitThread(); });
        control.ContextMenuStrip = menu;
        timer.Interval = 15000;
        timer.Tick += delegate { Poll(); };
        activationTimer.Interval = 500;
        activationTimer.Tick += delegate { if (this.activation.WaitOne(0)) Settings(); };
        activationTimer.Start();
        Settings();
        timer.Start();
        Poll();
    }
    void Settings() {
        if (settingsOpen || closing) return;
        settingsOpen = true;
        try {
        using (var form = new Form { Text = "DeployTray settings", Width = 490, Height = 320, StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false }) {
            form.TopMost = true;
            form.Shown += delegate { form.Activate(); };
            form.Controls.Add(new Label { Text = "Repositories: owner/repo, one per line", Left = 15, Top = 15, Width = 445 });
            var repos = new TextBox { Left = 15, Top = 40, Width = 445, Height = 110, Multiline = true, ScrollBars = ScrollBars.Vertical, Text = config.Repositories };
            form.Controls.Add(repos);
            form.Controls.Add(new Label { Text = "Workflow name contains (empty = all workflows)", Left = 15, Top = 160, Width = 445 });
            var filter = new TextBox { Left = 15, Top = 185, Width = 445, Text = config.WorkflowContains };
            form.Controls.Add(filter);
            var save = new Button { Text = "Save", Left = 360, Top = 230, Width = 100 };
            save.Click += delegate {
                foreach (var repo in SplitRepos(repos.Text)) {
                    if (!Regex.IsMatch(repo, @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")) { MessageBox.Show("Use owner/repo: " + repo); return; }
                }
                var next = new Config { Repositories = repos.Text.Trim(), WorkflowContains = filter.Text.Trim(), IntervalSeconds = 15 };
                try { File.WriteAllText(configPath, new JavaScriptSerializer().Serialize(next)); }
                catch (Exception e) { MessageBox.Show("Cannot save settings: " + e.Message); return; }
                config = next;
                generation++;
                foreach (var indicator in icons.Values) indicator.Dispose();
                icons.Clear();
                form.Close();
            };
            form.Controls.Add(save);
            form.AcceptButton = save;
            form.ShowDialog();
        }
        } finally { settingsOpen = false; }
        Poll();
    }
    static string[] SplitRepos(string value) { return (value ?? "").Split(new char[] { '\r', '\n', ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries); }
    static string Gh(string arguments) {
        var info = new ProcessStartInfo("gh.exe", arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.EnvironmentVariables["GH_PROMPT_DISABLED"] = "1";
        using (var process = Process.Start(info)) {
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30000)) { process.Kill(); throw new Exception("GitHub request timed out"); }
            var output = stdout.GetAwaiter().GetResult();
            stderr.GetAwaiter().GetResult();
            if (process.ExitCode != 0) throw new Exception("GitHub CLI failed. Check gh auth status and repository access.");
            return output;
        }
    }
    void Poll() {
        if (busy || closing) return;
        var snapshot = config;
        var version = generation;
        var repositories = SplitRepos(snapshot.Repositories);
        if (repositories.Length == 0) { control.Text = "DeployTray: configure repositories"; control.Visible = true; return; }
        busy = true;
        Task.Run(delegate {
            var runs = new Dictionary<string, RunInfo>();
            string error = null;
            try {
                var json = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
                foreach (var repo in repositories) {
                    if (!Regex.IsMatch(repo, @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")) throw new Exception("Invalid repository in config.json");
                    foreach (var status in new[] { "in_progress", "queued", "waiting", "pending", "requested" }) {
                        var pages = json.Deserialize<Page[]>(Gh("api --hostname github.com --method GET --paginate --slurp \"repos/" + repo + "/actions/runs?status=" + status + "&per_page=100\""));
                        foreach (var page in pages) foreach (var run in page.workflow_runs ?? new RunInfo[0]) {
                            if ((run.name ?? "").IndexOf(snapshot.WorkflowContains ?? "", StringComparison.OrdinalIgnoreCase) < 0) continue;
                            run.repo = repo;
                            runs[repo + ":" + run.id] = run;
                        }
                    }
                }
            } catch (Exception e) { error = e is System.ComponentModel.Win32Exception ? "Install GitHub CLI, then run gh auth login" : e.Message; }
            if (!closing) {
                try { dispatcher.BeginInvoke((Action)delegate {
                    busy = false;
                    if (closing) return;
                    if (version != generation) { Poll(); return; }
                    if (error != null) {
                        SetControl("GH", Color.FromArgb(225, 75, 75));
                        control.Text = Short("DeployTray: " + error, 63);
                        control.Visible = true;
                        // Keep last known runs, clearly mark their status as stale.
                        foreach (var entry in icons.Values) entry.Tray.Text = Short("UNKNOWN: connection lost; last known active", 63);
                        return;
                    }
                    SetControl("GH", Color.FromArgb(160, 170, 185));
                    control.Text = runs.Count == 0 ? "DeployTray: running, no active workflows" : "DeployTray: active workflows: " + runs.Count;
                    control.Visible = true;
                    var obsolete = new List<string>();
                    foreach (var key in icons.Keys) if (!runs.ContainsKey(key)) obsolete.Add(key);
                    foreach (var key in obsolete) { icons[key].Dispose(); icons.Remove(key); }
                    foreach (var pair in runs) {
                        var run = pair.Value;
                        Indicator indicator;
                        if (!icons.TryGetValue(pair.Key, out indicator)) {
                            var icon = MakeIcon(Letters(RunTitle(run)));
                            var tray = new NotifyIcon { Icon = icon, Visible = true };
                            var context = new ContextMenuStrip();
                            var url = run.html_url;
                            context.Items.Add("Open workflow", null, delegate { OpenUrl(url); });
                            context.Items.Add("Settings", null, delegate { Settings(); });
                            context.Items.Add("Exit", null, delegate { ExitThread(); });
                            tray.ContextMenuStrip = context;
                            tray.DoubleClick += delegate { OpenUrl(url); };
                            indicator = new Indicator { Tray = tray, Icon = icon };
                            icons.Add(pair.Key, indicator);
                        }
                        indicator.Tray.Text = Short(RunTitle(run) + " | " + run.repo + " | " + run.status + " | " + run.head_branch, 63);
                    }
                }); } catch (InvalidOperationException) { }
            }
        });
    }
    static string Short(string value, int length) { return value.Length <= length ? value : value.Substring(0, length - 1) + "…"; }
    public static string RunTitle(RunInfo run) {
        return String.IsNullOrWhiteSpace(run.display_title) ? run.name : run.display_title;
    }
    public static string Letters(string name) {
        var clean = Regex.Replace(name ?? "", @"[^\p{L}\p{N}]", "").ToUpperInvariant();
        return clean.Length == 0 ? "RUN" : clean.Substring(0, Math.Min(3, clean.Length));
    }
    void SetControl(string text, Color color) {
        var next = MakeIcon(text, color);
        control.Icon = next;
        if (controlIcon != null) controlIcon.Dispose();
        controlIcon = next;
    }
    static Icon MakeIcon(string text) { return MakeIcon(text, Color.FromArgb(245, 171, 35)); }
    static Icon MakeIcon(string text, Color color) {
        using (var bitmap = new Bitmap(32, 32)) using (var graphics = Graphics.FromImage(bitmap)) using (var font = new Font("Segoe UI", 10, FontStyle.Bold, GraphicsUnit.Pixel)) {
            graphics.Clear(color);
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) graphics.DrawString(text, font, Brushes.Black, new RectangleF(0, 0, 32, 32), format);
            var handle = bitmap.GetHicon();
            try { using (var borrowed = Icon.FromHandle(handle)) return (Icon)borrowed.Clone(); }
            finally { DestroyIcon(handle); }
        }
    }
    static void OpenUrl(string url) {
        Uri uri;
        if (Uri.TryCreate(url, UriKind.Absolute, out uri) && uri.Scheme == "https" && uri.Host == "github.com") {
            try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); } catch { }
        }
    }
    protected override void ExitThreadCore() {
        closing = true;
        timer.Stop(); timer.Dispose();
        activationTimer.Stop(); activationTimer.Dispose();
        foreach (var icon in icons.Values) icon.Dispose();
        control.Visible = false; control.Dispose();
        if (controlIcon != null) controlIcon.Dispose();
        dispatcher.Dispose();
        base.ExitThreadCore();
    }
}
