using Godot;
using System;

namespace SummerTrip.Features.Vn.Ui;

public partial class QuickMenu : Control
{
    private Button _rollback = null!;
    private Button _skip = null!;
    private Button _auto = null!;
    private Button _quickSave = null!;
    private Button _quickLoad = null!;

    public event Action? RollbackRequested;
    public event Action? HistoryRequested;
    public event Action? SkipRequested;
    public event Action? AutoRequested;
    public event Action? SaveRequested;
    public event Action? QuickSaveRequested;
    public event Action? QuickLoadRequested;
    public event Action? SettingsRequested;

    public override void _Ready()
    {
        _rollback = GetNode<Button>("%RollbackButton");
        _skip = GetNode<Button>("%SkipButton");
        _auto = GetNode<Button>("%AutoButton");
        _quickSave = GetNode<Button>("%QuickSaveButton");
        _quickLoad = GetNode<Button>("%QuickLoadButton");

        _rollback.Pressed += () => RollbackRequested?.Invoke();
        GetNode<Button>("%HistoryButton").Pressed += () => HistoryRequested?.Invoke();
        _skip.Pressed += () => SkipRequested?.Invoke();
        _auto.Pressed += () => AutoRequested?.Invoke();
        GetNode<Button>("%SaveButton").Pressed += () => SaveRequested?.Invoke();
        _quickSave.Pressed += () => QuickSaveRequested?.Invoke();
        _quickLoad.Pressed += () => QuickLoadRequested?.Invoke();
        GetNode<Button>("%SettingsButton").Pressed += () => SettingsRequested?.Invoke();
    }

    public void SetStates(
        bool autoPlaying,
        bool skipToggled,
        bool canRollback,
        bool ioAvailable,
        bool quickLoadAvailable
    )
    {
        _auto.Text = autoPlaying ? "自动 ●" : "自动";
        _skip.Text = skipToggled ? "快进 ●" : "快进";
        _rollback.Disabled = !canRollback || !ioAvailable;
        _quickSave.Disabled = !ioAvailable;
        _quickLoad.Disabled = !ioAvailable || !quickLoadAvailable;
    }
}
