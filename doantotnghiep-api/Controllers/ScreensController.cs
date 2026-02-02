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

        // =========================================
        // GET ALL
        // api/screens
        // =========================================
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetScreens()
        {
            var screens = await _context.Screens
                .OrderBy(x => x.ScreenName)
                .ToListAsync();

            return Ok(screens);
        }

        // =========================================
        // GET BY ID
        // api/screens/5
        // =========================================
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetScreen(int id)
        {
            var screen = await _context.Screens.FindAsync(id);

            if (screen == null)
                return NotFound("Screen not found");

            return Ok(screen);
        }

        // =========================================
        // CREATE
        // api/screens
        // =========================================
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateScreen([FromBody] Screen screen)
        {
            _context.Screens.Add(screen);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetScreen), new { id = screen.ScreenId }, screen);
        }

        // =========================================
        // UPDATE
        // api/screens/5
        // =========================================
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateScreen(int id, [FromBody] Screen screen)
        {
            if (id != screen.ScreenId)
                return BadRequest("Id mismatch");

            var exists = await _context.Screens.AnyAsync(x => x.ScreenId == id);
            if (!exists)
                return NotFound("Screen not found");

            _context.Entry(screen).State = EntityState.Modified;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // =========================================
        // DELETE
        // api/screens/5
        // =========================================
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
    }
}
