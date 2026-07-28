using Godot;
using SummerTrip.Core.Capture;
using SummerTrip.Core.Dialogue;
using SummerTrip.Core.Save;
using SummerTrip.Core.Session;
using SummerTrip.Core.Settings;
using SummerTrip.Features.Vn.Stage;
using SummerTrip.Features.Vn.Ui;
using System;
using System.Threading.Tasks;

namespace SummerTrip.Features.Vn;

public partial class VNMode : Control
{
    private const string DialoguePath = "res://content/dialogue/prologue.dialogue";
    private const double SkipAdvanceInterval = 0.05;

    private DialogueGateway? _gateway;
    private GameSession? _session;
    private ProfileData? _profile;
    private SaveService? _saveService;
    private PrologueProgression? _progression;
    private DialogueBox? _dialogueBox;
    private QuickMenu? _quickMenu;
    private VNRollbackBuffer? _rollback;
    private SettingsService? _settings;
    private ScreenshotService? _screenshots;
    private bool _autoPlaying;
    private bool _skipToggled;
    private bool _uiHidden;
    private bool _advanceInFlight;
    private bool _skipBlockedNotified;
    private double _autoElapsed;
    private double _skipElapsed;

    public event Action? Initialized;
    public event Action<string>? InitializationFailed;
    public event Action? GameMenuRequested;
    public event Action? HistoryRequested;
    public event Action? SaveScreenRequested;
    public event Action? SettingsRequested;
    public event Action<string>? NotificationRequested;

    public GameSession Session =>
        _session ?? throw new InvalidOperationException("VNMode is not ready.");
    public VNDirector Director { get; private set; } = null!;
    public SaveService Saves =>
        _saveService ?? throw new InvalidOperationException("VNMode is not ready.");
    public bool IsAutoPlaying => _autoPlaying;
    public bool IsSkipToggled => _skipToggled;
    public bool IsUiHidden => _uiHidden;
    public int RollbackSnapshotCount => _rollback?.Count ?? 0;
    public bool CanRollback => _rollback?.CanRollback == true;
    public string SaveRoot { get; set; } = "user://saves";
    public string ScreenshotRoot { get; set; } = "user://screenshots";

    public override async void _Ready()
    {
        try
        {
            var stage = GetNode<StageController>("%StageController");
            Director = GetNode<VNDirector>("%VNDirector");
            _dialogueBox = GetNode<DialogueBox>("%DialogueBox");
            _quickMenu = GetNode<QuickMenu>("%QuickMenu");
            _settings = GetNode<SettingsService>("/root/SettingsService");
            _screenshots = new ScreenshotService(ScreenshotRoot);

            _session = new GameSession();
            _saveService = new SaveService(SaveRoot);
            _profile = _saveService.LoadProfile();
            _gateway = new DialogueGateway(
                DialoguePath,
                new DialogueContext(_session, stage)
            );

            Director.Initialize(_gateway, _session, stage, _profile);
            _dialogueBox.Bind(Director);
            _dialogueBox.MenuRequested += OnGameMenuRequested;
            _dialogueBox.AdvanceRequested += OnAdvanceRequested;
            _dialogueBox.ChoiceRequested += OnChoiceRequested;
            Director.StableLineReached += OnStableLineReached;

            _rollback = new VNRollbackBuffer(Director);
            _rollback.AvailabilityChanged += RefreshQuickMenu;
            BindQuickMenu(_quickMenu);
            _progression = new PrologueProgression(
                Director,
                _profile,
                _saveService,
                stage.Catalog,
                CaptureSaveScreenshot
            );

            await Director.StartAsync();
            RefreshQuickMenu();
            Initialized?.Invoke();
        }
        catch (Exception exception)
        {
            GD.PushError($"Unable to start VN mode: {exception}");
            InitializationFailed?.Invoke(exception.Message);
        }
    }

    public override void _ExitTree()
    {
        if (_dialogueBox is not null)
        {
            _dialogueBox.MenuRequested -= OnGameMenuRequested;
            _dialogueBox.AdvanceRequested -= OnAdvanceRequested;
            _dialogueBox.ChoiceRequested -= OnChoiceRequested;
        }
        if (Director is not null)
        {
            Director.StableLineReached -= OnStableLineReached;
        }
        if (_rollback is not null)
        {
            _rollback.AvailabilityChanged -= RefreshQuickMenu;
            _rollback.Dispose();
        }
        _progression?.Dispose();
        _gateway?.Dispose();
        _session?.Dispose();
    }

