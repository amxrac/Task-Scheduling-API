using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using TaskSchedulingApi.Data;
using TaskSchedulingApi.Models;


namespace TaskSchedulingApi.Services
{  
    public class TaskNotificationService : BackgroundService
    {
        private readonly ILogger<TaskNotificationService> _logger;
        private readonly IServiceProvider _serviceProvider;
        public TaskNotificationService(ILogger<TaskNotificationService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Processing scheduled tasks at {time}", DateTime.UtcNow);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var _context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var _emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    var _userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

                    var tasksToProcess = await _context.ScheduledTasks.Where(t => t.NextRunAt <= DateTime.UtcNow && !t.IsDeleted && (t.Status == Models.TaskStatus.Pending || t.Status == Models.TaskStatus.Queued)).ToListAsync(stoppingToken);

                    _logger.LogInformation("{count} task(s) are due for processing", tasksToProcess.Count);

                    foreach (var task in tasksToProcess)
                    {
                        try
                        {
                            var user = await _userManager.FindByIdAsync(task.UserId);

                            if (user == null)
                            {
                                _logger.LogWarning("No user found for task: {taskId}", task.Id);
                                continue;
                            }

                            if (task.Type == TaskType.Recurring && task.RecurrenceInterval.HasValue)
                            {
                                task.NextRunAt = DateTime.UtcNow.Add(task.RecurrenceInterval.Value);
                                task.Status = Models.TaskStatus.Pending;
                                _logger.LogInformation("Recurring task {id} scheduled for next run at {nextRunTime}", task.Id, task.NextRunAt);
                            }
                            else
                            {
                                task.Status = Models.TaskStatus.Completed;
                            }

                            task.LastRunAt = DateTime.UtcNow;

                            if (!task.NotificationSent)
                            {
                                string taskType = task.Type.ToString();
                                string recurringInfo = task.Type == TaskType.Recurring && task.NextRunAt.HasValue ? $"<p>This is a recurring task. Next occurence: {task.NextRunAt.Value.ToString("f")}</p>" : "";

                                string emailBody = $@"
                                            <h2>Task Reminder</h2>
                                            <p>Your {taskType.ToLower()} task: <strong>{task.Title}</strong> is now due.</p>
                                            <p><strong>Description:</strong> {task.Description ?? "No description provided"}</p>
                                            <p><strong>Scheduled for:</strong> {task.ScheduledAt?.ToString("f") ?? "Immediate execution"}</p>
                                            {recurringInfo}
                                            <p>Please log in to your Task Scheduler account to view details.</p>
                                            <p>Thank you for using this service!</p>";

                                await _emailService.SendEmailAsync(user.Email, $"Task Reminder: {task.Title}", emailBody);

                                task.NotificationSent = true;
                                task.NotificationSentAt = DateTime.UtcNow;

                                _logger.LogInformation("Notification sent for task: {id} to {email}", task.Id, user.Email);
                            }

                            if (task.Type == TaskType.Recurring && task.Status == Models.TaskStatus.Pending)
                            {
                                task.NotificationSent = false;
                                task.NotificationSentAt = null;
                            }

                            await _context.SaveChangesAsync(stoppingToken);
                        }

                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing task: {id}", task.Id);

                            if (task.RetryCount < task.MaxRetries)
                            {
                                await Task.Delay(5000, stoppingToken);
                                task.RetryCount++;

                                task.Status = Models.TaskStatus.Queued;
                                _logger.LogWarning("Task {taskId} will be retried (attempt {attempt}/{maxRetries})", task.Id, task.RetryCount, task.MaxRetries);
                            }
                            else
                            {
                                task.Status = Models.TaskStatus.Failed;

                                _logger.LogError("Task {taskId} has failed after {maxRetries} retry attempts", task.Id, task.MaxRetries);

                            }
                        }
                    }

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in task notification service");
                }
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            _logger.LogInformation("Task notification service is stopping at {time}", DateTime.UtcNow);

        }


    }
}
