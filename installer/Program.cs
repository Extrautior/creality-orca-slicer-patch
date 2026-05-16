using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new InstallerForm(args));
    }
}

internal sealed class InstallerForm : Form
{
    private readonly string[] args;
    private readonly TextBox installDirBox = new();
    private readonly TextBox logBox = new();
    private readonly Button installButton = new();
    private readonly Button browseButton = new();
    private readonly ProgressBar progress = new();
    private readonly CheckBox forceCloseBox = new();
    private readonly CheckBox dryRunBox = new();

    public InstallerForm(string[] args)
    {
        this.args = args;
        Text = "Creality OrcaSlicer Patch Installer";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 520);
        Size = new Size(820, 580);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(248, 248, 248);

        var title = new Label
        {
            Text = "Creality OrcaSlicer Patch",
            Font = new Font("Segoe UI Semibold", 18F),
            AutoSize = true,
            Location = new Point(24, 22)
        };

        var subtitle = new Label
        {
            Text = "Adds Creality CFS support and the Creality device page to an existing OrcaSlicer install.",
            AutoSize = true,
            ForeColor = Color.FromArgb(80, 80, 80),
            Location = new Point(27, 62)
        };

        var dirLabel = new Label
        {
            Text = "OrcaSlicer install folder",
            AutoSize = true,
            Location = new Point(27, 104)
        };

        installDirBox.Location = new Point(30, 128);
        installDirBox.Width = 610;
        installDirBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        installDirBox.Text = FindDefaultInstallDir();

        browseButton.Text = "Browse...";
        browseButton.Location = new Point(652, 126);
        browseButton.Width = 120;
        browseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        browseButton.Click += (_, _) => BrowseInstallDir();

        forceCloseBox.Text = "Close OrcaSlicer if it is running";
        forceCloseBox.Location = new Point(30, 166);
        forceCloseBox.AutoSize = true;

        dryRunBox.Text = "Dry run only";
        dryRunBox.Location = new Point(260, 166);
        dryRunBox.AutoSize = true;
        dryRunBox.Visible = args.Any(a => a.Equals("--debug", StringComparison.OrdinalIgnoreCase));

        progress.Location = new Point(30, 204);
        progress.Width = 742;
        progress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        progress.Style = ProgressBarStyle.Continuous;

        logBox.Location = new Point(30, 234);
        logBox.Size = new Size(742, 240);
        logBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        logBox.Multiline = true;
        logBox.ReadOnly = true;
        logBox.ScrollBars = ScrollBars.Vertical;
        logBox.BackColor = Color.White;
        logBox.Font = new Font("Consolas", 9F);

        installButton.Text = "Install Patch";
        installButton.Location = new Point(632, 492);
        installButton.Width = 140;
        installButton.Height = 34;
        installButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        installButton.Click += async (_, _) => await InstallAsync();

