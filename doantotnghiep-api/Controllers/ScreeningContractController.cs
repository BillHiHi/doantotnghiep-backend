using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using doantotnghiep_api.Data;
using doantotnghiep_api.Models;
using doantotnghiep_api.DTOs;
using doantotnghiep_api.Services;
using doantotnghiep_api.Dto_s;

namespace doantotnghiep_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin, SUPER_ADMIN")]
    public class ScreeningContractsController : ControllerBase
    {
        private readonly ContractService _contractService;
        private readonly AppDbContext _context;

        public ScreeningContractsController(ContractService contractService, AppDbContext context)
        {
            _contractService = contractService;
            _context = context;
        }

        // POST: api/ScreeningContracts
        [HttpPost]
        public async Task<ActionResult<ContractResponse>> CreateContract(CreateContractRequest request)
        {
            // Giữ nguyên validate tổng slot phân bổ...
            int totalAllocated = request.TheaterAllocations?.Sum(t => t.AllocatedSlots) ?? 0;
            if (totalAllocated != request.TotalSlots)
                return BadRequest(new { message = $"Tổng slot phân bổ ({totalAllocated}) phải bằng TotalSlots hợp đồng ({request.TotalSlots})." });

            try
            {
                var contract = new ScreeningContract
                {
                    MovieId = request.MovieId,
                    ProducerId = request.ProducerId,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    TotalSlots = request.TotalSlots,
                    // THÊM DÒNG NÀY:
                    GoldHourPercentage = request.GoldHourPercentage,
                    CreatedAt = DateTime.Now,
                    ContractTheaters = request.TheaterAllocations.Select(t => new doantotnghiep_api.Models.ContractTheater
                    {
                        TheaterId = t.TheaterId,
                        AllocatedSlots = t.AllocatedSlots
                    }).ToList()
                };

                var result = await _contractService.CreateContractAsync(contract);

                return Ok(new ContractResponse
                {
                    ContractId = result.ContractId,
                    StartDate = result.StartDate,
                    EndDate = result.EndDate,
                    TotalSlots = result.TotalSlots,
                    // THÊM CÁC DÒNG NÀY ĐỂ TRẢ VỀ KẾT QUẢ TÍNH TOÁN:
                    GoldHourSlots = result.RequiredGoldHourSlots,
                    RegularSlots = result.TotalSlots - result.RequiredGoldHourSlots,
                    Status = result.Status
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // POST: api/ScreeningContracts/with-new-movie
        [HttpPost("with-new-movie")]
        public async Task<ActionResult<ContractResponse>> CreateMovieAndContract(CreateMovieAndContractRequest request)
        {
            try
            {
                var movie = new Movie { /* Giữ nguyên mapping phim */ };

                var contract = new ScreeningContract
                {
                    ProducerId = request.ProducerId,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    TotalSlots = request.TotalSlots,
                    // THÊM DÒNG NÀY:
                    GoldHourPercentage = request.GoldHourPercentage
                };

                var result = await _contractService.CreateMovieAndContractAsync(movie, contract);

                return Ok(new ContractResponse
                {
                    ContractId = result.ContractId,
                    MovieTitle = movie.Title,
                    GoldHourSlots = result.RequiredGoldHourSlots, // Trả về thông tin KPI
                    Status = result.Status
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpGet("progress")]
        public async Task<IActionResult> GetContractsProgress()
        {
            var now = DateTime.Now;
            var contracts = await _context.ScreeningContracts
                .Include(c => c.Movie)
                .Include(c => c.Producer)
                .Include(c => c.ContractTheaters)
                    .ThenInclude(ct => ct.Theater)
                .ToListAsync();

            // Lấy danh sách suất chiếu để tính toán thực tế
            var allShowtimes = await _context.Showtimes.ToListAsync();

            var response = contracts.Select(c =>
            {
                // 1. Tính toán thực tế tổng quát
                int totalUsed = allShowtimes.Count(s => s.MovieId == c.MovieId && s.StartTime >= c.StartDate && s.StartTime <= c.EndDate);

                // 2. TÍNH TOÁN THỰC TẾ GIỜ VÀNG (18h-22h)
                int usedGoldSlots = allShowtimes.Count(s =>
                    s.MovieId == c.MovieId &&
                    s.StartTime >= c.StartDate && s.StartTime <= c.EndDate &&
                    s.StartTime.Hour >= 18 && s.StartTime.Hour < 22);

                int totalDays = Math.Max((c.EndDate - c.StartDate).Days, 1);
                double progressPercent = Math.Round((double)totalUsed / c.TotalSlots * 100, 2);

                // Tính % tiến độ giờ vàng
                double goldProgressPercent = c.RequiredGoldHourSlots > 0
                    ? Math.Round((double)usedGoldSlots / c.RequiredGoldHourSlots * 100, 2) : 100;

                return new ContractProgressResponse
                {
                    ContractId = c.ContractId,
                    MovieTitle = c.Movie?.Title,
                    TotalSlots = c.TotalSlots,
                    UsedSlots = totalUsed,
                    RemainingSlots = Math.Max(c.TotalSlots - totalUsed, 0),
                    ProgressPercent = progressPercent,

                    // THÔNG TIN GIỜ VÀNG CHI TIẾT
                    GoldHourSlots = c.RequiredGoldHourSlots,
                    UsedGoldHourSlots = usedGoldSlots,
                    RemainingGoldHourSlots = Math.Max(c.RequiredGoldHourSlots - usedGoldSlots, 0),
                    GoldHourProgressPercent = goldProgressPercent,

                    StartDate = c.StartDate,
                    EndDate = c.EndDate,
                    Status = now > c.EndDate ? "Expired" : (totalUsed >= c.TotalSlots ? "Completed" : "Active"),
                    IsBehindSchedule = progressPercent < 50 && (c.EndDate - now).TotalDays < (totalDays / 2) // Ví dụ logic báo chậm
                };
            });

            return Ok(response);
        }

        // DELETE: api/ScreeningContracts/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteContract(int id)
        {
            try
            {
                // Gọi logic kiểm tra và xóa từ Service
                await _contractService.DeleteContractAsync(id);

                return Ok(new
                {
                    message = "Xóa hợp đồng thành công.",
                    contractId = id
                });
            }
            catch (Exception ex)
            {
                // Trả về thông báo lỗi cụ thể từ các ràng buộc ở Service
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }

        // PATCH: api/ScreeningContracts/5/cancel
        [HttpPatch("{id}/cancel")]
        public async Task<IActionResult> CancelContract(int id)
        {
            try
            {
                // Gọi logic từ service
                await _contractService.CancelContractAsync(id);

                return Ok(new
                {
                    message = "Hủy hợp đồng thành công.",
                    contractId = id,
                    cancelledAt = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                // Trả về lỗi nếu không tìm thấy hoặc vi phạm logic nghiệp vụ (ví dụ: hợp đồng đã chạy)
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}