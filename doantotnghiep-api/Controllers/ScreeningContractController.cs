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
            // Validate tổng slot phân bổ
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
                    CreatedAt = DateTime.Now,
                    // Sửa lỗi tại đây: Thêm namespace đầy đủ của Models vào trước ContractTheater
                    ContractTheaters = request.TheaterAllocations.Select(t => new doantotnghiep_api.Models.ContractTheater
                    {
                        TheaterId = t.TheaterId,
                        AllocatedSlots = t.AllocatedSlots
                    }).ToList()
                };

                var result = await _contractService.CreateContractAsync(contract);

                var movie = await _context.Movies.FindAsync(result.MovieId);
                var producer = await _context.Producers.FindAsync(result.ProducerId);

                return Ok(new ContractResponse
                {
                    ContractId = result.ContractId,
                    MovieTitle = movie?.Title,
                    ProducerName = producer?.Name,
                    StartDate = result.StartDate,
                    EndDate = result.EndDate,
                    TotalSlots = result.TotalSlots,
                    Status = "Active"
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
                var movie = new Movie
                {
                    Title = request.MovieTitle,
                    Duration = request.Duration,
                    Genre = request.Genre,
                    ReleaseDate = request.ReleaseDate,
                    ProducerId = request.ProducerId, // Cần ProducerId để mapping chính xác
                    Status = "Coming Soon"
                };

                var contract = new ScreeningContract
                {
                    ProducerId = request.ProducerId,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    TotalSlots = request.TotalSlots
                };

                var result = await _contractService.CreateMovieAndContractAsync(movie, contract);

                return Ok(new ContractResponse
                {
                    ContractId = result.ContractId,
                    MovieTitle = movie.Title,
                    Status = "Active"
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

            var response = contracts.Select(c =>
            {
                // Breakdown từng rạp
                var theaterBreakdowns = c.ContractTheaters.Select(ct =>
                {
                    int usedSlots = _context.Showtimes.Count(s =>
                        s.MovieId == c.MovieId &&
                        s.Screen.TheaterId == ct.TheaterId &&
                        s.StartTime >= c.StartDate &&
                        s.StartTime <= c.EndDate);

                    return new TheaterSlotBreakdown
                    {
                        TheaterId = ct.TheaterId,
                        TheaterName = ct.Theater?.Name,
                        AllocatedSlots = ct.AllocatedSlots,
                        UsedSlots = usedSlots,
                        RemainingSlots = ct.AllocatedSlots - usedSlots
                    };
                }).ToList();

                int totalUsed = theaterBreakdowns.Sum(t => t.UsedSlots);
                int totalDays = (c.EndDate - c.StartDate).Days;
                int daysRemaining = Math.Max((c.EndDate - now).Days, 0);
                double progressPercent = c.TotalSlots > 0
                    ? Math.Round((double)totalUsed / c.TotalSlots * 100, 2) : 0;
                bool isBehindSchedule = totalDays > 0 &&
                    progressPercent < (1 - (double)daysRemaining / totalDays) * 100;

                // Status rõ ràng hơn
                string status = now > c.EndDate ? "Expired"
                    : totalUsed >= c.TotalSlots ? "Exhausted"
                    : "Active";

                return new ContractProgressResponse
                {
                    ContractId = c.ContractId,
                    MovieTitle = c.Movie?.Title,
                    ProducerName = c.Producer?.Name,
                    TotalSlots = c.TotalSlots,
                    UsedSlots = totalUsed,
                    RemainingSlots = c.TotalSlots - totalUsed,
                    ProgressPercent = progressPercent,
                    StartDate = c.StartDate,
                    EndDate = c.EndDate,
                    Status = status,
                    IsBehindSchedule = isBehindSchedule,
                    TheaterBreakdowns = theaterBreakdowns  // ← chi tiết từng rạp
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
    }
}