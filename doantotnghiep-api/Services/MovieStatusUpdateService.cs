using doantotnghiep_api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace doantotnghiep_api.Services
{
    public class MovieStatusUpdateService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MovieStatusUpdateService> _logger;

        public MovieStatusUpdateService(IServiceProvider serviceProvider, ILogger<MovieStatusUpdateService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Movie Status Update Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                        // Lấy chuẩn giờ Việt Nam (UTC+7) để không bị lỗi khi deploy lên server nước ngoài
                        var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone);

                        bool hasChanges = false;

                        // 1. CẬP NHẬT PHIM: Sắp chiếu -> Đang chiếu
                        var moviesToStart = await context.Movies
                            .Where(m => m.Status == "ComingSoon" && m.ReleaseDate <= now)
                            .ToListAsync(stoppingToken);

                        foreach (var movie in moviesToStart)
                        {
                            movie.Status = "NowShowing";
                            _logger.LogInformation($"Updated status to NowShowing for Movie: {movie.Title}");
                            hasChanges = true;
                        }

                        // 2. CHUYỂN TRẠNG THÁI: Đang chiếu -> Đã kết thúc
                        var moviesToEnd = await context.Movies
                            .Where(m => m.Status == "NowShowing" && m.EndDate.HasValue && m.EndDate.Value < now)
                            .ToListAsync(stoppingToken);

                        foreach (var movie in moviesToEnd)
                        {
                            movie.Status = "Ended";
                            _logger.LogInformation($"Updated status to Ended for Movie: {movie.Title}");
                            hasChanges = true;
                        }

                        // 3. LƯU XUỐNG DATABASE (CỰC KỲ QUAN TRỌNG)
                        if (hasChanges)
                        {
                            await context.SaveChangesAsync(stoppingToken);
                            _logger.LogInformation("Successfully saved movie status changes to Database.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing Movie Status Update.");
                }

                // Chạy 1 giờ 1 lần
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }
}