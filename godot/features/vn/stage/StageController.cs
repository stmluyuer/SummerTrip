using Godot;
using SummerTrip.Content.Catalog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SummerTrip.Features.Vn.Stage;

public sealed record StageCharacterState(string ExpressionId, string PositionId);

public partial class StageController : Control
{
    private readonly Dictionary<string, StageCharacterState> _characters = new();
    private readonly Dictionary<string, TextureRect> _characterNodes = new();
    private TextureRect _previousBackground = null!;
    private TextureRect _background = null!;
    private ColorRect _overlay = null!;
    private Control _characterLayer = null!;
    private ColorRect _transitionCover = null!;
    private Tween? _overlayTween;
    private Tween? _transitionTween;
    private int _overlayAnimationGeneration;

    [Export]
    public ContentCatalog Catalog { get; set; } = null!;

    public string BackgroundId { get; private set; } = "";
    public string OverlayId { get; private set; } = "";
    public string AnimationId { get; private set; } = "";
    public string TransitionId { get; private set; } = "none";
    public IReadOnlyDictionary<string, StageCharacterState> Characters => _characters;
    public Texture2D? BackgroundTexture => _background.Texture;
    public Color OverlayColor => _overlay.Color;
    public float OverlayAlpha => _overlay.Modulate.A;
    public bool OverlayVisible => _overlay.Visible;

    public override void _Ready()
    {
        _previousBackground = GetNode<TextureRect>("%PreviousBackground");
        _background = GetNode<TextureRect>("%Background");
        _overlay = GetNode<ColorRect>("%Overlay");
        _characterLayer = GetNode<Control>("%Characters");
        _transitionCover = GetNode<ColorRect>("%TransitionCover");

        Catalog ??= ResourceLoader.Load<ContentCatalog>(
            "res://content/catalog/content_catalog.tres"
        ) ?? throw new InvalidOperationException("StageController requires a ContentCatalog.");
    }

    public async Task change_scene(
        string backgroundId,
        string overlayId,
        string animationId,
        string transitionId
    )
    {
        var background = Catalog.GetBackground(backgroundId);
        var overlay = Catalog.GetOverlay(overlayId);
        var animation = Catalog.GetAnimation(animationId);
        var transition = Catalog.GetTransition(transitionId);

        BackgroundId = backgroundId;
        OverlayId = overlayId;
        AnimationId = animationId;
        TransitionId = transitionId;
        ClearCharacters();
        StopOverlayAnimation();

        switch (transitionId)
        {
            case "none":
                ApplyScene(background, overlay);
                StartOverlayAnimation(animation);
                break;
            case "slow_dissolve":
                _previousBackground.Texture = _background.Texture;
                _previousBackground.Modulate = Colors.White;
                _previousBackground.Visible = _previousBackground.Texture is not null;
                ApplyScene(background, overlay);
                _background.Modulate = new Color(1, 1, 1, 0);
                _transitionTween?.Kill();
                _transitionTween = CreateTween().SetParallel();
                _transitionTween.TweenProperty(
                    _background,
                    "modulate:a",
                    1.0f,
                    transition.Duration
                );
                _transitionTween.TweenProperty(
                    _previousBackground,
                    "modulate:a",
                    0.0f,
                    transition.Duration
                );
                StartOverlayAnimation(animation);
                await ToSignal(_transitionTween, Tween.SignalName.Finished);
                _previousBackground.Hide();
                _previousBackground.Texture = null;
                break;
            case "scene_blend":
                await RunSceneBlendAsync(background, overlay, animation, transition);
                break;
            default:
                throw new InvalidOperationException(
                    $"Transition '{transitionId}' is catalogued but not implemented."
                );
        }
    }

    public void show_character(string characterId, string expressionId, string positionId)
    {
        var texture = Catalog.GetCharacter(characterId, expressionId);
        var position = Catalog.GetPosition(positionId);

        if (_characterNodes.Remove(characterId, out var existing))
        {
            existing.QueueFree();
        }

        var size = texture.GetSize();
        var character = new TextureRect
        {
            Name = characterId,
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.KeepSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, 0),
        };
        character.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        character.AnchorLeft = position.X;
        character.AnchorRight = position.X;
        character.AnchorTop = position.Y;
        character.AnchorBottom = position.Y;
        character.OffsetLeft = -size.X * position.XAnchor;
        character.OffsetRight = character.OffsetLeft + size.X;
        character.OffsetTop = -size.Y * position.YAnchor;
        character.OffsetBottom = character.OffsetTop + size.Y;
        character.PivotOffset = new Vector2(
            size.X * position.XAnchor,
            size.Y * position.YAnchor
        );
        character.Scale = Vector2.One * position.Scale;
        _characterLayer.AddChild(character);

