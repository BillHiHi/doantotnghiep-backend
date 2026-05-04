using doantotnghiep_api.Data;
using doantotnghiep_api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace doantotnghiep_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,SUPER_ADMIN,BRANCH_ADMIN,BranchAdmin")]
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
                .ThenBy(s => s.SeatId) // Sắp xếp theo ID vật lý
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
                .ThenBy(s => s.SeatId) // Sắp xếp theo ID vật lý
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
        // ✅ GENERATE CURVED SEATS (Cánh quạt)
        // ===============================
        [HttpPost("generate-curved")]
        public async Task<IActionResult> GenerateCurvedSeats([FromBody] GenerateCurvedSeatsDto dto)
        {
            var screen = await _context.Screens.FindAsync(dto.ScreenId);
            if (screen == null) return NotFound("Screen not found");

            // Tự động xóa ghế cũ trước khi tạo
            var existing = await _context.Seats.Where(s => s.ScreenId == dto.ScreenId).ToListAsync();
            if (existing.Any()) {
                _context.Seats.RemoveRange(existing);
                await _context.SaveChangesAsync();
            }

            var newSeats = new List<Seat>();
            char startRow = 'A';
            
            // Cấu hình số ghế cho từng hàng tạo hình cánh quạt (Bầu dục)
            int[] rowWidths = new int[] { 10, 12, 14, 16, 16, 16, 14, 12 };

            for (int i = 0; i < rowWidths.Length; i++)
            {
                string rowName = ((char)(startRow + i)).ToString();
                int numCols = rowWidths[i];

                for (int j = 1; j <= numCols; j++)
                {
                    // Gán ghế VIP cho vùng trung tâm
                    bool isVip = false;
                    if (i >= 2 && i <= 5) // Hàng C, D, E, F
                    {
                        int mid = numCols / 2;
                        if (j > mid - 3 && j <= mid + 2) isVip = true; // 4 ghế giữa
                    }

                    newSeats.Add(new Seat
                    {
                        ScreenId = dto.ScreenId,
                        RowNumber = rowName,
                        SeatNumber = j,
                        SeatType = isVip ? "VIP" : "Standard",
                        IsHidden = false
                    });
                }
            }

            _context.Seats.AddRange(newSeats);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Đã tạo sơ đồ cánh quạt ({newSeats.Count} ghế) cho phòng {screen.ScreenName}" });
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
            seat.IsHidden = dto.IsHidden;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // ===============================
        // ✅ TOGGLE HIDE SEAT
        // ===============================
        [HttpPut("{id}/toggle-hide")]
        public async Task<IActionResult> ToggleHideSeat(int id)
        {
            var seat = await _context.Seats.FindAsync(id);
            if (seat == null) return NotFound();

            seat.IsHidden = !seat.IsHidden;
            await _context.SaveChangesAsync();
            
            // Recalculate SeatNumber
            await RecalculateRowSeatNumbers(seat.ScreenId, seat.RowNumber);

            return Ok(seat);
        }

        // ===============================
        // ✅ DELETE
        // ===============================
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSeat(int id)
        {
            var seat = await _context.Seats.FindAsync(id);
            if (seat == null) return NotFound();

            var screenId = seat.ScreenId;
            var rowNum = seat.RowNumber;

            _context.Seats.Remove(seat);
            await _context.SaveChangesAsync();
            
            // Recalculate SeatNumber
            await RecalculateRowSeatNumbers(screenId, rowNum);

            return NoContent();
        }

        private async Task RecalculateRowSeatNumbers(int screenId, string rowNumber)
        {
            var seatsInRow = await _context.Seats
                .Where(s => s.ScreenId == screenId && s.RowNumber == rowNumber)
                .OrderBy(s => s.SeatId) // Giữ thứ tự vật lý
                .ToListAsync();

            int visibleCounter = 1;
            foreach(var s in seatsInRow)
            {
                if (s.IsHidden)
                {
                    s.SeatNumber = 0; // Đánh dấu là 0 cho ghế ẩn
                }
                else
                {
                    s.SeatNumber = visibleCounter++;
                }
            }
            await _context.SaveChangesAsync();
        }

        // ===============================
        // ✅ DELETE ALL BY SCREEN
        // ===============================
        [HttpDelete("delete-all-in-screen/{screenId}")]
        public async Task<IActionResult> DeleteAllByScreen(int screenId)
        {
            var seats = await _context.Seats.Where(s => s.ScreenId == screenId).ToListAsync();
            if (seats.Count == 0) return Ok(new { message = "Không có ghế nào để xóa." });

            _context.Seats.RemoveRange(seats);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Đã xóa thành công {seats.Count} ghế của phòng này." });
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

    public class GenerateCurvedSeatsDto
    {
        public int ScreenId { get; set; }
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
        public bool IsHidden { get; set; } = false;
    }
}
