using System.ComponentModel.DataAnnotations;

namespace doantotnghiep_api.Dto_s
{
    public class UpdateShowtimeDto
    {
        [Required]
        public int MovieId { get; set; }

        [Required]
        public int ScreenId { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        [Range(0, 1000000)]
        public decimal BasePrice { get; set; }
    }
}
