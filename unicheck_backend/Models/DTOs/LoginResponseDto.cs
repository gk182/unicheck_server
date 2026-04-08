namespace unicheck_backend.Models.DTOs
{
    /// <summary>
    /// Trả về client (Flutter) sau khi đăng nhập thành công qua REST API.
    /// </summary>
    public class LoginResponseDto
    {
        public string Token     { get; set; } = string.Empty;  // JWT Bearer Token
        public string Role      { get; set; } = string.Empty;
        public string Username  { get; set; } = string.Empty;
        public string FullName  { get; set; } = string.Empty;
        public string UserId    { get; set; } = string.Empty;  // StudentId hoặc LecturerId
        public DateTime ExpiresAt { get; set; }
    }
}
