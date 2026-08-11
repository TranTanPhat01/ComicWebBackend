using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ComicWeb.Application.Common.Exceptions;
using ComicWeb.Application.Common.Interfaces;
using ComicWeb.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ComicWeb.WebApi.Controllers;

[ApiController]
[Route("api/v1/admin/upload")]
[Authorize(Policy = "AdminOnly")]
public sealed class UploadController : BaseApiController
{
    private readonly IImageStorageService _storageService;

    private static readonly Dictionary<string, byte[]> ImageSignatures = new()
    {
        { "image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF } },
        { "image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47 } },
        { "image/gif", new byte[] { 0x47, 0x49, 0x46, 0x38 } },
        { "image/webp", new byte[] { 0x52, 0x49, 0x46, 0x46 } } // "RIFF"
    };

    private static readonly string[] AllowedMimeTypes = { "image/jpeg", "image/png", "image/webp", "image/gif" };
    private const long MaxFileSizeInBytes = 2 * 1024 * 1024; // 2MB

    public UploadController(IImageStorageService storageService)
    {
        _storageService = storageService;
    }

    [HttpPost("image")]
    public async Task<ActionResult<ApiEnvelope<string>>> UploadImage(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            throw new AppException("FILE_REQUIRED", 400, "Validation failed", "File is required.");
        }

        // 1. Validate File Size
        if (file.Length > MaxFileSizeInBytes)
        {
            throw new AppException("FILE_TOO_LARGE", 400, "Validation failed", "File size exceeds the 2MB limit.");
        }

        // 2. Validate MIME type
        var contentType = file.ContentType.ToLowerInvariant();
        if (!AllowedMimeTypes.Contains(contentType))
        {
            throw new AppException("INVALID_FILE_TYPE", 400, "Validation failed", "Only JPEG, PNG, GIF, and WEBP images are allowed.");
        }

        // 3. Validate Header Signature (Magic Numbers)
        using var stream = file.OpenReadStream();
        if (!ValidateSignature(stream, contentType))
        {
            throw new AppException("INVALID_FILE_SIGNATURE", 400, "Validation failed", "The uploaded file content does not match its image format.");
        }

        try
        {
            var imageUrl = await _storageService.UploadImageAsync(stream, file.FileName, contentType, cancellationToken);
            return Ok(new ApiEnvelope<string>(imageUrl, RequestId()));
        }
        catch (Exception ex)
        {
            throw new AppException("UPLOAD_FAILED", 500, "Upload failed", $"Failed to upload file: {ex.Message}");
        }
    }

    private bool ValidateSignature(Stream stream, string contentType)
    {
        if (!ImageSignatures.TryGetValue(contentType, out var signature))
        {
            return false;
        }

        var header = new byte[signature.Length];
        var bytesRead = stream.Read(header, 0, header.Length);
        stream.Position = 0; // Reset position for further processing

        if (bytesRead < signature.Length)
        {
            return false;
        }

        if (contentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase))
        {
            var fullHeader = new byte[12];
            var readLen = stream.Read(fullHeader, 0, fullHeader.Length);
            stream.Position = 0; // Reset position again

            if (readLen < 12)
            {
                return false;
            }

            // WEBP matches: starts with "RIFF" (52 49 46 46) and at index 8 has "WEBP" (57 45 42 50)
            return fullHeader.Take(4).SequenceEqual(signature) &&
                   fullHeader.Skip(8).Take(4).SequenceEqual(new byte[] { 0x57, 0x45, 0x42, 0x50 });
        }

        return header.SequenceEqual(signature);
    }

    private string RequestId() => HttpContext.TraceIdentifier;
}
