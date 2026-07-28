using Godot;
using SummerTrip.Core.Save;
using SummerTrip.Core.Session;
using SummerTrip.Core.Settings;
using SummerTrip.Features.Vn;
using SummerTrip.Features.Vn.Ui;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SummerTrip.Tests;

public partial class M7AvgFeatures : Node
{
    private readonly string _testRoot = ProjectSettings.GlobalizePath(
        "user://m7_avg_features"
    );
    private readonly List<string> _notifications = new();

    public override async void _Ready()
    {
        VNMode? mode = null;
        SettingsService? settings = null;
        var originalKeepSkipping = false;
        try
        {
            ResetTestDirectory();
            var saveRoot = Path.Combine(_testRoot, "saves");
            var screenshotsRoot = Path.Combine(_testRoot, "screenshots");
            SeedReadProfile(saveRoot);

            mode = GD.Load<PackedScene>(
                "res://features/vn/vn_mode.tscn"
            ).Instantiate<VNMode>();
            mode.SaveRoot = saveRoot;
            mode.ScreenshotRoot = screenshotsRoot;
            mode.NotificationRequested += _notifications.Add;
            var initialized = new TaskCompletionSource();
            mode.Initialized += () => initialized.TrySetResult();
            mode.InitializationFailed += message =>
                initialized.TrySetException(new InvalidOperationException(message));
            AddChild(mode);
            await initialized.Task;

            settings = GetNode<SettingsService>("/root/SettingsService");
            originalKeepSkipping = settings.Current.KeepSkippingAfterChoice;
            settings.Current.KeepSkippingAfterChoice = false;

            await ValidateReadSkipBoundaryAsync(mode);
            ValidateUiHiding(mode);
            await ValidateAutoPlaybackAsync(mode);
            await WalkToFirstChoiceAsync(mode.Director);
            ValidateAutoPausesAtChoice(mode);
            await ValidateRollbackAndQuickIoAsync(mode, saveRoot, settings);
            ValidateScreenshot(mode, screenshotsRoot);
            ValidateQuickMenuAndBindings(mode);

            GD.Print(
                "M7_AVG_FEATURES_OK unread_stop=true auto_choice_pause=true " +
                "quick_slots=1 rollback_memory=true quick_menu=8 screenshot=true"
            );
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"M7_AVG_FEATURES_FAILED: {exception}");
            GetTree().Quit(1);
        }
        finally
        {
            if (settings is not null)
            {
                settings.Current.KeepSkippingAfterChoice = originalKeepSkipping;
            }
            if (mode is not null)
            {
                mode.NotificationRequested -= _notifications.Add;
                RemoveChild(mode);
                mode.QueueFree();
            }
            TryCleanup();
        }
    }

    private async Task ValidateReadSkipBoundaryAsync(VNMode mode)
    {
        if (
            mode.Director.CurrentLine?.StaticId != "ST_PRT_L001" ||
            !mode.Director.CurrentLineWasRead
        )
        {
            throw new InvalidOperationException(
                "The seeded first line was not recognized as previously read."
            );
        }

        mode.ToggleSkip();
        await PumpUntilAsync(
            mode,
            () => mode.Director.CurrentLine?.StaticId == "ST_PRT_L003" &&
                !mode.IsSkipToggled,
            160
        );
        if (
            mode.Director.Interaction != InteractionState.Typing ||
            mode.Director.CurrentLineWasRead ||
            !_notifications.Any(message => message.Contains("未读对白", StringComparison.Ordinal))
        )
        {
            throw new InvalidOperationException(
                "Skip did not stop before consuming the first unread line."
            );
        }
    }

    private static void ValidateUiHiding(VNMode mode)
    {
        var staticId = mode.Director.CurrentLine?.StaticId;
        mode.ToggleUiVisibility();
        if (!mode.IsUiHidden)
        {
            throw new InvalidOperationException("Hiding the dialogue UI did not change state.");
        }
        mode.ToggleUiVisibility();
        if (
            mode.IsUiHidden ||
            mode.Director.CurrentLine?.StaticId != staticId
        )
        {
            throw new InvalidOperationException(
                "Restoring the dialogue UI changed the story position."
            );
        }
    }

    private async Task ValidateAutoPlaybackAsync(VNMode mode)
    {
        mode.Director.CompleteCurrentTyping();
        mode.ToggleAuto();
        await PumpUntilAsync(
            mode,
            () => mode.Director.CurrentLine?.StaticId == "ST_PRT_L004",
            80,
            0.25
        );
        if (!mode.IsAutoPlaying)
        {
            throw new InvalidOperationException(
                "Auto play stopped without user input after advancing a line."
            );
        }
        mode.ToggleAuto();
    }

    private static void ValidateAutoPausesAtChoice(VNMode mode)
    {
        var staticId = mode.Director.CurrentLine?.StaticId;
        mode.ToggleAuto();
        for (var index = 0; index < 12; index++)
        {
            mode._Process(1.0);
        }
        if (
            mode.Director.Interaction != InteractionState.AwaitingChoice ||
            mode.Director.CurrentLine?.StaticId != staticId ||
            !mode.IsAutoPlaying
        )
        {
            throw new InvalidOperationException(
                "Auto play did not remain paused at the choice."
            );
        }
        mode.ToggleAuto();
    }

    private async Task ValidateRollbackAndQuickIoAsync(
        VNMode mode,
        string saveRoot,
        SettingsService settings
    )
    {
        await mode.QuickLoadAsync();
        if (
            !_notifications.Any(message =>
                message.Contains("没有可读取的快速存档", StringComparison.Ordinal)
            )
        )
        {
            throw new InvalidOperationException(
                "Missing quick load did not produce a friendly notification."
            );
        }

        var choiceState = mode.Director.CaptureRollbackSnapshot();
        await mode.QuickSaveAsync();
        var quickSummary = mode.Saves.Inspect(
            new SaveSlot(SaveSlotKind.Quick, 1)
        );
        if (quickSummary.Status != SaveSlotStatus.Valid)
        {
            throw new InvalidOperationException("Quick save did not create its dedicated slot.");
        }

        mode.ToggleSkip();
        await mode.ChooseResponseAsync(0);
        if (mode.IsSkipToggled)
        {
            throw new InvalidOperationException(
                "Skip remained enabled after a choice when the setting was disabled."
            );
        }
        mode.Director.CompleteCurrentTyping();
        var changedState = mode.Director.CaptureRollbackSnapshot();
        if (
            changedState.Session.MinorikoAffection !=
                choiceState.Session.MinorikoAffection + 1
        )
        {
            throw new InvalidOperationException(
                "The choice mutation was not present before rollback."
            );
        }

        var filesBeforeRollback = CountFiles(saveRoot);
        if (!await mode.RollbackAsync())
        {
            throw new InvalidOperationException("Rollback was unavailable after a new line.");
        }
        var restored = mode.Director.CaptureRollbackSnapshot();
        AssertRollbackState(choiceState, restored);
        if (CountFiles(saveRoot) != filesBeforeRollback)
        {
            throw new InvalidOperationException(
                "Rollback wrote to or removed a disk save file."
            );
        }

        settings.Current.KeepSkippingAfterChoice = true;
        mode.ToggleSkip();
        await mode.ChooseResponseAsync(0);
        if (!mode.IsSkipToggled)
        {
            throw new InvalidOperationException(
                "Skip did not remain enabled after a choice when configured."
            );
        }
        mode.ToggleSkip();
        mode.Director.CompleteCurrentTyping();

        await mode.QuickLoadAsync();
        var quickRestored = mode.Director.CaptureRollbackSnapshot();
        AssertRollbackState(choiceState, quickRestored);
        if (mode.RollbackSnapshotCount != 1)
        {
            throw new InvalidOperationException(
                "Disk load did not reset the in-memory rollback history."
            );
        }
    }

    private static void ValidateScreenshot(VNMode mode, string screenshotRoot)
    {
        mode.TakeScreenshot();
        var screenshot = Directory.Exists(screenshotRoot)
            ? Directory.EnumerateFiles(screenshotRoot, "screenshot_*.png").SingleOrDefault()
            : null;
        if (screenshot is null)
        {
            throw new InvalidOperationException("Screenshot input did not create a PNG.");
        }
        using var image = Image.LoadFromFile(screenshot);
        if (image is null || image.IsEmpty())
        {
            throw new InvalidOperationException("The saved screenshot is unreadable.");
        }
    }

    private static void ValidateQuickMenuAndBindings(VNMode mode)
    {
        var quickMenu = mode.GetNode<QuickMenu>("%QuickMenu");
        if (quickMenu.FindChildren("*", "Button", true, false).Count != 8)
        {
            throw new InvalidOperationException(
                "The quick menu does not expose all eight required actions."
            );
        }

        foreach (var action in new[]
        {
            "vn_hide_ui",
            "vn_skip",
            "vn_auto",
            "vn_rollback",
            "vn_quick_save",
            "vn_quick_load",
            "vn_screenshot",
        })
        {
            if (!InputMap.HasAction(action) || InputMap.ActionGetEvents(action).Count == 0)
            {
                throw new InvalidOperationException(
                    $"Input action '{action}' has no default binding."
                );
            }
        }
    }

    private async Task PumpUntilAsync(
        VNMode mode,
        Func<bool> condition,
        int maximumFrames,
        double delta = 0.1
    )
    {
        for (var frame = 0; frame < maximumFrames; frame++)
        {
            mode._Process(delta);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (condition())
            {
                return;
            }
        }
        throw new InvalidOperationException("Timed out waiting for an AVG state transition.");
    }

    private static async Task WalkToFirstChoiceAsync(VNDirector director)
    {
        for (var step = 0; step < 40; step++)
        {
            if (director.Interaction == InteractionState.Typing)
            {
                director.CompleteCurrentTyping();
            }
            if (director.Interaction == InteractionState.AwaitingChoice)
            {
                return;
            }
            if (director.Interaction == InteractionState.AwaitingAdvance)
            {
                await director.AdvanceAsync();
                continue;
            }
            throw new InvalidOperationException(
                $"Cannot walk to a choice from {director.Interaction}."
            );
        }
        throw new InvalidOperationException("The first choice was not reached.");
    }

    private static void AssertRollbackState(
        VNRollbackSnapshot expected,
        VNRollbackSnapshot actual
    )
    {
        if (
            expected.Session.CurrentStaticLineId != actual.Session.CurrentStaticLineId ||
            expected.Session.MinorikoAffection != actual.Session.MinorikoAffection ||
            expected.Session.ShizuhaTrust != actual.Session.ShizuhaTrust ||
            expected.Session.AutumnMystery != actual.Session.AutumnMystery ||
            expected.Session.History.Count != actual.Session.History.Count ||
            expected.Stage.BackgroundId != actual.Stage.BackgroundId ||
            expected.Stage.OverlayId != actual.Stage.OverlayId ||
            expected.Stage.TransitionId != actual.Stage.TransitionId ||
            !expected.Stage.Characters.SequenceEqual(actual.Stage.Characters)
        )
        {
            throw new InvalidOperationException(
                "Rollback did not restore session, stage, history, and current line together."
            );
        }
    }

    private static int CountFiles(string root)
    {
        return Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Count()
            : 0;
    }

    private static void SeedReadProfile(string saveRoot)
    {
        var profile = new ProfileData();
        profile.ReadLineIds.Add("ST_PRT_L001");
        profile.ReadLineIds.Add("ST_PRT_L002");
        new SaveService(saveRoot).SaveProfile(profile);
    }

    private void ResetTestDirectory()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
        Directory.CreateDirectory(_testRoot);
    }

    private void TryCleanup()
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
            GD.PushWarning($"Unable to clean M7 test directory: {exception.Message}");
        }
    }
}
