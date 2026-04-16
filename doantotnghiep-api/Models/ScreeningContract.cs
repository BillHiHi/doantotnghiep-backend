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
        public int TotalSlots { get; set; } // Ví dụ: 100

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Foreign Keys logic
        [ForeignKey("ProducerId")]
        public virtual Producer Producer { get; set; } = null!;

        [ForeignKey("MovieId")]
        public virtual Movie Movie { get; set; } = null!;
    }
}
