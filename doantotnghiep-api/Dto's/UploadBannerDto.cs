namespace doantotnghiep_api.Dto_s
{
    public class UploadBannerDto
    {
        public IFormFile File { get; set; } = default!;

        public string? Title { get; set; }

        public string? Link { get; set; }

        public int OrderIndex { get; set; }
    }
}
