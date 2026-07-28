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
    private readonly DialogueContext _context;
    private readonly Resource _dialogueResource;

    public DialogueGateway(string dialoguePath, DialogueContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dialoguePath);
        _context = context ?? throw new ArgumentNullException(nameof(context));

        _dialogueResource = ResourceLoader.Load<Resource>(
            dialoguePath,
            cacheMode: ResourceLoader.CacheMode.IgnoreDeep
        ) ?? throw new InvalidOperationException($"Unable to load dialogue resource '{dialoguePath}'.");
    }

    public string ImportedVersion =>
        _dialogueResource.GetMeta("dialogue_manager_version", default).AsString();

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

    private async Task<DialogueEntry?> RequestAsync(string key)
    {
        using var line = await DialogueManager.GetNextDialogueLine(
            _dialogueResource,
            key,
            _context.ExtraGameStates,
            MutationBehaviour.Wait
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
