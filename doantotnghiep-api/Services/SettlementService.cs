using doantotnghiep_api.Data;
using Microsoft.EntityFrameworkCore;

namespace doantotnghiep_api.Services
{
    public class SettlementService
    {
        private readonly AppDbContext _context;

        public SettlementService(AppDbContext context) => _context = context;

        // Bước 12 & 13: Theo dõi doanh thu và quyết toán[cite: 1]
        public async Task<object> GetContractSettlementAsync(int contractId)
        {
            var contract = await _context.ScreeningContracts
                .Include(c => c.Movie)
                .FirstOrDefaultAsync(c => c.ContractId == contractId);

            if (contract == null) return null;

            // 1. Lấy danh sách Showtimes thuộc hợp đồng này
            var showtimes = await _context.Showtimes
                .Where(s => s.MovieId == contract.MovieId
                       && s.StartTime >= contract.StartDate
                       && s.StartTime <= contract.EndDate)
                .ToListAsync();

            var showtimeIds = showtimes.Select(s => s.ShowtimeId).ToList();

            // 2. JOIN với bảng Bookings để tính doanh thu thực tế
            // Chúng ta chỉ tính những vé có trạng thái "Paid" hoặc "Collected"
            var actualBookings = await _context.Bookings
                .Where(b => showtimeIds.Contains(b.ShowtimeId)
                       && (b.Status == "Paid" || b.Status == "Collected"))
                .ToListAsync();

            // 3. Tính toán các chỉ số
            int actualSlots = showtimes.Count;
            decimal totalActualRevenue = actualBookings.Sum(b => b.TotalAmount); // Tổng tiền thực thu từ khách
            int totalTicketsSold = actualBookings.Count;

            return new
            {
                ContractId = contract.ContractId,
                MovieTitle = contract.Movie.Title,
                Duration = $"{contract.StartDate:dd/MM} - {contract.EndDate:dd/MM}",

                // Chỉ số về suất chiếu[cite: 1]
                SlotsTarget = contract.TotalSlots,
                SlotsActual = actualSlots,

                // Chỉ số về tài chính[cite: 6]
                TicketsSold = totalTicketsSold,
                TotalRevenue = totalActualRevenue,

                // Trạng thái hoàn thành[cite: 1]
                Status = actualSlots < contract.TotalSlots ? "Chưa đạt chỉ tiêu suất chiếu" : "Hoàn thành chỉ tiêu"
            };
        }
    }
}
