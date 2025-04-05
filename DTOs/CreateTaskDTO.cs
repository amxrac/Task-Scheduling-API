using Task_Scheduling_API.Models;

namespace Task_Scheduling_API.DTOs
{
    public class CreateTaskDTO
    {
        public int UserId { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        public required Models.TaskStatus Status { get; set; }
        public required TaskType Type { get; set; }
        public DateTime CreatedAt = DateTime.UtcNow;
        public DateTime ScheduledAt { get; set; }
        public DateTime DelayMinutes { get; set; }
    }
}
