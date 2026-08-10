using Godot;
using SummerTrip.Core.Settings;
using System;

namespace SummerTrip.Features.Settings;

public enum SettingsMenuContext
{
    Title,
    Game,
}

public partial class SettingsScreen : Control
{
    private SettingsService _settings = null!;
    private bool _syncing;
    private CheckBox _windowMode = null!;
    private CheckBox _fullscreenMode = null!;
    private HSlider _textSpeed = null!;
    private HSlider _autoForward = null!;
    private CheckBox _skipUnread = null!;
    private CheckBox _keepSkip = null!;
    private CheckBox _ignoreTransitions = null!;
    private HSlider _music = null!;
    private HSlider _sound = null!;
    private HSlider _voice = null!;
    private CheckBox _muteAll = null!;

    public event Action? Closed;
    public event Action? NewGameRequested;
    public event Action? HistoryRequested;
    public event Action? SaveRequested;
    public event Action? LoadRequested;
    public event Action? ReturnTitleRequested;
    public event Action? AboutRequested;
    public event Action? HelpRequested;
    public event Action? ExitRequested;
    public event Action<string>? ErrorOccurred;

    public override void _Ready()
    {
        _settings = GetNode<SettingsService>("/root/SettingsService");
        _windowMode = GetNode<CheckBox>("%WindowMode");
        _fullscreenMode = GetNode<CheckBox>("%FullscreenMode");
        _textSpeed = GetNode<HSlider>("%TextSpeed");
        _autoForward = GetNode<HSlider>("%AutoForward");
        _skipUnread = GetNode<CheckBox>("%SkipUnread");
        _keepSkip = GetNode<CheckBox>("%KeepSkip");
        _ignoreTransitions = GetNode<CheckBox>("%IgnoreTransitions");
        _music = GetNode<HSlider>("%MusicVolume");
        _sound = GetNode<HSlider>("%SoundVolume");
        _voice = GetNode<HSlider>("%VoiceVolume");
        _muteAll = GetNode<CheckBox>("%MuteAll");

        var displayGroup = new ButtonGroup { AllowUnpress = false };
        _windowMode.ButtonGroup = displayGroup;
        _fullscreenMode.ButtonGroup = displayGroup;

        _windowMode.Pressed += () => Change(settings => settings.Fullscreen = false);
        _fullscreenMode.Pressed += () => Change(settings => settings.Fullscreen = true);
        _textSpeed.ValueChanged += value => Change(settings =>
            settings.TextCharactersPerSecond = (float)value
        );
        _autoForward.ValueChanged += value => Change(settings =>
            settings.AutoForwardTime = (float)value
        );
        _skipUnread.Toggled += value => Change(settings =>
            settings.SkipUnreadText = value
        );
        _keepSkip.Toggled += value => Change(settings =>
            settings.KeepSkippingAfterChoice = value
        );
        _ignoreTransitions.Toggled += value => Change(settings =>
            settings.TransitionsEnabled = !value
        );
        _music.ValueChanged += value => Change(settings =>
            settings.MusicVolume = (float)value
        );
        _sound.ValueChanged += value => Change(settings =>
            settings.SoundVolume = (float)value
        );
        _voice.ValueChanged += value => Change(settings =>
            settings.VoiceVolume = (float)value
        );
        _muteAll.Toggled += value => Change(settings => settings.MuteAll = value);

        GetNode<Button>("%NewGameButton").Pressed += () => NewGameRequested?.Invoke();
        GetNode<Button>("%HistoryButton").Pressed += () => HistoryRequested?.Invoke();
        GetNode<Button>("%SaveButton").Pressed += () => SaveRequested?.Invoke();
        GetNode<Button>("%LoadButton").Pressed += () => LoadRequested?.Invoke();
        GetNode<Button>("%TitleButton").Pressed += () => ReturnTitleRequested?.Invoke();
        GetNode<Button>("%AboutButton").Pressed += () => AboutRequested?.Invoke();
        GetNode<Button>("%HelpButton").Pressed += () => HelpRequested?.Invoke();
        GetNode<Button>("%ExitButton").Pressed += () => ExitRequested?.Invoke();
        GetNode<Button>("%ReturnButton").Pressed += () => Closed?.Invoke();
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

    public void Open(
        SettingsMenuContext context = SettingsMenuContext.Title,
        bool saveAvailable = false,
        bool loadAvailable = true
    )
    {
        var inGame = context == SettingsMenuContext.Game;
        GetNode<Button>("%NewGameButton").Visible = !inGame;
        GetNode<Button>("%HistoryButton").Visible = inGame;
        GetNode<Button>("%SaveButton").Visible = inGame;
        GetNode<Button>("%TitleButton").Visible = inGame;
        GetNode<Button>("%SaveButton").Disabled = !saveAvailable;
        GetNode<Button>("%LoadButton").Disabled = !loadAvailable;

        SyncControls();
        (inGame ? GetNode<Button>("%HistoryButton") : GetNode<Button>("%NewGameButton"))
            .GrabFocus();
    }

    private void Change(Action<SettingsData> mutate)
    {
        if (_syncing)
        {
            return;
        }

        try
        {
            mutate(_settings.Current);
            _settings.SaveAndApply();
        }
        catch (Exception exception)
        {
            ErrorOccurred?.Invoke($"无法保存设置：{exception.Message}");
            _settings.Load();
            _settings.Apply();
            SyncControls();
        }
    }

    private void SyncControls()
    {
        _syncing = true;
        var settings = _settings.Current;
        _windowMode.ButtonPressed = !settings.Fullscreen;
        _fullscreenMode.ButtonPressed = settings.Fullscreen;
        _textSpeed.Value = settings.TextCharactersPerSecond;
        _autoForward.Value = settings.AutoForwardTime;
        _skipUnread.ButtonPressed = settings.SkipUnreadText;
        _keepSkip.ButtonPressed = settings.KeepSkippingAfterChoice;
        _ignoreTransitions.ButtonPressed = !settings.TransitionsEnabled;
        _music.Value = settings.MusicVolume;
        _sound.Value = settings.SoundVolume;
        _voice.Value = settings.VoiceVolume;
        _muteAll.ButtonPressed = settings.MuteAll;
        _syncing = false;
    }
}
