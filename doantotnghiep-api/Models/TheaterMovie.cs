namespace doantotnghiep_api.Models
{
    public class TheaterMovie
    {
        public int MovieId { get; set; }
        public Movie Movie { get; set; }
        public int TheaterId { get; set; }
        public Theater Theater { get; set; }
    }
}
