using Godot;
using SummerTrip.Core.Session;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SummerTrip.Core.Save;

public enum SaveSlotKind
{
    Manual,
    Quick,
    Auto,
}

public readonly record struct SaveSlot
{
    public SaveSlot(SaveSlotKind kind, int index)
    {
        var maximum = kind switch
        {
            SaveSlotKind.Manual => SaveService.ManualSlotCount,
            SaveSlotKind.Quick => SaveService.QuickSlotCount,
            SaveSlotKind.Auto => SaveService.AutoSlotCount,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        if (index < 1 || index > maximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                $"Slot index for {kind} must be between 1 and {maximum}."
            );
        }

        Kind = kind;
        Index = index;
    }

    public SaveSlotKind Kind { get; }
    public int Index { get; }
}

public sealed record LoadedSave(
    SaveDocument Document,
    bool ScreenshotExists
);

public enum SaveSlotStatus
{
    Empty,
    Valid,
    ScreenshotMissing,
    Corrupt,
}

public sealed record SaveSlotSummary(
    SaveSlot Slot,
    SaveSlotStatus Status,
    SaveDocument? Document,
    string? ScreenshotPath,
    string? Error
);

internal enum SaveWriteCheckpoint
{
    ScreenshotValidated,
    TemporaryJsonValidated,
    JsonCommitted,
}

public sealed class SaveService
{
    public const int ManualSlotCount = 54;
    public const int QuickSlotCount = 1;
    public const int AutoSlotCount = 6;
    public const int ScreenshotWidth = 384;
    public const int ScreenshotHeight = 216;

    private static readonly JsonSerializerOptions CacheJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private readonly string _saveRoot;
    private readonly string _profilePath;

