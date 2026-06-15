using Microsoft.EntityFrameworkCore;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Features.Costs.Adjudicaciones.Infrastructure.Models;
using Abril_Backend.Features.CostsModule.Shared.Models;
using Abril_Backend.Features.CostsModule.Features.Configuration.ProjectLinkFeature.Infrastructure.Models;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models;
using Abril_Backend.Features.GestionAdministrativa.Lugares.Infrastructure.Models;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Infrastructure.Models;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Features.GestionAdministrativa.Trayectos.Infrastructure.Models;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Features.Evaluaciones.Infrastructure.Models;
using Abril_Backend.Features.Ssoma.Paso.Entities;
using Abril_Backend.Features.Ssoma.Rac.Entities;
using Abril_Backend.Features.SsomaModule.OptFeature.Infrastructure.Models;
using Abril_Backend.Features.SsomaModule.InspeccionFeature.Infrastructure.Models;
using Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Models;
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
        public DbSet<SubArea> SubArea { get; set; }
        public DbSet<ConstructionSiteLogbookControl> ConstructionSiteLogbookControl { get;set; }
        public DbSet<DocumentIdentityType> DocumentIdentityType { get; set; }
        public DbSet<ImageType> ImageType { get; set; }
        public DbSet<IvtControlPdf> IvtControlPdf { get;set; }
        public DbSet<Lesson> Lesson { get; set; }
        public DbSet<LessonImages> LessonImages { get; set; }
        public DbSet<Milestone> Milestone { get; set; }
        public DbSet<MilestoneSchedule> MilestoneSchedule { get; set; }
        public DbSet<MilestoneScheduleHistory> MilestoneScheduleHistory { get; set; }
        public DbSet<Person> Person { get; set; }
        public DbSet<Project> Project { get; set; }
        public DbSet<ProjectResident> ProjectResident {get;set;}
        public DbSet<ResidentReportIncidence> ResidentReportIncidence {get;set;}
        public DbSet<ResidentReportIncidenceImage> ResidentReportIncidenceImage {get;set;}
        public DbSet<ResidentReportResponse> ResidentReportResponse {get;set;}
        public DbSet<ResidentReportResponseImage> ResidentReportResponseImage {get;set;}
        public DbSet<Role> Role {get;set;}
        public DbSet<State> State { get; set; }
        public DbSet<User> User { get; set; }
        public DbSet<UserPasswordToken> UserPasswordToken {get;set;}
        public DbSet<UserRole> UserRole { get; set; }
        public DbSet<UserSession> UserSession { get; set; }
        public DbSet<UserProject> UserProject { get; set; }
        public DbSet<Contract> Contract { get; set; }
        public DbSet<ContractType> ContractType { get; set; }
        public DbSet<ContractModality> ContractModality { get; set; }
        public DbSet<ContractOrigin> ContractOrigin { get; set; }
        public DbSet<PaymentMethod> PaymentMethod { get; set; }
        public DbSet<PaymentForm> PaymentForm { get; set; }
        public DbSet<Contributor> Contributor { get; set; }
        public DbSet<Contractor> Contractor { get; set; }
        public DbSet<ContractorEmail> ContractorEmail { get; set; }
        public DbSet<ContractorPersonType> ContractorPersonType { get; set; }
        public DbSet<ContractorState> ContractorState { get; set; }
        public DbSet<ContractorUser> ContractorUser { get; set; }
        public DbSet<Currency> Currency { get; set; }
        public DbSet<WorkItemCategory> WorkItemCategory { get; set; }
        public DbSet<WorkItemCategoryClause> WorkItemCategoryClause { get; set; }
        public DbSet<WorkItemCategoryAnexo3Clause> WorkItemCategoryAnexo3Clause { get; set; }
        public DbSet<WorkItemCategoryAnexo4Clause> WorkItemCategoryAnexo4Clause { get; set; }
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
        public DbSet<ProjectSubContractorScannedDoc> ProjectSubContractorScannedDoc { get; set; }
        public DbSet<ProjectSubContractorPackage> ProjectSubContractorPackage { get; set; }
        public DbSet<ProjectSubContractorInstructivo> ProjectSubContractorInstructivo { get; set; }
        public DbSet<ProjectSubContractorNonConformingOutput> ProjectSubContractorNonConformingOutput { get; set; }
        public DbSet<ProjectSubContractorToleranceChart> ProjectSubContractorToleranceChart { get; set; }
        public DbSet<ProjectSubContractorFichaTecnica> ProjectSubContractorFichaTecnica { get; set; }
        public DbSet<ProjectSubContractorAnexo> ProjectSubContractorAnexo { get; set; }
        public DbSet<StaffProjectEmail> StaffProjectEmail { get; set; }
        public DbSet<CostosPresupuestosEmail> CostosPresupuestosEmail { get; set; }
        public DbSet<StaffProjectEmailType> StaffProjectEmailType { get; set; }
        public DbSet<ProjectLink> ProjectLink { get; set; }
        public DbSet<ProjectLinkType> ProjectLinkType { get; set; }
        public DbSet<AcActividad> AcActividad { get; set; }
        public DbSet<AcAvanceSemanal> AcAvanceSemanal { get; set; }
        public DbSet<AcEtapa> AcEtapa { get; set; }
        public DbSet<AcActividadPlantilla> AcActividadPlantilla { get; set; }
        public DbSet<AcCategoria> AcCategoria { get; set; }
        public DbSet<AcEspecialidad> AcEspecialidad { get; set; }
        public DbSet<Worker> Worker { get; set; }
        public DbSet<WorkersCategory> WorkersCategory => Set<WorkersCategory>();
        public DbSet<WorkerEmo> WorkerEmo { get; set; }
        public DbSet<WorkerEmoConvalidacion> WorkerEmoConvalidacion { get; set; }
        public DbSet<WorkerVinculacion> WorkerVinculacion { get; set; }
        public DbSet<WorkerProyecto> WorkerProyecto { get; set; }
        public DbSet<SsClinica> SsClinica { get; set; }
        public DbSet<SsClinicaResetToken> SsClinicaResetToken => Set<SsClinicaResetToken>();
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
        public DbSet<SsRetiroAutomaticoLog> SsRetiroAutomaticoLog => Set<SsRetiroAutomaticoLog>();
        public DbSet<AuditoriaCambio> AuditoriaCambios => Set<AuditoriaCambio>();
        public DbSet<SsHabDocumentoVersion> SsHabDocumentoVersion => Set<SsHabDocumentoVersion>();
        public DbSet<SsHabDocumentoArchivo> SsHabDocumentoArchivo => Set<SsHabDocumentoArchivo>();
        public DbSet<SsResetToken> SsResetToken => Set<SsResetToken>();
        public DbSet<SsTareo> SsTareo => Set<SsTareo>();
        public DbSet<SsTareoPartida> SsTareoPartida => Set<SsTareoPartida>();
        public DbSet<SsTareoDetalleCasa> SsTareoDetalleCasa => Set<SsTareoDetalleCasa>();
        public DbSet<SsTareoDetalleContratista> SsTareoDetalleContratista => Set<SsTareoDetalleContratista>();
        public DbSet<CatSubarea> CatSubarea => Set<CatSubarea>();
        public DbSet<CatCategoria> CatCategoria => Set<CatCategoria>();
        public DbSet<CatOcupacion> CatOcupacion => Set<CatOcupacion>();
        public DbSet<WorkerEvento> WorkerEvento => Set<WorkerEvento>();
        public DbSet<SsTrabajadorRestringido> SsTrabajadorRestringido => Set<SsTrabajadorRestringido>();
        public DbSet<CatJefatura> CatJefatura => Set<CatJefatura>();
        public DbSet<SsClinicaEmail> SsClinicaEmail => Set<SsClinicaEmail>();
        public DbSet<GaHoraOpcion> GaHoraOpcion { get; set; }
        public DbSet<GaLugar> GaLugar { get; set; }
        public DbSet<GaMotivoSalida> GaMotivoSalida { get; set; }
        public DbSet<GaSolicitudSalida> GaSolicitudSalida { get; set; }
        public DbSet<GaSolicitudTrayecto> GaSolicitudTrayecto { get; set; }
        public DbSet<GaSolicitudCaptura> GaSolicitudCaptura { get; set; }
        public DbSet<GaRendicion> GaRendicion { get; set; }
        public DbSet<GaTrayecto> GaTrayecto { get; set; }
        // ── Lecciones aprendidas / Áreas (wip/lecciones-aprendidas) ─────────────
        public DbSet<CatalogType> CatalogType => Set<CatalogType>();
        public DbSet<CatalogItem> CatalogItem => Set<CatalogItem>();
        public DbSet<ScopeItem> ScopeItem => Set<ScopeItem>();
        public DbSet<ScopeTemplate> ScopeTemplate => Set<ScopeTemplate>();
        public DbSet<ScopeTemplateItem> ScopeTemplateItem => Set<ScopeTemplateItem>();
        public DbSet<AreaType> AreaType => Set<AreaType>();
        public DbSet<AreaItem> AreaItem => Set<AreaItem>();
        public DbSet<Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Models.AreaScope> AreaScope => Set<Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Models.AreaScope>();
        public DbSet<Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonAreasFeature.Infrastructure.Models.LessonArea> LessonArea => Set<Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonAreasFeature.Infrastructure.Models.LessonArea>();
        public DbSet<Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonRemindersFeature.Infrastructure.Models.ProjectStaffReminder> ProjectStaffReminder => Set<Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonRemindersFeature.Infrastructure.Models.ProjectStaffReminder>();
        public DbSet<Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonRemindersFeature.Infrastructure.Models.LessonJefeReminder> LessonJefeReminder => Set<Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonRemindersFeature.Infrastructure.Models.LessonJefeReminder>();

        // ── master ─────────────────────────────────────────────────────────────
        public DbSet<SsClinicaUsuario> SsClinicaUsuario => Set<SsClinicaUsuario>();
        public DbSet<SsClinicaToken> SsClinicaToken => Set<SsClinicaToken>();
        public DbSet<SsClinicaAuditoria> SsClinicaAuditoria => Set<SsClinicaAuditoria>();
        public DbSet<SsContratistaRol> SsContratistaRoles { get; set; }
        public DbSet<SsContratistaUsuario> SsContratistaUsuarios { get; set; }
        public DbSet<SsContratistaUsuarioProyecto> SsContratistaUsuarioProyectos { get; set; }
        public DbSet<ProjectActivity> ProjectActivity { get; set; }
        public DbSet<EvPeriodo> EvPeriodos => Set<EvPeriodo>();
        public DbSet<EvPlantilla> EvPlantillas => Set<EvPlantilla>();
        public DbSet<EvEvaluacionResidente> EvEvaluacionesResidente => Set<EvEvaluacionResidente>();
        public DbSet<EvEvaluacionResidenteDetalle> EvEvaluacionesResidenteDetalle => Set<EvEvaluacionResidenteDetalle>();
        public DbSet<EvNoAplica> EvNoAplica => Set<EvNoAplica>();
        public DbSet<EvRecordatorioLog> EvRecordatorioLogs => Set<EvRecordatorioLog>();
        public DbSet<EvAsignacionSupervisor> EvAsignacionesSupervisor => Set<EvAsignacionSupervisor>();
        public DbSet<SsomaPasoCategoria> SsomaPasoCategorias { get; set; }
        public DbSet<SsomaPaso> SsomaPasos { get; set; }
        public DbSet<SsomaPasoActividad> SsomaPasoActividades { get; set; }
        public DbSet<SsomaPasoEjecucion> SsomaPasoEjecuciones { get; set; }
        public DbSet<SsomaPasoAuditoria> SsomaPasoAuditorias { get; set; }
        // ── RAC — Reporte de Actos y Condiciones Subestándar ─────────────────
        public DbSet<SsomaRacCategoria> SsomaRacCategorias { get; set; }
        public DbSet<SsomaRacInfraccion> SsomaRacInfracciones { get; set; }
        public DbSet<SsomaUitAnio> SsomaUitAnios { get; set; }
        public DbSet<SsomaRac> SsomaRacs { get; set; }
        public DbSet<SsomaRacFoto> SsomaRacFotos { get; set; }
        public DbSet<SsomaRacPenalidad> SsomaRacPenalidades { get; set; }
        public DbSet<SsomaOpt> SsomaOpt { get; set; }
        public DbSet<SsomaOptTrabajador> SsomaOptTrabajador { get; set; }
        public DbSet<SsomaPet> SsomaPet { get; set; }
        public DbSet<SsomaOptCriterioVerificacion> SsomaOptCriterioVerificacion { get; set; }
        public DbSet<SsomaOptVerificacion> SsomaOptVerificacion { get; set; }
        public DbSet<SsomaOptPaso> SsomaOptPaso { get; set; }
        public DbSet<Feriado> Feriados { get; set; }
        public DbSet<ActivityPredecessor> ActivityPredecessors { get; set; }
        // ── Inspecciones ───────────────────────────────────────────────────────
        public DbSet<SsomaInspeccionTipo> SsomaInspeccionTipo => Set<SsomaInspeccionTipo>();
        public DbSet<SsomaInspeccionChecklistItem> SsomaInspeccionChecklistItem => Set<SsomaInspeccionChecklistItem>();
        public DbSet<SsomaInspeccion> SsomaInspeccion => Set<SsomaInspeccion>();
        public DbSet<SsomaInspeccionRespuesta> SsomaInspeccionRespuesta => Set<SsomaInspeccionRespuesta>();
        public DbSet<SsomaInspeccionHallazgo> SsomaInspeccionHallazgo => Set<SsomaInspeccionHallazgo>();
        public DbSet<SsomaInspeccionHallazgoFoto> SsomaInspeccionHallazgoFoto => Set<SsomaInspeccionHallazgoFoto>();

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

            modelBuilder.Entity<SsProgramacionEmo>()
                .Property(e => e.Origen)
                .HasDefaultValue("Manual");

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

            modelBuilder.Entity<ContractorEmail>()
                .HasOne(e => e.PersonType)
                .WithMany()
                .HasForeignKey(e => e.ContractorPersonTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ContractorUser>()
                .HasOne(cu => cu.Contractor)
                .WithMany(c => c.Users)
                .HasForeignKey(cu => cu.ContractorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ContractorUser>()
                .HasOne(cu => cu.User)
                .WithMany()
                .HasForeignKey(cu => cu.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProjectSubContractorQuotationFile>()
                .HasOne(f => f.ProjectSubContractor)
                .WithMany(s => s.QuotationFiles)
                .HasForeignKey(f => f.ProjectSubContractorId);

            modelBuilder.Entity<ProjectSubContractorComparativeFile>()
                .HasOne(f => f.ProjectSubContractor)
                .WithMany(s => s.ComparativeFiles)
                .HasForeignKey(f => f.ProjectSubContractorId);

            /*modelBuilder.Entity<ProjectSubContractorPackage>()
                .HasOne(f => f.ProjectSubContractor)
                .WithMany(s => s.Packages)
                .HasForeignKey(f => f.ProjectSubContractorId);*/

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

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.Package)
                .WithMany()
                .HasForeignKey(s => s.ProjectSubContractorPackageId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.NonConformingOutput)
                .WithMany()
                .HasForeignKey(s => s.ProjectSubContractorNonConformingOutputId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.ToleranceChart)
                .WithMany()
                .HasForeignKey(s => s.ProjectSubContractorToleranceChartId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.FichaTecnica)
                .WithMany()
                .HasForeignKey(s => s.ProjectSubContractorFichaTecnicaId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractor>()
                .HasOne(s => s.Anexo)
                .WithMany()
                .HasForeignKey(s => s.ProjectSubContractorAnexoId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractorNonConformingOutput>()
                .HasOne(e => e.FileStatus)
                .WithMany()
                .HasForeignKey(e => e.ProjectSubContractorFileStatusId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractorToleranceChart>()
                .HasOne(e => e.FileStatus)
                .WithMany()
                .HasForeignKey(e => e.ProjectSubContractorFileStatusId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractorFichaTecnica>()
                .HasOne(e => e.FileStatus)
                .WithMany()
                .HasForeignKey(e => e.ProjectSubContractorFileStatusId)
                .IsRequired(false);

            modelBuilder.Entity<ProjectSubContractorAnexo>()
                .HasOne(e => e.FileStatus)
                .WithMany()
                .HasForeignKey(e => e.ProjectSubContractorFileStatusId)
                .IsRequired(false);

            modelBuilder.Entity<StaffProjectEmail>()
                .HasOne(s => s.EmailType)
                .WithMany()
                .HasForeignKey(s => s.StaffProjectEmailTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Lecciones aprendidas / Áreas (wip/lecciones-aprendidas) ─────
            // ScopeItem: self-referential parent/children
            modelBuilder.Entity<ScopeItem>()
                .HasOne(s => s.Parent)
                .WithMany(s => s.Children)
                .HasForeignKey(s => s.ScopeItemParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ScopeTemplateItem: self-referential parent/children
            modelBuilder.Entity<ScopeTemplateItem>()
                .HasOne(s => s.Parent)
                .WithMany(s => s.Children)
                .HasForeignKey(s => s.ScopeTemplateItemParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // AreaItem: FK a AreaType (sin jerarquía — eso vive en AreaScope)
            modelBuilder.Entity<AreaItem>()
                .HasOne(a => a.AreaType)
                .WithMany()
                .HasForeignKey(a => a.AreaTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // AreaScope: árbol con FK a AreaItem + self-referential parent
            modelBuilder.Entity<Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Models.AreaScope>()
                .HasOne(s => s.AreaItem)
                .WithMany()
                .HasForeignKey(s => s.AreaItemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Models.AreaScope>()
                .HasOne(s => s.Parent)
                .WithMany()
                .HasForeignKey(s => s.AreaScopeParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── master ─────────────────────────────────────────────────────
            modelBuilder.Entity<SsClinicaUsuario>().HasKey(x => x.ClinicaUsuarioId);
            modelBuilder.Entity<SsClinicaToken>().HasKey(x => x.TokenId);
            modelBuilder.Entity<SsClinicaAuditoria>().HasKey(x => x.AuditoriaId);

            modelBuilder.Entity<WorkerProyecto>()
                .HasOne(wp => wp.Worker)
                .WithMany()
                .HasForeignKey(wp => wp.WorkerId);

            modelBuilder.Entity<SsomaPasoCategoria>().ToTable("ssoma_paso_categoria");
            modelBuilder.Entity<SsomaPaso>().ToTable("ssoma_paso");
            modelBuilder.Entity<SsomaPasoActividad>().ToTable("ssoma_paso_actividad");
            modelBuilder.Entity<SsomaPasoEjecucion>().ToTable("ssoma_paso_ejecucion");
            modelBuilder.Entity<SsomaPaso>()
                .HasMany(x => x.Actividades).WithOne(x => x.Paso).HasForeignKey(x => x.PasoId);
            modelBuilder.Entity<SsomaPasoActividad>()
                .HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId);
            modelBuilder.Entity<SsomaPasoActividad>()
                .HasMany(x => x.Ejecuciones).WithOne(x => x.Actividad).HasForeignKey(x => x.ActividadId);

            // ── RAC — tablas, relaciones y defaults ──────────────────────────
            modelBuilder.Entity<SsomaRacCategoria>().ToTable("ssoma_rac_categoria");
            modelBuilder.Entity<SsomaRacInfraccion>().ToTable("ssoma_rac_infraccion");
            modelBuilder.Entity<SsomaUitAnio>().ToTable("ssoma_uit_anio");
            modelBuilder.Entity<SsomaRac>().ToTable("ssoma_rac");
            modelBuilder.Entity<SsomaRacFoto>().ToTable("ssoma_rac_foto");
            modelBuilder.Entity<SsomaRacPenalidad>().ToTable("ssoma_rac_penalidad");

            modelBuilder.Entity<SsomaRac>()
                .HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId).IsRequired();
            modelBuilder.Entity<SsomaRacFoto>()
                .HasOne(x => x.Rac).WithMany(x => x.Fotos).HasForeignKey(x => x.RacId);
            modelBuilder.Entity<SsomaRacPenalidad>()
                .HasOne(x => x.Rac).WithOne(x => x.Penalidad).HasForeignKey<SsomaRacPenalidad>(x => x.RacId);
            modelBuilder.Entity<SsomaRacPenalidad>()
                .HasOne(x => x.Infraccion).WithMany().HasForeignKey(x => x.InfraccionId).IsRequired(false);

            modelBuilder.Entity<SsomaRac>()
                .Property(x => x.Estado).HasDefaultValue("Abierto");
            modelBuilder.Entity<SsomaRacPenalidad>()
                .Property(x => x.Estado).HasDefaultValue("EnEvaluacion");
            modelBuilder.Entity<SsomaRacFoto>()
                .Property(x => x.Tipo).HasDefaultValue("Hallazgo");
            modelBuilder.Entity<SsomaRacFoto>()
                .Property(x => x.Orden).HasDefaultValue(1);

            // ── OPT — tablas y nombres explícitos ────────────────────────────
            modelBuilder.Entity<SsomaOpt>().ToTable("ssoma_opt");
            modelBuilder.Entity<SsomaOptTrabajador>().ToTable("ssoma_opt_trabajador");
            modelBuilder.Entity<SsomaPet>().ToTable("ssoma_pet");
            modelBuilder.Entity<SsomaOptCriterioVerificacion>().ToTable("ssoma_opt_criterio_verificacion");
            modelBuilder.Entity<SsomaOptVerificacion>().ToTable("ssoma_opt_verificacion");
            modelBuilder.Entity<SsomaOptPaso>().ToTable("ssoma_opt_paso");

            modelBuilder.Entity<SsomaOpt>()
                .Property(x => x.Estado).HasDefaultValue("Completado");

            // ── Inspecciones — tablas y nombres explícitos ────────────────────
            modelBuilder.Entity<SsomaInspeccionTipo>().ToTable("ssoma_inspeccion_tipo");
            modelBuilder.Entity<SsomaInspeccionChecklistItem>().ToTable("ssoma_inspeccion_checklist_item");
            modelBuilder.Entity<SsomaInspeccion>().ToTable("ssoma_inspeccion");
            modelBuilder.Entity<SsomaInspeccionRespuesta>().ToTable("ssoma_inspeccion_respuesta");
            modelBuilder.Entity<SsomaInspeccionHallazgo>().ToTable("ssoma_inspeccion_hallazgo");
            modelBuilder.Entity<SsomaInspeccionHallazgoFoto>().ToTable("ssoma_inspeccion_hallazgo_foto");
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

            modelBuilder.Entity<ProjectSubContractor>(entity =>
            {
                // La convención snake_case produce "step6signed_*" (no inserta guion después
                // del dígito 6). Forzamos los nombres con guion para que coincidan con las
                // columnas realmente creadas en la BD.
                entity.Property(e => e.Step6SignedCostos)
                      .HasColumnName("step6_signed_costos");
                entity.Property(e => e.Step6SignedGerenteInmobiliario)
                      .HasColumnName("step6_signed_gerente_inmobiliario");
                entity.Property(e => e.Step6SignedGerenteGeneral)
                      .HasColumnName("step6_signed_gerente_general");
            });

            modelBuilder.Entity<WorkItemCategoryClause>()
                .HasOne(c => c.WorkItemCategory)
                .WithMany()
                .HasForeignKey(c => c.WorkItemCategoryId)
                .IsRequired();
            modelBuilder.Entity<WorkItemCategoryAnexo3Clause>(entity =>
            {
                // Mismo caso que Anexo4: la convención snake_case produce "work_item_category_anexo3clause"
                // (sin guion antes de "clause" por el dígito 3). Forzamos los nombres con guion.
                entity.ToTable("work_item_category_anexo3_clause");
                entity.Property(e => e.WorkItemCategoryAnexo3ClauseId)
                      .HasColumnName("work_item_category_anexo3_clause_id");
                entity.HasOne(c => c.WorkItemCategory)
                      .WithMany()
                      .HasForeignKey(c => c.WorkItemCategoryId)
                      .IsRequired();
            });
            modelBuilder.Entity<WorkItemCategoryAnexo4Clause>(entity =>
            {
                // La convención snake_case produce "work_item_category_anexo4clause" (no inserta guion
                // antes de "clause" por el dígito 4). Forzamos los nombres con guion para que coincidan
                // con la tabla/columna realmente creadas en la BD.
                entity.ToTable("work_item_category_anexo4_clause");
                entity.Property(e => e.WorkItemCategoryAnexo4ClauseId)
                      .HasColumnName("work_item_category_anexo4_clause_id");
                entity.HasOne(c => c.WorkItemCategory)
                      .WithMany()
                      .HasForeignKey(c => c.WorkItemCategoryId)
                      .IsRequired();
            });
            modelBuilder.Entity<MilestoneSchedule>(entity =>
            {
                entity.Property(e => e.Order).HasColumnName("milestone_schedule_order");
            });
            modelBuilder.Entity<MilestoneScheduleHistory>(entity =>
            {
                entity.Property(e => e.IsEqualToLastVersion).HasColumnName("is_equal_to_last_version");
            });
            modelBuilder.Entity<AuditoriaCambio>(entity =>
            {
                entity.Property(e => e.DatosAnteriores).HasColumnType("jsonb");
                entity.Property(e => e.DatosNuevos).HasColumnType("jsonb");
            });
            modelBuilder.Entity<SsomaPasoAuditoria>(entity =>
            {
                entity.ToTable("ssoma_paso_auditoria");
                entity.Property(e => e.EntidadId).HasColumnName("entidad_id");
                entity.Property(e => e.PasoId).HasColumnName("paso_id");
                entity.Property(e => e.UsuarioId).HasColumnName("usuario_id");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.ValorAnterior).HasColumnName("valor_anterior").HasColumnType("jsonb");
                entity.Property(e => e.ValorNuevo).HasColumnName("valor_nuevo").HasColumnType("jsonb");
            });
            modelBuilder.Entity<WorkerEvento>().ToTable("worker_eventos");
            modelBuilder.Entity<WorkerEvento>().Property(e => e.Datos).HasColumnType("jsonb");

            // ── Lecciones aprendidas / Áreas (wip/lecciones-aprendidas) ─────
            // ScopeItem: evitar ambigüedad en FK self-referential con snake_case
            modelBuilder.Entity<ScopeItem>()
                .Property(s => s.ScopeItemParentId)
                .HasColumnName("scope_item_parent_id");

            // ScopeTemplateItem: evitar ambigüedad en FK self-referential con snake_case
            modelBuilder.Entity<ScopeTemplateItem>()
                .Property(s => s.ScopeTemplateItemParentId)
                .HasColumnName("scope_template_item_parent_id");

            // ── master ─────────────────────────────────────────────────────
            modelBuilder.Entity<SsClinicaAuditoria>().Property(e => e.DetalleAdicional).HasColumnType("jsonb");
            modelBuilder.Entity<ProjectActivity>(entity =>
            {
                entity.ToTable("project_activity");
                entity.Property(e => e.Order).HasColumnName("project_activity_order");
                entity.Property(e => e.ActivityDescription).IsRequired().HasMaxLength(500);
                entity.Property(e => e.ProgressPercentage).HasDefaultValue(0);
                entity.HasOne<ProjectActivity>()
                    .WithMany()
                    .HasForeignKey(e => e.ParentId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Feriado>(entity =>
            {
                entity.ToTable("feriados");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Fecha).IsRequired();
                entity.Property(e => e.Descripcion).HasMaxLength(200);
                entity.HasIndex(e => e.Fecha).IsUnique();
            });

            modelBuilder.Entity<ActivityPredecessor>(entity =>
            {
                entity.ToTable("activity_predecessor");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.ActivityId, e.PredecessorId }).IsUnique();
                entity.HasIndex(e => e.ActivityId);
                entity.HasIndex(e => e.PredecessorId);
                entity.HasOne<ProjectActivity>()
                    .WithMany()
                    .HasForeignKey(e => e.ActivityId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne<ProjectActivity>()
                    .WithMany()
                    .HasForeignKey(e => e.PredecessorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ── RAC — HasColumnName para prefijos conflictivos en snake_case ──
            // "Sp" (SharePoint ID) y "Uit" (Unidad Impositiva Tributaria) pueden
            // producir nombres incorrectos dependiendo de la versión de Humanizer.
            modelBuilder.Entity<SsomaRacFoto>(entity =>
            {
                entity.Property(e => e.SpId).HasColumnName("sp_id");
            });
            modelBuilder.Entity<SsomaRac>(entity =>
            {
                entity.Property(e => e.PdfSpId).HasColumnName("pdf_sp_id");
                entity.Property(e => e.FirmaReportanteSpId).HasColumnName("firma_reportante_sp_id");
                entity.Property(e => e.PdfUrl).HasColumnName("pdf_url");
            });
            modelBuilder.Entity<SsomaRacInfraccion>(entity =>
            {
                entity.Property(e => e.FactorUit).HasColumnName("factor_uit");
            });
            modelBuilder.Entity<SsomaRacPenalidad>(entity =>
            {
                entity.Property(e => e.UitReferencia).HasColumnName("uit_referencia");
                entity.Property(e => e.PdfResolucionUrl).HasColumnName("pdf_resolucion_url");
            });
        }
    }
}
