using doantotnghiep_api.Data;
using doantotnghiep_api.Dto_s;
using doantotnghiep_api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using doantotnghiep_api.Services;

namespace doantotnghiep_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly IEmailService _emailService;

        public AuthController(AppDbContext context, IConfiguration config, IEmailService emailService)
        {
            _context = context;
            _config = config;
            _emailService = emailService;
        }

        // ... (Hash and GenerateJwt methods remain the same) ...

        // ================= FORGOT PASSWORD =================
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
        {
            if (string.IsNullOrEmpty(request.Email)) return BadRequest("Email không được để trống");
            
            var trimmedEmail = request.Email.Trim().ToLower();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == trimmedEmail);
            if (user == null) return NotFound("Email không tồn tại trong hệ thống");

            // Tạo mật khẩu tạm thời ngẫu nhiên
            string tempPassword = Guid.NewGuid().ToString().Substring(0, 8);
            user.PasswordHash = Hash(tempPassword);
            await _context.SaveChangesAsync();

            // Gửi email
            string subject = "Reset Mật Khẩu - Cinema Ticket";
            string body = $@"
                <div style='font-family:Arial, sans-serif; padding:20px;'>
                    <h2>Yêu cầu khôi phục mật khẩu</h2>
                    <p>Chào {user.FullName ?? "bạn"},</p>
                    <p>Mật khẩu của bạn đã được đặt lại thành công.</p>
                    <p style='font-size:18px;'>Mật khẩu tạm thời của bạn là: <strong>{tempPassword}</strong></p>
                    <p>Vui lòng đăng nhập và đổi lại mật khẩu ngay sau đó để đảm bảo an toàn.</p>
                    <hr/>
                    <p style='color:#777; font-size:12px;'>Đây là email tự động, vui lòng không trả lời.</p>
                </div>";

            await _emailService.SendEmailAsync(user.Email, subject, body);

            return Ok("Mật khẩu mới đã được gửi vào email của bạn");
        }

        // ================= HASH =================
        private string Hash(string password)
        {
            using var sha = SHA256.Create();
            return Convert.ToBase64String(
                sha.ComputeHash(Encoding.UTF8.GetBytes(password))
            );
        }

        // ================= CREATE JWT =================
        private string GenerateJwt(User user)
        {
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Key"] ?? "default_secret_key_fixed")
            );

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName ?? ""),
                new Claim(ClaimTypes.Role, user.Role) // ⭐ QUAN TRỌNG CHO ADMIN
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // ================= REGISTER =================
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            if (await _context.Users.AnyAsync(x => x.Email == request.Email))
                return BadRequest("Email đã tồn tại");

            var user = new User
            {
                Email = request.Email,
                PasswordHash = Hash(request.Password),
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber,
                Role = "User",
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var token = GenerateJwt(user);

            return Ok(new
            {
                token,
                user.UserId,
                user.Email,
                user.FullName,
                user.Role
            });
        }

        // ================= LOGIN =================
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var hash = Hash(request.Password);

            var user = await _context.Users
                .FirstOrDefaultAsync(x =>
                    x.Email == request.Email &&
                    x.PasswordHash == hash);

            if (user == null)
                return BadRequest("Sai email hoặc mật khẩu");

            var token = GenerateJwt(user);

            return Ok(new
            {
                token,
                user.UserId,
                user.Email,
                user.FullName,
                user.Role
            });
        }
        // ================= GET PROFILE =================
        [HttpGet("profile/{userId}")]
        public async Task<IActionResult> GetProfile(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound("Người dùng không tồn tại");

            return Ok(new
            {
                user.UserId,
                user.Email,
                user.FullName,
                user.PhoneNumber,
                user.Role,
                user.CreatedAt
            });
        }

        // ================= UPDATE PROFILE =================
        [HttpPost("update-profile")]
        public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request)
        {
            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null) return NotFound("Người dùng không tồn tại");

            user.FullName = request.FullName;
            user.PhoneNumber = request.PhoneNumber;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                user.UserId,
                user.Email,
                user.FullName,
                user.PhoneNumber
            });
        }

        // ================= CHANGE PASSWORD =================
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
        {
            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null) return NotFound("Người dùng không tồn tại");

            user.PasswordHash = Hash(request.NewPassword);
            await _context.SaveChangesAsync();

            return Ok("Đổi mật khẩu thành công");
        }
    }
}
