using Godot;
using SummerTrip.Content.Catalog;
using SummerTrip.Core.Dialogue;
using SummerTrip.Core.Save;
using SummerTrip.Core.Session;
using SummerTrip.Features.Vn;
using SummerTrip.Features.Vn.Stage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SummerTrip.Tests;

public partial class FullPrologue : Node
{
    private const string DialoguePath = "res://content/dialogue/prologue.dialogue";
    private const string RenpyPath = "res://../game/aki_sisters_prologue.rpy";
    private const string StageScenePath = "res://features/vn/stage/stage.tscn";
    private const string CatalogPath = "res://content/catalog/content_catalog.tres";
    private const int ExpectedLineCount = 100;
    private const int ExpectedChoiceCount = 15;
    private const int ExpectedTitleCount = 16;

    private static readonly RouteExpectation[] Routes =
    [
        new(
            "minoriko_basket_trace",
            [
                "ST_AHW_R001",
                "ST_THM_R001",
                "ST_MTI_R001",
                "ST_RTC_R001",
            ],
            "ST_EMI_L005",
            "ending_minoriko_prologue",
            3,
            0,
            2
        ),
        new(
            "minoriko_listen_basket",
            [
                "ST_AHW_R001",
                "ST_THM_R002",
                "ST_ASL_R002",
                "ST_MTI_R002",
                "ST_RTC_R002",
            ],
            "ST_ESH_L005",
            "ending_shizuha_prologue",
            3,
            2,
            1
        ),
        new(
            "leaf_immediate_crops",
            [
                "ST_AHW_R002",
                "ST_ASL_R001",
                "ST_MTI_R002",
                "ST_RTC_R003",
            ],
            "ST_ESI_L006",
            "ending_sisters_prologue",
            1,
            1,
            3
        ),
        new(
            "leaf_basket_leaves",
            [
                "ST_AHW_R002",
                "ST_ASL_R002",
                "ST_MTI_R003",
                "ST_RTC_R001",
            ],
            "ST_EMI_L005",
            "ending_minoriko_prologue",
            2,
            2,
            2
        ),
        new(
            "bell_festival",
            [
                "ST_AHW_R003",
                "ST_MBL_R001",
                "ST_RTC_R002",
            ],
            "ST_ESH_L005",
            "ending_shizuha_prologue",
            0,
            1,
            1
        ),
        new(
            "bell_direction_trace",
            [
                "ST_AHW_R003",
                "ST_MBL_R002",
                "ST_MTI_R001",
                "ST_RTC_R003",
            ],
            "ST_ESI_L006",
            "ending_sisters_prologue",
            0,
            0,
            4
        ),
    ];

    private readonly HashSet<string> _visitedLineIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _selectedChoiceIds = new(StringComparer.Ordinal);
    private string _testRoot = "";
    private string _saveRoot = "";
    private int _manualSlotIndex = 1;
    private int _screenshotSeed = 1;

