namespace InternProjects.Models.ViewModels
{
    /// <summary>Текст плюс снимките, които могат да се вмъкнат в него.</summary>
    public class TrainingContentViewModel
    {
        public string? Text { get; set; }
        public List<TrainingImage> Images { get; set; } = new();

        public bool IsEmpty => string.IsNullOrWhiteSpace(Text) && Images.Count == 0;
    }
}
