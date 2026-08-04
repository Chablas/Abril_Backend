using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Configuracion;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Repositories
{
    /// <summary>
    /// Destinatarios de los correos de programación de EMO (ss_emo_correo_destinatario).
    /// Sirve tanto a la pantalla de configuración (CRUD) como al envío real.
    /// </summary>
    public class EmoCorreoConfigRepository : IEmoCorreoConfigRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public EmoCorreoConfigRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<EmoCorreosConfigDto> GetConfigAsync()
        {
            using var ctx = _factory.CreateDbContext();

            // Una sola consulta para ambas secciones: se separan en memoria por el
            // código del tipo, sin un segundo roundtrip por lista.
            var filas = await (
                from d in ctx.SsEmoCorreoDestinatario
                join t in ctx.SsEmoCorreoTipo on d.TipoId equals t.Id
                where d.State && t.State
                orderby d.Orden, d.Id
                select new EmoCorreoDestinatarioDto
                {
                    Id          = d.Id,
                    Tipo        = t.Codigo,
                    Codigo      = d.Codigo,
                    Email       = d.Email,
                    Nombre      = d.Nombre,
                    Descripcion = d.Descripcion,
                    Editable    = d.Editable,
                    Active      = d.Active,
                    Orden       = d.Orden,
                })
                .AsNoTracking()
                .ToListAsync();

            return new EmoCorreosConfigDto
            {
                Principales = filas
                    .Where(f => string.Equals(f.Tipo, EmoCorreoTipoCodigo.Principal, StringComparison.OrdinalIgnoreCase))
                    .ToList(),
                Copias = filas
                    .Where(f => string.Equals(f.Tipo, EmoCorreoTipoCodigo.Copia, StringComparison.OrdinalIgnoreCase))
                    .ToList(),
            };
        }

        public async Task<int> CreateAsync(string tipoCodigo, string email, string? nombre)
        {
            using var ctx = _factory.CreateDbContext();

            var tipo = await ctx.SsEmoCorreoTipo
                .FirstOrDefaultAsync(t => t.State && t.Codigo.ToUpper() == tipoCodigo.ToUpper())
                ?? throw new AbrilException("El tipo de destinatario no existe.", 400);

            var emailNorm = email.Trim();

            var duplicado = await ctx.SsEmoCorreoDestinatario.AnyAsync(d =>
                d.State &&
                d.TipoId == tipo.Id &&
                d.Email != null &&
                d.Email.ToLower() == emailNorm.ToLower());
            if (duplicado)
                throw new AbrilException("Ese correo ya está registrado en esta lista.", 409);

            // Los nuevos van al final de su sección (los fijos usan orden 0).
            var ultimoOrden = await ctx.SsEmoCorreoDestinatario
                .Where(d => d.State && d.TipoId == tipo.Id)
                .MaxAsync(d => (int?)d.Orden) ?? 0;

            var ent = new SsEmoCorreoDestinatario
            {
                TipoId    = tipo.Id,
                Codigo    = null,
                Email     = emailNorm,
                Nombre    = string.IsNullOrWhiteSpace(nombre) ? null : nombre.Trim(),
                Editable  = true,
                Orden     = ultimoOrden + 1,
                Active    = true,
                State     = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            ctx.SsEmoCorreoDestinatario.Add(ent);
            await ctx.SaveChangesAsync();
            return ent.Id;
        }

        public async Task UpdateAsync(int id, string email, string? nombre)
        {
            using var ctx = _factory.CreateDbContext();

            var ent = await ctx.SsEmoCorreoDestinatario.FirstOrDefaultAsync(d => d.Id == id && d.State)
                ?? throw new AbrilException("Destinatario no encontrado.", 404);

            if (!ent.Editable)
                throw new AbrilException("Este destinatario es fijo: solo se puede activar o desactivar.", 409);

            var emailNorm = email.Trim();

            var duplicado = await ctx.SsEmoCorreoDestinatario.AnyAsync(d =>
                d.State &&
                d.Id != id &&
                d.TipoId == ent.TipoId &&
                d.Email != null &&
                d.Email.ToLower() == emailNorm.ToLower());
            if (duplicado)
                throw new AbrilException("Ese correo ya está registrado en esta lista.", 409);

            ent.Email     = emailNorm;
            ent.Nombre    = string.IsNullOrWhiteSpace(nombre) ? null : nombre.Trim();
            ent.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task SetActiveAsync(int id, bool active)
        {
            using var ctx = _factory.CreateDbContext();

            var ent = await ctx.SsEmoCorreoDestinatario.FirstOrDefaultAsync(d => d.Id == id && d.State)
                ?? throw new AbrilException("Destinatario no encontrado.", 404);

            ent.Active    = active;
            ent.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            using var ctx = _factory.CreateDbContext();

            var ent = await ctx.SsEmoCorreoDestinatario.FirstOrDefaultAsync(d => d.Id == id && d.State)
                ?? throw new AbrilException("Destinatario no encontrado.", 404);

            if (!ent.Editable)
                throw new AbrilException("Este destinatario es fijo y no se puede eliminar.", 409);

            // Soft delete: nada se borra de la BD (auditoría).
            ent.State     = false;
            ent.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task<EmoCorreoEnvioConfigDto> GetEnvioConfigAsync()
        {
            using var ctx = _factory.CreateDbContext();

            var filas = await (
                from d in ctx.SsEmoCorreoDestinatario
                join t in ctx.SsEmoCorreoTipo on d.TipoId equals t.Id
                where d.State && t.State && d.Active
                select new { t.Codigo, DestCodigo = d.Codigo, d.Email })
                .AsNoTracking()
                .ToListAsync();

            var cfg = new EmoCorreoEnvioConfigDto
            {
                // Si la tabla está vacía (p. ej. antes de sembrarla) se mantiene el
                // comportamiento histórico: el correo va a la clínica.
                IncluirClinica = filas.Count == 0
                    || filas.Any(f => string.Equals(f.DestCodigo, EmoCorreoDestinatarioCodigo.Clinica, StringComparison.OrdinalIgnoreCase)),
            };

            foreach (var f in filas)
            {
                if (string.IsNullOrWhiteSpace(f.Email)) continue; // destinatarios dinámicos (CLINICA)

                var lista = string.Equals(f.Codigo, EmoCorreoTipoCodigo.Copia, StringComparison.OrdinalIgnoreCase)
                    ? cfg.Copias
                    : cfg.Principales;
                lista.Add(f.Email.Trim());
            }

            return cfg;
        }
    }
}
