using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Task_Scheduling_API.Data;
using Task_Scheduling_API.DTOs;
using Task_Scheduling_API.Services;
using Task_Scheduling_API.Models;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Pqc.Crypto.Lms;

namespace Task_Scheduling_API.Controllers
{
    [Authorize(Policy = "EmailConfirmed")]
    [ApiController]
    [Route("api/[Controller]")]
    public class TaskController : ControllerBase
    {
        private readonly ILogger<TaskController> _logger;
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        public TaskController(ILogger<TaskController> logger, AppDbContext context, UserManager<AppUser> userManager)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
        }


        [HttpPost]
        public async Task<IActionResult> CreateTask([FromBody] CreateTaskDTO model)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                _logger.LogWarning("Profile access attempted with valid token but user not found");
                return NotFound(new { message = "User not found." });
            }

            _logger.LogInformation("Task creation for user: {Email}", user.Email);

            if (!ModelState.IsValid)
            {
                _logger.LogError("Invalid model state {ex} by {userEmail}", ModelState, user.Email);
                return BadRequest(new { message = "Invalid details provided.", errors = ModelState });
            }

            if (!Enum.IsDefined(typeof(Models.TaskType), model.Type))
            {
                return BadRequest(new { message = "Invalid type value. Accepted values are: 0 (OneOff), 1 (Delayed), 2 (Recurring)." });
            }

            DateTime? scheduledAt = null;
            TimeSpan? recurrenceInterval = null;

            switch (model.Type)
            {
                case TaskType.OneOff:
                    if (model.ScheduledAt.HasValue && model.ScheduledAt < DateTime.UtcNow)
                        return BadRequest(new { message = "Scheduled time cannot be in the past." });

                    scheduledAt = model.ScheduledAt ?? (model.DelayMinutes.HasValue ? DateTime.UtcNow.AddMinutes(model.DelayMinutes.Value) : DateTime.UtcNow);
                    break;

                case TaskType.Delayed:
                    if (!model.DelayMinutes.HasValue)
                        return BadRequest(new { message = "DelayMinutes is required for Delayed tasks." });

                    scheduledAt = DateTime.UtcNow.AddMinutes(model.DelayMinutes.Value);
                    break;

                case TaskType.Recurring:
                    if (!model.ScheduledAt.HasValue)
                        return BadRequest(new { message = "ScheduledAt is required for Recurring tasks." });

                    if (model.ScheduledAt.Value < DateTime.UtcNow)
                        return BadRequest(new { message = "Scheduled time cannot be in the past." });

                    if (!model.RecurrenceInterval.HasValue)
                        return BadRequest(new { message = "RecurrenceInterval is required for Recurring tasks." });

                    if (string.IsNullOrEmpty(model.RecurrenceUnit) ||
                        (model.RecurrenceUnit.ToLower() != "h" && model.RecurrenceUnit.ToLower() != "d"))
                    {
                        return BadRequest(new { message = "RecurrenceUnit must be specified for recurring tasks. h for hour(s) and d for day(s)" });
                    }

                    scheduledAt = model.ScheduledAt;

                    if (model.RecurrenceUnit.ToLower() == "h")
                        recurrenceInterval = TimeSpan.FromHours(model.RecurrenceInterval.Value);
                    else
                        recurrenceInterval = TimeSpan.FromDays(model.RecurrenceInterval.Value);
                    break;
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var scheduledTask = new ScheduledTask
                {
                    UserId = userId,
                    Title = model.Title,
                    Description = model.Description,
                    Status = Models.TaskStatus.Pending,
                    Type = model.Type,
                    ScheduledAt = scheduledAt,
                    NextRunAt = scheduledAt,
                    RecurrenceInterval = recurrenceInterval
                };

                _logger.LogInformation("New task created by {userEmail} at {Timestamp}", user.Email, DateTime.UtcNow);
                await _context.ScheduledTasks.AddAsync(scheduledTask);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("New task by user {userEmail} saved", user.Email);

                return StatusCode(StatusCodes.Status201Created, new
                {
                    message = "task created successfully",
                    id = scheduledTask.Id,
                    createdAt = scheduledTask.CreatedAt,
                    title = scheduledTask.Title,
                    description = scheduledTask.Description,
                    status = Models.TaskStatus.Pending,
                    type = scheduledTask.Type,
                    ScheduledAt = scheduledTask.ScheduledAt,
                    NextRunAt = scheduledTask.NextRunAt,
                    recurrenceInterval = scheduledTask.RecurrenceInterval
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error during task creation by {userEmail}", user.Email);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while creating your task. Please try again later." });
            }

        }


        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetTask(int id)
        {            
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                _logger.LogWarning("Profile access attempted with valid token but user not found");
                return NotFound(new { message = "User not found." });
            }

            _logger.LogInformation("Fetching task details for {userEmail}", user.Email);

            if (id <= 0)
            {
                _logger.LogError("Invalid task id: {id} entered by user: {userEmail}", id, user.Email);
                return BadRequest(new { message = "Invalid id." });
            }

            var task = await _context.ScheduledTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
            {
                _logger.LogError("Task {id} not found", id);
                return NotFound(new { message = "Task not found. Ensure the Id is valid", id = id});
            }
            if (task.IsDeleted == true)
            {
                _logger.LogError("Task {id} has already been deleted", id);
                return NotFound(new { message = "Task has been deleted.", id = id });
            }

            _logger.LogInformation("Task {id} details retrieved successfully for {userEmail}", id, user.Email);

            return Ok(new
            {
                message = "Task retrieved successfully",
                id = task.Id,
                title = task.Title,
                description = task.Description,
                status = task.Status,
                type = task.Type,
                ScheduledAt = task.ScheduledAt,
                NextRunAt = task.NextRunAt,
                LastRunAt = task.LastRunAt,
                RetryCount = task.RetryCount,
                MaxRetries = task.MaxRetries,
                IsDeleted = task.IsDeleted
            });
        }


        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateTask(int id, [FromBody] UpdateTaskDTO model)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                _logger.LogWarning("Profile access attempted with valid token but user not found");
                return NotFound(new { message = "User not found." });
            }

            _logger.LogInformation("Updating task {id} for {userEmail}", id, user.Email);

            var task = await _context.ScheduledTasks.FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
            {
                _logger.LogError("Task {id} not found", id);
                return NotFound(new { message = "Task not found. Ensure the Id is valid", id = id });
            }

            if (!ModelState.IsValid)
            {
                _logger.LogError("Invalid model state {ex} by {userEmail}", ModelState, user.Email);
                return BadRequest(new { message = "Invalid details provided.", errors = ModelState });
            }

            if (!Enum.IsDefined(typeof(Models.TaskType), model.Type))
            {
                return BadRequest(new { message = "Invalid type value. Accepted values are: 0 (OneOff), 1 (Delayed), 2 (Recurring)." });
            }

            DateTime? scheduledAt = null;
            TimeSpan? recurrenceInterval = null;

            switch (model.Type)
            {
                case TaskType.OneOff:
                    if (model.ScheduledAt.HasValue && model.ScheduledAt < DateTime.UtcNow)
                        return BadRequest(new { message = "Scheduled time cannot be in the past." });

                    scheduledAt = model.ScheduledAt ?? (model.DelayMinutes.HasValue ? DateTime.UtcNow.AddMinutes(model.DelayMinutes.Value) : DateTime.UtcNow);
                    break;

                case TaskType.Delayed:
                    if (!model.DelayMinutes.HasValue)
                        return BadRequest(new { message = "DelayMinutes is required for Delayed tasks." });

                    scheduledAt = DateTime.UtcNow.AddMinutes(model.DelayMinutes.Value);
                    break;

                case TaskType.Recurring:
                    if (!model.ScheduledAt.HasValue)
                        return BadRequest(new { message = "ScheduledAt is required for Recurring tasks." });

                    if (model.ScheduledAt.Value < DateTime.UtcNow)
                        return BadRequest(new { message = "Scheduled time cannot be in the past." });

                    if (!model.RecurrenceInterval.HasValue)
                        return BadRequest(new { message = "RecurrenceInterval is required for Recurring tasks." });

                    if (string.IsNullOrEmpty(model.RecurrenceUnit) ||
                        (model.RecurrenceUnit.ToLower() != "h" && model.RecurrenceUnit.ToLower() != "d"))
                    {
                        return BadRequest(new { message = "RecurrenceUnit must be specified for recurring tasks. h for hour(s) and d for day(s)" });
                    }

                    scheduledAt = model.ScheduledAt;

                    if (model.RecurrenceUnit.ToLower() == "h")
                        recurrenceInterval = TimeSpan.FromHours(model.RecurrenceInterval.Value);
                    else
                        recurrenceInterval = TimeSpan.FromDays(model.RecurrenceInterval.Value);
                    break;
            }
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (model.Title != null)
                    task.Title = model.Title;
                if (model.Description != null)
                    task.Description = model.Description;
                if (model.Type.HasValue)
                    task.Type = model.Type.Value;


                task.ScheduledAt = scheduledAt;
                task.NextRunAt = scheduledAt;
                task.RecurrenceInterval = recurrenceInterval;
                task.ModifiedAt = DateTime.UtcNow;

                _context.ScheduledTasks.Update(task);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Task {id} updated successfully by {userEmail}", id, user.Email);

                return Ok(new
                {
                    message = "Task updated successfully",
                    id = id,
                    title = task.Title,
                    description = task.Description,
                    status = task.Status,
                    type = task.Type,
                    ModifiedAt = task.ModifiedAt,
                    ScheduledAt = task.ScheduledAt,
                    NextRunAt = task.NextRunAt,
                    LastRunAt = task.LastRunAt,
                    RetryCount = task.RetryCount,
                    MaxRetries = task.MaxRetries,
                    recurrenceInterval = task.RecurrenceInterval
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error during update task operation");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while updating your task. Please try again later." });
            }

        }


        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetTasks()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);

                if (user == null)
                {
                    _logger.LogWarning("Profile access attempted with valid token but user not found");
                    return NotFound(new { message = "User not found." });
                }

                _logger.LogInformation("Retrieving tasks for {adminEmail}", user.Email);

                var tasks = await _context.ScheduledTasks.AsNoTracking().Select(t => new
                {
                    t.Id,
                    t.UserId,
                    t.Title,
                    t.Description,
                    t.Status,
                    t.Type,
                    t.CreatedAt,
                    t.ScheduledAt,
                    t.NextRunAt,
                    t.LastRunAt,
                    t.ModifiedAt,
                    t.RecurrenceInterval,
                    t.RetryCount,
                    t.IsDeleted
                }).ToListAsync();

                _logger.LogInformation("Tasks retrieved successfully for {adminEmail}", user.Email);

                return Ok(new
                {
                    message = "Tasks retrieved successfully.",
                    tasks = tasks
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving tasks.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving tasks. Please try again later." });
            }
        }


        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteTask(int id)
        {            
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("Profile access attempted with valid token but user not found");
                return NotFound(new { message = "User not found." });
            }

            if (id <= 0)
            {
                _logger.LogError("Invalid task id: {id} entered by user: {userEmail}", id, user.Email);
                return BadRequest(new { message = "Invalid id." });
            }

            var task = await _context.ScheduledTasks.FindAsync(id);

            if (task == null)
            {
                _logger.LogWarning("Invalid task {id} selected for deletion", id);
                return NotFound(new { message = $"Task with id {id} not found." });
            }

            _logger.LogInformation("Deleting task {id} for user: {userEmail}", id,user.Email);

            if (task.IsDeleted == true)
            {
                _logger.LogWarning("User {userEmail} requesting deletion for already deleted task {id}", user.Email, id);
                return NotFound(new { message = $"Task with id {id} has already been deleted." });
            }

            task.IsDeleted = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Task {id} deleted by user {userEmail} successfully", id, user.Email);
            return Ok(new { message = $"Task with id {id} has been deleted successfully." });
        }
    }
}
