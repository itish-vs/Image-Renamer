using System.Text.Json.Serialization;

namespace ImageRenamer.Models;

public sealed class AppSettings
{
    [JsonPropertyName("ImageRenamer")]
    public ImageRenamerOptions ImageRenamer { get; set; } = new();
}

public sealed class ImageRenamerOptions
{
    public string ImageFolder { get; set; } = @"C:\Images";

    public string TempFolderPrefix { get; set; } = ".image-renamer-tmp";

    public List<string> SupportedExtensions { get; set; } =
    [
        ".jpg", ".jpeg", ".jpe", ".jfif", ".png", ".bmp", ".dib",
        ".gif", ".tif", ".tiff", ".webp", ".ico", ".heic", ".heif", ".avif", ".svg",
        ".dng", ".cr2", ".cr3", ".nef", ".nrw", ".arw", ".orf", ".rw2", ".raf", ".pef", ".srw", ".tga", ".psd"
    ];
}
