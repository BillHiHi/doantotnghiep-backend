namespace doantotnghiep_api.Dto_s
{
    public class MovieDTO
    {
        public int MovieId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? PosterUrl { get; set; }
        public int Duration { get; set; }
        public string? Genre { get; set; }
    }
}
