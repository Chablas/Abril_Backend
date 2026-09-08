using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.Trayectos.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Trayectos.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Trayectos.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Trayectos.Infrastructure.Repositories
{
    public class GaTrayectoRepository : IGaTrayectoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public GaTrayectoRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<GaTrayectoListItemDto>> GetAll()
        {
            using var ctx = _factory.CreateDbContext();

            return await (
                from t  in ctx.GaTrayecto
                join lo in ctx.GaLugar  on t.LugarOrigenId  equals lo.Id
                join po in ctx.Project  on lo.ProjectId     equals (int?)po.ProjectId into poGroup
                from po in poGroup.DefaultIfEmpty()
                join ld in ctx.GaLugar  on t.LugarDestinoId equals ld.Id
                join pd in ctx.Project  on ld.ProjectId     equals (int?)pd.ProjectId into pdGroup
                from pd in pdGroup.DefaultIfEmpty()
                orderby t.CreatedAt descending
                select new GaTrayectoListItemDto
                {
                    Id                = t.Id,
                    LugarOrigenId     = t.LugarOrigenId,
                    LugarOrigenNombre = lo.Tipo == "proyecto"
                                        ? (po != null ? po.ProjectDescription : "[Sin proyecto]")
                                        : (lo.Nombre ?? string.Empty),
                    LugarDestinoId    = t.LugarDestinoId,
                    LugarDestinoNombre = ld.Tipo == "proyecto"
                                        ? (pd != null ? pd.ProjectDescription : "[Sin proyecto]")
                                        : (ld.Nombre ?? string.Empty),
                    Monto             = t.Monto,
                    EsReembolsable    = t.EsReembolsable,
                    Activo            = t.Activo,
                    CreatedAt         = t.CreatedAt,
                }
            ).ToListAsync();
        }

        public async Task<List<GaTrayectoLugarOptionDto>> GetLugaresActivos()
        {
            using var ctx = _factory.CreateDbContext();

            return await (
                from l in ctx.GaLugar
                join p in ctx.Project on l.ProjectId equals p.ProjectId into pGroup
                from p in pGroup.DefaultIfEmpty()
                where l.Activo && l.Tipo != "libre"
                orderby l.Orden
                select new GaTrayectoLugarOptionDto
                {
                    Id            = l.Id,
                    NombreDisplay = l.Tipo == "proyecto"
                                    ? (p != null ? p.ProjectDescription : "[Sin proyecto]")
                                    : (l.Nombre ?? string.Empty),
                }
            ).ToListAsync();
        }

        public async Task Create(GaTrayectoCreateDto dto)
        {
            ValidarDto(dto.LugarOrigenId, dto.LugarDestinoId, dto.Monto);

            using var ctx = _factory.CreateDbContext();

            var claves = await AsegurarLugaresActivos(ctx, dto.LugarOrigenId, dto.LugarDestinoId);

            await ValidarParDeLugaresAsync(ctx, claves, excluirTrayectoId: null);

            ctx.GaTrayecto.Add(new GaTrayecto
            {
                LugarOrigenId  = dto.LugarOrigenId,
                LugarDestinoId = dto.LugarDestinoId,
                Monto          = dto.Monto,
                EsReembolsable = dto.EsReembolsable,
                Activo         = true,
                CreatedAt      = DateTimeOffset.UtcNow,
            });

            await ctx.SaveChangesAsync();
        }

        public async Task<bool> Toggle(int id)
        {
            using var ctx = _factory.CreateDbContext();

            var trayecto = await ctx.GaTrayecto.FindAsync(id)
                ?? throw new AbrilException("Trayecto no encontrado.", 404);

            trayecto.Activo = !trayecto.Activo;
            await ctx.SaveChangesAsync();
            return trayecto.Activo;
        }

        public async Task Edit(int id, GaTrayectoEditDto dto)
        {
            ValidarDto(dto.LugarOrigenId, dto.LugarDestinoId, dto.Monto);

            using var ctx = _factory.CreateDbContext();

            var trayecto = await ctx.GaTrayecto.FindAsync(id)
                ?? throw new AbrilException("Trayecto no encontrado.", 404);

            var claves = await AsegurarLugaresActivos(ctx, dto.LugarOrigenId, dto.LugarDestinoId);

            // El par se revisa solo si la edición lo cambia. Si el usuario solo toca el monto o el
            // reembolso, un choque preexistente (los que quedaron de antes de esta validación) no
            // puede dejar la fila sin poder editarse.
            var cambiaElPar = trayecto.LugarOrigenId != dto.LugarOrigenId
                           || trayecto.LugarDestinoId != dto.LugarDestinoId;
            if (cambiaElPar)
                await ValidarParDeLugaresAsync(ctx, claves, excluirTrayectoId: id);

            trayecto.LugarOrigenId  = dto.LugarOrigenId;
            trayecto.LugarDestinoId = dto.LugarDestinoId;
            trayecto.Monto          = dto.Monto;
            trayecto.EsReembolsable = dto.EsReembolsable;
            await ctx.SaveChangesAsync();
        }

        /// <summary>
        /// Valida el par (origen, destino) contra el catálogo: no puede ir de un lugar a sí mismo ni
        /// repetir un par ya registrado. La comparación es por LUGAR (el nombre que se ve en la
        /// tabla), no por id de <c>ga_lugar</c>: un mismo lugar puede tener más de una fila en el
        /// catálogo —"OFICINA CENTRAL" existe como lugar fijo y como proyecto— y comparar ids dejaba
        /// pasar dos filas que en pantalla se leen idénticas. El sentido inverso (destino → origen)
        /// es un trayecto distinto y sí se permite.
        ///
        /// No se filtra por <c>activo</c>: un trayecto desactivado sigue ocupando su par, igual que
        /// antes de este cambio (lo que corresponde es reactivarlo y editarlo, no duplicarlo). La
        /// misma regla está en BD como trigger, por si alguien inserta por fuera de la app.
        /// </summary>
        /// <param name="claves">Claves del par nuevo, tal como las devuelve <see cref="AsegurarLugaresActivos"/>.</param>
        /// <param name="excluirTrayectoId">Fila que se está editando (no compite consigo misma).</param>
        private static async Task ValidarParDeLugaresAsync(
            AppDbContext ctx, (string Origen, string Destino) claves, int? excluirTrayectoId)
        {
            if (claves.Origen == claves.Destino)
                throw new AbrilException("El lugar de origen y el destino son el mismo lugar.", 400);

            // Los nombres se resuelven en la misma consulta que trae los trayectos: un solo viaje.
            var existentes = await (
                from t  in ctx.GaTrayecto
                join lo in ctx.GaLugar on t.LugarOrigenId equals lo.Id
                join po in ctx.Project on lo.ProjectId    equals (int?)po.ProjectId into poGroup
                from po in poGroup.DefaultIfEmpty()
                join ld in ctx.GaLugar on t.LugarDestinoId equals ld.Id
                join pd in ctx.Project on ld.ProjectId     equals (int?)pd.ProjectId into pdGroup
                from pd in pdGroup.DefaultIfEmpty()
                where t.Id != excluirTrayectoId
                select new
                {
                    t.Id,
                    OrigenId  = lo.Id,
                    DestinoId = ld.Id,
                    Origen    = lo.Tipo == "proyecto" ? (po != null ? po.ProjectDescription : null) : lo.Nombre,
                    Destino   = ld.Tipo == "proyecto" ? (pd != null ? pd.ProjectDescription : null) : ld.Nombre,
                }
            ).ToListAsync();

            var duplicado = existentes.Any(t =>
                Clave(t.OrigenId,  t.Origen)  == claves.Origen &&
                Clave(t.DestinoId, t.Destino) == claves.Destino);

            if (duplicado)
                throw new AbrilException(
                    "Ya existe un trayecto de ese origen a ese destino. El sentido inverso sí se puede registrar aparte.",
                    409);
        }

        /// <summary>
        /// Identidad de un lugar: su nombre mostrado normalizado (mayúsculas, sin espacios al
        /// borde). Un lugar sin nombre resoluble queda único para sí mismo, para no colisionar con
        /// nadie. Misma regla que <c>ga_lugar_clave()</c> en BD.
        /// </summary>
        private static string Clave(int lugarId, string? nombre)
            => string.IsNullOrWhiteSpace(nombre) ? $"#{lugarId}" : nombre.Trim().ToUpperInvariant();

        private static void ValidarDto(int origenId, int destinoId, decimal monto)
        {
            if (origenId == destinoId)
                throw new AbrilException("El lugar de origen y el destino no pueden ser iguales.", 400);
            if (monto < 0)
                throw new AbrilException("El monto no puede ser negativo.", 400);
        }

        /// <summary>
        /// Valida que los dos lugares existan, estén activos y no sean "Otro lugar", y devuelve de
        /// paso la clave de cada uno (ver <see cref="ValidarParDeLugaresAsync"/>): resolver el
        /// nombre acá evita una segunda consulta a <c>ga_lugar</c> solo para eso.
        /// </summary>
        private static async Task<(string Origen, string Destino)> AsegurarLugaresActivos(
            AppDbContext ctx, int origenId, int destinoId)
        {
            var lugares = await (
                from l in ctx.GaLugar
                join p in ctx.Project on l.ProjectId equals p.ProjectId into pGroup
                from p in pGroup.DefaultIfEmpty()
                where l.Id == origenId || l.Id == destinoId
                select new
                {
                    l.Id,
                    l.Activo,
                    l.Tipo,
                    Nombre = l.Tipo == "proyecto" ? (p != null ? p.ProjectDescription : null) : l.Nombre,
                }
            ).ToListAsync();

            var origen = lugares.FirstOrDefault(l => l.Id == origenId)
                ?? throw new AbrilException("Lugar de origen no encontrado.", 404);
            var destino = lugares.FirstOrDefault(l => l.Id == destinoId)
                ?? throw new AbrilException("Lugar de destino no encontrado.", 404);

            if (!origen.Activo || origen.Tipo == "libre")
                throw new AbrilException("El lugar de origen debe estar activo y no puede ser \"Otro lugar\".", 400);
            if (!destino.Activo || destino.Tipo == "libre")
                throw new AbrilException("El lugar de destino debe estar activo y no puede ser \"Otro lugar\".", 400);

            return (Clave(origen.Id, origen.Nombre), Clave(destino.Id, destino.Nombre));
        }
    }
}
