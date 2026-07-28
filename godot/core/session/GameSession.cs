using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SummerTrip.Core.Session;

public sealed record DialogueHistoryEntry(
    string StaticLineId,
    string Character,
    string Text
);

public sealed record GameSessionSnapshot(
    string PlayerName,
    int MinorikoAffection,
    int ShizuhaTrust,
    int AutumnMystery,
    string StoryResourceId,
    string ChapterId,
    string CurrentStaticLineId,
    IReadOnlyList<DialogueHistoryEntry> History,
    double PlayTimeSeconds,
    IReadOnlyDictionary<string, bool> ChapterFlags
);

public partial class GameSession : RefCounted
{
    public const string DefaultPlayerName = "旅人";
    public const int MaximumHistoryEntries = 250;

    [Export]
    public string player_name { get; set; } = DefaultPlayerName;

    [Export]
    public int minoriko_affection { get; set; }

    [Export]
    public int shizuha_trust { get; set; }

    [Export]
    public int autumn_mystery { get; set; }

    [Export]
    public string CurrentStaticLineId { get; set; } = "";

    public string StoryResourceId { get; set; } = "prologue";
    public string ChapterId { get; set; } = "prologue";
    public double PlayTimeSeconds { get; set; }
    public List<DialogueHistoryEntry> History { get; } = new();
    public Dictionary<string, bool> ChapterFlags { get; } = new(StringComparer.Ordinal);

    public GameSessionSnapshot CaptureSnapshot()
    {
        return new GameSessionSnapshot(
            player_name,
            minoriko_affection,
            shizuha_trust,
            autumn_mystery,
            StoryResourceId,
            ChapterId,
            CurrentStaticLineId,
            History.ToArray(),
            PlayTimeSeconds,
            new Dictionary<string, bool>(ChapterFlags, StringComparer.Ordinal)
        );
    }

    public void RestoreSnapshot(GameSessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateSnapshot(snapshot);

        player_name = snapshot.PlayerName;
        minoriko_affection = snapshot.MinorikoAffection;
        shizuha_trust = snapshot.ShizuhaTrust;
        autumn_mystery = snapshot.AutumnMystery;
        StoryResourceId = snapshot.StoryResourceId;
        ChapterId = snapshot.ChapterId;
        CurrentStaticLineId = snapshot.CurrentStaticLineId;
        PlayTimeSeconds = snapshot.PlayTimeSeconds;

        History.Clear();
        History.AddRange(snapshot.History.TakeLast(MaximumHistoryEntries));
        ChapterFlags.Clear();
        foreach (var (key, value) in snapshot.ChapterFlags)
        {
            ChapterFlags[key] = value;
        }
    }

    public void RecordLine(string staticLineId, string character, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(staticLineId);
        History.Add(new DialogueHistoryEntry(staticLineId, character, text));
        if (History.Count > MaximumHistoryEntries)
        {
            History.RemoveRange(0, History.Count - MaximumHistoryEntries);
        }
    }

    public void Reset()
    {
        player_name = DefaultPlayerName;
        minoriko_affection = 0;
        shizuha_trust = 0;
        autumn_mystery = 0;
        StoryResourceId = "prologue";
        ChapterId = "prologue";
        CurrentStaticLineId = "";
        PlayTimeSeconds = 0.0;
        History.Clear();
        ChapterFlags.Clear();
    }

    private static void ValidateSnapshot(GameSessionSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.PlayerName))
        {
            throw new ArgumentException("Session player name is required.", nameof(snapshot));
        }

        if (
            string.IsNullOrWhiteSpace(snapshot.StoryResourceId) ||
            string.IsNullOrWhiteSpace(snapshot.ChapterId) ||
            string.IsNullOrWhiteSpace(snapshot.CurrentStaticLineId)
        )
        {
            throw new ArgumentException(
                "Session story resource, chapter, and static line IDs are required.",
                nameof(snapshot)
            );
        }

        if (
            double.IsNaN(snapshot.PlayTimeSeconds) ||
            double.IsInfinity(snapshot.PlayTimeSeconds) ||
            snapshot.PlayTimeSeconds < 0.0
        )
        {
            throw new ArgumentException(
                "Session play time must be a finite non-negative number.",
                nameof(snapshot)
            );
        }

        if (snapshot.History.Count > MaximumHistoryEntries)
        {
            throw new ArgumentException(
                $"Session history cannot exceed {MaximumHistoryEntries} entries.",
                nameof(snapshot)
            );
        }
    }
}
