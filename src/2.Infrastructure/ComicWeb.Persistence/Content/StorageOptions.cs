namespace ComicWeb.Persistence.Content;

public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Supported values: "Local" or "Cloudinary"
    /// </summary>
    public string Provider { get; set; } = "Local";

    /// <summary>
    /// Local uploads folder (e.g. "wwwroot/uploads"). Only applicable when Provider is "Local".
    /// </summary>
    public string UploadsFolder { get; set; } = "wwwroot/uploads";

    /// <summary>
    /// Local base URL (e.g. "http://localhost:8080"). Only applicable when Provider is "Local".
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:8080";

    /// <summary>
    /// Cloudinary integration settings. Only applicable when Provider is "Cloudinary".
    /// </summary>
    public CloudinarySettings? Cloudinary { get; set; }
}

public class CloudinarySettings
{
    public string CloudName { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
}
