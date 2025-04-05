using Task_Scheduling_API.Models;

namespace Task_Scheduling_API.DTOs
{
    public class CreateTaskDTO
    {
        public required string Title { get; set; }
        public string? Description { get; set; }
        public required Models.TaskStatus Status { get; set; } = Models.TaskStatus.Pending;
        public required Models.TaskType Type { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ScheduledAt { get; set; }
        public int? DelayMinutes { get; set; }
    }
}
