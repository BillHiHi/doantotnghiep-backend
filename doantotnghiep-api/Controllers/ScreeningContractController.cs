using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using doantotnghiep_api.Data;
using doantotnghiep_api.Models;
using doantotnghiep_api.DTOs;

namespace doantotnghiep_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class ScreeningContractsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ScreeningContractsController(AppDbContext context)
        {
            _context = context;
        }

        // 1. POST: api/ScreeningContracts (Tạo hợp đồng mới)
        [HttpPost]
        public async Task<ActionResult<ContractResponse>> CreateContract(CreateContractRequest request)
        {
            // Kiểm tra phim và NSX có tồn tại không
            var movie = await _context.Movies.FindAsync(request.MovieId);
            var producer = await _context.Producers.FindAsync(request.ProducerId);

            if (movie == null || producer == null)
                return BadRequest("Thông tin Phim hoặc Nhà sản xuất không hợp lệ.");

            if (request.EndDate <= request.StartDate)
                return BadRequest("Ngày kết thúc phải sau ngày bắt đầu.");

            // Tính toán logic nghiệp vụ
            int durationDays = (request.EndDate - request.StartDate).Days + 1;
            int goldHourSlots = (int)Math.Round((request.TotalSlots * request.GoldHourPercentage) / 100.0);
            int regularSlots = request.TotalSlots - goldHourSlots;

            var contract = new ScreeningContract
            {
                MovieId = request.MovieId,
                ProducerId = request.ProducerId,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                TotalSlots = request.TotalSlots,
                // Giả sử bạn đã thêm các field này vào Model ScreeningContract như trao đổi trước đó
                CreatedAt = DateTime.Now
            };

            _context.ScreeningContracts.Add(contract);
            await _context.SaveChangesAsync();

            return Ok(new ContractResponse
            {
                ContractId = contract.ContractId,
                MovieTitle = movie.Title,
                ProducerName = producer.Name,
                StartDate = contract.StartDate,
                EndDate = contract.EndDate,
                TotalSlots = contract.TotalSlots,
                GoldHourSlots = goldHourSlots,
                RegularSlots = regularSlots,
                DurationDays = durationDays,
                AverageSlotsPerDay = Math.Round((double)request.TotalSlots / durationDays, 2),
                CreatedAt = contract.CreatedAt,
                Status = "Active"
            });
        }

        // 2. GET: api/ScreeningContracts/progress (Lấy danh sách kèm tiến độ)
        [HttpGet("progress")]
        public async Task<ActionResult<IEnumerable<ContractProgressResponse>>> GetContractsProgress()
        {
            var now = DateTime.Now;

            // Lấy tất cả hợp đồng và include Showtimes để đếm suất thực tế
            var contracts = await _context.ScreeningContracts
                .Include(c => c.Movie)
                .ThenInclude(m => m.Showtimes) // Giả sử Movie có ICollection<Showtime>
                .Include(c => c.Producer)
                .ToListAsync();

            var response = contracts.Select(c =>
            {
                // Đếm số suất thực tế đã lên lịch trong khoảng thời gian hợp đồng
                int usedSlots = c.Movie.Showtimes
                    .Count(s => s.StartTime >= c.StartDate && s.StartTime <= c.EndDate);

                int durationDays = (c.EndDate - c.StartDate).Days + 1;
                int remainingSlots = Math.Max(0, c.TotalSlots - usedSlots);
                double progressPercent = Math.Round((double)usedSlots / c.TotalSlots * 100, 2);

                // Tính toán xem có bị chậm tiến độ không
                // Logic: % thời gian đã trôi qua > % suất đã chiếu
                double timeElapsedPercent = 0;
                if (now > c.StartDate)
                {
                    double totalTime = (c.EndDate - c.StartDate).TotalMinutes;
                    double elapsed = (now - c.StartDate).TotalMinutes;
                    timeElapsedPercent = Math.Min(100, (elapsed / totalTime) * 100);
                }

                bool isBehind = timeElapsedPercent > progressPercent && progressPercent < 100;

                // Số suất cần thêm mỗi ngày để hoàn thành đúng hạn
                int remainingDays = Math.Max(1, (c.EndDate - now).Days + 1);
                int slotsNeeded = (int)Math.Ceiling((double)remainingSlots / remainingDays);

                return new ContractProgressResponse
                {
                    ContractId = c.ContractId,
                    MovieTitle = c.Movie.Title,
                    ProducerName = c.Producer.Name,
                    StartDate = c.StartDate,
                    EndDate = c.EndDate,
                    TotalSlots = c.TotalSlots,
                    DurationDays = durationDays,
                    UsedSlots = usedSlots,
                    RemainingSlots = remainingSlots,
                    ProgressPercent = progressPercent,
                    IsBehindSchedule = isBehind,
                    SlotsNeededPerDayToComplete = remainingSlots > 0 ? slotsNeeded : 0,
                    AverageSlotsPerDay = Math.Round((double)c.TotalSlots / durationDays, 2),
                    Status = now > c.EndDate ? "Expired" : (now < c.StartDate ? "Pending" : "In Progress")
                };
            });

            return Ok(response);
        }
    }
}