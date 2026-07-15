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

        public async Task<List<WorkerSearchResultDto>> Search(string? q, int limit, int? empresaIdContratista = null)
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

            // Un contratista solo debe poder buscar/seleccionar trabajadores
            // vinculados actualmente a su propia empresa.
            if (empresaIdContratista.HasValue)
            {
                workers = workers.Where(w => ctx.WorkerVinculacion.Any(v =>
                    v.WorkerId == w.Id
                    && v.EmpresaId == empresaIdContratista.Value
                    && (v.FechaFin == null || v.FechaFin >= hoy)));
            }

            return await EnrichAsync(ctx, workers.OrderBy(w => w.Person != null ? w.Person.FullName : null).Take(limit), hoy);
        }

        public async Task<WorkerSearchResultDto?> GetByUserId(int userId)
        {
            using var ctx = _factory.CreateDbContext();
            var hoy = DateOnly.FromDateTime(DateTime.Today);

            var workers = ctx.Worker.Where(w => w.Person != null && w.Person.UserId == userId).Take(1);
            var result = await EnrichAsync(ctx, workers, hoy);
            return result.FirstOrDefault();
        }

        private static async Task<List<WorkerSearchResultDto>> EnrichAsync(AppDbContext ctx, IQueryable<Worker> workersQuery, DateOnly hoy)
        {
            var baseList = await workersQuery
                .Select(w => new
                {
                    w.Id,
                    ApellidoNombre = w.Person != null ? w.Person.FullName : null,
                    Dni = w.Person != null ? w.Person.DocumentIdentityCode : null,
                    w.Ocupacion,
                    w.Categoria,
                    w.Estado,
                    w.AniosExperiencia,
                    w.FechaIngreso
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
                    v.Puesto,
                    v.FechaInicio
                }).ToListAsync();

            var porWorker = vinculacionActual
                .GroupBy(x => x.WorkerId)
                .ToDictionary(g => g.Key, g => g.First());

            // Obtener EsAbril por empresa
            var empresaIds = vinculacionActual.Where(v => v.EmpresaId.HasValue)
                .Select(v => v.EmpresaId!.Value).Distinct().ToList();
            var esAbrilPorEmpresa = await ctx.Contributor
                .Where(c => empresaIds.Contains(c.ContributorId))
                .Select(c => new { c.ContributorId, c.EsAbril })
                .ToDictionaryAsync(c => c.ContributorId, c => c.EsAbril);

            // Calcular inhabilitados por puntaje SSOMA (>= 10 puntos acumulados)
            var inhabilitadosSet = await ctx.SsomaAmonestaciones
                .Where(a => ids.Contains(a.WorkerId) && a.State)
                .GroupBy(a => a.WorkerId)
                .Where(g => g.Sum(a => a.PuntosInfraccion) >= 10)
                .Select(g => g.Key)
                .ToHashSetAsync();

            return baseList.Select(b =>
            {
                porWorker.TryGetValue(b.Id, out var vin);
                return new WorkerSearchResultDto
                {
                    Id = b.Id,
                    ApellidoNombre = b.ApellidoNombre,
                    Dni = b.Dni,
                    Ocupacion = b.Ocupacion,
                    Categoria = b.Categoria,
                    Cargo = vin?.Puesto,
                    EmpresaActualId = vin?.EmpresaId,
                    EmpresaActual = vin?.EmpresaNombre,
                    Activo = !string.IsNullOrWhiteSpace(b.Estado)
                             && b.Estado.Trim().Equals("ACTIVO", StringComparison.OrdinalIgnoreCase),
                    AniosExperiencia = b.AniosExperiencia,
                    FechaIngreso = b.FechaIngreso,
                    InhabilitadoSsoma = inhabilitadosSet.Contains(b.Id)
                                     || b.Estado == "INHABILITADO_SSOMA",
                    EsAbril = vin?.EmpresaId.HasValue == true
                        && esAbrilPorEmpresa.TryGetValue(vin!.EmpresaId!.Value, out var ea) && ea
                };
            }).ToList();
        }

        public async Task<List<DocumentTypeDto>> GetDocumentTypes()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.DocumentIdentityType
                .Where(t => t.Active && t.State)
                .OrderBy(t => t.DocumentIdentityTypeId)
                .Select(t => new DocumentTypeDto
                {
                    Id = t.DocumentIdentityTypeId,
                    Abreviatura = t.DocumentIdentityTypeAbbreviation,
                    Descripcion = t.DocumentIdentityTypeDescription,
                })
                .ToListAsync();
        }

        public async Task<int> Create(WorkerCreateDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var dniUpper = dto.Dni.Trim().ToUpper();

            var existeActivo = await ctx.Worker
                .AnyAsync(w => w.Person != null && w.Person.DocumentIdentityCode != null
                            && w.Person.DocumentIdentityCode.ToUpper() == dniUpper
                            && w.Estado == "ACTIVO");
            if (existeActivo)
                throw new AbrilException("Ya existe un trabajador activo con ese DNI.", 409);

            var esCasaDto = string.Equals(dto.ContrataCasa?.Trim(), "Casa", StringComparison.OrdinalIgnoreCase);
            if (esCasaDto)
            {
                var existeRetirado = await ctx.Worker
                    .AnyAsync(w => w.Person != null && w.Person.DocumentIdentityCode != null
                                && w.Person.DocumentIdentityCode.ToUpper() == dniUpper
                                && w.Estado != "ACTIVO");
                if (existeRetirado)
                    throw new AbrilException("Ya existe un trabajador registrado con ese DNI (retirado). Use la opción de Reingreso en vez de crear uno nuevo.", 409);
            }

            var workerExistente = await ctx.Worker
                .Where(w => w.Person != null && w.Person.DocumentIdentityCode != null
                         && w.Person.DocumentIdentityCode.ToUpper() == dniUpper)
                .Select(w => new { w.Id })
                .FirstOrDefaultAsync();
            if (workerExistente != null)
                await VerificarNoActivoEnOtraEmpresaAsync(ctx, workerExistente.Id, dto.EmpresaId);

            var now = DateTimeOffset.UtcNow;

            // Reusar Person existente para evitar error 23505 (unique en document_identity_code)
            var person = await ctx.Person
                .FirstOrDefaultAsync(p => p.DocumentIdentityCode != null
                                       && p.DocumentIdentityCode.ToUpper() == dniUpper);
            if (person == null)
            {
                person = new Person
                {
                    FullName = dto.ApellidoNombre,
                    DocumentIdentityCode = dniUpper,
                    PhoneNumber = int.TryParse(dto.Celular, out var ph1) ? ph1 : (int?)null,
                    Active = true,
                    State = true,
                    CreatedDateTime = DateTime.UtcNow
                };
                ctx.Person.Add(person);
                await ctx.SaveChangesAsync();
            }
            if (!string.IsNullOrWhiteSpace(dto.Sexo)) person.Sexo = dto.Sexo;

            var worker = new Worker
            {
                Person = person,
                EmailCorporativo = dto.EmailCorporativo,
                FechaNacimiento = dto.FechaNacimiento,
                FechaIngreso = dto.FechaIngreso,
                Categoria = dto.Categoria,
                Ocupacion = dto.Ocupacion,
                OcupacionId = dto.OcupacionId,
                Puesto = dto.Puesto,
                Area = dto.Area,
                Subarea = dto.Subarea,
                // Match interno: deriva el nodo normalizado area_scope a partir del texto capturado.
                AreaScopeId = Abril_Backend.Shared.Services.AreaScopeMatcher.Resolve(dto.Area, dto.Subarea, dto.ObraOficina),
                ContrataCasa = dto.ContrataCasa,
                ObraOficina = dto.ObraOficina,
                Jefatura = dto.Jefatura,
                Procedencia = dto.Procedencia,
                CondicionMedica = dto.CondicionMedica,
                Notas = dto.Notas,
                Sctr = dto.Sctr,
                HabilitadoObra = dto.HabilitadoObra,
                AniosExperiencia = dto.AniosExperiencia,
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

            if (worker.Person != null)
            {
                worker.Person.FullName      = dto.ApellidoNombre;
                worker.Person.PhoneNumber   = int.TryParse(dto.Celular, out var ph2) ? ph2 : (int?)null;
                if (!string.IsNullOrWhiteSpace(dto.Sexo)) worker.Person.Sexo = dto.Sexo;
            }
            worker.EmailCorporativo = dto.EmailCorporativo;
            worker.FechaNacimiento = dto.FechaNacimiento;
            worker.FechaIngreso = dto.FechaIngreso;
            worker.Categoria = dto.Categoria;
            worker.Ocupacion = dto.Ocupacion;
            worker.OcupacionId = dto.OcupacionId;
            worker.Puesto = dto.Puesto;
            worker.Area = dto.Area;
            worker.Subarea = dto.Subarea;
            // Match interno: deriva el nodo normalizado area_scope a partir del texto capturado.
            worker.AreaScopeId = Abril_Backend.Shared.Services.AreaScopeMatcher.Resolve(worker.Area, worker.Subarea, worker.ObraOficina);
            worker.ContrataCasa = dto.ContrataCasa;
            worker.ObraOficina = dto.ObraOficina;
            worker.Jefatura = dto.Jefatura;
            worker.Procedencia = dto.Procedencia;
            worker.CondicionMedica = dto.CondicionMedica;
            worker.Notas = dto.Notas;
            worker.Sctr = dto.Sctr;
            worker.HabilitadoObra = dto.HabilitadoObra;
            if (dto.AniosExperiencia.HasValue) worker.AniosExperiencia = dto.AniosExperiencia;
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

        public async Task UpdateDatosBasicos(int id, WorkerDatosBasicosDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var worker = await ctx.Worker.Include(w => w.Person).FirstOrDefaultAsync(w => w.Id == id);
            if (worker == null)
                throw new AbrilException("Trabajador no encontrado.", 404);
            if (worker.Person == null)
                throw new AbrilException("El trabajador no tiene datos de persona asociados.", 400);

            if (string.IsNullOrWhiteSpace(dto.NombreCompleto))
                throw new AbrilException("El nombre completo es obligatorio.", 400);

            worker.Person.FullName = dto.NombreCompleto.Trim();

            if (dto.DocumentIdentityTypeId.HasValue)
                worker.Person.DocumentIdentityTypeId = dto.DocumentIdentityTypeId;

            if (!string.IsNullOrWhiteSpace(dto.NumeroDocumento))
            {
                var nuevoDoc = dto.NumeroDocumento.Trim().ToUpper();
                if (!string.Equals(nuevoDoc, worker.Person.DocumentIdentityCode, StringComparison.OrdinalIgnoreCase))
                {
                    var duplicado = await ctx.Person.AnyAsync(p =>
                        p.PersonId != worker.Person.PersonId
                        && p.DocumentIdentityCode != null
                        && p.DocumentIdentityCode.ToUpper() == nuevoDoc);
                    if (duplicado)
                        throw new AbrilException("Ya existe otra persona con ese número de documento.", 409);
                    worker.Person.DocumentIdentityCode = nuevoDoc;
                }
            }

            worker.Person.Cumpleanos = dto.Cumpleanos;
            worker.Person.UpdatedDateTime = DateTime.UtcNow;

            worker.Categoria = dto.Categoria;
            worker.Ocupacion = dto.Ocupacion;
            worker.OcupacionId = dto.OcupacionId;
            worker.Puesto = dto.Puesto;
            worker.UpdatedAt = DateTimeOffset.UtcNow;

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
