using InternProjects.Data;
using InternProjects.Models;
using InternProjects.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;

namespace InternProjects.Controllers
{
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        [Authorize(Roles="Intern")]
        public async Task<IActionResult> Intern()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var intern = await _context.Interns
                .Include(i => i.User)
                .FirstOrDefaultAsync(i => i.User.Id == userId);

            if (intern == null) return NotFound();

            var internId = intern.Id;


            var assignments = await _context.TaskAssignments
                .Include(a => a.Task)
                .Where(a => a.InternId == internId)
                .OrderByDescending(a => a.StartDate)
                .ToListAsync();

            var taskIds = assignments.Select(a => a.TaskId).Distinct().ToList();

            var relatedAssignments = await _context.TaskAssignments
                .Where(a => taskIds.Contains(a.TaskId))
                .Select(a => new { a.Id, a.TaskId })
                .ToListAsync();

            var taskIdByAssignment = relatedAssignments
                .ToDictionary(x => x.Id, x => x.TaskId);
            var relatedIds = taskIdByAssignment.Keys.ToList();

            var reviewed = await _context.Submissions
                .Include(s => s.ReviewedBy)
                .Where(s => relatedIds.Contains(s.AssignmentId) && s.MentorFeedback != null)
                .OrderByDescending(s => s.ReviewDate)
                .ThenByDescending(s => s.Version)
                .ToListAsync();

            var feedbackByTask = new Dictionary<int, TaskFeedbackViewModel>();
            foreach (var s in reviewed)
            {
                if (!taskIdByAssignment.TryGetValue(s.AssignmentId, out var tId)) continue;
                if (feedbackByTask.ContainsKey(tId)) continue;

                feedbackByTask[tId] = new TaskFeedbackViewModel
                {
                    TaskId = tId,
                    Feedback = s.MentorFeedback!,
                    ReviewDate = s.ReviewDate,
                    SubmissionStatus = s.StatusSubmission,
                    Version = s.Version,
                    ReviewerName = s.ReviewedBy != null
                        ? $"{s.ReviewedBy.FirstName} {s.ReviewedBy.LastName}"
                        : null
                };
            }

            var freeTasks = await _context.TaskItems
                .Include(t => t.Category)
                .Where(t => t.Status == "Свободна")
                .OrderBy(t => t.Deadline)
                .Take(10)
                .ToListAsync();

            var vm = new InternDashboardViewModel
            {
                InternName = $"{intern.User!.FirstName} {intern.User.LastName}",
                TotalHours = intern.TotalHours,
                TaskHours = intern.TaskHours,
                AddedHours = intern.AddedHours,
                ReportedHours = intern.ReportedHours,
                RemainingHours = intern.RemainingHours,

                ActiveTasks = assignments.Where(a => a.Status == "В процес").ToList(),
                ReturnedTasks = assignments.Where(a => a.Status == "Върната за корекция").ToList(),
                SubmittedTasks = assignments.Where(a => a.Status == "Предадена за проверка").ToList(),
                AcceptedTasks = assignments.Where(a => a.Status == "Приета").ToList(),

                FeedbackByTask = feedbackByTask,
                FreeTasks = freeTasks
            };

            return View(vm);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Admin()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var interns = await _context.Interns
                .Include(i => i.User)
                .ToListAsync();

            var assignments = await _context.TaskAssignments
                .Include(a => a.Task)
                .Include(a => a.Intern).ThenInclude(i => i.User)
                .ToListAsync();

            var vm = new AdminDashboardViewModel
            {
                TotalInterns = interns.Count,
                ActiveInterns = interns.Count(i => i.User != null && i.User.Status == "Активен"),

                TasksForReview = assignments.Count(a => a.Status == "Предадена за проверка"),
                TasksInProgress = assignments.Count(a => a.Status == "В процес"),
                ReturnedTasks = assignments.Count(a => a.Status == "Върната за корекция"),
                AcceptedTasks = assignments.Count(a => a.Status == "Приета"),
                FreeTasks = await _context.TaskItems.CountAsync(t => t.Status == "Свободна"),

                TotalReportedHours = interns.Sum(i => i.ReportedHours),

                LowActivityInterns = interns
                    .Where(i => i.TotalHours > 0 && i.ReportedHours / i.TotalHours < 0.25f)
                    .OrderBy(i => i.ReportedHours)
                    .Take(5)
                    .ToList(),

                NearCompletionInterns = interns
                    .Where(i => i.TotalHours > 0 && i.ReportedHours / i.TotalHours >= 0.8f)
                    .OrderByDescending(i => i.ReportedHours)
                    .Take(5)
                    .ToList(),

                LatestSubmissions = assignments
                    .Where(a => a.SubmitDate != null)
                    .OrderByDescending(a => a.SubmitDate)
                    .Take(8)
                    .ToList()
            };

            return View(vm);
        }
    }
}
