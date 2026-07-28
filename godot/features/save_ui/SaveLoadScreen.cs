using Godot;
using SummerTrip.Core.Save;
using System;
using System.Collections.Generic;

namespace SummerTrip.Features.SaveUi;

public enum SaveScreenMode
{
    Save,
    Load,
}

public partial class SaveLoadScreen : Control
{
    private readonly List<SaveSlot> _slotControls = new();
    private SaveService? _saveService;
    private SaveScreenMode _mode;
    private SaveSlotKind _kind = SaveSlotKind.Manual;
    private int _page = 1;
    private GridContainer _grid = null!;
    private Label _heading = null!;
    private Label _pageLabel = null!;
    private Button _previousButton = null!;
    private Button _nextButton = null!;
    private PackedScene _slotScene = null!;

    public event Action<SaveSlotSummary>? SlotActivated;
    public event Action<SaveSlotSummary>? DeleteRequested;
    public event Action? Closed;

    public override void _Ready()
    {
        _grid = GetNode<GridContainer>("%SlotGrid");
        _heading = GetNode<Label>("%Heading");
        _pageLabel = GetNode<Label>("%PageLabel");
        _previousButton = GetNode<Button>("%PreviousButton");
        _nextButton = GetNode<Button>("%NextButton");
        _slotScene = GD.Load<PackedScene>("res://features/save_ui/save_slot.tscn");

        GetNode<Button>("%ManualButton").Pressed += () => SelectKind(SaveSlotKind.Manual);
        GetNode<Button>("%AutoButton").Pressed += () => SelectKind(SaveSlotKind.Auto);
        GetNode<Button>("%QuickButton").Pressed += () => SelectKind(SaveSlotKind.Quick);
        GetNode<Button>("%CloseButton").Pressed += () => Closed?.Invoke();
        _previousButton.Pressed += () => ChangePage(-1);
        _nextButton.Pressed += () => ChangePage(1);
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

    public void Open(SaveService saveService, SaveScreenMode mode)
    {
        _saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
        _mode = mode;
        _kind = SaveSlotKind.Manual;
        _page = 1;
        _heading.Text = mode == SaveScreenMode.Save ? "保存游戏" : "读取游戏";
        GetNode<Button>("%AutoButton").Disabled = mode == SaveScreenMode.Save;
        GetNode<Button>("%QuickButton").Disabled = mode == SaveScreenMode.Save;
        Refresh();
    }

    public void Refresh()
    {
        if (_saveService is null)
        {
            return;
        }

        foreach (var child in _grid.GetChildren())
        {
            _grid.RemoveChild(child);
            child.QueueFree();
        }
        _slotControls.Clear();

        var slotCount = GetSlotCount(_kind);
        var pageCount = _kind == SaveSlotKind.Manual ? 9 : 1;
        _page = Math.Clamp(_page, 1, pageCount);
        var firstIndex = (_page - 1) * 6 + 1;
        var lastIndex = Math.Min(firstIndex + 5, slotCount);
        for (var index = firstIndex; index <= lastIndex; index++)
        {
            var control = _slotScene.Instantiate<SaveSlot>();
            _grid.AddChild(control);
            control.Configure(
                _saveService.Inspect(new Core.Save.SaveSlot(_kind, index)),
                _mode == SaveScreenMode.Save && _kind == SaveSlotKind.Manual
            );
            control.Activated += summary => SlotActivated?.Invoke(summary);
            control.DeleteRequested += summary => DeleteRequested?.Invoke(summary);
            _slotControls.Add(control);
        }

        _pageLabel.Text = $"{_page} / {pageCount}";
        _previousButton.Disabled = _page <= 1;
        _nextButton.Disabled = _page >= pageCount;
        if (_slotControls.Count > 0)
        {
            _slotControls[0].FocusSlot();
        }
    }

    private void SelectKind(SaveSlotKind kind)
    {
        if (_mode == SaveScreenMode.Save && kind != SaveSlotKind.Manual)
        {
            return;
        }

        _kind = kind;
        _page = 1;
        Refresh();
    }

    private void ChangePage(int amount)
    {
        _page += amount;
        Refresh();
    }

    private static int GetSlotCount(SaveSlotKind kind)
    {
        return kind switch
        {
            SaveSlotKind.Manual => SaveService.ManualSlotCount,
            SaveSlotKind.Quick => SaveService.QuickSlotCount,
            SaveSlotKind.Auto => SaveService.AutoSlotCount,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }
}
