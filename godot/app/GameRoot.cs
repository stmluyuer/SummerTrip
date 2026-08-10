using Godot;
using SummerTrip.Core.Save;
using SummerTrip.Features.History;
using SummerTrip.Features.Menu;
using SummerTrip.Features.SaveUi;
using SummerTrip.Features.Settings;
using SummerTrip.Features.Vn;
using System;
using System.Threading.Tasks;

namespace SummerTrip.App;

public partial class GameRoot : Control
{
    private readonly SaveService _saveService = new();
    private Control _modeHost = null!;
    private Control _overlayHost = null!;
    private TitleMenu _titleMenu = null!;
    private ConfirmationDialog _confirmation = null!;
    private AcceptDialog _about = null!;
    private Label _toast = null!;
    private PackedScene _vnScene = null!;
    private PackedScene _gameMenuScene = null!;
    private PackedScene _saveScreenScene = null!;
    private PackedScene _historyScene = null!;
    private PackedScene _settingsScene = null!;
    private VNMode? _vnMode;
    private GameMenu? _gameMenu;
    private Control? _overlay;
    private Action? _confirmedAction;
    private int _toastGeneration;
    private bool _overlayPausesGame;

    public override void _Ready()
    {
        _modeHost = GetNode<Control>("%ModeHost");
        _overlayHost = GetNode<Control>("%OverlayHost");
        _titleMenu = GetNode<TitleMenu>("%TitleMenu");
        _confirmation = GetNode<ConfirmationDialog>("%Confirmation");
        _about = GetNode<AcceptDialog>("%About");
        _toast = GetNode<Label>("%Toast");
        _vnScene = GD.Load<PackedScene>("res://features/vn/vn_mode.tscn");
        _gameMenuScene = GD.Load<PackedScene>("res://features/menu/game_menu.tscn");
        _saveScreenScene = GD.Load<PackedScene>(
            "res://features/save_ui/save_load_screen.tscn"
        );
        _historyScene = GD.Load<PackedScene>(
            "res://features/history/history_screen.tscn"
        );
        _settingsScene = GD.Load<PackedScene>(
            "res://features/settings/settings_screen.tscn"
        );

        _titleMenu.NewGameRequested += () => _ = StartNewGameAsync();
        _titleMenu.ContinueRequested += () => _ = ContinueAsync();
        _titleMenu.LoadRequested += () => OpenSaveScreen(SaveScreenMode.Load);
        _titleMenu.SettingsRequested += OpenSettings;
        _titleMenu.AboutRequested += ShowAbout;
        _titleMenu.ExitRequested += ConfirmExit;
        _confirmation.Confirmed += OnConfirmed;
        _confirmation.Canceled += () => _confirmedAction = null;
        RefreshTitle();
    }

    private async Task StartNewGameAsync()
    {
        try
        {
            await CreateVnModeAsync();
        }
        catch (Exception exception)
        {
            ReturnToTitle();
            Notify($"无法开始游戏：{exception.Message}");
        }
    }

    private async Task ContinueAsync()
    {
        var latest = _saveService.GetLatestValidSlot();
        if (latest is null)
        {
            Notify("没有可继续的存档。");
            RefreshTitle();
            return;
        }

        await StartAndLoadAsync(latest);
    }

    private async Task CreateVnModeAsync()
    {
        RemoveOverlay();
        RemoveGameMenu();
        ReleaseVnMode();
        _titleMenu.Hide();

        var mode = _vnScene.Instantiate<VNMode>();
        var ready = new TaskCompletionSource();
        mode.Initialized += () => ready.TrySetResult();
        mode.InitializationFailed += message =>
            ready.TrySetException(new InvalidOperationException(message));
        mode.GameMenuRequested += OnGameMenuRequested;
        mode.HistoryRequested += OnQuickHistoryRequested;
        mode.SaveScreenRequested += OnQuickSaveRequested;
        mode.SettingsRequested += OnQuickSettingsRequested;
        mode.NotificationRequested += Notify;
        _vnMode = mode;
        _modeHost.AddChild(mode);
        await ready.Task;
    }

    private async Task StartAndLoadAsync(SaveSlotSummary summary)
    {
        try
        {
            await CreateVnModeAsync();
            await RestoreSlotAsync(summary);
            Notify(
                summary.Status == SaveSlotStatus.ScreenshotMissing
                    ? "读取完成；该存档截图缺失。"
                    : "读取完成。"
            );
        }
        catch (Exception exception)
        {
            ReturnToTitle();
            Notify($"读取失败：{exception.Message}");
        }
    }

