using Godot;
using SummerTrip.Core.Dialogue;
using SummerTrip.Core.Session;
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

    public event Action<DialogueEntry?>? LineChanged;
    public event Action? TypingSkipRequested;

    public DialogueEntry? CurrentLine { get; private set; }
    public InteractionState Interaction { get; private set; } = InteractionState.Loading;
    public OperationState Operation { get; private set; } = OperationState.None;

    public void Initialize(DialogueGateway gateway, GameSession session)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _session = session ?? throw new ArgumentNullException(nameof(session));
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
    }

    public void ToggleMenuOperation()
    {
        Operation = Operation == OperationState.Menu
            ? OperationState.None
            : OperationState.Menu;
    }

    private void ApplyLine(DialogueEntry? line)
    {
        CurrentLine = line;
        if (line is null)
        {
            Interaction = InteractionState.Ended;
            LineChanged?.Invoke(null);
            return;
        }

        _session!.CurrentStaticLineId = line.StaticId;
        Interaction = string.IsNullOrEmpty(line.Text)
            ? ResolveWaitingState(line)
            : InteractionState.Typing;
        LineChanged?.Invoke(line);
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
}
