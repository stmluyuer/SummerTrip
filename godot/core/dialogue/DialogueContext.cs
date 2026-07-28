using Godot;
using Godot.Collections;
using SummerTrip.Core.Session;
using SummerTrip.Features.Vn.Stage;
using System;

namespace SummerTrip.Core.Dialogue;

public sealed class DialogueContext
{
    private readonly Dictionary _namedStates;

    public DialogueContext(GameSession gameSession, StageController stageController)
    {
        ArgumentNullException.ThrowIfNull(gameSession);
        ArgumentNullException.ThrowIfNull(stageController);

        _namedStates = new Dictionary
        {
            ["GameState"] = gameSession,
            ["Presentation"] = stageController,
        };
        ExtraGameStates = new Array<Variant> { _namedStates };
    }

    public Array<Variant> ExtraGameStates { get; }
}
