using System.ComponentModel.DataAnnotations;

namespace doantotnghiep_api.Models
{
    public class Voucher
    {
        [Key]
        public int VoucherId { get; set; }
        
        [Required]
        public string Title { get; set; } = string.Empty;
        
        [Required]
        public string Code { get; set; } = string.Empty;
        
        public int DiscountPercent { get; set; }
        
        public int PointsRequired { get; set; } // Số điểm cần đổi
        
        public string? Description { get; set; }
        
        public string VoucherType { get; set; } = "All"; // All, Ticket, Water
        
        public bool IsActive { get; set; } = true;
        
        public DateTime? ExpiryDate { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