        var finalPosition = character.Position;
        character.Position += new Vector2(position.EnterOffsetX, 0);
        var tween = CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
        tween.SetParallel();
        tween.TweenProperty(character, "position", finalPosition, position.EnterDuration);
        tween.TweenProperty(character, "modulate:a", 1.0f, position.EnterDuration);

        _characterNodes[characterId] = character;
        _characters[characterId] = new StageCharacterState(expressionId, positionId);
    }

    public TextureRect? GetCharacterNode(string characterId)
    {
        return _characterNodes.GetValueOrDefault(characterId);
    }

    public void Reset()
    {
        BackgroundId = "";
        OverlayId = "";
        AnimationId = "";
        TransitionId = "none";
        ClearCharacters();
        StopOverlayAnimation();

        if (IsNodeReady())
        {
            _background.Texture = null;
            _previousBackground.Texture = null;
            _previousBackground.Hide();
            _overlay.Hide();
            _transitionCover.Hide();
        }
    }

    private async Task RunSceneBlendAsync(
        Texture2D background,
        Color? overlay,
        OverlayAnimation? animation,
        TransitionDefinition transition
    )
    {
        _transitionTween?.Kill();
        _transitionCover.Color = transition.Color;
        _transitionCover.Modulate = new Color(1, 1, 1, 0);
        _transitionCover.Show();

        _transitionTween = CreateTween();
        _transitionTween.TweenProperty(
            _transitionCover,
            "modulate:a",
            1.0f,
            transition.FadeOut
        );
        await ToSignal(_transitionTween, Tween.SignalName.Finished);

        ApplyScene(background, overlay);
        StartOverlayAnimation(animation);
        if (transition.Hold > 0)
        {
            await ToSignal(GetTree().CreateTimer(transition.Hold), SceneTreeTimer.SignalName.Timeout);
        }

        _transitionTween = CreateTween();
        _transitionTween.TweenProperty(
            _transitionCover,
            "modulate:a",
            0.0f,
            transition.FadeIn
        );
        await ToSignal(_transitionTween, Tween.SignalName.Finished);
        _transitionCover.Hide();
    }

    private void ApplyScene(Texture2D background, Color? overlay)
    {
        _background.Texture = background;
        _background.Modulate = Colors.White;

        if (overlay is null)
        {
            _overlay.Hide();
            return;
        }

        _overlay.Color = overlay.Value;
        _overlay.Modulate = Colors.White;
        _overlay.Show();
    }

    private async void StartOverlayAnimation(OverlayAnimation? animation)
    {
        if (animation is null || !_overlay.Visible)
        {
            return;
        }

        var generation = _overlayAnimationGeneration;
        _overlay.Modulate = new Color(1, 1, 1, 0);
        _overlayTween = CreateTween().SetEase(Tween.EaseType.Out);
        _overlayTween.TweenProperty(
            _overlay,
            "modulate:a",
            animation.InitialAlpha,
            animation.InitialDuration
        );
        await ToSignal(_overlayTween, Tween.SignalName.Finished);
        if (generation != _overlayAnimationGeneration || !IsInstanceValid(_overlay))
        {
            return;
        }

        _overlayTween = CreateTween().SetLoops();
        _overlayTween.TweenProperty(
            _overlay,
            "modulate:a",
            animation.CycleMinAlpha,
            animation.CycleLegDuration
        );
        _overlayTween.TweenProperty(
            _overlay,
            "modulate:a",
            animation.CycleMaxAlpha,
            animation.CycleLegDuration
        );
    }

    private void StopOverlayAnimation()
    {
        _overlayAnimationGeneration++;
        _overlayTween?.Kill();
        _overlayTween = null;
    }

    private void ClearCharacters()
    {
        foreach (var character in _characterNodes.Values)
        {
            character.QueueFree();
        }

        _characterNodes.Clear();
        _characters.Clear();
    }
}
