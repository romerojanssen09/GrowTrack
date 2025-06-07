using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Project_Creation.Controllers;
using Project_Creation.Data;
using Project_Creation.Models.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Project_Creation.Services
{
    public class CalendarNotificationService : BackgroundService
    {
        private readonly ILogger<CalendarNotificationService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public CalendarNotificationService(
            ILogger<CalendarNotificationService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Calendar Notification Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Get current time in Singapore timezone
                    var singaporeTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
                    var singaporeTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, singaporeTimeZone);

                    // Check if it's around 7 AM in the morning (between 6:45 AM and 7:15 AM)
                    if (singaporeTime.Hour == 7 && singaporeTime.Minute >= 0 && singaporeTime.Minute <= 15)
                    {
                        _logger.LogInformation("It's morning time. Sending daily calendar notifications.");

                        // Create a scope to resolve scoped services
                        using (var scope = _serviceProvider.CreateScope())
                        {
                            try
                            {
                                // Get the calendar controller to use its method
                                var calendarController = scope.ServiceProvider.GetRequiredService<CalendarController>();

                                // Send the notifications
                                await calendarController.SendDailyEventNotificationsAsync();

                                _logger.LogInformation("Daily calendar notifications sent successfully.");

                                // Wait longer before checking again (to avoid sending multiple times)
                                await Task.Delay(TimeSpan.FromHours(23), stoppingToken);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error sending daily calendar notifications");
                            }
                        }
                    }

                    // Wait for 15 minutes before checking again
                    await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in calendar notification background service");

                    // Wait a bit before trying again
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }

            _logger.LogInformation("Calendar Notification Service is stopping.");
        }
    }
} 