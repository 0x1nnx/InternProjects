namespace InternProjects.Models
{
    /// <summary>
    /// Модул от началното обучение - напр. "Първи ден" или "Ремонти".
    /// </summary>
    public class TrainingModule
    {
        public int Id { get; set; }
        public int SortOrder { get; set; }
        public string Title { get; set; } = "";
        public string? Summary { get; set; }

        /// <summary>Общи указания за модула, показвани преди секциите.</summary>
        public string? Content { get; set; }

        /// <summary>Скритите модули се виждат само от администратори.</summary>
        public bool IsPublished { get; set; } = true;

        public DateTime CreationDate { get; set; }
        public DateTime? UpdateDate { get; set; }
        public string? UpdatedByName { get; set; }

        public List<TrainingSection> Sections { get; set; } = new();
    }
}
