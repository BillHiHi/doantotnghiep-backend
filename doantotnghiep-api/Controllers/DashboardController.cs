using doantotnghiep_api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace doantotnghiep_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;
        public DashboardController(AppDbContext context) => _context = context;

        // ─────────────────────────────────────────────────────────
        // HELPER: RBAC — lấy TheaterId theo role
        // ─────────────────────────────────────────────────────────
        private int? GetTargetTheaterId(int? queryTheaterId)
        {
            var role = User.Claims
                .FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role")
                ?.Value?.ToUpper();
            var userTheaterIdStr = User.Claims
                .FirstOrDefault(c => c.Type == "TheaterId")?.Value;

            if (role == "BRANCH_ADMIN" && int.TryParse(userTheaterIdStr, out int tid))
                return tid;

            return queryTheaterId;
        }

        // ─────────────────────────────────────────────────────────
        // HELPER: điều kiện status hợp lệ (dùng chung)
        // ─────────────────────────────────────────────────────────
        private static bool ValidStatus(string? s) =>
            s != null && (s.ToLower() == "paid" || s.ToLower() == "collected" || s.ToLower() == "hoàn thành");

        // ─────────────────────────────────────────────────────────
        // GET /overview
        // Tổng doanh thu + vé + F&B + phim hot nhất
        // ─────────────────────────────────────────────────────────
        [HttpGet("overview")]
        public async Task<IActionResult> Overview(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int? theaterId)
        {
            var start = from ?? new DateTime(2020, 1, 1);
            var end = to ?? DateTime.Now.AddDays(1);
            var tid = GetTargetTheaterId(theaterId);

            var q = _context.Bookings
                .Include(b => b.Showtime.Screen)
                .Include(b => b.Showtime.Movie)
                .Where(b => ValidStatus(b.Status) && b.BookingDate >= start && b.BookingDate <= end);

            if (tid.HasValue)
                q = q.Where(b => b.Showtime.Screen.TheaterId == tid.Value);

            var totalTickets = await q.CountAsync();

            var distinctBookings = await q
                .GroupBy(b => b.PaymentCode)
                .Select(g => new {
                    TotalAmount = g.Max(x => x.TotalAmount),
                    TicketPriceTotal = g.Sum(x => x.Showtime != null ? (decimal?)x.Showtime.BasePrice : 0) ?? 0
                })
                .ToListAsync();

            var totalAmount = distinctBookings.Sum(x => x.TotalAmount);
            var totalRevenue = await q.SumAsync(b => (decimal?)b.Showtime.BasePrice) ?? 0;

            var topMovie = await q
                .GroupBy(x => x.Showtime.Movie.Title)
                .Select(g => new { Movie = g.Key, Tickets = g.Count() })
                .OrderByDescending(x => x.Tickets)
                .FirstOrDefaultAsync();

            return Ok(new
            {
                TotalRevenue = totalRevenue,
                TotalAmount = totalAmount,
                TotalFnb = Math.Max(0, totalAmount - totalRevenue),
                TotalTickets = totalTickets,
                TopMovie = topMovie?.Movie ?? "N/A",
                TopMovieTickets = topMovie?.Tickets ?? 0
            });
        }

        // ─────────────────────────────────────────────────────────
        // GET /theater-stats
        // Từng rạp: doanh thu + vé + phim bán chạy nhất
        // ✅ Thay thế hoàn toàn cho /top-theaters (đã xoá)
        // ─────────────────────────────────────────────────────────
        [HttpGet("theater-stats")]
        public async Task<IActionResult> TheaterStats(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var start = from ?? new DateTime(2020, 1, 1);
            var end = to ?? DateTime.Now.AddDays(1);

            var bookings = await _context.Bookings
                .AsNoTracking()
                .Include(b => b.Showtime).ThenInclude(s => s.Screen).ThenInclude(sc => sc.Theater)
                .Include(b => b.Showtime).ThenInclude(s => s.Movie)
                .Where(b =>
                    ValidStatus(b.Status) &&
                    b.BookingDate >= start && b.BookingDate <= end &&
                    b.Showtime != null &&
                    b.Showtime.Screen != null &&
                    b.Showtime.Screen.Theater != null)
                .ToListAsync();

            var result = bookings
                .GroupBy(b => new {
                    TheaterId = b.Showtime.Screen.TheaterId,
                    TheaterName = b.Showtime.Screen.Theater.Name
                })
                .Select(g =>
                {
                    var revenue = g
                        .GroupBy(b => b.PaymentCode ?? b.BookingId.ToString())
                        .Sum(pg => pg.Max(x => x.TotalAmount));

                    var topMovie = g
                        .GroupBy(b => b.Showtime?.Movie?.Title ?? "N/A")
                        .Select(mg => new { Title = mg.Key, Tickets = mg.Count() })
                        .OrderByDescending(x => x.Tickets)
                        .FirstOrDefault();

                    return new
                    {
                        TheaterId = g.Key.TheaterId,
                        TheaterName = g.Key.TheaterName,
                        Revenue = revenue,
                        TotalTickets = g.Count(),
                        TopMovie = topMovie?.Title ?? "N/A",
                        TopMovieTickets = topMovie?.Tickets ?? 0,
                        ShowtimeCount = g.Select(b => b.ShowtimeId).Distinct().Count()
                    };
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();

            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────
        // GET /genres
        // Tỉ trọng thể loại phim (Pie chart)
        // ─────────────────────────────────────────────────────────
        [HttpGet("genres")]
        public async Task<IActionResult> GenreStats(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int? theaterId)
        {
            var start = from ?? DateTime.MinValue;
            var end = to ?? DateTime.MaxValue;
            var tid = GetTargetTheaterId(theaterId);

            var q = _context.Bookings
                .Include(b => b.Showtime.Movie)
                .Include(b => b.Showtime.Screen)
                .Where(b => ValidStatus(b.Status) && b.BookingDate >= start && b.BookingDate <= end);

            if (tid.HasValue)
                q = q.Where(b => b.Showtime.Screen.TheaterId == tid.Value);

            var result = await q
                .GroupBy(x => x.Showtime.Movie.Genre ?? "Khác")
                .Select(g => new { Genre = g.Key, Tickets = g.Count() })
                .OrderByDescending(x => x.Tickets)
                .ToListAsync();

            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────
        // GET /movies
        // Xếp hạng phim theo vé bán + doanh thu
        // ─────────────────────────────────────────────────────────
        [HttpGet("movies")]
        public async Task<IActionResult> MovieStats(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int? theaterId)
        {
            var start = from ?? DateTime.MinValue;
            var end = to ?? DateTime.MaxValue;
            var tid = GetTargetTheaterId(theaterId);

            var q = _context.Bookings
                .Include(b => b.Showtime).ThenInclude(s => s.Screen)
                .Where(b => ValidStatus(b.Status) && b.BookingDate >= start && b.BookingDate <= end);

            if (tid.HasValue)
                q = q.Where(b => b.Showtime.Screen.TheaterId == tid.Value);

            var result = await q
                .GroupBy(x => x.Showtime.Movie.Title)
                .Select(g => new
                {
                    Title = g.Key,
                    Tickets = g.Count(),
                    Revenue = g.Sum(x => (decimal?)x.Showtime.BasePrice) ?? 0
                })
                .OrderByDescending(x => x.Tickets)
                .ToListAsync();

            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────
        // GET /revenue-by-date
        // Doanh thu theo ngày (Line chart)
        // ─────────────────────────────────────────────────────────
        [HttpGet("revenue-by-date")]
        public async Task<IActionResult> RevenueByDate(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int? theaterId)
        {
            var start = from ?? DateTime.MinValue;
            var end = to ?? DateTime.MaxValue;
            var tid = GetTargetTheaterId(theaterId);

            var q = _context.Bookings
                .Include(b => b.Showtime).ThenInclude(s => s.Screen)
                .Where(b => ValidStatus(b.Status) && b.BookingDate >= start && b.BookingDate <= end && b.Showtime != null);

            if (tid.HasValue)
                q = q.Where(b => b.Showtime.Screen.TheaterId == tid.Value);

            var result = await q
                .GroupBy(b => new { Date = b.BookingDate.Date, b.PaymentCode })
                .Select(g => new { g.Key.Date, Amount = g.Max(x => x.TotalAmount) })
                .GroupBy(x => x.Date)
                .Select(g => new { Date = g.Key, Revenue = g.Sum(x => x.Amount) })
                .OrderBy(x => x.Date)
                .ToListAsync();

            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────
        // GET /recent
        // 20 giao dịch gần nhất
        // ─────────────────────────────────────────────────────────
        [HttpGet("recent")]
        public async Task<IActionResult> RecentOrders(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int? theaterId)
        {
            var start = from ?? new DateTime(2020, 1, 1);
            var end = to ?? DateTime.Now.AddDays(1);
            var tid = GetTargetTheaterId(theaterId);

            var q = _context.Bookings
                .AsNoTracking()
                .Include(b => b.User)
                .Include(b => b.Showtime).ThenInclude(s => s.Movie)
                .Include(b => b.Showtime).ThenInclude(s => s.Screen).ThenInclude(sc => sc.Theater)
                .Where(b => b.BookingDate >= start && b.BookingDate <= end);

            if (tid.HasValue)
                q = q.Where(b => b.Showtime.Screen.TheaterId == tid.Value);

            var raw = await q.OrderByDescending(b => b.BookingDate).ToListAsync();

            var result = raw
                .GroupBy(b => b.PaymentCode ?? b.BookingId.ToString())
                .Take(20)
                .Select(g =>
                {
                    var f = g.First();
                    return new
                    {
                        Code = f.PaymentCode ?? f.BookingId.ToString(),
                        Movie = f.Showtime?.Movie?.Title ?? "N/A",
                        CustomerName = f.User?.FullName ?? f.User?.Email ?? "Khách",
                        Status = f.Status ?? "unknown",
                        Poster = f.Showtime?.Movie?.PosterUrl,
                        Time = f.BookingDate
                    };
                })
                .ToList();

            return Ok(result);
        }
    }
}