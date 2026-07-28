using Godot;
using SummerTrip.Core.Dialogue;
using SummerTrip.Core.Save;
using SummerTrip.Core.Session;
using SummerTrip.Features.Vn.Stage;
using System;
using System.Threading.Tasks;

namespace SummerTrip.Features.Vn;

public enum InteractionState
{
    Loading,
    Typing,
    AwaitingAdvance,
    AwaitingChoice,
    Ended,
}

public enum OperationState
{
    None,
    Menu,
    Saving,
    Loading,
}

public partial class VNDirector : Node
{
    private DialogueGateway? _gateway;
    private GameSession? _session;
    private StageController? _stage;
    private ProfileData? _profile;

    public event Action<DialogueEntry?>? LineChanged;
    public event Action? TypingSkipRequested;
    public event Action<DialogueEntry>? StableLineReached;

    public DialogueEntry? CurrentLine { get; private set; }
    public InteractionState Interaction { get; private set; } = InteractionState.Loading;
    public OperationState Operation { get; private set; } = OperationState.None;
    public GameSession Session =>
        _session ?? throw new InvalidOperationException("VNDirector is not initialized.");
    public ProfileData Profile =>
        _profile ?? throw new InvalidOperationException("VNDirector is not initialized.");

    public void Initialize(DialogueGateway gateway, GameSession session)
    {
        Initialize(gateway, session, stage: null, profile: null);
    }

