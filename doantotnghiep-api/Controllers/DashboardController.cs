using doantotnghiep_api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace doantotnghiep_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // ⭐ OVERVIEW (tổng tiền + tổng vé + phim hot nhất)
        // =========================================================
        [HttpGet("overview")]
        public async Task<IActionResult> Overview(DateTime? from, DateTime? to)
        {
            var start = from ?? DateTime.MinValue;
            var end = to ?? DateTime.MaxValue;

            var bookings = _context.Bookings
                .Where(b =>
                    b.Status == "Paid" &&
                    b.BookingDate >= start &&
                    b.BookingDate <= end);

            var totalTickets = await bookings.CountAsync();

            var totalRevenue = await bookings
                .Join(_context.Showtimes,
                    b => b.ShowtimeId,
                    s => s.ShowtimeId,
                    (b, s) => s.BasePrice)
                .SumAsync();

            var topMovie = await bookings
                .Join(_context.Showtimes, b => b.ShowtimeId, s => s.ShowtimeId, (b, s) => s)
                .Join(_context.Movies, s => s.MovieId, m => m.MovieId, (s, m) => m.Title)
                .GroupBy(x => x)
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
        public async Task<IActionResult> MovieStats(DateTime? from, DateTime? to)
        {
            var start = from ?? DateTime.MinValue;
            var end = to ?? DateTime.MaxValue;

            var result = await _context.Bookings
                .Where(b =>
                    b.Status == "Paid" &&
                    b.BookingDate >= start &&
                    b.BookingDate <= end)
                .Join(_context.Showtimes,
                    b => b.ShowtimeId,
                    s => s.ShowtimeId,
                    (b, s) => new { s.MovieId, s.BasePrice })
                .GroupBy(x => x.MovieId)
                .Select(g => new
                {
                    MovieId = g.Key,
                    Tickets = g.Count(),
                    Revenue = g.Sum(x => x.BasePrice)
                })
                .Join(_context.Movies,
                    x => x.MovieId,
                    m => m.MovieId,
                    (x, m) => new
                    {
                        m.Title,
                        x.Tickets,
                        x.Revenue
                    })
                .OrderByDescending(x => x.Tickets)
                .ToListAsync();

            return Ok(result);
        }


        // =========================================================
        // ⭐ DOANH THU THEO NGÀY (cho chart)
        // =========================================================
        [HttpGet("revenue-by-date")]
        public async Task<IActionResult> RevenueByDate()
        {
            var result = await _context.Bookings
                .Where(b => b.Status == "Paid")
                .Join(_context.Showtimes,
                    b => b.ShowtimeId,
                    s => s.ShowtimeId,
                    (b, s) => new
                    {
                        Date = b.BookingDate.Date,
                        s.BasePrice
                    })
                .GroupBy(x => x.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Revenue = g.Sum(x => x.BasePrice)
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            return Ok(result);
        }
    }
}
