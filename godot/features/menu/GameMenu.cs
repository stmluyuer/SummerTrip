using Godot;
using System;

namespace SummerTrip.Features.Menu;

public partial class GameMenu : Control
{
    public event Action? ResumeRequested;
    public event Action? SaveRequested;
    public event Action? LoadRequested;
    public event Action? HistoryRequested;
    public event Action? SettingsRequested;
    public event Action? ReturnTitleRequested;

    public override void _Ready()
    {
        GetNode<Button>("%ResumeButton").Pressed += () => ResumeRequested?.Invoke();
        GetNode<Button>("%SaveButton").Pressed += () => SaveRequested?.Invoke();
        GetNode<Button>("%LoadButton").Pressed += () => LoadRequested?.Invoke();
        GetNode<Button>("%HistoryButton").Pressed += () => HistoryRequested?.Invoke();
        GetNode<Button>("%SettingsButton").Pressed += () => SettingsRequested?.Invoke();
        GetNode<Button>("%TitleButton").Pressed += () => ReturnTitleRequested?.Invoke();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (
            Visible &&
            @event.IsPressed() &&
            !@event.IsEcho() &&
            (@event.IsActionPressed("ui_cancel") || @event.IsActionPressed("vn_menu"))
        )
        {
            ResumeRequested?.Invoke();
            GetViewport().SetInputAsHandled();
        }
    }

    public void FocusDefault()
    {
        GetNode<Button>("%ResumeButton").GrabFocus();
    }

    public void SetSaveAvailable(bool available)
    {
        GetNode<Button>("%SaveButton").Disabled = !available;
        GetNode<Button>("%SaveButton").TooltipText = available
            ? ""
            : "当前正在转场或恢复，暂时不能保存。";
    }

    public void SetLoadAvailable(bool available)
    {
        GetNode<Button>("%LoadButton").Disabled = !available;
        GetNode<Button>("%LoadButton").TooltipText = available
            ? ""
            : "当前正在转场或恢复，暂时不能读取。";
    }
}
