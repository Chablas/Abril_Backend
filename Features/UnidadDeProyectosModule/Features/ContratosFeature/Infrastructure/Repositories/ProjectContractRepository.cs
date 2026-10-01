using Microsoft.EntityFrameworkCore;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Infrastructure.Models;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Infrastructure.Repositories
{
    public class ProjectContractRepository : IProjectContractRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ProjectContractRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        /// <summary>Arma el DTO de presentación de un contrato desde su fila cruda + hitos ya
        /// cargados — calcula Amount/EsHitoDeGarantia de cada hito (último por Order = garantía,
        /// nunca uno marcado aparte, según lo acordado).</summary>
        private static void ComputeMilestoneFields(ProjectContractDTO dto)
        {
            for (int i = 0; i < dto.Milestones.Count; i++)
            {
                dto.Milestones[i].Amount = Math.Round(dto.Milestones[i].Percentage / 100m * dto.Amount, 2);
                dto.Milestones[i].EsHitoDeGarantia = i == dto.Milestones.Count - 1;
            }
        }

        public async Task<List<ProjectContractDTO>> GetAllByProjectIdAsync(int projectId)
        {
            using var ctx = _factory.CreateDbContext();

            var contratos = await (
                from pc in ctx.ProjectContract
                join contrib in ctx.Contributor on pc.ContributorId equals contrib.ContributorId
                join ws in ctx.WorkSpecialty on pc.WorkSpecialtyId equals ws.WorkSpecialtyId
                join curr in ctx.Currency on pc.CurrencyId equals curr.CurrencyId
                join status in ctx.ProjectContractStatus on pc.ProjectContractStatusId equals status.ProjectContractStatusId
                where pc.ProjectId == projectId && pc.State
                orderby pc.CreatedDateTime descending
                select new ProjectContractDTO
                {
                    ProjectContractId = pc.ProjectContractId,
                    ProjectId = pc.ProjectId,
                    ContributorId = pc.ContributorId,
                    ContractorName = contrib.ContributorName,
                    WorkSpecialtyId = pc.WorkSpecialtyId,
                    WorkSpecialtyDescription = ws.WorkSpecialtyDescription,
                    ProjectContractStatusId = pc.ProjectContractStatusId,
                    ProjectContractStatusDescription = status.ProjectContractStatusDescription,
                    ContractNumber = pc.ContractNumber,
                    ServiceDescription = pc.ServiceDescription,
                    Amount = pc.Amount,
                    CurrencyId = pc.CurrencyId,
                    CurrencyCode = curr.CurrencyCode,
                    ContractorEmail = pc.ContractorEmail,
                    SigningDate = pc.SigningDate,
                    StartDate = pc.StartDate,
                    EndDate = pc.EndDate,
                    TermDays = pc.TermDays,
                    DetalleServicios = pc.DetalleServicios,
                    CreatedDateTime = pc.CreatedDateTime,
                    CreatedUserId = pc.CreatedUserId,
                    Active = pc.Active,
                    ContractorNotificationSkipped = pc.ContractorNotificationSkipped,
                    ArrivedWithObservations = pc.ArrivedWithObservations,
                    ArrivalObservation = pc.ArrivalObservation,
                    Step6SignedJefeProyectos = pc.Step6SignedJefeProyectos,
                    Step6SignedGerenteInmobiliario = pc.Step6SignedGerenteInmobiliario,
                    Step6SignedGerenteGeneral = pc.Step6SignedGerenteGeneral
                }
            ).ToListAsync();

            return contratos;
        }

        public async Task<ProjectContractDTO?> GetByIdAsync(int projectContractId)
        {
            using var ctx = _factory.CreateDbContext();

            var dto = await (
                from pc in ctx.ProjectContract
                join contrib in ctx.Contributor on pc.ContributorId equals contrib.ContributorId
                join ws in ctx.WorkSpecialty on pc.WorkSpecialtyId equals ws.WorkSpecialtyId
                join curr in ctx.Currency on pc.CurrencyId equals curr.CurrencyId
                join status in ctx.ProjectContractStatus on pc.ProjectContractStatusId equals status.ProjectContractStatusId
                where pc.ProjectContractId == projectContractId && pc.State
                select new ProjectContractDTO
                {
                    ProjectContractId = pc.ProjectContractId,
                    ProjectId = pc.ProjectId,
                    ContributorId = pc.ContributorId,
                    ContractorName = contrib.ContributorName,
                    WorkSpecialtyId = pc.WorkSpecialtyId,
                    WorkSpecialtyDescription = ws.WorkSpecialtyDescription,
                    ProjectContractStatusId = pc.ProjectContractStatusId,
                    ProjectContractStatusDescription = status.ProjectContractStatusDescription,
                    ContractNumber = pc.ContractNumber,
                    ServiceDescription = pc.ServiceDescription,
                    Amount = pc.Amount,
                    CurrencyId = pc.CurrencyId,
                    CurrencyCode = curr.CurrencyCode,
                    ContractorEmail = pc.ContractorEmail,
                    SigningDate = pc.SigningDate,
                    StartDate = pc.StartDate,
                    EndDate = pc.EndDate,
                    TermDays = pc.TermDays,
                    DetalleServicios = pc.DetalleServicios,
                    CreatedDateTime = pc.CreatedDateTime,
                    CreatedUserId = pc.CreatedUserId,
                    Active = pc.Active,
                    ContractorNotificationSkipped = pc.ContractorNotificationSkipped,
                    ArrivedWithObservations = pc.ArrivedWithObservations,
                    ArrivalObservation = pc.ArrivalObservation,
                    Step6SignedJefeProyectos = pc.Step6SignedJefeProyectos,
                    Step6SignedGerenteInmobiliario = pc.Step6SignedGerenteInmobiliario,
                    Step6SignedGerenteGeneral = pc.Step6SignedGerenteGeneral
                }
            ).FirstOrDefaultAsync();

            if (dto == null) return null;

            dto.Milestones = await ctx.ProjectContractMilestone
                .Where(m => m.ProjectContractId == projectContractId && m.State)
                .OrderBy(m => m.Order)
                .Select(m => new ProjectContractMilestoneDTO
                {
                    ProjectContractMilestoneId = m.ProjectContractMilestoneId,
                    ProjectContractId = m.ProjectContractId,
                    Order = m.Order,
                    Description = m.Description,
                    Percentage = m.Percentage,
                    PaidDate = m.PaidDate,
                    ChequeRecibo = m.ChequeRecibo,
                    Observation = m.Observation
                })
                .ToListAsync();

            ComputeMilestoneFields(dto);
            return dto;
        }

        public async Task<int> CreateAsync(ProjectContractCreateDTO dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            // ContractNumber se asigna acá (al crear, no al generar): correlativo POR PROYECTO,
            // sin reiniciar por año (el año de la plantilla {{NUM_CONTRATO}} sale de SigningDate,
            // que puede no existir todavía al crear — mezclar "por año" complicaría el correlativo
            // sin aportar nada, ya que el AÑO visible en el documento ya lo pone el año de firma).
            //
            // Seguridad ante concurrencia: se bloquea la fila de `project` (FOR UPDATE) ANTES de
            // calcular el MAX — así dos creaciones simultáneas del mismo proyecto se serializan.
            // No alcanza con bloquear filas de project_contract: si es el primer contrato del
            // proyecto no hay ninguna fila que bloquear todavía. El índice único parcial
            // (project_id, contract_number) agregado en la migración es el seguro de última
            // instancia por si algo se cuela igual.
            await using var tx = await ctx.Database.BeginTransactionAsync();

            await ctx.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT project_id FROM project WHERE project_id = {dto.ProjectId} FOR UPDATE");

            var maxNumber = await ctx.ProjectContract
                .Where(c => c.ProjectId == dto.ProjectId)
                .Select(c => (int?)c.ContractNumber)
                .MaxAsync() ?? 0;

            var nuevo = new ProjectContract
            {
                ProjectId = dto.ProjectId,
                ContributorId = dto.ContributorId,
                WorkSpecialtyId = dto.WorkSpecialtyId,
                ProjectContractStatusId = 1, // Paso 1: Cotización/comparativo
                ContractNumber = maxNumber + 1,
                ServiceDescription = dto.ServiceDescription,
                Amount = dto.Amount,
                CurrencyId = dto.CurrencyId,
                ContractorEmail = dto.ContractorEmail,
                SigningDate = dto.SigningDate,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                TermDays = dto.TermDays,
                DetalleServicios = dto.DetalleServicios,
                Active = true,
                State = true,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId
            };

            ctx.ProjectContract.Add(nuevo);
            await ctx.SaveChangesAsync();
            await tx.CommitAsync();

            return nuevo.ProjectContractId;
        }

        public async Task EditAsync(int projectContractId, ProjectContractEditDTO dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var contrato = await ctx.ProjectContract
                .FirstOrDefaultAsync(c => c.ProjectContractId == projectContractId && c.State)
                ?? throw new AbrilException("Contrato no encontrado.", 404);

            contrato.ServiceDescription = dto.ServiceDescription;
            contrato.Amount = dto.Amount;
            contrato.CurrencyId = dto.CurrencyId;
            contrato.ContractorEmail = dto.ContractorEmail;
            contrato.SigningDate = dto.SigningDate;
            contrato.StartDate = dto.StartDate;
            contrato.EndDate = dto.EndDate;
            contrato.TermDays = dto.TermDays;
            contrato.DetalleServicios = dto.DetalleServicios;
            contrato.UpdatedDateTime = DateTime.UtcNow;
            contrato.UpdatedUserId = userId;

            await ctx.SaveChangesAsync();
        }

        public async Task<ProjectContractMilestoneDTO> AddMilestoneAsync(
            int projectContractId, ProjectContractMilestoneCreateDTO dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var contrato = await ctx.ProjectContract
                .FirstOrDefaultAsync(c => c.ProjectContractId == projectContractId && c.State)
                ?? throw new AbrilException("Contrato no encontrado.", 404);

            var maxOrder = await ctx.ProjectContractMilestone
                .Where(m => m.ProjectContractId == projectContractId && m.State)
                .Select(m => (int?)m.Order)
                .MaxAsync() ?? 0;

            var nuevo = new ProjectContractMilestone
            {
                ProjectContractId = projectContractId,
                Order = maxOrder + 1,
                Description = dto.Description,
                Percentage = dto.Percentage,
                PaidDate = dto.PaidDate,
                ChequeRecibo = dto.ChequeRecibo,
                Observation = dto.Observation,
                Active = true,
                State = true,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId
            };

            ctx.ProjectContractMilestone.Add(nuevo);
            await ctx.SaveChangesAsync();

            return new ProjectContractMilestoneDTO
            {
                ProjectContractMilestoneId = nuevo.ProjectContractMilestoneId,
                ProjectContractId = nuevo.ProjectContractId,
                Order = nuevo.Order,
                Description = nuevo.Description,
                Percentage = nuevo.Percentage,
                Amount = Math.Round(nuevo.Percentage / 100m * contrato.Amount, 2),
                PaidDate = nuevo.PaidDate,
                ChequeRecibo = nuevo.ChequeRecibo,
                Observation = nuevo.Observation
            };
        }

        public async Task DeleteMilestoneAsync(int projectContractMilestoneId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var hito = await ctx.ProjectContractMilestone
                .FirstOrDefaultAsync(m => m.ProjectContractMilestoneId == projectContractMilestoneId && m.State)
                ?? throw new AbrilException("Hito no encontrado.", 404);

            hito.State = false;
            hito.UpdatedDateTime = DateTime.UtcNow;
            hito.UpdatedUserId = userId;
            await ctx.SaveChangesAsync();
        }

        public async Task SetMilestonePaymentAsync(int projectContractMilestoneId, ProjectContractMilestonePaymentDTO dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var hito = await ctx.ProjectContractMilestone
                .FirstOrDefaultAsync(m => m.ProjectContractMilestoneId == projectContractMilestoneId && m.State)
                ?? throw new AbrilException("Hito no encontrado.", 404);

            hito.PaidDate = dto.PaidDate;
            hito.ChequeRecibo = dto.ChequeRecibo;
            hito.Observation = dto.Observation;
            hito.UpdatedDateTime = DateTime.UtcNow;
            hito.UpdatedUserId = userId;
            await ctx.SaveChangesAsync();
        }

        public async Task<int?> GetProjectContractIdForMilestoneAsync(int projectContractMilestoneId)
        {
            using var ctx = _factory.CreateDbContext();

            return await ctx.ProjectContractMilestone
                .Where(m => m.ProjectContractMilestoneId == projectContractMilestoneId && m.State)
                .Select(m => (int?)m.ProjectContractId)
                .FirstOrDefaultAsync();
        }

        public async Task<ProjectContractGenerationDataDTO> GetGenerationDataAsync(int projectContractId)
        {
            using var ctx = _factory.CreateDbContext();

            var data = await (
                from pc in ctx.ProjectContract
                join proj in ctx.Project on pc.ProjectId equals proj.ProjectId
                join contrib in ctx.Contributor on pc.ContributorId equals contrib.ContributorId
                join ws in ctx.WorkSpecialty on pc.WorkSpecialtyId equals ws.WorkSpecialtyId
                join curr in ctx.Currency on pc.CurrencyId equals curr.CurrencyId
                // Razón social "EL CONTRATANTE" — Contributor asociado al proyecto (Project.ContributorId).
                join projContribJoin in ctx.Contributor on proj.ContributorId equals projContribJoin.ContributorId into projContribGroup
                from projContrib in projContribGroup.DefaultIfEmpty()
                // Representante legal del contratista (opcional — persona natural se representa a sí misma).
                join personJoin in ctx.Person on contrib.LegalRepresentativePersonId equals personJoin.PersonId into personGroup
                from legalRep in personGroup.DefaultIfEmpty()
                where pc.ProjectContractId == projectContractId && pc.State
                select new ProjectContractGenerationDataDTO
                {
                    ProjectContractId = pc.ProjectContractId,
                    ProjectId = pc.ProjectId,
                    FolderName = pc.FolderName,
                    ContractNumber = pc.ContractNumber,
                    ServiceDescription = pc.ServiceDescription,
                    Amount = pc.Amount,
                    CurrencyCode = curr.CurrencyCode,
                    SigningDate = pc.SigningDate,
                    DetalleServicios = pc.DetalleServicios,
                    WorkSpecialtyDescription = ws.WorkSpecialtyDescription,
                    ProjectRazonSocial = projContrib != null ? projContrib.ContributorName : "",
                    ProjectRuc = projContrib != null ? projContrib.ContributorRuc : "",
                    ProjectNombre = proj.ProjectDescription ?? "",
                    ProjectUbicacionObra = proj.ProjectLocation,
                    ProjectDistrito = proj.ProjectDistrict,
                    ContratistaRazonSocial = contrib.ContributorName,
                    ContratistaRuc = contrib.ContributorRuc,
                    ContratistaUbicacion = contrib.ContributorAddress,
                    ContratistaDistrito = contrib.ContributorDistrict,
                    ContratistaRepresentanteNombre = legalRep != null ? legalRep.FullName : null,
                    ContratistaRepresentanteDni = legalRep != null ? legalRep.DocumentIdentityCode : null,
                    ProyectoAbreviatura = proj.Abbreviation
                }
            ).FirstOrDefaultAsync() ?? throw new AbrilException("Contrato no encontrado.", 404);

            data.Milestones = await ctx.ProjectContractMilestone
                .Where(m => m.ProjectContractId == projectContractId && m.State)
                .OrderBy(m => m.Order)
                .Select(m => new ProjectContractMilestoneDTO
                {
                    ProjectContractMilestoneId = m.ProjectContractMilestoneId,
                    ProjectContractId = m.ProjectContractId,
                    Order = m.Order,
                    Description = m.Description,
                    Percentage = m.Percentage,
                    PaidDate = m.PaidDate,
                    ChequeRecibo = m.ChequeRecibo,
                    Observation = m.Observation
                })
                .ToListAsync();

            for (int i = 0; i < data.Milestones.Count; i++)
            {
                data.Milestones[i].Amount = Math.Round(data.Milestones[i].Percentage / 100m * data.Amount, 2);
                data.Milestones[i].EsHitoDeGarantia = i == data.Milestones.Count - 1;
            }

            return data;
        }

        // ── Pasos 4-9 ─────────────────────────────────────────────────────────

        private async Task<ProjectContract> GetContratoOrThrowAsync(AppDbContext ctx, int projectContractId)
            => await ctx.ProjectContract.FirstOrDefaultAsync(c => c.ProjectContractId == projectContractId && c.State)
               ?? throw new AbrilException("Contrato no encontrado.", 404);

        public async Task SetStep4SentAsync(int projectContractId, bool notificationSkipped, int userId)
        {
            using var ctx = _factory.CreateDbContext();
            var contrato = await GetContratoOrThrowAsync(ctx, projectContractId);

            contrato.ContractorNotificationSkipped = notificationSkipped;
            contrato.ProjectContractStatusId = 4;
            contrato.UpdatedDateTime = DateTime.UtcNow;
            contrato.UpdatedUserId = userId;
            await ctx.SaveChangesAsync();
        }

        public async Task SetStep5ArrivalAsync(int projectContractId, ProjectContractStep5ArrivalDTO dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();
            var contrato = await GetContratoOrThrowAsync(ctx, projectContractId);

            contrato.ArrivedWithObservations = dto.ArrivedWithObservations;
            contrato.ArrivalObservation = dto.ArrivalObservation;
            contrato.ProjectContractStatusId = 5;
            contrato.UpdatedDateTime = DateTime.UtcNow;
            contrato.UpdatedUserId = userId;
            await ctx.SaveChangesAsync();
        }

        public async Task SetStep6SignaturesAsync(int projectContractId, ProjectContractStep6SignaturesDTO dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();
            var contrato = await GetContratoOrThrowAsync(ctx, projectContractId);

            contrato.Step6SignedJefeProyectos = dto.Step6SignedJefeProyectos;
            contrato.Step6SignedGerenteInmobiliario = dto.Step6SignedGerenteInmobiliario;
            contrato.Step6SignedGerenteGeneral = dto.Step6SignedGerenteGeneral;
            // Solo avanza el status si todavía no llegó más lejos (evita retroceder si ya
            // está en paso 7+ y alguien vuelve a tildar un checkbox por error).
            if (contrato.ProjectContractStatusId < 6)
                contrato.ProjectContractStatusId = 6;
            contrato.UpdatedDateTime = DateTime.UtcNow;
            contrato.UpdatedUserId = userId;
            await ctx.SaveChangesAsync();
        }

        public async Task SetStep8NotifiedAsync(int projectContractId, int userId)
        {
            using var ctx = _factory.CreateDbContext();
            var contrato = await GetContratoOrThrowAsync(ctx, projectContractId);

            contrato.ProjectContractStatusId = 8;
            contrato.UpdatedDateTime = DateTime.UtcNow;
            contrato.UpdatedUserId = userId;
            await ctx.SaveChangesAsync();
        }

        public async Task SetStep9ClosedAsync(int projectContractId, int userId)
        {
            using var ctx = _factory.CreateDbContext();
            var contrato = await GetContratoOrThrowAsync(ctx, projectContractId);

            contrato.ProjectContractStatusId = 9;
            contrato.UpdatedDateTime = DateTime.UtcNow;
            contrato.UpdatedUserId = userId;
            await ctx.SaveChangesAsync();
        }

        // ── Almacenamiento (SharePoint) ──────────────────────────────────────

        public async Task SetFolderNameAsync(int projectContractId, string folderName)
        {
            using var ctx = _factory.CreateDbContext();
            var contrato = await GetContratoOrThrowAsync(ctx, projectContractId);
            contrato.FolderName = folderName;
            await ctx.SaveChangesAsync();
        }

        public async Task SetContractDocumentAsync(
            int projectContractId, string fileUrl, string originalFileName, string? storageItemId)
        {
            using var ctx = _factory.CreateDbContext();
            var contrato = await GetContratoOrThrowAsync(ctx, projectContractId);
            contrato.ContractFileUrl = fileUrl;
            contrato.ContractOriginalFileName = originalFileName;
            contrato.ContractStorageItemId = storageItemId;
            await ctx.SaveChangesAsync();
        }

        public async Task SetScannedDocAsync(
            int projectContractId, int slot, string fileUrl, string originalFileName, string? storageItemId, int userId)
        {
            using var ctx = _factory.CreateDbContext();
            var contrato = await GetContratoOrThrowAsync(ctx, projectContractId);

            var existente = await ctx.ProjectContractScannedDoc
                .FirstOrDefaultAsync(d => d.ProjectContractId == projectContractId && d.Slot == slot && d.State);

            if (existente != null)
            {
                existente.FileUrl = fileUrl;
                existente.OriginalFileName = originalFileName;
                existente.StorageItemId = storageItemId;
                existente.UpdatedDateTime = DateTime.UtcNow;
                existente.UpdatedUserId = userId;
            }
            else
            {
                ctx.ProjectContractScannedDoc.Add(new ProjectContractScannedDoc
                {
                    ProjectContractId = projectContractId,
                    Slot = slot,
                    FileUrl = fileUrl,
                    OriginalFileName = originalFileName,
                    StorageItemId = storageItemId,
                    Active = true,
                    State = true,
                    CreatedDateTime = DateTime.UtcNow,
                    CreatedUserId = userId
                });
            }

            // Solo avanza el status si todavía no llegó más lejos (mismo criterio que el paso 6).
            if (contrato.ProjectContractStatusId < 7)
                contrato.ProjectContractStatusId = 7;
            contrato.UpdatedDateTime = DateTime.UtcNow;
            contrato.UpdatedUserId = userId;

            await ctx.SaveChangesAsync();
        }
    }
}
