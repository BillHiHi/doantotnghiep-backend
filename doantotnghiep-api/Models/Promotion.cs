using System.ComponentModel.DataAnnotations;

namespace doantotnghiep_api.Models
{
    public class Promotion
    {
        [Key]
        public int PromotionId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; }   // Tiêu đề

        public string? Summary { get; set; }   // Mô tả ngắn (hiển thị card)

        public string? Content { get; set; }   // Nội dung chi tiết

        public string? ImageUrl { get; set; }  // Ảnh banner

        public DateTime StartDate { get; set; }  // Ngày bắt đầu

        public DateTime EndDate { get; set; }    // Ngày kết thúc

        public bool IsPublished { get; set; } = true;  // Có hiển thị hay không

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}