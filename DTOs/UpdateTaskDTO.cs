namespace Task_Scheduling_API.DTOs
{
    public class UpdateTaskDTO
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public Models.TaskType? Type { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public int? DelayMinutes { get; set; }
        public int? RecurrenceInterval { get; set; }  // e.g., 1, 2, 3, etc.
        public string? RecurrenceUnit { get; set; }  // Hours or Days

    }
}