    private void OnGameMenuRequested()
    {
        if (_vnMode is null)
        {
            return;
        }

        if (_overlay is not null)
        {
            CloseOverlayToGameMenu();
            return;
        }

        if (_gameMenu is not null)
        {
            CloseGameMenu();
            return;
        }

        _vnMode.Director.ToggleMenuOperation();
        if (_vnMode.Director.Operation != OperationState.Menu)
        {
            return;
        }

        _overlayPausesGame = false;
        _gameMenu = _gameMenuScene.Instantiate<GameMenu>();
        _overlayHost.AddChild(_gameMenu);
        _gameMenu.ResumeRequested += CloseGameMenu;
        _gameMenu.SaveRequested += () => OpenSaveScreen(SaveScreenMode.Save);
        _gameMenu.LoadRequested += () => OpenSaveScreen(SaveScreenMode.Load);
        _gameMenu.HistoryRequested += OpenHistory;
        _gameMenu.SettingsRequested += OpenSettings;
        _gameMenu.ReturnTitleRequested += ConfirmReturnToTitle;
        var ioAvailable = _vnMode.Director.Interaction is
            InteractionState.AwaitingAdvance or InteractionState.AwaitingChoice;
        _gameMenu.SetSaveAvailable(ioAvailable);
        _gameMenu.SetLoadAvailable(ioAvailable);
        _gameMenu.FocusDefault();
    }

    private void CloseGameMenu()
    {
        RemoveOverlay();
        RemoveGameMenu();
        if (_vnMode?.Director.Operation == OperationState.Menu)
        {
            _vnMode.Director.ToggleMenuOperation();
        }
    }

    private void OpenSaveScreen(SaveScreenMode mode)
    {
        RemoveOverlay();
        if (_gameMenu is not null)
        {
            _gameMenu.Hide();
        }

        var screen = _saveScreenScene.Instantiate<SaveLoadScreen>();
        _overlayHost.AddChild(screen);
        _overlay = screen;
        screen.Open(_saveService, mode);
        screen.Closed += CloseOverlay;
        screen.DeleteRequested += summary => ConfirmDelete(screen, summary);
        screen.SlotActivated += summary =>
        {
            if (mode == SaveScreenMode.Save)
            {
                RequestSave(screen, summary);
            }
            else
            {
                _ = RequestLoadAsync(summary);
            }
        };
    }

    private void RequestSave(SaveLoadScreen screen, SaveSlotSummary summary)
    {
        if (_vnMode is null)
        {
            Notify("标题菜单不能写入存档。");
            return;
        }

        if (summary.Status == SaveSlotStatus.Empty)
        {
            SaveToSlot(screen, summary.Slot);
            return;
        }

        Confirm(
            "覆盖存档",
            $"槽位 {summary.Slot.Index:00} 已有内容，确定覆盖吗？",
            () => SaveToSlot(screen, summary.Slot)
        );
    }

    private void SaveToSlot(SaveLoadScreen screen, Core.Save.SaveSlot slot)
    {
        try
        {
            using var screenshot = _vnMode!.CaptureSaveScreenshot();
            _vnMode.Director.Save(_saveService, slot, screenshot);
            screen.Refresh();
            Notify($"已保存到手动槽位 {slot.Index:00}。");
        }
        catch (Exception exception)
        {
            Notify($"保存失败：{exception.Message}");
        }
    }

    private async Task RequestLoadAsync(SaveSlotSummary summary)
    {
        if (
            summary.Status is not (
                SaveSlotStatus.Valid or SaveSlotStatus.ScreenshotMissing
            )
        )
        {
            Notify(summary.Error ?? "该槽位无法读取。");
            return;
        }

        if (_vnMode is null)
        {
            await StartAndLoadAsync(summary);
            return;
        }

        try
        {
            await RestoreSlotAsync(summary);
            CloseGameMenu();
            Notify(
                summary.Status == SaveSlotStatus.ScreenshotMissing
                    ? "读取完成；该存档截图缺失。"
                    : "读取完成。"
            );
        }
        catch (Exception exception)
        {
            Notify($"读取失败：{exception.Message}");
        }
    }

    private async Task RestoreSlotAsync(SaveSlotSummary summary)
    {
        var loaded = _saveService.Load(summary.Slot);
        await _vnMode!.Director.RestoreAsync(loaded.Document, _saveService);
    }

    private void ConfirmDelete(SaveLoadScreen screen, SaveSlotSummary summary)
    {
        Confirm(
            "删除存档",
            $"确定删除槽位 {summary.Slot.Index:00} 吗？此操作无法撤销。",
            () =>
            {
                try
                {
                    _saveService.Delete(summary.Slot);
                    screen.Refresh();
                    RefreshTitle();
                    Notify("存档已删除。");
                }
                catch (Exception exception)
                {
                    Notify($"删除失败：{exception.Message}");
                }
            }
        );
    }

    private void OpenHistory()
    {
        if (_vnMode is null)
        {
            return;
        }

        RemoveOverlay();
        _gameMenu?.Hide();
        var screen = _historyScene.Instantiate<HistoryScreen>();
        _overlayHost.AddChild(screen);
        _overlay = screen;
        screen.Open(_vnMode.Session);
        screen.Closed += CloseOverlay;
    }

