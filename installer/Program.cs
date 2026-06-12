using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var options = CommandOptions.Parse(args);
        if (options.Operation is not null)
            return RunCommand(options);

        Application.Run(new InstallerForm());
        return 0;
    }

    private static int RunCommand(CommandOptions options)
    {
        var logLines = new List<string>();
        void Log(string line)
        {
            logLines.Add(line);
            Console.WriteLine(line);
        }

        try
        {
            var installDir = options.InstallDir ?? PatchEngine.FindDefaultInstallDir();
            if (NeedsElevation(installDir) && !options.NoElevate)
                return RelaunchElevated(options);

            using var bundle = PayloadBundle.Open(Log);
            var dataRoot = options.DataRoot ?? PatchEngine.DefaultDataRoot;
            var result = options.Operation switch
            {
                "install" => PatchEngine.Install(bundle, installDir, dataRoot, options.ForceClose, options.DryRun, Log),
                "restore" => PatchEngine.Restore(bundle, installDir, dataRoot, options.ForceClose, Log),
                "verify" => PatchEngine.Verify(bundle, installDir, Log),
                _ => throw new ArgumentException($"Unknown operation: {options.Operation}")
            };
            Log(result);
            WriteLog(options.LogFile, logLines);
            if (options.ShowResult)
                MessageBox.Show(result, "Creality OrcaSlicer Patch", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }
        catch (Exception ex)
        {
            Log("ERROR: " + ex);
            WriteLog(options.LogFile, logLines);
            if (options.ShowResult)
                MessageBox.Show(ex.Message, "Creality OrcaSlicer Patch", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    internal static bool NeedsElevation(string installDir)
    {
        if (IsAdministrator())
            return false;
        var full = Path.GetFullPath(installDir);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return full.StartsWith(programFiles + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               (!string.IsNullOrEmpty(programFilesX86) &&
                full.StartsWith(programFilesX86 + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    internal static int RelaunchElevated(CommandOptions options)
    {
        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not locate the installer executable.");
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = true,
            Verb = "runas",
            Arguments = options.ToArgumentString(includeNoElevate: true)
        };
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the elevated installer.");
        process.WaitForExit();
        return process.ExitCode;
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void WriteLog(string? logFile, IEnumerable<string> lines)
    {
        if (string.IsNullOrWhiteSpace(logFile))
            return;
        var full = Path.GetFullPath(logFile);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllLines(full, lines);
    }
}

internal sealed class InstallerForm : Form
{
    private readonly TextBox installDirBox = new();
    private readonly TextBox logBox = new();
    private readonly Button installButton = new();
    private readonly Button restoreButton = new();
    private readonly Button browseButton = new();
    private readonly ProgressBar progress = new();
    private readonly CheckBox forceCloseBox = new();

    public InstallerForm()
    {
        Text = "Creality OrcaSlicer 2.4.0-beta Patch";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(780, 550);
        Size = new Size(860, 610);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(248, 248, 248);

        var title = new Label
        {
            Text = "Creality Hi / CFS patch for OrcaSlicer 2.4.0-beta",
            Font = new Font("Segoe UI Semibold", 18F),
            AutoSize = true,
            Location = new Point(24, 20)
        };
        var subtitle = new Label
        {
            Text = "Exact-build verification, transactional backup, Creality device page, and rollback.",
            AutoSize = true,
            ForeColor = Color.FromArgb(80, 80, 80),
            Location = new Point(27, 60)
        };
        var versionNote = new Label
        {
            Text = "Supported build: OrcaSlicer 2.4.0-beta, commit fc9a8aa9 only.",
            AutoSize = true,
            ForeColor = Color.FromArgb(140, 60, 20),
            Location = new Point(27, 86)
        };
        var dirLabel = new Label
        {
            Text = "OrcaSlicer install folder",
            AutoSize = true,
            Location = new Point(27, 122)
        };

        installDirBox.Location = new Point(30, 146);
        installDirBox.Width = 650;
        installDirBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        installDirBox.Text = PatchEngine.FindDefaultInstallDir();

        browseButton.Text = "Browse...";
        browseButton.Location = new Point(692, 144);
        browseButton.Width = 120;
        browseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        browseButton.Click += (_, _) => Browse();

        forceCloseBox.Text = "Close OrcaSlicer automatically if it is running";
        forceCloseBox.Location = new Point(30, 184);
        forceCloseBox.AutoSize = true;
        forceCloseBox.Checked = true;

        progress.Location = new Point(30, 218);
        progress.Width = 782;
        progress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        progress.Style = ProgressBarStyle.Continuous;

        logBox.Location = new Point(30, 250);
        logBox.Size = new Size(782, 250);
        logBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        logBox.Multiline = true;
        logBox.ReadOnly = true;
        logBox.ScrollBars = ScrollBars.Vertical;
        logBox.BackColor = Color.White;
        logBox.Font = new Font("Consolas", 9F);

        installButton.Text = "Install / Verify";
        installButton.Location = new Point(662, 520);
        installButton.Size = new Size(150, 36);
        installButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        installButton.Click += async (_, _) => await RunAsync("install");

        restoreButton.Text = "Restore Backup";
        restoreButton.Location = new Point(500, 520);
        restoreButton.Size = new Size(150, 36);
        restoreButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        restoreButton.Click += async (_, _) => await RunAsync("restore");

        var closeButton = new Button
        {
            Text = "Close",
            Location = new Point(338, 520),
            Size = new Size(150, 36),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        closeButton.Click += (_, _) => Close();

        Controls.AddRange([
            title, subtitle, versionNote, dirLabel, installDirBox, browseButton, forceCloseBox,
            progress, logBox, closeButton, restoreButton, installButton
        ]);
    }

    private void Browse()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the OrcaSlicer 2.4.0-beta install folder",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(installDirBox.Text)
                ? installDirBox.Text
                : PatchEngine.FindDefaultInstallDir()
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            installDirBox.Text = dialog.SelectedPath;
    }

    private async Task RunAsync(string operation)
    {
        var installDir = installDirBox.Text.Trim();
        ToggleUi(false);
        logBox.Clear();
        progress.Style = ProgressBarStyle.Marquee;

        try
        {
            if (Program.NeedsElevation(installDir))
            {
                AppendLog("Requesting administrator permission...");
                var options = new CommandOptions
                {
                    Operation = operation,
                    InstallDir = installDir,
                    ForceClose = forceCloseBox.Checked,
                    ShowResult = true
                };
                var exitCode = await Task.Run(() => Program.RelaunchElevated(options));
                if (exitCode != 0)
                    throw new InvalidOperationException("The elevated patch operation did not complete successfully.");
                AppendLog("Elevated operation completed.");
            }
            else
            {
                var result = await Task.Run(() =>
                {
                    using var bundle = PayloadBundle.Open(AppendLog);
                    return operation == "restore"
                        ? PatchEngine.Restore(bundle, installDir, PatchEngine.DefaultDataRoot, forceCloseBox.Checked, AppendLog)
                        : PatchEngine.Install(bundle, installDir, PatchEngine.DefaultDataRoot, forceCloseBox.Checked, false, AppendLog);
                });
                AppendLog(result);
                MessageBox.Show(this, result, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            progress.Style = ProgressBarStyle.Continuous;
            progress.Value = 100;
        }
        catch (Exception ex)
        {
            progress.Style = ProgressBarStyle.Continuous;
            progress.Value = 0;
            AppendLog("ERROR: " + ex);
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            ToggleUi(true);
        }
    }

    private void ToggleUi(bool enabled)
    {
        installButton.Enabled = enabled;
        restoreButton.Enabled = enabled;
        browseButton.Enabled = enabled;
        installDirBox.Enabled = enabled;
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(AppendLog), message);
            return;
        }
        logBox.AppendText(message + Environment.NewLine);
    }
}

internal sealed class CommandOptions
{
    public string? Operation { get; set; }
    public string? InstallDir { get; set; }
    public string? DataRoot { get; set; }
    public string? LogFile { get; set; }
    public bool ForceClose { get; set; }
    public bool DryRun { get; set; }
    public bool ShowResult { get; set; }
    public bool NoElevate { get; set; }

    public static CommandOptions Parse(string[] args)
    {
        var options = new CommandOptions();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--install": options.Operation = "install"; break;
                case "--restore": options.Operation = "restore"; break;
                case "--verify": options.Operation = "verify"; break;
                case "--install-dir": options.InstallDir = RequireValue(args, ref i); break;
                case "--data-root": options.DataRoot = RequireValue(args, ref i); break;
                case "--log-file": options.LogFile = RequireValue(args, ref i); break;
                case "--force-close": options.ForceClose = true; break;
                case "--dry-run": options.DryRun = true; break;
                case "--show-result": options.ShowResult = true; break;
                case "--no-elevate": options.NoElevate = true; break;
                default: throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }
        return options;
    }

    public string ToArgumentString(bool includeNoElevate)
    {
        var args = new List<string>();
        if (Operation is not null) args.Add("--" + Operation);
        AddValue(args, "--install-dir", InstallDir);
        AddValue(args, "--data-root", DataRoot);
        AddValue(args, "--log-file", LogFile);
        if (ForceClose) args.Add("--force-close");
        if (DryRun) args.Add("--dry-run");
        if (ShowResult) args.Add("--show-result");
        if (includeNoElevate || NoElevate) args.Add("--no-elevate");
        return string.Join(" ", args);
    }

    private static string RequireValue(string[] args, ref int index)
    {
        if (++index >= args.Length)
            throw new ArgumentException($"Missing value after {args[index - 1]}");
        return args[index];
    }

    private static void AddValue(List<string> args, string name, string? value)
    {
        if (value is null)
            return;
        args.Add(name);
        args.Add(Quote(value));
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}
