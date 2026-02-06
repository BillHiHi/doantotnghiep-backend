using doantotnghiep_api.Data;
using doantotnghiep_api.Dto_s;
using doantotnghiep_api.Models;
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
        // PUBLIC APIs
        // =================================================

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetMovies()
        {
            var movies = await _context.Movies
                .OrderByDescending(x => x.ReleaseDate)
                .ToListAsync();

            return Ok(movies);
        }

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

                // ✅ NEW
                AgeRating = dto.AgeRating,
                Language = dto.Language,
                Status = dto.Status,

                ReleaseDate = dto.ReleaseDate
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
                return NotFound("Movie not found");

            movie.Title = dto.Title;
            movie.Description = dto.Description;
            movie.Duration = dto.Duration;
            movie.Genre = dto.Genre;
            movie.PosterUrl = dto.PosterUrl;

            movie.Director = dto.Director;
            movie.Actors = dto.Actors;
            movie.TrailerUrl = dto.TrailerUrl;

            // ✅ NEW
            movie.AgeRating = dto.AgeRating;
            movie.Language = dto.Language;
            movie.Status = dto.Status;

            movie.ReleaseDate = dto.ReleaseDate;

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
                return NotFound("Movie not found");

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
                return BadRequest("No file uploaded");

            var uploadsFolder = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "uploads"
            );

            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            var fileUrl = $"{Request.Scheme}://{Request.Host}/uploads/{fileName}";

            return Ok(new
            {
                message = "Upload success",
                url = fileUrl
            });
        }
    }
}
