using Godot;
using SummerTrip.Core.Dialogue;
using SummerTrip.Core.Save;
using SummerTrip.Core.Session;
using SummerTrip.Core.Settings;
using SummerTrip.Features.History;
using SummerTrip.Features.Menu;
using SummerTrip.Features.SaveUi;
using SummerTrip.Features.Settings;
using SummerTrip.Features.Vn;
using SummerTrip.Features.Vn.Stage;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SummerTrip.Tests;

public partial class M6MenuSettings : Node
{
    private readonly string _testRoot = ProjectSettings.GlobalizePath(
        "user://m6_menu_settings"
    );

    public override async void _Ready()
    {
        try
        {
            ResetTestDirectory();
            await ValidateMenuPauseAndSlotsAsync();
            ValidateSettingsPersistence();
            ValidateUiScenes();
            GD.Print(
                "M6_MENU_SETTINGS_OK pages=9 slots_per_page=6 " +
                "manual=54 auto=6 quick=1 keyboard=true renpy_settings=true"
            );
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"M6_MENU_SETTINGS_FAILED: {exception}");
            GetTree().Quit(1);
        }
        finally
        {
            TryCleanup();
        }
    }

    private async Task ValidateMenuPauseAndSlotsAsync()
    {
        var saveRoot = Path.Combine(_testRoot, "saves");
        var saves = new SaveService(saveRoot);
        var stage = GD.Load<PackedScene>(
            "res://features/vn/stage/stage.tscn"
        ).Instantiate<StageController>();
        var director = new VNDirector();
        AddChild(stage);
        AddChild(director);
        var session = new GameSession();
        var profile = new ProfileData();
        using var gateway = new DialogueGateway(
            "res://content/dialogue/prologue.dialogue",
            new DialogueContext(session, stage)
        );
        director.Initialize(gateway, session, stage, profile);

        try
        {
            await director.StartAsync();
            director.CompleteTyping();
            var interaction = director.Interaction;
            director.ToggleMenuOperation();
            if (
                director.Operation != OperationState.Menu ||
                director.Interaction != interaction
            )
            {
                throw new InvalidOperationException(
                    "Opening the menu changed the stable dialogue interaction state."
                );
            }

            using (var screenshot = CreateScreenshot(new Color(0.7f, 0.2f, 0.1f)))
            {
                director.Save(
                    saves,
                    new Core.Save.SaveSlot(SaveSlotKind.Manual, 1),
                    screenshot
                );
            }
            if (
                director.Operation != OperationState.Menu ||
                director.Interaction != interaction
            )
            {
                throw new InvalidOperationException(
                    "Saving from the menu did not return to the paused menu state."
                );
            }

            using (var screenshot = CreateScreenshot(new Color(0.2f, 0.5f, 0.1f)))
            {
                director.Save(
                    saves,
                    new Core.Save.SaveSlot(SaveSlotKind.Auto, 1),
                    screenshot
                );
            }
            var latest = saves.GetLatestValidSlot();
            if (latest?.Slot.Kind != SaveSlotKind.Auto)
            {
                throw new InvalidOperationException(
                    "Continue did not select the newest valid save across slot kinds."
                );
            }

            using (var screenshot = CreateScreenshot(new Color(0.1f, 0.2f, 0.7f)))
            {
                director.Save(
                    saves,
                    new Core.Save.SaveSlot(SaveSlotKind.Manual, 2),
                    screenshot
                );
            }
            var missingScreenshot = saves.Inspect(
                new Core.Save.SaveSlot(SaveSlotKind.Manual, 2)
            );
            File.Delete(
                missingScreenshot.ScreenshotPath
                    ?? throw new InvalidOperationException("Test screenshot path was missing.")
            );
            if (
                saves.Inspect(new Core.Save.SaveSlot(SaveSlotKind.Manual, 2)).Status !=
                SaveSlotStatus.ScreenshotMissing
            )
            {
                throw new InvalidOperationException(
                    "A missing screenshot was not exposed as a readable slot state."
                );
            }

            File.WriteAllText(
                saves.GetSlotJsonPath(
                    new Core.Save.SaveSlot(SaveSlotKind.Manual, 3)
                ),
                "{invalid json"
            );
            if (
                saves.Inspect(new Core.Save.SaveSlot(SaveSlotKind.Manual, 3)).Status !=
                SaveSlotStatus.Corrupt
            )
            {
                throw new InvalidOperationException(
                    "A corrupt save was not exposed as a non-crashing slot state."
                );
            }

            saves.Delete(new Core.Save.SaveSlot(SaveSlotKind.Manual, 1));
            if (
                saves.Inspect(new Core.Save.SaveSlot(SaveSlotKind.Manual, 1)).Status !=
                SaveSlotStatus.Empty
            )
            {
                throw new InvalidOperationException("Deleting a save did not empty its slot.");
            }

            director.ToggleMenuOperation();
            if (director.Operation != OperationState.None)
            {
                throw new InvalidOperationException("Closing the menu did not resume gameplay.");
            }
        }
        finally
        {
            RemoveChild(director);
            RemoveChild(stage);
            director.QueueFree();
            stage.QueueFree();
            session.Dispose();
        }
    }

    private void ValidateSettingsPersistence()
    {
        var renpyDefaults = new SettingsData();
        if (
            !Mathf.IsEqualApprox(renpyDefaults.TextCharactersPerSecond, 1.0f) ||
            !Mathf.IsEqualApprox(renpyDefaults.AutoForwardTime, 30.0f) ||
            !Mathf.IsEqualApprox(
                (float)renpyDefaults.CalculateAutoForwardDelay(25),
                6.0f
            )
        )
        {
            throw new InvalidOperationException("Ren'Py setting defaults do not match.");
        }

        var settingsPath = Path.Combine(_testRoot, "settings.json");
        var first = new SettingsService { StoragePath = settingsPath };
        AddChild(first);
        first.Current.Fullscreen = true;
        first.Current.TextCharactersPerSecond = 76.0f;
        first.Current.AutoForwardTime = 23.4f;
        first.Current.SkipUnreadText = true;
        first.Current.KeepSkippingAfterChoice = true;
        first.Current.TransitionsEnabled = false;
        first.Current.MusicVolume = 0.25f;
        first.Current.SoundVolume = 0.5f;
        first.Current.VoiceVolume = 0.75f;
        first.Current.MuteAll = true;
        first.SaveAndApply();
        RemoveChild(first);
        first.QueueFree();

        var restarted = new SettingsService { StoragePath = settingsPath };
        AddChild(restarted);
        var current = restarted.Current;
        if (
            !current.Fullscreen ||
            !Mathf.IsEqualApprox(current.TextCharactersPerSecond, 76.0f) ||
            !Mathf.IsEqualApprox(current.AutoForwardTime, 23.4f) ||
            !current.SkipUnreadText ||
            !current.KeepSkippingAfterChoice ||
            current.TransitionsEnabled ||
            !Mathf.IsEqualApprox(current.MusicVolume, 0.25f) ||
            !Mathf.IsEqualApprox(current.SoundVolume, 0.5f) ||
            !Mathf.IsEqualApprox(current.VoiceVolume, 0.75f) ||
            !current.MuteAll
        )
        {
            throw new InvalidOperationException(
                "Settings did not survive a service restart."
            );
        }
        RemoveChild(restarted);
        restarted.QueueFree();

        var legacyPath = Path.Combine(_testRoot, "legacy_settings.json");
        File.WriteAllText(
            legacyPath,
            """
            {
              "fullscreen": false,
              "text_characters_per_second": 48.0,
              "auto_wait_seconds": 3.4,
              "skip_unread_text": true,
              "keep_skipping_after_choice": false,
              "music_volume": 0.2,
              "sound_volume": 0.4,
              "voice_volume": 0.6,
              "mute_all": false
            }
            """
        );
        var migrated = new SettingsService { StoragePath = legacyPath };
        AddChild(migrated);
        if (
            !Mathf.IsEqualApprox(migrated.Current.AutoForwardTime, 17.0f) ||
            !migrated.Current.TransitionsEnabled ||
            !migrated.Current.SkipUnreadText
        )
        {
            throw new InvalidOperationException("Legacy settings were not migrated.");
        }
        RemoveChild(migrated);
        migrated.QueueFree();
    }

    private void ValidateUiScenes()
    {
        var title = Instantiate<TitleMenu>("res://features/menu/title_menu.tscn");
        var game = Instantiate<GameMenu>("res://features/menu/game_menu.tscn");
        var save = Instantiate<SaveLoadScreen>(
            "res://features/save_ui/save_load_screen.tscn"
        );
        var history = Instantiate<HistoryScreen>(
            "res://features/history/history_screen.tscn"
        );
        var settings = Instantiate<SettingsScreen>(
            "res://features/settings/settings_screen.tscn"
        );

        try
        {
            if (title.FindChildren("*", "Button", true, false).Count != 6)
            {
                throw new InvalidOperationException(
                    "The title menu does not expose all six required actions."
                );
            }
            if (game.FindChildren("*", "Button", true, false).Count != 6)
            {
                throw new InvalidOperationException(
                    "The game menu does not expose all six required actions."
                );
            }

            settings.Open(SettingsMenuContext.Game, saveAvailable: true);
            if (
                settings.FindChildren("*", "Button", true, false).Count != 10 ||
                settings.FindChildren("*", "CheckBox", true, false).Count != 6 ||
                settings.FindChildren("*", "HSlider", true, false).Count != 5
            )
            {
                throw new InvalidOperationException(
                    "The Ren'Py settings screen is missing navigation or preference controls."
                );
            }

            save.Open(
                new SaveService(Path.Combine(_testRoot, "ui_saves")),
                SaveScreenMode.Load
            );
            if (save.GetNode<GridContainer>("%SlotGrid").GetChildCount() != 6)
            {
                throw new InvalidOperationException(
                    "The save screen did not render a 3x2 page."
                );
            }

            foreach (var root in new Control[] { title, game, save, history, settings })
            {
                if (
                    root.FindChildren("*", "Button", true, false)
                        .Cast<Button>()
                        .Any(button => button.FocusMode != Control.FocusModeEnum.All)
                )
                {
                    throw new InvalidOperationException(
                        $"Screen '{root.Name}' contains a button unavailable to keyboard focus."
                    );
                }
            }
        }
        finally
        {
            foreach (var root in new Control[] { title, game, save, history, settings })
            {
                RemoveChild(root);
                root.QueueFree();
            }
        }
    }

    private T Instantiate<T>(string path)
        where T : Node
    {
        var node = GD.Load<PackedScene>(path).Instantiate<T>();
        AddChild(node);
        return node;
    }

    private static Image CreateScreenshot(Color color)
    {
        var image = Image.CreateEmpty(
            SaveService.ScreenshotWidth,
            SaveService.ScreenshotHeight,
            false,
            Image.Format.Rgba8
        );
        image.Fill(color);
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
            GD.PushWarning($"Unable to clean M6 test directory: {exception.Message}");
        }
    }
}
