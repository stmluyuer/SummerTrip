using SummerTrip.Core.Session;
using SummerTrip.Features.Vn.Stage;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SummerTrip.Core.Save;

public sealed class SaveValidationException : Exception
{
    public SaveValidationException(string message)
        : base(message)
    {
    }

    public SaveValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class SaveDocument
{
    public int SchemaVersion { get; set; }
    public string GameVersion { get; set; } = null!;
    public string SavedAt { get; set; } = null!;
    public StorySaveData Story { get; set; } = null!;
    public SessionSaveData Session { get; set; } = null!;
    public PresentationSaveData Presentation { get; set; } = null!;
    public List<HistorySaveEntry> History { get; set; } = null!;
    public List<string> ReadLineIds { get; set; } = null!;
    public double PlayTimeSeconds { get; set; }
    public string ScreenshotPath { get; set; } = null!;
}

public sealed class StorySaveData
{
    public string ResourceId { get; set; } = null!;
    public string ChapterId { get; set; } = null!;
    public string StaticLineId { get; set; } = null!;
}

public sealed class SessionSaveData
{
    public string PlayerName { get; set; } = null!;
    public int MinorikoAffection { get; set; }
    public int ShizuhaTrust { get; set; }
    public int AutumnMystery { get; set; }
    public Dictionary<string, bool> ChapterFlags { get; set; } = null!;
}

public sealed class PresentationSaveData
{
    public string BackgroundId { get; set; } = null!;
    public string OverlayId { get; set; } = null!;
    public string OverlayAnimationId { get; set; } = null!;
    public List<StageCharacterSaveData> Characters { get; set; } = null!;
    public string TransitionId { get; set; } = null!;
    public string? MusicId { get; set; }
    public double MusicPosition { get; set; }
}

public sealed class StageCharacterSaveData
{
    public string CharacterId { get; set; } = null!;
    public string ExpressionId { get; set; } = null!;
    public string PositionId { get; set; } = null!;
    public bool Visible { get; set; } = true;
}

public sealed class HistorySaveEntry
{
    public string StaticLineId { get; set; } = null!;
    public string Character { get; set; } = null!;
    public string Text { get; set; } = null!;
}

public static partial class SaveSchema
{
    public const int CurrentVersion = 1;
    public const string CurrentGameVersion = "0.1.0-prologue";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static SaveDocument Create(
        GameSessionSnapshot session,
        StageSnapshot presentation,
        ProfileSnapshot profile
    )
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(profile);

        return new SaveDocument
        {
            SchemaVersion = CurrentVersion,
            GameVersion = CurrentGameVersion,
            SavedAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            Story = new StorySaveData
            {
                ResourceId = session.StoryResourceId,
                ChapterId = session.ChapterId,
                StaticLineId = session.CurrentStaticLineId,
            },
            Session = new SessionSaveData
            {
                PlayerName = session.PlayerName,
                MinorikoAffection = session.MinorikoAffection,
                ShizuhaTrust = session.ShizuhaTrust,
                AutumnMystery = session.AutumnMystery,
                ChapterFlags = new Dictionary<string, bool>(
                    session.ChapterFlags,
                    StringComparer.Ordinal
                ),
            },
            Presentation = FromSnapshot(presentation),
            History = session.History.Select(entry => new HistorySaveEntry
            {
                StaticLineId = entry.StaticLineId,
                Character = entry.Character,
                Text = entry.Text,
            }).ToList(),
            ReadLineIds = profile.ReadLineIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            PlayTimeSeconds = session.PlayTimeSeconds,
        };
    }

    public static string Serialize(SaveDocument document)
    {
        Validate(document, requireScreenshotPath: true);
        return JsonSerializer.Serialize(document, JsonOptions);
    }

