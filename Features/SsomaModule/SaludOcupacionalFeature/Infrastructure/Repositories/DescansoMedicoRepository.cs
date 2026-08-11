using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.DescansoMedico;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models;
using Abril_Backend.Features.SsomaModule.IndicadoresProactivosFeature.Infrastructure;
using Abril_Backend.Features.SsomaModule.Shared;
using Abril_Backend.Features.SsomaModule.Shared.DescansoCertificados;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Repositories
{
    public class DescansoMedicoRepository : IDescansoMedicoRepository
    {
        private const int PageSize = 20;
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly ReactivosCacheVersion _reactivosCacheVersion;

        public DescansoMedicoRepository(IDbContextFactory<AppDbContext> factory, ReactivosCacheVersion reactivosCacheVersion)
        {
            _factory = factory;
            _reactivosCacheVersion = reactivosCacheVersion;
        }

        /// <summary>
        /// Catálogo ss_descanso_tipo. <paramref name="soloMiSalud"/> = true devuelve únicamente los
        /// tipos que el trabajador puede elegir desde Mi Salud (los "común").
        /// </summary>
        public async Task<List<DescansoTipoDto>> GetTipos(bool soloMiSalud = false)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsDescansoTipo
                .Where(t => t.State && t.Active && (!soloMiSalud || t.DisponibleMiSalud))
                .OrderBy(t => t.Orden).ThenBy(t => t.Nombre)
                .Select(t => new DescansoTipoDto
                {
                    Id          = t.Id,
                    Nombre      = t.Nombre,
                    NombreCorto = t.NombreCorto ?? t.Nombre,
                })
                .ToListAsync();
        }

        /// <summary>
        /// Resuelve el id de un tipo por su nombre de catálogo. Para los descansos que crea el
        /// sistema (p. ej. los que genera el Tópico Médico), que no pasan por un desplegable.
        /// </summary>
        public async Task<int> GetTipoIdPorNombre(string nombre)
        {
            using var ctx = _factory.CreateDbContext();
            var id = await ctx.SsDescansoTipo
                .Where(t => t.State && t.Nombre == nombre)
                .Select(t => (int?)t.Id)
                .FirstOrDefaultAsync();
            return id ?? throw new AbrilException($"No se encontró el tipo de descanso '{nombre}' en el catálogo.", 500);
        }

        public async Task<PagedResult<DescansoMedicoListItemDto>> ListPaged(DescansoMedicoFilterDto filter)
        {
            using var ctx = _factory.CreateDbContext();

            var q =
                from d in ctx.SsDescansoMedico
                where d.State
                join w in ctx.Worker on d.WorkerId equals w.Id
                join t in ctx.SsDescansoTipo on d.TipoId equals t.Id
                join em in ctx.Contributor on d.EmpresaId equals em.ContributorId into emj
                from em in emj.DefaultIfEmpty()
                select new { d, w, t, em };

            if (filter.WorkerId.HasValue)
                q = q.Where(x => x.d.WorkerId == filter.WorkerId.Value);
            if (!string.IsNullOrWhiteSpace(filter.Estado))
                q = q.Where(x => x.d.Estado == filter.Estado);
            if (filter.TipoId.HasValue)
                q = q.Where(x => x.d.TipoId == filter.TipoId.Value);
            if (filter.EmpresaId.HasValue)
                q = q.Where(x => x.d.EmpresaId == filter.EmpresaId.Value);
            if (filter.FechaDesde.HasValue)
                q = q.Where(x => x.d.FechaInicio >= filter.FechaDesde.Value);
            if (filter.FechaHasta.HasValue)
                q = q.Where(x => x.d.FechaInicio <= filter.FechaHasta.Value);

            var total = await q.CountAsync();
            var page = filter.Page < 1 ? 1 : filter.Page;

            var items = await q
                .OrderByDescending(x => x.d.FechaInicio)
                .ThenByDescending(x => x.d.CreatedAt)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(x => new DescansoMedicoListItemDto
                {
                    Id = x.d.Id,
                    WorkerId = x.d.WorkerId,
                    WorkerNombre = x.w.Person != null ? x.w.Person.FullName : null,
                    WorkerDni = x.w.Person != null ? x.w.Person.DocumentIdentityCode : null,
                    EmpresaNombre = x.em != null ? x.em.ContributorName : null,
                    TipoId = x.d.TipoId,
                    Tipo = x.t.Nombre,
                    FechaInicio = x.d.FechaInicio,
                    FechaFin = x.d.FechaFin,
                    Dias = x.d.Dias,
                    Estado = x.d.Estado,
                    TopicoOrigenId = x.d.TopicoOrigenId,
                    TrabajadorBloqueado = ctx.SsTrabajadorRestringido.Any(r => r.WorkerId == x.d.WorkerId && r.Activo),
                    ReportadoPorTrabajador = x.d.ReportadoPorTrabajador,
                    CreatedAt = x.d.CreatedAt
                })
                .ToListAsync();

            return new PagedResult<DescansoMedicoListItemDto>
            {
                Page = page,
                PageSize = PageSize,
                TotalRecords = total,
                TotalPages = (int)Math.Ceiling(total / (double)PageSize),
                Data = items
            };
        }

        public async Task<DescansoMedicoDetalleDto> GetById(int id)
        {
            using var ctx = _factory.CreateDbContext();

            var row = await (
                from d in ctx.SsDescansoMedico
                where d.Id == id && d.State
                join w in ctx.Worker on d.WorkerId equals w.Id
                join t in ctx.SsDescansoTipo on d.TipoId equals t.Id
                join em in ctx.Contributor on d.EmpresaId equals em.ContributorId into emj
                from em in emj.DefaultIfEmpty()
                select new { d, w, t, em }
            ).FirstOrDefaultAsync()
              ?? throw new AbrilException("Descanso médico no encontrado.", 404);

            var adjuntos = await ctx.SsDescansoMedicoAdjunto
                .Where(a => a.State && a.DescansoId == id)
                .OrderBy(a => a.Id)
                .Select(a => new DescansoAdjuntoDto { Id = a.Id, Url = a.Url, Nombre = a.NombreArchivo })
                .ToListAsync();

            return new DescansoMedicoDetalleDto
            {
                Id = row.d.Id,
                WorkerId = row.d.WorkerId,
                WorkerNombre = row.w.Person != null ? row.w.Person.FullName : null,
                WorkerDni = row.w.Person != null ? row.w.Person.DocumentIdentityCode : null,
                ProyectoId = row.d.ProyectoId,
                EmpresaId = row.d.EmpresaId,
                EmpresaNombre = row.em != null ? row.em.ContributorName : null,
                TipoId = row.d.TipoId,
                Tipo = row.t.Nombre,
                FechaInicio = row.d.FechaInicio,
                FechaFin = row.d.FechaFin,
                Dias = row.d.Dias,
                Diagnostico = row.d.Diagnostico,
                DiagnosticoCie10 = row.d.DiagnosticoCie10,
                UrlCertificado = row.d.UrlCertificado,
                Adjuntos = adjuntos,
                UrlDocumento = row.d.UrlDocumento,
                Estado = row.d.Estado,
                MotivoRechazo = row.d.MotivoRechazo,
                AprobadoPorId = row.d.AprobadoPorId,
                FechaAprobacion = row.d.FechaAprobacion,
                AccidenteId = row.d.AccidenteId,
                EsRecaida = row.d.EsRecaida,
                NotificadoGth = row.d.NotificadoGth,
                NotificadoJefe = row.d.NotificadoJefe,
                ReportadoPorTrabajador = row.d.ReportadoPorTrabajador,
                Observaciones = row.d.Observaciones,
                TopicoOrigenId = row.d.TopicoOrigenId,
                ProrrogaDelId = row.d.ProrrogaDelId,
                FechaAlta = row.d.FechaAlta,
                AltaPorId = row.d.AltaPorId,
                AltaObservaciones = row.d.AltaObservaciones,
                RegistradoPorId = row.d.RegistradoPorId,
                CreatedAt = row.d.CreatedAt,
                UpdatedAt = row.d.UpdatedAt
            };
        }

        /// <param name="adjuntos">Certificados médicos ya subidos a la carpeta configurada en SharePoint.</param>
        public async Task<int> Create(DescansoMedicoCreateDto dto, int registradoPorId, List<DescansoCertificadoSubidoDto> adjuntos)
        {
            using var ctx = _factory.CreateDbContext();

            // El tipo tiene que existir en el catálogo: es el único clasificador del descanso.
            var tipoValido = await ctx.SsDescansoTipo.AnyAsync(t => t.Id == dto.TipoId && t.State && t.Active);
            if (!tipoValido)
                throw new AbrilException("El tipo de descanso seleccionado no es válido.", 400);

            var dias = dto.FechaFin.DayNumber - dto.FechaInicio.DayNumber + 1;

            var entity = new SsDescansoMedico
            {
                WorkerId = dto.WorkerId,
                TipoId = dto.TipoId,
                FechaInicio = dto.FechaInicio,
                FechaFin = dto.FechaFin,
                Dias = dias,
                Diagnostico = dto.Diagnostico,
                DiagnosticoCie10 = dto.DiagnosticoCie10,
                // El primer adjunto también va a url_certificado para no romper las vistas
                // antiguas que muestran un único certificado.
                UrlCertificado = adjuntos.Count > 0 ? adjuntos[0].Url : null,
                Estado = "Pendiente",
                // Registrado por SSOMA, no autorreportado: Revisión de Descansos solo revisa
                // los que reporta el propio trabajador desde Mi Salud.
                ReportadoPorTrabajador = false,
                AccidenteId = dto.AccidenteId,
                EsRecaida = dto.EsRecaida,
                TopicoOrigenId = dto.TopicoOrigenId,
                ProrrogaDelId = dto.ProrrogaDelId,
                ProyectoId = dto.ProyectoId,
                EmpresaId = dto.EmpresaId,
                RegistradoPorId = registradoPorId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                State = true
            };

            ctx.SsDescansoMedico.Add(entity);

            foreach (var adjunto in adjuntos)
            {
                ctx.SsDescansoMedicoAdjunto.Add(new SsDescansoMedicoAdjunto
                {
                    Descanso      = entity,
                    Url           = adjunto.Url,
                    NombreArchivo = adjunto.Nombre,
                    DriveId       = adjunto.DriveId,
                    ItemId        = adjunto.ItemId,
                    State         = true,
                    CreatedAt     = DateTimeOffset.UtcNow,
                    UpdatedAt     = DateTimeOffset.UtcNow,
                });
            }

            await ctx.SaveChangesAsync();
            return entity.Id;
        }

        public async Task<DescansoAdjuntoArchivoDto?> GetAdjunto(int adjuntoId)
        {
            using var ctx = _factory.CreateDbContext();

            return await (
                from a in ctx.SsDescansoMedicoAdjunto
                join d in ctx.SsDescansoMedico on a.DescansoId equals d.Id
                where a.Id == adjuntoId && a.State && d.State
                select new DescansoAdjuntoArchivoDto
                {
                    Url           = a.Url,
                    NombreArchivo = a.NombreArchivo,
                    DriveId       = a.DriveId,
                    ItemId        = a.ItemId,
                }
            ).FirstOrDefaultAsync();
        }

        public async Task Update(int id, DescansoMedicoUpdateDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var entity = await ctx.SsDescansoMedico.FirstOrDefaultAsync(d => d.Id == id && d.State)
                ?? throw new AbrilException("Descanso médico no encontrado.", 404);

            if (entity.Estado != "Pendiente")
                throw new AbrilException("Solo se puede editar un descanso en estado Pendiente.", 400);

            var tipoValido = await ctx.SsDescansoTipo.AnyAsync(t => t.Id == dto.TipoId && t.State && t.Active);
            if (!tipoValido)
                throw new AbrilException("El tipo de descanso seleccionado no es válido.", 400);

            entity.TipoId = dto.TipoId;
            entity.FechaInicio = dto.FechaInicio;
            entity.FechaFin = dto.FechaFin;
            entity.Dias = dto.FechaFin.DayNumber - dto.FechaInicio.DayNumber + 1;
            entity.Diagnostico = dto.Diagnostico;
            entity.DiagnosticoCie10 = dto.DiagnosticoCie10;
            entity.UpdatedAt = DateTimeOffset.UtcNow;

            await ctx.SaveChangesAsync();
        }

        public async Task Aprobar(int id, DescansoAprobarDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var entity = await ctx.SsDescansoMedico
                .Include(d => d.Worker)
                    .ThenInclude(w => w!.Person)
                .FirstOrDefaultAsync(d => d.Id == id && d.State)
                ?? throw new AbrilException("Descanso médico no encontrado.", 404);

            if (entity.Estado == "Aprobado")
                throw new AbrilException("El descanso ya está aprobado.", 400);

            entity.Estado = "Aprobado";
            entity.AprobadoPorId = userId;
            entity.FechaAprobacion = DateTimeOffset.UtcNow;
            if (dto.Observaciones != null)
                entity.Observaciones = dto.Observaciones;
            entity.UpdatedAt = DateTimeOffset.UtcNow;

            // Bloqueo automático en Control de Acceso para TODOS los descansos aprobados
            {
                var dni = entity.Worker?.Person?.DocumentIdentityCode;
                var nombre = entity.Worker?.Person?.FullName;

                var yaRestringido = await ctx.SsTrabajadorRestringido
                    .AnyAsync(r => r.WorkerId == entity.WorkerId && r.Activo);

                if (!yaRestringido)
                {
                    var anterior = await ctx.SsTrabajadorRestringido
                        .FirstOrDefaultAsync(r => r.WorkerId == entity.WorkerId && !r.Activo);

                    if (anterior is not null)
                    {
                        anterior.Activo = true;
                        anterior.Motivo = $"Descanso médico aprobado (ID {id})";
                        anterior.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        ctx.SsTrabajadorRestringido.Add(new SsTrabajadorRestringido
                        {
                            WorkerId = entity.WorkerId,
                            Dni = dni,
                            ApellidoNombre = nombre,
                            Motivo = $"Descanso médico aprobado (ID {id})",
                            FechaRestriccion = DateOnly.FromDateTime(DateTime.UtcNow),
                            Activo = true,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            await ctx.SaveChangesAsync();

            // Recalcular dias_descanso_reales en ss_accidente_trabajo
            if (entity.AccidenteId.HasValue)
            {
                using var ctx2 = _factory.CreateDbContext();
                var totalDias = await ctx2.SsDescansoMedico
                    .Where(d => d.AccidenteId == entity.AccidenteId && d.Estado == "Aprobado" && d.State)
                    .SumAsync(d => d.Dias);

                var accidente = await ctx2.SsAccidenteTrabajo.FindAsync(entity.AccidenteId.Value);
                if (accidente != null)
                {
                    accidente.DiasDescansoReales = totalDias;
                    accidente.UpdatedAt = DateTimeOffset.UtcNow;
                    await ctx2.SaveChangesAsync();
                    _reactivosCacheVersion.Bump();
                }
            }
        }

        public async Task Rechazar(int id, DescansoRechazarDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var entity = await ctx.SsDescansoMedico.FirstOrDefaultAsync(d => d.Id == id && d.State)
                ?? throw new AbrilException("Descanso médico no encontrado.", 404);

            if (entity.Estado == "Rechazado")
                throw new AbrilException("El descanso ya está rechazado.", 400);

            entity.Estado = "Rechazado";
            entity.MotivoRechazo = dto.MotivoRechazo;
            entity.UpdatedAt = DateTimeOffset.UtcNow;

            await ctx.SaveChangesAsync();
        }

        public async Task DarAlta(int id, DarAltaDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var entity = await ctx.SsDescansoMedico.FirstOrDefaultAsync(d => d.Id == id && d.State)
                ?? throw new AbrilException("Descanso médico no encontrado.", 404);

            if (entity.Estado != "Aprobado")
                throw new AbrilException("Solo se puede dar de alta un descanso en estado Aprobado.", 400);

            entity.Estado = "Completado";
            entity.FechaAlta = DateOnly.FromDateTime(DateTime.UtcNow);
            entity.AltaPorId = userId;
            if (!string.IsNullOrWhiteSpace(dto.Observaciones))
                entity.AltaObservaciones = dto.Observaciones;
            entity.UpdatedAt = DateTimeOffset.UtcNow;

            // Desbloquear al trabajador en Control de Acceso
            var restriccion = await ctx.SsTrabajadorRestringido
                .FirstOrDefaultAsync(r => r.WorkerId == entity.WorkerId && r.Activo);
            if (restriccion is not null)
            {
                restriccion.Activo = false;
                restriccion.UpdatedAt = DateTime.UtcNow;
            }

            await ctx.SaveChangesAsync();
        }

        public async Task<List<DescansoSeguimientoDto>> GetSeguimientos(int descansoId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsDescansoSeguimiento
                .Where(s => s.DescansoId == descansoId && s.State)
                .OrderByDescending(s => s.FechaSeguimiento)
                .Select(s => new DescansoSeguimientoDto
                {
                    Id = s.Id,
                    DescansoId = s.DescansoId,
                    FechaSeguimiento = s.FechaSeguimiento,
                    Tipo = s.Tipo,
                    RealizadoPorRol = s.RealizadoPorRol,
                    RealizadoPorId = s.RealizadoPorId,
                    Nota = s.Nota,
                    ProximaCita = s.ProximaCita,
                    UrlEvidencia = s.UrlEvidencia,
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<int> CreateSeguimiento(int descansoId, DescansoSeguimientoCreateDto dto, int registradoPorId, string? rolUsuario)
        {
            using var ctx = _factory.CreateDbContext();

            var existe = await ctx.SsDescansoMedico.AnyAsync(d => d.Id == descansoId && d.State);
            if (!existe) throw new AbrilException("Descanso médico no encontrado.", 404);

            var entity = new SsDescansoSeguimiento
            {
                DescansoId = descansoId,
                FechaSeguimiento = DateTimeOffset.UtcNow,
                Tipo = dto.Tipo,
                RealizadoPorRol = rolUsuario,
                RealizadoPorId = registradoPorId,
                Nota = dto.Nota,
                ProximaCita = dto.ProximaCita,
                UrlEvidencia = dto.UrlEvidencia,
                CreatedAt = DateTimeOffset.UtcNow
            };

            ctx.SsDescansoSeguimiento.Add(entity);
            await ctx.SaveChangesAsync();
            return entity.Id;
        }

        public async Task Delete(int id)
        {
            using var ctx = _factory.CreateDbContext();

            var entity = await ctx.SsDescansoMedico.FirstOrDefaultAsync(d => d.Id == id && d.State)
                ?? throw new AbrilException("Descanso médico no encontrado.", 404);

            entity.State = false;
            entity.UpdatedAt = DateTimeOffset.UtcNow;

            await ctx.SaveChangesAsync();
        }
    }
}
