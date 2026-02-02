using doantotnghiep_api.Data;
using doantotnghiep_api.Dto_s;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        // PUBLIC APIs (ai cũng xem được)
        // =================================================

        // GET: api/movies
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetMovies()
        {
            var movies = await _context.Movies
                .OrderByDescending(x => x.ReleaseDate)
                .ToListAsync();

            return Ok(movies);
        }

        // GET: api/movies/5
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetMovie(int id)
        {
            var movie = await _context.Movies.FindAsync(id);

            if (movie == null)
                return NotFound("Movie not found");

            return Ok(movie);
        }

        // =================================================
        // ADMIN ONLY APIs
        // =================================================

        // POST: api/movies
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateMovie([FromBody] Movie movie)
        {
            _context.Movies.Add(movie);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetMovie), new { id = movie.MovieId }, movie);
        }

        // PUT: api/movies/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateMovie(int id, [FromBody] Movie movie)
        {
            if (id != movie.MovieId)
                return BadRequest("Id mismatch");

            var exists = await _context.Movies.AnyAsync(x => x.MovieId == id);
            if (!exists)
                return NotFound("Movie not found");

            _context.Entry(movie).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteMovie(int id)
        {
            var movie = await _context.Movies.FindAsync(id);

            if (movie == null)
                return NotFound("Movie not found");

            _context.Movies.Remove(movie);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("upload")]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadPoster([FromForm] UploadPosterDto dto)
        {
            var file = dto.File;

            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            // tạo thư mục uploads nếu chưa có
            var uploadsFolder = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "uploads"
            );

            Directory.CreateDirectory(uploadsFolder);

            // tạo tên file random
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            // save file
            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            // trả url
            var fileUrl = $"{Request.Scheme}://{Request.Host}/uploads/{fileName}";

            return Ok(new
            {
                message = "Upload success",
                url = fileUrl
            });
        }
    }
}
