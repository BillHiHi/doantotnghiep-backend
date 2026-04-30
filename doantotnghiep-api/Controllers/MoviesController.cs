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
        public async Task<IActionResult> GetMovies(
            [FromQuery] bool activeOnly = false,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10) // 💡 TỐI ƯU 1: Thêm phân trang mặc định
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0 || pageSize > 100) pageSize = 10;
            var query = _context.Movies.AsNoTracking().AsQueryable();

            // 💡 GIẢM BỚT ĐIỀU KIỆN LỌC: 
            // Cho phép phim "Sắp chiếu" hiện lên nếu được phân phối cho rạp này
            query = query.Where(m => m.Status != "Hidden" && m.Status != "Cancelled");

            // Đếm tổng số lượng record trước khi phân trang để frontend làm giao diện
            var totalRecords = await query.CountAsync();

            var movies = await query
                .OrderByDescending(x => x.ReleaseDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 💡 TỐI ƯU 2: Cập nhật lại status theo thời gian thực (vì thời gian đã trôi qua kể từ lúc lưu DB)
            foreach (var m in movies)
            {
                m.Status = DetermineStatus(m.ReleaseDate, m.EndDate, m.Status);
            }

            return Ok(new
            {
                TotalRecords = totalRecords,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize),
                Data = movies
            });
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetMovie(int id)
        {
            var movie = await _context.Movies.AsNoTracking().FirstOrDefaultAsync(m => m.MovieId == id);

            if (movie == null)
                return NotFound(new { message = "Movie not found" });

            // Cập nhật status thời gian thực
            movie.Status = DetermineStatus(movie.ReleaseDate, movie.EndDate, movie.Status);

            return Ok(movie);
        }

        // =================================================
        // MOVIES BY THEATER APIs
        // =================================================

        [HttpGet("by-theater/{theaterId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetMoviesByTheater(int theaterId, [FromQuery] bool activeOnly = false)
        {
            var query = _context.Movies
                .Where(m => m.TheaterMovies.Any(tm => tm.TheaterId == theaterId))
                .AsNoTracking();

            // 💡 GIẢM BỚT ĐIỀU KIỆN LỌC: 
            // Cho phép phim "Sắp chiếu" hiện lên nếu được phân phối cho rạp này
            query = query.Where(m => m.Status != "Hidden" && m.Status != "Cancelled");

            var movies = await query.OrderByDescending(x => x.ReleaseDate).ToListAsync();

            foreach (var m in movies)
            {
                m.Status = DetermineStatus(m.ReleaseDate, m.EndDate, m.Status);
            }

            return Ok(movies);
        }

        [HttpPost("assign-to-theater")]
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN,BranchAdmin")]
        public async Task<IActionResult> AssignMovieToTheater([FromBody] AssignMovieTheaterDto dto)
        {
            var movieExists = await _context.Movies.AnyAsync(m => m.MovieId == dto.MovieID);
            var theaterExists = await _context.Theaters.AnyAsync(t => t.TheaterId == dto.TheaterID);

            if (!movieExists || !theaterExists)
                return NotFound(new { message = "Movie or Theater not found." });

            var isAlreadyAssigned = await _context.TheaterMovies
                .AnyAsync(tm => tm.MovieId == dto.MovieID && tm.TheaterId == dto.TheaterID);

            if (isAlreadyAssigned)
                return BadRequest(new { message = "Phim này đã được gán cho rạp này từ trước." });

            var movie = await _context.Movies.FindAsync(dto.MovieID);
            if (movie.Status == "Cancelled" || movie.Status == "Ended")
                return BadRequest(new { message = "Không thể gán phim đã kết thúc hoặc bị hủy vào rạp." });

            var theaterMovie = new TheaterMovie
            {
                MovieId = dto.MovieID,
                TheaterId = dto.TheaterID
            };

            _context.TheaterMovies.Add(theaterMovie);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Gán phim vào rạp thành công." });
        }

        [HttpDelete("remove-from-theater/{theaterId}/{movieId}")]
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN,BranchAdmin")]
        public async Task<IActionResult> RemoveMovieFromTheater(int theaterId, int movieId)
        {
            var theaterMovie = await _context.TheaterMovies
                .FirstOrDefaultAsync(tm => tm.TheaterId == theaterId && tm.MovieId == movieId);

            if (theaterMovie == null)
                return NotFound(new { message = "Không tìm thấy dữ liệu gán phim cho rạp này." });

            _context.TheaterMovies.Remove(theaterMovie);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã xóa phim khỏi rạp thành công." });
        }

        // =================================================
        // ADMIN ONLY
        // =================================================

        [HttpPost]
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN,BranchAdmin")]
        public async Task<IActionResult> CreateMovie([FromBody] CreateMovieDto dto)
        {
            // TỐI ƯU: Nên kiểm tra logic ngày tháng
            if (dto.EndDate.HasValue && dto.EndDate.Value <= dto.ReleaseDate)
                return BadRequest(new { message = "Ngày kết thúc phải lớn hơn ngày khởi chiếu." });
            var existingMovie = await _context.Movies
            .FirstOrDefaultAsync(m => m.Title.ToLower() == dto.Title.ToLower());

            if (existingMovie != null)
                return BadRequest(new { message = "Phim với tiêu đề này đã tồn tại trong hệ thống." });

            // ❌ Thiếu 1: Không kiểm tra ReleaseDate có hợp lệ không
            // Ví dụ: ReleaseDate = DateTime.MinValue hoặc quá xa tương lai
            if (dto.ReleaseDate == default)
                return BadRequest(new { message = "Ngày khởi chiếu không hợp lệ." });

            // ❌ Thiếu 2: Duration không được kiểm tra
            // Duration = 0, âm, hoặc phi thực tế (ví dụ 9999 phút)
            if (dto.Duration <= 0 || dto.Duration > 600)
                return BadRequest(new { message = "Thời lượng phim không hợp lệ (1 - 600 phút)." });

            // ❌ Thiếu 3: Title rỗng hoặc quá dài không bị chặn ở tầng business
            // (Chỉ dựa vào [Required] của DTO là chưa đủ nếu DTO không có annotation)
            if (string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest(new { message = "Tiêu đề phim không được để trống." });

            // ❌ Thiếu 4: PosterUrl không validate định dạng URL
            // Có thể lưu chuỗi rác vào DB
            if (!string.IsNullOrEmpty(dto.PosterUrl) && !Uri.IsWellFormedUriString(dto.PosterUrl, UriKind.Absolute))
                return BadRequest(new { message = "PosterUrl không hợp lệ." });

            // ❌ Thiếu 5: TrailerUrl tương tự
            if (!string.IsNullOrEmpty(dto.TrailerUrl) && !Uri.IsWellFormedUriString(dto.TrailerUrl, UriKind.Absolute))
                return BadRequest(new { message = "TrailerUrl không hợp lệ." });

            // ❌ Thiếu 6: Status đầu vào không được whitelist
            // Người dùng có thể truyền bất kỳ chuỗi nào, DetermineStatus chỉ xử lý "Hidden"/"Cancelled"
            // nhưng không chặn giá trị rác
            var allowedStatuses = new[] { "Hidden", "Cancelled", "ComingSoon", "NowShowing", "Ended" };
            if (!string.IsNullOrEmpty(dto.Status) && !allowedStatuses.Contains(dto.Status))
                return BadRequest(new { message = "Trạng thái phim không hợp lệ." });
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
                EndDate = dto.EndDate,
                Status = DetermineStatus(dto.ReleaseDate, dto.EndDate, dto.Status)
            };

            _context.Movies.Add(movie);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetMovie), new { id = movie.MovieId }, movie);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN,BranchAdmin")]
        public async Task<IActionResult> UpdateMovie(int id, [FromBody] UpdateMovieDto dto)
        {
            var movie = await _context.Movies.FindAsync(id);
            if (movie == null)
                return NotFound(new { message = "Không tìm thấy phim" });

            if (dto.EndDate.HasValue && dto.EndDate.Value <= dto.ReleaseDate)
                return BadRequest(new { message = "Ngày kết thúc phải lớn hơn ngày khởi chiếu." });

            var hasActiveShowtimes = await _context.Showtimes
                .AnyAsync(s => s.MovieId == id && s.StartTime > DateTime.Now);
            if (hasActiveShowtimes && dto.EndDate.HasValue && dto.EndDate.Value < DateTime.Now)
                return BadRequest(new { message = "Không thể đặt ngày kết thúc trong quá khứ khi phim còn lịch chiếu." });

            // ❌ Thiếu 1: Không kiểm tra ReleaseDate có hợp lệ không
            // Ví dụ: ReleaseDate = DateTime.MinValue hoặc quá xa tương lai
            if (dto.ReleaseDate == default)
                return BadRequest(new { message = "Ngày khởi chiếu không hợp lệ." });

            // ❌ Thiếu 2: Duration không được kiểm tra
            // Duration = 0, âm, hoặc phi thực tế (ví dụ 9999 phút)
            if (dto.Duration <= 0 || dto.Duration > 600)
                return BadRequest(new { message = "Thời lượng phim không hợp lệ (1 - 600 phút)." });

            // ❌ Thiếu 3: Title rỗng hoặc quá dài không bị chặn ở tầng business
            // (Chỉ dựa vào [Required] của DTO là chưa đủ nếu DTO không có annotation)
            if (string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest(new { message = "Tiêu đề phim không được để trống." });

            // ❌ Thiếu 4: PosterUrl không validate định dạng URL
            // Có thể lưu chuỗi rác vào DB
            if (!string.IsNullOrEmpty(dto.PosterUrl) && !Uri.IsWellFormedUriString(dto.PosterUrl, UriKind.Absolute))
                return BadRequest(new { message = "PosterUrl không hợp lệ." });

            // ❌ Thiếu 5: TrailerUrl tương tự
            if (!string.IsNullOrEmpty(dto.TrailerUrl) && !Uri.IsWellFormedUriString(dto.TrailerUrl, UriKind.Absolute))
                return BadRequest(new { message = "TrailerUrl không hợp lệ." });

            // ❌ Thiếu 6: Status đầu vào không được whitelist
            // Người dùng có thể truyền bất kỳ chuỗi nào, DetermineStatus chỉ xử lý "Hidden"/"Cancelled"
            // nhưng không chặn giá trị rác
            var allowedStatuses = new[] { "Hidden", "Cancelled", "ComingSoon", "NowShowing", "Ended" };
            if (!string.IsNullOrEmpty(dto.Status) && !allowedStatuses.Contains(dto.Status))
                return BadRequest(new { message = "Trạng thái phim không hợp lệ." });

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
            movie.EndDate = dto.EndDate;
            movie.Status = DetermineStatus(dto.ReleaseDate, dto.EndDate, dto.Status);

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN,BranchAdmin")]
        public async Task<IActionResult> DeleteMovie(int id)
        {
            var movie = await _context.Movies.FindAsync(id);
            if (movie == null)
                return NotFound(new { message = "Không tìm thấy phim" });

            var hasFutureShowtimes = await _context.Showtimes
                .AnyAsync(s => s.MovieId == id && s.StartTime > DateTime.Now);
            if (hasFutureShowtimes)
                return BadRequest(new { message = "Không thể xóa phim đang có lịch chiếu. Hãy hủy lịch trước." });

            var hasBookings = await _context.Bookings
                .AnyAsync(b => b.Showtime.MovieId == id);
            if (hasBookings)
                return BadRequest(new { message = "Không thể xóa phim đã có lịch sử đặt vé. Hãy ẩn phim thay vì xóa." });

            _context.Movies.Remove(movie);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // =================================================
        // UPLOAD POSTER
        // =================================================

        [HttpPost("upload")]
        [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN,BranchAdmin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadPoster([FromForm] UploadPosterDto dto)
        {
            var file = dto.File;

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded" });

            // 💡 TỐI ƯU 3: Kiểm tra dung lượng (Ví dụ: Max 5MB)
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(new { message = "Kích thước file không được vượt quá 5MB." });

            // 💡 TỐI ƯU 4: Kiểm tra định dạng file an toàn
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
                return BadRequest(new { message = "Chỉ chấp nhận các định dạng ảnh: .jpg, .jpeg, .png, .webp" });

            try
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var fileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                await using var stream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(stream);

                var fileUrl = $"{Request.Scheme}://{Request.Host}/uploads/{fileName}";

                return Ok(new { message = "Upload success", url = fileUrl });
            }
            catch (Exception ex)
            {
                // Ghi log lỗi ex.Message tại đây nếu có thư viện Logger
                return StatusCode(500, new { message = "Có lỗi xảy ra khi lưu file trên server." });
            }
        }

        // =================================================
        // HELPER METHODS
        // =================================================

        private string DetermineStatus(DateTime releaseDate, DateTime? endDate, string defaultStatus)
        {
            var now = DateTime.Now;

            if (defaultStatus == "Hidden" || defaultStatus == "Cancelled")
                return defaultStatus;

            if (now < releaseDate)
                return "ComingSoon";

            //if (endDate.HasValue && now > endDate.Value)//
                //return "Ended";//

            return "NowShowing";
        }
    }
}