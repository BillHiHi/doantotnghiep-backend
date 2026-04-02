using doantotnghiep_api.Data;
using doantotnghiep_api.Dto_s;
using doantotnghiep_api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace doantotnghiep_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,SUPER_ADMIN")] // 💡 BẢO MẬT: Chỉ hệ thống Admin/SuperAdmin mới được gọi các API này
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UserController(AppDbContext context)
        {
            _context = context;
        }

        // =================================================
        // LẤY DANH SÁCH USER (Có phân trang)
        // =================================================
        [HttpGet]
        public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var query = _context.Users.AsNoTracking();

            var totalRecords = await query.CountAsync();

            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                // 💡 TỐI ƯU: Select để loại bỏ cột PasswordHash, không gửi pass băm về frontend
                .Select(u => new
                {
                    u.UserId,
                    u.Email,
                    u.FullName,
                    u.PhoneNumber,
                    u.Role,
                    u.CreatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                TotalRecords = totalRecords,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize),
                Data = users
            });
        }

        // =================================================
        // LẤY THÔNG TIN 1 USER
        // =================================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _context.Users
                .AsNoTracking()
                .Where(u => u.UserId == id)
                .Select(u => new
                {
                    u.UserId,
                    u.Email,
                    u.FullName,
                    u.PhoneNumber,
                    u.Role,
                    u.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return NotFound(new { message = "Không tìm thấy người dùng này." });

            return Ok(user);
        }

        // =================================================
        // TẠO USER MỚI
        // =================================================
        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
        {
            // Kiểm tra email trùng lặp
            var emailExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == dto.Email.ToLower());
            if (emailExists)
                return BadRequest(new { message = "Email này đã được sử dụng." });

            var newUser = new User
            {
                Email = dto.Email,
                FullName = dto.FullName,
                PhoneNumber = dto.PhoneNumber,
                Role = dto.Role,
                CreatedAt = DateTime.UtcNow,
                // 💡 BẢO MẬT: Băm mật khẩu trước khi lưu vào DB
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // Trả về dữ liệu không có password
            return Ok(new { message = "Tạo người dùng thành công.", userId = newUser.UserId });
        }

        // =================================================
        // CẬP NHẬT USER
        // =================================================
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto dto)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
                return NotFound(new { message = "Không tìm thấy người dùng này." });

            // Kiểm tra xem email muốn đổi có bị trùng với người khác không
            if (user.Email.ToLower() != dto.Email.ToLower())
            {
                var emailExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == dto.Email.ToLower());
                if (emailExists)
                    return BadRequest(new { message = "Email này đã được sử dụng bởi tài khoản khác." });
            }

            user.Email = dto.Email;
            user.FullName = dto.FullName;
            user.PhoneNumber = dto.PhoneNumber;
            user.Role = dto.Role;

            // 💡 Cập nhật mật khẩu nếu Admin có nhập mật khẩu mới
            if (!string.IsNullOrEmpty(dto.NewPassword))
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Cập nhật thông tin thành công." });
        }

        // =================================================
        // XÓA USER
        // =================================================
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
                return NotFound(new { message = "Không tìm thấy người dùng này." });

            // 💡 Tùy chọn: Chặn Admin tự xóa chính mình (nếu bạn có lấy được ID của người đang login)
            // if (id == currentAdminId) return BadRequest("Không thể tự xóa chính mình");

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã xóa người dùng thành công." });
        }
    }
}