    public void Initialize(
        DialogueGateway gateway,
        GameSession session,
        StageController? stage,
        ProfileData? profile
    )
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _stage = stage;
        _profile = profile ?? new ProfileData();
    }

    public async Task StartAsync(string title = "start")
    {
        EnsureInitialized();
        Interaction = InteractionState.Loading;
        ApplyLine(await _gateway!.StartAsync(title));
    }

    public async Task AdvanceAsync()
    {
        EnsureInitialized();
        if (Operation != OperationState.None)
        {
            return;
        }

        if (Interaction == InteractionState.Typing)
        {
            TypingSkipRequested?.Invoke();
            return;
        }

        if (Interaction != InteractionState.AwaitingAdvance || CurrentLine is null)
        {
            return;
        }

        Interaction = InteractionState.Loading;
        ApplyLine(await _gateway!.AdvanceAsync(CurrentLine));
    }

    public async Task ChooseAsync(int choiceIndex)
    {
        EnsureInitialized();
        if (Operation != OperationState.None)
        {
            return;
        }

        if (Interaction != InteractionState.AwaitingChoice || CurrentLine is null)
        {
            return;
        }

        if (choiceIndex < 0 || choiceIndex >= CurrentLine.Choices.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(choiceIndex));
        }

        var choice = CurrentLine.Choices[choiceIndex];
        Interaction = InteractionState.Loading;
        ApplyLine(await _gateway!.ChooseAsync(choice));
    }

    public void CompleteTyping()
    {
        if (Interaction != InteractionState.Typing || CurrentLine is null)
        {
            return;
        }

        Interaction = CurrentLine.Choices.Count > 0
            ? InteractionState.AwaitingChoice
            : InteractionState.AwaitingAdvance;
        StableLineReached?.Invoke(CurrentLine);
    }

    public void ToggleMenuOperation()
    {
        if (Operation is OperationState.Saving or OperationState.Loading)
        {
            return;
        }

        if (Interaction == InteractionState.Typing)
        {
            FinishTypingForSave();
        }

        Operation = Operation == OperationState.Menu
            ? OperationState.None
            : OperationState.Menu;
    }

    public SaveDocument Save(SaveService saveService, SaveSlot slot, Image screenshot)
    {
        EnsureSaveInitialized();
        ArgumentNullException.ThrowIfNull(saveService);
        ArgumentNullException.ThrowIfNull(screenshot);
        if (Operation is not (OperationState.None or OperationState.Menu))
        {
            throw new InvalidOperationException(
                $"Cannot save while operation state is {Operation}."
            );
        }

        var previousOperation = Operation;
        try
        {
            FinishTypingForSave();
            if (
                Interaction is not (
                    InteractionState.AwaitingAdvance or InteractionState.AwaitingChoice
                )
            )
            {
                throw new InvalidOperationException(
                    $"Cannot save while dialogue interaction state is {Interaction}."
                );
            }

            Operation = OperationState.Saving;
            var document = SaveSchema.Create(
                _session!.CaptureSnapshot(),
                _stage!.CaptureSnapshot(),
                _profile!.CaptureSnapshot()
            );
            return saveService.Save(slot, document, screenshot);
        }
        finally
        {
            Operation = previousOperation;
        }
    }

    public async Task RestoreAsync(
        SaveDocument document,
        SaveService saveService
    )
    {
        EnsureSaveInitialized();
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(saveService);
        if (Operation is not (OperationState.None or OperationState.Menu))
        {
            throw new InvalidOperationException(
                $"Cannot load while operation state is {Operation}."
            );
        }

        var previousOperation = Operation;
        var previousInteraction = Interaction;
        var previousLine = CurrentLine;
        var previousSession = _session!;
        var previousSessionSnapshot = previousSession.CaptureSnapshot();
        var previousStageSnapshot = _stage!.CaptureSnapshot();
        var candidateSession = new GameSession();
        DialogueGateway? candidateGateway = null;
        var runtimeMutated = false;

        Operation = OperationState.Loading;
        try
        {
            SaveSchema.Validate(document, requireScreenshotPath: true);
            var candidateSessionSnapshot = SaveSchema.ToSessionSnapshot(document);
            var candidateStageSnapshot = SaveSchema.ToStageSnapshot(document);
            candidateSession.RestoreSnapshot(candidateSessionSnapshot);
            _stage.ValidateSnapshot(candidateStageSnapshot);

            if (!string.Equals(
                    _gateway!.ResourceId,
                    candidateSession.StoryResourceId,
                    StringComparison.Ordinal
                ))
            {
                throw new InvalidOperationException(
                    $"Loaded story resource '{candidateSession.StoryResourceId}' does not match the active resource '{_gateway.ResourceId}'."
                );
            }

            var resourcePath = DialogueGateway.ResolveResourcePath(
                candidateSession.StoryResourceId
            );
            candidateGateway = new DialogueGateway(
                resourcePath,
                new DialogueContext(candidateSession, _stage),
                candidateSession.StoryResourceId
            );
            var candidateLine = await candidateGateway.RestoreAsync(
                candidateSession.CurrentStaticLineId
            );

            var candidateProfile = _profile!.Clone();
            candidateProfile.MergeReadLineIds(document.ReadLineIds);

            runtimeMutated = true;
            _stage.RestoreSnapshot(candidateStageSnapshot);
            previousSession.RestoreSnapshot(candidateSessionSnapshot);
            _session = previousSession;
            _gateway.ReplaceContext(new DialogueContext(previousSession, _stage));
            ApplyLine(candidateLine, recordLine: false, forceComplete: true);

            saveService.SaveProfile(candidateProfile);
            _profile.ReplaceWith(candidateProfile);
            Operation = OperationState.None;
        }
        catch
        {
            if (runtimeMutated)
            {
                RestorePreviousRuntime(
                    previousSession,
                    previousSessionSnapshot,
                    previousStageSnapshot,
                    previousLine,
                    previousInteraction
                );
            }

            Operation = previousOperation;
            throw;
        }
        finally
        {
            candidateGateway?.Dispose();
            candidateSession.Dispose();
        }
    }

    private void ApplyLine(
        DialogueEntry? line,
        bool recordLine = true,
        bool forceComplete = false
    )
    {
        CurrentLine = line;
        if (line is null)
        {
            Interaction = InteractionState.Ended;
            LineChanged?.Invoke(null);
            return;
        }

        _session!.CurrentStaticLineId = line.StaticId;
        if (recordLine)
        {
            _session.RecordLine(line.StaticId, line.Character, line.Text);
            _profile?.ReadLineIds.Add(line.StaticId);
        }

        var isImmediatelyStable = forceComplete || string.IsNullOrEmpty(line.Text);
        Interaction = isImmediatelyStable
            ? ResolveWaitingState(line)
            : InteractionState.Typing;
        LineChanged?.Invoke(line);
        if (isImmediatelyStable && !forceComplete)
        {
            StableLineReached?.Invoke(line);
        }
    }

    private static InteractionState ResolveWaitingState(DialogueEntry line)
    {
        return line.Choices.Count > 0
            ? InteractionState.AwaitingChoice
            : InteractionState.AwaitingAdvance;
    }

    private void EnsureInitialized()
    {
        if (_gateway is null || _session is null)
        {
            throw new InvalidOperationException("VNDirector must be initialized before use.");
        }
    }

    private void EnsureSaveInitialized()
    {
        EnsureInitialized();
        if (_stage is null || _profile is null)
        {
            throw new InvalidOperationException(
                "VNDirector save support requires a stage and profile during initialization."
            );
        }
    }

    private void FinishTypingForSave()
    {
        if (Interaction != InteractionState.Typing)
        {
            return;
        }

        TypingSkipRequested?.Invoke();
        if (Interaction == InteractionState.Typing)
        {
            CompleteTyping();
        }
    }

    private void RestorePreviousRuntime(
        GameSession previousSession,
        GameSessionSnapshot previousSessionSnapshot,
        StageSnapshot previousStageSnapshot,
        DialogueEntry? previousLine,
        InteractionState previousInteraction
    )
    {
        previousSession.RestoreSnapshot(previousSessionSnapshot);
        _stage!.RestoreSnapshot(previousStageSnapshot);
        _session = previousSession;
        _gateway!.ReplaceContext(new DialogueContext(previousSession, _stage));
        CurrentLine = previousLine;
        Interaction = previousInteraction;
        LineChanged?.Invoke(previousLine);
    }
}
