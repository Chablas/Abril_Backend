using Microsoft.EntityFrameworkCore;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Features.Costs.Adjudicaciones.Infrastructure.Models;
using Abril_Backend.Features.CostsModule.Shared.Models;

namespace Abril_Backend.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        private readonly string _provider;
        public AppDbContext(DbContextOptions<AppDbContext> opciones, IConfiguration config) : base(opciones)
        {
            _provider = config["Database:DatabaseProvider"] ?? "SqlServer";
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
        public DbSet<ResidentReportResponseImage> ResidentReportResponseImage {get;set;}
        public DbSet<Role> Role {get;set;}
        public DbSet<Stage> Stage { get; set; }
        public DbSet<State> State { get; set; }
        public DbSet<SubSpecialty> SubSpecialty { get; set; }
        public DbSet<SubStage> SubStage { get; set; }
        public DbSet<User> User { get; set; }
        public DbSet<UserPasswordToken> UserPasswordToken {get;set;}
        public DbSet<UserRole> UserRole { get; set; }
        public DbSet<UserSession> UserSession { get; set; }
        public DbSet<UserProject> UserProject { get; set; }
        public DbSet<Contract> Contract { get; set; }
        public DbSet<ContractType> ContractType { get; set; }
        public DbSet<ContractOrigin> ContractOrigin { get; set; }
        public DbSet<PaymentMethod> PaymentMethod { get; set; }
        public DbSet<Contributor> Contributor { get; set; }
        public DbSet<Contractor> Contractor { get; set; }
        public DbSet<ContractorEmail> ContractorEmail { get; set; }
        public DbSet<ContractorState> ContractorState { get; set; }
        public DbSet<Currency> Currency { get; set; }
        public DbSet<WorkItemCategory> WorkItemCategory { get; set; }
        public DbSet<WorkItem> WorkItem { get; set; }
        public DbSet<ProjectSubContractor> ProjectSubContractor { get; set; }
        public DbSet<ProjectSubContractorQuotationFile> ProjectSubContractorQuotationFile { get; set; }
        public DbSet<ProjectSubContractorComparativeFile> ProjectSubContractorComparativeFile { get; set; }
        public DbSet<ProjectSubContractorStatus> ProjectSubContractorStatus { get; set; }
        public DbSet<ProjectSubContractorContract> ProjectSubContractorContract { get; set; }
        public DbSet<ProjectSubContractorSummarySheet> ProjectSubContractorSummarySheet { get; set; }
        public DbSet<ProjectSubContractorBudget> ProjectSubContractorBudget { get; set; }
        public DbSet<ProjectSubContractorSchedule> ProjectSubContractorSchedule { get; set; }
        public DbSet<ProjectSubContractorAttachedQuotation> ProjectSubContractorAttachedQuotation { get; set; }
        public DbSet<ProjectSubContractorServiceOrder> ProjectSubContractorServiceOrder { get; set; }
        public DbSet<ProjectSubContractorFileStatus> ProjectSubContractorFileStatus { get; set; }
        public DbSet<ProjectSubContractorPromissoryNote> ProjectSubContractorPromissoryNote { get; set; }
        public DbSet<StaffProjectEmail> StaffProjectEmail { get; set; }
        public DbSet<AcActividad> AcActividad { get; set; }
        public DbSet<AcEtapa> AcEtapa { get; set; }
        public DbSet<AcActividadPlantilla> AcActividadPlantilla { get; set; }
        public DbSet<AcCategoria> AcCategoria { get; set; }
        public DbSet<AcEspecialidad> AcEspecialidad { get; set; }
        public DbSet<Worker> Worker { get; set; }
        public DbSet<Proyecto> Proyecto { get; set; }
        public DbSet<Empresa> Empresa { get; set; }

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

            modelBuilder.Entity<Person>()
                .HasOne(p => p.User)
                .WithOne(u => u.Person)
                .HasForeignKey<Person>(p => p.UserId);

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

            modelBuilder.Entity<ResidentReportResponseImage>()
                .HasOne(r => r.ResidentReportResponse)
                .WithMany(s => s.Images)
                .HasForeignKey(r => r.ResidentReportResponseId);

            modelBuilder.Entity<ResidentReportIncidence>()
                .HasOne(r => r.StateNavigation)
                .WithMany(s => s.ResidentReportIncidences)
                .HasForeignKey(r => r.StateId);

            modelBuilder.Entity<Contractor>()
                .HasOne(c => c.Contributor)
                .WithMany()
                .HasForeignKey(c => c.ContributorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Contractor>()
                .HasOne(c => c.ContractorState)
                .WithMany()
                .HasForeignKey(c => c.ContractorStateId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Contractor>()
                .HasIndex(c => c.ContributorId)
                .IsUnique();

            modelBuilder.Entity<ContractorEmail>()
                .HasOne(e => e.Contractor)
                .WithMany(c => c.Emails)
                .HasForeignKey(e => e.ContractorId);

            modelBuilder.Entity<ProjectSubContractorQuotationFile>()
                .HasOne(f => f.ProjectSubContractor)
                .WithMany(s => s.QuotationFiles)
                .HasForeignKey(f => f.ProjectSubContractorId);

            modelBuilder.Entity<ProjectSubContractorComparativeFile>()
                .HasOne(f => f.ProjectSubContractor)
                .WithMany(s => s.ComparativeFiles)
                .HasForeignKey(f => f.ProjectSubContractorId);

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.Project)
                .WithMany()
                .HasForeignKey(s => s.ProjectId);

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.Contractor)
                .WithMany()
                .HasForeignKey(s => s.ContractorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Project>()
                .HasOne(p => p.Contributor)
                .WithMany()
                .HasForeignKey(p => p.ContributorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.Contract)
                .WithMany()
                .HasForeignKey(s => s.ProjectSubContractorContractId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.SummarySheet)
                .WithMany()
                .HasForeignKey(s => s.ProjectSubContractorSummarySheetId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.Budget)
                .WithMany()
                .HasForeignKey(s => s.ProjectSubContractorBudgetId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.Schedule)
                .WithMany()
                .HasForeignKey(s => s.ProjectSubContractorScheduleId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.AttachedQuotation)
                .WithMany()
                .HasForeignKey(s => s.ProjectSubContractorAttachedQuotationId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.ServiceOrder)
                .WithMany()
                .HasForeignKey(s => s.ProjectSubContractorServiceOrderId)
                .IsRequired(false);

            // FK: cada tabla de documento → project_sub_contractor_file_status
            modelBuilder.Entity<ProjectSubContractorContract>()
                .HasOne(e => e.FileStatus).WithMany()
                .HasForeignKey(e => e.ProjectSubContractorFileStatusId)
                .IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProjectSubContractorSummarySheet>()
                .HasOne(e => e.FileStatus).WithMany()
                .HasForeignKey(e => e.ProjectSubContractorFileStatusId)
                .IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProjectSubContractorBudget>()
                .HasOne(e => e.FileStatus).WithMany()
                .HasForeignKey(e => e.ProjectSubContractorFileStatusId)
                .IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProjectSubContractorSchedule>()
                .HasOne(e => e.FileStatus).WithMany()
                .HasForeignKey(e => e.ProjectSubContractorFileStatusId)
                .IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProjectSubContractorAttachedQuotation>()
                .HasOne(e => e.FileStatus).WithMany()
                .HasForeignKey(e => e.ProjectSubContractorFileStatusId)
                .IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProjectSubContractorServiceOrder>()
                .HasOne(e => e.FileStatus).WithMany()
                .HasForeignKey(e => e.ProjectSubContractorFileStatusId)
                .IsRequired(false).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.PromissoryNote)
                .WithMany()
                .HasForeignKey(s => s.ProjectSubContractorPromissoryNoteId)
                .IsRequired(false);
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