    public SaveService(string saveRoot = "user://saves")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(saveRoot);
        _saveRoot = Globalize(saveRoot);
        _profilePath = Path.Combine(
            Directory.GetParent(_saveRoot)?.FullName
                ?? throw new ArgumentException("Save root must have a parent directory.", nameof(saveRoot)),
            "profile.json"
        );
    }

    internal Action<SaveWriteCheckpoint>? WriteCheckpointObserver { get; set; }
    public string? LastProfileRecoveryWarning { get; private set; }

    public string GetSlotJsonPath(SaveSlot slot)
    {
        return Path.Combine(_saveRoot, GetSlotDirectoryName(slot), GetSlotBaseName(slot) + ".json");
    }

    public string GetProfilePath()
    {
        return _profilePath;
    }

    public SaveSlot GetNextAutoSlot()
    {
        SaveSlot? oldestSlot = null;
        DateTimeOffset oldestSavedAt = DateTimeOffset.MaxValue;
        for (var index = 1; index <= AutoSlotCount; index++)
        {
            var slot = new SaveSlot(SaveSlotKind.Auto, index);
            var path = GetSlotJsonPath(slot);
            if (!File.Exists(path))
            {
                return slot;
            }

            try
            {
                var document = SaveSchema.Deserialize(File.ReadAllText(path));
                if (
                    !DateTimeOffset.TryParseExact(
                        document.SavedAt,
                        "O",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out var savedAt
                    )
                )
                {
                    return slot;
                }

                if (savedAt < oldestSavedAt)
                {
                    oldestSavedAt = savedAt;
                    oldestSlot = slot;
                }
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is SaveValidationException
            )
            {
                return slot;
            }
        }

        return oldestSlot
            ?? throw new InvalidOperationException("Unable to select an automatic save slot.");
    }

    public SaveDocument Save(SaveSlot slot, SaveDocument document, Image screenshot)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(screenshot);
        if (
            screenshot.GetWidth() != ScreenshotWidth ||
            screenshot.GetHeight() != ScreenshotHeight
        )
        {
            throw new SaveValidationException(
                $"Save screenshot must be {ScreenshotWidth}x{ScreenshotHeight} pixels."
            );
        }

        var slotDirectory = Path.Combine(_saveRoot, GetSlotDirectoryName(slot));
        Directory.CreateDirectory(slotDirectory);
        var generation = NextGeneration(slotDirectory, GetSlotBaseName(slot));
        var screenshotFileName =
            $"{GetSlotBaseName(slot)}.g{generation.ToString(CultureInfo.InvariantCulture)}.png";
        var screenshotPath = Path.Combine(slotDirectory, screenshotFileName);
        var screenshotRelativePath =
            $"{GetSlotDirectoryName(slot)}/{screenshotFileName}";
        var jsonPath = GetSlotJsonPath(slot);
        var temporaryPath = jsonPath + $".{Guid.NewGuid():N}.tmp";
        var jsonCommitted = false;
        try
        {
            var screenshotError = screenshot.SavePng(screenshotPath);
            if (screenshotError != Error.Ok)
            {
                throw new IOException(
                    $"Unable to write save screenshot '{screenshotRelativePath}': {screenshotError}."
                );
            }

            ValidateScreenshotFile(screenshotPath);
            WriteCheckpointObserver?.Invoke(SaveWriteCheckpoint.ScreenshotValidated);

            document.ScreenshotPath = screenshotRelativePath;
            document.SavedAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            SaveSchema.Validate(document, requireScreenshotPath: true);
            ValidateScreenshotForSlot(slot, document.ScreenshotPath);

            WriteTextAndFlush(temporaryPath, SaveSchema.Serialize(document));
            _ = SaveSchema.Deserialize(File.ReadAllText(temporaryPath));
            WriteCheckpointObserver?.Invoke(SaveWriteCheckpoint.TemporaryJsonValidated);

            var backupPath = jsonPath + ".bak";
            ReplaceFile(temporaryPath, jsonPath, backupPath);
            jsonCommitted = true;
            WriteCheckpointObserver?.Invoke(SaveWriteCheckpoint.JsonCommitted);

            TryUpdateIndex();
            TryCleanupOldScreenshots(
                slotDirectory,
                GetSlotBaseName(slot),
                screenshotPath,
                TryGetBackupScreenshotPath(backupPath)
            );
            TryCleanupTemporaryFiles(slotDirectory, GetSlotBaseName(slot));
            return document;
        }
        catch
        {
            if (!jsonCommitted)
            {
                TryDeleteFile(screenshotPath, "incomplete save screenshot");
                TryDeleteFile(temporaryPath, "incomplete save metadata");
            }
            throw;
        }
    }

    public LoadedSave Load(SaveSlot slot)
    {
        var jsonPath = GetSlotJsonPath(slot);
        if (!File.Exists(jsonPath))
        {
            throw new FileNotFoundException(
                $"No {slot.Kind.ToString().ToLowerInvariant()} save exists in slot {slot.Index}.",
                jsonPath
            );
        }

        SaveDocument document;
        try
        {
            document = SaveSchema.Deserialize(File.ReadAllText(jsonPath));
        }
        catch (SaveValidationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException
        )
        {
            throw new IOException($"Unable to read save slot '{jsonPath}'.", exception);
        }

        ValidateScreenshotForSlot(slot, document.ScreenshotPath);
        var screenshotPath = ResolveControlledRelativePath(document.ScreenshotPath);
        return new LoadedSave(document, File.Exists(screenshotPath));
    }

    public SaveSlotSummary Inspect(SaveSlot slot)
    {
        if (!File.Exists(GetSlotJsonPath(slot)))
        {
            return new SaveSlotSummary(
                slot,
                SaveSlotStatus.Empty,
                null,
                null,
                null
            );
        }

        try
        {
            var loaded = Load(slot);
            var screenshotPath = ResolveControlledRelativePath(
                loaded.Document.ScreenshotPath
            );
            return new SaveSlotSummary(
                slot,
                loaded.ScreenshotExists
                    ? SaveSlotStatus.Valid
                    : SaveSlotStatus.ScreenshotMissing,
                loaded.Document,
                loaded.ScreenshotExists ? screenshotPath : null,
                loaded.ScreenshotExists ? null : "存档截图缺失，仍可读取。"
            );
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is SaveValidationException
        )
        {
            return new SaveSlotSummary(
                slot,
                SaveSlotStatus.Corrupt,
                null,
                null,
                $"存档已损坏：{exception.Message}"
            );
        }
    }

    public SaveSlotSummary? GetLatestValidSlot()
    {
        SaveSlotSummary? latest = null;
        DateTimeOffset latestSavedAt = DateTimeOffset.MinValue;
        foreach (var kind in new[]
        {
            SaveSlotKind.Manual,
            SaveSlotKind.Quick,
            SaveSlotKind.Auto,
        })
        {
            var count = kind switch
            {
                SaveSlotKind.Manual => ManualSlotCount,
                SaveSlotKind.Quick => QuickSlotCount,
                SaveSlotKind.Auto => AutoSlotCount,
                _ => 0,
            };
            for (var index = 1; index <= count; index++)
            {
                var summary = Inspect(new SaveSlot(kind, index));
                if (
                    summary.Status is not (
                        SaveSlotStatus.Valid or SaveSlotStatus.ScreenshotMissing
                    ) ||
                    summary.Document is null ||
                    !DateTimeOffset.TryParseExact(
                        summary.Document.SavedAt,
                        "O",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out var savedAt
                    ) ||
                    savedAt <= latestSavedAt
                )
                {
                    continue;
                }

                latest = summary;
                latestSavedAt = savedAt;
            }
        }

        return latest;
    }

    public void Delete(SaveSlot slot)
    {
        var jsonPath = GetSlotJsonPath(slot);
        var directory = Path.GetDirectoryName(jsonPath)
            ?? throw new InvalidOperationException("Save slot path has no directory.");
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(
            directory,
            $"{GetSlotBaseName(slot)}.*"
        ))
        {
            File.Delete(path);
        }

        TryUpdateIndex();
    }

    public ProfileData LoadProfile()
    {
        LastProfileRecoveryWarning = null;
        var backupPath = _profilePath + ".bak";
        if (!File.Exists(_profilePath))
        {
            if (!File.Exists(backupPath))
            {
                return new ProfileData();
            }

            try
            {
                var recovered = LoadProfileFile(backupPath);
                var backupWasPersisted = TryPersistRecoveredProfile(
                    recovered,
                    primaryCanBeReplaced: true
                );
                LastProfileRecoveryWarning = backupWasPersisted
                    ? "全局进度主文件缺失，已从备份恢复。"
                    : "全局进度主文件缺失，本次已从备份载入内存，但无法重建主文件。";
                GD.PushWarning(LastProfileRecoveryWarning);
                return recovered;
            }
            catch (Exception backupException) when (
                backupException is IOException ||
                backupException is UnauthorizedAccessException ||
                backupException is SaveValidationException
            )
            {
                var backupQuarantined = TryQuarantineProfileFile(backupPath);
                var defaults = new ProfileData();
                var defaultsWerePersisted = TryPersistRecoveredProfile(
                    defaults,
                    backupQuarantined
                );
                LastProfileRecoveryWarning = defaultsWerePersisted
                    ? "全局进度主文件缺失且备份损坏，已保留损坏备份并创建默认进度。"
                    : "全局进度主文件缺失且备份损坏，本次使用默认进度，但无法安全重建文件。";
                GD.PushWarning(
                    $"{LastProfileRecoveryWarning} Cause: {backupException.Message}"
                );
                return defaults;
            }
        }

        try
        {
            return LoadProfileFile(_profilePath);
        }
        catch (Exception primaryException) when (
            primaryException is IOException ||
            primaryException is UnauthorizedAccessException ||
            primaryException is SaveValidationException
        )
        {
            ProfileData? recovered = null;
            Exception? backupException = null;
            if (File.Exists(backupPath))
            {
                try
                {
                    recovered = LoadProfileFile(backupPath);
                }
                catch (Exception exception) when (
                    exception is IOException ||
                    exception is UnauthorizedAccessException ||
                    exception is SaveValidationException
                )
                {
                    backupException = exception;
                }
            }

            var primaryQuarantined = TryQuarantineProfileFile(_profilePath);
            if (recovered is not null)
            {
                var backupWasPersisted = TryPersistRecoveredProfile(
                    recovered,
                    primaryQuarantined
                );
                LastProfileRecoveryWarning = backupWasPersisted
                    ? "全局进度文件损坏，已从备份恢复；原文件已保留为 .corrupt。"
                    : "全局进度文件损坏，本次已在内存中从备份恢复，但无法安全重写主文件。";
                GD.PushWarning(
                    $"{LastProfileRecoveryWarning} Cause: {primaryException.Message}"
                );
                return recovered;
            }

            var backupQuarantined = TryQuarantineProfileFile(backupPath);
            var defaults = new ProfileData();
            var defaultsWerePersisted = TryPersistRecoveredProfile(
                defaults,
                primaryQuarantined && backupQuarantined
            );
            LastProfileRecoveryWarning = defaultsWerePersisted
                ? "全局进度文件及备份均不可用，已保留损坏文件并创建默认进度。"
                : "全局进度文件及备份均不可用，本次使用默认进度，但无法安全重写文件。";
            GD.PushWarning(
                $"{LastProfileRecoveryWarning} Primary: {primaryException.Message}" +
                (backupException is null ? "" : $" Backup: {backupException.Message}")
            );
            return defaults;
        }
    }

    public void SaveProfile(ProfileData profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Directory.CreateDirectory(
            Path.GetDirectoryName(_profilePath)
                ?? throw new InvalidOperationException("Profile path has no directory.")
        );

        var temporaryPath = _profilePath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            var json = JsonSerializer.Serialize(
                ProfileFileData.FromSnapshot(profile.CaptureSnapshot()),
                CacheJsonOptions
            );
            WriteTextAndFlush(temporaryPath, json);
            var candidate = JsonSerializer.Deserialize<ProfileFileData>(
                File.ReadAllText(temporaryPath),
                CacheJsonOptions
            ) ?? throw new SaveValidationException("Temporary profile JSON is invalid.");
            _ = candidate.ToProfile();
            ReplaceFile(temporaryPath, _profilePath, _profilePath + ".bak");
        }
        finally
        {
            TryDeleteFile(temporaryPath, "incomplete profile metadata");
        }
    }

    private static ProfileData LoadProfileFile(string path)
    {
        try
        {
            var document = JsonSerializer.Deserialize<ProfileFileData>(
                File.ReadAllText(path),
                CacheJsonOptions
            ) ?? throw new SaveValidationException("Profile JSON did not contain an object.");
            return document.ToProfile();
        }
        catch (SaveValidationException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new SaveValidationException(
                $"Profile JSON has an invalid field or value: {exception.Message}",
                exception
            );
        }
    }

    private bool TryQuarantineProfileFile(string path)
    {
        if (!File.Exists(path))
        {
            return true;
        }

        try
        {
            var timestamp = DateTimeOffset.UtcNow.ToString(
                "yyyyMMdd_HHmmss_fff",
                CultureInfo.InvariantCulture
            );
            var destination = $"{path}.{timestamp}.corrupt";
            var suffix = 1;
            while (File.Exists(destination))
            {
                destination = $"{path}.{timestamp}_{suffix:00}.corrupt";
                suffix++;
            }
            File.Move(path, destination);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException
        )
        {
            GD.PushWarning(
                $"Damaged profile file '{path}' could not be quarantined: {exception.Message}"
            );
            return false;
        }
    }

    private bool TryPersistRecoveredProfile(ProfileData profile, bool primaryCanBeReplaced)
    {
        if (!primaryCanBeReplaced)
        {
            GD.PushWarning(
                "Recovered profile is available in memory but could not replace the damaged file."
            );
            return false;
        }

        try
        {
            SaveProfile(profile);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is SaveValidationException
        )
        {
            GD.PushWarning(
                $"Recovered profile could not be persisted: {exception.Message}"
            );
            return false;
        }
    }

    private void TryUpdateIndex()
    {
        try
        {
            Directory.CreateDirectory(_saveRoot);
            var entries = EnumerateSlotJsonFiles()
                .Select(TryReadIndexEntry)
                .Where(entry => entry is not null)
                .Select(entry => entry!)
                .OrderByDescending(entry => entry.SavedAt, StringComparer.Ordinal)
                .ToList();
            var index = new SaveIndexData { SchemaVersion = 1, Entries = entries };
            var path = Path.Combine(_saveRoot, "index.json");
            var temporaryPath = path + $".{Guid.NewGuid():N}.tmp";
            WriteTextAndFlush(
                temporaryPath,
                JsonSerializer.Serialize(index, CacheJsonOptions)
            );
            _ = JsonSerializer.Deserialize<SaveIndexData>(
                File.ReadAllText(temporaryPath),
                CacheJsonOptions
            ) ?? throw new JsonException("Index JSON did not contain an object.");
            ReplaceFile(temporaryPath, path, path + ".bak");
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is JsonException ||
            exception is SaveValidationException
        )
        {
            GD.PushWarning($"Save index cache could not be updated: {exception.Message}");
        }
    }

    private SaveIndexEntry? TryReadIndexEntry(string path)
    {
        try
        {
            return new SaveIndexEntry
            {
                Path = Path.GetRelativePath(_saveRoot, path).Replace('\\', '/'),
                SavedAt = SaveSchema.Deserialize(File.ReadAllText(path)).SavedAt,
            };
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is SaveValidationException
        )
        {
            return null;
        }
    }

    private IEnumerable<string> EnumerateSlotJsonFiles()
    {
        foreach (var directoryName in new[] { "manual", "quick", "auto" })
        {
            var directory = Path.Combine(_saveRoot, directoryName);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(directory, "slot_*.json"))
            {
                yield return path;
            }
        }
    }

    private string ResolveControlledRelativePath(string relativePath)
    {
        var combined = Path.GetFullPath(
            Path.Combine(_saveRoot, relativePath.Replace('/', Path.DirectorySeparatorChar))
        );
        var rootWithSeparator = Path.GetFullPath(_saveRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new SaveValidationException(
                "Save screenshot path resolves outside the save directory."
            );
        }

        return combined;
    }

    private static void ValidateScreenshotForSlot(SaveSlot slot, string relativePath)
    {
        var expectedPrefix = $"{GetSlotDirectoryName(slot)}/{GetSlotBaseName(slot)}.g";
        if (
            !relativePath.StartsWith(expectedPrefix, StringComparison.Ordinal) ||
            !relativePath.EndsWith(".png", StringComparison.Ordinal)
        )
        {
            throw new SaveValidationException(
                $"Save screenshot path does not belong to {slot.Kind} slot {slot.Index}."
            );
        }
    }

    private static void ValidateScreenshotFile(string path)
    {
        using var image = Image.LoadFromFile(path);
        if (
            image is null ||
            image.IsEmpty() ||
            image.GetWidth() != ScreenshotWidth ||
            image.GetHeight() != ScreenshotHeight
        )
        {
            throw new SaveValidationException(
                $"Written save screenshot is not a readable {ScreenshotWidth}x{ScreenshotHeight} PNG."
            );
        }
    }

    private static long NextGeneration(string directory, string slotBaseName)
    {
        var generation = DateTime.UtcNow.Ticks;
        while (File.Exists(Path.Combine(directory, $"{slotBaseName}.g{generation}.png")))
        {
            generation++;
        }

        return generation;
    }

    private static void WriteTextAndFlush(string path, string content)
    {
        using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            System.IO.FileAccess.Write,
            FileShare.None
        );
        using var writer = new StreamWriter(stream);
        writer.Write(content);
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }

    private static void ReplaceFile(string temporaryPath, string targetPath, string backupPath)
    {
        if (File.Exists(targetPath))
        {
            File.Replace(temporaryPath, targetPath, backupPath, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(temporaryPath, targetPath);
        }
    }

    private static void TryCleanupOldScreenshots(
        string directory,
        string slotBaseName,
        string currentScreenshotPath,
        string? backupScreenshotPath
    )
    {
        try
        {
            foreach (var path in Directory.EnumerateFiles(directory, $"{slotBaseName}.g*.png"))
            {
                if (
                    !string.Equals(
                        path,
                        currentScreenshotPath,
                        StringComparison.OrdinalIgnoreCase
                    ) &&
                    !string.Equals(
                        path,
                        backupScreenshotPath,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    File.Delete(path);
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException
        )
        {
            GD.PushWarning($"Old save screenshots could not be cleaned: {exception.Message}");
        }
    }

    private static void TryCleanupTemporaryFiles(string directory, string slotBaseName)
    {
        try
        {
            foreach (var path in Directory.EnumerateFiles(
                directory,
                $"{slotBaseName}.json.*.tmp"
            ))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException
        )
        {
            GD.PushWarning($"Orphaned save metadata could not be cleaned: {exception.Message}");
        }
    }

    private static void TryDeleteFile(string path, string description)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException
        )
        {
            GD.PushWarning($"Unable to remove {description} '{path}': {exception.Message}");
        }
    }

    private string? TryGetBackupScreenshotPath(string backupJsonPath)
    {
        if (!File.Exists(backupJsonPath))
        {
            return null;
        }

        try
        {
            var backup = SaveSchema.Deserialize(File.ReadAllText(backupJsonPath));
            return ResolveControlledRelativePath(backup.ScreenshotPath);
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is SaveValidationException
        )
        {
            GD.PushWarning($"Previous save backup could not be inspected: {exception.Message}");
            return null;
        }
    }

    private static string GetSlotDirectoryName(SaveSlot slot)
    {
        return slot.Kind.ToString().ToLowerInvariant();
    }

    private static string GetSlotBaseName(SaveSlot slot)
    {
        return $"slot_{slot.Index:000}";
    }

    private static string Globalize(string path)
    {
        return path.StartsWith("user://", StringComparison.Ordinal)
            ? ProjectSettings.GlobalizePath(path)
            : Path.GetFullPath(path);
    }

    private sealed class SaveIndexData
    {
        public int SchemaVersion { get; set; }
        public List<SaveIndexEntry> Entries { get; set; } = new();
    }

    private sealed class SaveIndexEntry
    {
        public string Path { get; set; } = "";
        public string SavedAt { get; set; } = "";
    }

    private sealed class ProfileFileData
    {
        public int ProfileVersion { get; set; }
        public List<string> ReadLineIds { get; set; } = null!;
        public List<string> UnlockedEndings { get; set; } = null!;
        public List<string> UnlockedCgs { get; set; } = null!;
        public List<string> Achievements { get; set; } = null!;

        public static ProfileFileData FromSnapshot(ProfileSnapshot snapshot)
        {
            return new ProfileFileData
            {
                ProfileVersion = snapshot.ProfileVersion,
                ReadLineIds = snapshot.ReadLineIds.ToList(),
                UnlockedEndings = snapshot.UnlockedEndings.ToList(),
                UnlockedCgs = snapshot.UnlockedCgs.ToList(),
                Achievements = snapshot.Achievements.ToList(),
            };
        }

        public ProfileData ToProfile()
        {
            if (
                ReadLineIds is null ||
                UnlockedEndings is null ||
                UnlockedCgs is null ||
                Achievements is null
            )
            {
                throw new SaveValidationException("Profile collection fields are required.");
            }

            var profile = new ProfileData();
            try
            {
                profile.RestoreSnapshot(new ProfileSnapshot(
                    ProfileVersion,
                    ReadLineIds,
                    UnlockedEndings,
                    UnlockedCgs,
                    Achievements
                ));
            }
            catch (ArgumentException exception)
            {
                throw new SaveValidationException(
                    $"Profile data is invalid: {exception.Message}",
                    exception
                );
            }

            return profile;
        }
    }
}
