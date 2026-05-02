using doantotnghiep_api.Data;
using doantotnghiep_api.Models;
using Microsoft.EntityFrameworkCore;

namespace doantotnghiep_api.Services
{
    public class ContractService
    {
        private readonly AppDbContext _context;

        public ContractService(AppDbContext context) => _context = context;

        // Tạo hợp đồng đơn lẻ với xử lý % giờ vàng linh hoạt
        public async Task<ScreeningContract> CreateContractAsync(ScreeningContract contract)
        {
            // 1. Kiểm tra Nhà sản xuất
            var producer = await _context.Producers.FindAsync(contract.ProducerId);
            if (producer == null)
                throw new Exception("Nhà sản xuất không tồn tại trong hệ thống.");

            // 2. Kiểm tra Phim
            var movie = await _context.Movies.FindAsync(contract.MovieId);
            if (movie == null)
                throw new Exception("Phim không tồn tại.");

            // 3. Kiểm tra tính nhất quán
            if (movie.ProducerId != contract.ProducerId)
                throw new Exception("Nhà sản xuất trong hợp đồng không khớp với nhà sản xuất của phim.");

            // 4. Kiểm tra logic thời gian
            if (contract.EndDate <= contract.StartDate)
                throw new Exception("Ngày kết thúc phải sau ngày bắt đầu.");

            if (contract.StartDate < movie.ReleaseDate)
                throw new Exception("Ngày bắt đầu hợp đồng không thể trước ngày phát hành phim.");

            // 5. Validation % Giờ vàng (0% - 100%)
            if (contract.GoldHourPercentage < 0 || contract.GoldHourPercentage > 100)
                throw new Exception("Tỷ lệ suất chiếu giờ vàng phải từ 0% đến 100%.");

            // 6. Tính toán số lượng suất chiếu giờ vàng tối thiểu cần đạt (KPI)
            // Công thức: TotalSlots * % Gold Hour
            contract.RequiredGoldHourSlots = (int)Math.Ceiling(contract.TotalSlots * (contract.GoldHourPercentage / 100.0));

            contract.CreatedAt = DateTime.Now;
            contract.Status = "Active";

            _context.ScreeningContracts.Add(contract);
            await _context.SaveChangesAsync();

            await NotifyProducerAsync(contract.ProducerId);

            return contract;
        }

        // Tạo cả Phim và Hợp đồng trong một Transaction
        public async Task<ScreeningContract> CreateMovieAndContractAsync(Movie movie, ScreeningContract contract)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Thêm phim trước để lấy ID
                _context.Movies.Add(movie);
                await _context.SaveChangesAsync();

                // Gán thông tin cho hợp đồng
                contract.MovieId = movie.MovieId;

                // Gọi logic tạo hợp đồng (bao gồm cả tính KPI giờ vàng)
                var createdContract = await CreateContractAsync(contract);

                await transaction.CommitAsync();
                return createdContract;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Lỗi khi tạo combo Phim & Hợp đồng: {ex.Message}");
            }
        }

        public async Task<bool> DeleteContractAsync(int id)
        {
            var contract = await _context.ScreeningContracts
                .Include(c => c.Movie)
                .ThenInclude(m => m.Showtimes)
                .FirstOrDefaultAsync(c => c.ContractId == id);

            if (contract == null) throw new Exception("Không tìm thấy hợp đồng.");

            // RÀNG BUỘC: Không xóa nếu đã/đang diễn ra
            if (contract.StartDate <= DateTime.Now)
            {
                throw new Exception("Không thể xóa hợp đồng đã hoặc đang hiệu lực. Hãy sử dụng chức năng Hủy.");
            }

            // Kiểm tra suất chiếu đã lập
            var hasShowtimes = contract.Movie.Showtimes.Any(s => s.StartTime >= contract.StartDate && s.StartTime <= contract.EndDate);
            if (hasShowtimes)
            {
                throw new Exception("Hợp đồng này đã có các suất chiếu được lập lịch, hãy xóa lịch chiếu trước.");
            }

            _context.ScreeningContracts.Remove(contract);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task CancelContractAsync(int id)
        {
            var contract = await _context.ScreeningContracts.FindAsync(id);
            if (contract == null) throw new Exception("Không tìm thấy hợp đồng.");

            // Đổi trạng thái thay vì xóa để lưu lịch sử
            contract.Status = "Cancelled";
            _context.ScreeningContracts.Update(contract);
            await _context.SaveChangesAsync();
        }

        private async Task NotifyProducerAsync(int producerId)
        {
            // Logic gửi thông báo hoặc email cho NSX
        }
    }
}