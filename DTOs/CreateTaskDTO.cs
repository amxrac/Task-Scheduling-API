﻿using Task_Scheduling_API.Models;

namespace Task_Scheduling_API.DTOs
{
    public class CreateTaskDTO
    {
        public required string Title { get; set; }
        public string? Description { get; set; }
        public required Models.TaskType Type { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public int? DelayMinutes { get; set; }
        public int? RecurrenceInterval { get; set; }  // e.g., 1, 2, 3, etc.
        public string? RecurrenceUnit { get; set; }  // Hours or Days
    }
}
