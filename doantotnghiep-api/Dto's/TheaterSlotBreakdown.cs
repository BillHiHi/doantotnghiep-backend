namespace doantotnghiep_api.Dto_s
{
    public class TheaterSlotBreakdown
    {
        public int TheaterId { get; set; }
        public string? TheaterName { get; set; }
        public int AllocatedSlots { get; set; }
        public int UsedSlots { get; set; }
        public int RemainingSlots { get; set; }
    }
}
