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

        private static bool ValidStatus(string? s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            var lower = s.ToLower();
            return lower == "paid" || lower == "collected" || lower == "hoàn thành" || lower == "completed";
        }

        [HttpGet("overview")]
        public async Task<IActionResult> Overview(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int? theaterId)
        {
            try
            {
                var start = from ?? new DateTime(2020, 1, 1);
                var end = to ?? DateTime.Now.AddDays(1);
                var tid = GetTargetTheaterId(theaterId);

                var bookings = await _context.Bookings
                    .AsNoTracking()
                    .Include(b => b.Showtime).ThenInclude(s => s.Screen).ThenInclude(sc => sc.Theater)
                    .Include(b => b.Showtime).ThenInclude(s => s.Movie)
                    .Where(b => b.BookingDate >= start && b.BookingDate <= end)
                    .ToListAsync();

                if (tid.HasValue)
                    bookings = bookings.Where(b => b.Showtime?.Screen?.TheaterId == tid.Value).ToList();

                var validBookings = bookings.Where(b => ValidStatus(b.Status)).ToList();
                var totalTickets = validBookings.Count;

                var totalAmount = validBookings
                    .GroupBy(b => b.PaymentCode ?? b.BookingId.ToString())
                    .Sum(g => g.Max(x => x.TotalAmount));

                var ticketRevenue = validBookings
                    .Where(b => b.Showtime?.BasePrice > 0)
                    .Sum(b => b.Showtime.BasePrice);

                var topMovie = validBookings
                    .Where(b => !string.IsNullOrEmpty(b.Showtime?.Movie?.Title))
                    .GroupBy(b => b.Showtime.Movie.Title)
                    .Select(g => new { Movie = g.Key, Tickets = g.Count() })
                    .OrderByDescending(x => x.Tickets)
                    .FirstOrDefault();

                return Ok(new
                {
                    TotalRevenue = totalAmount,
                    TotalTickets = totalTickets,
                    TotalFnb = Math.Max(0, totalAmount - ticketRevenue),
                    TopMovie = topMovie?.Movie ?? "N/A",
                    TopMovieTickets = topMovie?.Tickets ?? 0
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("theater-stats")]
        public async Task<IActionResult> TheaterStats(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            try
            {
                var start = from ?? new DateTime(2020, 1, 1);
                var end = to ?? DateTime.Now.AddDays(1);

                var bookings = await _context.Bookings
                    .AsNoTracking()
                    .Include(b => b.Showtime).ThenInclude(s => s.Screen).ThenInclude(sc => sc.Theater)
                    .Include(b => b.Showtime).ThenInclude(s => s.Movie)
                    .Where(b => b.BookingDate >= start && b.BookingDate <= end)
                    .ToListAsync();

                var validBookings = bookings.Where(b => ValidStatus(b.Status)).ToList();

                var result = validBookings
                    .Where(b => b.Showtime?.Screen?.Theater != null)
                    .GroupBy(b => new {
                        TheaterId = b.Showtime.Screen.TheaterId,
                        TheaterName = b.Showtime.Screen.Theater.Name
                    })
                    .Select(g =>
                    {
                        var revenue = g.Sum(x => x.TotalAmount);

                        var topMovie = g
                            .Where(b => !string.IsNullOrEmpty(b.Showtime?.Movie?.Title))
                            .GroupBy(b => b.Showtime.Movie.Title)
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
                            TopMovieTickets = topMovie?.Tickets ?? 0
                        };
                    })
                    .OrderByDescending(x => x.Revenue)
                    .ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("genres")]
        public async Task<IActionResult> GenreStats(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int? theaterId)
        {
            try
            {
                var start = from ?? DateTime.MinValue;
                var end = to ?? DateTime.MaxValue;
                var tid = GetTargetTheaterId(theaterId);

                var bookings = await _context.Bookings
                    .AsNoTracking()
                    .Include(b => b.Showtime).ThenInclude(s => s.Movie)
                    .Include(b => b.Showtime).ThenInclude(s => s.Screen)
                    .Where(b => b.BookingDate >= start && b.BookingDate <= end)
                    .ToListAsync();

                var validBookings = bookings.Where(b => ValidStatus(b.Status)).ToList();

                if (tid.HasValue)
                    validBookings = validBookings.Where(b => b.Showtime?.Screen?.TheaterId == tid.Value).ToList();

                var result = validBookings
                    .Where(b => b.Showtime?.Movie != null)
                    .GroupBy(x => x.Showtime.Movie.Genre ?? "Khác")
                    .Select(g => new { Genre = g.Key, Tickets = g.Count() })
                    .OrderByDescending(x => x.Tickets)
                    .ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("movies")]
        public async Task<IActionResult> MovieStats(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int? theaterId)
        {
            try
            {
                var start = from ?? DateTime.MinValue;
                var end = to ?? DateTime.MaxValue;
                var tid = GetTargetTheaterId(theaterId);

                var bookings = await _context.Bookings
                    .AsNoTracking()
                    .Include(b => b.Showtime).ThenInclude(s => s.Screen)
                    .Include(b => b.Showtime).ThenInclude(s => s.Movie)
                    .Where(b => b.BookingDate >= start && b.BookingDate <= end)
                    .ToListAsync();

                var validBookings = bookings.Where(b => ValidStatus(b.Status)).ToList();

                if (tid.HasValue)
                    validBookings = validBookings.Where(b => b.Showtime?.Screen?.TheaterId == tid.Value).ToList();

                var result = validBookings
                    .Where(b => b.Showtime?.Movie != null)
                    .GroupBy(x => x.Showtime.Movie.Title)
                    .Select(g => new
                    {
                        Title = g.Key,
                        Tickets = g.Count(),
                        Revenue = g.Sum(x => x.Showtime?.BasePrice ?? 0)
                    })
                    .OrderByDescending(x => x.Tickets)
                    .ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("revenue-by-date")]
        public async Task<IActionResult> RevenueByDate(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int? theaterId)
        {
            try
            {
                var start = from ?? DateTime.MinValue;
                var end = to ?? DateTime.MaxValue;
                var tid = GetTargetTheaterId(theaterId);

                var bookings = await _context.Bookings
                    .AsNoTracking()
                    .Include(b => b.Showtime).ThenInclude(s => s.Screen)
                    .Where(b => b.BookingDate >= start && b.BookingDate <= end)
                    .ToListAsync();

                var validBookings = bookings.Where(b => ValidStatus(b.Status)).ToList();

                if (tid.HasValue)
                    validBookings = validBookings.Where(b => b.Showtime?.Screen?.TheaterId == tid.Value).ToList();

                var result = validBookings
                    .GroupBy(b => b.BookingDate.Date)
                    .Select(g => new {
                        Date = g.Key,
                        Revenue = g.Sum(x => x.TotalAmount)
                    })
                    .OrderBy(x => x.Date)
                    .ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("recent")]
        public async Task<IActionResult> RecentOrders(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int? theaterId)
        {
            try
            {
                var start = from ?? new DateTime(2020, 1, 1);
                var end = to ?? DateTime.Now.AddDays(1);
                var tid = GetTargetTheaterId(theaterId);

                var query = _context.Bookings
                    .AsNoTracking()
                    .Include(b => b.User)
                    .Include(b => b.Showtime).ThenInclude(s => s.Movie)
                    .Include(b => b.Showtime).ThenInclude(s => s.Screen).ThenInclude(sc => sc.Theater)
                    .Where(b => b.BookingDate >= start && b.BookingDate <= end);

                if (tid.HasValue)
                    query = query.Where(b => b.Showtime.Screen.TheaterId == tid.Value);

                var raw = await query.OrderByDescending(b => b.BookingDate).Take(20).ToListAsync();

                var result = raw
                    .GroupBy(b => b.PaymentCode ?? b.BookingId.ToString())
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
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
