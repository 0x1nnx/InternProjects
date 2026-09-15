namespace InternProjects.Models
{
    /// <summary>
    /// Подточка в секция - напр. "AKS 1" или "Клиент е недоволен".
    /// </summary>
    public class TrainingTopic
    {
        public int Id { get; set; }
        public int SectionId { get; set; }
        public TrainingSection? Section { get; set; }

        public int SortOrder { get; set; }
        public string Title { get; set; } = "";
        public string? Content { get; set; }
    }
}