    private void OpenSettings()
    {
        RemoveOverlay();
        _gameMenu?.Hide();
        var screen = _settingsScene.Instantiate<SettingsScreen>();
        _overlayHost.AddChild(screen);
        _overlay = screen;
        screen.Open();
        screen.Closed += CloseOverlay;
        screen.ErrorOccurred += Notify;
    }

    private void CloseOverlay()
    {
        RemoveOverlay();
        if (_gameMenu is not null)
        {
            _gameMenu.Show();
            _gameMenu.FocusDefault();
        }
        else
        {
            if (
                _overlayPausesGame &&
                _vnMode?.Director.Operation == OperationState.Menu
            )
            {
                _overlayPausesGame = false;
                _vnMode.Director.ToggleMenuOperation();
            }
            else
            {
                RefreshTitle();
            }
        }
    }

    private void CloseOverlayToGameMenu()
    {
        if (_gameMenu is null)
        {
            CloseOverlay();
            return;
        }

        RemoveOverlay();
        _gameMenu.Show();
        _gameMenu.FocusDefault();
    }

    private void ConfirmReturnToTitle()
    {
        Confirm(
            "返回标题",
            "未保存的进度将丢失，确定返回标题吗？",
            ReturnToTitle
        );
    }

    private void OnQuickHistoryRequested()
    {
        OpenQuickOverlay(OpenHistory);
    }

    private void OnQuickSaveRequested()
    {
        OpenQuickOverlay(() => OpenSaveScreen(SaveScreenMode.Save));
    }

    private void OnQuickSettingsRequested()
    {
        OpenQuickOverlay(OpenSettings);
    }

    private void OpenQuickOverlay(Action open)
    {
        if (
            _vnMode is null ||
            _gameMenu is not null ||
            _overlay is not null ||
            _vnMode.Director.Operation != OperationState.None
        )
        {
            return;
        }

        _vnMode.Director.ToggleMenuOperation();
        if (_vnMode.Director.Operation != OperationState.Menu)
        {
            return;
        }

        _overlayPausesGame = true;
        open();
    }

    private void ConfirmExit()
    {
        Confirm("退出游戏", "确定退出 SummerTrip 吗？", () => GetTree().Quit());
    }

    private void ShowAbout()
    {
        _about.DialogText =
            "SummerTrip · 秋日旅途\nGodot 4.7.1 .NET 迁移版\n\n" +
            "当前内容：序章六条路线与三个结尾。\n" +
            "Ren'Py 旧存档与 Godot 存档不兼容。";
        _about.PopupCentered(new Vector2I(760, 420));
    }

    private void Confirm(string title, string message, Action action)
    {
        _confirmedAction = action;
        _confirmation.Title = title;
        _confirmation.DialogText = message;
        _confirmation.PopupCentered(new Vector2I(720, 300));
    }

    private void OnConfirmed()
    {
        var action = _confirmedAction;
        _confirmedAction = null;
        action?.Invoke();
    }

    private void ReturnToTitle()
    {
        _overlayPausesGame = false;
        RemoveOverlay();
        RemoveGameMenu();
        ReleaseVnMode();
        RefreshTitle();
        _titleMenu.Show();
    }

    private void ReleaseVnMode()
    {
        if (_vnMode is null)
        {
            return;
        }

        _vnMode.GameMenuRequested -= OnGameMenuRequested;
        _vnMode.HistoryRequested -= OnQuickHistoryRequested;
        _vnMode.SaveScreenRequested -= OnQuickSaveRequested;
        _vnMode.SettingsRequested -= OnQuickSettingsRequested;
        _vnMode.NotificationRequested -= Notify;
        _modeHost.RemoveChild(_vnMode);
        _vnMode.QueueFree();
        _vnMode = null;
    }

    private void RemoveGameMenu()
    {
        if (_gameMenu is null)
        {
            return;
        }

        _overlayHost.RemoveChild(_gameMenu);
        _gameMenu.QueueFree();
        _gameMenu = null;
    }

    private void RemoveOverlay()
    {
        if (_overlay is null)
        {
            return;
        }

        _overlayHost.RemoveChild(_overlay);
        _overlay.QueueFree();
        _overlay = null;
    }

    private void RefreshTitle()
    {
        _titleMenu.SetContinueAvailable(_saveService.GetLatestValidSlot() is not null);
    }

    private async void Notify(string message)
    {
        var generation = ++_toastGeneration;
        _toast.Text = message;
        _toast.Show();
        await ToSignal(GetTree().CreateTimer(3.0), SceneTreeTimer.SignalName.Timeout);
        if (generation == _toastGeneration)
        {
            _toast.Hide();
        }
    }
}
