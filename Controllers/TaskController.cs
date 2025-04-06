using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Task_Scheduling_API.Data;
using Task_Scheduling_API.DTOs;
using Task_Scheduling_API.Services;
using Task_Scheduling_API.Models;
using Microsoft.EntityFrameworkCore;

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

        [HttpPost("task")]
        public async Task<IActionResult> CreateTask([FromBody] CreateTaskDTO model)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogError("JWT claim missing or invalid");
                return Unauthorized("Missing or Invalid token.");
            }

            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                _logger.LogWarning("Profile access attempted with valid token but user not found");
                return NotFound(new { message = "User not found." });
            }

            _logger.LogInformation("Task creation for user: {Email}", user.Email);

            if (model.ScheduledAt.HasValue && model.ScheduledAt.Value < DateTime.UtcNow)
            {
                _logger.LogError("Invalid time entered by {userEmail}: {Time}", user.Email, model.ScheduledAt);
                return BadRequest(new { message = "Scheduled time cannot be in the past." });
            }

            if (!Enum.IsDefined(typeof(Models.TaskStatus), model.Status))
            {
                return BadRequest(new { message = "Invalid status value. Accepted values are: Pending, Queued, Completed, Failed." });
            }

            if (!Enum.IsDefined(typeof(Models.TaskType), model.Type))
            {
                return BadRequest(new { message = "Invalid status value. Accepted values are: OneOff, Delayed, Recurring." });
            }

            if (!ModelState.IsValid)
            {
                _logger.LogError("Invalid model state {ex} by {userEmail}", ModelState, user.Email);
                return BadRequest(new { message = "Invalid details provided.", errors = ModelState });
            }

            var scheduledTask = new ScheduledTask
            {
                UserId = userId,
                Title = model.Title,
                Description = model.Description,
                Status = model.Status,
                Type = model.Type,
                CreatedAt = DateTime.UtcNow,
                ScheduledAt = CalculateScheduledTime(model),
                NextRunAt = CalculateScheduledTime(model)
            };

            _logger.LogInformation("New task created by {userEmail} at {Timestamp}", user.Email, DateTime.UtcNow);
            await _context.ScheduledTasks.AddAsync(scheduledTask);
            await _context.SaveChangesAsync();

            _logger.LogInformation("New task by user {userEmail} saved", user.Email);

            return StatusCode(StatusCodes.Status201Created, new
            {
                message = "task created successfully",
                id = scheduledTask.Id,
                title = model.Title,
                description = model.Description,
                status = model.Status,
                type = model.Type,
                ScheduledAt = CalculateScheduledTime(model),
            });
        }

        private DateTime CalculateScheduledTime(CreateTaskDTO model)
        {
            if (model.ScheduledAt.HasValue && model.DelayMinutes.HasValue)
            {
                return model.ScheduledAt.Value.AddMinutes(model.DelayMinutes.Value);
            }
            else if (model.ScheduledAt.HasValue && model.DelayMinutes == null)
            {
                return model.ScheduledAt.Value;
            }
            else if (model.DelayMinutes.HasValue && model.ScheduledAt == null)
            {
                return DateTime.UtcNow.AddMinutes(model.DelayMinutes.Value);
            }

            return DateTime.UtcNow;
        }

        [HttpGet("test")]
        public async Task<IActionResult> Test()
        {
            return Ok(new { message = "test successful" });

        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetTask(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogError("JWT claim missing or invalid");
                return Unauthorized("Missing or Invalid token.");
            }

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

    }
}