    public override void _Process(double delta)
    {
        if (_session is null || Director is null)
        {
            return;
        }

        if (Director.Operation == OperationState.None)
        {
            _session.PlayTimeSeconds += delta;
        }
        else
        {
            ResetAutomationTimers();
            return;
        }

        if (Director.Interaction == InteractionState.AwaitingChoice)
        {
            ResetAutomationTimers();
            return;
        }

        var skipActive = _skipToggled || Input.IsActionPressed("vn_skip");
        if (skipActive)
        {
            ProcessSkip(delta);
            return;
        }

        _skipBlockedNotified = false;
        _skipElapsed = 0.0;
        if (_autoPlaying && Director.Interaction == InteractionState.AwaitingAdvance)
        {
            _autoElapsed += delta;
            if (_autoElapsed >= (_settings?.Current.AutoWaitSeconds ?? 2.0f))
            {
                _autoElapsed = 0.0;
                _ = AdvanceAutomaticallyAsync();
            }
        }
        else
        {
            _autoElapsed = 0.0;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsPressed() || @event.IsEcho() || Director is null)
        {
            return;
        }

        if (@event.IsActionPressed("vn_hide_ui"))
        {
            ToggleUiVisibility();
        }
        else if (@event.IsActionPressed("vn_auto"))
        {
            ToggleAuto();
        }
        else if (@event.IsActionPressed("vn_rollback"))
        {
            _ = RollbackAsync();
        }
        else if (@event.IsActionPressed("vn_quick_save"))
        {
            _ = QuickSaveAsync();
        }
        else if (@event.IsActionPressed("vn_quick_load"))
        {
            _ = QuickLoadAsync();
        }
        else if (@event.IsActionPressed("vn_screenshot"))
        {
            TakeScreenshot();
        }
        else
        {
            return;
        }

        GetViewport().SetInputAsHandled();
    }

    public void ToggleUiVisibility()
    {
        _uiHidden = !_uiHidden;
        _dialogueBox?.SetUserHidden(_uiHidden);
        if (_quickMenu is not null)
        {
            _quickMenu.Visible = !_uiHidden;
        }
    }

    public void ToggleAuto()
    {
        if (Director.Operation != OperationState.None)
        {
            return;
        }

        _autoPlaying = !_autoPlaying;
        if (_autoPlaying)
        {
            _skipToggled = false;
        }
        ResetAutomationTimers();
        RefreshQuickMenu();
    }

    public void ToggleSkip()
    {
        if (Director.Operation != OperationState.None)
        {
            return;
        }

        _skipToggled = !_skipToggled;
        if (_skipToggled)
        {
            _autoPlaying = false;
        }
        _skipBlockedNotified = false;
        ResetAutomationTimers();
        RefreshQuickMenu();
    }

    public async Task QuickSaveAsync()
    {
        if (_saveService is null || Director.Operation != OperationState.None)
        {
            return;
        }

        StopAutomation();
        try
        {
            using var screenshot = CaptureSaveScreenshot();
            Director.Save(
                _saveService,
                new SaveSlot(SaveSlotKind.Quick, 1),
                screenshot
            );
            Notify("快速存档完成。");
        }
        catch (Exception exception)
        {
            Notify($"快速存档失败：{exception.Message}");
        }
        finally
        {
            RefreshQuickMenu();
        }
        await Task.CompletedTask;
    }

    public async Task QuickLoadAsync()
    {
        if (_saveService is null || Director.Operation != OperationState.None)
        {
            return;
        }

        StopAutomation();
        var summary = _saveService.Inspect(new SaveSlot(SaveSlotKind.Quick, 1));
        if (
            summary.Status is not (
                SaveSlotStatus.Valid or SaveSlotStatus.ScreenshotMissing
            )
        )
        {
            Notify(summary.Error ?? "没有可读取的快速存档。");
            return;
        }

        try
        {
            var loaded = _saveService.Load(summary.Slot);
            await Director.RestoreAsync(loaded.Document, _saveService);
            Notify(
                summary.Status == SaveSlotStatus.ScreenshotMissing
                    ? "快速读取完成；存档截图缺失。"
                    : "快速读取完成。"
            );
        }
        catch (Exception exception)
        {
            Notify($"快速读取失败：{exception.Message}");
        }
        finally
        {
            RefreshQuickMenu();
        }
    }

    public async Task<bool> RollbackAsync()
    {
        if (_rollback is null)
        {
            return false;
        }

        StopAutomation();
        try
        {
            var restored = await _rollback.RollbackAsync();
            if (!restored)
            {
                Notify("没有更早的对白可回退。");
            }
            return restored;
        }
        catch (Exception exception)
        {
            Notify($"回退失败：{exception.Message}");
            return false;
        }
        finally
        {
            RefreshQuickMenu();
        }
    }

    public void TakeScreenshot()
    {
        try
        {
            using var image = CaptureViewportImage();
            var result = _screenshots!.Save(image);
            Notify($"截图已保存：{result.FileName}");
        }
        catch (Exception exception)
        {
            Notify($"截图失败：{exception.Message}");
        }
    }

    public Image CaptureSaveScreenshot()
    {
        var image = CaptureViewportImage();
        image.Resize(
            SaveService.ScreenshotWidth,
            SaveService.ScreenshotHeight,
            Image.Interpolation.Lanczos
        );
        return image;
    }

