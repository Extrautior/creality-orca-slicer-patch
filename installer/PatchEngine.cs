using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

internal sealed class PayloadManifest
{
    public string PatchId { get; set; } = "";
    public string Name { get; set; } = "";
    public string TargetVersion { get; set; } = "";
    public string TargetCommit { get; set; } = "";
    public Dictionary<string, string> ExpectedCleanCore { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<PayloadFile> Files { get; set; } = [];
}

internal sealed class PayloadFile
{
    public string RelativePath { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
}

internal sealed class BackupManifest
{
    public string PatchId { get; set; } = "";
    public string InstallDir { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public bool PreviousMarkerExisted { get; set; }
    public List<BackupEntry> Entries { get; set; } = [];
}

internal sealed class BackupEntry
{
    public string RelativePath { get; set; } = "";
    public bool Existed { get; set; }
    public string? OriginalSha256 { get; set; }
    public string PatchedSha256 { get; set; } = "";
}

internal sealed class PatchMarker
{
    public string PatchId { get; set; } = "";
    public string TargetVersion { get; set; } = "";
    public string InstallDir { get; set; } = "";
    public string BackupDir { get; set; } = "";
    public DateTimeOffset InstalledAt { get; set; }
}

internal sealed class PayloadBundle : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string TempRoot { get; }
    public string PayloadRoot { get; }
    public PayloadManifest Manifest { get; }

    private PayloadBundle(string tempRoot, string payloadRoot, PayloadManifest manifest)
    {
        TempRoot = tempRoot;
        PayloadRoot = payloadRoot;
        Manifest = manifest;
    }

    public static PayloadBundle Open(Action<string> log)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("payload.zip", StringComparison.OrdinalIgnoreCase));
        var tempRoot = Path.Combine(Path.GetTempPath(), "CrealityOrcaPatcher", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var zipPath = Path.Combine(tempRoot, "payload.zip");

        using (var input = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("The embedded patch payload is missing."))
        using (var output = File.Create(zipPath))
        {
            input.CopyTo(output);
        }

        ZipFile.ExtractToDirectory(zipPath, tempRoot);
        var payloadDir = Path.Combine(tempRoot, "payload");
        var payloadRoot = Path.Combine(payloadDir, "root");
        var manifestPath = Path.Combine(payloadDir, "manifest.json");
        if (!Directory.Exists(payloadRoot) || !File.Exists(manifestPath))
            throw new InvalidDataException("The embedded payload has an invalid layout.");

        var manifest = JsonSerializer.Deserialize<PayloadManifest>(File.ReadAllText(manifestPath), JsonOptions)
            ?? throw new InvalidDataException("The embedded payload manifest is invalid.");
        if (string.IsNullOrWhiteSpace(manifest.PatchId) || manifest.Files.Count == 0)
            throw new InvalidDataException("The embedded payload manifest is incomplete.");

        log($"Verifying {manifest.Files.Count} embedded payload files...");
        foreach (var file in manifest.Files)
        {
            var path = PatchEngine.SafeCombine(payloadRoot, file.RelativePath);
            if (!File.Exists(path))
                throw new FileNotFoundException($"Payload file is missing: {file.RelativePath}");
            var info = new FileInfo(path);
            if (info.Length != file.Size || !PatchEngine.HashFile(path).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Payload verification failed: {file.RelativePath}");
        }

        return new PayloadBundle(tempRoot, payloadRoot, manifest);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(TempRoot))
                Directory.Delete(TempRoot, true);
        }
        catch
        {
            // The OS will eventually clear temporary files; patch success must not depend on cleanup.
        }
    }
}

internal static class PatchEngine
{
    private const string MarkerDirectoryName = ".creality-orca-patch";
    private const string MarkerFileName = "current.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string DefaultDataRoot =>
        Environment.GetEnvironmentVariable("CREALITY_ORCA_PATCHER_DATA")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrealityOrcaPatcher");

