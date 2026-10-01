using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
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
        /// Una obra de la que una persona está a cargo, y en qué papel. Puede ser las dos cosas a la
        /// vez, aunque en la práctica no pasa.
        /// </summary>
        public sealed record ObraACargo(int ProjectId, string Nombre, bool EsResidente, bool EsAdministrador);

        /// <summary>
        /// Los proyectos vivos que son OBRAS: los de tipo PROYECTO (edificios que se venden al
        /// público). OFICINA CENTRAL, las áreas internas registradas como proyecto (Post Venta,
        /// Arquitectura Comercial, Eventos), la FFT y el proyecto de prueba NO son obra: su gente es de
        /// oficina central (ver <see cref="ObraVigentePorTrabajadorAsync"/>). Hasta el 2026-09-29 se
        /// excluía solo OFICINA CENTRAL, por nombre, y a la gente de Post Venta y Arquitectura
        /// Comercial la revisaba y consolidaba el «administrador de obra» de esas áreas.
        ///
        /// No es <c>project_tipo.es_obra</c>, que también marca FFT y Prueba: esa bandera la usan otras
        /// pantallas, donde esos dos sí cuentan como obra.
        ///
        /// Va sin <c>AsNoTracking</c> para poder usarse también como subconsulta dentro de otra
        /// consulta (así lo usa la visibilidad); quienes la consumen proyectan, así que no se
        /// rastrea nada igual.
        /// </summary>
        public static IQueryable<Project> Obras(AppDbContext ctx) =>
            ctx.Project.Where(p => p.State && p.ProjectTipoId == ProjectTipoIds.Proyecto);

        /// <summary>
        /// El proyecto OFICINA CENTRAL (tipo OFICINA_CENTRAL; si hubiera más de uno, el activo de id
        /// más bajo), o null si no existe.
        /// </summary>
        public static Task<int?> OficinaCentralAsync(AppDbContext ctx) =>
            ctx.Project.AsNoTracking()
                .Where(p => p.State && p.ProjectTipoId == ProjectTipoIds.OficinaCentral)
                .OrderByDescending(p => p.Active)
                .ThenBy(p => p.ProjectId)
                .Select(p => (int?)p.ProjectId)
                .FirstOrDefaultAsync();

        /// <summary>
        /// La ubicación vigente de cada trabajador: su vinculación con <c>fecha_fin</c> NULL y
        /// proyecto, la más reciente por <c>created_at</c> y después por id (criterio de
        /// <c>HabTrabajadorRepository.LatestVincActiva</c>, que es lo que mantiene GTH con
        /// "Cambiar obra / puesto de trabajo"). Si ese proyecto no es una obra (<see cref="Obras"/>:
        /// un área interna, la FFT, el de prueba...), se lo ubica en OFICINA CENTRAL, como si su
        /// vinculación dijera eso: así le toca lo personalizado para oficina central y el área no se
        /// parte por un «proyecto» que no lo es. Un trabajador retirado no tiene vinculación vigente
        /// y no aparece en el diccionario.
        /// </summary>
        public static async Task<Dictionary<int, int?>> ObraVigentePorTrabajadorAsync(
            AppDbContext ctx, IReadOnlyCollection<int> workerIds)
        {
            var ids = workerIds as List<int> ?? workerIds.ToList();
            if (ids.Count == 0) return new Dictionary<int, int?>();

            var vinculaciones = await (
                from v in ctx.WorkerVinculacion.AsNoTracking()
                where ids.Contains(v.WorkerId) && v.FechaFin == null && v.ProyectoId != null
                join p in ctx.Project.AsNoTracking() on v.ProyectoId equals p.ProjectId into pj
                from p in pj.DefaultIfEmpty()
                orderby v.CreatedAt descending, v.Id descending
                select new
                {
                    v.WorkerId,
                    v.ProyectoId,
                    EsObra = p != null && p.State && p.ProjectTipoId == ProjectTipoIds.Proyecto,
                }
            ).ToListAsync();

            var vigentes = vinculaciones
                .GroupBy(v => v.WorkerId)
                .ToDictionary(g => g.Key, g => g.First());

            var oficinaCentral = vigentes.Values.Any(v => !v.EsObra) ? await OficinaCentralAsync(ctx) : null;

            return vigentes.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.EsObra ? kv.Value.ProyectoId : oficinaCentral ?? kv.Value.ProyectoId);
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
