using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace unicheck_backend.Services;

/// <summary>
/// Giao tiếp với Python FastAPI AI Service (port 8000) để:
///   - Trích xuất face embedding từ ảnh   → POST /register-face
///   - Xác thực khuôn mặt vs embedding    → POST /verify-face
/// API dùng multipart/form-data, không phải application/json.
/// </summary>
public class FaceService
{
    private readonly IConfiguration _config;
    private readonly HttpClient _http;
    private readonly double _threshold;

    public FaceService(IConfiguration config, IHttpClientFactory httpFactory)
    {
        _config    = config;
        _http      = httpFactory.CreateClient("FaceAI");
        _threshold = config.GetValue("FaceAI:Threshold", 0.8);
    }

    // ── PUBLIC API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Gửi ảnh base64 lên Python service /register-face để lấy face embedding vector.
    /// </summary>
    /// <returns>JSON string float[] sẵn sàng lưu vào Student.FaceEmbedding, hoặc null nếu lỗi.</returns>
    public async Task<FaceExtractResult> ExtractEmbedding(string imageBase64)
    {
        try
        {
            using var form    = BuildMultipartForm(imageBase64);
            using var resp    = await _http.PostAsync("/register-face", form);
            var       body    = await resp.Content.ReadAsStringAsync();
            var       result  = JsonSerializer.Deserialize<AiRegisterResponse>(body, JsonOptions)
                                ?? throw new Exception("Empty response");

            if (!result.Success)
                return FaceExtractResult.Fail(result.Message ?? "AI service trả về lỗi.");

            // Serialize vector về JSON string để lưu DB
            var embeddingJson = JsonSerializer.Serialize(result.Vector);
            return FaceExtractResult.Ok(embeddingJson);
        }
        catch (HttpRequestException ex)
        {
            return FaceExtractResult.Fail($"Không kết nối được AI service: {ex.Message}");
        }
        catch (Exception ex)
        {
            return FaceExtractResult.Fail($"Lỗi trích xuất embedding: {ex.Message}");
        }
    }

    /// <summary>
    /// Gửi ảnh + embedding đã lưu lên Python service /verify-face để xác thực.
    /// </summary>
    public async Task<FaceVerifyResult> VerifyFace(string imageBase64, string storedEmbeddingJson)
    {
        if (string.IsNullOrEmpty(storedEmbeddingJson))
            return FaceVerifyResult.Fail("Sinh viên chưa đăng ký khuôn mặt.");

        try
        {
            using var form = BuildMultipartFormWithVector(imageBase64, storedEmbeddingJson);
            using var resp = await _http.PostAsync("/verify-face", form);
            var       body = await resp.Content.ReadAsStringAsync();
            var result     = JsonSerializer.Deserialize<AiVerifyResponse>(body, JsonOptions)
                             ?? throw new Exception("Empty response");

            return new FaceVerifyResult
            {
                IsMatch    = result.IsMatch,
                Confidence = result.Confidence,
                Message    = result.Message ?? string.Empty,
            };
        }
        catch (HttpRequestException ex)
        {
            return FaceVerifyResult.Fail($"Không kết nối được AI service: {ex.Message}");
        }
        catch (Exception ex)
        {
            return FaceVerifyResult.Fail($"Lỗi xác thực khuôn mặt: {ex.Message}");
        }
    }

    // ── STATIC HELPERS ────────────────────────────────────────────────────────

    /// <summary>Tính cosine similarity giữa 2 embedding vectors.</summary>
    public static double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot   += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    /// <summary>Parse JSON embedding string từ DB về float[].</summary>
    public static float[]? ParseEmbedding(string? json)
        => string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<float[]>(json);

    // ── PRIVATE HELPERS ───────────────────────────────────────────────────────

    /// <summary>
    /// Build multipart/form-data form với 1 file image (for /register-face).
    /// Python nhận: file: UploadFile
    /// </summary>
    private static MultipartFormDataContent BuildMultipartForm(string imageBase64)
    {
        var imageBytes = Convert.FromBase64String(imageBase64);
        var form       = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(imageBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "file", "face.jpg");
        return form;
    }

    /// <summary>
    /// Build multipart/form-data với file + known_vector string (for /verify-face).
    /// Python nhận: file: UploadFile, known_vector: str = Form(...)
    /// </summary>
    private static MultipartFormDataContent BuildMultipartFormWithVector(string imageBase64, string knownVectorJson)
    {
        var imageBytes  = Convert.FromBase64String(imageBase64);
        var form        = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(imageBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "file", "face.jpg");
        form.Add(new StringContent(knownVectorJson), "known_vector");
        return form;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ── AI RESPONSE MODELS ────────────────────────────────────────────────────

    private class AiRegisterResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public float[]? Vector { get; set; }
    }

    private class AiVerifyResponse
    {
        public bool Success { get; set; }
        [JsonPropertyName("is_match")]
        public bool IsMatch { get; set; }
        public double Confidence { get; set; }
        public string? Message { get; set; }
    }
}

// ── RESULT DTOs ───────────────────────────────────────────────────────────────

public class FaceExtractResult
{
    public bool Success { get; init; }
    public string? EmbeddingJson { get; init; }
    public string? Error { get; init; }

    public static FaceExtractResult Ok(string json) => new() { Success = true, EmbeddingJson = json };
    public static FaceExtractResult Fail(string err) => new() { Success = false, Error = err };
}

public class FaceVerifyResult
{
    public bool IsMatch { get; init; }
    public double Confidence { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool ServiceError { get; init; }

    public static FaceVerifyResult Fail(string err) => new() { IsMatch = false, Confidence = 0, Message = err, ServiceError = true };
}
