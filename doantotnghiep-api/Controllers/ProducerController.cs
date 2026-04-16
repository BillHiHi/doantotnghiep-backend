using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using doantotnghiep_api.Data; // Thay bằng namespace DbContext của bạn
using doantotnghiep_api.Models;
using doantotnghiep_api.Dto_s;

namespace doantotnghiep_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin, SUPER_ADMIN")] // Chỉ Admin mới có quyền truy cập các API này
    public class ProducersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProducersController(AppDbContext context)
        {
            _context = context;
        }

        // 1. GET: api/Producers (Lấy danh sách)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProducerResponse>>> GetProducers()
        {
            var producers = await _context.Producers
                .Include(p => p.Movies)
                .Include(p => p.Contracts)
                .Select(p => new ProducerResponse
                {
                    ProducerId = p.ProducerId,
                    Name = p.Name,
                    Email = p.Email,
                    PhoneNumber = p.PhoneNumber,
                    TotalMovies = p.Movies.Count,
                    TotalContracts = p.Contracts.Count
                })
                .ToListAsync();

            return Ok(producers);
        }

        // 2. GET: api/Producers/5 (Lấy chi tiết)
        [HttpGet("{id}")]
        public async Task<ActionResult<ProducerResponse>> GetProducer(int id)
        {
            var producer = await _context.Producers
                .Include(p => p.Movies)
                .Include(p => p.Contracts)
                .FirstOrDefaultAsync(p => p.ProducerId == id);

            if (producer == null) return NotFound("Không tìm thấy nhà sản xuất");

            return Ok(new ProducerResponse
            {
                ProducerId = producer.ProducerId,
                Name = producer.Name,
                Email = producer.Email,
                PhoneNumber = producer.PhoneNumber,
                TotalMovies = producer.Movies.Count,
                TotalContracts = producer.Contracts.Count
            });
        }

        // 3. POST: api/Producers (Thêm mới)
        [HttpPost]
        public async Task<ActionResult<ProducerResponse>> CreateProducer(CreateProducerRequest request)
        {
            var producer = new Producer
            {
                Name = request.Name,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber
            };

            _context.Producers.Add(producer);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProducer), new { id = producer.ProducerId }, new ProducerResponse
            {
                ProducerId = producer.ProducerId,
                Name = producer.Name,
                Email = producer.Email,
                PhoneNumber = producer.PhoneNumber
            });
        }

        // 4. PUT: api/Producers/5 (Cập nhật)
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProducer(int id, UpdateProducerRequest request)
        {
            var producer = await _context.Producers.FindAsync(id);
            if (producer == null) return NotFound("Không tìm thấy nhà sản xuất");

            producer.Name = request.Name;
            producer.Email = request.Email;
            producer.PhoneNumber = request.PhoneNumber;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProducerExists(id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        // 5. DELETE: api/Producers/5 (Xóa)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProducer(int id)
        {
            var producer = await _context.Producers
                .Include(p => p.Movies)
                .FirstOrDefaultAsync(p => p.ProducerId == id);

            if (producer == null) return NotFound("Không tìm thấy nhà sản xuất");

            // Logic an toàn: Không cho xóa nếu đang có phim thuộc nhà sản xuất này
            if (producer.Movies.Any())
            {
                return BadRequest("Không thể xóa nhà sản xuất đang có phim trong hệ thống. Hãy xóa phim hoặc chuyển phim sang NSX khác trước.");
            }

            _context.Producers.Remove(producer);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ProducerExists(int id)
        {
            return _context.Producers.Any(e => e.ProducerId == id);
        }
    }
}