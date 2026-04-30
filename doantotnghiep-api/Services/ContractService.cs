using doantotnghiep_api.Data;
using doantotnghiep_api.Models;
using Microsoft.EntityFrameworkCore;

namespace doantotnghiep_api.Services
{
    public class ContractService
    {
        private readonly AppDbContext _context;

        public ContractService(AppDbContext context) => _context = context;

        // Tạo hợp đồng đơn lẻ
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

            // 3. Kiểm tra tính nhất quán giữa Phim và NSX[cite: 8]
            if (movie.ProducerId != contract.ProducerId)
                throw new Exception("Nhà sản xuất trong hợp đồng không khớp với nhà sản xuất của phim.");

            // 4. Kiểm tra logic thời gian[cite: 8]
            if (contract.EndDate <= contract.StartDate)
                throw new Exception("Ngày kết thúc phải sau ngày bắt đầu.");

            if (contract.StartDate < movie.ReleaseDate)
                throw new Exception("Ngày bắt đầu hợp đồng không thể trước ngày phát hành phim.");

            _context.ScreeningContracts.Add(contract);
            await _context.SaveChangesAsync();

            // 5. Gửi thông báo (giả định)
            await NotifyProducerAsync(contract.ProducerId);

            return contract;
        }

        // Tạo cả Phim và Hợp đồng trong một Transaction[cite: 8]
        public async Task<ScreeningContract> CreateMovieAndContractAsync(Movie movie, ScreeningContract contract)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Movies.Add(movie);
                await _context.SaveChangesAsync();

                contract.MovieId = movie.MovieId;
                contract.CreatedAt = DateTime.Now;

                // Gọi lại logic tạo hợp đồng đã viết ở trên để reuse validation
                var createdContract = await CreateContractAsync(contract);

                await transaction.CommitAsync();
                return createdContract;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task<bool> DeleteContractAsync(int id)
        {
            var contract = await _context.ScreeningContracts
                .Include(c => c.Movie)
                .ThenInclude(m => m.Showtimes)
                .FirstOrDefaultAsync(c => c.ContractId == id);

            if (contract == null) throw new Exception("Không tìm thấy hợp đồng.");

            // RÀNG BUỘC 1: Nếu hợp đồng đã hoặc đang diễn ra (StartDate <= Hiện tại)
            if (contract.StartDate <= DateTime.Now)
            {
                throw new Exception("Không thể xóa hợp đồng đã hoặc đang trong thời gian hiệu lực. Hãy sử dụng chức năng Hủy.");
            }

            // RÀNG BUỘC 2: Kiểm tra xem đã có suất chiếu nào được tạo cho phim này trong khoảng thời gian hợp đồng chưa
            var hasShowtimes = contract.Movie.Showtimes.Any(s => s.StartTime >= contract.StartDate && s.StartTime <= contract.EndDate);
            if (hasShowtimes)
            {
                throw new Exception("Hợp đồng này đã có các suất chiếu được lập lịch, không thể xóa trực tiếp.");
            }

            _context.ScreeningContracts.Remove(contract);
            await _context.SaveChangesAsync();
            return true;
        }
        private async Task NotifyProducerAsync(int producerId)
        {
            // Logic gửi email hoặc thông báo hệ thống
        }
    }
}