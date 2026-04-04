namespace doantotnghiep_api.Dto_s
{
    public class CreateUserDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty; // Mật khẩu gốc từ form
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public string Role { get; set; } = "User"; // Mặc định là User, Admin có thể chọn Role khác
    }
}
