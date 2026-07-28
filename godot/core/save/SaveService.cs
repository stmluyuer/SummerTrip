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

        var jsonPath = GetSlotJsonPath(slot);
        var temporaryPath = jsonPath + $".{Guid.NewGuid():N}.tmp";
        WriteTextAndFlush(temporaryPath, SaveSchema.Serialize(document));
        _ = SaveSchema.Deserialize(File.ReadAllText(temporaryPath));
        WriteCheckpointObserver?.Invoke(SaveWriteCheckpoint.TemporaryJsonValidated);

        var backupPath = jsonPath + ".bak";
        ReplaceFile(temporaryPath, jsonPath, backupPath);
        WriteCheckpointObserver?.Invoke(SaveWriteCheckpoint.JsonCommitted);

        TryUpdateIndex();
        TryCleanupOldScreenshots(
            slotDirectory,
            GetSlotBaseName(slot),
            screenshotPath,
            TryGetBackupScreenshotPath(backupPath)
        );
        return document;
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

    public ProfileData LoadProfile()
    {
        if (!File.Exists(_profilePath))
        {
            return new ProfileData();
        }

        try
        {
            var document = JsonSerializer.Deserialize<ProfileFileData>(
                File.ReadAllText(_profilePath),
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

    public void SaveProfile(ProfileData profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Directory.CreateDirectory(
            Path.GetDirectoryName(_profilePath)
                ?? throw new InvalidOperationException("Profile path has no directory.")
        );

        var temporaryPath = _profilePath + $".{Guid.NewGuid():N}.tmp";
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

    private void TryUpdateIndex()
    {
        try
        {
            Directory.CreateDirectory(_saveRoot);
            var entries = EnumerateSlotJsonFiles()
                .Select(path => new SaveIndexEntry
                {
                    Path = Path.GetRelativePath(_saveRoot, path).Replace('\\', '/'),
                    SavedAt = SaveSchema.Deserialize(File.ReadAllText(path)).SavedAt,
                })
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
