using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using ComicWeb.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace ComicWeb.Persistence.Content;

public class SupabaseImageStorageService : IImageStorageService
{
    private readonly StorageOptions _options;
    private static readonly HttpClient HttpClient = new();

    public SupabaseImageStorageService(IOptions<StorageOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> UploadImageAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        if (_options.Supabase == null ||
            string.IsNullOrWhiteSpace(_options.Supabase.Url) ||
            string.IsNullOrWhiteSpace(_options.Supabase.ServiceRoleKey))
        {
            throw new InvalidOperationException("Supabase storage settings are not configured properly.");
        }

        var url = _options.Supabase.Url.TrimEnd('/');
        var serviceRoleKey = _options.Supabase.ServiceRoleKey;
        var bucket = string.IsNullOrWhiteSpace(_options.Supabase.BucketName) ? "story-covers" : _options.Supabase.BucketName;
        
        var uniqueFileName = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";
        
        // Target endpoint: POST {supabaseUrl}/storage/v1/object/{bucket}/{filename}
        var requestUrl = $"{url}/storage/v1/object/{bucket}/{uniqueFileName}";

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        request.Headers.Add("Authorization", $"Bearer {serviceRoleKey}");
        request.Headers.Add("apikey", serviceRoleKey);
        
        // Content body: stream content
        using var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        request.Content = streamContent;

        var response = await HttpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Supabase storage upload failed with status {response.StatusCode}: {errorContent}");
        }

        // Public URL format: {supabaseUrl}/storage/v1/object/public/{bucket}/{filename}
        return $"{url}/storage/v1/object/public/{bucket}/{uniqueFileName}";
    }

    public async Task DeleteImageAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return;
        }

        if (_options.Supabase == null ||
            string.IsNullOrWhiteSpace(_options.Supabase.Url) ||
            string.IsNullOrWhiteSpace(_options.Supabase.ServiceRoleKey))
        {
            return;
        }

        var url = _options.Supabase.Url.TrimEnd('/');
        var serviceRoleKey = _options.Supabase.ServiceRoleKey;
        var bucket = string.IsNullOrWhiteSpace(_options.Supabase.BucketName) ? "story-covers" : _options.Supabase.BucketName;

        try
        {
            // Expected format contains /storage/v1/object/public/{bucket}/{filename}
            var publicPrefix = $"{url}/storage/v1/object/public/{bucket}/";
            if (!imageUrl.StartsWith(publicPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return; // Not a Supabase image from this bucket, skip deletion
            }

            var fileName = imageUrl.Substring(publicPrefix.Length);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }

            // Target endpoint: DELETE {supabaseUrl}/storage/v1/object/{bucket}/{filename}
            var requestUrl = $"{url}/storage/v1/object/{bucket}/{fileName}";

            using var request = new HttpRequestMessage(HttpMethod.Delete, requestUrl);
            request.Headers.Add("Authorization", $"Bearer {serviceRoleKey}");
            request.Headers.Add("apikey", serviceRoleKey);

            var response = await HttpClient.SendAsync(request, cancellationToken);
            // Silent failure or logs, as per requirements
        }
        catch
        {
            // Fail silently on deletion error as per master plan requirements
        }
    }
}
