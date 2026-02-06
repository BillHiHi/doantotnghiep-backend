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
    [Route("api/[controller]")]
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
        public async Task<IActionResult> GetAll()
        {
            var data = await _context.Showtimes
                .AsNoTracking()
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
                    TotalSeats = _context.Seats.Count(st => st.ScreenId == s.ScreenId),
                    AvailableSeats = _context.Seats.Count(st => st.ScreenId == s.ScreenId) -
                                     _context.Bookings.Count(b => b.ShowtimeId == s.ShowtimeId && b.Status == "Hoàn thành")
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
                    ScreenId = s.ScreenId,
                    ScreenName = s.Screen.ScreenName,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    TheaterId = s.Screen.TheaterId,
                    BasePrice = s.BasePrice,
                    TotalSeats = _context.Seats.Count(st => st.ScreenId == s.ScreenId),
                    AvailableSeats = _context.Seats.Count(st => st.ScreenId == s.ScreenId) -
                                     _context.Bookings.Count(b => b.ShowtimeId == s.ShowtimeId && b.Status == "Hoàn thành")
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
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    TheaterId = s.Screen.TheaterId,
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

            var showtime = new Showtime
            {
                MovieId = dto.MovieId,
                ScreenId = dto.ScreenId,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime == default
                    ? dto.StartTime.AddMinutes(movie.Duration + 15)
                    : dto.EndTime,
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

            showtime.MovieId = dto.MovieId;
            showtime.ScreenId = dto.ScreenId;
            showtime.StartTime = dto.StartTime;
            showtime.EndTime = dto.EndTime;
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
        // GET SEATS
        // =====================================================
        [HttpGet("{id}/seats")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSeats(int id)
        {
            var showtime = await _context.Showtimes
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ShowtimeId == id);

            if (showtime == null)
                return NotFound();

            var seats = await _context.Seats
                .AsNoTracking()
                .Where(s => s.ScreenId == showtime.ScreenId)
                .ToListAsync();

            var booked = (await _context.Bookings
                .Where(b => b.ShowtimeId == id && (b.Status == "Hoàn thành" || b.Status == "Paid"))
                .Select(b => b.SeatId)
                .ToListAsync()).ToHashSet();

            var locked = (await _context.SeatLocks
                .Where(l => l.ShowtimeId == id && l.ExpiryTime > DateTime.UtcNow)
                .Select(l => l.SeatId)
                .ToListAsync()).ToHashSet();

            var result = seats
                .GroupBy(s => s.RowNumber)
                .Select(g => new
                {
                    Row = g.Key,
                    Seats = g.Select(s => new
                    {
                        Id = s.SeatId,
                        Code = $"{s.RowNumber}{s.SeatNumber}",
                        Type = s.SeatType,
                        Status =
                            booked.Contains(s.SeatId) ? "booked" :
                            locked.Contains(s.SeatId) ? "locked" :
                            "available"
                    })
                });

            return Ok(result);
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
