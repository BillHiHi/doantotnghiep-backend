using doantotnghiep_api.Data;
using doantotnghiep_api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace doantotnghiep_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class SeatsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SeatsController(AppDbContext context)
        {
            _context = context;
        }

        // ===============================
        // ✅ GET ALL (ADMIN PAGE)
        // ===============================
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var seats = await _context.Seats
                .Include(s => s.Screen)
                .OrderBy(s => s.ScreenId)
                .ThenBy(s => s.RowNumber)
                .ThenBy(s => s.SeatNumber)
                .ToListAsync();

            return Ok(seats);
        }

        // ===============================
        // ✅ GET BY SCREEN (PUBLIC)
        // ===============================
        [HttpGet("screen/{screenId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByScreen(int screenId)
        {
            var seats = await _context.Seats
                .Where(s => s.ScreenId == screenId)
                .OrderBy(s => s.RowNumber)
                .ThenBy(s => s.SeatNumber)
                .ToListAsync();

            return Ok(seats);
        }

        // ===============================
        // ✅ ADD SINGLE SEAT
        // ===============================
        [HttpPost]
        public async Task<IActionResult> AddSeat([FromBody] CreateSeatDto dto)
        {
            var seat = new Seat
            {
                ScreenId = dto.ScreenId,
                RowNumber = dto.RowNumber,
                SeatNumber = dto.SeatNumber,
                SeatType = dto.SeatType
            };

            _context.Seats.Add(seat);
            await _context.SaveChangesAsync();

            return Ok(seat);
        }

        // ===============================
        // ✅ GENERATE MULTIPLE SEATS
        // ===============================
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateSeats([FromBody] GenerateSeatsDto dto)
        {
            var screen = await _context.Screens.FindAsync(dto.ScreenId);
            if (screen == null)
                return NotFound("Screen not found");

            var newSeats = new List<Seat>();
            char startRow = 'A';

            for (int i = 0; i < dto.NumRows; i++)
            {
                string rowName = ((char)(startRow + i)).ToString();

                for (int j = 1; j <= dto.NumCols; j++)
                {
                    newSeats.Add(new Seat
                    {
                        ScreenId = dto.ScreenId,
                        RowNumber = rowName,
                        SeatNumber = j,
                        SeatType = "Standard"
                    });
                }
            }

            _context.Seats.AddRange(newSeats);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = $"Đã tạo {newSeats.Count} ghế cho phòng {screen.ScreenName}"
            });
        }

        // ===============================
        // ✅ UPDATE
        // ===============================
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSeat(int id, [FromBody] UpdateSeatDto dto)
        {
            var seat = await _context.Seats.FindAsync(id);
            if (seat == null) return NotFound();

            seat.RowNumber = dto.RowNumber;
            seat.SeatNumber = dto.SeatNumber;
            seat.SeatType = dto.SeatType;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // ===============================
        // ✅ DELETE
        // ===============================
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSeat(int id)
        {
            var seat = await _context.Seats.FindAsync(id);
            if (seat == null) return NotFound();

            _context.Seats.Remove(seat);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }

    // =====================================
    // DTOs
    // =====================================

    public class GenerateSeatsDto
    {
        public int ScreenId { get; set; }
        public int NumRows { get; set; }
        public int NumCols { get; set; }
    }

    public class CreateSeatDto
    {
        public int ScreenId { get; set; }
        public string RowNumber { get; set; } = string.Empty;
        public int SeatNumber { get; set; }
        public string SeatType { get; set; } = "Standard";
    }

    public class UpdateSeatDto
    {
        public string RowNumber { get; set; } = string.Empty;
        public int SeatNumber { get; set; }
        public string SeatType { get; set; } = "Standard";
    }
}
