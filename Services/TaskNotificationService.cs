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


                    var tasksToProcess = await _context.ScheduledTasks.Where(t => t.NextRunAt <= DateTime.UtcNow && !t.IsDeleted && (t.Status == Models.TaskStatus.Pending || t.Status == Models.TaskStatus.Queued)).ToListAsync(stoppingToken);

                    _logger.LogInformation("{count} task(s) are due for processing", tasksToProcess.Count);

                    var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 5, CancellationToken = stoppingToken };

                    await Parallel.ForEachAsync(tasksToProcess, parallelOptions, async (task, token) =>
                    {
                        using var _taskScope = _serviceProvider.CreateScope();
                        var _taskContext = _taskScope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var _emailService = _taskScope.ServiceProvider.GetRequiredService<IEmailService>();
                        var _userManager = _taskScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
                        var _loggerFactory = _taskScope.ServiceProvider.GetRequiredService<ILoggerFactory>();
                        var _taskLogger = _loggerFactory.CreateLogger($"TaskProcessor-{task.Id}");
                        try
                        {
                            var currentTask = await _taskContext.ScheduledTasks.FirstOrDefaultAsync(t => t.Id == task.Id, token);

                            if (currentTask == null) return;

                            var user = await _userManager.FindByIdAsync(currentTask.UserId);

                            if (user == null)
                            {
                                _taskLogger.LogWarning("No user found for task: {taskId}", currentTask.Id);
                                return;
                            }

                            if (currentTask.Type == TaskType.Recurring && currentTask.RecurrenceInterval.HasValue)
                            {
                                currentTask.NextRunAt = DateTime.UtcNow.Add(currentTask.RecurrenceInterval.Value);
                                currentTask.Status = Models.TaskStatus.Pending;
                                _taskLogger.LogInformation("Recurring task {id} scheduled for next run at {nextRunTime}", currentTask.Id, currentTask.NextRunAt);
                            }
                            else
                            {
                                currentTask.Status = Models.TaskStatus.Completed;
                            }

                            currentTask.LastRunAt = DateTime.UtcNow;

                            if (!currentTask.NotificationSent)
                            {
                                string taskType = currentTask.Type.ToString();

                                string emailBody = GenerateEmailBody(currentTask, taskType);

                                await _emailService.SendEmailAsync(user.Email, $"Task Reminder: {currentTask.Title}", emailBody);

                                currentTask.NotificationSent = true;
                                currentTask.NotificationSentAt = DateTime.UtcNow;

                                _taskLogger.LogInformation("Notification sent for task: {id} to {email}", currentTask.Id, user.Email);
                            }

                            if (currentTask.Type == TaskType.Recurring && currentTask.Status == Models.TaskStatus.Pending)
                            {
                                currentTask.NotificationSent = false;
                                currentTask.NotificationSentAt = null;
                            }

                            _taskContext.ScheduledTasks.Update(currentTask);
                            await _taskContext.SaveChangesAsync(token);
                        }

                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing task: {id}", task.Id);

                            if (task.RetryCount < task.MaxRetries)
                            {                                
                                task.RetryCount++;

                                int backoffSeconds = Math.Min((int)Math.Pow(2, task.RetryCount), 60);

                                _taskLogger.LogWarning("Waiting {seconds} seconds before retrying task {taskId}", backoffSeconds, task.Id);
                                await Task.Delay(TimeSpan.FromSeconds(backoffSeconds));


                                task.Status = Models.TaskStatus.Queued;
                                _logger.LogWarning("Task {taskId} will be retried (attempt {attempt}/{maxRetries})", task.Id, task.RetryCount, task.MaxRetries);
                            }
                            else
                            {
                                task.Status = Models.TaskStatus.Failed;

                                _logger.LogError("Task {taskId} has failed after {maxRetries} retry attempts", task.Id, task.MaxRetries);

                            }

                            _taskContext.ScheduledTasks.Update(task);
                            await _taskContext.SaveChangesAsync();
                        }
                    });

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in task notification service");
                }
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            _logger.LogInformation("Task notification service is stopping at {time}", DateTime.UtcNow);

        }

        private static string GenerateEmailBody(ScheduledTask task, string taskType)
        {
            string recurringInfo = task.Type == TaskType.Recurring && task.NextRunAt.HasValue
                ? $"<p>This is a recurring task. Next occurence: {task.NextRunAt.Value:f}</p>" : "";

            return $@"
        <h2>Task Reminder</h2>
        <p>Your {taskType.ToLower()} task: <strong>{task.Title}</strong> is now due.</p>
        <p><strong>Description:</strong> {task.Description ?? "No description provided"}</p>
        <p><strong>Scheduled for:</strong> {task.ScheduledAt?.ToString("f") ?? "Immediate execution"}</p>
        {recurringInfo}
        <p>Please log in to your Task Scheduler account to view details.</p>
        <p>Thank you for using this service!</p>";
        }


    }


}
