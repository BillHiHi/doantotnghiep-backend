using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace doantotnghiep_api.Models
{
    public class PointTransaction
    {
        [Key]
        public int TransactionId { get; set; }
        
        [Required]
        public int UserId { get; set; }
        
        [Required]
        public int Points { get; set; } // Số điểm cộng hoặc trừ
        
        [Required]
        public string Description { get; set; } = string.Empty;
        
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }
    }
}
