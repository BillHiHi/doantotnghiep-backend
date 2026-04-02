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

        // =====================================================
        // GET ALL
        // =====================================================
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll(DateTime? from, DateTime? to)
        {
            var query = _context.Showtimes.AsNoTracking();

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
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(CreateShowtimeDto dto)
        {
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
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, UpdateShowtimeDto dto)
        {
            var showtime = await _context.Showtimes.FindAsync(id);
            if (showtime == null)
                return NotFound();

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
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var showtime = await _context.Showtimes.FindAsync(id);
            if (showtime == null)
                return NotFound();

            _context.Showtimes.Remove(showtime);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // =====================================================
        // GET SEATS (Đã dọn dẹp Git Conflict và tối ưu an toàn)
        // =====================================================
        [HttpGet("{id}/seats")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSeats(int id)
        {
            try
            {
                // 1. Lấy thông tin suất chiếu
                var showtime = await _context.Showtimes
                    .AsNoTracking()
                    .Where(s => s.ShowtimeId == id)
                    .Select(s => new { s.ScreenId })
                    .FirstOrDefaultAsync();

                if (showtime == null)
                    return NotFound(new { detail = $"Không tìm thấy suất chiếu ID {id}" });

                // 2. Lấy danh sách ghế của phòng này
                var seats = await _context.Seats
                    .AsNoTracking()
                    .Where(s => s.ScreenId == showtime.ScreenId)
                    .ToListAsync();

                if (seats == null || !seats.Any())
                {
                    // Nếu không có ghế, trả về mảng rỗng thay vì lỗi
                    return Ok(new List<object>());
                }

                // 3. Lấy thông tin vé đã bán
                var bookedIds = await _context.Bookings
                    .AsNoTracking()
                    .Where(b => b.ShowtimeId == id && (b.Status == "Hoàn thành" || b.Status == "Paid"))
                    .Select(b => b.SeatId)
                    .ToListAsync();

                var bookedSet = bookedIds.ToHashSet();

                // 4. Lấy thông tin ghế đang bị khóa (giữ ghế tạm thời)
                var lockedList = await _context.SeatLocks
                    .AsNoTracking()
                    .Where(l => l.ShowtimeId == id && l.ExpiryTime > DateTime.UtcNow)
                    .Select(l => new { l.SeatId, l.UserId })
                    .ToListAsync();

                var lockedLookup = lockedList.ToLookup(l => l.SeatId, l => l.UserId);

                // 5. Nhóm ghế theo hàng và map ra cấu trúc JSON
                var result = seats
                    .OrderBy(s => s.RowNumber ?? "Unknown")
                    .ThenBy(s => s.SeatNumber)
                    .GroupBy(s => s.RowNumber ?? "Unknown")
                    .Select(g => new
                    {
                        Row = g.Key,
                        Seats = g.Select(s => new
                        {
                            Id = s.SeatId,
                            Code = $"{(s.RowNumber ?? "Unknown")}{s.SeatNumber}",
                            Type = (s.SeatType ?? "Standard").ToLower(),
                            Status =
                                bookedSet.Contains(s.SeatId) ? "booked" :
                                lockedLookup.Contains(s.SeatId) ? "locked" :
                                "available",
                            LockerId = lockedLookup.Contains(s.SeatId) ? lockedLookup[s.SeatId].FirstOrDefault() : 0
                        }).ToList()
                    })
                    .ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                // Bắt lỗi chi tiết
                return StatusCode(500, new
                {
                    error = "Lỗi nghiêm trọng khi load ghế",
                    detail = ex.Message,
                    inner = ex.InnerException?.Message,
                    stack = ex.StackTrace
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