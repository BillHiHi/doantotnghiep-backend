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

namespace doantotnghiep_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public AuthController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
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
                Encoding.UTF8.GetBytes(_config["Jwt:Key"])
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
    }
}
