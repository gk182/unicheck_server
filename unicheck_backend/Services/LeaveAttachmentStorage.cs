using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace unicheck_backend.Services;

public interface ILeaveAttachmentStorage
{
    Task<LeaveAttachmentStoredFile> SaveLeaveAttachmentAsync(
        IFormFile file,
        string studentId,
        CancellationToken cancellationToken = default);
}

public class LocalLeaveAttachmentStorage : ILeaveAttachmentStorage
{
    private const string BaseFolder = "uploads/leave-attachments";
    private readonly IWebHostEnvironment _environment;

    public LocalLeaveAttachmentStorage(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<LeaveAttachmentStoredFile> SaveLeaveAttachmentAsync(
        IFormFile file,
        string studentId,
        CancellationToken cancellationToken = default)
    {
        var root = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(_environment.ContentRootPath, "wwwroot");
        }

        var safeStudentId = BuildSafeStudentId(studentId);
        var fileExt = Path.GetExtension(file.FileName).ToLowerInvariant();
        var uniqueName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{fileExt}";

        var relativeFolder = Path.Combine(BaseFolder, safeStudentId);
        var absoluteFolder = Path.Combine(root, relativeFolder);
        Directory.CreateDirectory(absoluteFolder);

        var absoluteFilePath = Path.Combine(absoluteFolder, uniqueName);
        await using (var output = new FileStream(absoluteFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(output, cancellationToken);
        }

        var relativeUrl = "/" + Path.Combine(relativeFolder, uniqueName).Replace("\\", "/");
        return new LeaveAttachmentStoredFile(
            relativeUrl,
            file.FileName,
            file.ContentType,
            file.Length);
    }

    private static string BuildSafeStudentId(string studentId)
    {
        var safe = string.Join(
            "_",
            studentId.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

        return string.IsNullOrWhiteSpace(safe) ? "unknown-student" : safe;
    }
}

public class CloudinaryLeaveAttachmentStorage : ILeaveAttachmentStorage
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public CloudinaryLeaveAttachmentStorage(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    public static bool HasRequiredConfig(IConfiguration configuration)
    {
        var cloudName = configuration["Cloudinary:CloudName"];
        var apiKey = configuration["Cloudinary:ApiKey"];
        var apiSecret = configuration["Cloudinary:ApiSecret"];

        return !string.IsNullOrWhiteSpace(cloudName)
            && !string.IsNullOrWhiteSpace(apiKey)
            && !string.IsNullOrWhiteSpace(apiSecret);
    }

    public async Task<LeaveAttachmentStoredFile> SaveLeaveAttachmentAsync(
        IFormFile file,
        string studentId,
        CancellationToken cancellationToken = default)
    {
        var cloudName = _configuration["Cloudinary:CloudName"]!;
        var apiKey = _configuration["Cloudinary:ApiKey"]!;
        var apiSecret = _configuration["Cloudinary:ApiSecret"]!;
        var folder = _configuration["Cloudinary:LeaveAttachmentsFolder"] ?? "unicheck/leave-attachments";
        var resourceType = _configuration["Cloudinary:ResourceType"] ?? "auto";
        var uploadPreset = _configuration["Cloudinary:UploadPreset"];

        var safeStudentId = BuildSafeStudentId(studentId);
        var fileExt = Path.GetExtension(file.FileName).ToLowerInvariant();
        var publicId = $"{safeStudentId}/{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{fileExt}";

        var endpoint = $"https://api.cloudinary.com/v1_1/{cloudName}/{resourceType}/upload";

        using var stream = file.OpenReadStream();
        using var streamContent = new StreamContent(stream);
        if (!string.IsNullOrWhiteSpace(file.ContentType))
        {
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        }

        using var form = new MultipartFormDataContent
        {
            { streamContent, "file", file.FileName }
        };

        // If preset is configured, use Cloudinary unsigned-preset upload flow.
        if (!string.IsNullOrWhiteSpace(uploadPreset))
        {
            form.Add(new StringContent(uploadPreset), "upload_preset");
        }
        else
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var signature = BuildSignature(folder, publicId, timestamp, null, apiSecret);

            form.Add(new StringContent(apiKey), "api_key");
            form.Add(new StringContent(timestamp.ToString()), "timestamp");
            form.Add(new StringContent(signature), "signature");
            form.Add(new StringContent(folder), "folder");
            form.Add(new StringContent(publicId), "public_id");
        }

        var client = _httpClientFactory.CreateClient("Cloudinary");
        using var response = await client.PostAsync(endpoint, form, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Cloudinary upload failed: {(int)response.StatusCode} {response.ReasonPhrase}. {body}");
        }

        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("secure_url", out var secureUrlElement))
        {
            throw new InvalidOperationException("Cloudinary upload failed: secure_url not found.");
        }

        var secureUrl = secureUrlElement.GetString();
        if (string.IsNullOrWhiteSpace(secureUrl))
        {
            throw new InvalidOperationException("Cloudinary upload failed: secure_url is empty.");
        }

        return new LeaveAttachmentStoredFile(
            secureUrl,
            file.FileName,
            file.ContentType,
            file.Length);
    }

    private static string BuildSafeStudentId(string studentId)
    {
        var normalized = new string(studentId
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_')
            .ToArray());

        return string.IsNullOrWhiteSpace(normalized) ? "unknown-student" : normalized;
    }

    private static string BuildSignature(
        string folder,
        string publicId,
        long timestamp,
        string? uploadPreset,
        string apiSecret)
    {
        var signParams = new SortedDictionary<string, string>
        {
            ["folder"] = folder,
            ["public_id"] = publicId,
            ["timestamp"] = timestamp.ToString()
        };
        if (!string.IsNullOrWhiteSpace(uploadPreset))
        {
            signParams["upload_preset"] = uploadPreset;
        }

        var toSign = string.Join("&", signParams.Select(kvp => $"{kvp.Key}={kvp.Value}")) + apiSecret;

        using var sha1 = SHA1.Create();
        var bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(toSign));
        var hex = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        return hex;
    }
}

public record LeaveAttachmentStoredFile(
    string RelativeUrl,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes);
