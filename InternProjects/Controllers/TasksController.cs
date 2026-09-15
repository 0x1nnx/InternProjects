using InternProjects.Data;
using InternProjects.Models;
using InternProjects.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InternProjects.Controllers
{
    [Authorize]
    public class TasksController : Controller
    {
        private readonly AppDbContext _context;

        public TasksController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? categoryId, string? difficulty, string? search)
        {
            var query = _context.TaskItems
                .Include(t => t.Category)
                .Where(t => t.Status == "Свободна");

            if (categoryId.HasValue)
                query = query.Where(t => t.CategoryId == categoryId.Value);

            if (!string.IsNullOrEmpty(difficulty))
                query = query.Where(t => t.Difficulty == difficulty);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(t => t.Title.Contains(search)
                    || (t.ShortDescription != null && t.ShortDescription.Contains(search)));

            var vm = new TaskListViewModel
            {
                Tasks = await query.OrderBy(t => t.Deadline).ToListAsync(),
                Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync(),
                CategoryId = categoryId,
                Difficulty = difficulty,
                Search = search
            };

            return View(vm);
        }

        public async Task<IActionResult> Details(int id)
        {
            var task = await _context.TaskItems
                .Include(t => t.Category)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null) return NotFound();

            if(User.IsInRole("Intern"))
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var intern = await _context.Interns.FirstOrDefaultAsync(i => i.UserId == userId);

                if (intern != null)
                {
                    var myAssignment = await _context.TaskAssignments
                        .FirstOrDefaultAsync(a => a.TaskId == id && a.InternId == intern.Id);

                    ViewBag.MyAssignment = myAssignment;

                    if (myAssignment != null)
                    {
                        var teamAssignmentIds = await _context.TaskAssignments
                            .Where(a => a.TaskId == id)
                            .Select(a => a.Id)
                            .ToListAsync();

                        ViewBag.MyFeedback = await _context.Submissions
                            .Include(s => s.ReviewedBy)
                            .Where(s => teamAssignmentIds.Contains(s.AssignmentId)
                                && s.MentorFeedback != null)
                            .OrderByDescending(s => s.ReviewDate)
                            .ThenByDescending(s => s.Version)
                            .FirstOrDefaultAsync();
                    }
                }
            }


            return View(task);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Intern")]
        public async Task<IActionResult> Choose(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var intern = await _context.Interns.FirstOrDefaultAsync(i => i.UserId == userId);
            if (intern == null) return NotFound();

            using var transaction = await _context.Database
                .BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            string title;
            try
            {
                var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == id);
                if (task == null) return NotFound();

                if (task.Status != "Свободна")
                {
                    TempData["Error"] = "Тази задача вече е заета.";
                    return RedirectToAction(nameof(Index));
                }

                bool alreadyHas = await _context.TaskAssignments
                    .AnyAsync(a => a.TaskId == id && a.InternId == intern.Id);
                if (alreadyHas)
                {
                    TempData["Error"] = "Вече си работил по тази задача.";
                    return RedirectToAction(nameof(Index));
                }

                var capacity = task.IsTeamTask ? Math.Max(task.MaxInterns, 1) : 1;
                var taken = await _context.TaskAssignments.CountAsync(a => a.TaskId == id);

                if (taken >= capacity)
                {
                    TempData["Error"] = "Тази задача вече е заета.";
                    return RedirectToAction(nameof(Index));
                }

                _context.TaskAssignments.Add(new TaskAssignment
                {
                    TaskId = task.Id,
                    InternId = intern.Id,
                    Status = "В процес",
                    StartDate = DateTime.Now
                });

                if (taken + 1 >= capacity)
                    task.Status = "В процес";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                title = task.Title;
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Задачата беше заета в същия момент. Опитай друга.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = $"Задачата „{title}\" е твоя. Успех!";
            return RedirectToAction("Intern", "Dashboard");
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create()
        {
            var vm = new TaskCreateViewModel();
            await FillCategories(vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(TaskCreateViewModel model)
        {
            if (model.IsTeamTask && model.MaxInterns < 2)
                ModelState.AddModelError(nameof(model.MaxInterns), "Екипната задача изисква поне 2 стажанти.");
            if (!model.IsTeamTask)
                model.MaxInterns = 1;

            if (model.Deadline != null && model.Deadline < DateTime.Today)
                ModelState.AddModelError(nameof(model.Deadline), "Срокът не може да е в миналото.");

            if (!ModelState.IsValid)
            {
                await FillCategories(model);
                return View(model);
            }

            var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var task = new TaskItem
            {
                Title = model.Title,
                CategoryId = model.CategoryId!.Value,
                ShortDescription = model.ShortDescription,
                LongDescription = model.LongDescription,
                SubmissionFormat = model.SubmissionFormat,
                AssignedHours = model.AssignedHours!.Value,
                Difficulty = model.Difficulty!,
                Priority = model.Priority!,
                SuitableFor = model.SuitableFor,
                IsTeamTask = model.IsTeamTask,
                MaxInterns = model.MaxInterns,
                Deadline = model.Deadline,
                Status = model.PublishNow ? "Свободна" : "Чернова",
                CreatorId = adminId,
                CreationDate = DateTime.Now
            };

            _context.TaskItems.Add(task);
            await _context.SaveChangesAsync();

            TempData["Success"] = model.PublishNow
                ? $"Задачата „{task.Title}\" е създадена и публикувана."
                : $"Задачата „{task.Title}\" е запазена като чернова.";

            return RedirectToAction(nameof(Index));
        }

        private async Task FillCategories(TaskCreateViewModel vm)
        {
            vm.Categories = await _context.Categories
                .OrderBy(c => c.Name)
                .Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                })
                .ToListAsync();
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Manage(string? status)
        {
            var query = _context.TaskItems
                .Include(t => t.Category)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(t => t.Status == status);

            ViewBag.CurrentStatus = status;

            var tasks = await query
                .OrderByDescending(t => t.CreationDate)
                .ToListAsync();

            return View(tasks);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == id);
            if (task == null) return NotFound();

            var vm = new TaskEditViewModel
            {
                Id = task.Id,
                Status = task.Status,
                Title = task.Title,
                CategoryId = task.CategoryId,
                ShortDescription = task.ShortDescription,
                LongDescription = task.LongDescription,
                SubmissionFormat = task.SubmissionFormat ?? "",
                AssignedHours = task.AssignedHours,
                Difficulty = task.Difficulty,
                Priority = task.Priority,
                SuitableFor = task.SuitableFor,
                IsTeamTask = task.IsTeamTask,
                MaxInterns = task.MaxInterns,
                Deadline = task.Deadline
            };
            await FillCategories(vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(TaskEditViewModel model)
        {
            if (model.IsTeamTask && model.MaxInterns < 2)
                ModelState.AddModelError(nameof(model.MaxInterns), "Екипната задача изисква поне 2 стажанти.");
            if (!model.IsTeamTask)
                model.MaxInterns = 1;

            var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == model.Id);
            if (task == null) return NotFound();

            if (task.Status != "Чернова" && task.Status != "Свободна"
                && model.AssignedHours != task.AssignedHours)
            {
                ModelState.AddModelError(nameof(model.AssignedHours),
                    "Часовете не могат да се променят, докато задачата е поета от стажант. Ползвай „Коригиране на часове\" при нужда.");
            }

            if (!ModelState.IsValid)
            {
                model.Status = task.Status;
                await FillCategories(model);
                return View(model);
            }

            task.Title = model.Title;
            task.CategoryId = model.CategoryId!.Value;
            task.ShortDescription = model.ShortDescription;
            task.LongDescription = model.LongDescription;
            task.SubmissionFormat = model.SubmissionFormat;
            task.AssignedHours = model.AssignedHours!.Value;
            task.Difficulty = model.Difficulty!;
            task.Priority = model.Priority!;
            task.SuitableFor = model.SuitableFor;
            task.IsTeamTask = model.IsTeamTask;
            task.MaxInterns = model.MaxInterns;
            task.Deadline = model.Deadline;
            task.UpdateDate = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Задачата „{task.Title}\" е обновена.";
            return RedirectToAction(nameof(Manage));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Publish(int id)
        {
            var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == id);
            if (task == null) return NotFound();

            if (task.Status != "Чернова")
            {
                TempData["Error"] = "Само чернова може да се публикува.";
                return RedirectToAction(nameof(Manage));
            }

            task.Status = "Свободна";
            task.UpdateDate = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"„{task.Title}\" е публикувана и видима за стажантите.";
            return RedirectToAction(nameof(Manage));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Archive(int id)
        {
            var task = await _context.TaskItems.FirstOrDefaultAsync(t => t.Id == id);
            if (task == null) return NotFound();

            if (task.Status == "В процес" || task.Status == "Предадена за проверка"
                || task.Status == "Върната за корекция")
            {
                TempData["Error"] = "Задачата е поета от стажант - първо приключете работата по нея.";
                return RedirectToAction(nameof(Manage));
            }

            task.Status = "Архивирана";
            task.UpdateDate = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"„{task.Title}\" е архивирана.";
            return RedirectToAction(nameof(Manage));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var task = await _context.TaskItems
                .Include(t => t.Assignments)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null) return NotFound();

            if (task.Assignments.Any(a => a.Status == "В процес"
                || a.Status == "Предадена за проверка"
                || a.Status == "Върната за корекция"))
            {
                TempData["Error"] = "Задачата е поета от стажант - приключете работата по нея или я архивирайте.";
                return RedirectToAction(nameof(Manage));
            }

            var title = task.Title;
            var assignmentIds = task.Assignments.Select(a => a.Id).ToList();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var timeLogs = await _context.TimeLogs
                    .Where(t => t.AssignmentId != null && assignmentIds.Contains(t.AssignmentId.Value))
                    .ToListAsync();
                foreach (var log in timeLogs)
                    log.AssignmentId = null;

                var submissions = await _context.Submissions
                    .Where(s => assignmentIds.Contains(s.AssignmentId))
                    .ToListAsync();
                _context.Submissions.RemoveRange(submissions);

                _context.TaskAssignments.RemoveRange(task.Assignments);
                await _context.SaveChangesAsync();

                _context.TaskItems.Remove(task);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Грешка при изтриването. Нищо не е записано - опитай пак.";
                return RedirectToAction(nameof(Manage));
            }

            TempData["Success"] = $"Задачата „{title}\" е изтрита.";
            return RedirectToAction(nameof(Manage));
        }
    }
}