using Godot;
using SummerTrip.Core.Session;
using System;

namespace SummerTrip.Features.History;

public partial class HistoryScreen : Control
{
    private VBoxContainer _entries = null!;

    public event Action? Closed;

    public override void _Ready()
    {
        _entries = GetNode<VBoxContainer>("%Entries");
        GetNode<Button>("%CloseButton").Pressed += () => Closed?.Invoke();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (
            Visible &&
            @event.IsPressed() &&
            !@event.IsEcho() &&
            @event.IsActionPressed("ui_cancel")
        )
        {
            Closed?.Invoke();
            GetViewport().SetInputAsHandled();
        }
    }

    public void Open(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        foreach (var child in _entries.GetChildren())
        {
            _entries.RemoveChild(child);
            child.QueueFree();
        }

        if (session.History.Count == 0)
        {
            _entries.AddChild(new Label { Text = "还没有历史记录。" });
        }
        else
        {
            foreach (var entry in session.History)
            {
                var label = new RichTextLabel
                {
                    BbcodeEnabled = true,
                    FitContent = true,
                    ScrollActive = false,
                    CustomMinimumSize = new Vector2(0.0f, 72.0f),
                    Text = string.IsNullOrWhiteSpace(entry.Character)
                        ? entry.Text
                        : $"[color=#9b3c10][b]{entry.Character}[/b][/color]\n{entry.Text}",
                };
                _entries.AddChild(label);
            }
        }

        GetNode<Button>("%CloseButton").GrabFocus();
    }
}
