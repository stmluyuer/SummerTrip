using Godot;
using SummerTrip.Core.Dialogue;
using SummerTrip.Core.Settings;
using System;

namespace SummerTrip.Features.Vn.Ui;

public partial class DialogueBox : Control
{
    private Label _characterName = null!;
    private ColorRect _nameUnderline = null!;
    private RichTextLabel _dialogueText = null!;
    private VBoxContainer _choices = null!;
    private VNDirector? _director;
    private DialogueEntry? _line;
    private SettingsService? _settings;
    private float _visibleCharacterProgress;
    private bool _userHidden;

    public event Action? MenuRequested;
    public event Action? AdvanceRequested;
    public event Action<int>? ChoiceRequested;

    public override void _Ready()
    {
        _characterName = GetNode<Label>("%CharacterName");
        _nameUnderline = GetNode<ColorRect>("%NameUnderline");
        _dialogueText = GetNode<RichTextLabel>("%DialogueText");
        _choices = GetNode<VBoxContainer>("%Choices");
        _settings = GetNodeOrNull<SettingsService>("/root/SettingsService");
        SetProcess(false);
    }

    public override void _ExitTree()
    {
        if (_director is not null)
        {
            _director.LineChanged -= OnLineChanged;
            _director.TypingSkipRequested -= FinishTyping;
        }
    }

    public override void _Process(double delta)
    {
        if (_line is null || _director?.Interaction != InteractionState.Typing)
        {
            SetProcess(false);
            return;
        }

        _visibleCharacterProgress +=
            (_settings?.Current.TextCharactersPerSecond ?? 48.0f) * (float)delta;
        _dialogueText.VisibleCharacters = Mathf.FloorToInt(_visibleCharacterProgress);
        if (_dialogueText.VisibleCharacters >= _line.Text.Length)
        {
            FinishTyping();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_director is null || !@event.IsPressed() || @event.IsEcho())
        {
            return;
        }

        if (@event.IsActionPressed("vn_menu"))
        {
            MenuRequested?.Invoke();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed("vn_advance"))
        {
            AdvanceRequested?.Invoke();
            GetViewport().SetInputAsHandled();
        }
    }

    public void Bind(VNDirector director)
    {
        if (_director is not null)
        {
            throw new InvalidOperationException("DialogueBox is already bound.");
        }

        _director = director ?? throw new ArgumentNullException(nameof(director));
        _director.LineChanged += OnLineChanged;
        _director.TypingSkipRequested += FinishTyping;
    }

    public void SetUserHidden(bool hidden)
    {
        _userHidden = hidden;
        Visible = !hidden && _line is not null;
    }

    private void OnLineChanged(DialogueEntry? line)
    {
        _line = line;
        ClearChoices();

        if (line is null)
        {
            Hide();
            SetProcess(false);
            return;
        }

        Visible = !_userHidden;
        _characterName.Text = line.Character;
        _characterName.Visible = !string.IsNullOrWhiteSpace(line.Character);
        _nameUnderline.Visible = _characterName.Visible;
        _dialogueText.Text = line.Text;

        if (
            string.IsNullOrEmpty(line.Text) ||
            _director?.Interaction != InteractionState.Typing
        )
        {
            _dialogueText.VisibleCharacters = -1;
            PopulateChoices();
            SetProcess(false);
            return;
        }

        _visibleCharacterProgress = 0.0f;
        _dialogueText.VisibleCharacters = 0;
        SetProcess(true);
    }

    private void FinishTyping()
    {
        if (_line is null || _director?.Interaction != InteractionState.Typing)
        {
            return;
        }

        _dialogueText.VisibleCharacters = -1;
        SetProcess(false);
        _director.CompleteTyping();
        PopulateChoices();
    }

    private void PopulateChoices()
    {
        if (_line is null || _director?.Interaction != InteractionState.AwaitingChoice)
        {
            return;
        }

        for (var index = 0; index < _line.Choices.Count; index++)
        {
            var choiceIndex = index;
            var button = new Button
            {
                Text = _line.Choices[index].Text,
                FocusMode = FocusModeEnum.All,
                CustomMinimumSize = new Vector2(0.0f, 56.0f),
            };
            button.Pressed += () => ChoiceRequested?.Invoke(choiceIndex);
            _choices.AddChild(button);
        }

        if (_choices.GetChildCount() > 0)
        {
            _choices.GetChild<Button>(0).GrabFocus();
        }
    }

    private void ClearChoices()
    {
        foreach (var child in _choices.GetChildren())
        {
            _choices.RemoveChild(child);
            child.QueueFree();
        }
    }
}
