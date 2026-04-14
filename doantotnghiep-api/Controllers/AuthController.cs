using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using doantotnghiep_api.Data;
using doantotnghiep_api.Dto_s;
using doantotnghiep_api.Models;
using doantotnghiep_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

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

        // ====================================================================
        // 1️⃣ AUTHENTICATION ENDPOINTS
        // ====================================================================

        /// <summary>
        /// Đăng ký tài khoản mới
        /// </summary>
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (await _context.Users.AnyAsync(x => x.Email == request.Email))
                return BadRequest(new { message = "Email đã tồn tại" });

            var user = new User
            {
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
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
                user.Role,
                message = "Đăng ký thành công"
            });
        }

        /// <summary>
        /// Đăng nhập bằng email & mật khẩu
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.Theater)
                    .FirstOrDefaultAsync(x => x.Email == request.Email);

                if (user == null)
                    return BadRequest(new { message = "Sai email hoặc mật khẩu" });

                bool isPasswordValid = false;
                try
                {
                    isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
                }
                catch (Exception)
                {
                    return BadRequest(new { message = "Tài khoản cũ cần đặt lại mật khẩu để nâng cấp bảo mật" });
                }

                if (!isPasswordValid)
                    return BadRequest(new { message = "Sai email hoặc mật khẩu" });

                var token = GenerateJwt(user);

                return Ok(new
                {
                    token,
                    user.UserId,
                    user.Email,
                    user.FullName,
                    user.Role,
                    user.TheaterId,
                    theaterName = user.Theater?.Name,
                    message = "Đăng nhập thành công"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOGIN ERROR]: {ex}");
                return StatusCode(500, new { message = "Lỗi hệ thống khi đăng nhập", detail = ex.Message });
            }
        }

        // ====================================================================
        // 2️⃣ GOOGLE OAUTH ENDPOINTS
        // ====================================================================

        /// <summary>
        /// Lấy URL đăng nhập Google
        /// </summary>
        [HttpGet("google-login-url")]
        public IActionResult GetGoogleLoginUrl()
        {
            var clientId = _config["Authentication:Google:ClientId"];
            // Phải là FRONTEND URL - nơi Google redirect về sau khi user đăng nhập
            var redirectUri = _config["Authentication:Google:RedirectUri"]
                ?? "http://localhost:5173/auth/google-callback";

            var googleLoginUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                $"client_id={clientId}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&response_type=code" +
                $"&scope={Uri.EscapeDataString("openid profile email")}";

            return Ok(new { loginUrl = googleLoginUrl });
        }

        /// <summary>
        /// Xử lý callback từ Google OAuth
        /// </summary>
        [HttpGet("google/callback")]
        [HttpPost("google/callback")]
        [AllowAnonymous]
        public async Task<IActionResult> GoogleCallback([FromQuery] string code)
        {
            try
            {
                if (string.IsNullOrEmpty(code))
                    return BadRequest(new { message = "❌ Authorization code không tìm thấy" });

                var clientId = _config["Authentication:Google:ClientId"];
                var clientSecret = _config["Authentication:Google:ClientSecret"];
                var redirectUri = "https://localhost:7221/api/auth/google/callback";

                // 1️⃣ Trao đổi authorization code lấy access token
                var accessToken = await ExchangeCodeForAccessToken(clientId, clientSecret, redirectUri, code);
                if (string.IsNullOrEmpty(accessToken))
                    return BadRequest(new { message = "❌ Không thể lấy access token từ Google" });

                // 2️⃣ Lấy thông tin user từ Google
                var userData = await GetGoogleUserInfo(accessToken);
                if (userData == null)
                    return BadRequest(new { message = "❌ Không thể lấy thông tin user từ Google" });

                // 3️⃣ Kiểm tra hoặc tạo user mới
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userData.Email);
                if (user == null)
                {
                    user = new User
                    {
                        Email = userData.Email,
                        FullName = userData.Name,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                        Role = "User",
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }

                // 4️⃣ Tạo JWT token
                var token = GenerateJwt(user);

                return Ok(new
                {
                    token,
                    user.UserId,
                    user.Email,
                    user.FullName,
                    user.Role,
                    user.TheaterId,
                    message = "✅ Đăng nhập Google thành công"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GOOGLE AUTH ERROR]: {ex}");
                return StatusCode(500, new { message = "❌ Lỗi hệ thống", detail = ex.Message });
            }
        }

        // ====================================================================
        // 3️⃣ PROFILE MANAGEMENT ENDPOINTS
        // ====================================================================

        /// <summary>
        /// Lấy thông tin profile
        /// </summary>
        [HttpGet("profile/{userId}")]
        [Authorize]
        public async Task<IActionResult> GetProfile(int userId)
        {
            var user = await _context.Users
                .Include(u => u.Theater)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return NotFound(new { message = "Người dùng không tồn tại" });

            return Ok(new
            {
                user.UserId,
                user.Email,
                user.FullName,
                user.PhoneNumber,
                user.Role,
                user.TheaterId,
                theaterName = user.Theater?.Name,
                dob = user.Dob?.ToString("yyyy-MM-dd"),
                user.IdCard,
                user.Gender,
                user.City,
                user.District,
                user.Address,
                user.Points,
                user.CreatedAt
            });
        }

        /// <summary>
        /// Cập nhật thông tin profile
        /// </summary>
        [HttpPut("update-profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null)
                return NotFound(new { message = "Người dùng không tồn tại" });

            user.FullName = request.FullName;
            user.PhoneNumber = request.PhoneNumber;

            if (!string.IsNullOrEmpty(request.Dob))
            {
                if (DateTime.TryParse(request.Dob, out DateTime dobDate))
                    user.Dob = dobDate.ToUniversalTime();
            }
            else
            {
                user.Dob = null;
            }

            user.IdCard = request.IdCard;
            user.Gender = request.Gender;
            user.City = request.City;
            user.District = request.District;
            user.Address = request.Address;

            await _context.SaveChangesAsync();
            return Ok(new { message = "✅ Cập nhật hồ sơ thành công" });
        }

        // ====================================================================
        // 4️⃣ PASSWORD MANAGEMENT ENDPOINTS
        // ====================================================================

        /// <summary>
        /// Đổi mật khẩu
        /// </summary>
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null)
                return NotFound(new { message = "Người dùng không tồn tại" });

            if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash))
                return BadRequest(new { message = "Mật khẩu cũ không chính xác" });

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await _context.SaveChangesAsync();

            return Ok(new { message = "✅ Đổi mật khẩu thành công" });
        }

        /// <summary>
        /// Quên mật khẩu - gửi mật khẩu tạm qua email
        /// </summary>
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (string.IsNullOrEmpty(request.Email))
                return BadRequest(new { message = "Email không được để trống" });

            var trimmedEmail = request.Email.Trim().ToLower();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == trimmedEmail);
            if (user == null)
                return NotFound(new { message = "Email không tồn tại trong hệ thống" });

            string tempPassword = Guid.NewGuid().ToString().Substring(0, 8);
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword);
            await _context.SaveChangesAsync();

            string subject = "🔐 Reset Mật Khẩu - Cinema Ticket";
            string body = $@"
                <div style='font-family:Arial, sans-serif; padding:20px; background-color:#f5f5f5;'>
                    <h2>Yêu cầu khôi phục mật khẩu</h2>
                    <p>Chào <strong>{user.FullName ?? "bạn"}</strong>,</p>
                    <p>Mật khẩu của bạn đã được đặt lại thành công.</p>
                    <div style='background-color:#fff; padding:15px; border-radius:5px; margin:20px 0;'>
                        <p style='font-size:14px;'>Mật khẩu tạm thời của bạn là:</p>
                        <p style='font-size:18px; font-weight:bold; color:#007bff;'>{tempPassword}</p>
                    </div>
                    <p>✅ Vui lòng đăng nhập và đổi lại mật khẩu ngay sau đó để đảm bảo an toàn.</p>
                    <hr style='margin-top:30px; border:none; border-top:1px solid #ccc;'>
                    <p style='font-size:12px; color:#999;'>Nếu bạn không yêu cầu reset mật khẩu, vui lòng bỏ qua email này.</p>
                </div>";

            await _emailService.SendEmailAsync(user.Email, subject, body);

            return Ok(new { message = "✅ Mật khẩu mới đã được gửi vào email của bạn" });
        }

        // ====================================================================
        // 5️⃣ HELPER METHODS
        // ====================================================================

        /// <summary>
        /// Tạo JWT token
        /// </summary>
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
                claims.Add(new Claim("TheaterId", user.TheaterId.Value.ToString()));

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Trao đổi authorization code lấy access token từ Google
        /// </summary>
        private async Task<string> ExchangeCodeForAccessToken(string clientId, string clientSecret, string redirectUri, string code)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        { "code", code },
                        { "client_id", clientId },
                        { "client_secret", clientSecret },
                        { "redirect_uri", redirectUri },
                        { "grant_type", "authorization_code" }
                    });

                    var response = await client.PostAsync("https://oauth2.googleapis.com/token", content);
                    if (!response.IsSuccessStatusCode)
                        return null;

                    var jsonString = await response.Content.ReadAsStringAsync();
                    using (JsonDocument doc = JsonDocument.Parse(jsonString))
                    {
                        var root = doc.RootElement;
                        return root.GetProperty("access_token").GetString();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Exchange Code Error]: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Lấy thông tin user từ Google API
        /// </summary>
        private async Task<GoogleUserInfo> GetGoogleUserInfo(string accessToken)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                    var response = await client.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo");
                    if (!response.IsSuccessStatusCode)
                        return null;

                    var jsonString = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    return JsonSerializer.Deserialize<GoogleUserInfo>(jsonString, options);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Get Google User Info Error]: {ex.Message}");
                return null;
            }
        }

        // ====================================================================
        // 6️⃣ DTO & MODEL CLASSES
        // ====================================================================

        /// <summary>
        /// Response từ Google userinfo endpoint
        /// </summary>
        public class GoogleUserInfo
        {
            [System.Text.Json.Serialization.JsonPropertyName("email")]
            public string Email { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("name")]
            public string Name { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("picture")]
            public string Picture { get; set; }
        }
    }
}