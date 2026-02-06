using doantotnghiep_api.Data;
using doantotnghiep_api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace doantotnghiep_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScreensController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ScreensController(AppDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // GET ALL + FILTER BY THEATER
        // api/screens
        // api/screens?theaterId=1
        // =====================================================
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetScreens([FromQuery] int? theaterId)
        {
            var query = _context.Screens
                .Include(s => s.Theater) // ⭐ JOIN theater
                .AsQueryable();

            if (theaterId.HasValue)
                query = query.Where(x => x.TheaterId == theaterId);

            var screens = await query
                .OrderBy(x => x.ScreenName)
                .Select(s => new
                {
                    s.ScreenId,
                    s.ScreenName,
                    s.ScreenType,
                    s.TheaterId,
                    TheaterName = s.Theater.Name
                })
                .ToListAsync();

            return Ok(screens);
        }

        // =====================================================
        // GET BY ID
        // api/screens/5
        // =====================================================
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetScreen(int id)
        {
            var screen = await _context.Screens
                .Include(s => s.Theater)
                .Where(s => s.ScreenId == id)
                .Select(s => new
                {
                    s.ScreenId,
                    s.ScreenName,
                    s.ScreenType,
                    s.TheaterId,
                    TheaterName = s.Theater.Name
                })
                .FirstOrDefaultAsync();

            if (screen == null)
                return NotFound("Screen not found");

            return Ok(screen);
        }

        // =====================================================
        // CREATE
        // api/screens
        // =====================================================
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateScreen([FromBody] Screen screen)
        {
            var theaterExists = await _context.Theaters
                .AnyAsync(t => t.TheaterId == screen.TheaterId);

            if (!theaterExists)
                return BadRequest("Theater không tồn tại");

            _context.Screens.Add(screen);
            await _context.SaveChangesAsync();

            return Ok(screen);
        }

        // =====================================================
        // UPDATE
        // api/screens/5
        // =====================================================
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateScreen(int id, [FromBody] Screen updatedScreen)
        {
            var screen = await _context.Screens.FindAsync(id);

            if (screen == null)
                return NotFound("Screen not found");

            screen.ScreenName = updatedScreen.ScreenName;
            screen.ScreenType = updatedScreen.ScreenType;
            screen.TheaterId = updatedScreen.TheaterId;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // =====================================================
        // DELETE
        // api/screens/5
        // =====================================================
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteScreen(int id)
        {
            var screen = await _context.Screens.FindAsync(id);

            if (screen == null)
                return NotFound("Screen not found");

            _context.Screens.Remove(screen);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        // =====================================================
        // SEED SEATS (QUICK CREATE 80 SEATS)
        // =====================================================
        [HttpPost("{id}/seed-seats")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SeedSeats(int id)
        {
            var screen = await _context.Screens.FindAsync(id);
            if (screen == null) return NotFound("Screen not found");

            // Check if seats already exist
            if (await _context.Seats.AnyAsync(s => s.ScreenId == id))
                return BadRequest("Phòng đã có ghế, không thể seed thêm.");

            var rows = new[] { "A", "B", "C", "D", "E", "F", "G", "H" };
            var seatsToCreate = new List<Seat>();

            foreach (var rowName in rows)
            {
                for (int i = 1; i <= 10; i++)
                {
                    seatsToCreate.Add(new Seat
                    {
                        ScreenId = id,
                        RowNumber = rowName,
                        SeatNumber = i,
                        SeatType = (rowName == "F" || rowName == "G" || rowName == "H") ? "VIP" : "Normal",

                    });
                }
            }

            _context.Seats.AddRange(seatsToCreate);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Đã tạo 80 ghế cho phòng {screen.ScreenName}" });
        }
    }
}
