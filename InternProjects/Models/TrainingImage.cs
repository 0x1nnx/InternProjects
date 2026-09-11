namespace InternProjects.Models
{
    /// <summary>
    /// Снимка, прикачена към модул, секция или подточка от обучението.
    /// Вмъква се в текста чрез маркер [img:Id].
    /// </summary>
    public class TrainingImage
    {
        public const string OwnerModule = "Module";
        public const string OwnerSection = "Section";
        public const string OwnerTopic = "Topic";

        public int Id { get; set; }

        /// <summary>Module, Section или Topic.</summary>
        public string OwnerType { get; set; } = "";
        public int OwnerId { get; set; }

        /// <summary>Името на файла на диска (GUID + разширение).</summary>
        public string FileName { get; set; } = "";

        /// <summary>Оригиналното име, с което е качен файлът.</summary>
        public string OriginalName { get; set; } = "";

        public string? Caption { get; set; }
        public int SortOrder { get; set; }
        public DateTime UploadDate { get; set; }

        public string Marker => $"[img:{Id}]";
    }
}
