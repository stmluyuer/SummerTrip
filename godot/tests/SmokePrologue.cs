using Godot;
using SummerTrip.Core.Dialogue;
using SummerTrip.Core.Session;
using SummerTrip.Features.Vn;
using SummerTrip.Features.Vn.Stage;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SummerTrip.Tests;

public partial class SmokePrologue : Node
{
    private const string DialoguePath = "res://content/dialogue/prologue.dialogue";
    private const string ExpectedDialogueManagerVersion = "3.10.5";
    private const int ExpectedStaticIdCount = 115;
    private const int ExpectedTitleCount = 16;

    private static readonly string[] FirstChoiceLineIds =
    [
        "ST_PRT_L001",
        "ST_PRT_L002",
        "ST_PRT_L003",
        "ST_PRT_L004",
        "ST_PRT_L005",
        "ST_PRT_L006",
        "ST_PRT_L007",
        "ST_PRT_L008",
        "ST_PRT_L009",
        "ST_PRT_L010",
        "ST_PRT_L011",
        "ST_PRT_L012",
        "ST_PRT_L013",
        "ST_PRT_L014",
        "ST_PRT_L015",
        "ST_AHW_L001",
        "ST_AHW_L002",
        "ST_AHW_L003",
        "ST_AHW_L004",
        "ST_AHW_L005",
        "ST_AHW_L006",
        "ST_AHW_L007",
        "ST_AHW_L008",
        "ST_AHW_L009",
        "ST_AHW_L010",
    ];

    private static readonly BranchExpectation[] Branches =
    [
        new(0, "ST_THM_L001", 1, 0, 0),
        new(1, "ST_ASL_L001", 0, 1, 1),
        new(2, "ST_MBL_L001", 0, 0, 1),
    ];

    private static readonly (string Action, int MinimumBindings)[] RequiredInputActions =
    [
        ("vn_advance", 3),
        ("vn_menu", 2),
        ("vn_hide_ui", 1),
        ("vn_skip", 1),
        ("vn_auto", 0),
        ("vn_rollback", 1),
        ("vn_quick_save", 0),
        ("vn_quick_load", 0),
        ("vn_screenshot", 1),
        ("ui_accept", 1),
        ("ui_cancel", 1),
    ];

    public override async void _Ready()
    {
        var exitCode = 1;

        try
        {
            await RunAsync();
            GD.Print(
                $"SMOKE_PROLOGUE_M2_OK titles={ExpectedTitleCount} " +
                $"static_ids={ExpectedStaticIdCount} first_choice_branches={Branches.Length}"
            );
            exitCode = 0;
        }
        catch (Exception exception)
        {
            GD.PushError($"SMOKE_PROLOGUE_M2_FAILED: {exception}");
        }

        GetTree().Quit(exitCode);
    }

    private async Task RunAsync()
    {
        ValidateProjectSettings();
        ValidateFonts();
        var (staticIds, titles) = ValidateDialogueSource();

        foreach (var branch in Branches)
        {
            await ValidateBranchAsync(staticIds, titles, branch);
        }
    }

    private static (string[] StaticIds, string[] Titles) ValidateDialogueSource()
    {
        var source = Godot.FileAccess.GetFileAsString(DialoguePath);
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new InvalidOperationException("The dialogue source is empty.");
        }

        var staticIds = Regex.Matches(
                source,
                @"\[ID:(?<id>[A-Z0-9_]+)\]",
                RegexOptions.CultureInvariant
            )
            .Select(match => match.Groups["id"].Value)
            .ToArray();

        if (staticIds.Length != ExpectedStaticIdCount)
        {
            throw new InvalidOperationException(
                $"Expected {ExpectedStaticIdCount} static IDs, found {staticIds.Length}."
            );
        }

        if (staticIds.Distinct(StringComparer.Ordinal).Count() != staticIds.Length)
        {
            throw new InvalidOperationException("The dialogue source contains duplicate static IDs.");
        }

        var titles = Regex.Matches(
                source,
                @"^~ (?<title>[a-z0-9_]+)\s*$",
                RegexOptions.CultureInvariant | RegexOptions.Multiline
            )
            .Select(match => match.Groups["title"].Value)
            .ToArray();

        if (titles.Length != ExpectedTitleCount)
        {
            throw new InvalidOperationException(
                $"Expected {ExpectedTitleCount} titles, found {titles.Length}."
            );
        }

        if (!source.Contains("风里有泥土、草叶", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The first Chinese line is not intact.");
        }

        return (staticIds, titles);
    }

