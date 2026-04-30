using doantotnghiep_api.Data;
using doantotnghiep_api.Models;
using Microsoft.EntityFrameworkCore;

namespace doantotnghiep_api.Services
{
    public class ShowtimeService
    {
        private readonly AppDbContext _context;

        public ShowtimeService(AppDbContext context) => _context = context;

        // Bước 10: Phân bổ lịch chiếu và xử lý xung đột
        public async Task<Showtime> CreateShowtimeAsync(Showtime newShowtime)
        {
            // 1. Lấy TheaterId từ Screen
            var screen = await _context.Screens.FindAsync(newShowtime.ScreenId);
            if (screen == null) throw new Exception("Không tìm thấy phòng chiếu.");

            // 2. Lấy ContractTheater theo MovieId + TheaterId + thời điểm hợp lệ
            var contractTheater = await _context.ContractTheaters
                .Include(ct => ct.Contract)
                .FirstOrDefaultAsync(ct =>
                    ct.Contract.MovieId == newShowtime.MovieId &&
                    ct.TheaterId == screen.TheaterId &&
                    newShowtime.StartTime >= ct.Contract.StartDate &&
                    newShowtime.StartTime <= ct.Contract.EndDate);

            if (contractTheater == null)
                throw new Exception("Rạp này không có hợp đồng hợp lệ cho phim và thời điểm này.");

            // 3. Đếm slot đã dùng của rạp này trong kỳ hợp đồng
            int usedSlots = await _context.Showtimes.CountAsync(s =>
                s.MovieId == newShowtime.MovieId &&
                s.Screen.TheaterId == screen.TheaterId &&
                s.StartTime >= contractTheater.Contract.StartDate &&
                s.StartTime <= contractTheater.Contract.EndDate);

            if (usedSlots >= contractTheater.AllocatedSlots)
                throw new Exception($"Rạp đã đạt giới hạn {contractTheater.AllocatedSlots} suất chiếu được phân bổ theo hợp đồng.");

            // 4. Kiểm tra xung đột lịch phòng chiếu (giữ nguyên logic cũ)
            bool isOverlapping = await _context.Showtimes.AnyAsync(s =>
                s.ScreenId == newShowtime.ScreenId &&
                ((newShowtime.StartTime >= s.StartTime && newShowtime.StartTime < s.EndTime) ||
                 (newShowtime.EndTime > s.StartTime && newShowtime.EndTime <= s.EndTime)));

            if (isOverlapping)
                throw new Exception("Xung đột lịch chiếu: Phòng này đã có suất chiếu khác trong khoảng thời gian này.");

            _context.Showtimes.Add(newShowtime);
            await _context.SaveChangesAsync();
            return newShowtime;
        }
    }
}
