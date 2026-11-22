using System.IO;
namespace ImageRenamer.Services;

public sealed record RenameProgress(int Percentage, string Message, int Completed, int Total);

public sealed record RenameResult(
    bool Success,
    int RenamedCount,
    string Message,
    string? RecoveryFolder = null);

public sealed class ImageRenameService
{
    public Task<IReadOnlyList<string>> GetImagesAsync(
        string folderPath,
        IEnumerable<string> supportedExtensions)
    {
        return Task.Run<IReadOnlyList<string>>(() => GetImages(folderPath, supportedExtensions));
    }

    public Task<RenameResult> RenameAsync(
        string folderPath,
        IEnumerable<string> supportedExtensions,
        string tempFolderPrefix,
        IProgress<RenameProgress> progress)
    {
        // File moves are deliberately sequential. Parallel renames in one directory
        // add contention and make rollback harder without improving disk throughput.
        return Task.Run(() => Rename(folderPath, supportedExtensions, tempFolderPrefix, progress));
    }

    private static IReadOnlyList<string> GetImages(
        string folderPath,
        IEnumerable<string> supportedExtensions)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new InvalidOperationException("Set an image folder in appsettings.json or choose one with Browse.");
        }

        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Folder does not exist: {folderPath}");
        }

        var extensions = new HashSet<string>(
            (supportedExtensions ?? Enumerable.Empty<string>())
                .Where(extension => !string.IsNullOrWhiteSpace(extension))
                .Select(extension => extension.StartsWith('.') ? extension : "." + extension),
            StringComparer.OrdinalIgnoreCase);

        return Directory.EnumerateFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
            .Where(path => extensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToArray();
    }

    private static RenameResult Rename(
        string folderPath,
        IEnumerable<string> supportedExtensions,
        string tempFolderPrefix,
        IProgress<RenameProgress> progress)
    {
        IReadOnlyList<string> files;
        try
        {
            files = GetImages(folderPath, supportedExtensions);
        }
        catch (Exception ex)
        {
            return new RenameResult(false, 0, ex.Message);
        }

        if (files.Count == 0)
        {
            return new RenameResult(false, 0, "No supported image files were found in this folder.");
        }

        var safePrefix = Path.GetFileName((tempFolderPrefix ?? string.Empty).Trim());
        if (string.IsNullOrWhiteSpace(safePrefix))
        {
            safePrefix = ".image-renamer-tmp";
        }

        var tempFolder = Path.Combine(folderPath, $"{safePrefix}-{Guid.NewGuid():N}");
        var entries = new List<RenameEntry>(files.Count);
        var rollbackErrors = new List<string>();

        try
        {
            Directory.CreateDirectory(tempFolder);
            progress.Report(new RenameProgress(0, "Preparing files…", 0, files.Count));

            // Phase 1: move every source image out of the way. This prevents an
            // existing name such as 10.jpg from colliding with a new 10.jpg.
            for (var index = 0; index < files.Count; index++)
            {
                var originalPath = files[index];
                var extension = Path.GetExtension(originalPath);
                var stagedPath = Path.Combine(tempFolder, $"{index + 1:D8}{extension}");
                var finalPath = Path.Combine(folderPath, $"{index + 1}{extension}");

                File.Move(originalPath, stagedPath);
                entries.Add(new RenameEntry(originalPath, stagedPath, finalPath));

                var percentage = (int)Math.Round((index + 1d) / files.Count * 45d);
                progress.Report(new RenameProgress(
                    percentage,
                    $"Staging images… {index + 1} of {files.Count}",
                    index + 1,
                    files.Count));
            }

            // Phase 2: move staged files back using sequential names. Extensions
            // are kept exactly as they appeared in the original file names.
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                File.Move(entry.StagedPath, entry.FinalPath);
                entry.IsCommitted = true;

                var percentage = 45 + (int)Math.Round((index + 1d) / entries.Count * 55d);
                progress.Report(new RenameProgress(
                    percentage,
                    $"Renaming images… {index + 1} of {entries.Count}",
                    index + 1,
                    entries.Count));
            }

            TryDeleteDirectory(tempFolder);
            progress.Report(new RenameProgress(100, "Rename complete.", files.Count, files.Count));
            return new RenameResult(true, files.Count, $"Successfully renamed {files.Count:N0} image(s).");
        }
        catch (Exception ex)
        {
            // Roll back committed targets into staging first, then restore each
            // source name. All source images were staged before phase 2 began.
            foreach (var entry in entries.AsEnumerable().Reverse())
            {
                if (!entry.IsCommitted)
                {
                    continue;
                }

                try
                {
                    if (File.Exists(entry.FinalPath))
                    {
                        File.Move(entry.FinalPath, entry.StagedPath);
                    }

                    entry.IsCommitted = false;
                }
                catch (Exception rollbackException)
                {
                    rollbackErrors.Add($"{Path.GetFileName(entry.FinalPath)}: {rollbackException.Message}");
                }
            }

            foreach (var entry in entries.AsEnumerable().Reverse())
            {
                try
                {
                    if (File.Exists(entry.StagedPath))
                    {
                        if (File.Exists(entry.OriginalPath))
                        {
                            throw new IOException($"Original file already exists: {entry.OriginalPath}");
                        }

                        File.Move(entry.StagedPath, entry.OriginalPath);
                    }
                }
                catch (Exception rollbackException)
                {
                    rollbackErrors.Add($"{Path.GetFileName(entry.OriginalPath)}: {rollbackException.Message}");
                }
            }

            if (rollbackErrors.Count == 0)
            {
                TryDeleteDirectory(tempFolder);
                return new RenameResult(
                    false,
                    0,
                    $"Rename failed. Original file names were restored. Details: {ex.Message}");
            }

            var rollbackSummary = string.Join(Environment.NewLine, rollbackErrors);
            return new RenameResult(
                false,
                0,
                $"Rename failed and automatic rollback was incomplete. Do not delete the recovery folder. Details: {ex.Message}{Environment.NewLine}{rollbackSummary}",
                tempFolder);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: false);
            }
        }
        catch
        {
            // A leftover empty staging directory is safer than masking the
            // completed rename operation with a cleanup-only exception.
        }
    }

    private sealed class RenameEntry(string originalPath, string stagedPath, string finalPath)
    {
        public string OriginalPath { get; } = originalPath;
        public string StagedPath { get; } = stagedPath;
        public string FinalPath { get; } = finalPath;
        public bool IsCommitted { get; set; }
    }
}
