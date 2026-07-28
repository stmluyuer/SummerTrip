using DialogueManagerRuntime;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SummerTrip.Core.Dialogue;

public sealed record DialogueChoice(
    string Text,
    string NextKey,
    string StaticId
);

public sealed record DialogueEntry(
    string Text,
    string Character,
    string NextKey,
    string StaticId,
    IReadOnlyList<DialogueChoice> Choices
);

public sealed class DialogueGateway : IDisposable
{
    private DialogueContext _context;
    private readonly Resource _dialogueResource;

    public DialogueGateway(
        string dialoguePath,
        DialogueContext context,
        string resourceId = "prologue"
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dialoguePath);
        _context = context ?? throw new ArgumentNullException(nameof(context));
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId);
        DialoguePath = dialoguePath;
        ResourceId = resourceId;

        _dialogueResource = ResourceLoader.Load<Resource>(
            dialoguePath,
            cacheMode: ResourceLoader.CacheMode.IgnoreDeep
        ) ?? throw new InvalidOperationException($"Unable to load dialogue resource '{dialoguePath}'.");
    }

    public string ImportedVersion =>
        _dialogueResource.GetMeta("dialogue_manager_version", default).AsString();
    public string DialoguePath { get; }
    public string ResourceId { get; }

    public Task<DialogueEntry?> StartAsync(string title = "start")
    {
        return RequestAsync(title);
    }

    public Task<DialogueEntry?> AdvanceAsync(DialogueEntry current)
    {
        ArgumentNullException.ThrowIfNull(current);
        return RequestAsync(current.NextKey);
    }

    public Task<DialogueEntry?> ChooseAsync(DialogueChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        return RequestAsync(choice.NextKey);
    }

    public async Task<DialogueEntry> RestoreAsync(string staticId)
    {
        var runtimeIds = ResolveStaticId(staticId);
        if (runtimeIds.Count != 1)
        {
            throw new InvalidOperationException(
                $"Static dialogue ID '{staticId}' resolved to {runtimeIds.Count} runtime lines; expected exactly one."
            );
        }

        var line = await RequestAsync(runtimeIds[0], MutationBehaviour.Skip)
            ?? throw new InvalidOperationException(
                $"Static dialogue ID '{staticId}' did not restore a visible line."
            );
        if (!string.Equals(line.StaticId, staticId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Restored dialogue line reported static ID '{line.StaticId}', expected '{staticId}'."
            );
        }

        return line;
    }

    public void ReplaceContext(DialogueContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public static string ResolveResourcePath(string resourceId)
    {
        return resourceId switch
        {
            "prologue" => "res://content/dialogue/prologue.dialogue",
            _ => throw new ArgumentException(
                $"Unknown dialogue resource ID '{resourceId}'.",
                nameof(resourceId)
            ),
        };
    }

    public IReadOnlyList<string> ResolveStaticId(string staticId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(staticId);
        return DialogueManager
            .StaticIdToLineIds(_dialogueResource, staticId)
            .ToArray();
    }

    public bool HasTitle(string title)
    {
        using var titles = _dialogueResource.Get("titles").AsGodotDictionary();
        return titles.ContainsKey(title);
    }

    public bool HasCompiledStaticLine(string staticId)
    {
        var runtimeIds = ResolveStaticId(staticId);
        if (runtimeIds.Count != 1)
        {
            return false;
        }

        using var lines = _dialogueResource.Get("lines").AsGodotDictionary();
        return lines.ContainsKey(runtimeIds[0]);
    }

    private Task<DialogueEntry?> RequestAsync(string key)
    {
        return RequestAsync(key, MutationBehaviour.Wait);
    }

    private async Task<DialogueEntry?> RequestAsync(
        string key,
        MutationBehaviour mutationBehaviour
    )
    {
        using var line = await DialogueManager.GetNextDialogueLine(
            _dialogueResource,
            key,
            _context.ExtraGameStates,
            mutationBehaviour
        );

        if (line is null)
        {
            return null;
        }

        var choices = line.Responses
            .Where(response => response.IsAllowed)
            .Select(response => new DialogueChoice(
                response.Text,
                response.NextId,
                response.TranslationKey
            ))
            .ToArray();

        return new DialogueEntry(
            line.Text,
            line.Character,
            line.NextId,
            line.TranslationKey,
            choices
        );
    }

    public void Dispose()
    {
        _dialogueResource.SetScript(default);
        _dialogueResource.Dispose();
    }
}
