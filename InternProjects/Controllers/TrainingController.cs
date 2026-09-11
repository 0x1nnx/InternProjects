using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using InternProjects.Data;
using InternProjects.Models;
using InternProjects.Models.ViewModels;

namespace InternProjects.Controllers
{
    [Authorize]
    public class TrainingController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        private static readonly string[] AllowedImageExtensions =
            { ".png", ".jpg", ".jpeg", ".gif", ".webp" };
        private const long MaxImageSize = 5 * 1024 * 1024;

        public TrainingController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        private bool IsAdmin => User.IsInRole("Admin");

        private string ImagesDir => Path.Combine(FilesController.UploadsRoot(_env), "training");

        // ------------------------------------------------------------------
        // Четене - достъпно за всички влезли потребители
        // ------------------------------------------------------------------

        public async Task<IActionResult> Index()
        {
            var modules = await VisibleModules()
                .Include(m => m.Sections)
                .OrderBy(m => m.SortOrder)
                .ToListAsync();

            return View(modules);
        }

        public async Task<IActionResult> Module(int id)
        {
            var module = await VisibleModules()
                .Include(m => m.Sections.OrderBy(s => s.SortOrder))
                    .ThenInclude(s => s.Topics.OrderBy(t => t.SortOrder))
                .FirstOrDefaultAsync(m => m.Id == id);

            if (module == null) return NotFound();

            var all = await VisibleModules()
                .OrderBy(m => m.SortOrder)
                .Select(m => new TrainingModule { Id = m.Id, Title = m.Title, SortOrder = m.SortOrder })
                .ToListAsync();

            int index = all.FindIndex(m => m.Id == module.Id);

            return View(new TrainingModuleViewModel
            {
                Module = module,
                AllModules = all,
                Previous = index > 0 ? all[index - 1] : null,
                Next = index >= 0 && index < all.Count - 1 ? all[index + 1] : null,
                Images = await LoadModuleImages(module)
            });
        }

        /// <summary>Отдава прикачена снимка. Извън wwwroot, за да иска вход.</summary>
        public async Task<IActionResult> Image(int id)
        {
            var image = await _context.TrainingImages.FindAsync(id);
            if (image == null) return NotFound();

            var fullPath = Path.Combine(ImagesDir, image.FileName);
            if (!System.IO.File.Exists(fullPath)) return NotFound();

            if (!new FileExtensionContentTypeProvider().TryGetContentType(image.FileName, out var contentType))
                contentType = "application/octet-stream";

            return PhysicalFile(fullPath, contentType);
        }

        private IQueryable<TrainingModule> VisibleModules() =>
            IsAdmin
                ? _context.TrainingModules
                : _context.TrainingModules.Where(m => m.IsPublished);

        // ------------------------------------------------------------------
        // Администриране
        // ------------------------------------------------------------------

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Manage()
        {
            var modules = await _context.TrainingModules
                .Include(m => m.Sections)
                    .ThenInclude(s => s.Topics)
                .OrderBy(m => m.SortOrder)
                .ToListAsync();

            return View(modules);
        }