    public static SaveDocument Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new SaveValidationException("Save JSON is empty.");
        }

        try
        {
            var document = JsonSerializer.Deserialize<SaveDocument>(json, JsonOptions)
                ?? throw new SaveValidationException("Save JSON did not contain an object.");
            Validate(document, requireScreenshotPath: true);
            return document;
        }
        catch (SaveValidationException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new SaveValidationException(
                $"Save JSON has an invalid field or value: {exception.Message}",
                exception
            );
        }
    }

    public static void Validate(SaveDocument document, bool requireScreenshotPath)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.SchemaVersion != CurrentVersion)
        {
            throw new SaveValidationException(
                $"Unsupported save schema version {document.SchemaVersion}; expected {CurrentVersion}."
            );
        }

        if (!string.Equals(document.GameVersion, CurrentGameVersion, StringComparison.Ordinal))
        {
            throw new SaveValidationException(
                $"Unsupported game version '{document.GameVersion}'; expected '{CurrentGameVersion}'."
            );
        }

        if (
            !DateTimeOffset.TryParseExact(
                document.SavedAt,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out _
            )
        )
        {
            throw new SaveValidationException("Save field 'saved_at' must be an ISO-8601 timestamp.");
        }

        ValidateRequiredId(document.Story?.ResourceId, "story.resource_id");
        ValidateRequiredId(document.Story?.ChapterId, "story.chapter_id");
        ValidateStaticId(document.Story?.StaticLineId, "story.static_line_id");

        if (document.Session is null)
        {
            throw new SaveValidationException("Save field 'session' is required.");
        }

        if (string.IsNullOrWhiteSpace(document.Session.PlayerName))
        {
            throw new SaveValidationException("Save field 'session.player_name' is required.");
        }

        if (document.Session.ChapterFlags is null)
        {
            throw new SaveValidationException("Save field 'session.chapter_flags' is required.");
        }

        foreach (var flag in document.Session.ChapterFlags.Keys)
        {
            ValidateRequiredId(flag, "session.chapter_flags key");
        }

        ValidatePresentation(document.Presentation);

        if (document.History is null)
        {
            throw new SaveValidationException("Save field 'history' is required.");
        }

        if (document.History.Count > GameSession.MaximumHistoryEntries)
        {
            throw new SaveValidationException(
                $"Save history cannot exceed {GameSession.MaximumHistoryEntries} entries."
            );
        }

        foreach (var entry in document.History)
        {
            if (entry is null)
            {
                throw new SaveValidationException("Save history entries must be objects.");
            }

            ValidateStaticId(entry.StaticLineId, "history.static_line_id");
            if (entry.Character is null || entry.Text is null)
            {
                throw new SaveValidationException(
                    "Save history character and text fields cannot be null."
                );
            }
        }

        if (document.ReadLineIds is null)
        {
            throw new SaveValidationException("Save field 'read_line_ids' is required.");
        }

        foreach (var staticLineId in document.ReadLineIds)
        {
            ValidateStaticId(staticLineId, "read_line_ids");
        }

        if (
            double.IsNaN(document.PlayTimeSeconds) ||
            double.IsInfinity(document.PlayTimeSeconds) ||
            document.PlayTimeSeconds < 0.0
        )
        {
            throw new SaveValidationException(
                "Save field 'play_time_seconds' must be a finite non-negative number."
            );
        }

        if (requireScreenshotPath)
        {
            ValidateScreenshotPath(document.ScreenshotPath);
        }
    }

    public static GameSessionSnapshot ToSessionSnapshot(SaveDocument document)
    {
        Validate(document, requireScreenshotPath: true);
        return new GameSessionSnapshot(
            document.Session.PlayerName,
            document.Session.MinorikoAffection,
            document.Session.ShizuhaTrust,
            document.Session.AutumnMystery,
            document.Story.ResourceId,
            document.Story.ChapterId,
            document.Story.StaticLineId,
            document.History.Select(entry => new DialogueHistoryEntry(
                entry.StaticLineId,
                entry.Character,
                entry.Text
            )).ToArray(),
            document.PlayTimeSeconds,
            new Dictionary<string, bool>(
                document.Session.ChapterFlags,
                StringComparer.Ordinal
            )
        );
    }

    public static StageSnapshot ToStageSnapshot(SaveDocument document)
    {
        Validate(document, requireScreenshotPath: true);
        return new StageSnapshot(
            document.Presentation.BackgroundId,
            document.Presentation.OverlayId,
            document.Presentation.OverlayAnimationId,
            document.Presentation.Characters.Select(character => new StageCharacterSnapshot(
                character.CharacterId,
                character.ExpressionId,
                character.PositionId,
                character.Visible
            )).ToArray(),
            document.Presentation.TransitionId,
            document.Presentation.MusicId,
            document.Presentation.MusicPosition
        );
    }

    private static PresentationSaveData FromSnapshot(StageSnapshot snapshot)
    {
        return new PresentationSaveData
        {
            BackgroundId = snapshot.BackgroundId,
            OverlayId = snapshot.OverlayId,
            OverlayAnimationId = snapshot.OverlayAnimationId,
            Characters = snapshot.Characters.Select(character => new StageCharacterSaveData
            {
                CharacterId = character.CharacterId,
                ExpressionId = character.ExpressionId,
                PositionId = character.PositionId,
                Visible = character.Visible,
            }).ToList(),
            TransitionId = snapshot.TransitionId,
            MusicId = snapshot.MusicId,
            MusicPosition = snapshot.MusicPosition,
        };
    }

    private static void ValidatePresentation(PresentationSaveData? presentation)
    {
        if (presentation is null)
        {
            throw new SaveValidationException("Save field 'presentation' is required.");
        }

        ValidateRequiredId(presentation.BackgroundId, "presentation.background_id");
        ValidateOptionalId(presentation.OverlayId, "presentation.overlay_id");
        ValidateOptionalId(
            presentation.OverlayAnimationId,
            "presentation.overlay_animation_id"
        );
        ValidateRequiredId(presentation.TransitionId, "presentation.transition_id");
        ValidateOptionalId(presentation.MusicId, "presentation.music_id");
        if (
            double.IsNaN(presentation.MusicPosition) ||
            double.IsInfinity(presentation.MusicPosition) ||
            presentation.MusicPosition < 0.0
        )
        {
            throw new SaveValidationException(
                "Save field 'presentation.music_position' must be non-negative."
            );
        }

        if (presentation.Characters is null)
        {
            throw new SaveValidationException("Save field 'presentation.characters' is required.");
        }

        var characterIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var character in presentation.Characters)
        {
            if (character is null)
            {
                throw new SaveValidationException(
                    "Save presentation character entries must be objects."
                );
            }

            ValidateRequiredId(character.CharacterId, "presentation.characters.character_id");
            ValidateRequiredId(character.ExpressionId, "presentation.characters.expression_id");
            ValidateRequiredId(character.PositionId, "presentation.characters.position_id");
            if (!characterIds.Add(character.CharacterId))
            {
                throw new SaveValidationException(
                    $"Duplicate stage character '{character.CharacterId}' in save."
                );
            }
        }
    }

    private static void ValidateScreenshotPath(string? path)
    {
        if (
            string.IsNullOrWhiteSpace(path) ||
            path.Contains('\\', StringComparison.Ordinal) ||
            path.StartsWith("/", StringComparison.Ordinal) ||
            path.Contains("..", StringComparison.Ordinal) ||
            !ScreenshotPathPattern().IsMatch(path)
        )
        {
            throw new SaveValidationException(
                "Save field 'screenshot_path' must be a controlled relative slot PNG path."
            );
        }
    }

    private static void ValidateRequiredId(string? id, string field)
    {
        if (string.IsNullOrWhiteSpace(id) || !SemanticIdPattern().IsMatch(id))
        {
            throw new SaveValidationException(
                $"Save field '{field}' must be a lowercase semantic ID."
            );
        }
    }

    private static void ValidateOptionalId(string? id, string field)
    {
        if (!string.IsNullOrEmpty(id))
        {
            ValidateRequiredId(id, field);
        }
    }

    private static void ValidateStaticId(string? id, string field)
    {
        if (string.IsNullOrWhiteSpace(id) || !StaticIdPattern().IsMatch(id))
        {
            throw new SaveValidationException(
                $"Save field '{field}' must be a stable dialogue ID."
            );
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SemanticIdPattern();

    [GeneratedRegex("^[A-Z][A-Z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex StaticIdPattern();

    [GeneratedRegex(
        "^(manual/slot_[0-9]{3}|quick/slot_001|auto/slot_[0-9]{3})\\.g[0-9]{12,20}\\.png$",
        RegexOptions.CultureInvariant
    )]
    private static partial Regex ScreenshotPathPattern();
}
