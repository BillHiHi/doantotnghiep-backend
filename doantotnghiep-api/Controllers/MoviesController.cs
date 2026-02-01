using doantotnghiep_api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Route("api/[controller]")]
[ApiController]
public class MoviesController : ControllerBase
{
    private readonly AppDbContext _context;

    public MoviesController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/movies
    [HttpGet]
    public async Task<IActionResult> GetMovies()
    {
        var movies = await _context.Movies
            .Where(x => x.Status == "NowShowing")
            .OrderByDescending(x => x.ReleaseDate)
            .ToListAsync();

        return Ok(movies);
    }
}
