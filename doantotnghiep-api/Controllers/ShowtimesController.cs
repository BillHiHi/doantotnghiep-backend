using doantotnghiep_api.Data;
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
        // 1️⃣ GET: api/showtimes/movie/1
        // Lấy tất cả suất chiếu của 1 phim
        // Vue lichchieu.vue dùng API này
        // =====================================================
        [HttpGet("movie/{movieId}")]
        public async Task<IActionResult> GetByMovie(int movieId)
        {
            var showtimes = await _context.Showtimes
                .AsNoTracking()
                .Where(s => s.MovieId == movieId)
                .OrderBy(s => s.StartTime)
                .Select(s => new
                {
                    s.ShowtimeId,
                    Time = s.StartTime.ToString("HH:mm"),
                    s.StartTime,
                    s.EndTime,
                    s.BasePrice
                })
                .ToListAsync();

            return Ok(showtimes);
        }



        // =====================================================
        // 2️⃣ GET: api/showtimes/6
        // Lấy chi tiết 1 showtime
        // =====================================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetDetail(int id)
        {
            var showtime = await _context.Showtimes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ShowtimeId == id);

            if (showtime == null)
                return NotFound();

            return Ok(showtime);
        }



        // =====================================================
        // 3️⃣ GET: api/showtimes/6/seats
        // Seat Map cho trang SeatMap.vue
        // =====================================================
        [HttpGet("{id}/seats")]
        public async Task<IActionResult> GetSeats(int id)
        {
            // 🔹 check showtime tồn tại
            var showtime = await _context.Showtimes
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.ShowtimeId == id);

            if (showtime == null)
                return NotFound("Showtime không tồn tại");


            // 🔹 lấy tất cả ghế trong phòng chiếu
            var seats = await _context.Seats
                .AsNoTracking()
                .Where(s => s.ScreenId == showtime.ScreenId)
                .OrderBy(s => s.RowNumber)
                .ThenBy(s => s.SeatNumber)
                .ToListAsync();


            // 🔹 ghế đã BOOK (đã thanh toán)
            var bookedSeatIds = await _context.Bookings
                .AsNoTracking()
                .Where(b =>
                    b.ShowtimeId == id &&
                    b.Status == "Hoàn thành")
                .Select(b => b.SeatId)
                .ToListAsync();


            // 🔹 ghế đang HOLD (đang giữ chỗ)
            var lockedSeatIds = await _context.SeatLocks
                .AsNoTracking()
                .Where(l =>
                    l.ShowtimeId == id &&
                    l.ExpiryTime > DateTime.UtcNow)
                .Select(l => l.SeatId)
                .ToListAsync();


            var bookedSet = bookedSeatIds.ToHashSet();
            var lockedSet = lockedSeatIds.ToHashSet();


            // 🔹 build seat map
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

                        Status =
                            bookedSet.Contains(s.SeatId) ? "booked" :
                            lockedSet.Contains(s.SeatId) ? "locked" :
                            "available"
                    })
                });

            return Ok(result);
        }
    }
}