    private Image CaptureViewportImage()
    {
        if (string.Equals(
                DisplayServer.GetName(),
                "headless",
                StringComparison.OrdinalIgnoreCase
            ))
        {
            var placeholder = Image.CreateEmpty(
                1920,
                1080,
                false,
                Image.Format.Rgba8
            );
            placeholder.Fill(Colors.Black);
            return placeholder;
        }

        var image = GetViewport().GetTexture().GetImage();
        if (image is null || image.IsEmpty())
        {
            image?.Dispose();
            throw new InvalidOperationException("Viewport screenshot is empty.");
        }
        return image;
    }

    private void BindQuickMenu(QuickMenu menu)
    {
        menu.RollbackRequested += () => _ = RollbackAsync();
        menu.HistoryRequested += RequestHistory;
        menu.SkipRequested += ToggleSkip;
        menu.AutoRequested += ToggleAuto;
        menu.SaveRequested += RequestSaveScreen;
        menu.QuickSaveRequested += () => _ = QuickSaveAsync();
        menu.QuickLoadRequested += () => _ = QuickLoadAsync();
        menu.SettingsRequested += RequestSettings;
    }

    private void ProcessSkip(double delta)
    {
        var canSkipCurrent =
            Director.CurrentLine is not null &&
            (Director.CurrentLineWasRead || _settings?.Current.SkipUnreadText == true);
        if (!canSkipCurrent)
        {
            _skipElapsed = 0.0;
            if (_skipToggled)
            {
                _skipToggled = false;
                RefreshQuickMenu();
            }
            if (!_skipBlockedNotified)
            {
                _skipBlockedNotified = true;
                Notify("快进已在未读对白处停止。");
            }
            return;
        }

        _skipBlockedNotified = false;
        if (Director.Interaction == InteractionState.Typing)
        {
            Director.CompleteCurrentTyping();
            return;
        }
        if (Director.Interaction != InteractionState.AwaitingAdvance)
        {
            _skipElapsed = 0.0;
            return;
        }

        _skipElapsed += delta;
        if (_skipElapsed >= SkipAdvanceInterval)
        {
            _skipElapsed = 0.0;
            _ = AdvanceAutomaticallyAsync();
        }
    }

    private async Task AdvanceAutomaticallyAsync()
    {
        if (_advanceInFlight || Director.Operation != OperationState.None)
        {
            return;
        }

        _advanceInFlight = true;
        try
        {
            await Director.AdvanceAsync();
        }
        catch (Exception exception)
        {
            StopAutomation();
            Notify($"自动推进失败：{exception.Message}");
        }
        finally
        {
            _advanceInFlight = false;
        }
    }

    private void OnAdvanceRequested()
    {
        if (_uiHidden)
        {
            ToggleUiVisibility();
            return;
        }

        StopAutomation();
        _ = AdvanceManuallyAsync();
    }

    private async Task AdvanceManuallyAsync()
    {
        try
        {
            await Director.AdvanceAsync();
        }
        catch (Exception exception)
        {
            Notify($"推进失败：{exception.Message}");
        }
    }

    private void OnChoiceRequested(int choiceIndex)
    {
        if (_skipToggled && _settings?.Current.KeepSkippingAfterChoice != true)
        {
            _skipToggled = false;
        }
        ResetAutomationTimers();
        RefreshQuickMenu();
        _ = ChooseResponseAsync(choiceIndex);
    }

    public async Task ChooseResponseAsync(int choiceIndex)
    {
        try
        {
            await Director.ChooseAsync(choiceIndex);
        }
        catch (Exception exception)
        {
            StopAutomation();
            Notify($"选择失败：{exception.Message}");
        }
    }

    private void OnStableLineReached(DialogueEntry line)
    {
        ResetAutomationTimers();
        RefreshQuickMenu();
    }

    private void OnGameMenuRequested()
    {
        StopAutomation();
        GameMenuRequested?.Invoke();
    }

    private void RequestHistory()
    {
        StopAutomation();
        HistoryRequested?.Invoke();
    }

    private void RequestSaveScreen()
    {
        StopAutomation();
        SaveScreenRequested?.Invoke();
    }

    private void RequestSettings()
    {
        StopAutomation();
        SettingsRequested?.Invoke();
    }

    private void StopAutomation()
    {
        _autoPlaying = false;
        _skipToggled = false;
        ResetAutomationTimers();
        RefreshQuickMenu();
    }

    private void ResetAutomationTimers()
    {
        _autoElapsed = 0.0;
        _skipElapsed = 0.0;
    }

    private void RefreshQuickMenu()
    {
        if (_quickMenu is null || _saveService is null || Director is null)
        {
            return;
        }

        var ioAvailable =
            Director.Operation == OperationState.None &&
            Director.Interaction is
                InteractionState.AwaitingAdvance or InteractionState.AwaitingChoice;
        var quickLoadAvailable =
            _saveService.Inspect(new SaveSlot(SaveSlotKind.Quick, 1)).Status is
                SaveSlotStatus.Valid or SaveSlotStatus.ScreenshotMissing;
        _quickMenu.SetStates(
            _autoPlaying,
            _skipToggled,
            _rollback?.CanRollback == true,
            ioAvailable,
            quickLoadAvailable
        );
    }

    private void Notify(string message)
    {
        NotificationRequested?.Invoke(message);
    }
}
