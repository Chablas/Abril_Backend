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
        public DbSet<ConstructionSiteLogbookControl> ConstructionSiteLogbookControl { get;set; }
        public DbSet<DocumentIdentityType> DocumentIdentityType { get; set; }
        public DbSet<ImageType> ImageType { get; set; }
        public DbSet<IvtControlPdf> IvtControlPdf { get;set; }
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
        public DbSet<ProjectResident> ProjectResident {get;set;}
        public DbSet<ResidentReportIncidence> ResidentReportIncidence {get;set;}
        public DbSet<ResidentReportIncidenceImage> ResidentReportIncidenceImage {get;set;}
        public DbSet<ResidentReportResponse> ResidentReportResponse {get;set;}
        public DbSet<Role> Role {get;set;}
        public DbSet<Schedule> Schedule { get; set; }
        public DbSet<Stage> Stage { get; set; }
        public DbSet<State> State { get; set; }
        public DbSet<SubSpecialty> SubSpecialty { get; set; }
        public DbSet<SubStage> SubStage { get; set; }
        public DbSet<User> User { get; set; }
        public DbSet<UserRegistrationToken> UserRegistrationTokens { get; set; }
        public DbSet<UserRole> UserRole { get; set; }
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

            modelBuilder.Entity<ResidentReportIncidenceImage>()
                .HasOne(i => i.ResidentReportIncidence)
                .WithMany(r => r.Images)
                .HasForeignKey(i => i.ResidentReportIncidenceId);
                
            modelBuilder.Entity<ResidentReportIncidence>()
                .HasOne(r => r.Project)
                .WithMany(p => p.Incidences)
                .HasForeignKey(r => r.ProjectId);

            modelBuilder.Entity<ResidentReportResponse>()
                .HasOne(r => r.ResidentReportIncidence)
                .WithMany(p => p.ResidentReportResponses)
                .HasForeignKey(r => r.ResidentReportIncidenceId);

            modelBuilder.Entity<ResidentReportIncidence>()
                .HasOne(r => r.StateNavigation)
                .WithMany(s => s.ResidentReportIncidences)
                .HasForeignKey(r => r.StateId);
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
            modelBuilder.Entity<MilestoneScheduleHistory>(entity =>
            {
                entity.Property(e => e.IsEqualToLastVersion).HasColumnName("is_equal_to_last_version");
            });
        }
    }
}