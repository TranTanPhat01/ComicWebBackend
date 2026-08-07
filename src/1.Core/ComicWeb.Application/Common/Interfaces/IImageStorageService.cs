using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ComicWeb.Application.Common.Interfaces;

public interface IImageStorageService
{
    /// <summary>
    /// Uploads an image stream and returns its public access URL.
    /// </summary>
    Task<string> UploadImageAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an image from storage by its public URL.
    /// </summary>
    Task DeleteImageAsync(string imageUrl, CancellationToken cancellationToken = default);
}
