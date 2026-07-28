using Godot;
using System.Collections.Generic;

namespace SummerTrip.Features.Vn.Stage;

public sealed record StageCharacterState(string ExpressionId, string PositionId);

public partial class StageController : Node
{
    private readonly Dictionary<string, StageCharacterState> _characters = new();

    public string BackgroundId { get; private set; } = "";
    public string OverlayId { get; private set; } = "";
    public string AnimationId { get; private set; } = "";
    public string TransitionId { get; private set; } = "none";
    public IReadOnlyDictionary<string, StageCharacterState> Characters => _characters;

    public void change_scene(
        string backgroundId,
        string overlayId,
        string animationId,
        string transitionId
    )
    {
        BackgroundId = backgroundId;
        OverlayId = overlayId;
        AnimationId = animationId;
        TransitionId = transitionId;
        _characters.Clear();
    }

    public void show_character(string characterId, string expressionId, string positionId)
    {
        _characters[characterId] = new StageCharacterState(expressionId, positionId);
    }

    public void Reset()
    {
        BackgroundId = "";
        OverlayId = "";
        AnimationId = "";
        TransitionId = "none";
        _characters.Clear();
    }
}
