using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ComicWeb.Application.Common.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace ComicWeb.Persistence.Content;

public class LocalImageStorageService : IImageStorageService
{
    private readonly StorageOptions _options;
    private readonly IWebHostEnvironment _env;

    public LocalImageStorageService(IOptions<StorageOptions> options, IWebHostEnvironment env)
    {
        _options = options.Value;
        _env = env;
    }

    public async Task<string> UploadImageAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var webRootPath = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var folderPath = Path.Combine(webRootPath, "uploads");
        
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";
        var filePath = Path.Combine(folderPath, uniqueFileName);

        using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await stream.CopyToAsync(fileStream, cancellationToken);
        }

        var baseUrl = _options.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/uploads/{uniqueFileName}";
    }

    public Task DeleteImageAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return Task.CompletedTask;
        }

        try
        {
            var uri = new Uri(imageUrl);
            var fileName = Path.GetFileName(uri.LocalPath);
            var webRootPath = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var folderPath = Path.Combine(webRootPath, "uploads");
            var filePath = Path.Combine(folderPath, fileName);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Fail silently on deletion error as per master plan requirements
        }

        return Task.CompletedTask;
    }
}
