namespace InternProjects.Models
{
    /// <summary>
    /// Секция в модул. Може да съдържа само текст или да групира подточки.
    /// </summary>
    public class TrainingSection
    {
        public int Id { get; set; }
        public int ModuleId { get; set; }
        public TrainingModule? Module { get; set; }

        public int SortOrder { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string? Content { get; set; }

        public List<TrainingTopic> Topics { get; set; } = new();
    }
}
