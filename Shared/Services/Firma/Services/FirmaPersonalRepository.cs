using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Models;
using Abril_Backend.Shared.Services.Firma.Dtos;
using Abril_Backend.Shared.Services.Firma.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Shared.Services.Firma.Services
{
    /// <inheritdoc cref="IFirmaPersonalRepository"/>
    public class FirmaPersonalRepository : IFirmaPersonalRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public FirmaPersonalRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        /// <summary>Los timestamps se guardan en UTC y se sirven al frontend en hora de Perú.</summary>
        private static readonly TimeSpan PeruOffset = TimeSpan.FromHours(-5);

        /// <summary>
        /// Orden en el que se muestran y en el que se elige cuál estampar: la IMAGEN primero. Cuando
        /// alguien subió una imagen es porque quiere esa y no el trazo del mouse, así que gana
        /// aunque el dibujo sea más reciente.
        /// </summary>
        private static readonly string[] OrdenTipos = { FirmaTipo.CodigoImagen, FirmaTipo.CodigoDibujo };

        private static int Prioridad(string codigo)
        {
            var i = Array.IndexOf(OrdenTipos, codigo);
            return i < 0 ? OrdenTipos.Length : i;
        }

        public async Task<FirmaPersonalEstadoDto> GetEstadoByUserId(int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var tipos = await LeerTipos(ctx);

            // El data URL y la conversión a hora de Perú se arman en memoria: ni el base64 ni el
            // cambio de offset se traducen a SQL.
            var filas = await (
                from pf in ctx.PersonFirma
                join p in ctx.Person on pf.PersonId equals p.PersonId
                join ft in ctx.FirmaTipo on pf.FirmaTipoId equals ft.Id
                where pf.State && ft.State && p.UserId == userId
                select new { ft.Codigo, pf.ImageBytes, pf.Mime, pf.CreatedDateTime, pf.UpdatedDateTime })
                .ToListAsync();

            return new FirmaPersonalEstadoDto
            {
                Tipos  = tipos,
                Firmas = filas
                    .OrderBy(f => Prioridad(f.Codigo))
                    .Select(f => new FirmaPersonalDto
                    {
                        Tipo            = f.Codigo,
                        ImageDataUrl    = $"data:{f.Mime};base64,{Convert.ToBase64String(f.ImageBytes)}",
                        UpdatedDateTime = (f.UpdatedDateTime ?? f.CreatedDateTime)
                            .ToOffset(PeruOffset).DateTime,
                    })
                    .ToList(),
            };
        }

        public async Task<FirmaPersonalEstadoDto> Upsert(
            int userId, string tipoCodigo, byte[] imageBytes, string mime)
        {
            using var ctx = _factory.CreateDbContext();

            var personId = await ctx.Person
                .Where(x => x.UserId == userId)
                .Select(x => (int?)x.PersonId)
                .FirstOrDefaultAsync()
                ?? throw new AbrilException("No se encontró una persona asociada al usuario actual.");

            await UpsertInterno(ctx, personId, tipoCodigo, imageBytes, mime, userId);

            return await GetEstadoByUserId(userId);
        }

        public async Task UpsertByPersonId(int personId, string tipoCodigo, byte[] imageBytes, string mime)
        {
            using var ctx = _factory.CreateDbContext();

            var existe = await ctx.Person.AnyAsync(x => x.PersonId == personId);
            if (!existe)
                throw new AbrilException(
                    "No encontramos tu ficha en nuestros registros. Escríbele a Gestión de Talento Humano.", 404);

            // Sin usuario detrás (el postulante firma desde el enlace público), así que la fila no
            // lleva quién la creó: la trazabilidad de esa firma la da la carta oferta.
            await UpsertInterno(ctx, personId, tipoCodigo, imageBytes, mime, userId: null);
        }

        /// <summary>
        /// Escribe la firma de esa persona y ese tipo. El índice único parcial
        /// <c>ux_person_firma_vigente</c> deja una sola fila viva por (persona, tipo), así que
        /// registrar de nuevo reemplaza la imagen en vez de agregar una fila.
        /// </summary>
        private static async Task UpsertInterno(
            AppDbContext ctx, int personId, string tipoCodigo, byte[] imageBytes, string mime, int? userId)
        {
            var tipoId = await ctx.FirmaTipo
                .Where(t => t.State && t.Codigo == tipoCodigo)
                .Select(t => (int?)t.Id)
                .FirstOrDefaultAsync()
                ?? throw new AbrilException($"El tipo de firma '{tipoCodigo}' no existe.", 400);

            var firma = await ctx.PersonFirma
                .FirstOrDefaultAsync(x => x.State && x.PersonId == personId && x.FirmaTipoId == tipoId);

            var ahora = DateTimeOffset.UtcNow;

            if (firma == null)
            {
                ctx.PersonFirma.Add(new PersonFirma
                {
                    PersonId        = personId,
                    FirmaTipoId     = tipoId,
                    ImageBytes      = imageBytes,
                    Mime            = mime,
                    CreatedDateTime = ahora,
                    CreatedUserId   = userId,
                });
            }
            else
            {
                firma.ImageBytes      = imageBytes;
                firma.Mime            = mime;
                firma.UpdatedDateTime = ahora;
                firma.UpdatedUserId   = userId;
            }

            await ctx.SaveChangesAsync();
        }

        public async Task<(byte[] Bytes, string Mime)?> GetActiveBytesByUserId(
            int userId, IReadOnlyCollection<string>? tiposPermitidos = null)
        {
            using var ctx = _factory.CreateDbContext();

            var candidatas = await (
                from pf in ctx.PersonFirma
                join p in ctx.Person on pf.PersonId equals p.PersonId
                join ft in ctx.FirmaTipo on pf.FirmaTipoId equals ft.Id
                where pf.State && ft.State && p.UserId == userId
                select new { ft.Codigo, pf.ImageBytes, pf.Mime })
                .ToListAsync();

            var elegida = candidatas
                .Where(x => tiposPermitidos == null || tiposPermitidos.Contains(x.Codigo))
                .OrderBy(x => Prioridad(x.Codigo))
                .FirstOrDefault();

            return elegida == null ? null : (elegida.ImageBytes, elegida.Mime);
        }

        public async Task<(byte[] Bytes, string Mime, DateTimeOffset? UpdatedDateTime)?>
            GetActiveBytesByPersonId(int personId)
        {
            using var ctx = _factory.CreateDbContext();

            var candidatas = await (
                from pf in ctx.PersonFirma
                join ft in ctx.FirmaTipo on pf.FirmaTipoId equals ft.Id
                where pf.State && ft.State && pf.PersonId == personId
                select new { ft.Codigo, pf.ImageBytes, pf.Mime, pf.CreatedDateTime, pf.UpdatedDateTime })
                .ToListAsync();

            var elegida = candidatas.OrderBy(x => Prioridad(x.Codigo)).FirstOrDefault();

            return elegida == null
                ? null
                : (elegida.ImageBytes, elegida.Mime, elegida.UpdatedDateTime ?? elegida.CreatedDateTime);
        }

        public async Task<List<FirmaTipoDto>> GetTipos()
        {
            using var ctx = _factory.CreateDbContext();
            return await LeerTipos(ctx);
        }

        public async Task<List<FirmaTipoDto>> SaveTipos(
            IReadOnlyCollection<FirmaTipoActivoDto> tipos, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var codigos = tipos.Select(t => t.Codigo).ToList();
            var filas = await ctx.FirmaTipo
                .Where(t => t.State && codigos.Contains(t.Codigo))
                .ToListAsync();

            foreach (var fila in filas)
            {
                var pedido = tipos.First(t => t.Codigo == fila.Codigo);
                if (fila.Active == pedido.Activo) continue;

                fila.Active           = pedido.Activo;
                fila.UpdatedDateTime  = DateTimeOffset.UtcNow;
                fila.UpdatedUserId    = userId;
            }

            await ctx.SaveChangesAsync();

            return await LeerTipos(ctx);
        }

        private static async Task<List<FirmaTipoDto>> LeerTipos(AppDbContext ctx)
        {
            var tipos = await ctx.FirmaTipo
                .Where(t => t.State)
                .Select(t => new FirmaTipoDto { Codigo = t.Codigo, Nombre = t.Nombre, Activo = t.Active })
                .ToListAsync();

            return tipos.OrderBy(t => Prioridad(t.Codigo)).ToList();
        }
    }
}
