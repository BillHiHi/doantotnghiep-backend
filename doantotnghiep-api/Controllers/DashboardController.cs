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
            var roleClaim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role");
            var role = roleClaim?.Value?.ToUpper();

            var theaterIdClaim = User.Claims.FirstOrDefault(c => c.Type == "TheaterId");
            var userTheaterIdStr = theaterIdClaim?.Value;

            if ((role == "BRANCH_ADMIN" || role == "BRANCHADMIN") && int.TryParse(userTheaterIdStr, out int theaterId))
            {
                return theaterId;
            }

            return queryTheaterId;
        }

        // =========================================================
        // ⭐ OVERVIEW (tổng tiền + tổng vé + phim hot nhất)
        // =========================================================
        [HttpGet("overview")]
        public async Task<IActionResult> Overview([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? theaterId)
        {
            var start = from ?? new DateTime(2020, 1, 1);
            var end = to ?? DateTime.Now.AddDays(1);
            var targetTheaterId = GetTargetTheaterId(theaterId);

            var bookingsQuery = _context.Bookings
                .Include(b => b.Showtime.Screen)
                .Include(b => b.Showtime.Movie)
                .Where(b =>
                    ((b.Status ?? "").ToLower() == "paid" || (b.Status ?? "").ToLower() == "collected" || (b.Status ?? "").ToLower() == "hoàn thành") &&
                    b.BookingDate >= start &&
                    b.BookingDate <= end);

            if (targetTheaterId.HasValue)
            {
                bookingsQuery = bookingsQuery.Where(b => b.Showtime.Screen.TheaterId == targetTheaterId.Value);
            }

            var totalTickets = await bookingsQuery.CountAsync();

            // 💡 FIX: Chỉ lấy TotalAmount 1 lần cho mỗi PaymentCode để tránh nhân bản doanh thu
            var distinctBookings = await bookingsQuery
                .GroupBy(b => b.PaymentCode)
                .Select(g => new {
                    TotalAmount = g.Max(x => x.TotalAmount),
                })
                .ToListAsync();

            var totalAmount = distinctBookings.Sum(x => x.TotalAmount);
            var totalRevenue = await bookingsQuery.SumAsync(b => (decimal?)b.Showtime.BasePrice) ?? 0;
            var totalFnb = totalAmount - totalRevenue;

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
                TotalAmount = totalAmount,
                TotalFnb = totalFnb > 0 ? totalFnb : 0,
                TotalTickets = totalTickets,
                TopMovie = topMovie?.Movie ?? "N/A",
                TopMovieTickets = topMovie?.Tickets ?? 0
            });
        }

        // =========================================================
        // ⭐ MOVIES STATS (Thống kê phim theo số vé)
        // =========================================================
        [HttpGet("movies")]
        public async Task<IActionResult> Movies([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? theaterId)
        {
            var start = from ?? new DateTime(2020, 1, 1);
            var end = to ?? DateTime.Now.AddDays(1);
            var targetTheaterId = GetTargetTheaterId(theaterId);

            var bookingsQuery = _context.Bookings
                .Include(b => b.Showtime.Movie)
                .Include(b => b.Showtime.Screen)
                .Where(b => ((b.Status ?? "").ToLower() == "paid" || (b.Status ?? "").ToLower() == "collected" || (b.Status ?? "").ToLower() == "hoàn thành") && b.BookingDate >= start && b.BookingDate <= end);

            if (targetTheaterId.HasValue)
            {
                bookingsQuery = bookingsQuery.Where(b => b.Showtime.Screen.TheaterId == targetTheaterId.Value);
            }

            var result = await bookingsQuery
                .GroupBy(b => new { b.Showtime.Movie.Title, b.Showtime.Movie.PosterUrl })
                .Select(g => new
                {
                    Title = g.Key.Title,
                    Poster = g.Key.PosterUrl ?? "",
                    Tickets = g.Count(),
                    Revenue = g.Sum(x => (decimal?)x.TotalAmount) ?? 0
                })
                .OrderByDescending(x => x.Tickets)
                .ToListAsync();

            return Ok(result);
        }

        // =========================================================
        // ⭐ REVENUE BY DATE (Doanh thu theo ngày)
        // =========================================================
        [HttpGet("revenue-by-date")]
        public async Task<IActionResult> RevenueByDate([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? theaterId)
        {
            var start = from ?? new DateTime(2020, 1, 1);
            var end = to ?? DateTime.Now.AddDays(1);
            var targetTheaterId = GetTargetTheaterId(theaterId);

            var bookingsQuery = _context.Bookings
                .Include(b => b.Showtime.Screen)
                .Where(b => ((b.Status ?? "").ToLower() == "paid" || (b.Status ?? "").ToLower() == "collected" || (b.Status ?? "").ToLower() == "hoàn thành") && b.BookingDate >= start && b.BookingDate <= end);

            if (targetTheaterId.HasValue)
            {
                bookingsQuery = bookingsQuery.Where(b => b.Showtime.Screen.TheaterId == targetTheaterId.Value);
            }

            var result = await bookingsQuery
                .GroupBy(b => new { Date = b.BookingDate.Date, b.PaymentCode })
                .Select(g => new
                {
                    Date = g.Key.Date,
                    PaymentCode = g.Key.PaymentCode,
                    Amount = g.Max(x => x.TotalAmount)
                })
                .GroupBy(x => x.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Revenue = g.Sum(x => x.Amount)
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            return Ok(result);
        }

        // =========================================================
        // ⭐ GENRES STATS (Thống kê theo thể loại)
        // =========================================================
        [HttpGet("genres")]
        public async Task<IActionResult> Genres([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? theaterId)
        {
            var start = from ?? new DateTime(2020, 1, 1);
            var end = to ?? DateTime.Now.AddDays(1);
            var targetTheaterId = GetTargetTheaterId(theaterId);

            var bookingsQuery = _context.Bookings
                .Include(b => b.Showtime.Movie)
                .Include(b => b.Showtime.Screen)
                .Where(b => ((b.Status ?? "").ToLower() == "paid" || (b.Status ?? "").ToLower() == "collected" || (b.Status ?? "").ToLower() == "hoàn thành") && b.BookingDate >= start && b.BookingDate <= end);

            if (targetTheaterId.HasValue)
            {
                bookingsQuery = bookingsQuery.Where(b => b.Showtime.Screen.TheaterId == targetTheaterId.Value);
            }

            var result = await bookingsQuery
                .GroupBy(b => b.Showtime.Movie.Genre)
                .Select(g => new
                {
                    Genre = g.Key ?? "Khác",
                    Tickets = g.Count()
                })
                .OrderByDescending(x => x.Tickets)
                .ToListAsync();

            return Ok(result);
        }

        // =========================================================
        // ⭐ TOP RẠP DOANH THU (Dành cho Admin Hệ Thống)
        // =========================================================
        [HttpGet("top-theaters")]
        public async Task<IActionResult> TopTheaters([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var start = from ?? new DateTime(2020, 1, 1);
            var end = to ?? DateTime.Now.AddDays(1);

            var result = await _context.Bookings
                .Include(b => b.Showtime.Screen.Theater)
                .Where(b => ((b.Status ?? "").ToLower() == "paid" || (b.Status ?? "").ToLower() == "collected" || (b.Status ?? "").ToLower() == "hoàn thành") && b.BookingDate >= start && b.BookingDate <= end)
                .GroupBy(b => new { TheaterName = b.Showtime.Screen.Theater.Name, b.PaymentCode })
                .Select(g => new
                {
                    TheaterName = g.Key.TheaterName ?? "N/A",
                    PaymentCode = g.Key.PaymentCode,
                    OrderAmount = g.Max(x => x.TotalAmount),
                    TicketCount = g.Count()
                })
                .GroupBy(x => x.TheaterName)
                .Select(g => new
                {
                    Theater = g.Key,
                    Revenue = g.Sum(x => x.OrderAmount),
                    Tickets = g.Sum(x => x.TicketCount)
                })
                .OrderByDescending(x => x.Revenue)
                .Take(5)
                .ToListAsync();

            return Ok(result);
        }
    }
}