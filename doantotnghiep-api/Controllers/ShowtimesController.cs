using System.Security.Claims;
using doantotnghiep_api.Data;
using doantotnghiep_api.Dto_s;
using doantotnghiep_api.Dtos;
using doantotnghiep_api.Models;
using doantotnghiep_api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace doantotnghiep_api.Controllers
{
    [ApiController]
    [Route("api/showtimes")]
    public class ShowtimesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ShowtimeService _showtimeService;
        private readonly ShowtimeAutomationService _automationService;

        public ShowtimesController(
            AppDbContext context,
            ShowtimeService showtimeService,
            ShowtimeAutomationService automationService)
        {
            _context = context;
            _showtimeService = showtimeService;
            _automationService = automationService;
        }

        // --- Helper Properties để kiểm tra quyền ---
        private int? UserTheaterId => int.TryParse(User.Claims.FirstOrDefault(c => c.Type == "TheaterId")?.Value, out var id) ? id : null;

        private bool IsBranchAdmin => User.IsInRole("BRANCH_ADMIN") || User.IsInRole("BRANCHADMIN");
        private bool IsSuperAdmin => User.IsInRole("SUPER_ADMIN") || User.IsInRole("ADMIN");

        // --- 1. Lấy danh sách suất chiếu (Dành cho Admin quản lý) ---
        [HttpGet]
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN")]
        public async Task<IActionResult> GetAll([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? theaterId)
        {
            var query = _context.Showtimes
                .AsNoTracking()
                .Include(s => s.Movie)
                .Include(s => s.Screen)
                    .ThenInclude(sc => sc.Theater)
                .AsQueryable();

            // Logic phân quyền: Branch Admin chỉ thấy rạp của mình
            if (IsBranchAdmin)
            {
                if (UserTheaterId.HasValue)
                    query = query.Where(s => s.Screen.TheaterId == UserTheaterId.Value);
                else
                    return Ok(new List<object>()); // Trả về rỗng nếu Admin chi nhánh không có TheaterId
            }
            else if (theaterId.HasValue) // Super Admin có thể lọc theo TheaterId truyền vào
            {
                query = query.Where(s => s.Screen.TheaterId == theaterId.Value);
            }

            // Lọc theo thời gian
            if (from.HasValue)
                query = query.Where(s => s.StartTime >= from.Value);
            if (to.HasValue)
                query = query.Where(s => s.StartTime <= to.Value);

            var data = await query
                .OrderByDescending(s => s.StartTime)
                .Select(s => new
                {
                    ShowtimeId = s.ShowtimeId,
                    MovieId = s.MovieId,
                    MovieTitle = s.Movie != null ? s.Movie.Title : "N/A",
                    ScreenId = s.ScreenId,
                    ScreenName = s.Screen != null ? s.Screen.ScreenName : "N/A",
                    TheaterName = s.Screen != null && s.Screen.Theater != null ? s.Screen.Theater.Name : "N/A",
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    BasePrice = s.BasePrice,
                    IsEarlyScreening = s.IsEarlyScreening
                })
                .ToListAsync();

            return Ok(data);
        }

        // --- 2. Tạo suất chiếu thủ công ---
        [HttpPost]
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN")]
        public async Task<IActionResult> Create([FromBody] Showtime newShowtime)
        {
            try
            {
                // Kiểm tra nếu là Branch Admin thì không được tạo cho rạp khác
                if (IsBranchAdmin && UserTheaterId.HasValue)
                {
                    var screen = await _context.Screens.FindAsync(newShowtime.ScreenId);
                    if (screen == null || screen.TheaterId != UserTheaterId.Value)
                        return Forbid("Bạn chỉ có quyền tạo suất chiếu cho rạp mình quản lý.");
                }

                var result = await _showtimeService.CreateShowtimeAsync(newShowtime);
                return CreatedAtAction(nameof(GetAll), new { id = result.ShowtimeId }, result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // --- 3. Cập nhật suất chiếu thủ công ---
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN")]
        public async Task<IActionResult> Update(int id, [FromBody] Showtime updatedShowtime)
        {
            try
            {
                // Logic bảo mật: Kiểm tra xem suất chiếu này có thuộc rạp của Admin không
                if (IsBranchAdmin && UserTheaterId.HasValue)
                {
                    var existing = await _context.Showtimes.Include(s => s.Screen).FirstOrDefaultAsync(s => s.ShowtimeId == id);
                    if (existing == null || existing.Screen.TheaterId != UserTheaterId.Value)
                        return Forbid("Bạn không có quyền chỉnh sửa suất chiếu của rạp khác.");
                }

                var result = await _showtimeService.UpdateShowtimeAsync(id, updatedShowtime);
                return Ok(new { message = "Cập nhật thành công.", data = result });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // --- 4. Xóa suất chiếu ---
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                if (IsBranchAdmin && UserTheaterId.HasValue)
                {
                    var existing = await _context.Showtimes.Include(s => s.Screen).FirstOrDefaultAsync(s => s.ShowtimeId == id);
                    if (existing == null || existing.Screen.TheaterId != UserTheaterId.Value)
                        return Forbid("Bạn không có quyền xóa suất chiếu của rạp khác.");
                }

                await _showtimeService.DeleteShowtimeAsync(id);
                return Ok(new { message = "Xóa suất chiếu thành công." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // --- 4b. Xóa hàng loạt suất chiếu ---
        [HttpPost("bulk-delete")]
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN")]
        public async Task<IActionResult> DeleteMultiple([FromBody] List<int> ids)
        {
            try
            {
                if (ids == null || !ids.Any()) return BadRequest("Danh sách ID không hợp lệ.");

                if (IsBranchAdmin && UserTheaterId.HasValue)
                {
                    // Kiểm tra xem tất cả các suất chiếu có thuộc rạp của Admin không
                    var countInOtherTheaters = await _context.Showtimes
                        .Include(s => s.Screen)
                        .Where(s => ids.Contains(s.ShowtimeId) && s.Screen.TheaterId != UserTheaterId.Value)
                        .CountAsync();

                    if (countInOtherTheaters > 0)
                        return Forbid("Bạn không có quyền xóa suất chiếu của rạp khác trong danh sách này.");
                }

                await _showtimeService.DeleteMultipleShowtimesAsync(ids);
                return Ok(new { message = $"Đã xóa thành công {ids.Count} suất chiếu." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // --- 5. Tạo tự động (Giữ nguyên logic cũ nhưng tích hợp bảo mật) ---
        [HttpPost("generate-auto")]
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN")]
        public async Task<IActionResult> GenerateAuto([FromBody] GenerateAutoDto dto)
        {
            // 1. Kiểm tra quyền hạn của Branch Admin đối với cơ sở cụ thể
            if (IsBranchAdmin && UserTheaterId.HasValue && dto.TheaterId != UserTheaterId.Value)
            {
                return Forbid("Bạn không có quyền tự động tạo lịch cho rạp khác.");
            }

            try
            {
                // 2. Gọi service để thực hiện logic phân bổ theo KPI và Giờ vàng
                var result = await _automationService.GenerateShowtimesAsync(dto.TheaterId, dto.TargetDate);

                if (!result.Success)
                {
                    return BadRequest(new
                    {
                        message = "Không thể tạo lịch tự động",
                        errors = result.Errors
                    });
                }

                // 3. Thống kê nhanh số suất chiếu trong khung giờ vàng (18h - 22h) để phản hồi
                int goldHourCount = result.CreatedShowtimes.Count(s => s.StartTime.Hour >= 18 && s.StartTime.Hour < 22);

                return Ok(new
                {
                    success = true,
                    message = $"Đã tạo thành công {result.TotalCreated} suất chiếu.",
                    theaterId = result.TheaterId,
                    targetDate = result.TargetDate.ToString("yyyy-MM-dd"),
                    summary = new
                    {
                        totalSlots = result.TotalCreated,
                        goldHourSlots = goldHourCount,
                        regularSlots = result.TotalCreated - goldHourCount
                    },
                    data = result.CreatedShowtimes,
                    debugInfo = result.DebugInfo
                });
            }
            catch (Exception ex)
            {
                // Log lỗi tại đây nếu cần thiết
                return BadRequest(new
                {
                    message = "Lỗi hệ thống trong quá trình tạo tự động",
                    error = ex.Message
                });
            }
        }

        // --- 5b. Lấy chi tiết một suất chiếu ---
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id)
        {
            var showtime = await _context.Showtimes
                .AsNoTracking()
                .Include(s => s.Movie)
                .Include(s => s.Screen)
                    .ThenInclude(sc => sc.Theater)
                .Select(s => new
                {
                    s.ShowtimeId,
                    s.MovieId,
                    MovieTitle = s.Movie.Title,
                    MoviePoster = s.Movie.PosterUrl,
                    MovieGenre = s.Movie.Genre,
                    MovieDuration = s.Movie.Duration,
                    MovieAgeRating = s.Movie.AgeRating,
                    s.StartTime,
                    s.EndTime,
                    s.BasePrice,
                    ScreenName = s.Screen.ScreenName,
                    ScreenType = s.Screen.ScreenType,
                    TheaterName = s.Screen.Theater.Name
                })
                .FirstOrDefaultAsync(s => s.ShowtimeId == id);

            if (showtime == null) return NotFound("Không tìm thấy suất chiếu.");
            return Ok(showtime);
        }

        // --- 5c. Lấy sơ đồ ghế của suất chiếu ---
        [HttpGet("{id}/seats")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSeats(int id)
        {
            var showtime = await _context.Showtimes.FindAsync(id);
            if (showtime == null) return NotFound("Không tìm thấy suất chiếu.");

            // 1. Lấy tất cả ghế của phòng này
            var allSeats = await _context.Seats
                .Where(s => s.ScreenId == showtime.ScreenId)
                .OrderBy(s => s.RowNumber)
                .ThenBy(s => s.SeatNumber)
                .ToListAsync();

            // 2. Lấy danh sách ghế đã được đặt (Booking)
            var bookedSeatIds = await _context.Bookings
                .Where(b => b.ShowtimeId == id && b.Status == "Hoàn thành")
                .Select(b => b.SeatId)
                .ToListAsync();

            // 3. Lấy danh sách ghế đang bị giữ (SeatLock) chưa hết hạn
            var lockedSeats = await _context.SeatLocks
                .Where(sl => sl.ShowtimeId == id && sl.ExpiryTime > DateTime.Now)
                .ToListAsync();

            // 4. Nhóm ghế theo hàng để trả về cho frontend
            var result = allSeats
                .GroupBy(s => s.RowNumber)
                .Select(g => new
                {
                    Row = g.Key,
                    Seats = g.Select(s => new
                    {
                        Id = s.SeatId,
                        Code = $"{s.RowNumber}{s.SeatNumber}",
                        Type = s.SeatType, // 'Normal' / 'VIP'
                        Status = bookedSeatIds.Contains(s.SeatId) ? "booked" :
                                 lockedSeats.Any(ls => ls.SeatId == s.SeatId) ? "locked" : "available",
                        LockerId = lockedSeats.FirstOrDefault(ls => ls.SeatId == s.SeatId)?.UserId
                    })
                });

            return Ok(result);
        }

        // --- 6. Endpoints dành cho khách hàng (Booking) ---
        [HttpGet("by-movie/{movieId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetShowtimesByMovie(int movieId, [FromQuery] int? theaterId, [FromQuery] DateTime? date)
        {
            var query = _context.Showtimes
                .AsNoTracking()
                .Where(s => s.MovieId == movieId && s.StartTime >= DateTime.Now)
                .Include(s => s.Screen)
                    .ThenInclude(sc => sc.Theater)
                .AsQueryable();

            if (theaterId.HasValue)
                query = query.Where(s => s.Screen.TheaterId == theaterId.Value);

            if (date.HasValue)
                query = query.Where(s => s.StartTime.Date == date.Value.Date);

            var data = await query
                .OrderBy(s => s.StartTime)
                .Select(s => new
                {
                    s.ShowtimeId,
                    s.MovieId,
                    s.ScreenId,
                    ScreenName = s.Screen.ScreenName,
                    ScreenType = s.Screen.ScreenType,
                    s.StartTime,
                    s.EndTime,
                    s.BasePrice,
                    TheaterId = s.Screen.TheaterId
                })
                .ToListAsync();
            return Ok(data);
        }

        [HttpGet("theaters-by-movie/{movieId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetTheatersByMovie(int movieId)
        {
            var theaters = await _context.Showtimes
                .AsNoTracking()
                .Where(s => s.MovieId == movieId && s.StartTime >= DateTime.Now)
                .Select(s => s.Screen.Theater)
                .Distinct()
                .Select(t => new
                {
                    t.TheaterId,
                    t.Name,
                    t.City
                })
                .ToListAsync();

            return Ok(theaters);
        }
    }
}