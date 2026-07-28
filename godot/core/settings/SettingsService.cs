using Godot;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SummerTrip.Core.Settings;

public sealed class SettingsData
{
    public bool Fullscreen { get; set; }
    public float TextCharactersPerSecond { get; set; } = 48.0f;
    public float AutoWaitSeconds { get; set; } = 2.0f;
    public bool SkipUnreadText { get; set; }
    public bool KeepSkippingAfterChoice { get; set; }
    public float MusicVolume { get; set; } = 1.0f;
    public float SoundVolume { get; set; } = 1.0f;
    public float VoiceVolume { get; set; } = 1.0f;
    public bool MuteAll { get; set; }
}

public partial class SettingsService : Node
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public SettingsData Current { get; private set; } = new();
    public string? LastLoadWarning { get; private set; }
    public string StoragePath { get; set; } = "user://settings.json";

    public override void _Ready()
    {
        Load();
        Apply();
    }

    public void Load()
    {
        LastLoadWarning = null;
        var path = Globalize(StoragePath);
        if (!File.Exists(path))
        {
            Current = new SettingsData();
            return;
        }

        try
        {
            Current = JsonSerializer.Deserialize<SettingsData>(
                File.ReadAllText(path),
                JsonOptions
            ) ?? throw new InvalidDataException("设置文件不是有效对象。");
            Validate(Current);
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is JsonException ||
            exception is InvalidDataException
        )
        {
            Current = new SettingsData();
            LastLoadWarning = $"设置文件无效，已恢复默认值：{exception.Message}";
            GD.PushWarning(LastLoadWarning);
        }
    }

    public void SaveAndApply()
    {
        Validate(Current);
        var path = Globalize(StoragePath);
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("设置文件路径没有父目录。");
        Directory.CreateDirectory(directory);
        var temporaryPath = path + $".{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(Current, JsonOptions));
        _ = JsonSerializer.Deserialize<SettingsData>(
            File.ReadAllText(temporaryPath),
            JsonOptions
        ) ?? throw new InvalidDataException("临时设置文件不是有效对象。");
        if (File.Exists(path))
        {
            File.Replace(temporaryPath, path, path + ".bak", ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(temporaryPath, path);
        }

        Apply();
    }

    public void ResetToDefaults()
    {
        Current = new SettingsData();
        SaveAndApply();
    }

    public void Apply()
    {
        if (!string.Equals(DisplayServer.GetName(), "headless", StringComparison.OrdinalIgnoreCase))
        {
            DisplayServer.WindowSetMode(
                Current.Fullscreen
                    ? DisplayServer.WindowMode.Fullscreen
                    : DisplayServer.WindowMode.Windowed
            );
        }

        ApplyBusVolume("Music", Current.MusicVolume);
        ApplyBusVolume("Sound", Current.SoundVolume);
        ApplyBusVolume("Voice", Current.VoiceVolume);
    }

    private void ApplyBusVolume(string busName, float linearVolume)
    {
        var busIndex = AudioServer.GetBusIndex(busName);
        if (busIndex < 0)
        {
            return;
        }

        AudioServer.SetBusMute(busIndex, Current.MuteAll);
        AudioServer.SetBusVolumeDb(
            busIndex,
            linearVolume <= 0.0001f
                ? -80.0f
                : Mathf.LinearToDb(linearVolume)
        );
    }

    private static void Validate(SettingsData settings)
    {
        if (
            settings.TextCharactersPerSecond is < 10.0f or > 120.0f ||
            settings.AutoWaitSeconds is < 0.5f or > 5.0f ||
            settings.MusicVolume is < 0.0f or > 1.0f ||
            settings.SoundVolume is < 0.0f or > 1.0f ||
            settings.VoiceVolume is < 0.0f or > 1.0f
        )
        {
            throw new InvalidDataException("设置值超出允许范围。");
        }
    }

    private static string Globalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return path.StartsWith("user://", StringComparison.Ordinal)
            ? ProjectSettings.GlobalizePath(path)
            : Path.GetFullPath(path);
    }
}