    public static string FindDefaultInstallDir()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "OrcaSlicer"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "OrcaSlicer")
        };
        return candidates.FirstOrDefault(IsOrcaInstallDir) ?? candidates[0];
    }

    public static bool IsOrcaInstallDir(string path) =>
        Directory.Exists(path) &&
        File.Exists(Path.Combine(path, "orca-slicer.exe")) &&
        File.Exists(Path.Combine(path, "OrcaSlicer.dll")) &&
        Directory.Exists(Path.Combine(path, "resources"));

    public static string Install(
        PayloadBundle bundle,
        string installDir,
        string dataRoot,
        bool forceClose,
        bool dryRun,
        Action<string> log)
    {
        installDir = NormalizeInstallDir(installDir);
        ValidateInstall(bundle.Manifest, installDir, log, out var clean, out var patched);
        StopOrcaIfNeeded(installDir, forceClose, log);

        if (patched)
        {
            VerifyInstalledPayload(bundle, installDir);
            log("This exact patch is already installed and verified.");
            return "The patch is already installed and all payload files are valid.";
        }

        if (!clean)
            throw new InvalidOperationException("The target is neither the supported clean beta nor this patch.");

        if (dryRun)
        {
            log("Dry run complete. No files were changed.");
            return "Dry run passed. The installed OrcaSlicer build is supported.";
        }

        var backupDir = Path.Combine(
            Path.GetFullPath(dataRoot),
            "Backups",
            bundle.Manifest.TargetVersion,
            DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
        var backupFilesDir = Path.Combine(backupDir, "files");
        Directory.CreateDirectory(backupFilesDir);
        var markerPath = GetMarkerPath(installDir);
        var previousMarkerPath = Path.Combine(backupDir, "previous-current.json");
        var backup = new BackupManifest
        {
            PatchId = bundle.Manifest.PatchId,
            InstallDir = installDir,
            CreatedAt = DateTimeOffset.Now,
            PreviousMarkerExisted = File.Exists(markerPath)
        };

        if (backup.PreviousMarkerExisted)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(previousMarkerPath)!);
            File.Copy(markerPath, previousMarkerPath, true);
            log("Preserved the previous patch marker; it will be restored on rollback.");
        }

        log($"Creating rollback backup: {backupDir}");
        foreach (var payloadFile in bundle.Manifest.Files)
        {
            var target = SafeCombine(installDir, payloadFile.RelativePath);
            var entry = new BackupEntry
            {
                RelativePath = payloadFile.RelativePath,
                Existed = File.Exists(target),
                PatchedSha256 = payloadFile.Sha256
            };
            if (entry.Existed)
            {
                entry.OriginalSha256 = HashFile(target);
                var backupPath = SafeCombine(backupFilesDir, payloadFile.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                File.Copy(target, backupPath, true);
            }
            backup.Entries.Add(entry);
        }
        WriteJson(Path.Combine(backupDir, "backup.json"), backup);

        try
        {
            log("Installing verified payload...");
            foreach (var payloadFile in bundle.Manifest.Files)
            {
                var source = SafeCombine(bundle.PayloadRoot, payloadFile.RelativePath);
                var target = SafeCombine(installDir, payloadFile.RelativePath);
                CopyAtomically(source, target, payloadFile.Sha256);
            }

            VerifyInstalledPayload(bundle, installDir);
            var marker = new PatchMarker
            {
                PatchId = bundle.Manifest.PatchId,
                TargetVersion = bundle.Manifest.TargetVersion,
                InstallDir = installDir,
                BackupDir = backupDir,
                InstalledAt = DateTimeOffset.Now
            };
            WriteJson(markerPath, marker);
            log("Post-install hash verification passed.");
            return $"Patch installed successfully. Rollback backup: {backupDir}";
        }
        catch
        {
            log("Installation failed. Restoring the pre-install state...");
            RestoreEntries(backup, backupDir, markerPath, log);
            throw;
        }
    }

    public static string Restore(
        PayloadBundle bundle,
        string installDir,
        string dataRoot,
        bool forceClose,
        Action<string> log)
    {
        installDir = NormalizeInstallDir(installDir);
        StopOrcaIfNeeded(installDir, forceClose, log);
        var backupDir = FindBackup(bundle.Manifest.PatchId, installDir, dataRoot);
        var backupPath = Path.Combine(backupDir, "backup.json");
        var backup = JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(backupPath), JsonOptions)
            ?? throw new InvalidDataException("The rollback manifest is invalid.");
        if (!Path.GetFullPath(backup.InstallDir).Equals(installDir, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The rollback backup belongs to a different OrcaSlicer install.");
        ValidateRestoreTarget(backup, installDir);

        log($"Restoring backup: {backupDir}");
        RestoreEntries(backup, backupDir, GetMarkerPath(installDir), log);
        foreach (var entry in backup.Entries.Where(entry => entry.Existed))
        {
            var target = SafeCombine(installDir, entry.RelativePath);
            if (!File.Exists(target) ||
                !HashFile(target).Equals(entry.OriginalSha256, StringComparison.OrdinalIgnoreCase))
                throw new IOException($"Rollback verification failed: {entry.RelativePath}");
        }
        log("Rollback hash verification passed.");
        return "The exact files that existed before this patch were restored.";
    }

    public static string Verify(PayloadBundle bundle, string installDir, Action<string> log)
    {
        installDir = NormalizeInstallDir(installDir);
        ValidateInstall(bundle.Manifest, installDir, log, out var clean, out var patched);
        if (patched)
        {
            VerifyInstalledPayload(bundle, installDir);
            return "Patched OrcaSlicer 2.4.0-beta installation verified.";
        }
        if (clean)
            return "Clean supported OrcaSlicer 2.4.0-beta installation verified.";
        throw new InvalidOperationException("Unsupported OrcaSlicer installation.");
    }

    public static bool HasRestorableBackup(PayloadManifest manifest, string installDir, string dataRoot)
    {
        try
        {
            _ = FindBackup(manifest.PatchId, NormalizeInstallDir(installDir), dataRoot);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ValidateInstall(
        PayloadManifest manifest,
        string installDir,
        Action<string> log,
        out bool clean,
        out bool patched)
    {
        if (!IsOrcaInstallDir(installDir))
            throw new DirectoryNotFoundException("The selected folder is not a complete OrcaSlicer installation.");

        var coreNames = new[] { "orca-slicer.exe", "OrcaSlicer.dll" };
        var actual = coreNames.ToDictionary(
            name => name,
            name => HashFile(Path.Combine(installDir, name)),
            StringComparer.OrdinalIgnoreCase);
        var patchedHashes = manifest.Files
            .Where(file => coreNames.Contains(Path.GetFileName(file.RelativePath), StringComparer.OrdinalIgnoreCase))
            .ToDictionary(file => Path.GetFileName(file.RelativePath), file => file.Sha256, StringComparer.OrdinalIgnoreCase);

        clean = coreNames.All(name =>
            manifest.ExpectedCleanCore.TryGetValue(name, out var expected) &&
            actual[name].Equals(expected, StringComparison.OrdinalIgnoreCase));
        patched = coreNames.All(name =>
            patchedHashes.TryGetValue(name, out var expected) &&
            actual[name].Equals(expected, StringComparison.OrdinalIgnoreCase));

        log($"Target: {installDir}");
        log($"orca-slicer.exe SHA256: {actual["orca-slicer.exe"]}");
        log($"OrcaSlicer.dll SHA256: {actual["OrcaSlicer.dll"]}");
        if (!clean && !patched)
        {
            throw new InvalidOperationException(
                $"Unsupported or partially modified OrcaSlicer build. This patch only accepts {manifest.TargetVersion} " +
                $"at commit {manifest.TargetCommit}, or an already-complete installation of this exact patch.");
        }
    }

    private static void VerifyInstalledPayload(PayloadBundle bundle, string installDir)
    {
        foreach (var payloadFile in bundle.Manifest.Files)
        {
            var target = SafeCombine(installDir, payloadFile.RelativePath);
            if (!File.Exists(target) ||
                new FileInfo(target).Length != payloadFile.Size ||
                !HashFile(target).Equals(payloadFile.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Installed payload verification failed: {payloadFile.RelativePath}");
        }
    }

    private static void RestoreEntries(
        BackupManifest backup,
        string backupDir,
        string markerPath,
        Action<string> log)
    {
        var backupFilesDir = Path.Combine(backupDir, "files");
        foreach (var entry in backup.Entries.AsEnumerable().Reverse())
        {
            var target = SafeCombine(backup.InstallDir, entry.RelativePath);
            if (entry.Existed)
            {
                var source = SafeCombine(backupFilesDir, entry.RelativePath);
                if (!File.Exists(source))
                    throw new FileNotFoundException($"Rollback file is missing: {entry.RelativePath}");
                CopyAtomically(source, target, entry.OriginalSha256!);
            }
            else if (File.Exists(target))
            {
                File.Delete(target);
            }
        }

        var previousMarkerPath = Path.Combine(backupDir, "previous-current.json");
        if (backup.PreviousMarkerExisted && File.Exists(previousMarkerPath))
            CopyAtomically(previousMarkerPath, markerPath, HashFile(previousMarkerPath));
        else if (File.Exists(markerPath))
            File.Delete(markerPath);

        log("Pre-patch files restored.");
    }

    private static void ValidateRestoreTarget(BackupManifest backup, string installDir)
    {
        foreach (var coreName in new[] { "orca-slicer.exe", "OrcaSlicer.dll" })
        {
            var entry = backup.Entries.SingleOrDefault(item =>
                Path.GetFileName(item.RelativePath).Equals(coreName, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
                throw new InvalidDataException($"The rollback manifest is missing {coreName}.");

            var target = Path.Combine(installDir, coreName);
            if (!File.Exists(target))
                continue;
            var actual = HashFile(target);
            var isPatched = actual.Equals(entry.PatchedSha256, StringComparison.OrdinalIgnoreCase);
            var isOriginal = actual.Equals(entry.OriginalSha256, StringComparison.OrdinalIgnoreCase);
            if (!isPatched && !isOriginal)
            {
                throw new InvalidOperationException(
                    $"{coreName} changed after this patch was installed. Rollback was stopped to avoid " +
                    "overwriting a newer or unrelated OrcaSlicer build.");
            }
        }
    }

    private static string FindBackup(string patchId, string installDir, string dataRoot)
    {
        var markerPath = GetMarkerPath(installDir);
        if (File.Exists(markerPath))
        {
            try
            {
                var marker = JsonSerializer.Deserialize<PatchMarker>(File.ReadAllText(markerPath), JsonOptions);
                if (marker?.PatchId == patchId && File.Exists(Path.Combine(marker.BackupDir, "backup.json")))
                    return marker.BackupDir;
            }
            catch
            {
                // A stale marker from an older patch is ignored.
            }
        }

        var backupRoot = Path.Combine(Path.GetFullPath(dataRoot), "Backups");
        if (!Directory.Exists(backupRoot))
            throw new DirectoryNotFoundException("No rollback backup was found for this patch.");

        foreach (var path in Directory.EnumerateFiles(backupRoot, "backup.json", SearchOption.AllDirectories)
                     .OrderByDescending(File.GetLastWriteTimeUtc))
        {
            try
            {
                var backup = JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(path), JsonOptions);
                if (backup?.PatchId == patchId &&
                    Path.GetFullPath(backup.InstallDir).Equals(installDir, StringComparison.OrdinalIgnoreCase))
                    return Path.GetDirectoryName(path)!;
            }
            catch
            {
                // Ignore incomplete backups and continue looking.
            }
        }

        throw new DirectoryNotFoundException("No rollback backup was found for this OrcaSlicer installation.");
    }

    private static void StopOrcaIfNeeded(string installDir, bool forceClose, Action<string> log)
    {
        var running = Process.GetProcesses()
            .Where(process =>
            {
                try
                {
                    var path = process.MainModule?.FileName;
                    return path is not null &&
                           Path.GetFullPath(path).StartsWith(installDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                           process.ProcessName.Equals("orca-slicer", StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            })
            .ToArray();
        if (running.Length == 0)
            return;
        if (!forceClose)
            throw new InvalidOperationException("OrcaSlicer is running from the target folder. Close it or enable automatic close.");

        log($"Closing {running.Length} OrcaSlicer process(es)...");
        foreach (var process in running)
        {
            using (process)
            {
                process.Kill(true);
                if (!process.WaitForExit(10000))
                    throw new InvalidOperationException("OrcaSlicer did not exit within 10 seconds.");
            }
        }
    }

    private static void CopyAtomically(string source, string target, string expectedHash)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temp = target + ".creality-new-" + Guid.NewGuid().ToString("N");
        try
        {
            File.Copy(source, temp, true);
            if (!HashFile(temp).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new IOException($"Temporary copy verification failed: {target}");
            File.Move(temp, target, true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    private static void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temp, JsonSerializer.Serialize(value, JsonOptions));
        File.Move(temp, path, true);
    }

    private static string NormalizeInstallDir(string installDir) =>
        Path.GetFullPath(installDir.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static string GetMarkerPath(string installDir) =>
        Path.Combine(installDir, MarkerDirectoryName, MarkerFileName);

    internal static string SafeCombine(string root, string relativePath)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath));
        if (!fullPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Path escapes the expected root: {relativePath}");
        return fullPath;
    }

    internal static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
