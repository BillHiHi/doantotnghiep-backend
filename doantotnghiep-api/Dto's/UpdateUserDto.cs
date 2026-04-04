namespace doantotnghiep_api.Dto_s
{
    public class UpdateUserDto
    {
        public string Email { get; set; } = string.Empty;
        // Cho phép đổi pass, nếu để trống (null) thì giữ nguyên pass cũ
        public string? NewPassword { get; set; }
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public string Role { get; set; } = "User";
    }
}
