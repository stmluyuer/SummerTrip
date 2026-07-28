using Godot;
using System;

namespace SummerTrip.Features.Menu;

public partial class TitleMenu : Control
{
    public event Action? NewGameRequested;
    public event Action? ContinueRequested;
    public event Action? LoadRequested;
    public event Action? SettingsRequested;
    public event Action? AboutRequested;
    public event Action? ExitRequested;

    public override void _Ready()
    {
        GetNode<Button>("%NewGameButton").Pressed += () => NewGameRequested?.Invoke();
        GetNode<Button>("%ContinueButton").Pressed += () => ContinueRequested?.Invoke();
        GetNode<Button>("%LoadButton").Pressed += () => LoadRequested?.Invoke();
        GetNode<Button>("%SettingsButton").Pressed += () => SettingsRequested?.Invoke();
        GetNode<Button>("%AboutButton").Pressed += () => AboutRequested?.Invoke();
        GetNode<Button>("%ExitButton").Pressed += () => ExitRequested?.Invoke();
        GetNode<Button>("%NewGameButton").GrabFocus();
    }

    public void SetContinueAvailable(bool available)
    {
        GetNode<Button>("%ContinueButton").Disabled = !available;
    }
}
