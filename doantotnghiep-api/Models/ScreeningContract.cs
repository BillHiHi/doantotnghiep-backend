using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace doantotnghiep_api.Models
{
    public class ScreeningContract
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ContractId { get; set; }

        [Required]
        public int ProducerId { get; set; }

        [Required]
        public int MovieId { get; set; }

        [Required]
        public int TotalSlots { get; set; }

        // --- CẬP NHẬT MỚI ---

        [Required]
        [Range(0, 100)]
        public int GoldHourPercentage { get; set; } = 30; // Tỷ lệ % do NSX nhập (mặc định 30)

        [Required]
        public int RequiredGoldHourSlots { get; set; } // Số suất vàng mục tiêu (TotalSlots * % / 100)

        // ---------------------

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Active"; // Active, Completed, Cancelled

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Foreign Keys logic
        [ForeignKey("ProducerId")]
        public virtual Producer Producer { get; set; } = null!;

        [ForeignKey("MovieId")]
        public virtual Movie Movie { get; set; } = null!;

        public virtual ICollection<ContractTheater> ContractTheaters { get; set; } = new List<ContractTheater>();
    }
}