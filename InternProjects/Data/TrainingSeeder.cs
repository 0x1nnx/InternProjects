using Microsoft.EntityFrameworkCore;
using InternProjects.Models;

namespace InternProjects.Data
{
    /// <summary>
    /// Създава таблиците на началното обучение и първоначалната структура от модули.
    ///
    /// Схемата се създава явно, защото приложението използва EnsureCreated(), който
    /// добавя таблици само при създаване на нова база - при вече съществуваща база
    /// новите таблици няма да се появят от само себе си.
    /// </summary>
    public static class TrainingSeeder
    {
        public static void EnsureSchema(AppDbContext context)
        {
            context.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'[TrainingModules]', N'U') IS NULL
CREATE TABLE [TrainingModules] (
    [Id] int NOT NULL IDENTITY,
    [SortOrder] int NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Summary] nvarchar(max) NULL,
    [Content] nvarchar(max) NULL,
    [IsPublished] bit NOT NULL,
    [CreationDate] datetime2 NOT NULL,
    [UpdateDate] datetime2 NULL,
    [UpdatedByName] nvarchar(max) NULL,
    CONSTRAINT [PK_TrainingModules] PRIMARY KEY ([Id])
);

IF OBJECT_ID(N'[TrainingSections]', N'U') IS NULL
CREATE TABLE [TrainingSections] (
    [Id] int NOT NULL IDENTITY,
    [ModuleId] int NOT NULL,
    [SortOrder] int NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NULL,
    [Content] nvarchar(max) NULL,
    CONSTRAINT [PK_TrainingSections] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TrainingSections_TrainingModules_ModuleId] FOREIGN KEY ([ModuleId])
        REFERENCES [TrainingModules] ([Id]) ON DELETE CASCADE
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TrainingSections_ModuleId')
CREATE INDEX [IX_TrainingSections_ModuleId] ON [TrainingSections] ([ModuleId]);

IF OBJECT_ID(N'[TrainingTopics]', N'U') IS NULL
CREATE TABLE [TrainingTopics] (
    [Id] int NOT NULL IDENTITY,
    [SectionId] int NOT NULL,
    [SortOrder] int NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Content] nvarchar(max) NULL,
    CONSTRAINT [PK_TrainingTopics] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TrainingTopics_TrainingSections_SectionId] FOREIGN KEY ([SectionId])
        REFERENCES [TrainingSections] ([Id]) ON DELETE CASCADE
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TrainingTopics_SectionId')
CREATE INDEX [IX_TrainingTopics_SectionId] ON [TrainingTopics] ([SectionId]);

IF OBJECT_ID(N'[TrainingImages]', N'U') IS NULL
CREATE TABLE [TrainingImages] (
    [Id] int NOT NULL IDENTITY,
    [OwnerType] nvarchar(max) NOT NULL,
    [OwnerId] int NOT NULL,
    [FileName] nvarchar(max) NOT NULL,
    [OriginalName] nvarchar(max) NOT NULL,
    [Caption] nvarchar(max) NULL,
    [SortOrder] int NOT NULL,
    [UploadDate] datetime2 NOT NULL,
    CONSTRAINT [PK_TrainingImages] PRIMARY KEY ([Id])
);
");
        }

        /// <summary>
        /// Попълва първоначалната структура. Текстът на указанията се въвежда
        /// от администратор през интерфейса, затова тук се задават само заглавията.
        /// </summary>
        public static void Seed(AppDbContext context)
        {
            if (context.TrainingModules.Any()) return;

            var now = DateTime.Now;
            var modules = new List<TrainingModule>
            {
                Module(1, "Първи ден", "Начало на работния ден, задачи и основни правила.",
                    Section("Как се започва работният ден"),
                    Section("Проверка на задачите"),
                    Section("Основни правила за поведение и организация")),

                Module(2, "Работа с обектите", "Отваряне, заключване и ежедневни проверки в обектите.",
                    Section("Отваряне на всеки обект"),
                    Section("Аларми, осветление, почистване"),
                    Section("Проверки при започване на работа"),
                    Section("Заключване на всеки обект"),
                    Section("Какво се прави преди напускане"),
                    Section("Специфични инструкции за обектите",
                        "Указания, които важат само за конкретния обект.",
                        "AKS 1", "AKS 2", "AKS 3")),

                Module(3, "Работа с клиенти", "Посрещане, въпроси, приемане на устройство и комуникация.",
                    Section("Как се посреща клиент"),
                    Section("Как се задават правилните въпроси"),
                    Section("Как се приема устройство"),
                    Section("Как се комуникира при ремонт"),
                    Section("Какво може и какво не може да се обещава на клиент"),
                    Section("Примерни ситуации и правилни отговори")),

                Module(4, "Ремонти", "От приемането на устройството до предаването му на клиента.",
                    Section("Основен процес при приемане на устройство"),
                    Section("Регистрация на ремонт"),
                    Section("Работа със статуса на ремонта"),
                    Section("Предаване на устройство"),
                    Section("Основни правила за работа с клиентски устройства")),

                Module(5, "Поставяне на протектори", "Подготовка, поставяне и проверка на протектори."),

                Module(6, "Продажби", "Касов апарат, продукти и разговор с клиента при продажба.",
                    Section("Работа с касов апарат"),
                    Section("Основни правила при продажба"),
                    Section("Работа с продукти"),
                    Section("Поведение при въпроси от клиент"),
                    Section("Какво да правим, когато не знаем отговора")),

                Module(7, "Вътрешни правила", "Комуникация, дисциплина, работа в екип и чести ситуации.",
                    Section("Комуникация"),
                    Section("Дисциплина"),
                    Section("Чистота и организация"),
                    Section("Работа в екип"),
                    Section("Забрани и ограничения"),
                    Section("Работа с вътрешни системи"),
                    Section("Какво правя, ако...?",
                        "Чести ситуации и как се реагира при всяка от тях.",
                        "Не знам как да изпълня задача",
                        "Няма поставена задача",
                        "Клиент е недоволен",
                        "Направил съм грешка",
                        "Повредил съм устройство",
                        "Не знам каква цена да кажа",
                        "Има проблем в обекта",
                        "Не мога да заключа обекта",
                        "Не мога да изпълня поставена задача"))
            };

            foreach (var module in modules)
                module.CreationDate = now;

            context.TrainingModules.AddRange(modules);
            context.SaveChanges();
        }

        private static TrainingModule Module(int order, string title, string summary,
            params TrainingSection[] sections)
        {
            for (int i = 0; i < sections.Length; i++)
                sections[i].SortOrder = i + 1;

            return new TrainingModule
            {
                SortOrder = order,
                Title = title,
                Summary = summary,
                IsPublished = true,
                Sections = sections.ToList()
            };
        }

        private static TrainingSection Section(string title, string? description = null,
            params string[] topics)
        {
            return new TrainingSection
            {
                Title = title,
                Description = description,
                Topics = topics
                    .Select((t, i) => new TrainingTopic { Title = t, SortOrder = i + 1 })
                    .ToList()
            };
        }
    }
}