        // --- Модули ---

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult CreateModule()
        {
            SetFormData(nameof(CreateModule), "Нов модул");
            return View("ModuleForm", new TrainingModule { IsPublished = true });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateModule(TrainingModule model)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                ModelState.AddModelError(nameof(model.Title), "Заглавието е задължително.");

            if (!ModelState.IsValid)
            {
                SetFormData(nameof(CreateModule), "Нов модул");
                return View("ModuleForm", model);
            }

            int maxOrder = await _context.TrainingModules.AnyAsync()
                ? await _context.TrainingModules.MaxAsync(m => m.SortOrder)
                : 0;

            model.SortOrder = maxOrder + 1;
            model.CreationDate = DateTime.Now;
            Stamp(model);

            _context.TrainingModules.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Модулът „{model.Title}\" е създаден.";
            return RedirectToAction(nameof(EditModule), new { id = model.Id });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> EditModule(int id)
        {
            var module = await _context.TrainingModules
                .Include(m => m.Sections.OrderBy(s => s.SortOrder))
                    .ThenInclude(s => s.Topics.OrderBy(t => t.SortOrder))
                .FirstOrDefaultAsync(m => m.Id == id);

            if (module == null) return NotFound();

            SetFormData(nameof(EditModule), "Редакция на модул");
            await SetImageData(TrainingImage.OwnerModule, module.Id);
            return View("ModuleForm", module);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditModule(TrainingModule model)
        {
            var module = await _context.TrainingModules.FindAsync(model.Id);
            if (module == null) return NotFound();

            if (string.IsNullOrWhiteSpace(model.Title))
                ModelState.AddModelError(nameof(model.Title), "Заглавието е задължително.");

            if (!ModelState.IsValid)
            {
                await _context.Entry(module).Collection(m => m.Sections).LoadAsync();
                SetFormData(nameof(EditModule), "Редакция на модул");
                await SetImageData(TrainingImage.OwnerModule, model.Id);
                return View("ModuleForm", model);
            }

            module.Title = model.Title;
            module.Summary = model.Summary;
            module.Content = model.Content;
            module.IsPublished = model.IsPublished;
            Stamp(module);

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Модулът „{module.Title}\" е обновен.";
            return RedirectToAction(nameof(EditModule), new { id = module.Id });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteModule(int id)
        {
            var module = await _context.TrainingModules
                .Include(m => m.Sections)
                    .ThenInclude(s => s.Topics)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (module == null)
            {
                TempData["Error"] = "Модулът не е намерен.";
                return RedirectToAction(nameof(Manage));
            }

            var sectionIds = module.Sections.Select(s => s.Id).ToList();
            var topicIds = module.Sections.SelectMany(s => s.Topics).Select(t => t.Id).ToList();

            await RemoveImages(TrainingImage.OwnerTopic, topicIds);
            await RemoveImages(TrainingImage.OwnerSection, sectionIds);
            await RemoveImages(TrainingImage.OwnerModule, new[] { module.Id });

            _context.TrainingModules.Remove(module);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Модулът „{module.Title}\" е изтрит.";
            return RedirectToAction(nameof(Manage));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveModule(int id, string direction)
        {
            var modules = await _context.TrainingModules.OrderBy(m => m.SortOrder).ToListAsync();
            Reorder(modules, id, direction, m => m.Id, (m, order) => m.SortOrder = order);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Manage));
        }

        // --- Секции ---

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> CreateSection(int moduleId)
        {
            var module = await _context.TrainingModules.FindAsync(moduleId);
            if (module == null) return NotFound();

            SetFormData(nameof(CreateSection), "Нова секция");
            ViewData["ModuleTitle"] = module.Title;
            return View("SectionForm", new TrainingSection { ModuleId = moduleId });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSection(TrainingSection model)
        {
            var module = await _context.TrainingModules.FindAsync(model.ModuleId);
            if (module == null) return NotFound();

            if (string.IsNullOrWhiteSpace(model.Title))
                ModelState.AddModelError(nameof(model.Title), "Заглавието е задължително.");

            if (!ModelState.IsValid)
            {
                SetFormData(nameof(CreateSection), "Нова секция");
                ViewData["ModuleTitle"] = module.Title;
                return View("SectionForm", model);
            }

            model.SortOrder = await NextOrder(
                _context.TrainingSections.Where(s => s.ModuleId == model.ModuleId),
                s => s.SortOrder);
            model.Module = null;

            _context.TrainingSections.Add(model);
            Stamp(module);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Секцията „{model.Title}\" е създадена.";
            return RedirectToAction(nameof(EditSection), new { id = model.Id });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> EditSection(int id)
        {
            var section = await _context.TrainingSections
                .Include(s => s.Module)
                .Include(s => s.Topics.OrderBy(t => t.SortOrder))
                .FirstOrDefaultAsync(s => s.Id == id);

            if (section == null) return NotFound();

            SetFormData(nameof(EditSection), "Редакция на секция");
            ViewData["ModuleTitle"] = section.Module?.Title;
            await SetImageData(TrainingImage.OwnerSection, section.Id);
            return View("SectionForm", section);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSection(TrainingSection model)
        {
            var section = await _context.TrainingSections.FindAsync(model.Id);
            if (section == null) return NotFound();

            if (string.IsNullOrWhiteSpace(model.Title))
                ModelState.AddModelError(nameof(model.Title), "Заглавието е задължително.");

            if (!ModelState.IsValid)
            {
                await _context.Entry(section).Collection(s => s.Topics).LoadAsync();
                SetFormData(nameof(EditSection), "Редакция на секция");
                await SetImageData(TrainingImage.OwnerSection, model.Id);
                return View("SectionForm", model);
            }

            section.Title = model.Title;
            section.Description = model.Description;
            section.Content = model.Content;

            await TouchModule(section.ModuleId);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Секцията „{section.Title}\" е обновена.";
            return RedirectToAction(nameof(EditSection), new { id = section.Id });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSection(int id)
        {
            var section = await _context.TrainingSections
                .Include(s => s.Topics)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (section == null)
            {
                TempData["Error"] = "Секцията не е намерена.";
                return RedirectToAction(nameof(Manage));
            }

            int moduleId = section.ModuleId;

            await RemoveImages(TrainingImage.OwnerTopic, section.Topics.Select(t => t.Id).ToList());
            await RemoveImages(TrainingImage.OwnerSection, new[] { section.Id });

            _context.TrainingSections.Remove(section);
            await TouchModule(moduleId);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Секцията „{section.Title}\" е изтрита.";
            return RedirectToAction(nameof(EditModule), new { id = moduleId });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveSection(int id, string direction)
        {
            var section = await _context.TrainingSections.FindAsync(id);
            if (section == null) return NotFound();

            var siblings = await _context.TrainingSections
                .Where(s => s.ModuleId == section.ModuleId)
                .OrderBy(s => s.SortOrder)
                .ToListAsync();

            Reorder(siblings, id, direction, s => s.Id, (s, order) => s.SortOrder = order);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(EditModule), new { id = section.ModuleId });
        }

        // --- Подточки ---

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> CreateTopic(int sectionId)
        {
            var section = await _context.TrainingSections.FindAsync(sectionId);
            if (section == null) return NotFound();

            SetFormData(nameof(CreateTopic), "Нова подточка");
            ViewData["SectionTitle"] = section.Title;
            return View("TopicForm", new TrainingTopic { SectionId = sectionId });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTopic(TrainingTopic model)
        {
            var section = await _context.TrainingSections.FindAsync(model.SectionId);
            if (section == null) return NotFound();

            if (string.IsNullOrWhiteSpace(model.Title))
                ModelState.AddModelError(nameof(model.Title), "Заглавието е задължително.");

            if (!ModelState.IsValid)
            {
                SetFormData(nameof(CreateTopic), "Нова подточка");
                ViewData["SectionTitle"] = section.Title;
                return View("TopicForm", model);
            }

            model.SortOrder = await NextOrder(
                _context.TrainingTopics.Where(t => t.SectionId == model.SectionId),
                t => t.SortOrder);
            model.Section = null;

            _context.TrainingTopics.Add(model);
            await TouchModule(section.ModuleId);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Подточката „{model.Title}\" е създадена.";
            return RedirectToAction(nameof(EditTopic), new { id = model.Id });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> EditTopic(int id)
        {
            var topic = await _context.TrainingTopics
                .Include(t => t.Section)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (topic == null) return NotFound();

            SetFormData(nameof(EditTopic), "Редакция на подточка");
            ViewData["SectionTitle"] = topic.Section?.Title;
            await SetImageData(TrainingImage.OwnerTopic, topic.Id);
            return View("TopicForm", topic);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTopic(TrainingTopic model)
        {
            var topic = await _context.TrainingTopics.FindAsync(model.Id);
            if (topic == null) return NotFound();

            if (string.IsNullOrWhiteSpace(model.Title))
                ModelState.AddModelError(nameof(model.Title), "Заглавието е задължително.");

            if (!ModelState.IsValid)
            {
                SetFormData(nameof(EditTopic), "Редакция на подточка");
                await SetImageData(TrainingImage.OwnerTopic, model.Id);
                return View("TopicForm", model);
            }

            topic.Title = model.Title;
            topic.Content = model.Content;

            var section = await _context.TrainingSections.FindAsync(topic.SectionId);
            if (section != null) await TouchModule(section.ModuleId);

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Подточката „{topic.Title}\" е обновена.";
            return RedirectToAction(nameof(EditTopic), new { id = topic.Id });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTopic(int id)
        {
            var topic = await _context.TrainingTopics.FindAsync(id);
            if (topic == null)
            {
                TempData["Error"] = "Подточката не е намерена.";
                return RedirectToAction(nameof(Manage));
            }

            int sectionId = topic.SectionId;

            await RemoveImages(TrainingImage.OwnerTopic, new[] { topic.Id });
            _context.TrainingTopics.Remove(topic);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Подточката „{topic.Title}\" е изтрита.";
            return RedirectToAction(nameof(EditSection), new { id = sectionId });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveTopic(int id, string direction)
        {
            var topic = await _context.TrainingTopics.FindAsync(id);
            if (topic == null) return NotFound();

            var siblings = await _context.TrainingTopics
                .Where(t => t.SectionId == topic.SectionId)
                .OrderBy(t => t.SortOrder)
                .ToListAsync();

            Reorder(siblings, id, direction, t => t.Id, (t, order) => t.SortOrder = order);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(EditSection), new { id = topic.SectionId });
        }

        // ------------------------------------------------------------------
        // Снимки - извикват се по AJAX, за да не се губи неспазен текст
        // ------------------------------------------------------------------

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxImageSize + 1024 * 1024)]
        public async Task<IActionResult> UploadImage(string ownerType, int ownerId, IFormFile? file)
        {
            if (!await OwnerExists(ownerType, ownerId))
                return Json(new { ok = false, error = "Елементът не е намерен." });

            if (file == null || file.Length == 0)
                return Json(new { ok = false, error = "Не е избран файл." });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedImageExtensions.Contains(ext))
                return Json(new { ok = false, error = "Разрешени са само PNG, JPG, GIF и WEBP." });

            if (file.Length > MaxImageSize)
                return Json(new { ok = false, error = "Файлът е над 5 MB." });

            Directory.CreateDirectory(ImagesDir);

            var fileName = $"{Guid.NewGuid()}{ext}";
            var fullPath = Path.Combine(ImagesDir, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var image = new TrainingImage
            {
                OwnerType = ownerType,
                OwnerId = ownerId,
                FileName = fileName,
                OriginalName = Path.GetFileName(file.FileName),
                UploadDate = DateTime.Now,
                SortOrder = await NextOrder(
                    _context.TrainingImages.Where(i => i.OwnerType == ownerType && i.OwnerId == ownerId),
                    i => i.SortOrder)
            };

            _context.TrainingImages.Add(image);
            await _context.SaveChangesAsync();

            return Json(new
            {
                ok = true,
                id = image.Id,
                marker = image.Marker,
                url = Url.Action(nameof(Image), "Training", new { id = image.Id }),
                originalName = image.OriginalName
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteImage(int id)
        {
            var image = await _context.TrainingImages.FindAsync(id);
            if (image == null) return Json(new { ok = false, error = "Снимката не е намерена." });

            DeleteFile(image.FileName);
            _context.TrainingImages.Remove(image);
            await _context.SaveChangesAsync();

            return Json(new { ok = true, id });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateImageCaption(int id, string? caption)
        {
            var image = await _context.TrainingImages.FindAsync(id);
            if (image == null) return Json(new { ok = false, error = "Снимката не е намерена." });

            image.Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
            await _context.SaveChangesAsync();

            return Json(new { ok = true, id, caption = image.Caption });
        }

        // ------------------------------------------------------------------
        // Помощни
        // ------------------------------------------------------------------

        private void SetFormData(string action, string heading)
        {
            ViewData["FormAction"] = action;
            ViewData["Heading"] = heading;
        }

        private async Task SetImageData(string ownerType, int ownerId)
        {
            ViewData["OwnerType"] = ownerType;
            ViewData["OwnerId"] = ownerId;
            ViewData["Images"] = await _context.TrainingImages
                .Where(i => i.OwnerType == ownerType && i.OwnerId == ownerId)
                .OrderBy(i => i.SortOrder)
                .ToListAsync();
        }

        private async Task<Dictionary<string, List<TrainingImage>>> LoadModuleImages(TrainingModule module)
        {
            var sectionIds = module.Sections.Select(s => s.Id).ToList();
            var topicIds = module.Sections.SelectMany(s => s.Topics).Select(t => t.Id).ToList();

            var images = await _context.TrainingImages
                .Where(i =>
                    (i.OwnerType == TrainingImage.OwnerModule && i.OwnerId == module.Id) ||
                    (i.OwnerType == TrainingImage.OwnerSection && sectionIds.Contains(i.OwnerId)) ||
                    (i.OwnerType == TrainingImage.OwnerTopic && topicIds.Contains(i.OwnerId)))
                .OrderBy(i => i.SortOrder)
                .ToListAsync();

            return images
                .GroupBy(i => $"{i.OwnerType}:{i.OwnerId}")
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        private async Task<bool> OwnerExists(string ownerType, int ownerId) => ownerType switch
        {
            TrainingImage.OwnerModule => await _context.TrainingModules.AnyAsync(m => m.Id == ownerId),
            TrainingImage.OwnerSection => await _context.TrainingSections.AnyAsync(s => s.Id == ownerId),
            TrainingImage.OwnerTopic => await _context.TrainingTopics.AnyAsync(t => t.Id == ownerId),
            _ => false
        };

        private async Task RemoveImages(string ownerType, IReadOnlyCollection<int> ownerIds)
        {
            if (ownerIds.Count == 0) return;

            var images = await _context.TrainingImages
                .Where(i => i.OwnerType == ownerType && ownerIds.Contains(i.OwnerId))
                .ToListAsync();

            foreach (var image in images)
                DeleteFile(image.FileName);

            _context.TrainingImages.RemoveRange(images);
        }

        private void DeleteFile(string fileName)
        {
            try
            {
                var fullPath = Path.Combine(ImagesDir, fileName);
                if (System.IO.File.Exists(fullPath))
                    System.IO.File.Delete(fullPath);
            }
            catch (IOException)
            {
                // Файлът остава на диска, но записът се трие - не спираме заради това.
            }
        }

        private static async Task<int> NextOrder<T>(IQueryable<T> source, System.Linq.Expressions.Expression<Func<T, int>> selector) =>
            await source.AnyAsync() ? await source.MaxAsync(selector) + 1 : 1;

        /// <summary>Размества елемента една позиция нагоре или надолу и преномерира списъка.</summary>
        private static void Reorder<T>(List<T> items, int id, string direction,
            Func<T, int> getId, Action<T, int> setOrder)
        {
            int index = items.FindIndex(x => getId(x) == id);
            if (index < 0) return;

            int target = direction == "up" ? index - 1 : index + 1;
            if (target < 0 || target >= items.Count) return;

            (items[index], items[target]) = (items[target], items[index]);

            for (int i = 0; i < items.Count; i++)
                setOrder(items[i], i + 1);
        }

        private void Stamp(TrainingModule module)
        {
            module.UpdateDate = DateTime.Now;
            module.UpdatedByName = User.Identity?.Name;
        }

        private async Task TouchModule(int moduleId)
        {
            var module = await _context.TrainingModules.FindAsync(moduleId);
            if (module != null) Stamp(module);
        }
    }
}
