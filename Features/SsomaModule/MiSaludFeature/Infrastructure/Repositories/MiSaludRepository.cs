using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.MiSaludFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.MiSaludFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.MiSaludFeature.Infrastructure.Repositories
{
    public class MiSaludRepository : IMiSaludRepository
    {
        private const int PageSize = 10;
        // Regla de negocio: los descansos registrados por el propio trabajador
        // se guardan siempre con este tipo (el valor vive en ss_descanso_tipo).
        private const string TipoPorDefecto = "Particular";
        /// <summary>Nombre exacto del área en area_item cuyo area_scope.email es el correo de GTH.</summary>
        private const string AreaGthNombre = "Gestión del Talento Humano";
        private readonly IDbContextFactory<AppDbContext> _factory;

        public MiSaludRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<int> ResolverWorkerIdAsync(int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var person = await ctx.Person.FirstOrDefaultAsync(p => p.UserId == userId)
                ?? throw new AbrilException("No tienes un perfil de persona asociado a tu usuario.", 403);

            var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.PersonId == person.PersonId)
                ?? throw new AbrilException("No tienes un perfil de trabajador registrado en el sistema.", 403);

            return worker.Id;
        }

        public async Task<MiSaludResumenDto> GetResumen(int workerId)
        {
            using var ctx = _factory.CreateDbContext();
            var hoy = DateOnly.FromDateTime(DateTime.Today);

            var worker = await ctx.Worker
                .Include(w => w.Person)
                .FirstOrDefaultAsync(w => w.Id == workerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            // EMO activo
            var emo = await (
                from e in ctx.WorkerEmo
                join t in ctx.SsEmoTipo on e.TipoEmoId equals t.Id into tj
                from t in tj.DefaultIfEmpty()
                where e.WorkerId == workerId && e.Activo
                orderby e.FechaEmo descending
                select new { e, tipoNombre = t != null ? t.Nombre : null }
            ).FirstOrDefaultAsync();

            // Restricciones vigentes del EMO activo
            var restricciones = new List<string>();
            if (emo != null)
            {
                restricciones = await (
                    from r in ctx.SsEmoRestriccion
                    join rt in ctx.SsRestriccionTipo on r.RestriccionTipoId equals rt.Id into rtj
                    from rt in rtj.DefaultIfEmpty()
                    where r.EmoId == emo.e.Id && r.Vigente
                    select rt != null ? rt.Descripcion : r.DescripcionLibre
                ).Where(d => d != null).Select(d => d!).ToListAsync();
            }

            // Último descanso
            var ultimoDescanso = await ctx.SsDescansoMedico
                .Where(d => d.WorkerId == workerId && d.State)
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new { d.Estado, d.FechaFin })
                .FirstOrDefaultAsync();

            // Catálogo de motivos para el formulario de registro
            var motivos = await ctx.SsDescansoMotivo
                .Where(m => m.State && m.Active)
                .OrderBy(m => m.Nombre)
                .Select(m => new DescansoMotivoDto { Id = m.Id, Nombre = m.Nombre })
                .ToListAsync();

            DateOnly? fechaVenc = emo?.e.FechaVencimientoCalculada ?? emo?.e.FechaVencimiento;

            return new MiSaludResumenDto
            {
                WorkerId        = workerId,
                WorkerNombre    = worker.Person?.FullName,
                TieneEmo        = emo != null,
                EmoId           = emo?.e.Id,
                TipoEmo         = emo?.tipoNombre,
                Aptitud         = emo?.e.Aptitud,
                FechaEmo        = emo?.e.FechaEmo,
                FechaVencimiento = fechaVenc,
                DiasParaVencer  = fechaVenc.HasValue ? fechaVenc.Value.DayNumber - hoy.DayNumber : null,
                RestriccionesVigentes = restricciones,
                UltimoDescansoEstado  = ultimoDescanso?.Estado,
                UltimoDescansoFechaFin = ultimoDescanso?.FechaFin,
                MotivosDescanso = motivos,
            };
        }

        public async Task<PagedResult<MiDescansoDto>> GetDescansos(int workerId, int page)
        {
            using var ctx = _factory.CreateDbContext();

            var q = ctx.SsDescansoMedico
                .Where(d => d.WorkerId == workerId && d.State)
                .OrderByDescending(d => d.FechaInicio);

            var total = await q.CountAsync();
            var pg    = page < 1 ? 1 : page;

            var items = await (
                from d in q
                join m in ctx.SsDescansoMotivo on d.MotivoId equals m.Id into mj
                from m in mj.DefaultIfEmpty()
                select new MiDescansoDto
                {
                    Id               = d.Id,
                    Tipo             = d.Tipo,
                    FechaInicio      = d.FechaInicio,
                    FechaFin         = d.FechaFin,
                    Dias             = d.Dias,
                    Motivo           = m != null ? m.Nombre : d.Motivo,
                    Diagnostico      = d.Diagnostico,
                    Estado           = d.Estado,
                    MotivoRechazo    = d.MotivoRechazo,
                    UrlCertificado   = d.UrlCertificado,
                    UrlDocumento     = d.UrlDocumento,
                    CreatedAt        = d.CreatedAt,
                })
                .Skip((pg - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // Adjuntos de los descansos de la página (una sola consulta, sin N+1)
            var ids = items.Select(i => i.Id).ToList();
            if (ids.Count > 0)
            {
                var adjuntos = await ctx.SsDescansoMedicoAdjunto
                    .Where(a => a.State && ids.Contains(a.DescansoId))
                    .OrderBy(a => a.Id)
                    .Select(a => new { a.DescansoId, a.Url, a.NombreArchivo })
                    .ToListAsync();

                var porDescanso = adjuntos
                    .GroupBy(a => a.DescansoId)
                    .ToDictionary(g => g.Key, g => g
                        .Select(a => new MiDescansoAdjuntoDto { Url = a.Url, Nombre = a.NombreArchivo })
                        .ToList());

                foreach (var item in items)
                    if (porDescanso.TryGetValue(item.Id, out var lista))
                        item.Adjuntos = lista;
            }

            return new PagedResult<MiDescansoDto>
            {
                Page        = pg,
                PageSize    = PageSize,
                TotalRecords = total,
                TotalPages  = (int)Math.Ceiling(total / (double)PageSize),
                Data        = items,
            };
        }

        public async Task<int> CreateDescanso(int workerId, CrearMiDescansoDto dto, int? userId, List<(string Url, string Nombre)> adjuntos)
        {
            using var ctx = _factory.CreateDbContext();

            var tipo = await ctx.SsDescansoTipo
                .FirstOrDefaultAsync(t => t.State && t.Nombre == TipoPorDefecto)
                ?? throw new AbrilException($"No se encontró el tipo de descanso '{TipoPorDefecto}' en el catálogo.", 500);

            SsDescansoMotivo? motivo = null;
            if (dto.MotivoId.HasValue)
            {
                motivo = await ctx.SsDescansoMotivo
                    .FirstOrDefaultAsync(m => m.Id == dto.MotivoId.Value && m.State)
                    ?? throw new AbrilException("El motivo seleccionado no es válido.", 400);
            }

            var entity = new SsDescansoMedico
            {
                WorkerId               = workerId,
                Tipo                   = tipo.Nombre,
                TipoId                 = tipo.Id,
                FechaInicio            = dto.FechaInicio,
                FechaFin               = dto.FechaFin,
                Dias                   = dto.Dias ?? (dto.FechaFin.DayNumber - dto.FechaInicio.DayNumber + 1),
                Motivo                 = motivo?.Nombre,
                MotivoId               = motivo?.Id,
                Diagnostico            = dto.Diagnostico,
                UrlCertificado         = adjuntos.Count > 0 ? adjuntos[0].Url : null,
                Estado                 = "Pendiente",
                ReportadoPorTrabajador = true,
                RegistradoPorId        = userId ?? workerId,
                CreatedAt              = DateTimeOffset.UtcNow,
                UpdatedAt              = DateTimeOffset.UtcNow,
                State                  = true,
            };

            ctx.SsDescansoMedico.Add(entity);

            foreach (var (url, nombre) in adjuntos)
            {
                ctx.SsDescansoMedicoAdjunto.Add(new SsDescansoMedicoAdjunto
                {
                    Descanso      = entity,
                    Url           = url,
                    NombreArchivo = nombre,
                    State         = true,
                    CreatedAt     = DateTimeOffset.UtcNow,
                    UpdatedAt     = DateTimeOffset.UtcNow,
                });
            }

            await ctx.SaveChangesAsync();
            return entity.Id;
        }

        public async Task<DescansoNotificacionDatosDto> GetDatosNotificacionDescansoAsync(int workerId, int userId, int? motivoId)
        {
            using var ctx = _factory.CreateDbContext();

            var worker = await ctx.Worker
                .Where(w => w.Id == workerId)
                .Select(w => new
                {
                    Nombre = w.Person != null ? w.Person.FullName : null,
                    w.EmailCorporativo,
                })
                .FirstOrDefaultAsync();

            var userEmail = await ctx.User
                .Where(u => u.UserId == userId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            // Correo del área GTH: configurable en area_scope.email (mismo criterio que
            // el fallback de solicitud-salidas) para no hardcodear el correo del área.
            var gthEmail = await (
                from s in ctx.AreaScope
                join ai in ctx.AreaItem on s.AreaItemId equals ai.AreaItemId
                where s.State && ai.State
                      && ai.AreaItemName == AreaGthNombre
                      && s.Email != null && s.Email != ""
                orderby s.AreaScopeId
                select s.Email
            ).FirstOrDefaultAsync();

            string? motivoNombre = null;
            if (motivoId.HasValue)
                motivoNombre = await ctx.SsDescansoMotivo
                    .Where(m => m.Id == motivoId.Value)
                    .Select(m => m.Nombre)
                    .FirstOrDefaultAsync();

            return new DescansoNotificacionDatosDto
            {
                WorkerNombre = worker?.Nombre,
                WorkerEmail  = !string.IsNullOrWhiteSpace(userEmail) ? userEmail.Trim() : worker?.EmailCorporativo?.Trim(),
                GthEmail     = gthEmail?.Trim(),
                MotivoNombre = motivoNombre,
            };
        }

        public async Task<List<MiDescansoCorreoConfigDto>> GetCorreoConfigsAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsDescansoCorreoConfig
                .Where(c => c.State)
                .OrderBy(c => c.Orden)
                .ThenBy(c => c.Id)
                .Select(c => new MiDescansoCorreoConfigDto
                {
                    Id          = c.Id,
                    Codigo      = c.Codigo,
                    Nombre      = c.Nombre,
                    Descripcion = c.Descripcion,
                    Active      = c.Active,
                    Orden       = c.Orden,
                })
                .ToListAsync();
        }

        public async Task<Dictionary<string, bool>> GetCorreoConfigMapAsync()
        {
            using var ctx = _factory.CreateDbContext();
            var rows = await ctx.SsDescansoCorreoConfig
                .Where(c => c.State)
                .Select(c => new { c.Codigo, c.Active })
                .ToListAsync();

            // Case-insensitive por codigo; si hubiera duplicados vivos, gana el último.
            var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in rows)
                map[r.Codigo] = r.Active;
            return map;
        }

        public async Task<bool> SetCorreoConfigActiveAsync(int id, bool active)
        {
            using var ctx = _factory.CreateDbContext();
            var row = await ctx.SsDescansoCorreoConfig.FirstOrDefaultAsync(c => c.Id == id && c.State);
            if (row == null) return false;

            row.Active    = active;
            row.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
            return true;
        }
    }
}
