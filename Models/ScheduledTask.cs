namespace Task_Scheduling_API.Models
{
    public class ScheduledTask
    {
        public int Id { get; set; }
        public required string UserId { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        public required TaskStatus Status { get; set; }
        public required TaskType Type { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public DateTime? NextRunAt { get; set; }
        public DateTime? LastRunAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public TimeSpan? RecurrenceInterval { get; set; }
        public int RetryCount { get; set; } = 0;
        public int MaxRetries { get; set; } = 3;
        public bool IsDeleted { get; set; }
        public bool NotificationSent { get; set; }
        public DateTime? NotificationSentAt { get; set; }
    }

    public enum TaskStatus { Pending, Queued, Completed, Failed }
    public enum TaskType { OneOff, Delayed, Recurring }
}
