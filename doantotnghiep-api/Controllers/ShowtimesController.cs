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
        private readonly ShowtimeAutomationService _automationService;

        public ShowtimesController(AppDbContext context, ShowtimeAutomationService automationService)
        {
            _context = context;
            _automationService = automationService;
        }

        // 💡 RBAC HELPER: Lấy TheaterId của người dùng hiện tại từ Token
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
        // GET ALL
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
                    TotalSeats = s.Screen != null
                                        ? _context.Seats.Count(st => st.ScreenId == s.ScreenId)
                                        : 0,
                    AvailableSeats = (s.Screen != null
                                        ? _context.Seats.Count(st => st.ScreenId == s.ScreenId)
                                        : 0)
                                     - _context.Bookings.Count(b =>
                                         b.ShowtimeId == s.ShowtimeId &&
                                         (b.Status == "Hoàn thành" || b.Status == "Paid"
                                          || b.Status == "paid" || b.Status == "collected")),
                    IsEarlyScreening = s.IsEarlyScreening
                })
                .ToListAsync();

            return Ok(data);
        }

        // =====================================================
        // ⭐ AUTO GENERATE (Dựa trên hợp đồng KPI)
        // =====================================================
        [HttpPost("generate-auto")]
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN")]
        public async Task<IActionResult> GenerateAuto([FromBody] GenerateAutoDto dto)
        {
            if (IsBranchAdmin && UserTheaterId.HasValue && dto.TheaterId != UserTheaterId.Value)
                return Forbid("Bạn không có quyền tạo lịch cho rạp khác");

            try
            {
                var result = await _automationService.GenerateShowtimesAsync(dto.TheaterId, dto.TargetDate, dto.MovieIds);

                if (!result.Success)
                {
                    return Ok(result); 
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi hệ thống: {ex.Message}");
            }
        }

        // =====================================================
        // GET DETAIL
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
                                      _context.SeatLocks.Count(sl => sl.ShowtimeId == s.ShowtimeId && sl.ExpiryTime > DateTime.UtcNow)),
                    IsEarlyScreening = s.IsEarlyScreening
                })
                .FirstOrDefaultAsync();

            if (data == null)
                return NotFound();

            return Ok(data);
        }

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
                                      _context.SeatLocks.Count(sl => sl.ShowtimeId == s.ShowtimeId && sl.ExpiryTime > DateTime.UtcNow)),
                    IsEarlyScreening = s.IsEarlyScreening
                })
                .ToListAsync();

            return Ok(data);
        }

        [HttpGet("filter")]
        [AllowAnonymous]
        public async Task<IActionResult> Filter(int movieId, int theaterId, DateTime date)
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

        [HttpPost]
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN")]
        public async Task<IActionResult> Create(CreateShowtimeDto dto)
        {
            var screen = await _context.Screens.FindAsync(dto.ScreenId);
            if (screen == null) return BadRequest("Room not found");

            if (IsBranchAdmin && UserTheaterId.HasValue && screen.TheaterId != UserTheaterId.Value)
                return Forbid("Bạn không có quyền quản lý phòng chiếu của rạp khác");

            var movie = await _context.Movies.FindAsync(dto.MovieId);
            if (movie == null) return BadRequest("Movie not found");

            var newStartTime = dto.StartTime;
            var newEndTime = dto.EndTime == default ? dto.StartTime.AddMinutes(movie.Duration + 15) : dto.EndTime;

            var isOverlapping = await _context.Showtimes
                .AnyAsync(s => s.ScreenId == dto.ScreenId && s.StartTime < newEndTime && s.EndTime > newStartTime);

            if (isOverlapping) return BadRequest("Lịch chiếu bị trùng với một suất chiếu khác tại phòng này.");

            var showtime = new Showtime
            {
                MovieId = dto.MovieId,
                ScreenId = dto.ScreenId,
                StartTime = newStartTime,
                EndTime = newEndTime,
                BasePrice = dto.BasePrice,
                IsEarlyScreening = dto.IsEarlyScreening
            };

            _context.Showtimes.Add(showtime);
            await _context.SaveChangesAsync();
            return Ok(showtime.ShowtimeId);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN")]
        public async Task<IActionResult> Update(int id, UpdateShowtimeDto dto)
        {
            var showtime = await _context.Showtimes
                .Include(s => s.Screen)
                .FirstOrDefaultAsync(s => s.ShowtimeId == id);

            if (showtime == null) return NotFound();

            if (IsBranchAdmin && UserTheaterId.HasValue && showtime.Screen.TheaterId != UserTheaterId.Value)
                return Forbid("Bạn không có quyền sửa suất chiếu rạp khác");

            var movie = await _context.Movies.FindAsync(dto.MovieId);
            if (movie == null) return BadRequest("Movie not found");

            var newStartTime = dto.StartTime;
            var newEndTime = dto.EndTime == default ? dto.StartTime.AddMinutes(movie.Duration + 15) : dto.EndTime;

            var isOverlapping = await _context.Showtimes
                .AnyAsync(s => s.ShowtimeId != id && s.ScreenId == dto.ScreenId && s.StartTime < newEndTime && s.EndTime > newStartTime);

            if (isOverlapping) return BadRequest("Lịch chiếu bị trùng với một suất chiếu khác tại phòng này.");

            showtime.MovieId = dto.MovieId;
            showtime.ScreenId = dto.ScreenId;
            showtime.StartTime = newStartTime;
            showtime.EndTime = newEndTime;
            showtime.BasePrice = dto.BasePrice;
            showtime.IsEarlyScreening = dto.IsEarlyScreening;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN")]
        public async Task<IActionResult> Delete(int id)
        {
            var showtime = await _context.Showtimes
                .Include(s => s.Screen)
                .FirstOrDefaultAsync(s => s.ShowtimeId == id);

            if (showtime == null) return NotFound();

            if (IsBranchAdmin && UserTheaterId.HasValue && showtime.Screen.TheaterId != UserTheaterId.Value)
                return Forbid("Bạn không có quyền xóa suất chiếu rạp khác");

            _context.Showtimes.Remove(showtime);
            await _context.SaveChangesAsync();
            return NoContent();
        }

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
                        IsBooked = _context.Bookings.Any(b => b.SeatId == s.SeatId && b.ShowtimeId == id && (b.Status == "Hoàn thành" || b.Status == "Paid")),
                        LockerId = _context.SeatLocks.Where(l => l.SeatId == s.SeatId && l.ShowtimeId == id && l.ExpiryTime > DateTime.UtcNow).Select(l => (int?)l.UserId).FirstOrDefault()
                    })
                    .OrderBy(x => x.Seat.RowNumber ?? "Unknown")
                    .ThenBy(x => x.Seat.SeatNumber)
                    .ToListAsync();

                if (!seatsData.Any()) return Ok(new List<object>());

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
                            Status = x.IsBooked ? "booked" : x.LockerId.HasValue ? "locked" : "available",
                            LockerId = x.LockerId ?? 0
                        }).ToList()
                    })
                    .ToList();

                return Ok(result);
            }
            catch (Exception ex) { return StatusCode(500, new { error = "Lỗi khi load ghế", detail = ex.Message }); }
        }

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
                        .Where(s => s.ScreenId == sc.ScreenId && s.StartTime >= start && s.StartTime < end)
                        .OrderBy(s => s.StartTime)
                        .Select(s => new
                        {
                            s.ShowtimeId,
                            MovieTitle = s.Movie.Title,
                            s.BasePrice,
                            Start = s.StartTime.ToString("HH:mm"),
                            End = s.EndTime.ToString("HH:mm"),
                            IsEarlyScreening = s.IsEarlyScreening
                        })
                        .ToList()
                })
                .ToListAsync();

            return Ok(screens);
        }

        [HttpPost("seed-today-showtimes/{theaterId}")]
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN")]
        public async Task<IActionResult> SeedTodayShowtimesForTheater(int theaterId)
        {
            try
            {
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var userTheaterId = User.FindFirst("TheaterId")?.Value;
                if (userRole == "BRANCH_ADMIN" && userTheaterId != theaterId.ToString()) return Forbid();

                var today = DateTime.UtcNow.Date;
                var timeSlots = new[] { "09:00", "12:00", "15:00", "18:00", "21:00" };
                var screens = await _context.Screens.Where(s => s.TheaterId == theaterId).Include(s => s.Theater).ToListAsync();
                if (!screens.Any()) return BadRequest(new { message = "Không tìm thấy phòng chiếu nào" });

                var movieIds = await _context.TheaterMovies.Where(tm => tm.TheaterId == theaterId).Select(tm => tm.MovieId).ToListAsync();
                var moviesQuery = _context.Movies.AsQueryable();
                if (movieIds.Any()) moviesQuery = moviesQuery.Where(m => movieIds.Contains(m.MovieId));
                else moviesQuery = moviesQuery.Where(m => m.Status == "NowShowing" || m.Status == "Đang chiếu");

                var movies = await moviesQuery.ToListAsync();
                if (!movies.Any()) return BadRequest(new { message = "Không tìm thấy phim nào" });

                var random = new Random();
                foreach (var timeSlot in timeSlots)
                {
                    var startTime = DateTime.ParseExact(timeSlot, "HH:mm", null);
                    foreach (var screen in screens)
                    {
                        var slotStart = today.AddHours(startTime.Hour).AddMinutes(startTime.Minute);
                        var slotEnd = slotStart.AddHours(3);
                        if (await _context.Showtimes.AnyAsync(s => s.ScreenId == screen.ScreenId && s.StartTime.Date == today && s.StartTime < slotEnd && s.EndTime > slotStart)) continue;

                        var movie = movies[random.Next(movies.Count)];
                        _context.Showtimes.Add(new Showtime
                        {
                            MovieId = movie.MovieId,
                            ScreenId = screen.ScreenId,
                            StartTime = slotStart,
                            EndTime = slotStart.AddMinutes(movie.Duration + 15),
                            BasePrice = CalculateBasePrice(screen.ScreenType)
                        });
                    }
                }
                await _context.SaveChangesAsync();
                return Ok(new { message = "Tạo suất chiếu thành công!" });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Lỗi khi tạo suất chiếu", detail = ex.Message }); }
        }

        private decimal CalculateBasePrice(string screenType)
        {
            return screenType?.ToLower() switch { "imax" => 150000, "4dx" => 140000, "3d" => 100000, _ => 80000 };
        }

        [HttpDelete("delete-all-showtimes/{theaterId}")]
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN")]
        public async Task<IActionResult> DeleteAllShowtimesForTheater(int theaterId, [FromQuery] DateTime? date)
        {
            try
            {
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var userTheaterId = User.FindFirst("TheaterId")?.Value;
                if (userRole == "BRANCH_ADMIN" && userTheaterId != theaterId.ToString()) return Forbid();

                var targetDate = date?.Date ?? DateTime.UtcNow.Date;
                var showtimesToDelete = await _context.Showtimes.Where(s => s.Screen.TheaterId == theaterId && s.StartTime.Date == targetDate).ToListAsync();
                if (!showtimesToDelete.Any()) return Ok(new { message = "Không có suất chiếu để xóa", deletedCount = 0 });

                _context.Showtimes.RemoveRange(showtimesToDelete);
                await _context.SaveChangesAsync();
                return Ok(new { message = $"Đã xóa {showtimesToDelete.Count} suất chiếu!", deletedCount = showtimesToDelete.Count });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Lỗi khi xóa", detail = ex.Message }); }
        }
    }
}