using doantotnghiep_api.Data;
using doantotnghiep_api.Models;
using Microsoft.EntityFrameworkCore;

namespace doantotnghiep_api.Services
{
    public class ShowtimeAutomationService
    {
        private readonly AppDbContext _context;
        private const int CleaningTimeMinutes = 20; // Thời gian dọn dẹp giữa các suất

        public ShowtimeAutomationService(AppDbContext context) => _context = context;

        /// <summary>
        /// Tự động tạo lịch chiếu dựa trên hợp đồng với logic KPI-based
        /// </summary>
        public async Task<ShowtimeGenerationResult> GenerateShowtimesAsync(int theaterId, DateTime targetDate)
        {
            var result = new ShowtimeGenerationResult
            {
                TheaterId = theaterId,
                TargetDate = targetDate,
                CreatedShowtimes = new List<ShowtimeInfo>(),
                Errors = new List<string>()
            };

            try
            {
                // 1. Lấy các hợp đồng còn hiệu lực cho ngày target (có phân bổ cho theater này)
                var activeContracts = await GetActiveContractsForTheater(theaterId, targetDate);

                if (!activeContracts.Any())
                {
                    result.Errors.Add("Không có hợp đồng nào còn hiệu lực cho ngày này tại rạp này");
                    return result;
                }

                // 2. Lấy danh sách phòng của rạp
                var screens = await _context.Screens
                    .Where(s => s.TheaterId == theaterId)
                    .OrderBy(s => s.ScreenId)
                    .ToListAsync();

                if (!screens.Any())
                {
                    result.Errors.Add("Rạp không có phòng chiếu nào");
                    return result;
                }

                // 3. Định nghĩa khung giờ hoạt động (08:00 - 23:30)
                var openingTime = targetDate.Date.AddHours(8);
                var closingTime = targetDate.Date.AddHours(23).AddMinutes(30);

                // 4. Tính toán target cho mỗi hợp đồng (dùng for loop để await async)
                var contractTargets = new List<ContractTarget>();
                foreach (var c in activeContracts)
                {
                    var dailyTarget = CalculateDailyTarget(c);
                    var slotsToCreate = await GetSlotsToCreateToday(c, targetDate);
                    var isBehind = await IsBehindSchedule(c, targetDate);
                    var allocated = await GetAllocatedSlotsForTheaterAsync(c, theaterId);

                    contractTargets.Add(new ContractTarget
                    {
                        Contract = c,
                        DailyTarget = dailyTarget,
                        SlotsToCreateToday = slotsToCreate,
                        IsBehindSchedule = isBehind,
                        AllocatedSlotsForThisTheater = allocated
                    });
                }

                // 5. Sắp xếp ưu tiên: BehindSchedule trước, sau đó theo slots còn lại nhiều nhất
                var prioritizedContracts = contractTargets
                    .Where(ct => ct.SlotsToCreateToday > 0 && ct.AllocatedSlotsForThisTheater > 0)
                    .OrderByDescending(ct => ct.IsBehindSchedule)
                    .ThenByDescending(ct => ct.SlotsToCreateToday)
                    .ToList();

                // 6. Gap Finder: Tìm slot trống cho từng phòng
                foreach (var screen in screens)
                {
                    var availableSlots = await FindAvailableTimeSlots(
                        screen.ScreenId,
                        targetDate,
                        openingTime,
                        closingTime
                    );

                    // 7. Lấp đầy các slot trống với các hợp đồng ưu tiên
                    foreach (var timeSlot in availableSlots)
                    {
                        // Kiểm tra từng hợp đồng theo thứ tự ưu tiên
                        foreach (var target in prioritizedContracts.ToList())
                        {
                            if (target.SlotsToCreateToday <= 0) continue;

                            // Kiểm tra còn allocated slots cho rạp này không
                            if (target.AllocatedSlotsForThisTheater <= 0) continue;

                            // Kiểm tra thời gian có đủ cho phim không
                            var movieDuration = target.Contract.Movie?.Duration ?? 120;
                            var slotDuration = (timeSlot.EndTime - timeSlot.StartTime).TotalMinutes;

                            if (slotDuration < movieDuration + CleaningTimeMinutes)
                            {
                                continue; // Slot không đủ dài
                            }

                            // Tạo showtime
                            var showtime = await CreateShowtime(
                                target.Contract,
                                screen.ScreenId,
                                timeSlot.StartTime,
                                movieDuration
                            );

                            if (showtime != null)
                            {
                                result.CreatedShowtimes.Add(new ShowtimeInfo
                                {
                                    ShowtimeId = showtime.ShowtimeId,
                                    MovieTitle = target.Contract.Movie?.Title ?? "Unknown",
                                    ScreenId = screen.ScreenId,
                                    StartTime = showtime.StartTime,
                                    EndTime = showtime.EndTime,
                                    ContractId = target.Contract.ContractId
                                });

                                // Cập nhật counters
                                target.SlotsToCreateToday--;
                                target.AllocatedSlotsForThisTheater--;

                                // Đánh dấu slot đã được sử dụng
                                timeSlot.IsUsed = true;
                                break; // Chuyển sang slot tiếp theo
                            }
                        }
                    }
                }

                // 8. Lưu thay đổi với Transaction
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }

                result.TotalCreated = result.CreatedShowtimes.Count;
                result.Success = result.TotalCreated > 0;

                if (result.TotalCreated == 0)
                {
                    result.Errors.Add("Không thể tạo thêm suất chiếu nào - Có thể do không đủ slot trống hoặc hợp đồng đã đạt chỉ tiêu");
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Lỗi: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Tìm các khoảng thời gian trống trong phòng chiếu
        /// </summary>
        private async Task<List<TimeSlot>> FindAvailableTimeSlots(int screenId, DateTime date, DateTime openingTime, DateTime closingTime)
        {
            var slots = new List<TimeSlot>();

            // Lấy các showtime hiện có trong phòng cho ngày này
            var existingShowtimes = await _context.Showtimes
                .Where(s => s.ScreenId == screenId
                    && s.StartTime.Date == date.Date)
                .OrderBy(s => s.StartTime)
                .ToListAsync();

            if (!existingShowtimes.Any())
            {
                // Toàn bộ ngày trống
                slots.Add(new TimeSlot { StartTime = openingTime, EndTime = closingTime, IsUsed = false });
                return slots;
            }

            // Tìm gap trước showtime đầu tiên
            var firstShowtime = existingShowtimes.First();
            if (firstShowtime.StartTime > openingTime.AddMinutes(CleaningTimeMinutes))
            {
                slots.Add(new TimeSlot
                {
                    StartTime = openingTime,
                    EndTime = firstShowtime.StartTime.AddMinutes(-CleaningTimeMinutes),
                    IsUsed = false
                });
            }

            // Tìm gap giữa các showtime
            for (int i = 0; i < existingShowtimes.Count - 1; i++)
            {
                var currentEnd = existingShowtimes[i].EndTime.AddMinutes(CleaningTimeMinutes);
                var nextStart = existingShowtimes[i + 1].StartTime;

                if (nextStart > currentEnd.AddMinutes(30)) // Cần ít nhất 30p để tạo suất mới
                {
                    slots.Add(new TimeSlot
                    {
                        StartTime = currentEnd,
                        EndTime = nextStart.AddMinutes(-CleaningTimeMinutes),
                        IsUsed = false
                    });
                }
            }

            // Tìm gap sau showtime cuối
            var lastShowtime = existingShowtimes.Last();
            var lastEndWithCleaning = lastShowtime.EndTime.AddMinutes(CleaningTimeMinutes);
            if (lastEndWithCleaning < closingTime.AddMinutes(-60)) // Cần ít nhất 60p cuối ngày
            {
                slots.Add(new TimeSlot
                {
                    StartTime = lastEndWithCleaning,
                    EndTime = closingTime,
                    IsUsed = false
                });
            }

            return slots.Where(s => (s.EndTime - s.StartTime).TotalMinutes >= 90).ToList(); // Chỉ lấy slot >= 90p
        }

        /// <summary>
        /// Tạo một showtime mới
        /// </summary>
        private async Task<Showtime?> CreateShowtime(ScreeningContract contract, int screenId, DateTime startTime, int duration)
        {
            try
            {
                // Kiểm tra lại slot còn trống không (double check)
                var endTime = startTime.AddMinutes(duration);
                var hasOverlap = await _context.Showtimes
                    .AnyAsync(s => s.ScreenId == screenId
                        && s.StartTime.Date == startTime.Date
                        && ((s.StartTime < endTime && s.EndTime > startTime)));

                if (hasOverlap)
                    return null;

                // Lấy ScreenType từ DB bằng screenId
                var screen = await _context.Screens.FindAsync(screenId);

                var showtime = new Showtime
                {
                    MovieId = contract.MovieId,
                    ScreenId = screenId,
                    StartTime = startTime,
                    EndTime = endTime,
                    BasePrice = CalculateSmartPrice(startTime, screen?.ScreenType),
                    IsEarlyScreening = contract.Movie?.ReleaseDate > DateTime.Now
                };

                _context.Showtimes.Add(showtime);

                return showtime;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Lấy các hợp đồng còn hiệu lực có phân bổ cho theater này
        /// </summary>
        private async Task<List<ScreeningContract>> GetActiveContractsForTheater(int theaterId, DateTime targetDate)
        {
            var contracts = await _context.ScreeningContracts
                .Include(c => c.Movie)
                .Include(c => c.ContractTheaters)
                .Where(c => targetDate >= c.StartDate
                    && targetDate <= c.EndDate
                    && c.Status == "Active"
                    && c.ContractTheaters.Any(ct => ct.TheaterId == theaterId))
                .ToListAsync();

            return contracts;
        }
        /// Tính target số suất mỗi ngày cho hợp đồng
        /// </summary>
        private double CalculateDailyTarget(ScreeningContract contract)
        {
            var totalDays = (contract.EndDate - contract.StartDate).TotalDays;
            if (totalDays <= 0) return contract.TotalSlots;
            return (double)contract.TotalSlots / totalDays;
        }

        /// <summary>
        /// Xác định số suất cần tạo hôm nay cho hợp đồng
        /// </summary>
        private async Task<int> GetSlotsToCreateToday(ScreeningContract contract, DateTime targetDate)
        {
            var dailyTarget = CalculateDailyTarget(contract);
            var daysElapsed = (targetDate - contract.StartDate).TotalDays;
            // Đếm số showtime đã tạo cho hợp đồng này
            var actualSoFar = await _context.Showtimes
                .CountAsync(s => s.MovieId == contract.MovieId
                    && s.StartTime >= contract.StartDate
                    && s.StartTime <= contract.EndDate);

            var expectedSoFar = (int)Math.Ceiling(dailyTarget * daysElapsed);

            // Cần tạo thêm để bù backlog + target hôm nay
            var needed = expectedSoFar - actualSoFar + (int)Math.Ceiling(dailyTarget);
            var remainingSlots = contract.TotalSlots - actualSoFar;

            return Math.Max(0, Math.Min(needed, remainingSlots));
        }

        /// <summary>
        /// Kiểm tra hợp đồng có đang chậm tiến độ không
        /// </summary>
        private async Task<bool> IsBehindSchedule(ScreeningContract contract, DateTime targetDate)
        {
            var totalDays = (contract.EndDate - contract.StartDate).TotalDays;
            var daysRemaining = (contract.EndDate - targetDate).TotalDays;

            if (totalDays <= 0 || daysRemaining <= 0) return true;

            var expectedProgress = 1.0 - (daysRemaining / totalDays); // % nên hoàn thành
            // Đếm số showtime đã tạo
            var usedSlots = await _context.Showtimes
                .CountAsync(s => s.MovieId == contract.MovieId
                    && s.StartTime >= contract.StartDate
                    && s.StartTime <= contract.EndDate);
            var actualProgress = (double)usedSlots / contract.TotalSlots;

            return actualProgress < expectedProgress * 0.9; // Chậm hơn 10% so với dự kiến
        }

        /// <summary>
        /// Lấy số slot được phân bổ cho theater cụ thể
        /// </summary>
        private async Task<int> GetAllocatedSlotsForTheaterAsync(ScreeningContract contract, int theaterId)
        {
            var contractTheater = await _context.ContractTheaters
                .FirstOrDefaultAsync(ct => ct.ContractId == contract.ContractId && ct.TheaterId == theaterId);
            return contractTheater?.AllocatedSlots ?? 0;
        }

        /// <summary>
        /// Tính giá vé thông minh dựa trên khung giờ
        /// </summary>
        private decimal CalculateSmartPrice(DateTime startTime, string? screenType)
        {
            decimal basePrice = 50000;

            // Giờ vàng (18h-21h) tăng 40%
            if (startTime.Hour >= 18 && startTime.Hour <= 21)
                basePrice += 20000;

            // Phòng cao cấp tăng thêm
            if (!string.IsNullOrEmpty(screenType) &&
                (screenType.Contains("IMAX") || screenType.Contains("4DX")))
                basePrice += 30000;

            return basePrice;
        }

        // Helper Classes
        private class ContractTarget
        {
            public ScreeningContract Contract { get; set; } = null!;
            public double DailyTarget { get; set; }
            public int SlotsToCreateToday { get; set; }
            public bool IsBehindSchedule { get; set; }
            public int AllocatedSlotsForThisTheater { get; set; }
        }

        private class TimeSlot
        {
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public bool IsUsed { get; set; }
        }
    }

    // Result DTOs
    public class ShowtimeGenerationResult
    {
        public bool Success { get; set; }
        public int TheaterId { get; set; }
        public DateTime TargetDate { get; set; }
        public int TotalCreated { get; set; }
        public List<ShowtimeInfo> CreatedShowtimes { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public object DebugInfo { get; internal set; }
    }

    public class ShowtimeInfo
    {
        public int ShowtimeId { get; set; }
        public string MovieTitle { get; set; } = "";
        public int ScreenId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int ContractId { get; set; }
    }
}
