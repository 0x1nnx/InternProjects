using InternProjects.Models;

namespace InternProjects.Models.ViewModels
{
    public class InternDashboardViewModel
    {
        public string InternName { get; set; } = "";

        public float TotalHours { get; set; }
        public float TaskHours { get; set; }
        public float AddedHours { get; set; }
        public float ReportedHours { get; set; }
        public float RemainingHours { get; set; }
        public int ProgressPercent => TotalHours > 0
            ? (int)Math.Round(ReportedHours / TotalHours * 100)
            : 0;

        public List<TaskAssignment> ActiveTasks { get; set; } = new();
        public List<TaskAssignment> ReturnedTasks { get; set; } = new();
        public List<TaskAssignment> SubmittedTasks { get; set; } = new();
        public List<TaskAssignment> AcceptedTasks { get; set; } = new();

        public Dictionary<int, TaskFeedbackViewModel> FeedbackByTask { get; set; } = new();
        public List<TaskItem> FreeTasks { get; set; } = new();
    }

    public class TaskFeedbackViewModel
    {
        public int TaskId { get; set; }
        public string Feedback { get; set; } = "";
        public DateTime? ReviewDate { get; set; }
        public string SubmissionStatus { get; set; } = "";
        public int Version { get; set; }
        public string? ReviewerName { get; set; }
    }
}
