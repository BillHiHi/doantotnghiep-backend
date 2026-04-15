using System.ComponentModel.DataAnnotations;

namespace doantotnghiep_api.Dto_s
{
    public class RegisterRequest
    {
        [Required(ErrorMessage = "Họ và tên là bắt buộc")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ và tên phải từ 2-100 ký tự")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email là bắt buộc")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6-100 ký tự")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Xác nhận mật khẩu là bắt buộc")]
        [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp")]
        public string ConfirmPassword { get; set; }

        [Required(ErrorMessage = "Ngày sinh là bắt buộc")]
        public DateTime DateOfBirth { get; set; }

        // Không bắt buộc (không có dấu * trên form)
        public string? Gender { get; set; }

        [Required(ErrorMessage = "Số điện thoại là bắt buộc")]
        [RegularExpression(@"^(0[3|5|7|8|9])+([0-9]{8})$", ErrorMessage = "Số điện thoại không hợp lệ")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Mã xác thực là bắt buộc")]
        public string CaptchaCode { get; set; }

        [Required(ErrorMessage = "Bạn phải đồng ý với điều khoản sử dụng")]
        [Range(typeof(bool), "true", "true", ErrorMessage = "Bạn phải đồng ý với điều khoản sử dụng")]
        public bool AgreeToTerms { get; set; }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class AuthResponse
    {
        public int UserId { get; set; }
        public string Email { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Role { get; set; } = "";
    }
    public class ChangePasswordRequest
    {
        public int UserId { get; set; }
        public string OldPassword { get; set; } = "";
        public string NewPassword { get; set; } = "";
    }

    public class ForgotPasswordRequest
    {
        public string Email { get; set; } = "";
    }

    public class UpdateProfileRequest
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        
        public string Dob { get; set; } = ""; // Dùng string dạng YYYY-MM-DD từ frontend cho dễ xử lý
        public string IdCard { get; set; } = "";
        public string Gender { get; set; } = "";
        public string City { get; set; } = "";
        public string District { get; set; } = "";
        public string Address { get; set; } = "";
    }
}
