using Godot;
using SummerTrip.Core.Dialogue;
using SummerTrip.Core.Save;
using SummerTrip.Core.Session;
using SummerTrip.Features.Vn;
using SummerTrip.Features.Vn.Stage;
using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace SummerTrip.Tests;

public partial class SaveRoundtrip : Node
{
    private const string DialoguePath = "res://content/dialogue/prologue.dialogue";
    private const string StageScenePath = "res://features/vn/stage/stage.tscn";
    private const string LaterReadMarker = "ST_TEST_LATER_READ";

    private string _testRoot = "";
    private string _saveRoot = "";

    public override async void _Ready()
    {
        var preserveTestDirectory = false;
        try
        {
            var arguments = OS.GetCmdlineUserArgs();
            var writeRestartProbe = arguments.Contains(
                "--m4-write-probe",
                StringComparer.Ordinal
            );
            var readRestartProbe = arguments.Contains(
                "--m4-read-probe",
                StringComparer.Ordinal
            );
            _testRoot = ProjectSettings.GlobalizePath(
                writeRestartProbe || readRestartProbe
                    ? "user://m4_restart_probe"
                    : "user://m4_save_roundtrip"
            );
            _saveRoot = Path.Combine(_testRoot, "saves");

            if (writeRestartProbe)
            {
                ResetTestDirectory();
                WriteRestartProbe();
                preserveTestDirectory = true;
                GD.Print("SAVE_RESTART_PROBE_WRITTEN");
                GetTree().Quit(0);
                return;
            }

            if (readRestartProbe)
            {
                ReadRestartProbe();
                GD.Print("SAVE_RESTART_PROBE_READ_OK");
                GetTree().Quit(0);
                return;
            }

            ResetTestDirectory();
            await RunAsync();
            GD.Print(
                "SAVE_ROUNDTRIP_M4_OK savepoints=4 manual_slots=54 quick_slots=1 auto_slots=6"
            );
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"SAVE_ROUNDTRIP_M4_FAILED: {exception}");
            GetTree().Quit(1);
        }
        finally
        {
            if (!preserveTestDirectory)
            {
                TryCleanupTestDirectory();
            }
        }
    }

    private async Task RunAsync()
    {
        ValidateSlotRanges();
        ValidateProfileRecovery();

        var stageScene = ResourceLoader.Load<PackedScene>(StageScenePath)
            ?? throw new InvalidOperationException("Unable to load the stage scene.");
        var stage = stageScene.Instantiate<StageController>();
        var director = new VNDirector();
        AddChild(stage);
        AddChild(director);

        var session = new GameSession
        {
            StoryResourceId = "prologue",
            ChapterId = "prologue",
        };
        var profile = new ProfileData();
        using var gateway = new DialogueGateway(
            DialoguePath,
            new DialogueContext(session, stage),
            "prologue"
        );
        director.Initialize(gateway, session, stage, profile);

        try
        {
            await director.StartAsync();
            AssertLine(director, "ST_PRT_L001", InteractionState.Typing);

            var typingSave = await SaveAndRestoreAsync(
                director,
                stage,
                new SaveSlot(SaveSlotKind.Manual, 4),
                addLaterProfileRead: true
            );
            AssertLine(director, "ST_PRT_L001", InteractionState.AwaitingAdvance);
            if (!director.Profile.ReadLineIds.Contains(LaterReadMarker))
            {
                throw new InvalidOperationException(
                    "Loading an older save removed a later Profile read ID."
                );
            }
            await director.AdvanceAsync();

            await WalkToAsync(director, "ST_AHW_L001");
            director.CompleteTyping();
            var postTransitionSave = await SaveAndRestoreAsync(
                director,
                stage,
                new SaveSlot(SaveSlotKind.Manual, 3)
            );
            AssertLine(director, "ST_AHW_L001", InteractionState.AwaitingAdvance);
            await director.AdvanceAsync();

            await WalkToAsync(director, "ST_AHW_L010");
            director.CompleteTyping();
            var preChoiceSave = await SaveAndRestoreAsync(
                director,
                stage,
                new SaveSlot(SaveSlotKind.Manual, 1)
            );
            AssertLine(director, "ST_AHW_L010", InteractionState.AwaitingChoice);
            await director.ChooseAsync(0);
            if (director.Session.minoriko_affection != 1)
            {
                throw new InvalidOperationException(
                    "The first choice mutation did not execute exactly once."
                );
            }

            director.CompleteTyping();
            var postMutationSave = await SaveAndRestoreAsync(
                director,
                stage,
                new SaveSlot(SaveSlotKind.Manual, 2)
            );
            AssertLine(director, "ST_THM_L001", InteractionState.AwaitingAdvance);
            await director.AdvanceAsync();
            if (director.Session.minoriko_affection != 1)
            {
                throw new InvalidOperationException(
                    "Restoring and advancing replayed the saved affection mutation."
                );
            }

            ValidateMissingScreenshotDoesNotBlockLoad(typingSave);
            ValidateAtomicInterruptionKeepsOldSave(
                director,
                new SaveSlot(SaveSlotKind.Manual, 1),
                preChoiceSave
            );
            ValidateInvalidTemporaryFileKeepsOldSave(
                new SaveSlot(SaveSlotKind.Manual, 2),
                postMutationSave
            );
            ValidateBackupKeepsPreviousScreenshot(postMutationSave);
            ValidateSchemaFailures(postTransitionSave);
            await ValidateFailedRestoreRollsBackAsync(
                director,
                stage,
                postMutationSave
            );
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

    private async Task<SaveDocument> SaveAndRestoreAsync(
        VNDirector director,
        StageController stage,
        SaveSlot slot,
        bool addLaterProfileRead = false
    )
    {
        var service = new SaveService(_saveRoot);
        using var screenshot = CreateScreenshot(slot.Index);
        var saved = director.Save(service, slot, screenshot);
        var expectedSession = SaveSchema.ToSessionSnapshot(saved);
        var expectedStage = SaveSchema.ToStageSnapshot(saved);
        var sessionReference = director.Session;

        director.Session.player_name = "mutated";
        director.Session.minoriko_affection = 99;
        director.Session.shizuha_trust = 99;
        director.Session.autumn_mystery = 99;
        director.Session.History.Clear();
        stage.Reset();
        if (addLaterProfileRead)
        {
            director.Profile.ReadLineIds.Add(LaterReadMarker);
        }

        var restartedService = new SaveService(_saveRoot);
        var loaded = restartedService.Load(slot);
        if (!loaded.ScreenshotExists)
        {
            throw new InvalidOperationException(
                $"Slot {slot.Index} screenshot was missing immediately after save."
            );
        }

        await director.RestoreAsync(loaded.Document, restartedService);
        if (!ReferenceEquals(sessionReference, director.Session))
        {
            throw new InvalidOperationException(
                "Restore replaced the injected GameSession reference."
            );
        }
        AssertSession(expectedSession, director.Session.CaptureSnapshot());
        AssertStage(expectedStage, stage.CaptureSnapshot());
        if (director.Operation != OperationState.None)
        {
            throw new InvalidOperationException("Director did not return to the active operation state.");
        }
        if (
            addLaterProfileRead &&
            !restartedService.LoadProfile().ReadLineIds.Contains(LaterReadMarker)
        )
        {
            throw new InvalidOperationException(
                "Merged Profile data was not durable after restoring a save."
            );
        }

        return loaded.Document;
    }

    private static async Task WalkToAsync(VNDirector director, string targetStaticId)
    {
        for (var step = 0; step < 80; step++)
        {
            if (string.Equals(
                    director.CurrentLine?.StaticId,
                    targetStaticId,
                    StringComparison.Ordinal
                ))
            {
                return;
            }

            if (director.Interaction == InteractionState.Typing)
            {
                director.CompleteTyping();
            }

            if (director.Interaction == InteractionState.AwaitingAdvance)
            {
                await director.AdvanceAsync();
                continue;
            }

            throw new InvalidOperationException(
                $"Unable to walk to '{targetStaticId}' from interaction state {director.Interaction}."
            );
        }

        throw new InvalidOperationException(
            $"Dialogue did not reach '{targetStaticId}' within 80 steps."
        );
    }

    private void ValidateAtomicInterruptionKeepsOldSave(
        VNDirector director,
        SaveSlot slot,
        SaveDocument oldDocument
    )
    {
        var service = new SaveService(_saveRoot);
        var officialPath = service.GetSlotJsonPath(slot);
        var oldJson = File.ReadAllText(officialPath);
        var slotDirectory = Path.GetDirectoryName(officialPath)
            ?? throw new InvalidOperationException("Save slot path has no directory.");
        var filesBefore = Directory.EnumerateFiles(
            slotDirectory,
            $"slot_{slot.Index:000}.*"
        ).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        service.WriteCheckpointObserver = checkpoint =>
        {
            if (checkpoint == SaveWriteCheckpoint.ScreenshotValidated)
            {
                throw new IOException("Injected interruption after screenshot validation.");
            }
        };

        using var screenshot = CreateScreenshot(41);
        ExpectFailure(
            () => director.Save(service, slot, screenshot),
            "Injected interruption"
        );
        if (!string.Equals(File.ReadAllText(officialPath), oldJson, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "An interruption before JSON commit changed the official save."
            );
        }

        var filesAfter = Directory.EnumerateFiles(
            slotDirectory,
            $"slot_{slot.Index:000}.*"
        ).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        if (!filesBefore.SequenceEqual(filesAfter, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Interrupted save left an orphaned screenshot or temporary metadata file."
            );
        }

        var loaded = new SaveService(_saveRoot).Load(slot);
        if (loaded.Document.Story.StaticLineId != oldDocument.Story.StaticLineId)
        {
            throw new InvalidOperationException(
                "Interrupted save replaced the previous official metadata."
            );
        }
    }

    private void ValidateInvalidTemporaryFileKeepsOldSave(
        SaveSlot slot,
        SaveDocument oldDocument
    )
    {
        var service = new SaveService(_saveRoot);
        var temporaryPath = service.GetSlotJsonPath(slot) + ".broken.tmp";
        File.WriteAllText(temporaryPath, "{not valid json");

        var loaded = service.Load(slot);
        if (loaded.Document.Story.StaticLineId != oldDocument.Story.StaticLineId)
        {
            throw new InvalidOperationException(
                "An invalid temporary file prevented reading the official save."
            );
        }
    }

    private void ValidateMissingScreenshotDoesNotBlockLoad(SaveDocument document)
    {
        var screenshotPath = Path.Combine(
            _saveRoot,
            document.ScreenshotPath.Replace('/', Path.DirectorySeparatorChar)
        );
        File.Delete(screenshotPath);
        var loaded = new SaveService(_saveRoot).Load(
            new SaveSlot(SaveSlotKind.Manual, 4)
        );
        if (loaded.ScreenshotExists)
        {
            throw new InvalidOperationException(
                "Missing screenshot was incorrectly reported as present."
            );
        }
    }

    private void ValidateSchemaFailures(SaveDocument validDocument)
    {
        var validJson = SaveSchema.Serialize(validDocument);
        WriteInvalidSlotJson(5, json =>
        {
            json["schema_version"] = 999;
        });
        ExpectFailure(
            () => new SaveService(_saveRoot).Load(
                new SaveSlot(SaveSlotKind.Manual, 5)
            ),
            "Unsupported save schema version"
        );

        WriteInvalidSlotJson(6, json =>
        {
            json.Remove("story");
        });
        ExpectFailure(
            () => new SaveService(_saveRoot).Load(
                new SaveSlot(SaveSlotKind.Manual, 6)
            ),
            "story.resource_id"
        );

        WriteInvalidSlotJson(7, json =>
        {
            json["screenshot_path"] = "../outside.png";
        });
        ExpectFailure(
            () => new SaveService(_saveRoot).Load(
                new SaveSlot(SaveSlotKind.Manual, 7)
            ),
            "controlled relative"
        );

        WriteInvalidSlotJson(8, json =>
        {
            json.Remove("history");
        });
        ExpectFailure(
            () => new SaveService(_saveRoot).Load(
                new SaveSlot(SaveSlotKind.Manual, 8)
            ),
            "history"
        );
        return;

        void WriteInvalidSlotJson(int slotIndex, Action<JsonObject> mutate)
        {
            var root = JsonNode.Parse(validJson)?.AsObject()
                ?? throw new InvalidOperationException("Unable to parse valid save JSON.");
            mutate(root);
            var service = new SaveService(_saveRoot);
            var path = service.GetSlotJsonPath(
                new SaveSlot(SaveSlotKind.Manual, slotIndex)
            );
            Directory.CreateDirectory(
                Path.GetDirectoryName(path)
                    ?? throw new InvalidOperationException("Invalid test save path.")
            );
            File.WriteAllText(path, root.ToJsonString());
        }
    }

    private void ValidateBackupKeepsPreviousScreenshot(SaveDocument source)
    {
        var service = new SaveService(_saveRoot);
        var slot = new SaveSlot(SaveSlotKind.Manual, 9);
        var first = SaveSchema.Deserialize(SaveSchema.Serialize(source));
        using (var screenshot = CreateScreenshot(91))
        {
            service.Save(slot, first, screenshot);
        }

        var firstScreenshot = Path.Combine(
            _saveRoot,
            first.ScreenshotPath.Replace('/', Path.DirectorySeparatorChar)
        );
        var second = SaveSchema.Deserialize(SaveSchema.Serialize(source));
        using (var screenshot = CreateScreenshot(92))
        {
            service.Save(slot, second, screenshot);
        }

        var backupPath = service.GetSlotJsonPath(slot) + ".bak";
        var backup = SaveSchema.Deserialize(File.ReadAllText(backupPath));
        if (
            backup.ScreenshotPath != first.ScreenshotPath ||
            !File.Exists(firstScreenshot)
        )
        {
            throw new InvalidOperationException(
                "The previous JSON backup and its immutable screenshot were not kept together."
            );
        }
    }

    private static async Task ValidateFailedRestoreRollsBackAsync(
        VNDirector director,
        StageController stage,
        SaveDocument validDocument
    )
    {
        var service = new SaveService(
            Path.Combine(
                ProjectSettings.GlobalizePath("user://m4_save_roundtrip"),
                "saves"
            )
        );
        var beforeSession = director.Session.CaptureSnapshot();
        var beforeStage = stage.CaptureSnapshot();
        var beforeProfile = director.Profile.CaptureSnapshot();
        var beforeLine = director.CurrentLine?.StaticId;
        var beforeInteraction = director.Interaction;
        var beforeLineWasRead = director.CurrentLineWasRead;

        foreach (var invalid in new[]
        {
            MutateClone(validDocument, document =>
                document.Story.StaticLineId = "ST_UNKNOWN_LINE"
            ),
            MutateClone(validDocument, document =>
                document.Story.ResourceId = "unknown_story"
            ),
            MutateClone(validDocument, document =>
                document.Presentation.BackgroundId = "unknown_background"
            ),
        })
        {
            try
            {
                await director.RestoreAsync(invalid, service);
                throw new InvalidOperationException("Invalid save unexpectedly restored.");
            }
            catch (Exception exception) when (
                exception is SaveValidationException ||
                exception is InvalidOperationException ||
                exception is ArgumentException
            )
            {
            }

            AssertSession(beforeSession, director.Session.CaptureSnapshot());
            AssertStage(beforeStage, stage.CaptureSnapshot());
            AssertProfile(beforeProfile, director.Profile.CaptureSnapshot());
            if (
                director.CurrentLine?.StaticId != beforeLine ||
                director.Interaction != beforeInteraction ||
                director.CurrentLineWasRead != beforeLineWasRead
            )
            {
                throw new InvalidOperationException(
                    "Failed restore changed the current dialogue state."
                );
            }
        }

        if (
            beforeLineWasRead ||
            !director.Profile.ReadLineIds.Contains(validDocument.Story.StaticLineId)
        )
        {
            throw new InvalidOperationException(
                "Profile write failure test requires an unread current line and a read restore target."
            );
        }

        var blockerPath = Path.Combine(
            ProjectSettings.GlobalizePath("user://m4_save_roundtrip"),
            "profile_write_blocker"
        );
        File.WriteAllText(blockerPath, "not a directory");
        try
        {
            var failingProfileService = new SaveService(
                Path.Combine(blockerPath, "saves")
            );
            try
            {
                await director.RestoreAsync(validDocument, failingProfileService);
                throw new InvalidOperationException(
                    "Restore unexpectedly completed when Profile persistence was blocked."
                );
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException
            )
            {
            }

            AssertSession(beforeSession, director.Session.CaptureSnapshot());
            AssertStage(beforeStage, stage.CaptureSnapshot());
            AssertProfile(beforeProfile, director.Profile.CaptureSnapshot());
            if (
                director.CurrentLine?.StaticId != beforeLine ||
                director.Interaction != beforeInteraction ||
                director.CurrentLineWasRead != beforeLineWasRead
            )
            {
                throw new InvalidOperationException(
                    "Profile write failure left a partially restored dialogue state."
                );
            }
        }
        finally
        {
            File.Delete(blockerPath);
        }
    }

    private static SaveDocument MutateClone(
        SaveDocument source,
        Action<SaveDocument> mutate
    )
    {
        var clone = SaveSchema.Deserialize(SaveSchema.Serialize(source));
        mutate(clone);
        return clone;
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

    private void WriteRestartProbe()
    {
        var session = new GameSession
        {
            StoryResourceId = "prologue",
            ChapterId = "prologue",
            CurrentStaticLineId = "ST_PRT_L001",
            minoriko_affection = 7,
            PlayTimeSeconds = 12.5,
        };
        session.RecordLine("ST_PRT_L001", "", "restart probe");
        var document = SaveSchema.Create(
            session.CaptureSnapshot(),
            new StageSnapshot(
                "autumn_garden",
                "",
                "",
                Array.Empty<StageCharacterSnapshot>(),
                "none",
                null,
                0.0
            ),
            new ProfileData().CaptureSnapshot()
        );
        using var screenshot = CreateScreenshot(101);
        new SaveService(_saveRoot).Save(
            new SaveSlot(SaveSlotKind.Quick, 1),
            document,
            screenshot
        );
        session.Dispose();
    }

    private void ReadRestartProbe()
    {
        var loaded = new SaveService(_saveRoot).Load(
            new SaveSlot(SaveSlotKind.Quick, 1)
        );
        var session = SaveSchema.ToSessionSnapshot(loaded.Document);
        if (
            !loaded.ScreenshotExists ||
            session.CurrentStaticLineId != "ST_PRT_L001" ||
            session.MinorikoAffection != 7 ||
            session.History.Count != 1
        )
        {
            throw new InvalidOperationException(
                "Fresh process did not read the restart probe snapshot."
            );
        }
    }

    private static void AssertLine(
        VNDirector director,
        string staticId,
        InteractionState interaction
    )
    {
        if (
            !string.Equals(director.CurrentLine?.StaticId, staticId, StringComparison.Ordinal) ||
            director.Interaction != interaction
        )
        {
            throw new InvalidOperationException(
                $"Expected line '{staticId}' in {interaction}, found " +
                $"'{director.CurrentLine?.StaticId}' in {director.Interaction}."
            );
        }
    }

    private static void AssertSession(
        GameSessionSnapshot expected,
        GameSessionSnapshot actual
    )
    {
        if (
            expected.PlayerName != actual.PlayerName ||
            expected.MinorikoAffection != actual.MinorikoAffection ||
            expected.ShizuhaTrust != actual.ShizuhaTrust ||
            expected.AutumnMystery != actual.AutumnMystery ||
            expected.StoryResourceId != actual.StoryResourceId ||
            expected.ChapterId != actual.ChapterId ||
            expected.CurrentStaticLineId != actual.CurrentStaticLineId ||
            !expected.History.SequenceEqual(actual.History) ||
            !expected.ChapterFlags.OrderBy(pair => pair.Key)
                .SequenceEqual(actual.ChapterFlags.OrderBy(pair => pair.Key)) ||
            !Mathf.IsEqualApprox(
                (float)expected.PlayTimeSeconds,
                (float)actual.PlayTimeSeconds
            )
        )
        {
            throw new InvalidOperationException("Restored session does not match its snapshot.");
        }
    }

    private static void AssertStage(StageSnapshot expected, StageSnapshot actual)
    {
        if (
            expected.BackgroundId != actual.BackgroundId ||
            expected.OverlayId != actual.OverlayId ||
            expected.OverlayAnimationId != actual.OverlayAnimationId ||
            expected.TransitionId != actual.TransitionId ||
            expected.MusicId != actual.MusicId ||
            !Mathf.IsEqualApprox(
                (float)expected.MusicPosition,
                (float)actual.MusicPosition
            ) ||
            !expected.Characters.SequenceEqual(actual.Characters)
        )
        {
            throw new InvalidOperationException("Restored stage does not match its snapshot.");
        }
    }

    private static void AssertProfile(ProfileSnapshot expected, ProfileSnapshot actual)
    {
        if (
            expected.ProfileVersion != actual.ProfileVersion ||
            !expected.ReadLineIds.SequenceEqual(actual.ReadLineIds) ||
            !expected.UnlockedEndings.SequenceEqual(actual.UnlockedEndings) ||
            !expected.UnlockedCgs.SequenceEqual(actual.UnlockedCgs) ||
            !expected.Achievements.SequenceEqual(actual.Achievements)
        )
        {
            throw new InvalidOperationException("Failed restore changed the Profile.");
        }
    }

    private void ValidateProfileRecovery()
    {
        var recoverySaveRoot = Path.Combine(
            _testRoot,
            "profile_recovery",
            "saves"
        );
        var service = new SaveService(recoverySaveRoot);
        var backupProfile = new ProfileData();
        backupProfile.ReadLineIds.Add("ST_PROFILE_BACKUP");
        service.SaveProfile(backupProfile);

        var currentProfile = backupProfile.Clone();
        currentProfile.ReadLineIds.Add("ST_PROFILE_CURRENT");
        service.SaveProfile(currentProfile);

        var profilePath = service.GetProfilePath();
        File.WriteAllText(profilePath, "{broken profile");
        var backupRecovery = new SaveService(recoverySaveRoot);
        var recovered = backupRecovery.LoadProfile();
        if (
            !recovered.ReadLineIds.SetEquals(backupProfile.ReadLineIds) ||
            backupRecovery.LastProfileRecoveryWarning is null ||
            !File.Exists(profilePath)
        )
        {
            throw new InvalidOperationException(
                "A damaged Profile was not restored from its valid backup."
            );
        }

        File.WriteAllText(profilePath, "{broken primary");
        File.WriteAllText(profilePath + ".bak", "{broken backup");
        var defaultRecovery = new SaveService(recoverySaveRoot);
        var defaults = defaultRecovery.LoadProfile();
        var profileDirectory = Path.GetDirectoryName(profilePath)
            ?? throw new InvalidOperationException("Profile path has no directory.");
        if (
            defaults.ReadLineIds.Count != 0 ||
            defaultRecovery.LastProfileRecoveryWarning is null ||
            !File.Exists(profilePath) ||
            Directory.EnumerateFiles(
                profileDirectory,
                "profile.json*.corrupt"
            ).Count() < 3
        )
        {
            throw new InvalidOperationException(
                "Unrecoverable Profile files were not quarantined before defaults were created."
            );
        }
    }

    private static void ValidateSlotRanges()
    {
        _ = new SaveSlot(SaveSlotKind.Manual, SaveService.ManualSlotCount);
        _ = new SaveSlot(SaveSlotKind.Quick, SaveService.QuickSlotCount);
        _ = new SaveSlot(SaveSlotKind.Auto, SaveService.AutoSlotCount);
        ExpectFailure(
            () => _ = new SaveSlot(
                SaveSlotKind.Manual,
                SaveService.ManualSlotCount + 1
            ),
            "between 1 and 54"
        );
    }

    private static void ExpectFailure(Action action, string messageFragment)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (
            exception.Message.Contains(messageFragment, StringComparison.Ordinal)
        )
        {
            return;
        }

        throw new InvalidOperationException(
            $"Expected failure containing '{messageFragment}'."
        );
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
            GD.PushWarning($"Unable to clean M4 test directory: {exception.Message}");
        }
    }
}
