using Godot;

namespace SummerTrip.Core.Session;

public partial class GameSession : RefCounted
{
    public const string DefaultPlayerName = "旅人";

    [Export]
    public string player_name { get; set; } = DefaultPlayerName;

    [Export]
    public int minoriko_affection { get; set; }

    [Export]
    public int shizuha_trust { get; set; }

    [Export]
    public int autumn_mystery { get; set; }

    [Export]
    public string CurrentStaticLineId { get; set; } = "";

    public void Reset()
    {
        player_name = DefaultPlayerName;
        minoriko_affection = 0;
        shizuha_trust = 0;
        autumn_mystery = 0;
        CurrentStaticLineId = "";
    }
}
