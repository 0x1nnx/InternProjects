using Microsoft.EntityFrameworkCore;
using InternProjects.Models;

namespace InternProjects.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; } = default!;
        public DbSet<Intern> Interns { get; set; } = default!;
        public DbSet<Category> Categories { get; set; } = default!;
        public DbSet<TaskItem> TaskItems { get; set; } = default!;
        public DbSet<TaskAssignment> TaskAssignments { get; set; } = default!;
        public DbSet<Submission> Submissions { get; set; } = default!;
        public DbSet<TimeLog> TimeLogs { get; set; } = default!;
        public DbSet<ActivityLog> ActivityLogs { get; set; } = default!;
        public DbSet<TrainingModule> TrainingModules { get; set; } = default!;
        public DbSet<TrainingSection> TrainingSections { get; set; } = default!;
        public DbSet<TrainingTopic> TrainingTopics { get; set; } = default!;
        public DbSet<TrainingImage> TrainingImages { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TimeLog>()
                .HasOne(t => t.CreatedBy)
                .WithMany()
                .HasForeignKey(t => t.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TimeLog>()
                .HasOne(t => t.ApprovedBy)
                .WithMany()
                .HasForeignKey(t => t.ApprovedById)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Submission>()
                .HasOne(t => t.ReviewedBy)
                .WithMany()
                .HasForeignKey(t => t.ReviewedById)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TaskItem>()
                .HasOne(t => t.Creator)
                .WithMany()
                .HasForeignKey(t => t.CreatorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Intern>()
                .HasIndex(i => i.UserId)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Intern>()
                .HasOne(i => i.Mentor)
                .WithMany()
                .HasForeignKey(i => i.MentorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TimeLog>()
                .HasOne(t => t.Intern)
                .WithMany()
                .HasForeignKey(t => t.InternId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TaskAssignment>()
                .HasOne(t => t.Intern)
                .WithMany(i => i.Assignments)
                .HasForeignKey(t => t.InternId)
                .OnDelete(DeleteBehavior.Cascade);


            modelBuilder.Entity<Submission>()
                .HasOne(s => s.Assignment)
                .WithMany(a => a.Submissions)
                .HasForeignKey(s => s.AssignmentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TaskItem>()
                .HasOne(t => t.Category)
                .WithMany(c => c.Tasks)
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TrainingSection>()
                .HasOne(s => s.Module)
                .WithMany(m => m.Sections)
                .HasForeignKey(s => s.ModuleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TrainingTopic>()
                .HasOne(t => t.Section)
                .WithMany(s => s.Topics)
                .HasForeignKey(t => t.SectionId)
                .OnDelete(DeleteBehavior.Cascade);

        }
    }
}
