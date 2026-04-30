using doantotnghiep_api.Data;
using doantotnghiep_api.Models;
using Microsoft.EntityFrameworkCore;

namespace doantotnghiep_api.Services
{
    public class ShowtimeAutomationService
    {
        private readonly AppDbContext _context;

        public ShowtimeAutomationService(AppDbContext context) => _context = context;

        public async Task<int> GenerateAutoShowtimes(int theaterId, DateTime targetDate)
        {
            // 1. Lấy danh sách phòng của rạp
            var screens = await _context.Screens
                .Where(s => s.TheaterId == theaterId)
                .ToListAsync();

            // 2. Lấy các hợp đồng còn hiệu lực và còn suất chiếu
            var activeContracts = await _context.ScreeningContracts
                .Include(c => c.Movie)
                .Where(c => targetDate >= c.StartDate && targetDate <= c.EndDate)
                .ToListAsync();

            // 3. Định nghĩa khung giờ hoạt động (Ví dụ: 08:00 - 23:00)[cite: 5]
            var openingTime = targetDate.Date.AddHours(8);
            var closingTime = targetDate.Date.AddHours(23).AddMinutes(30);

            int createdCount = 0;

            foreach (var screen in screens)
            {
                var currentTime = openingTime;

                while (currentTime < closingTime)
                {
                    // Chọn phim dựa trên mức độ ưu tiên và số suất còn lại trong hợp đồng[cite: 1, 4]
                    var selectedContract = PickBestContract(activeContracts, targetDate);

                    if (selectedContract == null) break;

                    var movie = selectedContract.Movie;
                    var showtimeEnd = currentTime.AddMinutes(movie.Duration + 20); // Thời gian phim + 20p dọn dẹp

                    if (showtimeEnd > closingTime) break;

                    // Tạo thực thể suất chiếu[cite: 2]
                    var showtime = new Showtime
                    {
                        MovieId = movie.MovieId,
                        ScreenId = screen.ScreenId,
                        StartTime = currentTime,
                        EndTime = showtimeEnd,
                        BasePrice = CalculateSmartPrice(currentTime, screen.ScreenType), // Giá tự động theo khung giờ[cite: 5]
                        IsEarlyScreening = movie.ReleaseDate > targetDate // Tự động đánh dấu chiếu sớm[cite: 2, 3]
                    };

                    _context.Showtimes.Add(showtime);
                    createdCount++;

                    // Nhảy thời gian sang suất tiếp theo
                    currentTime = showtimeEnd;
                }
            }

            await _context.SaveChangesAsync();
            return createdCount;
        }

        private ScreeningContract? PickBestContract(List<ScreeningContract> contracts, DateTime date)
        {
            // Logic ưu tiên: Phim có TotalSlots lớn hơn hoặc chưa đạt chỉ tiêu ngày sẽ được chọn trước[cite: 1, 4]
            return contracts
                .OrderByDescending(c => c.TotalSlots)
                .FirstOrDefault();
        }

        private decimal CalculateSmartPrice(DateTime startTime, string screenType)
        {
            // Tự động tăng giá nếu vào khung giờ vàng (18h-21h)[cite: 1, 5]
            decimal basePrice = 50000;
            if (startTime.Hour >= 18 && startTime.Hour <= 21) basePrice += 20000;
            return basePrice;
        }
    }
}
