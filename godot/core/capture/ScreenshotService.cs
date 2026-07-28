using Godot;
using System;
using System.Globalization;
using System.IO;

namespace SummerTrip.Core.Capture;

public sealed record ScreenshotResult(string FileName, string AbsolutePath);

public sealed class ScreenshotService
{
    private readonly string _screenshotRoot;

    public ScreenshotService(string screenshotRoot = "user://screenshots")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(screenshotRoot);
        _screenshotRoot = screenshotRoot.StartsWith("user://", StringComparison.Ordinal)
            ? ProjectSettings.GlobalizePath(screenshotRoot)
            : Path.GetFullPath(screenshotRoot);
    }

    public ScreenshotResult Save(Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.IsEmpty())
        {
            throw new InvalidOperationException("截图图像为空。");
        }

        Directory.CreateDirectory(_screenshotRoot);
        var timestamp = DateTimeOffset.Now.ToString(
            "yyyyMMdd_HHmmss_fff",
            CultureInfo.InvariantCulture
        );
        var fileName = $"screenshot_{timestamp}.png";
        var path = Path.Combine(_screenshotRoot, fileName);
        var suffix = 1;
        while (File.Exists(path))
        {
            fileName = $"screenshot_{timestamp}_{suffix:00}.png";
            path = Path.Combine(_screenshotRoot, fileName);
            suffix++;
        }

        var error = image.SavePng(path);
        if (error != Error.Ok)
        {
            throw new IOException($"无法保存截图：{error}。");
        }

        using var verification = Image.LoadFromFile(path);
        if (
            verification is null ||
            verification.IsEmpty() ||
            verification.GetWidth() != image.GetWidth() ||
            verification.GetHeight() != image.GetHeight()
        )
        {
            throw new IOException("截图写入后无法按原尺寸重新读取。");
        }

        return new ScreenshotResult(fileName, path);
    }
}
