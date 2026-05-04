using doantotnghiep_api.Data;
using doantotnghiep_api.Models;
using doantotnghiep_api.Config;
using Microsoft.EntityFrameworkCore;

namespace doantotnghiep_api.Services
{
    public class ShowtimeAutomationService
    {
        private readonly AppDbContext _context;
        private const int CleaningTimeMinutes = 20; 

        public ShowtimeAutomationService(AppDbContext context) => _context = context;

        public async Task<ShowtimeGenerationResult> GenerateShowtimesAsync(int theaterId, DateTime targetDate, List<int>? movieIds = null)
        {
            var result = new ShowtimeGenerationResult
            {
                TheaterId = theaterId,
                TargetDate = targetDate,
                CreatedShowtimes = new List<ShowtimeInfo>(),
                Errors = new List<string>(),
                DebugInfo = new { logs = new List<string>() }
            };

            var debugLogs = new List<string>();
            debugLogs.Add($"Bắt đầu tạo lịch cho rạp {theaterId} ngày {targetDate:yyyy-MM-dd}");

            try
            {
                // 1. Lấy hợp đồng Active (có thể lọc theo movieIds)
                var activeContracts = await GetActiveContractsForTheater(theaterId, targetDate, movieIds);
                if (!activeContracts.Any())
                {
                    result.Errors.Add("Không có hợp đồng nào còn hiệu lực cho ngày này tại rạp này");
                    result.Success = false;
                    result.DebugInfo = new { logs = debugLogs };
                    return result;
                }

                // 2. Lấy danh sách phòng
                var screens = await _context.Screens
                    .Where(s => s.TheaterId == theaterId)
                    .OrderBy(s => s.ScreenId)
                    .ToListAsync();

                // 3. Giờ mở cửa: 09:00 - 23:30
                var openingTime = targetDate.Date.AddHours(9);
                var closingTime = targetDate.Date.AddHours(23).AddMinutes(30);

                // 4. Tính toán Target
                var contractTargets = new List<ContractTarget>();
                foreach (var c in activeContracts)
                {
                    var slotsToCreate = await GetSlotsToCreateToday(c, targetDate, theaterId);
                    var allocated = await GetAllocatedSlotsForTheaterAsync(c, theaterId);
                    var used = await GetActualUsedAtTheaterAsync(c, theaterId);

                    contractTargets.Add(new ContractTarget
                    {
                        Contract = c,
                        SlotsToCreateToday = slotsToCreate,
                        AllocatedSlotsForThisTheater = allocated,
                        ActualUsedAtThisTheater = used
                    });
                }

                // 5. Lấy danh sách showtime hiện có
                var existingShowtimesInTheater = await _context.Showtimes
                    .Include(s => s.Movie)
                    .Where(s => s.Screen.TheaterId == theaterId && s.StartTime.Date == targetDate.Date)
                    .ToListAsync();

                var dayConfig = GoldenHourConfig.GetConfigForDate(targetDate);
                int theaterGoldenShowsCreated = 0; 
                var random = new Random();

                // 6. Duyệt qua từng phòng
                foreach (var screen in screens)
                {
                    // Lệch giờ so le 0, 15, 30 phút
                    int screenOffset = (screens.IndexOf(screen) % 3) * 15;
                    var screenOpeningTime = openingTime.AddMinutes(screenOffset);

                    var screenShowtimes = existingShowtimesInTheater
                        .Where(s => s.ScreenId == screen.ScreenId)
                        .OrderBy(s => s.StartTime)
                        .ToList();

                    // TRƯỚC HẾT: Lấp đầy từ SÁNG SỚM (Phase 0: Morning Fill)
                    // Cố gắng đặt 1-2 suất từ 9h sáng nếu hợp đồng còn quota
                    bool morningAdded = true;
                    int morningCount = 0;
                    while (morningAdded && morningCount < 2)
                    {
                        morningAdded = false;
                        var gaps = FindAvailableTimeSlotsFromList(screenShowtimes, targetDate, screenOpeningTime, closingTime);
                        var morningGap = gaps.FirstOrDefault(g => g.StartTime.Hour < 14); // Chỉ ưu tiên slot trước 14h

                        if (morningGap != null)
                        {
                            // Luân phiên chọn phim để tránh tập trung 1 phim
                            var availableForMorning = contractTargets
                                .Where(ct => ct.SlotsToCreateToday > 0 && (ct.AllocatedSlotsForThisTheater - ct.ActualUsedAtThisTheater) > 0)
                                .OrderBy(ct => ct.ActualUsedAtThisTheater) // Chọn phim ít suất nhất trước
                                .ToList();

                            foreach (var target in availableForMorning)
                            {
                                var movieDuration = target.Contract.Movie?.Duration ?? 120;
                                if ((morningGap.EndTime - morningGap.StartTime).TotalMinutes >= movieDuration + CleaningTimeMinutes)
                                {
                                    var showtime = CreateShowtimeFromMemory(target.Contract, screen.ScreenId, morningGap.StartTime, movieDuration, screenShowtimes);
                                    if (showtime != null)
                                    {
                                        AddShowtimeToContext(showtime, screenShowtimes, target, result, debugLogs, "SÁNG");
                                        morningAdded = true;
                                        morningCount++;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    // TIẾP THEO: Ưu tiên GIỜ VÀNG (Phase 1)
                    var sortedGoldenSlots = dayConfig.GoldenHours.OrderByDescending(gs => gs.Weight).ToList();
                    foreach (var gSlot in sortedGoldenSlots)
                    {
                        if (theaterGoldenShowsCreated >= dayConfig.GoldenShowsRequired) break;

                        bool canAdd = true;
                        while (canAdd && theaterGoldenShowsCreated < dayConfig.GoldenShowsRequired)
                        {
                            canAdd = false;
                            var gaps = FindAvailableTimeSlotsFromList(screenShowtimes, targetDate, screenOpeningTime, closingTime);
                            DateTime goldenStart = targetDate.Date.AddHours(gSlot.StartHour);
                            DateTime goldenEnd = targetDate.Date.AddHours(gSlot.EndHour);

                            var goldenGap = gaps.FirstOrDefault(g => !(g.EndTime <= goldenStart || g.StartTime >= goldenEnd));
                            if (goldenGap != null)
                            {
                                DateTime bestStart = goldenGap.StartTime < goldenStart ? goldenStart : goldenGap.StartTime;
                                
                                var availableForGold = contractTargets
                                    .Where(ct => ct.SlotsToCreateToday > 0 && (ct.AllocatedSlotsForThisTheater - ct.ActualUsedAtThisTheater) > 0)
                                    .OrderBy(ct => ct.ActualUsedAtThisTheater)
                                    .ToList();

                                foreach (var target in availableForGold)
                                {
                                    var movieDuration = target.Contract.Movie?.Duration ?? 120;
                                    if ((goldenGap.EndTime - bestStart).TotalMinutes >= movieDuration + CleaningTimeMinutes)
                                    {
                                        var showtime = CreateShowtimeFromMemory(target.Contract, screen.ScreenId, bestStart, movieDuration, screenShowtimes);
                                        if (showtime != null)
                                        {
                                            AddShowtimeToContext(showtime, screenShowtimes, target, result, debugLogs, "VÀNG");
                                            theaterGoldenShowsCreated++;
                                            canAdd = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // CUỐI CÙNG: Lấp đầy CÁC KHOẢNG TRỐNG CÒN LẠI (Phase 2)
                    bool genericAdded;
                    do
                    {
                        genericAdded = false;
                        var gaps = FindAvailableTimeSlotsFromList(screenShowtimes, targetDate, screenOpeningTime, closingTime);
                        foreach (var gap in gaps)
                        {
                            var available = contractTargets
                                .Where(ct => ct.SlotsToCreateToday > 0 && (ct.AllocatedSlotsForThisTheater - ct.ActualUsedAtThisTheater) > 0)
                                .OrderBy(ct => ct.ActualUsedAtThisTheater)
                                .ToList();

                            foreach (var target in available)
                            {
                                var movieDuration = target.Contract.Movie?.Duration ?? 120;
                                if ((gap.EndTime - gap.StartTime).TotalMinutes >= movieDuration + CleaningTimeMinutes)
                                {
                                    var showtime = CreateShowtimeFromMemory(target.Contract, screen.ScreenId, gap.StartTime, movieDuration, screenShowtimes);
                                    if (showtime != null)
                                    {
                                        AddShowtimeToContext(showtime, screenShowtimes, target, result, debugLogs, "THƯỜNG");
                                        genericAdded = true;
                                        break;
                                    }
                                }
                            }
                            if (genericAdded) break;
                        }
                    } while (genericAdded);
                }

                // Lưu Transaction
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw new Exception("Lỗi Database: " + ex.Message);
                }

                result.TotalCreated = result.CreatedShowtimes.Count;
                result.Success = result.TotalCreated > 0;
                result.DebugInfo = new { logs = debugLogs };
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Lỗi: {ex.Message}");
            }

            return result;
        }

        private void AddShowtimeToContext(Showtime showtime, List<Showtime> screenShowtimes, ContractTarget target, ShowtimeGenerationResult result, List<string> debugLogs, string type)
        {
            _context.Showtimes.Add(showtime);
            screenShowtimes.Add(showtime);
            screenShowtimes.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

            result.CreatedShowtimes.Add(new ShowtimeInfo
            {
                ShowtimeId = showtime.ShowtimeId,
                MovieTitle = target.Contract.Movie?.Title ?? "Unknown",
                ScreenId = showtime.ScreenId,
                StartTime = showtime.StartTime,
                EndTime = showtime.EndTime,
                ContractId = target.Contract.ContractId
            });

            target.SlotsToCreateToday--;
            target.ActualUsedAtThisTheater++;
            debugLogs.Add($"[{type}] {showtime.StartTime:HH:mm} - {target.Contract.Movie?.Title}");
        }

        private List<TimeSlot> FindAvailableTimeSlotsFromList(List<Showtime> existingShowtimes, DateTime date, DateTime openingTime, DateTime closingTime)
        {
            var slots = new List<TimeSlot>();
            if (!existingShowtimes.Any())
            {
                slots.Add(new TimeSlot { StartTime = openingTime, EndTime = closingTime });
                return slots;
            }

            var first = existingShowtimes.First();
            if (first.StartTime > openingTime.AddMinutes(CleaningTimeMinutes))
                slots.Add(new TimeSlot { StartTime = openingTime, EndTime = first.StartTime.AddMinutes(-CleaningTimeMinutes) });

            for (int i = 0; i < existingShowtimes.Count - 1; i++)
            {
                var curEnd = existingShowtimes[i].EndTime.AddMinutes(CleaningTimeMinutes);
                var nextStart = existingShowtimes[i + 1].StartTime;
                if (nextStart > curEnd.AddMinutes(10)) 
                    slots.Add(new TimeSlot { StartTime = curEnd, EndTime = nextStart.AddMinutes(-CleaningTimeMinutes) });
            }

            var last = existingShowtimes.Last();
            var lastEnd = last.EndTime.AddMinutes(CleaningTimeMinutes);
            if (lastEnd < closingTime)
                slots.Add(new TimeSlot { StartTime = lastEnd, EndTime = closingTime });

            return slots.Where(s => (s.EndTime - s.StartTime).TotalMinutes >= 90).ToList();
        }

        private Showtime? CreateShowtimeFromMemory(ScreeningContract contract, int screenId, DateTime startTime, int duration, List<Showtime> screenShowtimes)
        {
            var endTime = startTime.AddMinutes(duration);
            if (screenShowtimes.Any(s => s.StartTime < endTime && s.EndTime > startTime)) return null;

            return new Showtime
            {
                MovieId = contract.MovieId,
                ScreenId = screenId,
                StartTime = startTime,
                EndTime = endTime,
                BasePrice = CalculateSmartPrice(startTime, null),
                IsEarlyScreening = contract.Movie?.ReleaseDate > DateTime.Now
            };
        }

        private async Task<List<ScreeningContract>> GetActiveContractsForTheater(int theaterId, DateTime targetDate, List<int>? movieIds = null)
        {
            var query = _context.ScreeningContracts
                .Include(c => c.Movie)
                .Include(c => c.ContractTheaters)
                .Where(c => targetDate.Date >= c.StartDate.Date 
                    && targetDate.Date <= c.EndDate.Date 
                    && c.Status == "Active" 
                    && c.ContractTheaters.Any(ct => ct.TheaterId == theaterId));

            if (movieIds != null && movieIds.Any())
            {
                query = query.Where(c => movieIds.Contains(c.MovieId));
            }

            return await query.ToListAsync();
        }

        private async Task<int> GetSlotsToCreateToday(ScreeningContract contract, DateTime targetDate, int theaterId)
        {
            var totalDays = Math.Max((contract.EndDate - contract.StartDate).TotalDays, 1);
            var dailyTarget = (double)contract.TotalSlots / totalDays;
            var daysElapsed = (targetDate.Date - contract.StartDate.Date).TotalDays;
            
            var actualSoFar = await _context.Showtimes.Include(s => s.Screen).CountAsync(s => s.MovieId == contract.MovieId && s.Screen.TheaterId == theaterId && s.StartTime >= contract.StartDate && s.StartTime <= contract.EndDate);
            var expectedSoFar = (int)Math.Ceiling(dailyTarget * daysElapsed);
            var needed = expectedSoFar - actualSoFar + (int)Math.Ceiling(dailyTarget);
            var allocated = (await _context.ContractTheaters.FirstOrDefaultAsync(ct => ct.ContractId == contract.ContractId && ct.TheaterId == theaterId))?.AllocatedSlots ?? 0;
            return Math.Max(0, Math.Min(needed, allocated - actualSoFar));
        }

        private async Task<int> GetActualUsedAtTheaterAsync(ScreeningContract contract, int theaterId)
        {
            return await _context.Showtimes.Include(s => s.Screen).CountAsync(s => s.MovieId == contract.MovieId && s.Screen.TheaterId == theaterId && s.StartTime >= contract.StartDate && s.StartTime <= contract.EndDate);
        }

        private async Task<int> GetAllocatedSlotsForTheaterAsync(ScreeningContract contract, int theaterId)
        {
            var ct = await _context.ContractTheaters.FirstOrDefaultAsync(ct => ct.ContractId == contract.ContractId && ct.TheaterId == theaterId);
            return ct?.AllocatedSlots ?? 0;
        }

        private decimal CalculateSmartPrice(DateTime startTime, string? screenType)
        {
            decimal basePrice = 50000;
            double weight = GoldenHourConfig.GetHourWeight(startTime);
            if (weight >= 1.8) basePrice += 40000;      
            else if (weight >= 1.5) basePrice += 30000; 
            else if (weight >= 1.0) basePrice += 20000; 
            else if (weight >= 0.7) basePrice += 10000; 
            return basePrice;
        }

        private class ContractTarget
        {
            public ScreeningContract Contract { get; set; } = null!;
            public int SlotsToCreateToday { get; set; }
            public int AllocatedSlotsForThisTheater { get; set; }
            public int ActualUsedAtThisTheater { get; set; }
        }

        private class TimeSlot
        {
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
        }
    }

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
