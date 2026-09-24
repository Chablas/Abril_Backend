using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Shared.Services
{
    /// <summary>
    /// Lo que Habilitación necesita de un trabajador que entra a un proyecto: su asignación
    /// (<c>ss_hab_worker_proyecto</c>) y sus entregables (<c>ss_hab_trabajador</c>).
    ///
    /// Vive acá y no en <c>HabTrabajadorRepository</c> porque lo escriben dos módulos: el alta
    /// de trabajador de SSOMA/Habilitación y la aprobación de la carta oferta de Reclutamiento.
    /// Hasta el 2026-09-24 la carta oferta no hacía ninguna de las dos cosas, así que el
    /// ingreso nunca salía en «Programar Inducción» —que arma su lista desde
    /// <c>ss_hab_worker_proyecto</c>— y aparecía «Habilitado» con el único entregable que tenía,
    /// el del EMO.
    ///
    /// Los dos métodos MUTAN el contexto que reciben y NO llaman a SaveChanges: la carta oferta
    /// los mete en la misma transacción que el paso de la ficha a ACTIVO.
    /// </summary>
    public static class HabilitacionIngresoHelper
    {
        /// <summary>
        /// Deja al trabajador asignado al proyecto. Si ya tiene una asignación abierta ahí, la
        /// devuelve tal cual (el alta manual responde 409 antes de llegar acá).
        ///
        /// Si ya estuvo en ese proyecto reabre la asignación cerrada en vez de crear otra fila,
        /// para no duplicar el historial cuando vuelve a un proyecto en el que ya estuvo (p. ej.
        /// tras un reingreso que no sincronizó esta tabla).
        ///
        /// Si ya tiene «Inducción Obra» aprobada, el proyecto la hereda: no debe quedar pendiente
        /// de inducción cuando su checklist ya dice Aprobado.
        /// </summary>
        public static async Task<WorkerProyecto> AsignarProyectoAsync(
            AppDbContext ctx, int workerId, int proyectoId, int? empresaId, DateOnly fechaInicio, DateTimeOffset ahora)
        {
            // Todas las del trabajador en ese proyecto, la más reciente primero: son pocas filas y
            // así la abierta y la cerrada a reabrir salen de una sola consulta.
            var asignaciones = await ctx.WorkerProyecto
                .Where(wp => wp.WorkerId == workerId && wp.ProyectoId == proyectoId)
                .OrderByDescending(wp => wp.CreatedAt)
                .ThenByDescending(wp => wp.Id)
                .ToListAsync();

            var activa = asignaciones.FirstOrDefault(wp => wp.FechaFin == null);
            if (activa != null) return activa;

            var induccionYaAprobada = await ctx.SsHabTrabajador
                .AnyAsync(h => h.WorkerId == workerId && h.ItemId == HabItemIds.InduccionObra && h.Estado == "Aprobado");
            DateOnly? fechaInduccion = induccionYaAprobada ? DateOnly.FromDateTime(ahora.UtcDateTime) : null;

            var cerrada = asignaciones.FirstOrDefault();
            if (cerrada != null)
            {
                cerrada.EmpresaId = empresaId ?? cerrada.EmpresaId;
                cerrada.FechaInicio = fechaInicio;
                cerrada.FechaFin = null;
                cerrada.InduccionCompletada = induccionYaAprobada;
                cerrada.FechaInduccion = fechaInduccion;
                cerrada.UpdatedAt = ahora;
                return cerrada;
            }

            var nueva = new WorkerProyecto
            {
                WorkerId = workerId,
                ProyectoId = proyectoId,
                EmpresaId = empresaId,
                FechaInicio = fechaInicio,
                FechaFin = null,
                InduccionCompletada = induccionYaAprobada,
                FechaInduccion = fechaInduccion,
                CreatedAt = ahora,
                UpdatedAt = null
            };
            ctx.WorkerProyecto.Add(nueva);
            return nueva;
        }

        /// <summary>
        /// Le crea en «Falta» los entregables que le aplican y todavía no tiene. Los que ya
        /// existen no se tocan: el Certificado de Aptitud de un ingreso por Reclutamiento, por
        /// ejemplo, ya viene aprobado desde su EMO de ingreso.
        ///
        /// La categoría llega aparte porque sale del puesto (<c>workers.puesto_id →
        /// puesto.categoria_id</c>) y cada llamador ya la tiene resuelta a su manera.
        /// </summary>
        public static async Task InicializarEntregablesAsync(
            AppDbContext ctx, Worker worker, int? categoriaId, string? categoriaNombre)
        {
            var workerType = string.Equals(worker.ContrataCasa?.Trim(), "Casa", StringComparison.OrdinalIgnoreCase)
                ? "CASA"
                : "CONTRATISTA";
            var esContratista = string.Equals(worker.ContrataCasa?.Trim(), "Contratista", StringComparison.OrdinalIgnoreCase);
            var esCasaPracticante = workerType == "CASA" && categoriaId == CategoriaIds.Practicante;
            var obraOficina = ObraOficinaStaffIds.Nombre(worker.ObraOficinaStaffId);

            var todosItems = await ctx.SsItemTrabajador
                .Where(i => i.Activo)
                .ToListAsync();

            var itemsAplicables = todosItems
                .Where(i => i.AplicaA == "TODOS" ||
                            (i.AplicaA == "CASA" && workerType == "CASA") ||
                            (i.AplicaA == "CONTRATISTA" && workerType == "CONTRATISTA"))
                .Where(i => CsvContiene(i.AplicaCategoria, categoriaNombre))
                .Where(i => CsvContiene(i.AplicaObraOficina, obraOficina))
                .Where(i => !CsvExcluye(i.ExcluyeObraOficina, obraOficina))
                .Where(i => !esContratista || !CsvExcluye(i.ExcluyeCategoriaContratista, categoriaNombre))
                .Where(i => !(esCasaPracticante && i.Id == HabItemIds.VidaLey))
                .ToList();

            var itemIds = itemsAplicables.Select(i => i.Id).ToList();

            var existentesIds = (await ctx.SsHabTrabajador
                .Where(h => h.WorkerId == worker.Id && itemIds.Contains(h.ItemId))
                .Select(h => h.ItemId)
                .ToListAsync())
                .ToHashSet();

            var ahora = DateTime.UtcNow;
            ctx.SsHabTrabajador.AddRange(itemsAplicables
                .Where(i => !existentesIds.Contains(i.Id))
                .Select(i => new SsHabTrabajador
                {
                    WorkerId = worker.Id,
                    ItemId = i.Id,
                    Estado = "Falta",
                    Vigencia = null,
                    CreatedAt = ahora,
                    UpdatedAt = ahora
                }));
        }

        private static bool CsvContiene(string? csv, string? valor)
            => csv == null || csv.Split(',', StringSplitOptions.TrimEntries)
                   .Contains(valor ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        private static bool CsvExcluye(string? csv, string? valor)
            => csv != null && csv.Split(',', StringSplitOptions.TrimEntries)
                   .Contains(valor ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }
}
