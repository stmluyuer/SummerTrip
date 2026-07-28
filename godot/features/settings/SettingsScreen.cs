using Godot;
using SummerTrip.Core.Settings;
using System;

namespace SummerTrip.Features.Settings;

public partial class SettingsScreen : Control
{
    private SettingsService _settings = null!;
    private bool _syncing;
    private OptionButton _displayMode = null!;
    private HSlider _textSpeed = null!;
    private HSlider _autoWait = null!;
    private CheckButton _skipUnread = null!;
    private CheckButton _keepSkip = null!;
    private HSlider _music = null!;
    private HSlider _sound = null!;
    private HSlider _voice = null!;
    private CheckButton _muteAll = null!;

    public event Action? Closed;
    public event Action<string>? ErrorOccurred;

    public override void _Ready()
    {
        _settings = GetNode<SettingsService>("/root/SettingsService");
        _displayMode = GetNode<OptionButton>("%DisplayMode");
        _textSpeed = GetNode<HSlider>("%TextSpeed");
        _autoWait = GetNode<HSlider>("%AutoWait");
        _skipUnread = GetNode<CheckButton>("%SkipUnread");
        _keepSkip = GetNode<CheckButton>("%KeepSkip");
        _music = GetNode<HSlider>("%MusicVolume");
        _sound = GetNode<HSlider>("%SoundVolume");
        _voice = GetNode<HSlider>("%VoiceVolume");
        _muteAll = GetNode<CheckButton>("%MuteAll");

        _displayMode.AddItem("窗口");
        _displayMode.AddItem("全屏");
        _displayMode.ItemSelected += index => Change(settings =>
            settings.Fullscreen = index == 1
        );
        _textSpeed.ValueChanged += value => Change(settings =>
            settings.TextCharactersPerSecond = (float)value
        );
        _autoWait.ValueChanged += value => Change(settings =>
            settings.AutoWaitSeconds = (float)value
        );
        _skipUnread.Toggled += value => Change(settings =>
            settings.SkipUnreadText = value
        );
        _keepSkip.Toggled += value => Change(settings =>
            settings.KeepSkippingAfterChoice = value
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
        _muteAll.Toggled += value => Change(settings =>
            settings.MuteAll = value
        );
        GetNode<Button>("%DefaultsButton").Pressed += ResetDefaults;
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

    public void Open()
    {
        SyncControls();
        _displayMode.GrabFocus();
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
            UpdateValueLabels();
        }
        catch (Exception exception)
        {
            ErrorOccurred?.Invoke($"无法保存设置：{exception.Message}");
            _settings.Load();
            _settings.Apply();
            SyncControls();
        }
    }

    private void ResetDefaults()
    {
        try
        {
            _settings.ResetToDefaults();
            SyncControls();
        }
        catch (Exception exception)
        {
            ErrorOccurred?.Invoke($"无法恢复默认设置：{exception.Message}");
        }
    }

    private void SyncControls()
    {
        _syncing = true;
        var settings = _settings.Current;
        _displayMode.Select(settings.Fullscreen ? 1 : 0);
        _textSpeed.Value = settings.TextCharactersPerSecond;
        _autoWait.Value = settings.AutoWaitSeconds;
        _skipUnread.ButtonPressed = settings.SkipUnreadText;
        _keepSkip.ButtonPressed = settings.KeepSkippingAfterChoice;
        _music.Value = settings.MusicVolume;
        _sound.Value = settings.SoundVolume;
        _voice.Value = settings.VoiceVolume;
        _muteAll.ButtonPressed = settings.MuteAll;
        _syncing = false;
        UpdateValueLabels();
    }

    private void UpdateValueLabels()
    {
        GetNode<Label>("%TextSpeedValue").Text = $"{_textSpeed.Value:0} 字/秒";
        GetNode<Label>("%AutoWaitValue").Text = $"{_autoWait.Value:0.0} 秒";
        GetNode<Label>("%MusicValue").Text = $"{_music.Value * 100.0:0}%";
        GetNode<Label>("%SoundValue").Text = $"{_sound.Value * 100.0:0}%";
        GetNode<Label>("%VoiceValue").Text = $"{_voice.Value * 100.0:0}%";
    }
}
