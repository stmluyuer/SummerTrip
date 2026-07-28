using Godot;
using System;
using System.Collections.Generic;

namespace SummerTrip.Content.Catalog;

public sealed record CharacterPosition(
    float X,
    float Y,
    float XAnchor,
    float YAnchor,
    float Scale,
    float EnterOffsetX,
    float EnterDuration
);

public sealed record OverlayAnimation(
    float InitialDuration,
    float InitialAlpha,
    float CycleLegDuration,
    float CycleMinAlpha,
    float CycleMaxAlpha
);

public sealed record TransitionDefinition(
    float Duration,
    float FadeOut,
    float Hold,
    float FadeIn,
    Color Color
);

public sealed record ChapterDefinition(
    string TitleKey,
    IReadOnlyDictionary<string, string> AutosaveTitleFirstLines,
    IReadOnlyDictionary<string, string> EndingFinalLines
);

public partial class ContentCatalog : Resource
{
    private static readonly Dictionary<string, string> Backgrounds = new()
    {
        ["autumn_garden"] = "res://assets/backgrounds/autumn_garden.png",
        ["autumn_room"] = "res://assets/backgrounds/autumn_room.png",
    };

    private static readonly Dictionary<string, string> Characters = new()
    {
        ["minoriko:neutral"] = "res://assets/characters/minoriko.png",
        ["shizuha:neutral"] = "res://assets/characters/shizuha.png",
    };

    private static readonly Dictionary<string, string> UiTextures = new()
    {
        ["dialogue_box"] = "res://assets/ui/dialogue/box_compact.png",
        ["choice_idle"] = "res://assets/ui/button/choice_idle_background.png",
        ["choice_hover"] = "res://assets/ui/button/choice_hover_background.png",
        ["window_icon"] = "res://assets/ui/window_icon.png",
    };

    private static readonly Dictionary<string, CharacterPosition> Positions = new()
    {
        ["right"] = new(0.72f, 1.02f, 0.5f, 1.0f, 0.66f, 64.0f, 0.28f),
        ["left"] = new(0.28f, 1.02f, 0.5f, 1.0f, 0.66f, -64.0f, 0.28f),
    };

    private static readonly Dictionary<string, Color> Overlays = new()
    {
        ["autumn_glow"] = new Color(0.851f, 0.541f, 0.169f, 0.133f),
        ["mystery_glow"] = new Color(0.435f, 0.561f, 0.722f, 0.2f),
    };

    private static readonly Dictionary<string, OverlayAnimation> Animations = new()
    {
        ["leaf_shiver"] = new(0.25f, 0.22f, 1.6f, 0.12f, 0.24f),
    };

    private static readonly Dictionary<string, TransitionDefinition> Transitions = new()
    {
        ["none"] = new(0.0f, 0.0f, 0.0f, 0.0f, Colors.Black),
        ["slow_dissolve"] = new(0.85f, 0.0f, 0.0f, 0.0f, Colors.Black),
        ["scene_blend"] = new(0.0f, 0.35f, 0.2f, 0.55f, new Color("#120b08")),
    };

    private static readonly Dictionary<string, ChapterDefinition> Chapters = new()
    {
        ["prologue"] = new(
            "chapter.prologue",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["aki_house_wakeup"] = "ST_AHW_L001",
                ["maple_tree_investigation"] = "ST_MTI_L001",
                ["route_choice"] = "ST_RTC_L001",
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ending_minoriko_prologue"] = "ST_EMI_L005",
                ["ending_shizuha_prologue"] = "ST_ESH_L005",
                ["ending_sisters_prologue"] = "ST_ESI_L006",
            }
        ),
    };

    public Texture2D GetBackground(string backgroundId)
    {
        return LoadTexture(Backgrounds, backgroundId, "background");
    }

    public Texture2D GetCharacter(string characterId, string expressionId)
    {
        return LoadTexture(Characters, $"{characterId}:{expressionId}", "character expression");
    }

    public CharacterPosition GetPosition(string positionId)
    {
        return GetRequired(Positions, positionId, "character position");
    }

    public Texture2D GetUiTexture(string textureId)
    {
        return LoadTexture(UiTextures, textureId, "UI texture");
    }

    public Color? GetOverlay(string overlayId)
    {
        return string.IsNullOrEmpty(overlayId)
            ? null
            : GetRequired(Overlays, overlayId, "overlay");
    }

    public OverlayAnimation? GetAnimation(string animationId)
    {
        return string.IsNullOrEmpty(animationId)
            ? null
            : GetRequired(Animations, animationId, "overlay animation");
    }

    public TransitionDefinition GetTransition(string transitionId)
    {
        return GetRequired(Transitions, transitionId, "transition");
    }

    public ChapterDefinition GetChapter(string chapterId)
    {
        return GetRequired(Chapters, chapterId, "chapter");
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        ValidateTextures(Backgrounds, "background", errors);
        ValidateTextures(Characters, "character expression", errors);
        ValidateTextures(UiTextures, "UI texture", errors);
        return errors;
    }

    private static Texture2D LoadTexture(
        IReadOnlyDictionary<string, string> paths,
        string id,
        string kind
    )
    {
        var path = GetRequired(paths, id, kind);
        return ResourceLoader.Load<Texture2D>(path)
            ?? throw new InvalidOperationException(
                $"Content catalog {kind} '{id}' could not load '{path}'."
            );
    }

    private static T GetRequired<T>(
        IReadOnlyDictionary<string, T> values,
        string id,
        string kind
    )
    {
        if (!values.TryGetValue(id, out var value))
        {
            throw new ArgumentException($"Unknown content catalog {kind} ID '{id}'.", nameof(id));
        }

        return value;
    }

    private static void ValidateTextures(
        IReadOnlyDictionary<string, string> paths,
        string kind,
        ICollection<string> errors
    )
    {
        foreach (var (id, path) in paths)
        {
            if (!ResourceLoader.Exists(path, "Texture2D"))
            {
                errors.Add($"{kind} '{id}' is missing texture '{path}'.");
            }
        }
    }
}
