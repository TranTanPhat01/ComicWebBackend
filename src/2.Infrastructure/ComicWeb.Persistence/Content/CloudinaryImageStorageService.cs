using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using ComicWeb.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace ComicWeb.Persistence.Content;

public class CloudinaryImageStorageService : IImageStorageService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryImageStorageService(IOptions<StorageOptions> options)
    {
        var settings = options.Value.Cloudinary ?? throw new InvalidOperationException("Cloudinary configurations are missing.");
        var account = new Account(settings.CloudName, settings.ApiKey, settings.ApiSecret);
        _cloudinary = new Cloudinary(account);
    }

    public async Task<string> UploadImageAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var sanitizedFileName = Path.GetFileNameWithoutExtension(fileName).Replace(" ", "_");
        var publicId = $"{sanitizedFileName}_{Guid.NewGuid().ToString().Substring(0, 8)}";

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(fileName, stream),
            Folder = "comicweb/covers",
            PublicId = publicId,
            Overwrite = true
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams, cancellationToken);
        
        if (uploadResult.Error != null)
        {
            throw new InvalidOperationException($"Cloudinary upload failed: {uploadResult.Error.Message}");
        }

        return uploadResult.SecureUrl.ToString();
    }

    public async Task DeleteImageAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return;
        }

        try
        {
            var publicId = GetPublicIdFromUrl(imageUrl);
            if (!string.IsNullOrEmpty(publicId))
            {
                await _cloudinary.DestroyAsync(new DeletionParams(publicId));
            }
        }
        catch
        {
            // Fail silently on deletion error as per master plan requirements
        }
    }

    private string GetPublicIdFromUrl(string url)
    {
        // Example: https://res.cloudinary.com/cloudname/image/upload/v12345/comicweb/covers/filename_guid.jpg
        var parts = url.Split("/upload/");
        if (parts.Length < 2)
        {
            return string.Empty;
        }

        var pathAfterUpload = parts[1];
        
        // Strip version number (e.g. v123456789/) if present
        var slashIndex = pathAfterUpload.IndexOf('/');
        if (pathAfterUpload.StartsWith("v") && slashIndex > 0 && long.TryParse(pathAfterUpload.Substring(1, slashIndex - 1), out _))
        {
            pathAfterUpload = pathAfterUpload.Substring(slashIndex + 1);
        }

        // Strip file extension
        var extensionIndex = pathAfterUpload.LastIndexOf('.');
        if (extensionIndex > 0)
        {
            pathAfterUpload = pathAfterUpload.Substring(0, extensionIndex);
        }

        return pathAfterUpload;
    }
}
