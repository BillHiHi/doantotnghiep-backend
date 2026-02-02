using System.ComponentModel.DataAnnotations;

namespace doantotnghiep_api.Dto_s
{
    public class UploadPosterDto
    {
        [Required]
        public IFormFile File { get; set; } = default!;
    }
}
