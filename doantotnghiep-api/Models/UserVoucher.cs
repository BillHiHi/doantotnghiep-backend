using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace doantotnghiep_api.Models
{
    public class UserVoucher
    {
        [Key]
        public int Id { get; set; }
        
        public int UserId { get; set; }
        
        public int VoucherId { get; set; }
        
        public bool IsUsed { get; set; } = false;
        
        public DateTime RedeemedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UsedAt { get; set; } // ⭐ NEW: Ngày sử dụng voucher

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        [ForeignKey("VoucherId")]
        public virtual Voucher? Voucher { get; set; }
    }
}
