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
        [HttpGet("all-theaters")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllTheaters()
        {
            try
            {
                // Lấy danh sách tất cả rạp từ database
                var theaters = await _context.Theaters
                    .Select(t => new {
                        t.TheaterId,
                        t.Name,
                        t.Address,
                        t.City
                    })
                    .ToListAsync();

                if (theaters == null || !theaters.Any())
                {
                    return NotFound("Không tìm thấy rạp nào.");
                }

                return Ok(theaters);
            }
            catch (Exception ex)
            {
                // Trả về lỗi nếu có vấn đề trong quá trình truy vấn
                return StatusCode(500, $"Lỗi server: {ex.Message}");
            }
        }

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
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN,BranchAdmin")]
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
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN,BranchAdmin")]
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
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN,BranchAdmin")]
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
        // Lấy phim theo rạp (Những phim ĐÃ CÓ suất chiếu)
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
        // 🍿 Lấy danh sách phim ĐƯỢC PHÂN PHỐI cho rạp (Để tạo suất chiếu)
        // =========================
        [HttpGet("{theaterId}/available-movies")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAvailableMovies(int theaterId)
        {
            var movies = await _context.TheaterMovies
                .Where(tm => tm.TheaterId == theaterId)
                .Include(tm => tm.Movie)
                .Select(tm => tm.Movie)
                .ToListAsync();

            // CHẾ ĐỘ NGHIÊM KHẮC: Chỉ hiện những phim đã được Admin phân phối.
            // Nếu muốn hiện tất cả nếu trống, hãy uncomment code cũ.
            return Ok(movies);
        }

        // =========================
        // 🔗 PHÂN PHỐI PHIM CHO RẠP (API MỚI)
        // =========================
        [HttpPost("{theaterId}/distribute")]
        [Authorize(Roles = "Admin,SUPER_ADMIN")]
        public async Task<IActionResult> DistributeMovies(int theaterId, [FromBody] List<int> movieIds)
        {
            var theater = await _context.Theaters.FindAsync(theaterId);
            if (theater == null) return NotFound("Rạp không tồn tại");

            // Xóa các liên kết cũ (Nếu muốn ghi đè) hoặc chỉ thêm cái mới
            // Ở đây mình chọn chỉ thêm cái mới để an toàn
            var existingIds = await _context.TheaterMovies
                .Where(tm => tm.TheaterId == theaterId)
                .Select(tm => tm.MovieId)
                .ToListAsync();

            var newMovies = movieIds.Except(existingIds).Select(mId => new TheaterMovie
            {
                TheaterId = theaterId,
                MovieId = mId
            });

            _context.TheaterMovies.AddRange(newMovies);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Đã phân phối {newMovies.Count()} phim mới cho rạp {theater.Name}" });
        }

        // =========================
        // ❌ GỠ PHIM KHỎI RẠP (API MỚI)
        // =========================
        [HttpDelete("{theaterId}/movies/{movieId}")]
        [Authorize(Roles = "Admin,SUPER_ADMIN")]
        public async Task<IActionResult> RemoveMovie(int theaterId, int movieId)
        {
            var link = await _context.TheaterMovies
                .FirstOrDefaultAsync(tm => tm.TheaterId == theaterId && tm.MovieId == movieId);

            if (link == null) return NotFound("Phim không được phân phối ở rạp này.");

            _context.TheaterMovies.Remove(link);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã gỡ phim khỏi rạp thành công" });
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
        // =========================
        // Lấy rạp theo khu vực (Grouped by City)
        // =========================
        [HttpGet("grouped")]
        [AllowAnonymous]
        public async Task<IActionResult> GetTheatersByGroup()
        {
            var data = await _context.Theaters
                .AsNoTracking()
                .GroupBy(t => t.City)
                .Select(g => new {
                    province = g.Key,
                    theaters = g.Select(t => new {
                        id = t.TheaterId,
                        name = t.Name
                    }).ToList()
                })
                .ToListAsync();
            return Ok(data);
        }
    }
}
