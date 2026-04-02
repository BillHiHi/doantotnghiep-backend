using doantotnghiep_api.Data;
using doantotnghiep_api.Models;
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
        [ResponseCache(Duration = 300)] // Cache 5 phút cho tất cả
        public async Task<IActionResult> GetTheaters([FromQuery] string? city = null)
        {
            var query = _context.Theaters.AsNoTracking();

            if (!string.IsNullOrEmpty(city))
            {
                query = query.Where(t => t.City == city);
            }

            var theaters = await query.ToListAsync();
            return Ok(theaters);
        }

        // =========================
        // Lấy danh sách thành phố (API MỚI)
        // =========================
        [HttpGet("cities")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCities()
        {
            var cities = await _context.Theaters
                .Where(t => !string.IsNullOrEmpty(t.City)) // Loại bỏ những rạp lỡ quên chưa nhập thành phố
                .Select(t => t.City)
                .Distinct()
                .ToListAsync();

            return Ok(cities);
        }

        // =========================
        // Thêm rạp mới
        // =========================
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateTheater([FromBody] Theater theater)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            _context.Theaters.Add(theater);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTheaters), new { id = theater.TheaterId }, theater);
        }

        // =========================
        // Cập nhật rạp
        // =========================
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateTheater(int id, [FromBody] Theater updatedTheater)
        {
            if (id != updatedTheater.TheaterId)
                return BadRequest("Id không khớp");

            var theater = await _context.Theaters.FindAsync(id);

            if (theater == null)
                return NotFound("Không tìm thấy rạp");

            theater.Name = updatedTheater.Name;
            theater.Address = updatedTheater.Address;
            theater.City = updatedTheater.City;

            await _context.SaveChangesAsync();

            return Ok(theater);
        }

        // =========================
        // Xóa rạp
        // =========================
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteTheater(int id)
        {
            var theater = await _context.Theaters.FindAsync(id);
            if (theater == null)
                return NotFound("Không tìm thấy rạp");

            _context.Theaters.Remove(theater);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Xóa rạp thành công" });
        }

        // =========================
        // Lấy phim theo rạp
        // =========================
        [HttpGet("{theaterId}/movies")]
        [AllowAnonymous]
        public async Task<IActionResult> GetMoviesByTheater(int theaterId)
        {
            var movies = await _context.Showtimes
                // Thêm s.StartTime >= DateTime.Now để không hiển thị phim của ngày hôm qua
                .Where(s => s.Screen.TheaterId == theaterId && s.StartTime >= DateTime.Now)
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
