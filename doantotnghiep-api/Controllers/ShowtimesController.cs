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
        public async Task<IActionResult> GetAll()
        {
            var showtimes = await _context.Showtimes
                .AsNoTracking()
                .OrderByDescending(s => s.StartTime)
                .Select(s => new ShowtimeDto
                {
                    ShowtimeId = s.ShowtimeId,
                    MovieId = s.MovieId,
                    MovieTitle = s.Movie.Title,
                    ScreenId = s.ScreenId,
                    ScreenName = s.Screen.ScreenName, // 👈 nhớ dùng Name nếu model là Name
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    BasePrice = s.BasePrice
                })
                .ToListAsync();

            return Ok(showtimes);
        }


        // =====================================================
        // GET DETAIL
        // =====================================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetDetail(int id)
        {
            var showtime = await _context.Showtimes
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
                    BasePrice = s.BasePrice
                })
                .FirstOrDefaultAsync();

            if (showtime == null)
                return NotFound();

            return Ok(showtime);
        }


        // =====================================================
        // GET BY MOVIE (lịch chiếu nhẹ)
        // =====================================================
        [HttpGet("movie/{movieId}")]
        public async Task<IActionResult> GetByMovie(int movieId)
        {
            var showtimes = await _context.Showtimes
                .AsNoTracking()
                .Where(s => s.MovieId == movieId)
                .OrderBy(s => s.StartTime)
                .Select(s => new ShowtimeSimpleDto
                {
                    ShowtimeId = s.ShowtimeId,
                    Time = s.StartTime.ToString("HH:mm"),
                    StartTime = s.StartTime,
                    BasePrice = s.BasePrice
                })
                .ToListAsync();

            return Ok(showtimes);
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

            return CreatedAtAction(nameof(GetDetail),
                new { id = showtime.ShowtimeId },
                showtime.ShowtimeId);
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
        // GET SEATS (Seat Map for SeatMap.vue)
        // =====================================================
        [HttpGet("{id}/seats")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSeats(int id)
        {
            // 1. Check showtime
            var showtime = await _context.Showtimes
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ShowtimeId == id);

            if (showtime == null)
                return NotFound("Showtime không tồn tại");

            // 2. Get seats for this screen
            var seats = await _context.Seats
                .AsNoTracking()
                .Where(s => s.ScreenId == showtime.ScreenId)
                .OrderBy(s => s.RowNumber)
                .ThenBy(s => s.SeatNumber)
                .ToListAsync();

            // 3. Get booked/locked seats
            var bookedSeatIds = await _context.Bookings
                .AsNoTracking()
                .Where(b => b.ShowtimeId == id && b.Status == "Hoàn thành")
                .Select(b => b.SeatId)
                .ToListAsync();

            var lockedSeatIds = await _context.SeatLocks
                .AsNoTracking()
                .Where(l => l.ShowtimeId == id && l.ExpiryTime > DateTime.UtcNow)
                .Select(l => l.SeatId)
                .ToListAsync();

            var bookedSet = bookedSeatIds.ToHashSet();
            var lockedSet = lockedSeatIds.ToHashSet();

            // 4. Build seat map
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
                        Status = bookedSet.Contains(s.SeatId) ? "booked" :
                                 lockedSet.Contains(s.SeatId) ? "locked" :
                                 "available"
                    })
                });

            return Ok(result);
        }
    }
}