        var closeButton = new Button
        {
            Text = "Close",
            Location = new Point(480, 492),
            Width = 140,
            Height = 34,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        closeButton.Click += (_, _) => Close();

        Controls.AddRange(new Control[]
        {
            title, subtitle, dirLabel, installDirBox, browseButton, forceCloseBox, dryRunBox,
            progress, logBox, installButton, closeButton
        });
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static string FindDefaultInstallDir()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "OrcaSlicer"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "OrcaSlicer")
        };

        return candidates.FirstOrDefault(IsOrcaInstallDir) ?? candidates[0];
    }

    private static bool IsOrcaInstallDir(string path)
    {
        return Directory.Exists(path) &&
               (File.Exists(Path.Combine(path, "orca-slicer.exe")) ||
                File.Exists(Path.Combine(path, "OrcaSlicer.dll")) ||
                Directory.Exists(Path.Combine(path, "resources")));
    }

    private void BrowseInstallDir()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the OrcaSlicer install folder",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(installDirBox.Text) ? installDirBox.Text : FindDefaultInstallDir()
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
            installDirBox.Text = dialog.SelectedPath;
    }

    private async Task InstallAsync()
    {
        var installDir = installDirBox.Text.Trim();
        if (!IsOrcaInstallDir(installDir))
        {
            MessageBox.Show(this, "That folder does not look like an OrcaSlicer install.", "Check install folder",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        installButton.Enabled = false;
        browseButton.Enabled = false;
        progress.Style = ProgressBarStyle.Marquee;
        logBox.Clear();
        AppendLog("Preparing patch payload...");

        try
        {
            var tempRoot = await Task.Run(ExtractPayload);
            await InstallPayloadAsync(tempRoot, installDir);

            SetProgressComplete();
            AppendLog("");
            AppendLog("Done.");
            MessageBox.Show(this, "Patch installed successfully.", "Creality OrcaSlicer Patch",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            progress.Style = ProgressBarStyle.Continuous;
            progress.Value = 0;
            AppendLog(ex.ToString());
            MessageBox.Show(this, ex.Message, "Creality OrcaSlicer Patch", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            installButton.Enabled = true;
            browseButton.Enabled = true;
        }
    }

    private static string ExtractPayload()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var payloadResource = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("creality-orca-patcher-2.3.2-stable-release.zip", StringComparison.OrdinalIgnoreCase));

        var tempRoot = Path.Combine(Path.GetTempPath(), "CrealityOrcaPatcher", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(tempRoot);

        var zipPath = Path.Combine(tempRoot, "patcher.zip");
        using (var input = assembly.GetManifestResourceStream(payloadResource)
            ?? throw new InvalidOperationException("The embedded patcher payload is missing."))
        using (var output = File.Create(zipPath))
        {
            input.CopyTo(output);
        }

        ZipFile.ExtractToDirectory(zipPath, tempRoot, overwriteFiles: true);
        return tempRoot;
    }

    private async Task InstallPayloadAsync(string tempRoot, string installDir)
    {
        var payloadRoot = Path.Combine(tempRoot, "payload", "root");
        if (!Directory.Exists(payloadRoot))
            throw new DirectoryNotFoundException("The embedded patch payload is missing its payload\\root folder.");

        var payloadFiles = Directory.GetFiles(payloadRoot, "*", SearchOption.AllDirectories);
        var backupDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CrealityOrcaPatcher",
            "Backups",
            DateTime.Now.ToString("yyyyMMdd-HHmmss"));

        progress.Style = ProgressBarStyle.Continuous;
        progress.Minimum = 0;
        progress.Maximum = Math.Max(payloadFiles.Length + 1, 1);
        progress.Value = 0;

        AppendLog($"Install dir: {installDir}");
        AppendLog($"Payload:     {payloadRoot}");
        AppendLog($"Backup dir:  {backupDir}");
        AppendLog($"Planning {payloadFiles.Length} file overlay(s)");

        StopOrcaIfNeeded(installDir);

        if (dryRunBox.Checked)
        {
            AppendLog("Dry run only. No files will be changed.");
            foreach (var payloadFile in payloadFiles)
            {
                AppendLog("Would copy: " + Path.GetRelativePath(payloadRoot, payloadFile).Replace('\\', '/'));
                StepProgress();
            }
            SetProgressComplete();
            return;
        }

        AppendLog("Backing up existing files by copying originals");
        foreach (var payloadFile in payloadFiles)
        {
            var relativePath = Path.GetRelativePath(payloadRoot, payloadFile);
            var targetPath = Path.Combine(installDir, relativePath);
            if (File.Exists(targetPath))
            {
                var backupPath = Path.Combine(backupDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                File.Copy(targetPath, backupPath, overwrite: true);
            }
            StepProgress();
        }

        AppendLog("Installing patch payload with robocopy");
        var exitCode = await RunRobocopyAsync(payloadRoot, installDir);
        if (exitCode > 7)
            throw new InvalidOperationException($"Robocopy failed with exit code {exitCode}.");

        StepProgress();
        SetProgressComplete();
    }

    private void StopOrcaIfNeeded(string installDir)
    {
        var installFullPath = Path.GetFullPath(installDir).TrimEnd('\\', '/');
        var running = Process.GetProcesses()
            .Where(process =>
            {
                try
                {
                    var path = process.MainModule?.FileName;
                    return path is not null &&
                           path.StartsWith(installFullPath, StringComparison.OrdinalIgnoreCase) &&
                           (process.ProcessName.Equals("orca-slicer", StringComparison.OrdinalIgnoreCase) ||
                            process.ProcessName.Equals("OrcaSlicer", StringComparison.OrdinalIgnoreCase));
                }
                catch
                {
                    return false;
                }
            })
            .ToArray();

        if (running.Length == 0)
            return;

        if (!forceCloseBox.Checked)
            throw new InvalidOperationException("OrcaSlicer is running. Close it first, or check the close option in the installer.");

        AppendLog("Closing running OrcaSlicer process(es)");
        foreach (var process in running)
        {
            using (process)
            {
                process.Kill();
                process.WaitForExit(10000);
            }
        }
    }

    private Task<int> RunRobocopyAsync(string sourceDir, string targetDir)
    {
        var completion = new TaskCompletionSource<int>();
        var arguments = $"{Quote(sourceDir)} {Quote(targetDir)} /E /COPY:DAT /DCOPY:DAT /R:2 /W:1 /MT:8 /NP";
        var process = new Process
        {
            StartInfo = new ProcessStartInfo("robocopy.exe", arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) AppendLog(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) AppendLog(e.Data); };
        process.Exited += (_, _) =>
        {
            var exitCode = process.ExitCode;
            process.Dispose();
            completion.TrySetResult(exitCode);
        };

        if (!process.Start())
            throw new InvalidOperationException("Failed to start robocopy.");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return completion.Task;
    }

    private void StepProgress()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(StepProgress));
            return;
        }

        if (progress.Value < progress.Maximum)
            progress.Value += 1;
    }

    private void SetProgressComplete()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(SetProgressComplete));
            return;
        }

        progress.Style = ProgressBarStyle.Continuous;
        progress.Minimum = 0;
        progress.Maximum = 100;
        progress.Value = 100;
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
