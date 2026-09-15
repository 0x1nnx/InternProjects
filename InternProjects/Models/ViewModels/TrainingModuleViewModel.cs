namespace InternProjects.Models.ViewModels
{
    public class TrainingModuleViewModel
    {
        public TrainingModule Module { get; set; } = default!;
        public TrainingModule? Previous { get; set; }
        public TrainingModule? Next { get; set; }
        public List<TrainingModule> AllModules { get; set; } = new();

        /// <summary>Снимките по притежател - ключът е "Section:12".</summary>
        public Dictionary<string, List<TrainingImage>> Images { get; set; } = new();

        public TrainingContentViewModel Content(string? text, string ownerType, int ownerId) =>
            new()
            {
                Text = text,
                Images = Images.TryGetValue($"{ownerType}:{ownerId}", out var list)
                    ? list
                    : new List<TrainingImage>()
            };
    }
}
