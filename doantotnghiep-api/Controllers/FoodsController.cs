using doantotnghiep_api.Data;
using doantotnghiep_api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace doantotnghiep_api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FoodsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public FoodsController(AppDbContext context)
        {
            _context = context;
        }

        // =========================
        // 1. Lấy tất cả món ăn
        // =========================
        [HttpGet]
        public async Task<IActionResult> GetAllFoods()
        {
            var foods = await _context.Foods.ToListAsync();
            return Ok(foods);
        }

        // =========================
        // 2. Lấy món theo ID
        // =========================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetFoodById(int id)
        {
            var food = await _context.Foods.FindAsync(id);

            if (food == null)
                return NotFound(new { message = "Không tìm thấy món ăn" });

            return Ok(food);
        }

        // =========================
        // 3. Thêm món ăn
        // =========================
        [HttpPost]
        public async Task<IActionResult> CreateFood(Foods food)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await _context.Foods.AddAsync(food);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Thêm món ăn thành công", data = food });
        }

        // =========================
        // 4. Cập nhật món ăn
        // =========================
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateFood(int id, Foods updatedFood)
        {
            if (id != updatedFood.FoodId)
                return BadRequest(new { message = "ID không khớp" });

            var food = await _context.Foods.FindAsync(id);

            if (food == null)
                return NotFound(new { message = "Không tìm thấy món ăn" });

            food.Name = updatedFood.Name;
            food.Description = updatedFood.Description;
            food.ImageUrl = updatedFood.ImageUrl;
            food.Price = updatedFood.Price;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Cập nhật thành công", data = food });
        }

        // =========================
        // 5. Xóa món ăn
        // =========================
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFood(int id)
        {
            var food = await _context.Foods.FindAsync(id);

            if (food == null)
                return NotFound(new { message = "Không tìm thấy món ăn" });

            _context.Foods.Remove(food);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Xóa món ăn thành công" });
        }
    }
}