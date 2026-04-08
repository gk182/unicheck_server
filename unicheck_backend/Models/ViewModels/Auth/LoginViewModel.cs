using System.ComponentModel.DataAnnotations;

namespace unicheck_backend.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập Email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }

    public class AuthResponseViewModel
    {
        public string Token { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
        public int UserId { get; set; }
    }
}