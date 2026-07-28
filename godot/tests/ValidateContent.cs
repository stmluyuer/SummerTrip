using Godot;
using SummerTrip.Content.Catalog;
using SummerTrip.Features.Vn.Stage;
using System;
using System.Threading.Tasks;

namespace SummerTrip.Tests;

public partial class ValidateContent : Node
{
    public override async void _Ready()
    {
        var exitCode = 1;
        try
        {
            await RunAsync();
            GD.Print(
                "VALIDATE_CONTENT_OK backgrounds=2 characters=2 positions=2 overlays=2 ui=4"
            );
            exitCode = 0;
        }
        catch (Exception exception)
        {
            GD.PushError($"VALIDATE_CONTENT_FAILED: {exception}");
        }

        GetTree().Quit(exitCode);
    }

    private async Task RunAsync()
    {
        using var catalog = ResourceLoader.Load<ContentCatalog>(
            "res://content/catalog/content_catalog.tres"
        ) ?? throw new InvalidOperationException("Unable to load ContentCatalog.");

        var errors = catalog.Validate();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(System.Environment.NewLine, errors));
        }

        ValidateParameters(catalog);
        ValidateUnknownIds(catalog);
        ValidateDialogueSemantics();
        await ValidateStageAsync();
    }

    private static void ValidateParameters(ContentCatalog catalog)
    {
        var right = catalog.GetPosition("right");
        var left = catalog.GetPosition("left");
        var shiver = catalog.GetAnimation("leaf_shiver")
            ?? throw new InvalidOperationException("leaf_shiver is missing.");
        var dissolve = catalog.GetTransition("slow_dissolve");
        var blend = catalog.GetTransition("scene_blend");

        RequireApprox(right.X, 0.72f, "right.x");
        RequireApprox(left.X, 0.28f, "left.x");
        RequireApprox(right.Y, 1.02f, "right.y");
        RequireApprox(left.Y, 1.02f, "left.y");
        RequireApprox(right.Scale, 0.66f, "right.scale");
        RequireApprox(left.Scale, 0.66f, "left.scale");
        RequireApprox(right.EnterOffsetX, 64.0f, "right.enter_offset_x");
        RequireApprox(left.EnterOffsetX, -64.0f, "left.enter_offset_x");
        RequireApprox(right.EnterDuration, 0.28f, "right.enter_duration");
        RequireApprox(shiver.InitialDuration, 0.25f, "leaf_shiver.initial_duration");
        RequireApprox(shiver.InitialAlpha, 0.22f, "leaf_shiver.initial_alpha");
        RequireApprox(shiver.CycleLegDuration, 1.6f, "leaf_shiver.cycle_leg_duration");
        RequireApprox(shiver.CycleMinAlpha, 0.12f, "leaf_shiver.cycle_min_alpha");
        RequireApprox(shiver.CycleMaxAlpha, 0.24f, "leaf_shiver.cycle_max_alpha");
        RequireApprox(dissolve.Duration, 0.85f, "slow_dissolve.duration");
        RequireApprox(blend.FadeOut, 0.35f, "scene_blend.fade_out");
        RequireApprox(blend.Hold, 0.2f, "scene_blend.hold");
        RequireApprox(blend.FadeIn, 0.55f, "scene_blend.fade_in");
    }

    private static void ValidateUnknownIds(ContentCatalog catalog)
    {
        RequireUnknown(() => catalog.GetBackground("unknown"), "background");
        RequireUnknown(() => catalog.GetCharacter("minoriko", "unknown"), "character");
        RequireUnknown(() => catalog.GetPosition("unknown"), "position");
        RequireUnknown(() => catalog.GetUiTexture("unknown"), "UI texture");
        RequireUnknown(() => catalog.GetOverlay("unknown"), "overlay");
        RequireUnknown(() => catalog.GetAnimation("unknown"), "animation");
        RequireUnknown(() => catalog.GetTransition("unknown"), "transition");
    }

    private static void ValidateDialogueSemantics()
    {
        var source = Godot.FileAccess.GetFileAsString(
            "res://content/dialogue/prologue.dialogue"
        );
        if (
            source.Contains("res://", StringComparison.Ordinal) ||
            source.Contains("$Stage", StringComparison.Ordinal) ||
            source.Contains("1920", StringComparison.Ordinal) ||
            source.Contains("1080", StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException(
                "Dialogue contains a resource path, node path, or pixel coordinate."
            );
        }
    }

    private async Task ValidateStageAsync()
    {
        using var stageScene = ResourceLoader.Load<PackedScene>(
            "res://features/vn/stage/stage.tscn"
        ) ?? throw new InvalidOperationException("Unable to load stage.tscn.");
        var stage = stageScene.Instantiate<StageController>();
        stage.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        stage.Size = new Vector2(1920, 1080);
        AddChild(stage);

        try
        {
            await stage.change_scene(
                "autumn_garden",
                "autumn_glow",
                "leaf_shiver",
                "none"
            );
            stage.show_character("minoriko", "neutral", "right");
            stage.show_character("shizuha", "neutral", "left");
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);

            if (
                stage.BackgroundTexture is null ||
                stage.GetCharacterNode("minoriko") is null ||
                stage.GetCharacterNode("shizuha") is null
            )
            {
                throw new InvalidOperationException("Stage did not resolve visual resources.");
            }

            var previousBackground = stage.GetNode<TextureRect>("%PreviousBackground");
            var dissolve = stage.change_scene("autumn_room", "", "", "slow_dissolve");
            await ToSignal(GetTree().CreateTimer(0.05), SceneTreeTimer.SignalName.Timeout);
            if (!previousBackground.Visible)
            {
                throw new InvalidOperationException(
                    "slow_dissolve did not preserve the previous background."
                );
            }
            await dissolve;
            if (
                stage.Characters.Count != 0 ||
                stage.GetCharacterNode("minoriko") is not null ||
                stage.OverlayVisible ||
                previousBackground.Visible
            )
            {
                throw new InvalidOperationException(
                    "change_scene did not clear old characters or overlay animation."
                );
            }
        }
        finally
        {
            RemoveChild(stage);
            stage.QueueFree();
        }
    }

    private static void RequireUnknown(Action action, string kind)
    {
        try
        {
            action();
        }
        catch (ArgumentException exception) when (
            exception.Message.Contains("Unknown content catalog", StringComparison.Ordinal)
        )
        {
            return;
        }

        throw new InvalidOperationException($"Unknown {kind} ID did not fail clearly.");
    }

    private static void RequireApprox(float actual, float expected, string name)
    {
        if (!Mathf.IsEqualApprox(actual, expected))
        {
            throw new InvalidOperationException(
                $"{name} is {actual}, expected {expected}."
            );
        }
    }
}
