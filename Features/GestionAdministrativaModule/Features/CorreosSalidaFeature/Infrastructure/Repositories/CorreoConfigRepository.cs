using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.CorreosSalida.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.CorreosSalida.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Models;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.CorreosSalida.Infrastructure.Repositories
{
    /// <summary>
    /// Configuración de los correos del flujo de salidas. La pantalla guarda al momento de tocar
    /// cada control, así que cada operación toca una sola fila: no hay reemplazo completo de la
    /// lista (pisaría lo que otro editor acabara de cambiar en otra fila).
    /// </summary>
    public class CorreoConfigRepository : ICorreoConfigRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private const string EmailDomainCorp = "@abril.pe";

        public CorreoConfigRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

        // ── Lectura ──────────────────────────────────────────────────────────

        public async Task<CorreoConfigInicialDto> GetInicialAsync()
        {
            using var ctx = _factory.CreateDbContext();

            var eventos = await ctx.GaCorreoEvento
                .Where(e => e.State)
                .OrderBy(e => e.Orden)
                .Select(e => new CorreoEventoDto
                {
                    Id = e.Id,
                    Codigo = e.Codigo,
                    Nombre = e.Nombre,
                    Descripcion = e.Descripcion,
                    Orden = e.Orden,
                    Active = e.Active,
                    DestinatarioPrincipalNombre = e.DestinatarioPrincipalNombre,
                    DestinatarioPrincipalActivo = e.DestinatarioPrincipalActivo,
                    PermiteDesactivarEnvio = e.PermiteDesactivarEnvio,
                    PermiteDesactivarPrincipal = e.PermiteDesactivarPrincipal,
                })
                .ToListAsync();

            var reglas = await (
                from r in ctx.GaCorreoRegla
                join t in ctx.GaCorreoTipoDestinatario on r.TipoId equals t.Id
                where r.State
                orderby r.Orden, r.Id
                select new
                {
                    r.Id,
                    r.EventoId,
                    TipoCodigo = t.Codigo,
                    r.WorkerId,
                    r.AreaScopeId,
                    r.Correo,
                    r.IncluirDescendientes,
                    r.Active,
                }
            ).ToListAsync();

            // Opciones de los desplegables del modal. Sirven además para resolver el nombre y el
            // correo de cada fila ya configurada, así que se cargan igual aunque no haya reglas.
            var trabajadores = await (
                from w in ctx.Worker
                where w.EmailCorporativo != null && w.EmailCorporativo.ToLower().Contains(EmailDomainCorp)
                join p in ctx.Person on w.PersonId equals p.PersonId
                where p.State == true
                orderby p.FullName
                select new CorreoWorkerOptionDto
                {
                    WorkerId = w.Id,
                    FullName = p.FullName,
                    Email = w.EmailCorporativo,
                }
            ).ToListAsync();

            var areas = await (
                from s in ctx.AreaScope
                join ai in ctx.AreaItem on s.AreaItemId equals ai.AreaItemId
                where s.State && ai.State
                orderby s.DisplayOrder
                select new CorreoAreaOptionDto
                {
                    AreaScopeId = s.AreaScopeId,
                    Nombre = ai.AreaItemName,
                    ParentId = s.AreaScopeParentId,
                    Email = s.Email,
                }
            ).ToListAsync();

            // Un trabajador referenciado por una regla puede no estar en `trabajadores` (sin correo
            // corporativo, o su persona dada de baja): se resuelve aparte para que la fila no salga
            // en blanco y se vea que hoy no le llega a nadie.
            var workerIdsEnReglas = reglas
                .Where(r => r.WorkerId.HasValue)
                .Select(r => r.WorkerId!.Value)
                .Distinct()
                .ToList();
            var workerInfo = workerIdsEnReglas.Count == 0
                ? new Dictionary<int, (string Nombre, string? Email)>()
                : (await (
                        from w in ctx.Worker
                        where workerIdsEnReglas.Contains(w.Id)
                        join p in ctx.Person on w.PersonId equals p.PersonId into pg
                        from p in pg.DefaultIfEmpty()
                        select new { w.Id, Nombre = p != null ? p.FullName : null, Email = w.EmailCorporativo }
                    ).ToListAsync())
                    .ToDictionary(x => x.Id, x => (x.Nombre ?? "[Trabajador sin nombre]", x.Email));

            // Miembros por área: solo se calcula si alguna regla apunta a un área. La expansión a
            // sub-áreas es la misma que hace CorreoSalidaRecipientResolver al enviar.
            var areaRules = reglas.Where(r => r.AreaScopeId.HasValue).ToList();
            var miembrosPorNodo = new Dictionary<int, int>();
            var hijosPorPadre = new Dictionary<int, List<int>>();
            if (areaRules.Count > 0)
            {
                var arbol = await GaAreaTreeLoader.LoadAsync(ctx);
                hijosPorPadre = arbol
                    .Where(n => n.AreaScopeParentId.HasValue)
                    .GroupBy(n => n.AreaScopeParentId!.Value)
                    .ToDictionary(g => g.Key, g => g.Select(n => n.AreaScopeId).ToList());

                var nodos = new HashSet<int>();
                foreach (var r in areaRules)
                    foreach (var id in Expandir(r.AreaScopeId!.Value, r.IncluirDescendientes, hijosPorPadre))
                        nodos.Add(id);

                miembrosPorNodo = (await ctx.Worker
                        .Where(w => w.PuestoCatalogo!.AreaDestinoScopeId != null
                                    && nodos.Contains(w.PuestoCatalogo.AreaDestinoScopeId!.Value)
                                    && w.EmailCorporativo != null && w.EmailCorporativo != "")
                        .GroupBy(w => w.PuestoCatalogo!.AreaDestinoScopeId!.Value)
                        .Select(g => new { AreaScopeId = g.Key, Total = g.Count() })
                        .ToListAsync())
                    .ToDictionary(x => x.AreaScopeId, x => x.Total);
            }

            var nombreArea = areas.ToDictionary(a => a.AreaScopeId, a => a.Nombre);

            foreach (var ev in eventos)
            {
                ev.Destinatarios = reglas
                    .Where(r => r.EventoId == ev.Id)
                    .Select(r =>
                    {
                        var fila = new CorreoDestinatarioDto
                        {
                            Id = r.Id,
                            TipoCodigo = r.TipoCodigo,
                            WorkerId = r.WorkerId,
                            AreaScopeId = r.AreaScopeId,
                            IncluirDescendientes = r.IncluirDescendientes,
                            Active = r.Active,
                        };

                        switch (r.TipoCodigo)
                        {
                            case CorreoTipoCodigos.Trabajador:
                                var info = r.WorkerId.HasValue && workerInfo.TryGetValue(r.WorkerId.Value, out var wi)
                                    ? wi
                                    : ("[Trabajador no encontrado]", (string?)null);
                                fila.Nombre = info.Item1;
                                fila.Email = info.Item2;
                                fila.SinCorreo = string.IsNullOrWhiteSpace(fila.Email);
                                break;

                            case CorreoTipoCodigos.Area:
                                fila.Nombre = r.AreaScopeId.HasValue && nombreArea.TryGetValue(r.AreaScopeId.Value, out var an)
                                    ? an
                                    : "[Área no encontrada]";
                                fila.Miembros = r.AreaScopeId.HasValue
                                    ? Expandir(r.AreaScopeId.Value, r.IncluirDescendientes, hijosPorPadre)
                                        .Sum(id => miembrosPorNodo.TryGetValue(id, out var n) ? n : 0)
                                    : 0;
                                fila.SinCorreo = (fila.Miembros ?? 0) == 0;
                                break;

                            default: // CORREO
                                fila.Nombre = r.Correo ?? string.Empty;
                                fila.Email = r.Correo;
                                fila.SinCorreo = string.IsNullOrWhiteSpace(r.Correo);
                                break;
                        }

                        return fila;
                    })
                    .ToList();
            }

            return new CorreoConfigInicialDto
            {
                Eventos = eventos,
                Trabajadores = trabajadores,
                Areas = areas,
            };
        }

        // ── Interruptores del correo ─────────────────────────────────────────

        public async Task SetEventoActiveAsync(string eventoCodigo, bool active)
        {
            using var ctx = _factory.CreateDbContext();
            var evento = await BuscarEventoAsync(ctx, eventoCodigo);

            if (evento.Active == active) return;
            if (!evento.PermiteDesactivarEnvio)
                throw new AbrilException("Este correo no se puede desactivar.", 400);

            evento.Active = active;
            evento.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task SetPrincipalActiveAsync(string eventoCodigo, bool active)
        {
            using var ctx = _factory.CreateDbContext();
            var evento = await BuscarEventoAsync(ctx, eventoCodigo);

            if (evento.DestinatarioPrincipalActivo == active) return;
            if (!evento.PermiteDesactivarPrincipal)
                throw new AbrilException("El destinatario principal de este correo no se puede desactivar.", 400);

            evento.DestinatarioPrincipalActivo = active;
            evento.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        // ── Destinatarios configurados ───────────────────────────────────────

        public async Task<int> CrearDestinatarioAsync(string eventoCodigo, CorreoDestinatarioInputDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var evento = await BuscarEventoAsync(ctx, eventoCodigo);

            var (tipoId, tipoCodigo) = await ResolverTipoAsync(ctx, dto.TipoCodigo);
            var (workerId, areaScopeId, correo) = await NormalizarAsync(ctx, tipoCodigo, dto);

            await ValidarNoDuplicadoAsync(ctx, evento.Id, null, tipoCodigo, workerId, areaScopeId, correo);

            var now = DateTimeOffset.UtcNow;
            var ultimoOrden = await ctx.GaCorreoRegla
                .Where(r => r.EventoId == evento.Id && r.State)
                .Select(r => (int?)r.Orden)
                .MaxAsync() ?? 0;

            var regla = new GaCorreoRegla
            {
                EventoId = evento.Id,
                TipoId = tipoId,
                WorkerId = workerId,
                AreaScopeId = areaScopeId,
                Correo = correo,
                IncluirDescendientes = tipoCodigo == CorreoTipoCodigos.Area && dto.IncluirDescendientes,
                Orden = ultimoOrden + 1,
                Active = true,
                State = true,
                CreatedAt = now,
                UpdatedAt = now,
            };

            ctx.GaCorreoRegla.Add(regla);
            await ctx.SaveChangesAsync();
            return regla.Id;
        }

        public async Task ActualizarDestinatarioAsync(int id, CorreoDestinatarioInputDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var regla = await BuscarReglaAsync(ctx, id);

            var (tipoId, tipoCodigo) = await ResolverTipoAsync(ctx, dto.TipoCodigo);
            var (workerId, areaScopeId, correo) = await NormalizarAsync(ctx, tipoCodigo, dto);

            await ValidarNoDuplicadoAsync(ctx, regla.EventoId, id, tipoCodigo, workerId, areaScopeId, correo);

            regla.TipoId = tipoId;
            regla.WorkerId = workerId;
            regla.AreaScopeId = areaScopeId;
            regla.Correo = correo;
            regla.IncluirDescendientes = tipoCodigo == CorreoTipoCodigos.Area && dto.IncluirDescendientes;
            regla.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task SetDestinatarioActiveAsync(int id, bool active)
        {
            using var ctx = _factory.CreateDbContext();
            var regla = await BuscarReglaAsync(ctx, id);

            if (regla.Active == active) return;
            regla.Active = active;
            regla.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task EliminarDestinatarioAsync(int id)
        {
            using var ctx = _factory.CreateDbContext();
            var regla = await BuscarReglaAsync(ctx, id);

            // Soft delete: la fila se conserva para saber a quién se le mandó este correo antes.
            regla.State = false;
            regla.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static async Task<GaCorreoEvento> BuscarEventoAsync(AppDbContext ctx, string eventoCodigo) =>
            await ctx.GaCorreoEvento.FirstOrDefaultAsync(e => e.Codigo == eventoCodigo && e.State)
            ?? throw new AbrilException("El correo indicado no existe.", 404);

        private static async Task<GaCorreoRegla> BuscarReglaAsync(AppDbContext ctx, int id) =>
            await ctx.GaCorreoRegla.FirstOrDefaultAsync(r => r.Id == id && r.State)
            ?? throw new AbrilException("El destinatario indicado no existe.", 404);

        private static async Task<(int TipoId, string TipoCodigo)> ResolverTipoAsync(AppDbContext ctx, string? codigo)
        {
            var buscado = (codigo ?? string.Empty).Trim().ToUpperInvariant();
            var tipo = await ctx.GaCorreoTipoDestinatario
                .Where(t => t.State && t.Codigo.ToUpper() == buscado)
                .Select(t => new { t.Id, t.Codigo })
                .FirstOrDefaultAsync()
                ?? throw new AbrilException($"Tipo de destinatario inválido: '{codigo}'.", 400);

            return (tipo.Id, tipo.Codigo.ToUpperInvariant());
        }

        /// <summary>
        /// Deja llena solo la columna que corresponde al tipo y valida que lo referenciado exista.
        /// Las otras dos quedan en null: una fila con worker_id Y correo a la vez sería ambigua al
        /// enviar.
        /// </summary>
        private static async Task<(int? WorkerId, int? AreaScopeId, string? Correo)> NormalizarAsync(
            AppDbContext ctx, string tipoCodigo, CorreoDestinatarioInputDto dto)
        {
            switch (tipoCodigo)
            {
                case CorreoTipoCodigos.Trabajador:
                    if (dto.WorkerId is null or <= 0)
                        throw new AbrilException("Falta seleccionar el trabajador.", 400);
                    if (!await ctx.Worker.AnyAsync(w => w.Id == dto.WorkerId))
                        throw new AbrilException("El trabajador seleccionado no existe.", 400);
                    return (dto.WorkerId, null, null);

                case CorreoTipoCodigos.Area:
                    if (dto.AreaScopeId is null or <= 0)
                        throw new AbrilException("Falta seleccionar el área.", 400);
                    if (!await ctx.AreaScope.AnyAsync(a => a.AreaScopeId == dto.AreaScopeId && a.State))
                        throw new AbrilException("El área seleccionada no existe.", 400);
                    return (null, dto.AreaScopeId, null);

                default: // CORREO
                    var correo = (dto.Correo ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(correo))
                        throw new AbrilException("Falta escribir el correo.", 400);
                    if (!correo.Contains('@') || correo.Contains(' '))
                        throw new AbrilException($"Correo inválido: '{dto.Correo}'.", 400);
                    return (null, null, correo);
            }
        }

        /// <summary>
        /// Impide que el mismo destinatario entre dos veces al mismo correo. Antes no hacía falta
        /// (la pantalla reemplazaba la lista completa); ahora que se agregan de a uno, un duplicado
        /// quedaría como dos filas idénticas con interruptores independientes.
        /// </summary>
        private static async Task ValidarNoDuplicadoAsync(
            AppDbContext ctx, int eventoId, int? excluirReglaId,
            string tipoCodigo, int? workerId, int? areaScopeId, string? correo)
        {
            var query = ctx.GaCorreoRegla.Where(r => r.EventoId == eventoId && r.State);
            if (excluirReglaId.HasValue) query = query.Where(r => r.Id != excluirReglaId.Value);

            var repetido = tipoCodigo switch
            {
                CorreoTipoCodigos.Trabajador => await query.AnyAsync(r => r.WorkerId == workerId),
                CorreoTipoCodigos.Area => await query.AnyAsync(r => r.AreaScopeId == areaScopeId),
                _ => await query.AnyAsync(r => r.Correo != null && r.Correo.ToLower() == correo!.ToLower()),
            };

            if (repetido)
                throw new AbrilException("Ese destinatario ya está en la lista de este correo.", 409);
        }

        /// <summary>Devuelve el nodo y, si <paramref name="incluirDescendientes"/>, todos sus descendientes.</summary>
        private static IEnumerable<int> Expandir(
            int areaScopeId, bool incluirDescendientes, Dictionary<int, List<int>> hijosPorPadre)
        {
            var resultado = new HashSet<int> { areaScopeId };
            if (!incluirDescendientes) return resultado;

            var cola = new Queue<int>();
            cola.Enqueue(areaScopeId);
            while (cola.Count > 0)
            {
                var actual = cola.Dequeue();
                if (hijosPorPadre.TryGetValue(actual, out var hijos))
                    foreach (var h in hijos)
                        if (resultado.Add(h)) cola.Enqueue(h);
            }
            return resultado;
        }
    }
}
