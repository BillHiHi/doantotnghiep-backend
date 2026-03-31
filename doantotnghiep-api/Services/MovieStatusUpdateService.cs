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
                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    // ✅ FIX TIMEZONE (QUAN TRỌNG)
                    TimeZoneInfo vnTimeZone;
                    try
                    {
                        vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
                    }
                    catch
                    {
                        vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                    }

                    var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone);

                    bool hasChanges = false;

                    var moviesToStart = await context.Movies
                        .Where(m => m.Status == "ComingSoon" && m.ReleaseDate <= now)
                        .ToListAsync(stoppingToken);

                    foreach (var movie in moviesToStart)
                    {
                        movie.Status = "NowShowing";
                        hasChanges = true;
                    }

                    var moviesToEnd = await context.Movies
                        .Where(m => m.Status == "NowShowing" && m.EndDate.HasValue && m.EndDate.Value < now)
                        .ToListAsync(stoppingToken);

                    foreach (var movie in moviesToEnd)
                    {
                        movie.Status = "Ended";
                        hasChanges = true;
                    }

                    if (hasChanges)
                    {
                        await context.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation("Updated movie status successfully.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in MovieStatusUpdateService");
                }

                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }
}