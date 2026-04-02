using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using doantotnghiep_api.Data;
using doantotnghiep_api.Models;

namespace doantotnghiep_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,SUPER_ADMIN")]
    public class PromotionController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PromotionController(AppDbContext context)
        {
            _context = context;
        }

        // ============================
        // 1. LẤY DANH SÁCH KHUYẾN MÃI
        // ============================
        [HttpGet]
        public async Task<IActionResult> GetAllPromotions()
        {
            var promotions = await _context.Promotions
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return Ok(promotions);
        }

        // ============================
        // 2. LẤY CHI TIẾT 1 KHUYẾN MÃI
        // ============================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPromotion(int id)
        {
            var promotion = await _context.Promotions.FindAsync(id);

            if (promotion == null)
                return NotFound("Không tìm thấy bài khuyến mãi.");

            return Ok(promotion);
        }

        // ============================
        // 3. THÊM KHUYẾN MÃI
        // ============================
        [HttpPost]
        public async Task<IActionResult> CreatePromotion(Promotion promotion)
        {
            promotion.CreatedAt = DateTime.UtcNow;

            _context.Promotions.Add(promotion);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Thêm khuyến mãi thành công",
                data = promotion
            });
        }

        // ============================
        // 4. CẬP NHẬT KHUYẾN MÃI
        // ============================
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePromotion(int id, Promotion updatedPromotion)
        {
            var promotion = await _context.Promotions.FindAsync(id);

            if (promotion == null)
                return NotFound("Không tìm thấy bài khuyến mãi.");

            promotion.Title = updatedPromotion.Title;
            promotion.Summary = updatedPromotion.Summary;
            promotion.Content = updatedPromotion.Content;
            promotion.ImageUrl = updatedPromotion.ImageUrl;
            promotion.StartDate = updatedPromotion.StartDate;
            promotion.EndDate = updatedPromotion.EndDate;
            promotion.IsPublished = updatedPromotion.IsPublished;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Cập nhật khuyến mãi thành công",
                data = promotion
            });
        }

        // ============================
        // 5. XOÁ KHUYẾN MÃI
        // ============================
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePromotion(int id)
        {
            var promotion = await _context.Promotions.FindAsync(id);

            if (promotion == null)
                return NotFound("Không tìm thấy bài khuyến mãi.");

            _context.Promotions.Remove(promotion);
            await _context.SaveChangesAsync();

            return Ok("Xoá khuyến mãi thành công");
        }
    }
}