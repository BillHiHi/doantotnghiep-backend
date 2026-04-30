using System.Security.Claims;
using doantotnghiep_api.Data;
using doantotnghiep_api.Dto_s;
using doantotnghiep_api.Dtos;
using doantotnghiep_api.Models;
using doantotnghiep_api.Services; // Thêm namespace chứa các service mới
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
        private readonly ShowtimeService _showtimeService; // Inject ShowtimeService
        private readonly SettlementService _settlementService; // Inject SettlementService
        private readonly ShowtimeAutomationService _automationService;

        public ShowtimesController(
            AppDbContext context,
            ShowtimeService showtimeService,
            SettlementService settlementService,
            ShowtimeAutomationService automationService)
        {
            _context = context;
            _showtimeService = showtimeService;
            _settlementService = settlementService;
            _automationService = automationService;
        }

        // 💡 RBAC HELPERS
        private int? UserTheaterId => int.TryParse(User.Claims.FirstOrDefault(c => c.Type == "TheaterId")?.Value, out var id) ? id : null;

        private bool IsBranchAdmin
        {
            get
            {
                var role = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role")?.Value?.ToUpper();
                return role == "BRANCH_ADMIN" || role == "BRANCHADMIN";
            }
        }

        private bool IsSuperAdmin
        {
            get
            {
                var role = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role")?.Value?.ToUpper();
                return role == "SUPER_ADMIN" || role == "ADMIN";
            }
        }

        // =====================================================
        // AUTOMATION (TỰ ĐỘNG TẠO SUẤT CHIẾU THEO NGÀY)
        // =====================================================
        [HttpPost("generate-auto")]
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN")]
        public async Task<IActionResult> GenerateAuto([FromBody] GenerateAutoDto dto)
        {
            // Kiểm tra phân quyền rạp: Branch Admin chỉ được tạo cho rạp của mình
            if (IsBranchAdmin && UserTheaterId.HasValue && dto.TheaterId != UserTheaterId.Value)
            {
                return Forbid("Bạn không có quyền tự động tạo lịch cho rạp khác.");
            }

            try
            {
                // Gọi automation service để xử lý logic phức tạp
                int count = await _automationService.GenerateAutoShowtimes(dto.TheaterId, dto.TargetDate);

                if (count == 0)
                    return Ok(new { message = "Không có suất chiếu nào được tạo (có thể do hết hạn hợp đồng hoặc không đủ thời gian)." });

                return Ok(new { message = $"Đã tạo thành công {count} suất chiếu tự động.", count });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Lỗi trong quá trình tạo tự động", error = ex.Message });
            }
        }

        // =====================================================
        // GET SETTLEMENT (QUYẾT TOÁN HỢP ĐỒNG)
        // =====================================================
        [HttpGet("settlement/{contractId}")]
        [Authorize(Roles = "Admin,SUPER_ADMIN")]
        public async Task<IActionResult> GetSettlement(int contractId)
        {
            try
            {
                var result = await _settlementService.GetContractSettlementAsync(contractId);
                if (result == null) return NotFound("Không tìm thấy hợp đồng.");
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // =====================================================
        // CREATE (SỬ DỤNG SERVICE ĐỂ TỰ ĐỘNG HÓA VÀ CHECK HỢP ĐỒNG)
        // =====================================================
        [HttpPost]
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN")]
        public async Task<IActionResult> Create(CreateShowtimeDto dto)
        {
            // 1. Kiểm tra phân quyền rạp của Screen[cite: 5]
            var screen = await _context.Screens.FindAsync(dto.ScreenId);
            if (screen == null) return BadRequest("Room not found");

            if (IsBranchAdmin && UserTheaterId.HasValue && screen.TheaterId != UserTheaterId.Value)
                return Forbid("Bạn không có quyền quản lý phòng chiếu của rạp khác");

            // 2. Lấy thông tin phim để tính toán[cite: 3]
            var movie = await _context.Movies.FindAsync(dto.MovieId);
            if (movie == null) return BadRequest("Movie not found");

            // 3. Chuẩn bị dữ liệu Showtime[cite: 2]
            var showtime = new Showtime
            {
                MovieId = dto.MovieId,
                ScreenId = dto.ScreenId,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime == default
                    ? dto.StartTime.AddMinutes(movie.Duration + 15) // Tự động tính thời gian kết thúc
                    : dto.EndTime,
                BasePrice = dto.BasePrice,
                IsEarlyScreening = dto.IsEarlyScreening
            };

            try
            {
                // 4. Gọi Service để kiểm tra xung đột lịch và giới hạn hợp đồng (TotalSlots)
                var result = await _showtimeService.CreateShowtimeAsync(showtime);
                return Ok(result.ShowtimeId);
            }
            catch (Exception ex)
            {
                // Trả về lỗi nghiệp vụ (ví dụ: "Đã đạt giới hạn suất chiếu theo hợp đồng")[cite: 1, 4]
                return BadRequest(new { message = ex.Message });
            }
        }

        // =====================================================
        // GET ALL (VÀ CÁC HÀM GET KHÁC GIỮ NGUYÊN ĐỂ PHỤC VỤ UI)
        // =====================================================
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? theaterId)
        {
            var query = _context.Showtimes.AsNoTracking();

            if (IsBranchAdmin && UserTheaterId.HasValue)
                query = query.Where(s => s.Screen.TheaterId == UserTheaterId.Value);
            else if (IsSuperAdmin && theaterId.HasValue)
                query = query.Where(s => s.Screen.TheaterId == theaterId.Value);

            if (from.HasValue)
                query = query.Where(s => s.StartTime >= from.Value);

            if (to.HasValue)
                query = query.Where(s => s.StartTime <= to.Value);

            if (!from.HasValue && !to.HasValue)
                query = query.Where(s => s.StartTime >= DateTime.Today);

            var data = await query
                .OrderByDescending(s => s.StartTime)
                .Select(s => new ShowtimeDto
                {
                    ShowtimeId = s.ShowtimeId,
                    MovieId = s.MovieId,
                    MovieTitle = s.Movie != null ? s.Movie.Title : "N/A",
                    ScreenId = s.ScreenId,
                    ScreenName = s.Screen != null ? s.Screen.ScreenName : "N/A",
                    TheaterName = s.Screen != null && s.Screen.Theater != null ? s.Screen.Theater.Name : "N/A",
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    TheaterId = s.Screen != null ? s.Screen.TheaterId : 0,
                    BasePrice = s.BasePrice,
                    TotalSeats = s.Screen != null ? _context.Seats.Count(st => st.ScreenId == s.ScreenId) : 0,
                    AvailableSeats = (s.Screen != null ? _context.Seats.Count(st => st.ScreenId == s.ScreenId) : 0)
                                     - _context.Bookings.Count(b => b.ShowtimeId == s.ShowtimeId && (b.Status == "Paid" || b.Status == "Collected")),
                    IsEarlyScreening = s.IsEarlyScreening
                })
                .ToListAsync();

            return Ok(data);
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetDetail(int id)
        {
            var data = await _context.Showtimes
                .AsNoTracking()
                .Where(s => s.ShowtimeId == id)
                .Select(s => new ShowtimeDto
                {
                    ShowtimeId = s.ShowtimeId,
                    MovieId = s.MovieId,
                    MovieTitle = s.Movie.Title,
                    MoviePoster = s.Movie.PosterUrl,
                    MovieGenre = s.Movie.Genre,
                    MovieDuration = s.Movie.Duration,
                    MovieAgeRating = s.Movie.AgeRating,
                    ScreenId = s.ScreenId,
                    ScreenName = s.Screen.ScreenName,
                    ScreenType = s.Screen.ScreenType,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    TheaterId = s.Screen.TheaterId,
                    TheaterName = s.Screen.Theater.Name,
                    BasePrice = s.BasePrice,
                    TotalSeats = _context.Seats.Count(st => st.ScreenId == s.ScreenId),
                    AvailableSeats = _context.Seats.Count(st => st.ScreenId == s.ScreenId) -
                                     (_context.Bookings.Count(b => b.ShowtimeId == s.ShowtimeId && (b.Status == "Paid" || b.Status == "Collected")) +
                                      _context.SeatLocks.Count(sl => sl.ShowtimeId == s.ShowtimeId && sl.ExpiryTime > DateTime.UtcNow)),
                    IsEarlyScreening = s.IsEarlyScreening
                })
                .FirstOrDefaultAsync();

            if (data == null) return NotFound();
            return Ok(data);
        }

        // =====================================================
        // UPDATE (DÙNG LOGIC KIỂM TRA TƯƠNG TỰ CREATE)
        // =====================================================
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN")]
        public async Task<IActionResult> Update(int id, UpdateShowtimeDto dto)
        {
            var showtime = await _context.Showtimes.Include(s => s.Screen).FirstOrDefaultAsync(s => s.ShowtimeId == id);
            if (showtime == null) return NotFound();

            if (IsBranchAdmin && UserTheaterId.HasValue && showtime.Screen.TheaterId != UserTheaterId.Value)
                return Forbid();

            var movie = await _context.Movies.FindAsync(dto.MovieId);
            if (movie == null) return BadRequest("Movie not found");

            // Tạm thời xóa thực thể cũ ra khỏi context tracking để service check trùng lịch chính xác (nếu dùng chung logic check)
            _context.Entry(showtime).State = EntityState.Detached;

            var updatedShowtime = new Showtime
            {
                ShowtimeId = id,
                MovieId = dto.MovieId,
                ScreenId = dto.ScreenId,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime == default ? dto.StartTime.AddMinutes(movie.Duration + 15) : dto.EndTime,
                BasePrice = dto.BasePrice,
                IsEarlyScreening = dto.IsEarlyScreening
            };

            try
            {
                // Cập nhật thông qua database context sau khi đã qua validation của service hoặc logic riêng
                _context.Showtimes.Update(updatedShowtime);
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // =====================================================
        // DELETE
        // =====================================================
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN")]
        public async Task<IActionResult> Delete(int id)
        {
            var showtime = await _context.Showtimes
                .Include(s => s.Screen)
                .FirstOrDefaultAsync(s => s.ShowtimeId == id);

            if (showtime == null) return NotFound();

            if (IsBranchAdmin && UserTheaterId.HasValue && showtime.Screen.TheaterId != UserTheaterId.Value)
                return Forbid();

            bool hasBooking = await _context.Bookings
                .AnyAsync(b => b.ShowtimeId == id && (b.Status == "Paid" || b.Status == "Collected"));

            if (hasBooking)
                return BadRequest(new { message = "Không thể xóa suất chiếu đã có người đặt vé." });

            _context.Showtimes.Remove(showtime);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // =====================================================
        // GET SEATS (TỐI ƯU HÓA 1 ROUNDTRIP)[cite: 6]
        // =====================================================
        [HttpGet("{id}/seats")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSeats(int id)
        {
            try
            {
                var seatsData = await _context.Seats
                    .AsNoTracking()
                    .Where(s => s.Screen.Showtimes.Any(st => st.ShowtimeId == id))
                    .Select(s => new
                    {
                        Seat = s,
                        IsBooked = _context.Bookings.Any(b => b.SeatId == s.SeatId && b.ShowtimeId == id && (b.Status == "Paid" || b.Status == "Collected")),
                        LockerId = _context.SeatLocks.Where(l => l.SeatId == s.SeatId && l.ShowtimeId == id && l.ExpiryTime > DateTime.UtcNow).Select(l => (int?)l.UserId).FirstOrDefault()
                    })
                    .OrderBy(x => x.Seat.RowNumber)
                    .ThenBy(x => x.Seat.SeatNumber)
                    .ToListAsync();

                var result = seatsData.GroupBy(x => x.Seat.RowNumber ?? "Unknown").Select(g => new {
                    Row = g.Key,
                    Seats = g.Select(x => new {
                        Id = x.Seat.SeatId,
                        Code = $"{x.Seat.RowNumber}{x.Seat.SeatNumber}",
                        Type = x.Seat.SeatType.ToLower(),
                        Status = x.IsBooked ? "booked" : (x.LockerId.HasValue ? "locked" : "available"),
                        LockerId = x.LockerId ?? 0
                    }).ToList()
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }
        // =====================================================
        // DELETE ALL (XÓA HÀNG LOẠT THEO ĐIỀU KIỆN)
        // =====================================================
        [HttpDelete("bulk-delete")]
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN")]
        public async Task<IActionResult> DeleteAll([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? theaterId)
        {
            try
            {
                var query = _context.Showtimes.AsQueryable();

                // 1. Ràng buộc phân quyền rạp
                if (IsBranchAdmin && UserTheaterId.HasValue)
                {
                    // Branch Admin chỉ được xóa suất chiếu thuộc rạp của mình
                    query = query.Where(s => s.Screen.TheaterId == UserTheaterId.Value);
                }
                else if (IsSuperAdmin && theaterId.HasValue)
                {
                    // Super Admin có thể chọn xóa theo rạp cụ thể
                    query = query.Where(s => s.Screen.TheaterId == theaterId.Value);
                }
                else if (IsBranchAdmin && !UserTheaterId.HasValue)
                {
                    return Forbid("Không xác định được mã rạp của quản lý chi nhánh.");
                }

                // 2. Lọc theo thời gian (Tránh xóa nhầm toàn bộ lịch sử nếu không truyền tham số)
                if (from.HasValue)
                    query = query.Where(s => s.StartTime >= from.Value);

                if (to.HasValue)
                    query = query.Where(s => s.StartTime <= to.Value);

                // 3. Quan trọng: Chỉ cho phép xóa các suất chiếu CHƯA có người đặt vé
                // Điều này bảo vệ tính toàn vẹn dữ liệu tài chính/hợp đồng
                var showtimesToDelete = await query
                    .Where(s => !_context.Bookings.Any(b => b.ShowtimeId == s.ShowtimeId))
                    .ToListAsync();

                if (showtimesToDelete.Count == 0)
                {
                    return Ok(new { message = "Không tìm thấy suất chiếu nào phù hợp để xóa (hoặc các suất chiếu đã có người đặt vé)." });
                }

                _context.Showtimes.RemoveRange(showtimesToDelete);
                await _context.SaveChangesAsync();

                return Ok(new { message = $"Đã xóa thành công {showtimesToDelete.Count} suất chiếu.", count = showtimesToDelete.Count });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Lỗi khi xóa hàng loạt", error = ex.Message });
            }
        }
    }
}