    public override async void _Ready()
    {
        try
        {
            _testRoot = ProjectSettings.GlobalizePath("user://m5_full_prologue");
            _saveRoot = Path.Combine(_testRoot, "saves");
            ResetTestDirectory();
            await RunAsync();
            GD.Print(
                "FULL_PROLOGUE_M5_OK routes=6 titles=16 lines=100 choices=15 endings=3 autoslots=6"
            );
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"FULL_PROLOGUE_M5_FAILED: {exception}");
            GetTree().Quit(1);
        }
        finally
        {
            TryCleanupTestDirectory();
        }
    }

    private async Task RunAsync()
    {
        var expectedLineIds = ValidateSourceParity();
        var stageScene = ResourceLoader.Load<PackedScene>(StageScenePath)
            ?? throw new InvalidOperationException("Unable to load the stage scene.");
        var catalog = ResourceLoader.Load<ContentCatalog>(CatalogPath)
            ?? throw new InvalidOperationException("Unable to load the content catalog.");
        var saveService = new SaveService(_saveRoot);

        foreach (var route in Routes)
        {
            await RunRouteAsync(route, stageScene, catalog, saveService);
        }

        if (!_visitedLineIds.SetEquals(expectedLineIds))
        {
            var missing = expectedLineIds.Except(_visitedLineIds, StringComparer.Ordinal);
            throw new InvalidOperationException(
                $"Six routes did not visit every story line: {string.Join(", ", missing)}."
            );
        }

        if (_selectedChoiceIds.Count != ExpectedChoiceCount)
        {
            throw new InvalidOperationException(
                $"Six routes selected {_selectedChoiceIds.Count} unique choices; expected {ExpectedChoiceCount}."
            );
        }

        var persistedProfile = saveService.LoadProfile();
        var expectedEndings = new[]
        {
            "ending_minoriko_prologue",
            "ending_shizuha_prologue",
            "ending_sisters_prologue",
        };
        if (
            !persistedProfile.UnlockedEndings.SetEquals(expectedEndings) ||
            !persistedProfile.ReadLineIds.SetEquals(expectedLineIds)
        )
        {
            throw new InvalidOperationException(
                "Persisted Profile does not contain all story lines and three endings."
            );
        }

        for (var index = 1; index <= SaveService.AutoSlotCount; index++)
        {
            var loaded = saveService.Load(new SaveSlot(SaveSlotKind.Auto, index));
            if (!loaded.ScreenshotExists)
            {
                throw new InvalidOperationException(
                    $"Automatic save slot {index} is missing its screenshot."
                );
            }
        }
    }

    private async Task RunRouteAsync(
        RouteExpectation route,
        PackedScene stageScene,
        ContentCatalog catalog,
        SaveService saveService
    )
    {
        using var session = new GameSession();
        var profile = saveService.LoadProfile();
        var stage = stageScene.Instantiate<StageController>();
        stage.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        stage.Size = new Vector2(1920, 1080);
        var director = new VNDirector();
        AddChild(stage);
        AddChild(director);

        try
        {
            using var gateway = new DialogueGateway(
                DialoguePath,
                new DialogueContext(session, stage),
                "prologue"
            );
            director.Initialize(gateway, session, stage, profile);
            using var progression = new PrologueProgression(
                director,
                profile,
                saveService,
                catalog,
                () => CreateScreenshot(_screenshotSeed++)
            );
            var autosaveLines = new List<string>();
            progression.AutoSaveCompleted += (staticLineId, _) =>
                autosaveLines.Add(staticLineId);

            await director.StartAsync();
            var routeLineIds = new HashSet<string>(StringComparer.Ordinal);
            var choiceCursor = 0;
            for (var step = 0; step < 400; step++)
            {
                if (director.Interaction == InteractionState.Ended)
                {
                    break;
                }

                var line = director.CurrentLine
                    ?? throw new InvalidOperationException(
                        $"Route '{route.Name}' lost its current line."
                    );
                _visitedLineIds.Add(line.StaticId);
                routeLineIds.Add(line.StaticId);

                if (
                    line.StaticId == "ST_MTI_L001" &&
                    (
                        stage.BackgroundId != "autumn_room" ||
                        stage.OverlayId != "mystery_glow"
                    )
                )
                {
                    throw new InvalidOperationException(
                        "D-003 failed: maple tree investigation did not retain autumn_room."
                    );
                }

                if (director.Interaction == InteractionState.Typing)
                {
                    director.CompleteTyping();
                    if (IsManualCheckpoint(line.StaticId, route.FinalStaticLineId))
                    {
                        await RoundTripCheckpointAsync(
                            route.Name,
                            director,
                            stage,
                            saveService,
                            line.StaticId
                        );
                    }
                    continue;
                }

                if (director.Interaction == InteractionState.AwaitingChoice)
                {
                    if (choiceCursor >= route.ChoiceIds.Count)
                    {
                        throw new InvalidOperationException(
                            $"Route '{route.Name}' has no configured choice for '{line.StaticId}'."
                        );
                    }

                    var expectedChoiceId = route.ChoiceIds[choiceCursor++];
                    var choiceIndex = line.Choices
                        .Select((choice, index) => (choice, index))
                        .Single(pair => string.Equals(
                            pair.choice.StaticId,
                            expectedChoiceId,
                            StringComparison.Ordinal
                        ))
                        .index;
                    _selectedChoiceIds.Add(expectedChoiceId);
                    await director.ChooseAsync(choiceIndex);
                    continue;
                }

                if (director.Interaction == InteractionState.AwaitingAdvance)
                {
                    await director.AdvanceAsync();
                    continue;
                }

                throw new InvalidOperationException(
                    $"Route '{route.Name}' entered unsupported state {director.Interaction}."
                );
            }

            if (
                director.Interaction != InteractionState.Ended ||
                choiceCursor != route.ChoiceIds.Count
            )
            {
                throw new InvalidOperationException(
                    $"Route '{route.Name}' did not consume its choices and reach END."
                );
            }

            if (
                session.minoriko_affection != route.MinorikoAffection ||
                session.shizuha_trust != route.ShizuhaTrust ||
                session.autumn_mystery != route.AutumnMystery
            )
            {
                throw new InvalidOperationException(
                    $"Route '{route.Name}' produced variables " +
                    $"{session.minoriko_affection}/{session.shizuha_trust}/" +
                    $"{session.autumn_mystery}, expected {route.MinorikoAffection}/" +
                    $"{route.ShizuhaTrust}/{route.AutumnMystery}."
                );
            }

            if (
                !profile.UnlockedEndings.Contains(route.EndingId) ||
                !saveService.LoadProfile().UnlockedEndings.Contains(route.EndingId)
            )
            {
                throw new InvalidOperationException(
                    $"Route '{route.Name}' did not persist ending '{route.EndingId}'."
                );
            }

            var expectedAutosaveLines = new List<string> { "ST_AHW_L001" };
            if (route.ChoiceIds.Any(id => id.StartsWith("ST_MTI_", StringComparison.Ordinal)))
            {
                expectedAutosaveLines.Add("ST_MTI_L001");
            }
            expectedAutosaveLines.Add("ST_RTC_L001");
            expectedAutosaveLines.Add(route.FinalStaticLineId);
            if (
                progression.LastError is not null ||
                !autosaveLines.SequenceEqual(expectedAutosaveLines, StringComparer.Ordinal)
            )
            {
                throw new InvalidOperationException(
                    $"Route '{route.Name}' autosaves were " +
                    $"[{string.Join(", ", autosaveLines)}], expected " +
                    $"[{string.Join(", ", expectedAutosaveLines)}].",
                    progression.LastError
                );
            }

            if (
                session.History.Count != routeLineIds.Count ||
                session.History.Count > GameSession.MaximumHistoryEntries
            )
            {
                throw new InvalidOperationException(
                    $"Route '{route.Name}' history contains duplicate or excess entries."
                );
            }
        }
        finally
        {
            RemoveChild(director);
            RemoveChild(stage);
            director.QueueFree();
            stage.QueueFree();
        }
    }

    private async Task RoundTripCheckpointAsync(
        string routeName,
        VNDirector director,
        StageController stage,
        SaveService saveService,
        string staticLineId
    )
    {
        var slot = new SaveSlot(SaveSlotKind.Manual, _manualSlotIndex++);
        using var screenshot = CreateScreenshot(_screenshotSeed++);
        var expectedSession = director.Session.CaptureSnapshot();
        var expectedStage = stage.CaptureSnapshot();
        var expectedInteraction = director.Interaction;
        director.Save(saveService, slot, screenshot);
        var loaded = saveService.Load(slot);
        await director.RestoreAsync(loaded.Document, saveService);

        if (
            director.CurrentLine?.StaticId != staticLineId ||
            director.Interaction != expectedInteraction ||
            !SessionMatches(expectedSession, director.Session.CaptureSnapshot()) ||
            !StageMatches(expectedStage, stage.CaptureSnapshot())
        )
        {
            throw new InvalidOperationException(
                $"Route '{routeName}' checkpoint '{staticLineId}' did not round trip."
            );
        }
    }

    private static HashSet<string> ValidateSourceParity()
    {
        var dialogue = Godot.FileAccess.GetFileAsString(DialoguePath);
        var renpy = Godot.FileAccess.GetFileAsString(RenpyPath);
        if (string.IsNullOrWhiteSpace(dialogue) || string.IsNullOrWhiteSpace(renpy))
        {
            throw new InvalidOperationException("Unable to read dialogue parity sources.");
        }

        var godotLines = Regex.Matches(
                dialogue,
                @"^(?<line>.+?)\s+\[ID:(?<id>ST_[A-Z]+_L\d+)\]\s*$",
                RegexOptions.CultureInvariant | RegexOptions.Multiline
            )
            .Select(match => (
                Id: match.Groups["id"].Value,
                Text: StripGodotSpeaker(match.Groups["line"].Value)
            ))
            .ToArray();
        var renpyLines = Regex.Matches(
                renpy,
                @"^\s*(?:n|m|s|p)\s+""(?<text>.*)""\s*$",
                RegexOptions.CultureInvariant | RegexOptions.Multiline
            )
            .Select(match => match.Groups["text"].Value)
            .ToArray();
        var godotChoices = Regex.Matches(
                dialogue,
                @"^- (?<text>.+?)\s+\[ID:ST_[A-Z]+_R\d+\]\s*$",
                RegexOptions.CultureInvariant | RegexOptions.Multiline
            )
            .Select(match => match.Groups["text"].Value)
            .ToArray();
        var renpyChoices = Regex.Matches(
                renpy,
                @"^\s+""(?<text>.*)"":\s*$",
                RegexOptions.CultureInvariant | RegexOptions.Multiline
            )
            .Select(match => match.Groups["text"].Value)
            .ToArray();
        var titleCount = Regex.Matches(
            dialogue,
            @"^~ [a-z0-9_]+\s*$",
            RegexOptions.CultureInvariant | RegexOptions.Multiline
        ).Count;

        if (
            godotLines.Length != ExpectedLineCount ||
            renpyLines.Length != ExpectedLineCount ||
            godotChoices.Length != ExpectedChoiceCount ||
            renpyChoices.Length != ExpectedChoiceCount ||
            titleCount != ExpectedTitleCount ||
            !godotLines.Select(line => line.Text).SequenceEqual(renpyLines) ||
            !godotChoices.SequenceEqual(renpyChoices)
        )
        {
            throw new InvalidOperationException(
                "Godot prologue text or choices differ from the Ren'Py baseline."
            );
        }

        return godotLines.Select(line => line.Id).ToHashSet(StringComparer.Ordinal);
    }

    private static string StripGodotSpeaker(string line)
    {
        foreach (var prefix in new[]
        {
            "秋穰子: ",
            "秋静叶: ",
            "{{GameState.player_name}}: ",
        })
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                return line[prefix.Length..];
            }
        }

        return line;
    }

    private static bool IsManualCheckpoint(string staticLineId, string finalStaticLineId)
    {
        return staticLineId is "ST_AHW_L010" or "ST_MTI_L006" or "ST_RTC_L005"
            || string.Equals(staticLineId, finalStaticLineId, StringComparison.Ordinal);
    }

    private static bool SessionMatches(
        GameSessionSnapshot expected,
        GameSessionSnapshot actual
    )
    {
        return expected.PlayerName == actual.PlayerName
            && expected.MinorikoAffection == actual.MinorikoAffection
            && expected.ShizuhaTrust == actual.ShizuhaTrust
            && expected.AutumnMystery == actual.AutumnMystery
            && expected.CurrentStaticLineId == actual.CurrentStaticLineId
            && expected.History.SequenceEqual(actual.History);
    }

    private static bool StageMatches(StageSnapshot expected, StageSnapshot actual)
    {
        return expected.BackgroundId == actual.BackgroundId
            && expected.OverlayId == actual.OverlayId
            && expected.OverlayAnimationId == actual.OverlayAnimationId
            && expected.TransitionId == actual.TransitionId
            && expected.Characters.SequenceEqual(actual.Characters);
    }

    private static Image CreateScreenshot(int seed)
    {
        var image = Image.CreateEmpty(
            SaveService.ScreenshotWidth,
            SaveService.ScreenshotHeight,
            false,
            Image.Format.Rgba8
        );
        image.Fill(new Color(
            (seed % 7) / 7.0f,
            (seed % 11) / 11.0f,
            (seed % 13) / 13.0f,
            1.0f
        ));
        return image;
    }

    private void ResetTestDirectory()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
        Directory.CreateDirectory(_testRoot);
    }

    private void TryCleanupTestDirectory()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException
        )
        {
            GD.PushWarning($"Unable to clean M5 test directory: {exception.Message}");
        }
    }

    private sealed record RouteExpectation(
        string Name,
        IReadOnlyList<string> ChoiceIds,
        string FinalStaticLineId,
        string EndingId,
        int MinorikoAffection,
        int ShizuhaTrust,
        int AutumnMystery
    );
}
