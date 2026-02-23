using Microsoft.EntityFrameworkCore;
using Abril_Backend.Infrastructure.Models;

namespace Abril_Backend.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        private readonly string _provider;
        public AppDbContext(DbContextOptions<AppDbContext> opciones, IConfiguration config) : base(opciones)
        {
            _provider = config["DatabaseProvider"] ?? "SqlServer";
        }
        public DbSet<Area> Area { get; set; }
        public DbSet<DocumentIdentityType> DocumentIdentityType { get; set; }
        public DbSet<ImageType> ImageType { get; set; }
        public DbSet<Layer> Layer { get; set; }
        public DbSet<Lesson> Lesson { get; set; }
        public DbSet<LessonImages> LessonImages { get; set; }
        public DbSet<Milestone> Milestone { get; set; }
        public DbSet<MilestoneSchedule> MilestoneSchedule { get; set; }
        public DbSet<MilestoneScheduleHistory> MilestoneScheduleHistory { get; set; }
        public DbSet<Person> Person { get; set; }
        public DbSet<Phase> Phase { get; set; }
        public DbSet<PhaseStageSubStageSubSpecialty> PhaseStageSubStageSubSpecialty { get; set; }
        public DbSet<Project> Project { get; set; }
        public DbSet<Schedule> Schedule { get; set; }
        public DbSet<Stage> Stage { get; set; }
        public DbSet<State> State { get; set; }
        public DbSet<SubSpecialty> SubSpecialty { get; set; }
        public DbSet<SubStage> SubStage { get; set; }
        public DbSet<User> User { get; set; }
        public DbSet<UserRegistrationToken> UserRegistrationTokens { get; set; }
        public DbSet<UserSession> UserSession { get; set; }
        public DbSet<UserProject> UserProject { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (_provider == "PostgreSQL")
            {
                ConfigurePostgreSQL(modelBuilder);
            }
            else
            {
                ConfigureSqlServer(modelBuilder);
            }

            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasOne(u => u.Person)
                .WithMany()
                .HasForeignKey(u => u.PersonId);
        }

        private void ConfigureSqlServer(ModelBuilder modelBuilder)
        {
        }

        private void ConfigurePostgreSQL(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("app_user");
            });
            modelBuilder.Entity<Phase>(entity =>
            {
                entity.Property(e => e.Order).HasColumnName("phase_order");
            });
            modelBuilder.Entity<MilestoneSchedule>(entity =>
            {
                entity.Property(e => e.Order).HasColumnName("milestone_schedule_order");
            });
        }
    }
}