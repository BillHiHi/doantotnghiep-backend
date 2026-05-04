using System;
using System.Collections.Generic;
using System.Linq;

namespace doantotnghiep_api.Config
{
    /// <summary>
    /// Cấu hình khung giờ vàng cho từng loại ngày
    /// </summary>
    public class GoldenHourConfig
    {
        /// <summary>
        /// Khung giờ vàng trong một ngày
        /// </summary>
        public class TimeSlot
        {
            public int StartHour { get; set; }  // 18
            public int EndHour { get; set; }    // 22
            public string Name { get; set; }    // "Tối vàng"
            public double Weight { get; set; }  // 1.0 (bình thường), 1.5 (vàng hơn)
        }

        /// <summary>
        /// Cấu hình cho một loại ngày
        /// </summary>
        public class DayTypeConfig
        {
            public string DayType { get; set; }  // "WEEKDAY", "FRIDAY", "WEEKEND", "HOLIDAY"
            public List<TimeSlot> GoldenHours { get; set; }
            public int TotalShowsPerDay { get; set; }  // Tổng suất trong ngày
            public int GoldenShowsRequired { get; set; }  // Số suất giờ vàng bắt buộc
        }

        // ============ NGÀY THƯỜNG (Thứ 2-5) ============
        public static readonly DayTypeConfig WEEKDAY = new DayTypeConfig
        {
            DayType = "WEEKDAY",
            TotalShowsPerDay = 6,  // 6 suất/ngày
            GoldenShowsRequired = 2,  // ≥ 2 suất vào giờ vàng (18h-22h)
            GoldenHours = new List<TimeSlot>
            {
                new TimeSlot
                {
                    StartHour = 18,
                    EndHour = 22,
                    Name = "Tối vàng",
                    Weight = 1.0  // Bình thường
                }
            }
        };

        // ============ THỨ 6 (Chiều Thứ 6 - Tối) ============
        public static readonly DayTypeConfig FRIDAY = new DayTypeConfig
        {
            DayType = "FRIDAY",
            TotalShowsPerDay = 8,  // 8 suất/ngày
            GoldenShowsRequired = 3,  // ≥ 3 suất vào giờ vàng
            GoldenHours = new List<TimeSlot>
            {
                new TimeSlot
                {
                    StartHour = 17,
                    EndHour = 23,
                    Name = "Chiều-Tối vàng",
                    Weight = 0.8  // Kém hơn tối
                },
                new TimeSlot
                {
                    StartHour = 18,
                    EndHour = 22,
                    Name = "Tối siêu vàng",
                    Weight = 1.5  // Ưu tiên cao hơn
                }
            }
        };

        // ============ THỨ 7 - CHỦ NHẬT (Weekend) ============
        public static readonly DayTypeConfig WEEKEND = new DayTypeConfig
        {
            DayType = "WEEKEND",
            TotalShowsPerDay = 10,  // 10 suất/ngày
            GoldenShowsRequired = 4,  // ≥ 4 suất vào giờ vàng
            GoldenHours = new List<TimeSlot>
            {
                new TimeSlot
                {
                    StartHour = 10,
                    EndHour = 12,
                    Name = "Sáng weekend",
                    Weight = 0.7  // Ít hơn
                },
                new TimeSlot
                {
                    StartHour = 14,
                    EndHour = 16,
                    Name = "Chiều mỏi dỏi",
                    Weight = 0.6
                },
                new TimeSlot
                {
                    StartHour = 17,
                    EndHour = 23,
                    Name = "Tối weekend vàng",
                    Weight = 1.2
                },
                new TimeSlot
                {
                    StartHour = 19,
                    EndHour = 21,
                    Name = "Tối siêu vàng",
                    Weight = 1.8  // HIGHEST - ưu tiên tuyệt đối
                }
            }
        };

