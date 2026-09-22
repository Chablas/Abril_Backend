using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Shared.Services.Jerarquia
{
    /// <summary>
    /// Las obras y quién está en cada una, en un solo lugar: qué proyecto cuenta como obra, cuál es
    /// la obra vigente de un trabajador y de qué obras es residente o administrador una persona.
    ///
    /// Lo usan los dos lados que tienen que decir lo mismo: el algoritmo que decide quién aprueba
    /// y firma (<c>JefeRevisorResolver</c>, <c>ConsolidadorResolver</c>, vía
    /// <see cref="EstructuraAreaLoader"/>) y la visibilidad de las bandejas de Gestión
    /// Administrativa, que desde el 2026-09-22 le muestra a cada residente y administrador de obra
    /// todo lo de su obra. Si las dos cosas calcularan la obra de un trabajador por separado, el
    /// algoritmo podría pedirle una firma a alguien que después no ve el documento.
    ///
    /// Todo se lee en vivo de <c>project</c> y <c>worker_vinculaciones</c>: cambiar al residente de
    /// una obra, o a un trabajador de obra, se refleja en la siguiente petición sin tocar nada más.
    /// </summary>
    public static class ObrasLoader
    {
        /// <summary>
        /// Identifica a OFICINA CENTRAL, que es una fila más de <c>project</c> sin bandera que la
        /// distinga de una obra: la única salida es el nombre normalizado (en prod va en mayúsculas
        /// y en dev como "Oficina Central"). Mismo criterio que usa el aviso de obra del Onboarding.
        /// </summary>
        private const string ProyectoOficinaCentral = "OFICINA CENTRAL";

        /// <summary>
        /// Una obra de la que una persona está a cargo, y en qué papel. Puede ser las dos cosas a la
        /// vez, aunque en la práctica no pasa.
        /// </summary>
        public sealed record ObraACargo(int ProjectId, string Nombre, bool EsResidente, bool EsAdministrador);

        /// <summary>
        /// Los proyectos vivos que son OBRAS: todos menos OFICINA CENTRAL, que tiene
        /// <c>residente_workers_id</c> cargado y sin esta exclusión se llevaría a toda la oficina.
        ///
        /// Va sin <c>AsNoTracking</c> para poder usarse también como subconsulta dentro de otra
        /// consulta (así lo usa la visibilidad); quienes la consumen proyectan, así que no se
        /// rastrea nada igual.
        /// </summary>
        public static IQueryable<Project> Obras(AppDbContext ctx) =>
            ctx.Project.Where(p =>
                p.State
                && p.ProjectDescription != null
                && p.ProjectDescription.ToUpper().Trim() != ProyectoOficinaCentral);

        /// <summary>
        /// La obra vigente de cada trabajador: su vinculación con <c>fecha_fin</c> NULL y proyecto,
        /// la más reciente por <c>created_at</c> y después por id (criterio de
        /// <c>HabTrabajadorRepository.LatestVincActiva</c>, que es lo que mantiene GTH con
        /// "Cambiar obra / puesto de trabajo"). Un trabajador retirado no tiene vinculación vigente
        /// y no aparece en el diccionario.
        /// </summary>
        public static async Task<Dictionary<int, int?>> ObraVigentePorTrabajadorAsync(
            AppDbContext ctx, IReadOnlyCollection<int> workerIds)
        {
            var ids = workerIds as List<int> ?? workerIds.ToList();
            if (ids.Count == 0) return new Dictionary<int, int?>();

            var vinculaciones = await ctx.WorkerVinculacion.AsNoTracking()
                .Where(v => ids.Contains(v.WorkerId) && v.FechaFin == null && v.ProyectoId != null)
                .OrderByDescending(v => v.CreatedAt)
                .ThenByDescending(v => v.Id)
                .Select(v => new { v.WorkerId, v.ProyectoId })
                .ToListAsync();

            return vinculaciones
                .GroupBy(v => v.WorkerId)
                .ToDictionary(g => g.Key, g => g.First().ProyectoId);
        }

        /// <summary>
        /// Los trabajadores cuya obra vigente es alguna de <paramref name="proyectoIds"/>. Se parte
        /// de quien tenga alguna vinculación abierta en esas obras y se confirma con
        /// <see cref="ObraVigentePorTrabajadorAsync"/>: con dos vinculaciones abiertas manda la más
        /// reciente, y esa puede ser de otra obra.
        /// </summary>
        public static async Task<HashSet<int>> TrabajadoresDeLasObrasAsync(
            AppDbContext ctx, IReadOnlyCollection<int> proyectoIds)
        {
            var obras = proyectoIds.Distinct().ToList();
            if (obras.Count == 0) return new HashSet<int>();

            var candidatos = await ctx.WorkerVinculacion.AsNoTracking()
                .Where(v => v.FechaFin == null
                         && v.ProyectoId != null
                         && obras.Contains(v.ProyectoId.Value))
                .Select(v => v.WorkerId)
                .Distinct()
                .ToListAsync();

            if (candidatos.Count == 0) return new HashSet<int>();

            var vigentes = await ObraVigentePorTrabajadorAsync(ctx, candidatos);
            return vigentes
                .Where(kv => kv.Value != null && obras.Contains(kv.Value.Value))
                .Select(kv => kv.Key)
                .ToHashSet();
        }

        /// <summary>
        /// Las obras de las que alguna de estas fichas es hoy residente
        /// (<c>project.residente_workers_id</c>) o administrador de obra
        /// (<c>project.workers_coord_admin_id</c>). Se pasan TODAS las fichas de la persona: con un
        /// reingreso el proyecto puede apuntar a cualquiera de ellas.
        /// </summary>
        public static async Task<List<ObraACargo>> ObrasACargoAsync(
            AppDbContext ctx, IReadOnlyCollection<int> workerIds)
        {
            var ids = workerIds as List<int> ?? workerIds.ToList();
            if (ids.Count == 0) return new List<ObraACargo>();

            var filas = await Obras(ctx)
                .Where(p => (p.ResidenteWorkersId != null && ids.Contains(p.ResidenteWorkersId.Value))
                         || (p.WorkersCoordAdminId != null && ids.Contains(p.WorkersCoordAdminId.Value)))
                .Select(p => new { p.ProjectId, p.ProjectDescription, p.ResidenteWorkersId, p.WorkersCoordAdminId })
                .ToListAsync();

            return filas
                .Select(p => new ObraACargo(
                    p.ProjectId,
                    p.ProjectDescription.Trim(),
                    p.ResidenteWorkersId != null && ids.Contains(p.ResidenteWorkersId.Value),
                    p.WorkersCoordAdminId != null && ids.Contains(p.WorkersCoordAdminId.Value)))
                .OrderBy(o => o.Nombre, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }
}
