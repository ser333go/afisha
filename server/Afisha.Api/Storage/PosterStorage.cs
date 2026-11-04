namespace Afisha.Api.Storage;

/// <summary>Сохраняет постеры на диск под именем Guid и проверяет формат/размер файла.</summary>
public sealed class PosterStorage(IWebHostEnvironment environment)
{
    public const long MaxFileSize = 5 * 1024 * 1024;

    private static readonly Dictionary<string, string[]> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = ["image/jpeg"],
        [".jpeg"] = ["image/jpeg"],
        [".png"] = ["image/png"],
        [".webp"] = ["image/webp"],
    };

    public async Task<(string? Path, string? Error)> SaveAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return (null, "Выберите файл постера.");
        }

        if (file.Length > MaxFileSize)
        {
            return (null, "Файл не должен превышать 5 МБ.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedTypes.TryGetValue(extension, out var contentTypes))
        {
            return (null, "Допустимые форматы: jpg, png, webp.");
        }

        if (!contentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return (null, "Тип файла не соответствует расширению. Допустимые форматы: jpg, png, webp.");
        }

        byte[] bytes;
        await using (var stream = file.OpenReadStream())
        using (var memory = new MemoryStream())
        {
            await stream.CopyToAsync(memory, cancellationToken);
            bytes = memory.ToArray();
        }

        if (!HasValidSignature(bytes, extension))
        {
            return (null, "Содержимое файла не является изображением jpg, png или webp.");
        }

        var directory = Path.Combine(WebRootPath, "uploads", "posters");
        Directory.CreateDirectory(directory);

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var physicalPath = Path.Combine(directory, fileName);
        await File.WriteAllBytesAsync(physicalPath, bytes, cancellationToken);

        return ($"/uploads/posters/{fileName}", null);
    }

    public void Delete(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        var relative = relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(WebRootPath);
        var fullPath = Path.GetFullPath(Path.Combine(root, relative));

        if (!fullPath.StartsWith(root, StringComparison.Ordinal))
        {
            return;
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    private string WebRootPath => environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");

    private static bool HasValidSignature(byte[] bytes, string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            ".png" => bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47,
            ".webp" => bytes.Length >= 12
                       && bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'
                       && bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P',
            _ => false,
        };
    }
}
