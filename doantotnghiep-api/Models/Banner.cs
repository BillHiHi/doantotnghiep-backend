using System.ComponentModel.DataAnnotations;

namespace doantotnghiep_api.Models
{
    public class Banner
    {
        [Key]
        public int BannerId { get; set; }

        public string ImageUrl { get; set; } = string.Empty;

        public string? Title { get; set; }

        public string? Link { get; set; }

        public int OrderIndex { get; set; } = 0;

        public bool IsActive { get; set; } = true;
    }
}
