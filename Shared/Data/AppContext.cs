using Microsoft.EntityFrameworkCore;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Features.Costs.Adjudicaciones.Infrastructure.Models;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Shared.Models;

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
        public DbSet<Projects> Projects { get; set; }
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
        public DbSet<Company> Company { get; set; }
        public DbSet<CompanyEmail> CompanyEmail { get; set; }
        public DbSet<CompanyState> CompanyState { get; set; }
        public DbSet<Currency> Currency { get; set; }
        public DbSet<WorkItemCategory> WorkItemCategory { get; set; }
        public DbSet<WorkItem> WorkItem { get; set; }
        public DbSet<ProjectSubContractor> ProjectSubContractor { get; set; }
        public DbSet<ProjectSubContractorQuotationFile> ProjectSubContractorQuotationFile { get; set; }
        public DbSet<ProjectSubContractorComparativeFile> ProjectSubContractorComparativeFile { get; set; }
        public DbSet<ProjectSubContractorStatus> ProjectSubContractorStatus { get; set; }
        public DbSet<AcActividad> AcActividad { get; set; }
        public DbSet<AcEtapa> AcEtapa { get; set; }
        public DbSet<AcActividadPlantilla> AcActividadPlantilla { get; set; }
        public DbSet<AcCategoria> AcCategoria { get; set; }
        public DbSet<AcEspecialidad> AcEspecialidad { get; set; }
        public DbSet<Worker> Worker { get; set; }
        public DbSet<Empresa> Empresa { get; set; }
        public DbSet<WorkerEmo> WorkerEmo { get; set; }
        public DbSet<WorkerEmoConvalidacion> WorkerEmoConvalidacion { get; set; }
        public DbSet<WorkerVinculacion> WorkerVinculacion { get; set; }
        public DbSet<SsClinica> SsClinica { get; set; }
        public DbSet<SsMedicoOcupacional> SsMedicoOcupacional { get; set; }
        public DbSet<SsEmoTipo> SsEmoTipo { get; set; }
        public DbSet<SsExamenTipo> SsExamenTipo { get; set; }
        public DbSet<SsRestriccionTipo> SsRestriccionTipo { get; set; }
        public DbSet<SsEmoExamenDetalle> SsEmoExamenDetalle { get; set; }
        public DbSet<SsEmoRestriccion> SsEmoRestriccion { get; set; }
        public DbSet<SsInterconsulta> SsInterconsulta { get; set; }
        public DbSet<SsProgramacionEmo> SsProgramacionEmo { get; set; }
        public DbSet<SsSeguimientoMedico> SsSeguimientoMedico { get; set; }
        public DbSet<SsAlertaEmo> SsAlertaEmo { get; set; }
        public DbSet<SsItemTrabajador> SsItemTrabajador => Set<SsItemTrabajador>();
        public DbSet<SsItemEmpresa> SsItemEmpresa => Set<SsItemEmpresa>();
        public DbSet<SsItemEquipo> SsItemEquipo => Set<SsItemEquipo>();
        public DbSet<SsCriterioEvaluacion> SsCriterioEvaluacion => Set<SsCriterioEvaluacion>();
        public DbSet<SsEmpresaContratista> SsEmpresaContratista => Set<SsEmpresaContratista>();
        public DbSet<SsEmpresaProyecto> SsEmpresaProyecto => Set<SsEmpresaProyecto>();
        public DbSet<SsHabTrabajador> SsHabTrabajador => Set<SsHabTrabajador>();
        public DbSet<SsHabEmpresa> SsHabEmpresa => Set<SsHabEmpresa>();
        public DbSet<SsSctrVidaley> SsSctrVidaley => Set<SsSctrVidaley>();
        public DbSet<SsSctrVidaLeyWorker> SsSctrVidaLeyWorker => Set<SsSctrVidaLeyWorker>();
        public DbSet<SsEquipo> SsEquipo => Set<SsEquipo>();
        public DbSet<SsHabEquipo> SsHabEquipo => Set<SsHabEquipo>();
        public DbSet<SsEvalSupervisor> SsEvalSupervisor => Set<SsEvalSupervisor>();
        public DbSet<SsEvalSupervisorItem> SsEvalSupervisorItem => Set<SsEvalSupervisorItem>();
        public DbSet<SsInduccion> SsInduccion => Set<SsInduccion>();
        public DbSet<SsRegistroModelo> SsRegistroModelo => Set<SsRegistroModelo>();
        public DbSet<SsItemTrabajadorRegla> SsItemTrabajadorRegla => Set<SsItemTrabajadorRegla>();
        public DbSet<SsHabBloqueoLog> SsHabBloqueoLog => Set<SsHabBloqueoLog>();
        public DbSet<AuditoriaCambio> AuditoriaCambios => Set<AuditoriaCambio>();
        public DbSet<SsHabDocumentoVersion> SsHabDocumentoVersion => Set<SsHabDocumentoVersion>();
        public DbSet<SsResetToken> SsResetToken => Set<SsResetToken>();

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
            modelBuilder.Entity<CompanyEmail>()
                .HasOne(e => e.Company)
                .WithMany(c => c.Emails)
                .HasForeignKey(e => e.CompanyId);

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

            modelBuilder.Entity<CompanyEmail>()
                .HasOne(e => e.Company)
                .WithMany(c => c.Emails)
                .HasForeignKey(e => e.CompanyId);
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
            modelBuilder.Entity<CompanyEmail>(entity =>
            {
               entity.Property(e => e.Email).HasColumnName("company_email"); 
            });
        }
    }
}