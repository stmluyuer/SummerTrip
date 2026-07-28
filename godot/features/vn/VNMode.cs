using Godot;
using SummerTrip.Core.Dialogue;
using SummerTrip.Core.Save;
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
    private SaveService? _saveService;
    private PrologueProgression? _progression;

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
            _saveService = new SaveService();
            _profile = _saveService.LoadProfile();
            var context = new DialogueContext(_session, stage);
            _gateway = new DialogueGateway(DialoguePath, context);

            director.Initialize(_gateway, _session, stage, _profile);
            dialogueBox.Bind(director);
            _progression = new PrologueProgression(
                director,
                _profile,
                _saveService,
                stage.Catalog,
                CaptureSaveScreenshot
            );
            await director.StartAsync();
        }
        catch (Exception exception)
        {
            GD.PushError($"Unable to start VN mode: {exception}");
        }
    }

    public override void _ExitTree()
    {
        _progression?.Dispose();
        _gateway?.Dispose();
        _session?.Dispose();
    }

    private Image CaptureSaveScreenshot()
    {
        var image = GetViewport().GetTexture().GetImage();
        if (image.IsEmpty())
        {
            image.Dispose();
            throw new InvalidOperationException("Viewport screenshot is empty.");
        }

        image.Resize(
            SaveService.ScreenshotWidth,
            SaveService.ScreenshotHeight,
            Image.Interpolation.Lanczos
        );
        return image;
    }
}
