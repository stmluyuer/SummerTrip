using Godot;
using SummerTrip.Content.Catalog;
using SummerTrip.Core.Dialogue;
using SummerTrip.Core.Save;
using SummerTrip.Core.Session;
using System;
using System.Linq;

namespace SummerTrip.Features.Vn;

public sealed class PrologueProgression : IDisposable
{
    private readonly VNDirector _director;
    private readonly ProfileData _profile;
    private readonly SaveService _saveService;
    private readonly Func<Image> _captureScreenshot;
    private readonly ChapterDefinition _chapter;

    public PrologueProgression(
        VNDirector director,
        ProfileData profile,
        SaveService saveService,
        ContentCatalog catalog,
        Func<Image> captureScreenshot
    )
    {
        _director = director ?? throw new ArgumentNullException(nameof(director));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
        ArgumentNullException.ThrowIfNull(catalog);
        _captureScreenshot = captureScreenshot
            ?? throw new ArgumentNullException(nameof(captureScreenshot));
        _chapter = catalog.GetChapter("prologue");
        _director.StableLineReached += OnStableLineReached;
    }

    public event Action<string, SaveSlot>? AutoSaveCompleted;
    public event Action<string>? EndingUnlocked;

    public Exception? LastError { get; private set; }
    public int AutoSaveCount { get; private set; }

    public void Dispose()
    {
        _director.StableLineReached -= OnStableLineReached;
    }

    private void OnStableLineReached(DialogueEntry line)
    {
        try
        {
            LastError = null;
            var candidateProfile = _profile.Clone();
            var endingId = _chapter.EndingFinalLines
                .FirstOrDefault(pair => string.Equals(
                    pair.Value,
                    line.StaticId,
                    StringComparison.Ordinal
                ))
                .Key;
            var endingWasUnlocked =
                !string.IsNullOrEmpty(endingId) &&
                !candidateProfile.UnlockedEndings.Contains(endingId);
            if (endingWasUnlocked)
            {
                candidateProfile.UnlockedEndings.Add(endingId);
            }

            _saveService.SaveProfile(candidateProfile);
            _profile.ReplaceWith(candidateProfile);
            if (endingWasUnlocked)
            {
                EndingUnlocked?.Invoke(endingId);
            }

            var isConfiguredTitleStart = _chapter.AutosaveTitleFirstLines.Values.Contains(
                line.StaticId,
                StringComparer.Ordinal
            );
            if (!isConfiguredTitleStart && string.IsNullOrEmpty(endingId))
            {
                return;
            }

            var slot = _saveService.GetNextAutoSlot();
            using var screenshot = _captureScreenshot();
            _director.Save(_saveService, slot, screenshot);
            AutoSaveCount++;
            AutoSaveCompleted?.Invoke(line.StaticId, slot);
        }
        catch (Exception exception)
        {
            LastError = exception;
            GD.PushError(
                $"Unable to persist prologue progress at '{line.StaticId}': {exception.Message}"
            );
        }
    }
}
