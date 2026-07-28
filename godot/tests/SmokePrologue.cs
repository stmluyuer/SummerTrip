using DialogueManagerRuntime;
using Godot;
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
                $"SMOKE_PROLOGUE_OK titles={ExpectedTitleCount} " +
                $"static_ids={ExpectedStaticIdCount} first=ST_PRT_L001"
            );
            exitCode = 0;
        }
        catch (Exception exception)
        {
            GD.PushError($"SMOKE_PROLOGUE_FAILED: {exception}");
        }

        GetTree().Quit(exitCode);
    }

    private static async Task RunAsync()
    {
        ValidateProjectSettings();

        if (!Engine.HasSingleton("DialogueManager"))
        {
            throw new InvalidOperationException("DialogueManager singleton is unavailable.");
        }

        using var dialogue = ResourceLoader.Load<Resource>(
            DialoguePath,
            cacheMode: ResourceLoader.CacheMode.IgnoreDeep
        );
        if (dialogue is null)
        {
            throw new InvalidOperationException($"Unable to load {DialoguePath}.");
        }

        var importedVersion = dialogue
            .GetMeta("dialogue_manager_version", default)
            .AsString();
        if (!string.Equals(
                importedVersion,
                ExpectedDialogueManagerVersion,
                StringComparison.Ordinal
            ))
        {
            throw new InvalidOperationException(
                $"Expected Dialogue Manager {ExpectedDialogueManagerVersion}, " +
                $"imported resource reports '{importedVersion}'."
            );
        }

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

        var sourceTitles = Regex.Matches(
                source,
                @"^~ (?<title>[a-z0-9_]+)\s*$",
                RegexOptions.CultureInvariant | RegexOptions.Multiline
            )
            .Select(match => match.Groups["title"].Value)
            .ToArray();

        if (sourceTitles.Length != ExpectedTitleCount)
        {
            throw new InvalidOperationException(
                $"Expected {ExpectedTitleCount} titles, found {sourceTitles.Length}."
            );
        }

        using var compiledTitles = dialogue.Get("titles").AsGodotDictionary();
        foreach (var title in sourceTitles)
        {
            if (!compiledTitles.ContainsKey(title))
            {
                throw new InvalidOperationException($"Compiled dialogue is missing title '{title}'.");
            }
        }

        using var compiledLines = dialogue.Get("lines").AsGodotDictionary();
        foreach (var staticId in staticIds)
        {
            var runtimeIds = DialogueManager.StaticIdToLineIds(dialogue, staticId);
            if (runtimeIds.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Static ID '{staticId}' resolved to {runtimeIds.Count} runtime IDs."
                );
            }

            var runtimeId = runtimeIds[0];
            if (!compiledLines.ContainsKey(runtimeId))
            {
                throw new InvalidOperationException(
                    $"Static ID '{staticId}' resolved to missing runtime ID '{runtimeId}'."
                );
            }

            using var compiledLine = compiledLines[runtimeId].AsGodotDictionary();
            var translationKey = compiledLine["translation_key"].AsString();
            if (!string.Equals(translationKey, staticId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Runtime ID '{runtimeId}' has translation key '{translationKey}', " +
                    $"expected '{staticId}'."
                );
            }
        }

        using var firstLine = await DialogueManager.GetNextDialogueLine(
            dialogue,
            "start",
            null,
            MutationBehaviour.Skip
        );

        if (firstLine is null)
        {
            throw new InvalidOperationException("The 'start' title returned no printable line.");
        }

        if (!string.Equals(firstLine.TranslationKey, "ST_PRT_L001", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"'start' resolved to '{firstLine.TranslationKey}', expected 'ST_PRT_L001'."
            );
        }

        if (!firstLine.Text.Contains("风里有泥土", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The first Chinese line was not imported intact.");
        }

        dialogue.SetScript(default);
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
}
