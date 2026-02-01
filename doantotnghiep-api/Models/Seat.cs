using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace doantotnghiep_api.Models
{
    public class Seat
    {
        [Key]
        public int SeatId { get; set; }

        [Required]
        public int ScreenId { get; set; }

        [Required]
        [StringLength(5)]
        public string RowNumber { get; set; } // Ví dụ: A, B, C...

        [Required]
        public int SeatNumber { get; set; } // Ví dụ: 1, 2, 3...

        [Required]
        [StringLength(20)]
        public string SeatType { get; set; } // Standard, Premium, VIP

        public Screen Screen { get; set; } = null!; // ⭐ thêm


    }
}
