using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Workers;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Repositories
{
    public class WorkerSearchRepository : IWorkerSearchRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public WorkerSearchRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<WorkerSearchResultDto>> Search(string? q, int limit)
        {
            using var ctx = _factory.CreateDbContext();
            var hoy = DateOnly.FromDateTime(DateTime.Today);

            var workers = ctx.Worker.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                workers = workers.Where(w =>
                    (w.Person != null && w.Person.FullName != null && w.Person.FullName.ToLower().Contains(term))
                    || (w.Person != null && w.Person.DocumentIdentityCode != null && w.Person.DocumentIdentityCode.ToLower().Contains(term)));
            }

            var baseList = await workers
                .OrderBy(w => w.Person != null ? w.Person.FullName : null)
                .Take(limit)
                .Select(w => new
                {
                    w.Id,
                    ApellidoNombre = w.Person != null ? w.Person.FullName : null,
                    Dni = w.Person != null ? w.Person.DocumentIdentityCode : null,
                    w.Ocupacion,
                    w.Estado
                })
                .ToListAsync();

            var ids = baseList.Select(b => b.Id).ToList();

            var vinculacionActual = await (
                from v in ctx.WorkerVinculacion
                join em in ctx.Contributor on v.EmpresaId equals em.ContributorId into ej
                from em in ej.DefaultIfEmpty()
                where ids.Contains(v.WorkerId)
                      && (v.FechaFin == null || v.FechaFin >= hoy)
                orderby v.FechaInicio descending
                select new
                {
                    v.WorkerId,
                    v.EmpresaId,
                    EmpresaNombre = em != null ? em.ContributorName : null,
                    v.FechaInicio
                }).ToListAsync();

            var porWorker = vinculacionActual
                .GroupBy(x => x.WorkerId)
                .ToDictionary(g => g.Key, g => g.First());

            return baseList.Select(b =>
            {
                porWorker.TryGetValue(b.Id, out var vin);
                return new WorkerSearchResultDto
                {
                    Id = b.Id,
                    ApellidoNombre = b.ApellidoNombre,
                    Dni = b.Dni,
                    Ocupacion = b.Ocupacion,
                    EmpresaActualId = vin?.EmpresaId,
                    EmpresaActual = vin?.EmpresaNombre,
                    Activo = !string.IsNullOrWhiteSpace(b.Estado)
                             && b.Estado.Trim().Equals("ACTIVO", StringComparison.OrdinalIgnoreCase)
                };
            }).ToList();
        }

        public async Task<int> Create(WorkerCreateDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var existeActivo = await ctx.Worker
                .AnyAsync(w => w.Person != null && w.Person.DocumentIdentityCode != null
                            && w.Person.DocumentIdentityCode.ToUpper() == dto.Dni.Trim().ToUpper()
                            && w.Estado == "ACTIVO");
            if (existeActivo)
                throw new AbrilException("Ya existe un trabajador activo con ese DNI.", 409);

            var dniUpper = dto.Dni.Trim().ToUpper();
            var workerExistente = await ctx.Worker
                .Where(w => w.Person != null && w.Person.DocumentIdentityCode != null
                         && w.Person.DocumentIdentityCode.ToUpper() == dniUpper)
                .Select(w => new { w.Id })
                .FirstOrDefaultAsync();
            if (workerExistente != null)
                await VerificarNoActivoEnOtraEmpresaAsync(ctx, workerExistente.Id, dto.EmpresaId);

            var now = DateTimeOffset.UtcNow;

            var worker = new Worker
            {
                Person = new Person
                {
                    FullName = dto.ApellidoNombre,
                    DocumentIdentityCode = dto.Dni.Trim().ToUpper(),
                    Active = true,
                    State = true,
                    CreatedDateTime = DateTime.UtcNow
                },
                Celular = dto.Celular,
                EmailPersonal = dto.EmailPersonal,
                EmailCorporativo = dto.EmailCorporativo,
                FechaNacimiento = dto.FechaNacimiento,
                FechaIngreso = dto.FechaIngreso,
                Categoria = dto.Categoria,
                Ocupacion = dto.Ocupacion,
                Area = dto.Area,
                Subarea = dto.Subarea,
                ContrataCasa = dto.ContrataCasa,
                ObraOficina = dto.ObraOficina,
                Jefatura = dto.Jefatura,
                Procedencia = dto.Procedencia,
                CondicionMedica = dto.CondicionMedica,
                Notas = dto.Notas,
                Sctr = dto.Sctr,
                HabilitadoObra = dto.HabilitadoObra,
                Estado = "ACTIVO",
                CreatedAt = now,
                UpdatedAt = now,
            };

            ctx.Worker.Add(worker);
            await ctx.SaveChangesAsync();

            if (dto.EmpresaId.HasValue || dto.ProyectoId.HasValue)
            {
                ctx.WorkerVinculacion.Add(new WorkerVinculacion
                {
                    WorkerId = worker.Id,
                    EmpresaId = dto.EmpresaId,
                    ProyectoId = dto.ProyectoId,
                    FechaInicio = DateOnly.FromDateTime(DateTime.Today),
                    CreatedAt = now,
                });
                await ctx.SaveChangesAsync();
            }

            return worker.Id;
        }

        public async Task Update(int id, WorkerUpdateDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var worker = await ctx.Worker.Include(w => w.Person).FirstOrDefaultAsync(w => w.Id == id);
            if (worker == null)
                throw new AbrilException("Trabajador no encontrado.", 404);

            if (worker.Person != null) worker.Person.FullName = dto.ApellidoNombre;
            worker.Celular = dto.Celular;
            worker.EmailPersonal = dto.EmailPersonal;
            worker.EmailCorporativo = dto.EmailCorporativo;
            worker.FechaNacimiento = dto.FechaNacimiento;
            worker.FechaIngreso = dto.FechaIngreso;
            worker.Categoria = dto.Categoria;
            worker.Ocupacion = dto.Ocupacion;
            worker.Area = dto.Area;
            worker.Subarea = dto.Subarea;
            worker.ContrataCasa = dto.ContrataCasa;
            worker.ObraOficina = dto.ObraOficina;
            worker.Jefatura = dto.Jefatura;
            worker.Procedencia = dto.Procedencia;
            worker.CondicionMedica = dto.CondicionMedica;
            worker.Notas = dto.Notas;
            worker.Sctr = dto.Sctr;
            worker.HabilitadoObra = dto.HabilitadoObra;
            worker.UpdatedAt = DateTimeOffset.UtcNow;

            if (dto.EmpresaId.HasValue || dto.ProyectoId.HasValue)
            {
                var hoy = DateOnly.FromDateTime(DateTime.Today);
                var vinculacion = await ctx.WorkerVinculacion
                    .Where(v => v.WorkerId == id && (v.FechaFin == null || v.FechaFin >= hoy))
                    .OrderByDescending(v => v.FechaInicio)
                    .FirstOrDefaultAsync();

                if (vinculacion != null)
                {
                    if (dto.EmpresaId.HasValue) vinculacion.EmpresaId = dto.EmpresaId;
                    if (dto.ProyectoId.HasValue) vinculacion.ProyectoId = dto.ProyectoId;
                    vinculacion.UpdatedAt = DateTimeOffset.UtcNow;
                }
                else
                {
                    ctx.WorkerVinculacion.Add(new WorkerVinculacion
                    {
                        WorkerId = id,
                        EmpresaId = dto.EmpresaId,
                        ProyectoId = dto.ProyectoId,
                        FechaInicio = hoy,
                        CreatedAt = DateTimeOffset.UtcNow,
                    });
                }
            }

            await ctx.SaveChangesAsync();
        }

        public async Task Retirar(int id)
        {
            using var ctx = _factory.CreateDbContext();

            var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.Id == id);
            if (worker == null)
                throw new AbrilException("Trabajador no encontrado.", 404);

            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var ahora = DateTimeOffset.UtcNow;

            worker.Estado = "RETIRADO";
            worker.FechaRetiro = hoy;
            worker.UpdatedAt = ahora;

            var vinculacionesAbiertas = await ctx.WorkerVinculacion
                .Where(v => v.WorkerId == id && v.FechaFin == null)
                .ToListAsync();

            foreach (var v in vinculacionesAbiertas)
            {
                v.FechaFin = hoy;
                v.UpdatedAt = ahora;
            }

            await ctx.SaveChangesAsync();
        }

        private static async Task VerificarNoActivoEnOtraEmpresaAsync(AppDbContext ctx, int workerId, int? empresaIdNueva)
        {
            var vinculActiva = await ctx.WorkerVinculacion
                .Where(v => v.WorkerId == workerId && v.FechaFin == null)
                .Select(v => new { v.EmpresaId })
                .FirstOrDefaultAsync();

            if (vinculActiva != null && vinculActiva.EmpresaId.HasValue && vinculActiva.EmpresaId != empresaIdNueva)
                throw new AbrilException(
                    "El trabajador ya se encuentra activo en otra empresa. Debe ser retirado antes de poder registrarlo en una nueva empresa.",
                    400);
        }
    }
}
