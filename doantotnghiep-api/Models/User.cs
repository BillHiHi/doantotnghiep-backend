using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace doantotnghiep_api.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public string? FullName { get; set; }

        public string? PhoneNumber { get; set; }

        public string Role { get; set; } = "User";
        public int? TheaterId { get; set; }

        [ForeignKey("TheaterId")]
        public virtual Theater? Theater { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
