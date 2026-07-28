using Godot;
using SummerTrip.Core.Save;
using System;
using System.Globalization;

namespace SummerTrip.Features.SaveUi;

public partial class SaveSlot : PanelContainer
{
    private Button _selectButton = null!;
    private TextureRect _screenshot = null!;
    private Label _title = null!;
    private Label _time = null!;
    private Label _status = null!;
    private Button _deleteButton = null!;

    public event Action<SaveSlotSummary>? Activated;
    public event Action<SaveSlotSummary>? DeleteRequested;
    public SaveSlotSummary Summary { get; private set; } = null!;

    public override void _Ready()
    {
        _selectButton = GetNode<Button>("%SelectButton");
        _screenshot = GetNode<TextureRect>("%Screenshot");
        _title = GetNode<Label>("%Title");
        _time = GetNode<Label>("%Time");
        _status = GetNode<Label>("%Status");
        _deleteButton = GetNode<Button>("%DeleteButton");
        _selectButton.Pressed += () => Activated?.Invoke(Summary);
        _deleteButton.Pressed += () => DeleteRequested?.Invoke(Summary);
    }

    public void Configure(SaveSlotSummary summary, bool allowWriting)
    {
        Summary = summary;
        _selectButton.Text = $"{KindName(summary.Slot.Kind)} {summary.Slot.Index:00}";
        _deleteButton.Visible = summary.Status != SaveSlotStatus.Empty;
        _selectButton.Disabled =
            !allowWriting &&
            summary.Status is not (
                SaveSlotStatus.Valid or SaveSlotStatus.ScreenshotMissing
            );
        _screenshot.Texture = null;

        if (summary.Document is null)
        {
            _title.Text = summary.Status == SaveSlotStatus.Empty ? "空槽位" : "无法读取";
            _time.Text = "";
            _status.Text = summary.Status == SaveSlotStatus.Corrupt
                ? "损坏"
                : "可保存";
            _status.TooltipText = summary.Error ?? "";
            return;
        }

        _title.Text = summary.Document.Story.ChapterId == "prologue"
            ? "序章"
            : summary.Document.Story.ChapterId;
        _time.Text = DateTimeOffset.TryParseExact(
            summary.Document.SavedAt,
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var savedAt
        )
            ? savedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture)
            : summary.Document.SavedAt;
        _status.Text = summary.Status == SaveSlotStatus.ScreenshotMissing
            ? "截图缺失 · 可读取"
            : "可读取";
        _status.TooltipText = summary.Error ?? "";

        if (summary.ScreenshotPath is not null)
        {
            using var image = Image.LoadFromFile(summary.ScreenshotPath);
            if (image is not null && !image.IsEmpty())
            {
                _screenshot.Texture = ImageTexture.CreateFromImage(image);
            }
        }
    }

    public void FocusSlot()
    {
        _selectButton.GrabFocus();
    }

    private static string KindName(SaveSlotKind kind)
    {
        return kind switch
        {
            SaveSlotKind.Manual => "手动",
            SaveSlotKind.Quick => "快速",
            SaveSlotKind.Auto => "自动",
            _ => "存档",
        };
    }
}
