using System;
using System.Collections.Generic;
using doantotnghiep_api.Data;
using doantotnghiep_api.Dto_s;
using doantotnghiep_api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using doantotnghiep_api.Services;
using Microsoft.AspNetCore.Authorization;

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

        // ================= REGISTER =================
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            if (await _context.Users.AnyAsync(x => x.Email == request.Email))
                return BadRequest("Email đã tồn tại");

            var user = new User
            {
                Email = request.Email,
                // 💡 Sử dụng BCrypt thay vì hàm Hash cũ
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber,
                Role = "User", // Mặc định là khách hàng
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
            try
            {
                var user = await _context.Users
                    .Include(u => u.Theater)
                    .FirstOrDefaultAsync(x => x.Email == request.Email);

                if (user == null)
                {
                    return BadRequest("Sai email hoặc mật khẩu");
                }

                bool isPasswordValid = false;
                try
                {
                    // 💡 Kiểm tra bằng BCrypt.Verify
                    isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
                }
                catch (Exception)
                {
                    // 🚨 LỖI: Hash cũ (MD5/SHA) không tương thích với BCrypt
                    // Ta coi như sai mật khẩu và đề nghị reset
                    return BadRequest("Tài khoản cũ cần đặt lại mật khẩu để nâng cấp bảo mật (BCrypt).");
                }

                if (!isPasswordValid)
                {
                    return BadRequest("Sai email hoặc mật khẩu");
                }

                var token = GenerateJwt(user);

                return Ok(new
                {
                    token,
                    user.UserId,
                    user.Email,
                    user.FullName,
                    user.Role,
                    user.TheaterId,
                    TheaterName = user.Theater?.Name
                });
            }
            catch (Exception ex)
            {
                // In lỗi chi tiết ra console server để debug
                Console.WriteLine($"[AUTH ERROR]: {ex}");
                return StatusCode(500, new { message = "Lỗi hệ thống khi đăng nhập", detail = ex.Message });
            }
        }

        // ================= FORGOT PASSWORD =================
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
        {
            if (string.IsNullOrEmpty(request.Email)) return BadRequest("Email không được để trống");

            var trimmedEmail = request.Email.Trim().ToLower();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == trimmedEmail);
            if (user == null) return NotFound("Email không tồn tại trong hệ thống");

            string tempPassword = Guid.NewGuid().ToString().Substring(0, 8);

            // 💡 Cập nhật mật khẩu tạm bằng BCrypt
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword);
            await _context.SaveChangesAsync();

            string subject = "Reset Mật Khẩu - Cinema Ticket";
            string body = $@"
                <div style='font-family:Arial, sans-serif; padding:20px;'>
                    <h2>Yêu cầu khôi phục mật khẩu</h2>
                    <p>Chào {user.FullName ?? "bạn"},</p>
                    <p>Mật khẩu của bạn đã được đặt lại thành công.</p>
                    <p style='font-size:18px;'>Mật khẩu tạm thời của bạn là: <strong>{tempPassword}</strong></p>
                    <p>Vui lòng đăng nhập và đổi lại mật khẩu ngay sau đó để đảm bảo an toàn.</p>
                </div>";

            await _emailService.SendEmailAsync(user.Email, subject, body);

            return Ok("Mật khẩu mới đã được gửi vào email của bạn");
        }

        // ================= GET PROFILE =================
        [HttpGet("profile/{userId}")]
        [Authorize]
        public async Task<IActionResult> GetProfile(int userId)
        {
            var user = await _context.Users
                .Include(u => u.Theater)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null) return NotFound("Người dùng không tồn tại");

            return Ok(new
            {
                user.UserId,
                user.Email,
                user.FullName,
                user.PhoneNumber,
                user.Role,
                user.TheaterId,
                TheaterName = user.Theater?.Name,
                Dob = user.Dob?.ToString("yyyy-MM-dd"), // Format chuẩn ISO cho component Date
                user.IdCard,
                user.Gender,
                user.City,
                user.District,
                user.Address,
                user.CreatedAt
            });
        }

        // ================= UPDATE PROFILE =================
        [HttpPut("update-profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null) return NotFound("Người dùng không tồn tại");

            user.FullName = request.FullName;
            user.PhoneNumber = request.PhoneNumber;
            
            if (!string.IsNullOrEmpty(request.Dob)) {
                if (DateTime.TryParse(request.Dob, out DateTime dobDate)) {
                    user.Dob = dobDate.ToUniversalTime(); // Hoặc cấu hình timezone cho phù hợp, tránh lỗi Postgres
                }
            } else {
                user.Dob = null;
            }

            user.IdCard = request.IdCard;
            user.Gender = request.Gender;
            user.City = request.City;
            user.District = request.District;
            user.Address = request.Address;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật hồ sơ thành công" });
        }

        // ================= CHANGE PASSWORD =================
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
        {
            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null) return NotFound("Người dùng không tồn tại");

            // 💡 Kiểm tra bằng BCrypt.Verify trước khi cho phép đổi
            if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash))
            {
                return BadRequest("Mật khẩu cũ không chính xác");
            }

            // 💡 Cập nhật mật khẩu mới bằng BCrypt
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await _context.SaveChangesAsync();

            return Ok("Đổi mật khẩu thành công");
        }

        // ================= HELPER: GENERATE JWT =================
        private string GenerateJwt(User user)
        {
            var keyStr = _config["Jwt:Key"] ?? "SecretKeyToDefendYourAPI2026";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName ?? ""),
                new Claim(ClaimTypes.Role, user.Role)
            };

            if (user.TheaterId.HasValue)
            {
                claims.Add(new Claim("TheaterId", user.TheaterId.Value.ToString()));
            }

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // Các hàm GetProfile, UpdateProfile giữ nguyên...
    }
}