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
        public async Task<Showtime> UpdateShowtimeAsync(int id, Showtime updatedShowtime)
        {
            var existingShowtime = await _context.Showtimes
                .Include(s => s.Screen)
                .FirstOrDefaultAsync(s => s.ShowtimeId == id);

            if (existingShowtime == null) throw new Exception("Không tìm thấy suất chiếu.");

            // 1. Kiểm tra nếu đã có vé được bán (Giả sử bạn có bảng Tickets)
            // var hasTickets = await _context.Tickets.AnyAsync(t => t.ShowtimeId == id);
            // if (hasTickets) throw new Exception("Không thể sửa suất chiếu đã có vé được bán.");

            // 2. Re-validate Hợp đồng nếu MovieId hoặc ScreenId thay đổi
            var screen = await _context.Screens.FindAsync(updatedShowtime.ScreenId);
            if (screen == null) throw new Exception("Không tìm thấy phòng chiếu.");

            var contractTheater = await _context.ContractTheaters
                .Include(ct => ct.Contract)
                .FirstOrDefaultAsync(ct =>
                    ct.Contract.MovieId == updatedShowtime.MovieId &&
                    ct.TheaterId == screen.TheaterId &&
                    updatedShowtime.StartTime >= ct.Contract.StartDate &&
                    updatedShowtime.StartTime <= ct.Contract.EndDate);

            if (contractTheater == null)
                throw new Exception("Không có hợp đồng hợp lệ cho thay đổi này.");

            // 3. Kiểm tra Overlap (Loại trừ chính nó)
            bool isOverlapping = await _context.Showtimes.AnyAsync(s =>
                s.ShowtimeId != id && // Quan trọng: Không check với chính nó
                s.ScreenId == updatedShowtime.ScreenId &&
                ((updatedShowtime.StartTime >= s.StartTime && updatedShowtime.StartTime < s.EndTime) ||
                 (updatedShowtime.EndTime > s.StartTime && updatedShowtime.EndTime <= s.EndTime)));

            if (isOverlapping)
                throw new Exception("Xung đột lịch chiếu với suất khác đã tồn tại.");

            // 4. Cập nhật thông tin
            existingShowtime.MovieId = updatedShowtime.MovieId;
            existingShowtime.ScreenId = updatedShowtime.ScreenId;
            existingShowtime.StartTime = updatedShowtime.StartTime;
            existingShowtime.EndTime = updatedShowtime.EndTime;
            existingShowtime.BasePrice = updatedShowtime.BasePrice;
            existingShowtime.IsEarlyScreening = updatedShowtime.IsEarlyScreening;

            await _context.SaveChangesAsync();
            return existingShowtime;
        }

        public async Task DeleteShowtimeAsync(int id)
        {
            var showtime = await _context.Showtimes.FindAsync(id);
            if (showtime == null) throw new Exception("Không tìm thấy suất chiếu để xóa.");

            // Kiểm tra ràng buộc: Nếu đã có vé bán thì không cho xóa (để đảm bảo data integrity)
            // var hasTickets = await _context.Tickets.AnyAsync(t => t.ShowtimeId == id);
            // if (hasTickets) throw new Exception("Suất chiếu đã có vé bán, không thể xóa.");

            _context.Showtimes.Remove(showtime);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteMultipleShowtimesAsync(IEnumerable<int> ids)
        {
            var showtimes = await _context.Showtimes.Where(s => ids.Contains(s.ShowtimeId)).ToListAsync();
            if (!showtimes.Any()) return;

            // Kiểm tra ràng buộc cho từng suất chiếu nếu cần (ví dụ: vé bán)
            // if (await _context.Tickets.AnyAsync(t => ids.Contains(t.ShowtimeId))) 
            //    throw new Exception("Một số suất chiếu đã có vé bán, không thể xóa hàng loạt.");

            _context.Showtimes.RemoveRange(showtimes);
            await _context.SaveChangesAsync();
        }
    }
}
