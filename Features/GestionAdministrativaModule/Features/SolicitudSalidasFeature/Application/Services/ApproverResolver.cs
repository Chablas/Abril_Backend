using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Services
{
    public class ApproverResolver : IApproverResolver
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ApproverResolver(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<string?> ResolveApproverEmailAsync(Worker user)
        {
            using var ctx = _factory.CreateDbContext();
            var workers = ctx.Worker.AsNoTracking();

            // ── REGLA 1: Jefe o Sub Gerente → Gerente del mismo Area ─────────
            if (user.Categoria == "Jefe" || user.Categoria == "Sub Gerente")
            {
                var gerente = await workers.FirstOrDefaultAsync(w =>
                    w.Area == user.Area && w.Categoria == "Gerente");
                return Pick(gerente);
            }

            // ── REGLA 2: Subarea Legal ───────────────────────────────────────
            if (user.Subarea == "Legal")
            {
                var coordinadorLegal = await workers.FirstOrDefaultAsync(w =>
                    w.Subarea == "Legal" && w.Categoria == "Coordinador");

                var gerenteLegal = await workers.FirstOrDefaultAsync(w =>
                    w.Area == user.Area && w.Categoria == "Gerente");

                // Coordinador Legal → Gerente
                if (user.Categoria == "Coordinador")
                    return Pick(gerenteLegal);

                // Otros en Legal → Coordinador (si no es self) → Gerente
                if (coordinadorLegal != null && coordinadorLegal.Id != user.Id)
                    return Pick(coordinadorLegal);
                return Pick(gerenteLegal);
            }

            // ── REGLA 3: Resto de áreas ──────────────────────────────────────
            var jefe = await workers.FirstOrDefaultAsync(w =>
                w.Subarea == user.Subarea && w.Categoria == "Jefe");
            if (jefe != null && jefe.Id != user.Id)
                return Pick(jefe);

            var subGerente = await workers.FirstOrDefaultAsync(w =>
                w.Subarea == user.Subarea && w.Categoria == "Sub Gerente");
            if (subGerente != null && subGerente.Id != user.Id)
                return Pick(subGerente);

            var gerenteResto = await workers.FirstOrDefaultAsync(w =>
                w.Area == user.Area && w.Categoria == "Gerente");
            return Pick(gerenteResto);
        }

        /// <summary>Devuelve el email del trabajador, o null si no tiene.</summary>
        private static string? Pick(Worker? w)
        {
            if (w == null) return null;
            if (!string.IsNullOrWhiteSpace(w.EmailPersonal)) return w.EmailPersonal.Trim();
            return null;
        }
    }
}
