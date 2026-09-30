using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Services.Residentes.Services;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Evaluaciones.Infrastructure.Repositories
{
    /// <summary>
    /// Los residentes que se evalúan en la Evaluación 360° de Residentes, una fila por obra: el
    /// residente de cada obra en Configuración → Proyectos, con el rol RESIDENTE
    /// (<c>ResidenteQueries.ObrasConSuResidente</c>), con ficha de Abril (Casa), no retirado y usuario
    /// activo. Ya no se deducen del puesto ni de la vinculación. Los usan la lista de residentes
    /// evaluables y los recordatorios, para que los dos vean los mismos; las consultas de Dapper los
    /// reciben como arreglos paralelos (<c>unnest(@ObraIds, @ResidenteUserIds)</c>).
    /// </summary>
    internal static class EvResidentesEvaluables
    {
        public static async Task<Arreglos> CargarAsync(AppDbContext ctx)
        {
            var filas = await (
                from o in ctx.ObrasConSuResidente()
                join w in ctx.Worker on o.WorkerId equals w.Id
                join u in ctx.User on o.UserId equals u.UserId
                where w.ContrataCasa == "Casa"
                   && WorkersEstadoIds.NoRetirados.Contains(w.WorkersEstadoId)
                   && u.Active
                select new { o.ProjectId, o.WorkerId, o.PersonId, o.UserId }
            ).ToListAsync();

            return new Arreglos(
                filas.Select(f => f.ProjectId).ToArray(),
                filas.Select(f => f.WorkerId).ToArray(),
                filas.Select(f => f.PersonId).ToArray(),
                filas.Select(f => f.UserId).ToArray());
        }

        /// <summary>Una posición por obra: el residente de <c>ObraIds[i]</c> es la ficha
        /// <c>ResidenteWorkerIds[i]</c>, de la persona <c>ResidentePersonIds[i]</c> y el usuario
        /// <c>ResidenteUserIds[i]</c>.</summary>
        public sealed record Arreglos(int[] ObraIds, int[] ResidenteWorkerIds, int[] ResidentePersonIds, int[] ResidenteUserIds)
        {
            public bool Vacio => ObraIds.Length == 0;
        }
    }
}