        // ============ NGÀY LỄ / NGÀY NGHỈ ============
        public static readonly DayTypeConfig HOLIDAY = new DayTypeConfig
        {
            DayType = "HOLIDAY",
            TotalShowsPerDay = 12,  // 12 suất/ngày
            GoldenShowsRequired = 6,  // ≥ 6 suất vào giờ vàng
            GoldenHours = new List<TimeSlot>
            {
                new TimeSlot
                {
                    StartHour = 9,
                    EndHour = 12,
                    Name = "Sáng lễ",
                    Weight = 0.8
                },
                new TimeSlot
                {
                    StartHour = 13,
                    EndHour = 16,
                    Name = "Trưa-Chiều lễ",
                    Weight = 0.9
                },
                new TimeSlot
                {
                    StartHour = 17,
                    EndHour = 23,
                    Name = "Tối lễ vàng",
                    Weight = 1.3
                },
                new TimeSlot
                {
                    StartHour = 19,
                    EndHour = 21,
                    Name = "Tối siêu vàng",
                    Weight = 2.0  // MAXIMUM - ưu tiên tuyệt đối
                }
            }
        };

        /// <summary>
        /// Danh sách ngày lễ Việt Nam (có thể mở rộng)
        /// </summary>
        public static readonly List<(int Month, int Day, string Name)> VIETNAM_HOLIDAYS = new()
        {
            (1, 1, "Tết Dương lịch"),
            (2, 10, "Tết Nguyên Đán - Mùng 1"),
            (4, 18, "Giỗ Tổ Hùng Vương"),
            (4, 30, "Ngày Thống Nhất"),
            (5, 1, "Ngày Quốc Tế Lao động"),
            (9, 2, "Quốc Khánh"),
            // Có thể thêm ngày lễ khác
        };

        /// <summary>
        /// Lấy cấu hình ngày dựa trên DateTime
        /// </summary>
        public static DayTypeConfig GetConfigForDate(DateTime date)
        {
            // Kiểm tra ngày lễ trước
            if (IsHoliday(date))
                return HOLIDAY;

            // Kiểm tra ngày trong tuần
            DayOfWeek day = date.DayOfWeek;

            return day switch
            {
                DayOfWeek.Friday => FRIDAY,
                DayOfWeek.Saturday => WEEKEND,
                DayOfWeek.Sunday => WEEKEND,
                _ => WEEKDAY  // Thứ 2 đến Thứ 5
            };
        }

        /// <summary>
        /// Kiểm tra xem ngày có phải ngày lễ không
        /// </summary>
        public static bool IsHoliday(DateTime date)
        {
            return VIETNAM_HOLIDAYS.Any(h => h.Month == date.Month && h.Day == date.Day);
        }

        /// <summary>
        /// Lấy tên loại ngày (debug)
        /// </summary>
        public static string GetDayTypeName(DateTime date)
        {
            if (IsHoliday(date))
                return "HOLIDAY";

            return date.DayOfWeek switch
            {
                DayOfWeek.Friday => "FRIDAY",
                DayOfWeek.Saturday => "WEEKEND",
                DayOfWeek.Sunday => "WEEKEND",
                _ => "WEEKDAY"
            };
        }

        /// <summary>
        /// Kiểm tra xem một giờ có phải giờ vàng không
        /// </summary>
        public static bool IsGoldenHour(DateTime dateTime)
        {
            var config = GetConfigForDate(dateTime);
            int hour = dateTime.Hour;

            return config.GoldenHours.Any(slot => hour >= slot.StartHour && hour < slot.EndHour);
        }

        /// <summary>
        /// Tính trọng số cho một giờ cụ thể
        /// </summary>
        public static double GetHourWeight(DateTime dateTime)
        {
            var config = GetConfigForDate(dateTime);
            int hour = dateTime.Hour;

            var matchingSlot = config.GoldenHours.FirstOrDefault(slot =>
                hour >= slot.StartHour && hour < slot.EndHour);

            return matchingSlot?.Weight ?? 0.3;  // Nếu không trong khung vàng, trọng số 0.3
        }
    }
}