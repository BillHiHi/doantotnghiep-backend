using System.ComponentModel.DataAnnotations;

namespace doantotnghiep_api.Models
{
    public class SeatLock
    {
        [Key]
        public int LockId { get; set; }

        public int ShowtimeId { get; set; }
        public int SeatId { get; set; }
        public int UserId { get; set; }
        public DateTime LockedAt { get; set; }

        public DateTime ExpiryTime { get; set; }
    }
}
