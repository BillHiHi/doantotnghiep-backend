using doantotnghiep_api.Data;
using doantotnghiep_api.Dto_s;
using doantotnghiep_api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace doantotnghiep_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MoviesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MoviesController(AppDbContext context)
        {
            _context = context;
        }

        // =================================================
        // PUBLIC APIs
        // =================================================

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetMovies([FromQuery] bool activeOnly = false)
        {
            // 💡 TỐI ƯU 1: Thêm AsNoTracking() giúp truy vấn nhanh hơn, giảm tải bộ nhớ vì chỉ đọc dữ liệu
            var query = _context.Movies.AsNoTracking().AsQueryable();

            // 💡 TỐI ƯU 2: Thêm cờ activeOnly để Client có thể lọc nhanh các phim CÒN CHIẾU
            if (activeOnly)
            {
                var now = DateTime.Now;
                query = query.Where(m => m.ReleaseDate <= now && (!m.EndDate.HasValue || m.EndDate.Value >= now));
            }

            var movies = await query
                .OrderByDescending(x => x.ReleaseDate)
                .ToListAsync();

            return Ok(movies);
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetMovie(int id)
        {
            // Dùng AsNoTracking cho API chỉ đọc (GET)
            var movie = await _context.Movies.AsNoTracking().FirstOrDefaultAsync(m => m.MovieId == id);

            if (movie == null)
                return NotFound(new { message = "Movie not found" });

            return Ok(movie);
        }

        // =================================================
        // ADMIN ONLY
        // =================================================

        // CREATE
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateMovie([FromBody] CreateMovieDto dto)
        {
            var movie = new Movie
            {
                Title = dto.Title,
                Description = dto.Description,
                Duration = dto.Duration,
                Genre = dto.Genre,
                PosterUrl = dto.PosterUrl,
                Director = dto.Director,
                Actors = dto.Actors,
                TrailerUrl = dto.TrailerUrl,
                AgeRating = dto.AgeRating,
                Language = dto.Language,
                ReleaseDate = dto.ReleaseDate,

                // ✅ THÊM MỚI
                EndDate = dto.EndDate,

                // 💡 TỐI ƯU 3: Tự động tính toán trạng thái thay vì tin tưởng hoàn toàn vào dữ liệu Client gửi lên
                Status = DetermineStatus(dto.ReleaseDate, dto.EndDate, dto.Status)
            };

            _context.Movies.Add(movie);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetMovie), new { id = movie.MovieId }, movie);
        }

        // UPDATE
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateMovie(int id, [FromBody] UpdateMovieDto dto)
        {
            var movie = await _context.Movies.FindAsync(id);

            if (movie == null)
                return NotFound(new { message = "Movie not found" });

            movie.Title = dto.Title;
            movie.Description = dto.Description;
            movie.Duration = dto.Duration;
            movie.Genre = dto.Genre;
            movie.PosterUrl = dto.PosterUrl;
            movie.Director = dto.Director;
            movie.Actors = dto.Actors;
            movie.TrailerUrl = dto.TrailerUrl;
            movie.AgeRating = dto.AgeRating;
            movie.Language = dto.Language;
            movie.ReleaseDate = dto.ReleaseDate;

            // ✅ THÊM MỚI
            movie.EndDate = dto.EndDate;

            // Cập nhật lại trạng thái dựa trên ngày tháng mới
            movie.Status = DetermineStatus(dto.ReleaseDate, dto.EndDate, dto.Status);

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteMovie(int id)
        {
            var movie = await _context.Movies.FindAsync(id);

            if (movie == null)
                return NotFound(new { message = "Movie not found" });

            _context.Movies.Remove(movie);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // =================================================
        // UPLOAD POSTER
        // =================================================

        [HttpPost("upload")]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadPoster([FromForm] UploadPosterDto dto)
        {
            var file = dto.File;

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded" });

            // Cân nhắc kiểm tra định dạng file (chỉ cho phép .jpg, .png) và dung lượng file tại đây

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            var fileUrl = $"{Request.Scheme}://{Request.Host}/uploads/{fileName}";

            return Ok(new { message = "Upload success", url = fileUrl });
        }

        // =================================================
        // HELPER METHODS
        // =================================================

        private string DetermineStatus(DateTime releaseDate, DateTime? endDate, string defaultStatus)
        {
            var now = DateTime.Now;

            // Nếu admin muốn chủ động set "Hidden" hay "Cancelled" thì giữ nguyên
            if (defaultStatus == "Hidden" || defaultStatus == "Cancelled")
                return defaultStatus;

            if (now < releaseDate)
                return "ComingSoon"; // Sắp chiếu

            if (endDate.HasValue && now > endDate.Value)
                return "Ended"; // Đã kết thúc

            return "NowShowing"; // Đang chiếu
        }
    }
}