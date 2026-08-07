using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Workers;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Services.AreaScope.Interfaces;
using Abril_Backend.Shared.Services.Revisores.Interfaces;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Repositories
{
    public class WorkerSearchRepository : IWorkerSearchRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IAreaScopeLegacyResolver _areaLegacyResolver;
        private readonly IJefePersonalizadoService _jefePersonalizado;

        public WorkerSearchRepository(
            IDbContextFactory<AppDbContext> factory,
            IAreaScopeLegacyResolver areaLegacyResolver,
            IJefePersonalizadoService jefePersonalizado)
        {
            _factory = factory;
            _areaLegacyResolver = areaLegacyResolver;
            _jefePersonalizado = jefePersonalizado;
        }

        /// <summary>
        /// Área a persistir.
        ///
        /// Si el formulario mandó el nodo del árbol, ese es la fuente de verdad y los campos legacy
        /// que hayan llegado en null se derivan de él (dirección nueva). Los que lleguen con valor se
        /// respetan: así un formulario que muestra los desplegables de área manda los tres en null y
        /// deja que se deriven, y uno que no los muestra puede reenviar intactos los que ya estaban
        /// guardados sin que se reescriban.
        ///
        /// Si no vino nodo se conserva el comportamiento viejo: se guardan los textos capturados y se
        /// intenta derivar el nodo a partir de la subárea (dirección original de AreaScopeMatcher).
        /// </summary>
        private async Task<(int? AreaScopeId, string? Area, string? Subarea, string? Jefatura)> ResolverAreaAsync(
            int? areaScopeId, string? area, string? subarea, string? jefatura)
        {
            if (areaScopeId is > 0)
            {
                var eq = await _areaLegacyResolver.ResolveAsync(areaScopeId);
                return (areaScopeId, area ?? eq?.Area, subarea ?? eq?.Subarea, jefatura ?? eq?.Jefatura);
            }

            return (Abril_Backend.Shared.Services.AreaScopeMatcher.Resolve(area, subarea),
                    area, subarea, jefatura);
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

        public async Task<WorkerSearchResultDto?> GetByUserId(int userId, bool esContratista)
        {
            using var ctx = _factory.CreateDbContext();
            var hoy = DateOnly.FromDateTime(DateTime.Today);

            IQueryable<Worker> workers;
            if (esContratista)
            {
                // El login de un contratista no tiene Person propia: el vínculo a su ficha de
                // trabajador va por ss_contratista_usuario.worker_id (asignado al dar de alta el
                // acceso), no por Person.UserId.
                var workerId = await ctx.SsContratistaUsuarios
                    .Where(cu => cu.UserId == userId && cu.Activo && cu.WorkerId != null)
                    .Select(cu => cu.WorkerId)
                    .FirstOrDefaultAsync();
                workers = ctx.Worker.Where(w => w.Id == workerId).Take(1);
            }
            else
            {
                workers = ctx.Worker.Where(w => w.Person != null && w.Person.UserId == userId).Take(1);
            }

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
                    w.EmailCorporativo,
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
                    EmailCorporativo = b.EmailCorporativo,
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

        public async Task<List<WorkerCategoryDto>> GetWorkerCategories()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.WorkersCategory
                .Where(c => c.Active && c.State)
                .OrderBy(c => c.Name)
                .Select(c => new WorkerCategoryDto
                {
                    Id = c.WorkersCategoryId,
                    Nombre = c.Name,
                })
                .ToListAsync();
        }

        /// <summary>
        /// Una sola consulta con dos CTEs: <c>self</c> (el trabajador que se está editando, para
        /// saber si su correo es corporativo y si realmente cambió) y <c>ocupado</c> (el primer
        /// trabajador NO retirado que ya tiene ese correo). Un trabajador retirado libera su
        /// buzón, igual que el índice único parcial de la BD.
        /// </summary>
        public async Task<EmailCorporativoContextoDto> GetContextoEmailCorporativo(string? emailNormalizado, int? workerId)
        {
            using var ctx = _factory.CreateDbContext();
            await ctx.Database.OpenConnectionAsync();
            var conn = ctx.Database.GetDbConnection();

            const string sql = """
                WITH self AS (
                    SELECT w.contrata_casa, w.obra_oficina_staff_id, w.email_corporativo, p.email AS email_personal
                    FROM workers w
                    LEFT JOIN person p ON p.person_id = w.person_id
                    WHERE @workerId::int IS NOT NULL AND w.id = @workerId::int
                ),
                ocupado AS (
                    SELECT w.id, p.full_name, p.document_identity_code
                    FROM workers w
                    LEFT JOIN person p ON p.person_id = w.person_id
                    WHERE @email::text IS NOT NULL
                      AND lower(btrim(w.email_corporativo)) = @email::text
                      AND coalesce(w.estado, 'ACTIVO') <> 'RETIRADO'
                      AND (@workerId::int IS NULL OR w.id <> @workerId::int)
                    ORDER BY w.id
                    LIMIT 1
                )
                SELECT
                    EXISTS (SELECT 1 FROM self)                     AS worker_encontrado,
                    (SELECT contrata_casa           FROM self)      AS worker_contrata_casa,
                    (SELECT obra_oficina_staff_id   FROM self)      AS worker_obra_oficina_staff_id,
                    (SELECT email_corporativo       FROM self)      AS worker_email_actual,
                    (SELECT email_personal          FROM self)      AS worker_email_personal_actual,
                    (SELECT id                      FROM ocupado)   AS ocupado_por_worker_id,
                    (SELECT full_name               FROM ocupado)   AS ocupado_por_nombre,
                    (SELECT document_identity_code  FROM ocupado)   AS ocupado_por_dni
                """;

            // Tipos explícitos: los parámetros pueden llegar en null y Npgsql no puede inferir
            // el tipo de un null suelto.
            var parametros = new DynamicParameters();
            parametros.Add("email", emailNormalizado, DbType.String);
            parametros.Add("workerId", workerId, DbType.Int32);

            var contexto = await conn.QueryFirstOrDefaultAsync<EmailCorporativoContextoDto>(sql, parametros);

            return contexto ?? new EmailCorporativoContextoDto();
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
                    Email = dto.EmailPersonal,
                    Active = true,
                    State = true,
                    CreatedDateTime = DateTime.UtcNow
                };
                ctx.Person.Add(person);
                await ctx.SaveChangesAsync();
            }
            if (!string.IsNullOrWhiteSpace(dto.Sexo)) person.SexoId = await ResolveSexoIdAsync(ctx, dto.Sexo) ?? person.SexoId;
            if (dto.FechaNacimiento.HasValue) person.FechaNacimiento = dto.FechaNacimiento;
            // Al reusar una Person existente (reingreso) se actualiza el correo de contacto solo si
            // vino uno: un campo vacío no borra el dato que ya estaba registrado.
            if (dto.EmailPersonal is not null) person.Email = dto.EmailPersonal;

            var areaResuelta = await ResolverAreaAsync(
                dto.AreaScopeId, dto.Area, dto.Subarea, dto.Jefatura);

            var worker = new Worker
            {
                Person = person,
                EmailCorporativo = dto.EmailCorporativo,
                FechaIngreso = dto.FechaIngreso,
                Categoria = dto.Categoria,
                Ocupacion = dto.Ocupacion,
                OcupacionId = dto.OcupacionId,
                Puesto = dto.Puesto,
                AreaScopeId = areaResuelta.AreaScopeId,
                Area = areaResuelta.Area,
                Subarea = areaResuelta.Subarea,
                ContrataCasa = dto.ContrataCasa,
                ObraOficinaStaffId = dto.ObraOficinaStaffId,
                Jefatura = areaResuelta.Jefatura,
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
            await GuardarCuidandoEmailUnicoAsync(ctx);

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

            // Jefe personalizado (checkbox del formulario). Solo se toca cuando el formulario
            // gestiona el campo: en obreros y contratistas ni siquiera se muestra.
            if (dto.GestionaJefe)
                await _jefePersonalizado.SetAsync(worker.Id, dto.JefePersonalizadoWorkerId);

            return worker.Id;
        }

        /// <summary>
        /// Resuelve el texto de sexo ('M'/'F' o 'Masculino'/'Femenino') que envía el frontend
        /// al id del catálogo normalizado <c>sexo</c>. Devuelve null si no hay match.
        /// </summary>
        private static async Task<int?> ResolveSexoIdAsync(AppDbContext ctx, string? sexo)
        {
            if (string.IsNullOrWhiteSpace(sexo)) return null;
            var s = sexo.Trim().ToUpperInvariant();
            var codigo = s.StartsWith("M") ? "M" : s.StartsWith("F") ? "F" : null;
            if (codigo is null) return null;
            return await ctx.Sexo.Where(x => x.State && x.Codigo == codigo)
                                 .Select(x => (int?)x.SexoId).FirstOrDefaultAsync();
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
                worker.Person.Email         = dto.EmailPersonal;
                if (!string.IsNullOrWhiteSpace(dto.Sexo)) worker.Person.SexoId = await ResolveSexoIdAsync(ctx, dto.Sexo) ?? worker.Person.SexoId;
                if (dto.FechaNacimiento.HasValue) worker.Person.FechaNacimiento = dto.FechaNacimiento;
            }
            worker.EmailCorporativo = dto.EmailCorporativo;
            worker.FechaIngreso = dto.FechaIngreso;
            worker.Categoria = dto.Categoria;
            worker.Ocupacion = dto.Ocupacion;
            worker.OcupacionId = dto.OcupacionId;
            worker.Puesto = dto.Puesto;
            var areaResuelta = await ResolverAreaAsync(
                dto.AreaScopeId, dto.Area, dto.Subarea, dto.Jefatura);
            worker.AreaScopeId = areaResuelta.AreaScopeId;
            worker.Area = areaResuelta.Area;
            worker.Subarea = areaResuelta.Subarea;
            worker.ContrataCasa = dto.ContrataCasa;
            worker.ObraOficinaStaffId = dto.ObraOficinaStaffId;
            worker.Jefatura = areaResuelta.Jefatura;
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

            await GuardarCuidandoEmailUnicoAsync(ctx);

            // Jefe personalizado (checkbox del formulario). Solo se toca cuando el formulario
            // gestiona el campo: en obreros y contratistas ni siquiera se muestra.
            if (dto.GestionaJefe)
                await _jefePersonalizado.SetAsync(id, dto.JefePersonalizadoWorkerId);
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

            worker.Person.FechaNacimiento = dto.Cumpleanos;
            worker.Person.UpdatedDateTime = DateTime.UtcNow;

            worker.Categoria = dto.Categoria;
            worker.Ocupacion = dto.Ocupacion;
            worker.OcupacionId = dto.OcupacionId;
            worker.Puesto = dto.Puesto;

            if (dto.AreaScopeId.HasValue && dto.AreaScopeId != worker.AreaScopeId)
            {
                var existeNodo = await ctx.AreaScope
                    .AnyAsync(s => s.AreaScopeId == dto.AreaScopeId.Value && s.State);
                if (!existeNodo)
                    throw new AbrilException("El área seleccionada no existe en la jerarquía de áreas.", 400);
            }
            worker.AreaScopeId = dto.AreaScopeId;

            if (dto.WorkerCategoryId.HasValue && dto.WorkerCategoryId != worker.WorkerCategoryId)
            {
                var existeCategoria = await ctx.WorkersCategory
                    .AnyAsync(c => c.WorkersCategoryId == dto.WorkerCategoryId.Value && c.State);
                if (!existeCategoria)
                    throw new AbrilException("La categoría seleccionada no existe.", 400);
            }
            worker.WorkerCategoryId = dto.WorkerCategoryId;

            // Ambos correos llegan ya normalizados y validados (formato, existencia en el tenant,
            // unicidad del corporativo y "al menos uno") por WorkerEmailValidator; aquí solo se persisten.
            worker.EmailCorporativo = dto.EmailCorporativo;
            worker.Person.Email = dto.EmailPersonal;

            worker.UpdatedAt = DateTimeOffset.UtcNow;

            await GuardarCuidandoEmailUnicoAsync(ctx);
        }

        /// <summary>
        /// Guarda traduciendo la violación del índice <c>ux_workers_email_corporativo_vigente</c>
        /// a un 409 legible. El servicio ya valida el correo antes de llegar aquí, así que este
        /// camino solo se da en una carrera entre dos altas simultáneas con el mismo buzón.
        /// </summary>
        private static async Task GuardarCuidandoEmailUnicoAsync(AppDbContext ctx)
        {
            try
            {
                await ctx.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is PostgresException pg
                && pg.SqlState == PostgresErrorCodes.UniqueViolation
                && pg.ConstraintName == "ux_workers_email_corporativo_vigente")
            {
                throw new AbrilException(
                    "Ese correo corporativo acaba de ser asignado a otro trabajador. Usa un correo distinto.", 409);
            }
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