    private async Task ValidateBranchAsync(
        string[] staticIds,
        string[] titles,
        BranchExpectation expectation
    )
    {
        using var session = new GameSession();
        using var stageScene = ResourceLoader.Load<PackedScene>(
            "res://features/vn/stage/stage.tscn"
        ) ?? throw new InvalidOperationException("Unable to load the stage scene.");
        var stage = stageScene.Instantiate<StageController>();
        stage.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        stage.Size = new Vector2(1920, 1080);
        var director = new VNDirector();
        AddChild(stage);
        AddChild(director);

        try
        {
            var context = new DialogueContext(session, stage);
            using var gateway = new DialogueGateway(DialoguePath, context);

            ValidateCompiledDialogue(gateway, staticIds, titles);
            director.Initialize(gateway, session);
            await director.StartAsync();

            for (var index = 0; index < FirstChoiceLineIds.Length; index++)
            {
                var line = director.CurrentLine
                    ?? throw new InvalidOperationException("Dialogue ended before the first choice.");
                var expectedId = FirstChoiceLineIds[index];

                if (!string.Equals(line.StaticId, expectedId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Expected line '{expectedId}', found '{line.StaticId}'."
                    );
                }

                if (director.Interaction != InteractionState.Typing)
                {
                    throw new InvalidOperationException(
                        $"Line '{expectedId}' did not enter the typing state."
                    );
                }

                if (
                    expectedId == "ST_PRT_L007" &&
                    !string.Equals(line.Character, GameSession.DefaultPlayerName, StringComparison.Ordinal)
                )
                {
                    throw new InvalidOperationException(
                        $"Dynamic player name resolved to '{line.Character}'."
                    );
                }

                var expectedCharacter = expectedId switch
                {
                    "ST_PRT_L001" => "",
                    "ST_PRT_L004" => "秋穰子",
                    "ST_AHW_L003" => "秋静叶",
                    _ => null,
                };
                if (
                    expectedCharacter is not null &&
                    !string.Equals(line.Character, expectedCharacter, StringComparison.Ordinal)
                )
                {
                    throw new InvalidOperationException(
                        $"Line '{expectedId}' resolved character '{line.Character}', " +
                        $"expected '{expectedCharacter}'."
                    );
                }

                director.CompleteTyping();
                var isChoiceLine = index == FirstChoiceLineIds.Length - 1;
                var expectedState = isChoiceLine
                    ? InteractionState.AwaitingChoice
                    : InteractionState.AwaitingAdvance;
                if (director.Interaction != expectedState)
                {
                    throw new InvalidOperationException(
                        $"Line '{expectedId}' entered {director.Interaction}, expected {expectedState}."
                    );
                }

                if (!isChoiceLine)
                {
                    await director.AdvanceAsync();
                }
            }

            await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);
            ValidateStageState(stage);
            ValidateFirstChoices(director);

            var interactionBeforeMenu = director.Interaction;
            director.ToggleMenuOperation();
            if (
                director.Operation != OperationState.Menu ||
                director.Interaction != interactionBeforeMenu
            )
            {
                throw new InvalidOperationException(
                    "Menu operation overwrote the dialogue interaction state."
                );
            }
            director.ToggleMenuOperation();

            await director.AdvanceAsync();
            if (
                director.Interaction != InteractionState.AwaitingChoice ||
                director.CurrentLine?.StaticId != "ST_AHW_L010"
            )
            {
                throw new InvalidOperationException("Advance input bypassed the active choices.");
            }

            await director.ChooseAsync(expectation.ChoiceIndex);
            ValidateBranchResult(session, director, expectation);
        }
        finally
        {
            RemoveChild(director);
            RemoveChild(stage);
            director.QueueFree();
            stage.QueueFree();
        }
    }

    private static void ValidateCompiledDialogue(
        DialogueGateway gateway,
        string[] staticIds,
        string[] titles
    )
    {
        if (!string.Equals(
                gateway.ImportedVersion,
                ExpectedDialogueManagerVersion,
                StringComparison.Ordinal
            ))
        {
            throw new InvalidOperationException(
                $"Expected Dialogue Manager {ExpectedDialogueManagerVersion}, " +
                $"imported resource reports '{gateway.ImportedVersion}'."
            );
        }

        foreach (var title in titles)
        {
            if (!gateway.HasTitle(title))
            {
                throw new InvalidOperationException($"Compiled dialogue is missing title '{title}'.");
            }
        }

        foreach (var staticId in staticIds)
        {
            var runtimeIds = gateway.ResolveStaticId(staticId);
            if (runtimeIds.Count != 1 || !gateway.HasCompiledStaticLine(staticId))
            {
                throw new InvalidOperationException(
                    $"Static ID '{staticId}' did not resolve to one compiled line."
                );
            }
        }
    }

    private static void ValidateStageState(StageController stage)
    {
        if (
            stage.BackgroundId != "autumn_room" ||
            stage.OverlayId != "autumn_glow" ||
            stage.AnimationId != "leaf_shiver" ||
            stage.TransitionId != "slow_dissolve"
        )
        {
            throw new InvalidOperationException("Stage scene state is incorrect at the first choice.");
        }

        if (
            !stage.Characters.ContainsKey("minoriko") ||
            !stage.Characters.ContainsKey("shizuha")
        )
        {
            throw new InvalidOperationException("Stage character state is incomplete.");
        }

        if (stage.BackgroundTexture is null || stage.OverlayAlpha <= 0.0f)
        {
            throw new InvalidOperationException("Stage visual resources were not applied.");
        }

        ValidateCharacterLayout(stage.GetCharacterNode("minoriko"), 0.72f);
        ValidateCharacterLayout(stage.GetCharacterNode("shizuha"), 0.28f);
    }

    private static void ValidateCharacterLayout(TextureRect? character, float expectedX)
    {
        if (
            character is null ||
            !Mathf.IsEqualApprox(character.AnchorLeft, expectedX) ||
            !Mathf.IsEqualApprox(character.AnchorTop, 1.02f) ||
            !character.Scale.IsEqualApprox(Vector2.One * 0.66f) ||
            character.Modulate.A < 0.99f
        )
        {
            throw new InvalidOperationException(
                $"Character layout did not reach x={expectedX}, y=1.02, scale=0.66."
            );
        }
    }

    private static void ValidateFirstChoices(VNDirector director)
    {
        var line = director.CurrentLine
            ?? throw new InvalidOperationException("The first choice line is unavailable.");
        var expectedChoiceIds = new[]
        {
            "ST_AHW_R001",
            "ST_AHW_R002",
            "ST_AHW_R003",
        };
        var actualChoiceIds = line.Choices.Select(choice => choice.StaticId).ToArray();

        if (!actualChoiceIds.SequenceEqual(expectedChoiceIds, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unexpected first choice IDs: {string.Join(", ", actualChoiceIds)}."
            );
        }
    }

    private static void ValidateBranchResult(
        GameSession session,
        VNDirector director,
        BranchExpectation expectation
    )
    {
        if (!string.Equals(
                director.CurrentLine?.StaticId,
                expectation.TargetStaticId,
                StringComparison.Ordinal
            ))
        {
            throw new InvalidOperationException(
                $"Choice {expectation.ChoiceIndex} reached " +
                $"'{director.CurrentLine?.StaticId}', expected '{expectation.TargetStaticId}'."
            );
        }

        if (
            session.minoriko_affection != expectation.MinorikoAffection ||
            session.shizuha_trust != expectation.ShizuhaTrust ||
            session.autumn_mystery != expectation.AutumnMystery
        )
        {
            throw new InvalidOperationException(
                $"Choice {expectation.ChoiceIndex} produced state " +
                $"{session.minoriko_affection}/{session.shizuha_trust}/{session.autumn_mystery}, " +
                $"expected {expectation.MinorikoAffection}/" +
                $"{expectation.ShizuhaTrust}/{expectation.AutumnMystery}."
            );
        }
    }

    private static void ValidateProjectSettings()
    {
        var viewportWidth = ProjectSettings
            .GetSetting("display/window/size/viewport_width")
            .AsInt32();
        var viewportHeight = ProjectSettings
            .GetSetting("display/window/size/viewport_height")
            .AsInt32();
        var stretchMode = ProjectSettings
            .GetSetting("display/window/stretch/mode")
            .AsString();
        var stretchAspect = ProjectSettings
            .GetSetting("display/window/stretch/aspect")
            .AsString();

        if (
            viewportWidth != 1920 ||
            viewportHeight != 1080 ||
            !string.Equals(stretchMode, "canvas_items", StringComparison.Ordinal) ||
            !string.Equals(stretchAspect, "keep", StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException(
                "Expected 1920x1080 canvas_items/keep display settings, found " +
                $"{viewportWidth}x{viewportHeight} {stretchMode}/{stretchAspect}."
            );
        }

        foreach (var (action, minimumBindings) in RequiredInputActions)
        {
            if (!InputMap.HasAction(action))
            {
                throw new InvalidOperationException($"Missing input action '{action}'.");
            }

            var bindingCount = InputMap.ActionGetEvents(action).Count;
            if (bindingCount < minimumBindings)
            {
                throw new InvalidOperationException(
                    $"Input action '{action}' requires at least {minimumBindings} bindings, " +
                    $"found {bindingCount}."
                );
            }
        }
    }

    private static void ValidateFonts()
    {
        var fontPaths = new[]
        {
            "res://assets/fonts/SourceHanSansCN-Regular.otf",
            "res://assets/fonts/SourceHanSansCN-Medium.otf",
            "res://assets/fonts/SourceHanSansCN-Bold.otf",
        };
        var requiredCharacters = "旅人秋穰子静叶，。？！……";

        foreach (var fontPath in fontPaths)
        {
            using var font = ResourceLoader.Load<FontFile>(fontPath)
                ?? throw new InvalidOperationException($"Unable to load font '{fontPath}'.");
            foreach (var character in requiredCharacters)
            {
                if (!font.HasChar(character))
                {
                    throw new InvalidOperationException(
                        $"Font '{fontPath}' is missing required character '{character}'."
                    );
                }
            }
        }
    }

    private sealed record BranchExpectation(
        int ChoiceIndex,
        string TargetStaticId,
        int MinorikoAffection,
        int ShizuhaTrust,
        int AutumnMystery
    );
}
