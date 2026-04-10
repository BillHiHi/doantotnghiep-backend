using System.ComponentModel.DataAnnotations;

namespace doantotnghiep_api.Models
{
    public class SeatLock
    {
        [Key]
        public int LockId { get; set; }

        public int ShowtimeId { get; set; }
        public int SeatId { get; set; }
        public int UserId { get; set; }
        public DateTime LockedAt { get; set; }

        public DateTime ExpiryTime { get; set; }

        public string? PaymentCode { get; set; }

        public decimal? TotalAmount { get; set; }

        public string? Combos { get; set; } // ⭐ Lưu danh sách combo bắp nước (JSON)
        
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public int? UserVoucherId { get; set; } // ⭐ Lưu ID voucher người dùng áp dụng
    }
}
