namespace doantotnghiep_api.Dto_s
{
    public class GenerateAutoDto
    {
        public int TheaterId { get; set; }
        public DateTime TargetDate { get; set; }
        public List<int>? MovieIds { get; set; }
    }
}
