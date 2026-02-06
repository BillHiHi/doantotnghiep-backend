using doantotnghiep_api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace doantotnghiep_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TheatersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TheatersController(AppDbContext context)
        {
            _context = context;
        }

        // =========================
        // Lấy tất cả rạp
        // =========================
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetTheaters()
        {
            var theaters = await _context.Theaters.ToListAsync();
            return Ok(theaters);
        }

        // =========================
        // Lấy phim theo rạp
        // =========================
        [HttpGet("{theaterId}/movies")]
        [AllowAnonymous]
        public async Task<IActionResult> GetMoviesByTheater(int theaterId)
        {
            var movies = await _context.Showtimes
                .Where(s => s.Screen.TheaterId == theaterId)
                .Include(s => s.Movie)
                .Select(s => s.Movie)
                .Distinct()
                .ToListAsync();

            return Ok(movies);
        }

        // =========================
        // Lấy showtime theo rạp + phim
        // =========================
        [HttpGet("{theaterId}/movies/{movieId}/showtimes")]
        [AllowAnonymous]
        public async Task<IActionResult> GetShowtimes(int theaterId, int movieId)
        {
            var showtimes = await _context.Showtimes
                .Where(s =>
                    s.Screen.TheaterId == theaterId &&
                    s.MovieId == movieId)
                .Include(s => s.Screen)
                .ToListAsync();

            return Ok(showtimes);
        }

        // =========================
        // Lấy tất cả showtime của rạp
        // =========================
        [HttpGet("{theaterId}/all-showtimes")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllShowtimes(int theaterId)
        {
            var showtimes = await _context.Showtimes
                .AsNoTracking()
                .Where(s => s.Screen.TheaterId == theaterId)
                .Include(s => s.Movie)
                .OrderBy(s => s.StartTime)
                .Select(s => new 
                {
                    s.ShowtimeId,
                    s.MovieId,
                    s.Movie,
                    s.ScreenId,
                    s.StartTime,
                    s.EndTime,
                    s.BasePrice,
                    AvailableSeats = _context.Seats.Count(st => st.ScreenId == s.ScreenId) -
                                     _context.Bookings.Count(b => b.ShowtimeId == s.ShowtimeId && b.Status == "Hoàn thành")
                })
                .ToListAsync();

            return Ok(showtimes);
        }
    }
}
