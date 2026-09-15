using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using InternProjects.Data;
using InternProjects.Models;
using InternProjects.Models.ViewModels;

namespace InternProjects.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ReviewController : Controller
    {
        private readonly AppDbContext _context;

        public ReviewController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var pending = await _context.TaskAssignments
                .Include(a => a.Task).ThenInclude(t => t!.Category)
                .Include(a => a.Intern).ThenInclude(i => i!.User)
                .Where(a => a.Status == "Предадена за проверка")
                .ToListAsync();

            var withSubmission = await _context.Submissions
                .Select(s => s.AssignmentId)
                .Distinct()
                .ToListAsync();

            var items = pending
                .GroupBy(a => a.TaskId)
                .Select(g =>
                {
                    var lead = g.FirstOrDefault(a => withSubmission.Contains(a.Id)) ?? g.First();
                    return new ReviewListItemViewModel
                    {
                        AssignmentId = lead.Id,
                        TaskTitle = lead.Task?.Title ?? "",
                        CategoryName = lead.Task?.Category?.Name,
                        AssignedHours = lead.Task?.AssignedHours ?? 0,
                        IsTeamTask = lead.Task?.IsTeamTask ?? false,
                        SubmitDate = g.Max(a => a.SubmitDate),
                        MemberNames = g
                            .Select(a => $"{a.Intern?.User?.FirstName} {a.Intern?.User?.LastName}".Trim())
                            .OrderBy(n => n)
                            .ToList()
                    };
                })
                .OrderBy(i => i.SubmitDate)
                .ToList();

            return View(items);
        }

        public async Task<IActionResult> Details(int id)
        {
            var assignment = await _context.TaskAssignments
                .Include(a => a.Task).ThenInclude(t => t!.Category)
                .Include(a => a.Intern).ThenInclude(i => i!.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assignment == null) return NotFound();

            var teamMembers = await _context.TaskAssignments
                .Include(a => a.Intern).ThenInclude(i => i!.User)
                .Where(a => a.TaskId == assignment.TaskId)
                .ToListAsync();

            var teamAssignmentIds = teamMembers.Select(a => a.Id).ToList();

            var submissions = await _context.Submissions
                .Include(s => s.ReviewedBy)
                .Where(s => teamAssignmentIds.Contains(s.AssignmentId))
                .OrderByDescending(s => s.Version)
                .ToListAsync();

            var vm = new ReviewDetailsViewModel
            {
                Assignment = assignment,
                Submissions = submissions,
                LatestSubmission = submissions.FirstOrDefault(),
                IsTeamTask = assignment.Task?.IsTeamTask ?? false,
                TeamMembers = teamMembers
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Accept(int id, string? feedback)
        {
            var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var assignment = await _context.TaskAssignments
                .Include(a => a.Task)
                .Include(a => a.Intern)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assignment == null) return NotFound();

            if (assignment.Status != "Предадена за проверка")
            {
                TempData["Error"] = "Тази задача не чака проверка.";
                return RedirectToAction(nameof(Index));
            }

            var hours = assignment.Task!.AssignedHours;

            var teamAssignments = await _context.TaskAssignments
                .Include(a => a.Intern)
                .Where(a => a.TaskId == assignment.TaskId && a.Status == "Предадена за проверка")
                .ToListAsync();

            var teamAssignmentIds = teamAssignments.Select(a => a.Id).ToList();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                assignment.Task.Status = "Приета";

                foreach (var a in teamAssignments)
                {
                    a.Status = "Приета";

                    _context.TimeLogs.Add(new TimeLog
                    {
                        SourceType = "task",
                        InternId = a.InternId,
                        AssignmentId = a.Id,
                        Hours = hours,
                        Date = DateTime.Now,
                        Description = $"Приета задача: {assignment.Task.Title}",
                        StatusApproval = "Одобрен",
                        CreatedById = adminId,
                        ApprovedById = adminId,
                        CreationDate = DateTime.Now
                    });

                    var member = a.Intern!;
                    member.TaskHours += hours;
                    member.ReportedHours = member.TaskHours + member.AddedHours;
                    member.RemainingHours = Math.Max(0, member.TotalHours - member.ReportedHours);
                }

                var latest = await _context.Submissions
                    .Where(s => teamAssignmentIds.Contains(s.AssignmentId))
                    .OrderByDescending(s => s.Version)
                    .FirstOrDefaultAsync();
                if (latest != null)
                {
                    latest.StatusSubmission = "Прието";
                    latest.ReviewDate = DateTime.Now;
                    latest.ReviewedById = adminId;
                    latest.MentorFeedback = string.IsNullOrWhiteSpace(feedback)
                        ? "Прието. Часовете са отчетени."
                        : feedback;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Грешка при приемането. Нищо не е записано - опитай пак.";
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["Success"] = teamAssignments.Count > 1
                ? $"Задачата е приета. {hours} ч. са начислени на всеки от {teamAssignments.Count} стажанти."
                : $"Задачата е приета. {hours} ч. са начислени на стажанта.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Return(int id, string feedback)
        {
            if (string.IsNullOrWhiteSpace(feedback))
            {
                TempData["Error"] = "При връщане за корекция обратната връзка е задължителна - стажантът трябва да знае какво да поправи.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var result = await SetReviewStatus(id, "Върната за корекция", "Върнато за корекция", feedback);
            if (result != null) return result;

            TempData["Success"] = "Задачата е върната за корекция.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? feedback)
        {
            var result = await SetReviewStatus(id, "Отхвърлена", "Отхвърлено",
                string.IsNullOrWhiteSpace(feedback) ? "Отхвърлено." : feedback);
            if (result != null) return result;

            TempData["Success"] = "Задачата е отхвърлена.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult?> SetReviewStatus(
            int id, string assignmentStatus, string submissionStatus, string feedback)
        {
            var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var assignment = await _context.TaskAssignments
                .Include(a => a.Task)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assignment == null) return NotFound();

            if (assignment.Status != "Предадена за проверка")
            {
                TempData["Error"] = "Тази задача не чака проверка.";
                return RedirectToAction(nameof(Index));
            }

            var teamAssignments = await _context.TaskAssignments
                .Where(a => a.TaskId == assignment.TaskId && a.Status == "Предадена за проверка")
                .ToListAsync();

            foreach (var a in teamAssignments)
                a.Status = assignmentStatus;

            if (assignment.Task != null)
                assignment.Task.Status = assignmentStatus;

            var teamAssignmentIds = teamAssignments.Select(a => a.Id).ToList();

            var latest = await _context.Submissions
                .Where(s => teamAssignmentIds.Contains(s.AssignmentId))
                .OrderByDescending(s => s.Version)
                .FirstOrDefaultAsync();
            if (latest != null)
            {
                latest.StatusSubmission = submissionStatus;
                latest.ReviewDate = DateTime.Now;
                latest.ReviewedById = adminId;
                latest.MentorFeedback = feedback;
            }

            await _context.SaveChangesAsync();
            return null;
        }
    }
}