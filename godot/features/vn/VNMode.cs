using Godot;
using SummerTrip.Core.Dialogue;
using SummerTrip.Core.Session;
using SummerTrip.Features.Vn.Stage;
using SummerTrip.Features.Vn.Ui;
using System;

namespace SummerTrip.Features.Vn;

public partial class VNMode : Control
{
    private const string DialoguePath = "res://content/dialogue/prologue.dialogue";

    private DialogueGateway? _gateway;
    private GameSession? _session;
    private ProfileData? _profile;

    public GameSession Session =>
        _session ?? throw new InvalidOperationException("VNMode is not ready.");

    public override async void _Ready()
    {
        try
        {
            var stage = GetNode<StageController>("%StageController");
            var director = GetNode<VNDirector>("%VNDirector");
            var dialogueBox = GetNode<DialogueBox>("%DialogueBox");

            _session = new GameSession();
            _profile = new ProfileData();
            var context = new DialogueContext(_session, stage);
            _gateway = new DialogueGateway(DialoguePath, context);

            director.Initialize(_gateway, _session, stage, _profile);
            dialogueBox.Bind(director);
            await director.StartAsync();
        }
        catch (Exception exception)
        {
            GD.PushError($"Unable to start VN mode: {exception}");
        }
    }

    public override void _ExitTree()
    {
        _gateway?.Dispose();
        _session?.Dispose();
    }
}
