using doantotnghiep_api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace doantotnghiep_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // 🔐 BẢO MẬT: Chỉ Admin mới được xem thống kê
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // HELPER: Lấy TheaterId từ Token hoặc Query (dành cho RBAC)
        // =========================================================
        private int? GetTargetTheaterId(int? queryTheaterId)
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            var userTheaterIdStr = User.FindFirstValue("TheaterId");

            // Nếu là Admin Rạp: Bắt buộc chỉ xem rạp của mình
            if (role == "BRANCH_ADMIN" && int.TryParse(userTheaterIdStr, out int theaterId))
            {
                return theaterId;
            }

            // Nếu là Admin Tổng: Xem theo rạp được chọn (hoặc null = tất cả)
            return queryTheaterId;
        }

        // =========================================================
        // ⭐ OVERVIEW (tổng tiền + tổng vé + phim hot nhất)
        // =========================================================
        [HttpGet("overview")]
        public async Task<IActionResult> Overview([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? theaterId)
        {
            var start = from ?? DateTime.MinValue;
            var end = to ?? DateTime.MaxValue;
            var targetTheaterId = GetTargetTheaterId(theaterId);

            var bookingsQuery = _context.Bookings
                .Include(b => b.Showtime)
                .ThenInclude(s => s.Screen)
                .Where(b =>
                    b.Status == "Paid" &&
                    b.BookingDate >= start &&
                    b.BookingDate <= end);

            // 💡 Lọc theo rạp nếu có
            if (targetTheaterId.HasValue)
            {
                bookingsQuery = bookingsQuery.Where(b => b.Showtime.Screen.TheaterId == targetTheaterId.Value);
            }

            var totalTickets = await bookingsQuery.CountAsync();

            var totalRevenue = await bookingsQuery.SumAsync(b => b.Showtime.BasePrice);

            var topMovie = await bookingsQuery
                .GroupBy(x => x.Showtime.Movie.Title)
                .Select(g => new
                {
                    Movie = g.Key,
                    Tickets = g.Count()
                })
                .OrderByDescending(x => x.Tickets)
                .FirstOrDefaultAsync();

            return Ok(new
            {
                TotalRevenue = totalRevenue,
                TotalTickets = totalTickets,
                TopMovie = topMovie?.Movie ?? "N/A",
                TopMovieTickets = topMovie?.Tickets ?? 0
            });
        }

        // =========================================================
        // ⭐ THỐNG KÊ THEO PHIM
        // =========================================================
        [HttpGet("movies")]
        public async Task<IActionResult> MovieStats([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? theaterId)
        {
            var start = from ?? DateTime.MinValue;
            var end = to ?? DateTime.MaxValue;
            var targetTheaterId = GetTargetTheaterId(theaterId);

            var bookingsQuery = _context.Bookings
                .Include(b => b.Showtime)
                .ThenInclude(s => s.Screen)
                .Where(b =>
                    b.Status == "Paid" &&
                    b.BookingDate >= start &&
                    b.BookingDate <= end);

            if (targetTheaterId.HasValue)
            {
                bookingsQuery = bookingsQuery.Where(b => b.Showtime.Screen.TheaterId == targetTheaterId.Value);
            }

            var result = await bookingsQuery
                .GroupBy(x => x.Showtime.Movie.Title)
                .Select(g => new
                {
                    Title = g.Key,
                    Tickets = g.Count(),
                    Revenue = g.Sum(x => x.Showtime.BasePrice)
                })
                .OrderByDescending(x => x.Tickets)
                .ToListAsync();

            return Ok(result);
        }

        // =========================================================
        // ⭐ DOANH THU THEO NGÀY (cho chart)
        // =========================================================
        [HttpGet("revenue-by-date")]
        public async Task<IActionResult> RevenueByDate([FromQuery] int? theaterId)
        {
            var targetTheaterId = GetTargetTheaterId(theaterId);

            var bookingsQuery = _context.Bookings
                .Include(b => b.Showtime)
                .ThenInclude(s => s.Screen)
                .Where(b => b.Status == "Paid");

            if (targetTheaterId.HasValue)
            {
                bookingsQuery = bookingsQuery.Where(b => b.Showtime.Screen.TheaterId == targetTheaterId.Value);
            }

            var result = await bookingsQuery
                .GroupBy(x => x.BookingDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Revenue = g.Sum(x => x.Showtime.BasePrice)
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            return Ok(result);
        }
    }
}
