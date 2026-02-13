using Microsoft.EntityFrameworkCore;
using Abril_Backend.Infrastructure.Models;

namespace Abril_Backend.Infrastructure.Data {
    public class AppDbContext : DbContext {
        public AppDbContext(DbContextOptions<AppDbContext> opciones): base(opciones) {
        }
        public DbSet<Area> Area {get; set;}
        public DbSet<DocumentIdentityType> DocumentIdentityType {get; set;}
        public DbSet<ImageType> ImageType {get; set;}
        public DbSet<Layer> Layer {get; set;}
        public DbSet<Lesson> Lesson {get; set;}
        public DbSet<LessonImages> LessonImages {get; set;}
        public DbSet<Milestone> Milestone {get;set;}
        public DbSet<MilestoneSchedule> MilestoneSchedule {get;set;}
        public DbSet<MilestoneScheduleHistory> MilestoneScheduleHistory {get;set;}
        public DbSet<Person> Person {get; set;}
        public DbSet<Phase> Phase {get; set;}
        public DbSet<PhaseStageSubStageSubSpecialty> PhaseStageSubStageSubSpecialty {get; set;}
        public DbSet<Project> Project {get; set;}
        public DbSet<Schedule> Schedule {get;set;}
        public DbSet<Stage> Stage {get; set;}
        public DbSet<State> State {get; set;}
        public DbSet<SubSpecialty> SubSpecialty {get; set;}
        public DbSet<SubStage> SubStage {get; set;}
        public DbSet<User> User {get; set;}
        public DbSet<UserRegistrationToken> UserRegistrationTokens { get; set; }
        public DbSet<UserSession> UserSession { get; set; }
        public DbSet<UserProject> UserProject {get;set;}

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasOne(u => u.Person)
                .WithMany()
                .HasForeignKey(u => u.PersonId);
        }
    }
}