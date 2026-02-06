using doantotnghiep_api.Data;
using doantotnghiep_api.Dto_s;
using doantotnghiep_api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace doantotnghiep_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BannersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BannersController(AppDbContext context)
        {
            _context = context;
        }

        // =====================================
        // PUBLIC
        // =====================================

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetBanners()
        {
            var banners = await _context.Banners
                .Where(x => x.IsActive)
                .OrderBy(x => x.OrderIndex)
                .ToListAsync();

            return Ok(banners);
        }

        // =====================================
        // ADMIN
        // =====================================

        // UPLOAD
        [HttpPost("upload")]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadBanner([FromForm] UploadBannerDto dto)
        {
            if (dto.File == null || dto.File.Length == 0)
                return BadRequest("No file");

            var uploadsFolder = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "banners"
            );

            Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(dto.File.FileName)}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await dto.File.CopyToAsync(stream);

            var url = $"{Request.Scheme}://{Request.Host}/banners/{fileName}";

            var banner = new Banner
            {
                ImageUrl = url,
                Title = dto.Title,
                Link = dto.Link,
                OrderIndex = dto.OrderIndex
            };

            _context.Banners.Add(banner);
            await _context.SaveChangesAsync();

            return Ok(banner);
        }

        // UPDATE
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateBanner(int id, [FromBody] Banner banner)
        {
            if (id != banner.BannerId)
                return BadRequest();

            _context.Entry(banner).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteBanner(int id)
        {
            var banner = await _context.Banners.FindAsync(id);

            if (banner == null)
                return NotFound();

            _context.Banners.Remove(banner);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
