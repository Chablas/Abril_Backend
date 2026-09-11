using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Services.Jerarquia;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Lo que las dos pantallas de asignación por área (Revisores y Consolidadores) hacen igual:
    /// armar la fila de cada área, colgarle lo asignado —por área y por proyecto—, marcar cuáles
    /// filtran por proyecto, traer el catálogo de proyectos y el selector de personas, y validar el
    /// PUT.
    ///
    /// Lo ÚNICO que cada una pone de su lado es de qué tabla salen las asignaciones y quién queda
    /// efectivamente a cargo (un ganador en revisores, todos los activos en consolidadores).
    /// </summary>
    public static class AreaAsignacionArmador
    {
        private const string EmailDomainCorp = EstructuraAreaLoader.EmailDomainCorp;

        /// <summary>Una fila viva de la tabla de asignaciones, sin interpretar.</summary>
        public class AsignacionCruda
        {
            public int AreaScopeId { get; set; }
            /// <summary>NULL = a nivel de área; con valor = de ese proyecto dentro del área.</summary>
            public int? ProjectId { get; set; }
            public AreaAsignadoDto Asignado { get; set; } = new();
        }

        /// <summary>
        /// Una fila por área configurable: las gerencias primero (raíz de cada rama) y luego las
        /// áreas estándar, cada bloque en orden alfabético.
        /// </summary>
        public static List<AreaAsignacionItemDto> ArmarAreas(
            List<AreaAsignacionNodos.NodoArea> configurables,
            List<AreaAsignacionNodos.NodoArea> todos)
        {
            return configurables
                .OrderBy(n => Array.IndexOf(AreaAsignacionNodos.TiposConfigurables, n.AreaTypeName))
                .ThenBy(n => n.AreaItemName)
                .Select(n => new AreaAsignacionItemDto
                {
                    AreaScopeId = n.AreaScopeId,
                    AreaName = n.AreaItemName,
                    AreaTypeName = n.AreaTypeName,
                    ParentName = n.AreaScopeParentId != null
                        ? todos.FirstOrDefault(p => p.AreaScopeId == n.AreaScopeParentId)?.AreaItemName
                        : null,
                })
                .ToList();
        }

        /// <summary>Proyectos activos: el catálogo de las subfilas de un área filtrada por proyecto.</summary>
        public static async Task<List<AreaProyectoOptionDto>> ProyectosActivosAsync(AppDbContext ctx)
            => await (
                from pr in ctx.Project
                where pr.State && pr.Active
                orderby pr.ProjectDescription
                select new AreaProyectoOptionDto { ProjectId = pr.ProjectId, ProjectName = pr.ProjectDescription }
            ).ToListAsync();

        /// <summary>Qué áreas de la lista están marcadas como "filtrar por proyecto".</summary>
        public static async Task<Dictionary<int, bool>> FiltranPorProyectoAsync(
            AppDbContext ctx, List<int> areaIds)
        {
            var flags = await ctx.GaSalidasAreaConfig
                .Where(f => f.State && areaIds.Contains(f.AreaScopeId))
                .Select(f => new { f.AreaScopeId, f.FiltraPorProyecto })
                .ToListAsync();

            return flags
                .GroupBy(f => f.AreaScopeId)
                .ToDictionary(g => g.Key, g => g.First().FiltraPorProyecto);
        }

        /// <summary>Opciones del selector: cualquier trabajador con correo corporativo.</summary>
        public static async Task<List<AreaWorkerOptionDto>> OpcionesAsync(AppDbContext ctx)
            => await (
                from w in ctx.Worker
                where w.EmailCorporativo != null && w.EmailCorporativo.ToLower().Contains(EmailDomainCorp)
                join p in ctx.Person on w.PersonId equals p.PersonId
                where p.State == true
                orderby p.FullName
                select new AreaWorkerOptionDto
                {
                    WorkerId = w.Id,
                    FullName = p.FullName,
                    Email = w.EmailCorporativo,
                }
            ).ToListAsync();

        /// <summary>
        /// Cuelga de cada área lo asignado, la bandera de proyecto, sus subfilas de proyecto y
        /// quién queda efectivamente a cargo. Los efectivos los resuelve cada pantalla con su
        /// propio algoritmo y llegan acá como funciones.
        /// </summary>
        public static void Completar(
            List<AreaAsignacionItemDto> areas,
            List<AsignacionCruda> asignaciones,
            Dictionary<int, bool> filtranPorProyecto,
            List<AreaProyectoOptionDto> proyectos,
            Func<int, List<AreaEfectivoDto>> efectivosDeArea,
            Func<int, int, List<AreaEfectivoDto>> efectivosDeProyecto)
        {
            var porArea = asignaciones
                .Where(a => a.ProjectId == null)
                .GroupBy(a => a.AreaScopeId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Asignado).ToList());

            var porAreaProyecto = asignaciones
                .Where(a => a.ProjectId != null)
                .GroupBy(a => (a.AreaScopeId, ProjectId: a.ProjectId!.Value))
                .ToDictionary(g => g.Key, g => g.Select(x => x.Asignado).ToList());

            foreach (var a in areas)
            {
                if (porArea.TryGetValue(a.AreaScopeId, out var propios)) a.Asignados = propios;
                a.FiltraPorProyecto = filtranPorProyecto.TryGetValue(a.AreaScopeId, out var f) && f;
                a.Efectivos = efectivosDeArea(a.AreaScopeId);

                if (!a.FiltraPorProyecto) continue;

                // TODOS los proyectos activos, no solo los que tienen algo asignado: el algoritmo
                // también resuelve los que no tienen nada cargado.
                a.Proyectos = proyectos
                    .Select(pr => new AreaProyectoAsignacionesDto
                    {
                        ProjectId = pr.ProjectId,
                        ProjectName = pr.ProjectName,
                        Asignados = porAreaProyecto.TryGetValue((a.AreaScopeId, pr.ProjectId), out var rp)
                            ? rp
                            : new List<AreaAsignadoDto>(),
                        Efectivos = efectivosDeProyecto(a.AreaScopeId, pr.ProjectId),
                    })
                    .ToList();
            }
        }

        /// <summary>
        /// Validaciones del PUT, iguales en las dos pantallas: el área tiene que ser configurable,
        /// el proyecto (si viene) tiene que existir, no se puede asignar dos veces a la misma
        /// persona ni repetir prioridad, y todos tienen que tener correo corporativo.
        /// </summary>
        /// <param name="sustantivo">"revisor" o "consolidador", para que el mensaje se lea natural.</param>
        public static async Task ValidarAsync(
            AppDbContext ctx, int areaScopeId, int? projectId,
            List<AreaAsignacionInputDto>? asignados, string sustantivo)
        {
            var nodos = await AreaAsignacionNodos.LoadNodosAsync(ctx);
            if (AreaAsignacionNodos.Configurables(nodos).All(n => n.AreaScopeId != areaScopeId))
                throw new AbrilException(
                    $"El área no existe o no admite {sustantivo}es (solo áreas de tipo Área de Gerencia o Área Estándar).",
                    404);

            if (projectId != null)
            {
                var proyectoValido = await ctx.Project.AnyAsync(p => p.ProjectId == projectId.Value && p.State);
                if (!proyectoValido) throw new AbrilException("El proyecto no existe.", 404);
            }

            var deseados = asignados ?? new List<AreaAsignacionInputDto>();

            if (deseados.GroupBy(r => r.WorkerId).Any(g => g.Count() > 1))
                throw new AbrilException($"No se puede asignar dos veces al mismo {sustantivo}.", 400);

            if (deseados.Any(r => r.OrdenPrioridad < 1))
                throw new AbrilException("La prioridad debe ser 1 o mayor.", 400);

            if (deseados.GroupBy(r => r.OrdenPrioridad).Any(g => g.Count() > 1))
                throw new AbrilException($"No puede haber dos {sustantivo}es con la misma prioridad.", 400);

            if (deseados.Count == 0) return;

            var ids = deseados.Select(r => r.WorkerId).ToList();
            var validos = await ctx.Worker
                .Where(w => ids.Contains(w.Id)
                            && w.EmailCorporativo != null
                            && w.EmailCorporativo.Trim().ToLower().EndsWith(EmailDomainCorp))
                .Select(w => w.Id)
                .ToListAsync();

            if (ids.Except(validos).Any())
                throw new AbrilException(
                    $"Uno o más {sustantivo}es no existen o no tienen correo corporativo {EmailDomainCorp}.", 400);
        }
    }
}
