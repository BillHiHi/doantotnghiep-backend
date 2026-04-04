using doantotnghiep_api.Data;
using doantotnghiep_api.Dto_s;
using doantotnghiep_api.Dtos;
using doantotnghiep_api.Models;
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

        public ShowtimesController(AppDbContext context)
        {
            _context = context;
        }

        // 💡 RBAC HELPER: Lấy TheaterId của người dùng hiện tại từ Token
        private int? UserTheaterId => int.TryParse(User.Claims.FirstOrDefault(c => c.Type == "TheaterId")?.Value, out var id) ? id : null;
        private bool IsBranchAdmin => User.IsInRole("BRANCH_ADMIN");
        private bool IsSuperAdmin => User.IsInRole("SUPER_ADMIN") || User.IsInRole("Admin");

        // =====================================================
        // GET ALL
        // =====================================================
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? theaterId)
        {
            var query = _context.Showtimes.AsNoTracking();

            // 💡 RBAC LỌC THEO RẠP
            if (IsBranchAdmin && UserTheaterId.HasValue)
            {
                query = query.Where(s => s.Screen.TheaterId == UserTheaterId.Value);
            }
            else if (IsSuperAdmin && theaterId.HasValue)
            {
                query = query.Where(s => s.Screen.TheaterId == theaterId.Value);
            }

            if (from.HasValue)
                query = query.Where(s => s.StartTime >= from.Value);
            
            if (to.HasValue)
                query = query.Where(s => s.StartTime <= to.Value);

            // Nếu không có filter, mặc định lấy suất chiếu từ hôm nay trở đi để tránh tải quá nhiều
            if (!from.HasValue && !to.HasValue)
                query = query.Where(s => s.StartTime >= DateTime.Today);

            var data = await query
                .OrderByDescending(s => s.StartTime)
                .Select(s => new ShowtimeDto
                {
                    ShowtimeId = s.ShowtimeId,
                    MovieId = s.MovieId,
                    MovieTitle = s.Movie.Title,
                    ScreenId = s.ScreenId,
                    ScreenName = s.Screen.ScreenName,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    TheaterId = s.Screen.TheaterId,
                    BasePrice = s.BasePrice,
                    // Tối ưu hóa: Chỉ tính count khi cần thiết hoặc dùng JOIN
                    TotalSeats = s.Screen != null ? s.Screen.Seats.Count() : 0,
                    AvailableSeats = (s.Screen != null ? s.Screen.Seats.Count() : 0) - 
                                     (s.Bookings != null ? s.Bookings.Count(b => b.Status == "Hoàn thành" || b.Status == "Paid") : 0)
                })
                .ToListAsync();

            return Ok(data);
        }

        // =====================================================
        // GET DETAIL
        // =====================================================
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
                                     (_context.Bookings.Count(b => b.ShowtimeId == s.ShowtimeId && (b.Status == "Hoàn thành" || b.Status == "Paid")) +
                                      _context.SeatLocks.Count(sl => sl.ShowtimeId == s.ShowtimeId && sl.ExpiryTime > DateTime.UtcNow))
                })
                .FirstOrDefaultAsync();

            if (data == null)
                return NotFound();

            return Ok(data);
        }

        // =====================================================
        // GET SHOWTIMES BY MOVIE
        // =====================================================
        [HttpGet("movie/{movieId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByMovie(int movieId)
        {
            var data = await _context.Showtimes
                .AsNoTracking()
                .Where(s => s.MovieId == movieId)
                .OrderBy(s => s.StartTime)
                .Select(s => new ShowtimeSimpleDto
                {
                    ShowtimeId = s.ShowtimeId,
                    Time = s.StartTime.ToString("HH:mm"),
                    StartTime = s.StartTime,
                    TotalSeats = _context.Seats.Count(st => st.ScreenId == s.ScreenId),
                    AvailableSeats =
                        _context.Seats.Count(st => st.ScreenId == s.ScreenId) -
                        (_context.Bookings.Count(b => b.ShowtimeId == s.ShowtimeId && (b.Status == "Hoàn thành" || b.Status == "Paid")) +
                         _context.SeatLocks.Count(sl => sl.ShowtimeId == s.ShowtimeId && sl.ExpiryTime > DateTime.UtcNow))
                })
                .ToListAsync();

            return Ok(data);
        }

        // =====================================================
        // ⭐ GET MOVIES BY THEATER
        // =====================================================
        [HttpGet("movies-by-theater/{theaterId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetMoviesByTheater(int theaterId)
        {
            var data = await _context.Showtimes
                .AsNoTracking()
                .Where(s => s.Screen.TheaterId == theaterId)
                .Select(s => s.Movie)
                .Distinct()
                .ToListAsync();

            return Ok(data);
        }

        // =====================================================
        // ⭐ GET THEATERS BY MOVIE
        // =====================================================
        [HttpGet("theaters-by-movie/{movieId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetTheatersByMovie(int movieId)
        {
            var data = await _context.Showtimes
                .AsNoTracking()
                .Where(s => s.MovieId == movieId)
                .Select(s => s.Screen.Theater)
                .Distinct()
                .ToListAsync();

            return Ok(data);
        }

        [HttpGet("all-by-theater/{theaterId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllByTheater(int theaterId)
        {
            var data = await _context.Showtimes
                .AsNoTracking()
                .Where(s => s.Screen.TheaterId == theaterId)
                .OrderBy(s => s.StartTime)
                .Select(s => new ShowtimeDto
                {
                    ShowtimeId = s.ShowtimeId,
                    MovieId = s.MovieId,
                    MovieTitle = s.Movie.Title,
                    MoviePoster = s.Movie.PosterUrl,
                    MovieGenre = s.Movie.Genre,
                    MovieDuration = s.Movie.Duration,
                    MovieTrailer = s.Movie.TrailerUrl,
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
                                     (_context.Bookings.Count(b => b.ShowtimeId == s.ShowtimeId && (b.Status == "Hoàn thành" || b.Status == "Paid")) +
                                      _context.SeatLocks.Count(sl => sl.ShowtimeId == s.ShowtimeId && sl.ExpiryTime > DateTime.UtcNow))
                })
                .ToListAsync();

            return Ok(data);
        }

        // =====================================================
        // ⭐ GET SHOWTIMES BY MOVIE + THEATER + DATE
        // =====================================================
        [HttpGet("filter")]
        [AllowAnonymous]
        public async Task<IActionResult> Filter(
            int movieId,
            int theaterId,
            DateTime date)
        {
            var start = date.Date;
            var end = start.AddDays(1);

            var data = await _context.Showtimes
                .AsNoTracking()
                .Where(s =>
                    s.MovieId == movieId &&
                    s.Screen.TheaterId == theaterId &&
                    s.StartTime >= start &&
                    s.StartTime < end)
                .OrderBy(s => s.StartTime)
                .Select(s => new ShowtimeSimpleDto
                {
                    ShowtimeId = s.ShowtimeId,
                    Time = s.StartTime.ToString("HH:mm"),
                    StartTime = s.StartTime,
                    TotalSeats = _context.Seats.Count(st => st.ScreenId == s.ScreenId),
                    AvailableSeats =
                        _context.Seats.Count(st => st.ScreenId == s.ScreenId) -
                        _context.Bookings.Count(b =>
                            b.ShowtimeId == s.ShowtimeId &&
                            b.Status == "Hoàn thành")
                })
                .ToListAsync();

            return Ok(data);
        }

        // =====================================================
        // CREATE
        // =====================================================
        [HttpPost]
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN")]
        public async Task<IActionResult> Create(CreateShowtimeDto dto)
        {
            // 💡 KIỂM TRA PHÂN QUYỀN TRÊN SCREEN
            var screen = await _context.Screens.FindAsync(dto.ScreenId);
            if (screen == null) return BadRequest("Room not found");

            if (IsBranchAdmin && UserTheaterId.HasValue && screen.TheaterId != UserTheaterId.Value)
                return Forbid("Bạn không có quyền quản lý phòng chiếu của rạp khác");

            var movie = await _context.Movies.FindAsync(dto.MovieId);
            if (movie == null)
                return BadRequest("Movie not found");

            var newStartTime = dto.StartTime;
            var newEndTime = dto.EndTime == default
                ? dto.StartTime.AddMinutes(movie.Duration + 15)
                : dto.EndTime;

            // Kiểm tra trùng lịch
            var isOverlapping = await _context.Showtimes
                .AnyAsync(s => s.ScreenId == dto.ScreenId &&
                               s.StartTime < newEndTime &&
                               s.EndTime > newStartTime);

            if (isOverlapping)
            {
                return BadRequest("Lịch chiếu bị trùng với một suất chiếu khác tại phòng này.");
            }

            var showtime = new Showtime
            {
                MovieId = dto.MovieId,
                ScreenId = dto.ScreenId,
                StartTime = newStartTime,
                EndTime = newEndTime,
                BasePrice = dto.BasePrice
            };

            _context.Showtimes.Add(showtime);
            await _context.SaveChangesAsync();

            return Ok(showtime.ShowtimeId);
        }

        // =====================================================
        // UPDATE
        // =====================================================
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN")]
        public async Task<IActionResult> Update(int id, UpdateShowtimeDto dto)
        {
            var showtime = await _context.Showtimes
                .Include(s => s.Screen)
                .FirstOrDefaultAsync(s => s.ShowtimeId == id);
            
            if (showtime == null) return NotFound();

            // 💡 CHẶN LỖI PHÂN QUYỀN
            if (IsBranchAdmin && UserTheaterId.HasValue && showtime.Screen.TheaterId != UserTheaterId.Value)
                return Forbid("Bạn không có quyền sửa suất chiếu rạp khác");

            var movie = await _context.Movies.FindAsync(dto.MovieId);
            if (movie == null)
                return BadRequest("Movie not found");

            var newStartTime = dto.StartTime;
            var newEndTime = dto.EndTime == default
                ? dto.StartTime.AddMinutes(movie.Duration + 15)
                : dto.EndTime;

            // Kiểm tra trùng lịch (bỏ qua showtime hiện tại)
            var isOverlapping = await _context.Showtimes
                .AnyAsync(s => s.ShowtimeId != id &&
                               s.ScreenId == dto.ScreenId &&
                               s.StartTime < newEndTime &&
                               s.EndTime > newStartTime);

            if (isOverlapping)
            {
                return BadRequest("Lịch chiếu bị trùng với một suất chiếu khác tại phòng này.");
            }

            showtime.MovieId = dto.MovieId;
            showtime.ScreenId = dto.ScreenId;
            showtime.StartTime = newStartTime;
            showtime.EndTime = newEndTime;
            showtime.BasePrice = dto.BasePrice;

            await _context.SaveChangesAsync();

            return NoContent();
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

            // 💡 CHẶN LỖI PHÂN QUYỀN
            if (IsBranchAdmin && UserTheaterId.HasValue && showtime.Screen.TheaterId != UserTheaterId.Value)
                return Forbid("Bạn không có quyền xóa suất chiếu rạp khác");

            _context.Showtimes.Remove(showtime);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // =====================================================
        // GET SEATS (TỐI ƯU HÓA ĐỈNH CAO - 1 DB ROUNDTRIP)
        // =====================================================
        [HttpGet("{id}/seats")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSeats(int id)
        {
            try
            {
                // Sử dụng Navigation Property (s.Screen.Showtimes) để gộp 4 truy vấn làm 1.
                // Đẩy toàn bộ logic kiểm tra "ghế đã bán" và "ghế đang khóa" xuống Database PostgreSQL
                var seatsData = await _context.Seats
                    .AsNoTracking()
                    // 1. Chỉ lấy ghế của phòng có suất chiếu mang ID này
                    .Where(s => s.Screen.Showtimes.Any(st => st.ShowtimeId == id))
                    .Select(s => new
                    {
                        Seat = s,

                        // 2. Sub-query (EXISTS in SQL): Kiểm tra ghế đã bán chưa
                        IsBooked = _context.Bookings.Any(b =>
                            b.SeatId == s.SeatId &&
                            b.ShowtimeId == id &&
                            (b.Status == "Hoàn thành" || b.Status == "Paid")),

                        // 3. Sub-query: Lấy UserId đang khóa ghế này (nếu có)
                        LockerId = _context.SeatLocks
                            .Where(l =>
                                l.SeatId == s.SeatId &&
                                l.ShowtimeId == id &&
                                l.ExpiryTime > DateTime.UtcNow)
                            .Select(l => (int?)l.UserId)
                            .FirstOrDefault()
                    })
                    // Nhờ DB sắp xếp luôn cho nhẹ RAM backend
                    .OrderBy(x => x.Seat.RowNumber ?? "Unknown")
                    .ThenBy(x => x.Seat.SeatNumber)
                    .ToListAsync();

                if (!seatsData.Any())
                {
                    return Ok(new List<object>());
                }

                // 4. Nhóm ghế theo hàng ngay trên RAM của C# (lúc này list data đã cực kỳ nhỏ gọn)
                var result = seatsData
                    .GroupBy(x => x.Seat.RowNumber ?? "Unknown")
                    .Select(g => new
                    {
                        Row = g.Key,
                        Seats = g.Select(x => new
                        {
                            Id = x.Seat.SeatId,
                            Code = $"{(x.Seat.RowNumber ?? "Unknown")}{x.Seat.SeatNumber}",
                            Type = (x.Seat.SeatType ?? "Standard").ToLower(),
                            // Render status cực nhanh vì đã xử lý ở Database
                            Status = x.IsBooked ? "booked" :
                                     x.LockerId.HasValue ? "locked" :
                                     "available",
                            LockerId = x.LockerId ?? 0
                        }).ToList()
                    })
                    .ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "Lỗi khi load ghế",
                    detail = ex.Message
                });
            }
        }

        // =====================================================
        // ⭐ SCHEDULE BY THEATER + DATE (CHO UI LỊCH CHIẾU)
        // =====================================================
        [HttpGet("schedule")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSchedule(int theaterId, DateTime date)
        {
            var start = date.Date;
            var end = start.AddDays(1);

            var screens = await _context.Screens
                .AsNoTracking()
                .Where(sc => sc.TheaterId == theaterId)
                .Select(sc => new
                {
                    sc.ScreenId,
                    sc.ScreenName,
                    sc.ScreenType,

                    Showtimes = _context.Showtimes
                        .Where(s =>
                            s.ScreenId == sc.ScreenId &&
                            s.StartTime >= start &&
                            s.StartTime < end)
                        .OrderBy(s => s.StartTime)
                        .Select(s => new
                        {
                            s.ShowtimeId,
                            MovieTitle = s.Movie.Title,
                            s.BasePrice,
                            Start = s.StartTime.ToString("HH:mm"),
                            End = s.EndTime.ToString("HH:mm")
                        })
                        .ToList()
                })
                .ToListAsync();

            return Ok(screens);
        }

    }
}