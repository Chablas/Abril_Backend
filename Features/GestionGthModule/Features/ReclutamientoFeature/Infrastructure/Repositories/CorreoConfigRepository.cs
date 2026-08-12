using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Repositories
{
    /// <inheritdoc cref="ICorreoConfigRepository"/>
    public class CorreoConfigRepository : ICorreoConfigRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public CorreoConfigRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        /// <summary>
        /// Categoría que identifica al Gerente General, distinta de la genérica "GERENTE" que
        /// comparten los gerentes de área. Los nombres del catálogo van en MAYÚSCULAS.
        /// </summary>
        private const string CategoriaGerenteGeneral = "GERENTE GENERAL";

        // ───────────────────────────── Lectura ─────────────────────────────
        public async Task<CorreoConfigDto> GetConfigAsync(IReadOnlyList<string> tipoCodigos)
        {
            using var ctx = _factory.CreateDbContext();

            var codigos = tipoCodigos.Select(c => c.ToUpperInvariant()).ToList();

            var tipos = await ctx.GthCorreoTipo.AsNoTracking()
                .Where(t => t.State && codigos.Contains(t.Codigo.ToUpper()))
                .OrderBy(t => t.Orden).ThenBy(t => t.GthCorreoTipoId)
                .Select(t => new CorreoConfigEventoDto
                {
                    Id          = t.GthCorreoTipoId,
                    Codigo      = t.Codigo,
                    Nombre      = t.Nombre,
                    Descripcion = t.Descripcion,
                    Active      = t.Active,
                    Orden       = t.Orden,
                })
                .ToListAsync();
            if (tipos.Count == 0) return new CorreoConfigDto();

            var tipoIds = tipos.Select(t => t.Id).ToList();

            var filas = await ctx.GthCorreoDestinatario.AsNoTracking()
                .Where(d => d.State && tipoIds.Contains(d.GthCorreoTipoId))
                .OrderBy(d => d.Orden).ThenBy(d => d.GthCorreoDestinatarioId)
                .Select(d => new
                {
                    d.GthCorreoTipoId,
                    Fila = new CorreoDestinatarioFilaDto
                    {
                        DestinatarioId = d.GthCorreoDestinatarioId,
                        Codigo         = d.Codigo,
                        Nombre         = d.Nombre,
                        Descripcion    = d.Descripcion,
                        Email          = d.Email,
                        EsCopia        = d.EsCopia,
                        Active         = d.Active,
                        Orden          = d.Orden,
                    },
                })
                .ToListAsync();

            // Los dinámicos que no dependen de la solicitud se resuelven acá para que la
            // pantalla muestre el correo real y avise si hoy no resuelve a nadie. Solo se
            // consulta lo que alguna fila necesita.
            var codigosPresentes = new HashSet<string>(
                filas.Where(f => !string.IsNullOrWhiteSpace(f.Fila.Codigo)).Select(f => f.Fila.Codigo!),
                StringComparer.OrdinalIgnoreCase);

            var gerenteGeneral = codigosPresentes.Contains(CorreoDestinatarioCodigo.GerenteGeneral)
                ? await GetGerenteGeneralAsync(ctx)
                : null;
            var emailGth = codigosPresentes.Contains(CorreoDestinatarioCodigo.GthArea)
                ? await GetEmailAreaGthAsync(ctx)
                : null;

            foreach (var f in filas)
            {
                var fila = f.Fila;
                var esAdicional = string.IsNullOrWhiteSpace(fila.Codigo);

                fila.Editable   = esAdicional;
                fila.Eliminable = esAdicional;

                if (esAdicional)
                {
                    fila.EmailResuelto = fila.Email;
                    continue;
                }

                switch (fila.Codigo!.ToUpperInvariant())
                {
                    case CorreoDestinatarioCodigo.GerenteGeneral:
                        fila.EmailResuelto  = gerenteGeneral?.Email;
                        fila.NombreResuelto = gerenteGeneral?.Nombre;
                        break;

                    case CorreoDestinatarioCodigo.GthArea:
                        fila.EmailResuelto = emailGth;
                        break;

                    case CorreoDestinatarioCodigo.GerenteArea:
                        // Cambia según quién registre la solicitud: no hay un correo que mostrar.
                        fila.DependeDeLaSolicitud = true;
                        break;
                }

                fila.SinCorreo = !fila.DependeDeLaSolicitud && string.IsNullOrWhiteSpace(fila.EmailResuelto);
            }

            var porTipo = filas.ToLookup(f => f.GthCorreoTipoId, f => f.Fila);
            foreach (var tipo in tipos)
                tipo.Destinatarios = porTipo[tipo.Id].ToList();

            return new CorreoConfigDto { Eventos = tipos };
        }

        public async Task<List<CorreoDestinatarioEnvioDto>> GetDestinatariosEnvioAsync(string tipoCodigo)
        {
            using var ctx = _factory.CreateDbContext();

            return await (
                from d in ctx.GthCorreoDestinatario
                join t in ctx.GthCorreoTipo on d.GthCorreoTipoId equals t.GthCorreoTipoId
                where d.State && d.Active
                      && t.State && t.Active && t.Codigo.ToUpper() == tipoCodigo.ToUpper()
                orderby d.Orden, d.GthCorreoDestinatarioId
                select new CorreoDestinatarioEnvioDto
                {
                    Codigo  = d.Codigo,
                    Email   = d.Email,
                    Nombre  = d.Nombre,
                    EsCopia = d.EsCopia,
                    Orden   = d.Orden,
                })
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<CorreoDestinatarioResueltoDto?> GetGerenteGeneralAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await GetGerenteGeneralAsync(ctx);
        }

        public async Task<string?> GetEmailAreaGthAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await GetEmailAreaGthAsync(ctx);
        }

        /// <summary>
        /// Gerente General vigente. Una misma persona puede tener varias fichas en
        /// <c>workers</c> (reingreso), así que se filtra por ACTIVO y se desempata por la ficha
        /// más reciente en vez de dejar que Postgres devuelva una cualquiera.
        /// </summary>
        private static async Task<CorreoDestinatarioResueltoDto?> GetGerenteGeneralAsync(AppDbContext ctx)
        {
            return await (
                from w in ctx.Worker.AsNoTracking()
                join c in ctx.Categoria.AsNoTracking() on w.CategoriaId equals c.CategoriaId
                where c.State && c.Nombre.ToUpper() == CategoriaGerenteGeneral
                      && w.Estado == "ACTIVO"
                      && w.EmailCorporativo != null && w.EmailCorporativo.Contains("@")
                orderby w.Id descending
                select new CorreoDestinatarioResueltoDto
                {
                    Email  = w.EmailCorporativo!,
                    Nombre = w.Person != null ? w.Person.FullName : w.ApellidoNombre,
                })
                .FirstOrDefaultAsync();
        }

        private static async Task<string?> GetEmailAreaGthAsync(AppDbContext ctx)
        {
            return await ctx.AreaScope.AsNoTracking()
                .Where(s => s.AreaScopeId == AreaScopeIds.GestionDelTalentoHumano && s.State)
                .Select(s => s.Email)
                .FirstOrDefaultAsync();
        }

        // ───────────────────────────── Escritura ─────────────────────────────
        public async Task<int> CreateAdicionalAsync(
            string tipoCodigo, string email, string? nombre, bool esCopia, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var tipo = await ctx.GthCorreoTipo
                .FirstOrDefaultAsync(t => t.State && t.Codigo.ToUpper() == tipoCodigo.ToUpper())
                ?? throw new AbrilException("El correo indicado no existe.", 400);

            var emailNorm = email.Trim().ToLowerInvariant();

            var duplicado = await ctx.GthCorreoDestinatario.AnyAsync(d =>
                d.State && d.Codigo == null && d.GthCorreoTipoId == tipo.GthCorreoTipoId &&
                d.Email != null && d.Email.ToLower() == emailNorm);
            if (duplicado)
                throw new AbrilException("Ese correo ya está agregado como destinatario de este correo.", 409);

            // Los adicionales van siempre después de los dinámicos del catálogo (orden 1..n).
            var ultimoOrden = await ctx.GthCorreoDestinatario
                .Where(d => d.State && d.GthCorreoTipoId == tipo.GthCorreoTipoId)
                .MaxAsync(d => (int?)d.Orden) ?? 0;

            var now = DateTimeOffset.UtcNow;
            var dest = new GthCorreoDestinatario
            {
                GthCorreoTipoId = tipo.GthCorreoTipoId,
                Codigo          = null,
                Email           = emailNorm,
                Nombre          = string.IsNullOrWhiteSpace(nombre) ? null : nombre.Trim(),
                EsCopia         = esCopia,
                Orden           = Math.Max(ultimoOrden + 1, 100),
                Active          = true,
                State           = true,
                CreatedDateTime = now,
                CreatedUserId   = userId,
            };
            ctx.GthCorreoDestinatario.Add(dest);
            await ctx.SaveChangesAsync();

            return dest.GthCorreoDestinatarioId;
        }

        public async Task UpdateAdicionalAsync(
            int destinatarioId, string email, string? nombre, bool esCopia, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var dest = await ctx.GthCorreoDestinatario
                .FirstOrDefaultAsync(d => d.GthCorreoDestinatarioId == destinatarioId && d.State)
                ?? throw new AbrilException("Destinatario no encontrado.", 404);

            if (dest.Codigo != null)
                throw new AbrilException(
                    "Este destinatario se resuelve al enviar: su correo no se edita acá.", 409);

            var emailNorm = email.Trim().ToLowerInvariant();

            var duplicado = await ctx.GthCorreoDestinatario.AnyAsync(d =>
                d.State && d.Codigo == null && d.GthCorreoDestinatarioId != destinatarioId &&
                d.GthCorreoTipoId == dest.GthCorreoTipoId &&
                d.Email != null && d.Email.ToLower() == emailNorm);
            if (duplicado)
                throw new AbrilException("Ese correo ya está agregado como destinatario de este correo.", 409);

            dest.Email             = emailNorm;
            dest.Nombre            = string.IsNullOrWhiteSpace(nombre) ? null : nombre.Trim();
            dest.EsCopia           = esCopia;
            dest.UpdatedDateTime   = DateTimeOffset.UtcNow;
            dest.UpdatedUserId     = userId;
            await ctx.SaveChangesAsync();
        }

        public async Task SetDestinatarioActiveAsync(int destinatarioId, bool active, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var dest = await ctx.GthCorreoDestinatario
                .FirstOrDefaultAsync(d => d.GthCorreoDestinatarioId == destinatarioId && d.State)
                ?? throw new AbrilException("Destinatario no encontrado.", 404);

            dest.Active          = active;
            dest.UpdatedDateTime = DateTimeOffset.UtcNow;
            dest.UpdatedUserId   = userId;
            await ctx.SaveChangesAsync();
        }

        public async Task SetTipoActiveAsync(string tipoCodigo, bool active, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var tipo = await ctx.GthCorreoTipo
                .FirstOrDefaultAsync(t => t.State && t.Codigo.ToUpper() == tipoCodigo.ToUpper())
                ?? throw new AbrilException("El correo indicado no existe.", 404);

            tipo.Active            = active;
            tipo.UpdatedDateTime   = DateTimeOffset.UtcNow;
            tipo.UpdatedUserId     = userId;
            await ctx.SaveChangesAsync();
        }

        public async Task DeleteAdicionalAsync(int destinatarioId, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var dest = await ctx.GthCorreoDestinatario
                .FirstOrDefaultAsync(d => d.GthCorreoDestinatarioId == destinatarioId && d.State)
                ?? throw new AbrilException("Destinatario no encontrado.", 404);

            if (dest.Codigo != null)
                throw new AbrilException(
                    "Este destinatario es parte del catálogo y no se puede eliminar. Apágalo con su interruptor.", 409);

            // Soft delete: nada se borra de la BD (auditoría).
            dest.State           = false;
            dest.UpdatedDateTime = DateTimeOffset.UtcNow;
            dest.UpdatedUserId   = userId;
            await ctx.SaveChangesAsync();
        }
    }
}
