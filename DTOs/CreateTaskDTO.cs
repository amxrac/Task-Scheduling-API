using Task_Scheduling_API.Models;

namespace Task_Scheduling_API.DTOs
{
    public class CreateTaskDTO
    {
        /// <summary>
        /// Title of the task (required).
        /// </summary>
        public required string Title { get; set; }

        /// <summary>
        /// Optional description of the task.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Type of task: 0 (OneOff), 1 (Delayed), 2 (Recurring).
        /// </summary>
        public required Models.TaskType Type { get; set; }

        /// <summary>
        /// When the task is scheduled to start (optional for OneOff).
        /// </summary>
        public DateTime? ScheduledAt { get; set; }

        /// <summary>
        /// Delay in minutes for Delayed tasks (required if Type = Delayed).
        /// </summary>
        public int? DelayMinutes { get; set; }

        /// <summary>
        /// Interval between runs for Recurring tasks (e.g., 1, 2).
        /// </summary>
        public int? RecurrenceInterval { get; set; }

        /// <summary>
        /// Unit of recurrence: 'h' (hours) or 'd' (days) for Recurring tasks.
        /// </summary>
        public string? RecurrenceUnit { get; set; }
    }
}
