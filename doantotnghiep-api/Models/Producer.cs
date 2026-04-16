using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace doantotnghiep_api.Models
{
    public class Producer
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ProducerId { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Email { get; set; }

        public string? PhoneNumber { get; set; }

        // Navigation properties
        public virtual ICollection<Movie> Movies { get; set; } = new List<Movie>();
        public virtual ICollection<ScreeningContract> Contracts { get; set; } = new List<ScreeningContract>();
    }
}
