using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.RevisoresAreas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.RevisoresAreas.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Models;
using Abril_Backend.Shared.Services.Actores.Interfaces;
using Abril_Backend.Shared.Services.Jerarquia;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.RevisoresAreas.Infrastructure.Repositories
{
    /// <summary>
    /// Lectura y escritura de Gestión Administrativa → Configuración → Revisores de Áreas: lo
    /// personalizado por área (<c>area_actor_asignacion</c>) para los cinco actores y los cinco casos.
    ///
    /// Reemplaza a las tres pantallas que decían lo mismo por separado (Revisores de Áreas de
    /// Solicitud de Salidas y de Mis Rendiciones, y Consolidadores de Consolidados). Lo que la
    /// pantalla MUESTRA —quién le toca hoy a cada fila y de dónde sale— lo resuelve
    /// <see cref="IActoresResolver"/>, el mismo que decide a quién se le manda cada correo: la
    /// pantalla no puede mostrar a alguien distinto de quien va a actuar.
    ///
    /// Visibilidad: ADMINISTRADOR DE SOLICITUD DE SALIDAS y USUARIO DE GTH ven todas las áreas y las
    /// editan; una jefatura (<c>CategoriaIds.ConVistaDeSuArea</c>) ve solo su área, sin editar.
    /// </summary>
    public class RevisoresAreasRepository : IRevisoresAreasRepository
    {
        private const string EmailDomainCorp = EstructuraAreaLoader.EmailDomainCorp;

        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IActoresResolver _actores;

        public RevisoresAreasRepository(IDbContextFactory<AppDbContext> factory, IActoresResolver actores)
        {
            _factory = factory;
            _actores = actores;
        }

        // ══ Carga inicial ════════════════════════════════════════════════════

        public async Task<RevisoresAreasInicialDto> GetInitialDataAsync(int userId, bool verTodas)
        {
            using var ctx = _factory.CreateDbContext();

            var nodos = await AreaAsignacionNodos.LoadNodosAsync(ctx);
            var elegibles = await ElegiblesAsync(ctx, userId, verTodas, nodos);

            var (actores, casos) = await CatalogosAsync(ctx);
            var resultado = new RevisoresAreasInicialDto
            {
                Actores = actores,
                Casos = casos,
                PuedeEditar = verTodas,
            };
            if (elegibles.Count == 0) return resultado;

            var padres = nodos.ToDictionary(n => n.AreaScopeId);
            var areas = elegibles
                .OrderBy(n => Array.IndexOf(AreaAsignacionNodos.TiposConfigurables, n.AreaTypeName))
                .ThenBy(n => n.AreaItemName)
                .Select(n => new RevisoresAreaFilaDto
                {
                    AreaScopeId  = n.AreaScopeId,
                    AreaName     = n.AreaItemName,
                    AreaTypeName = n.AreaTypeName,
                    ParentName   = n.AreaScopeParentId != null && padres.TryGetValue(n.AreaScopeParentId.Value, out var p)
                                       ? p.AreaItemName : null,
                    EsGerencia   = n.AreaTypeName == AreaAsignacionNodos.AreaTypeGerencia,
                    CasoId       = ActorCasoIds.OficinaCentral,
                })
                .ToList();

            // ── Qué áreas se parten por obra (deducido) ──────────────────────
            var areaIds = areas.Select(a => a.AreaScopeId).ToList();
            var ubicaciones = await UbicacionesPorAreaAsync(ctx, nodos, areaIds);

            var conObraAsignada = (await ctx.AreaActorAsignacion.AsNoTracking()
                    .Where(a => a.State && a.ProjectId != null && areaIds.Contains(a.AreaScopeId))
                    .Select(a => new { a.AreaScopeId, ProjectId = a.ProjectId!.Value })
                    .Distinct()
                    .ToListAsync())
                .ToLookup(a => a.AreaScopeId, a => a.ProjectId);

            var obras = (await ObrasLoader.Obras(ctx).Select(p => p.ProjectId).ToListAsync()).ToHashSet();

            // Las subfilas: todas las obras activas, como cuando se marcaba la casilla, más las
            // ubicaciones donde el área tiene gente o algo personalizado aunque el proyecto ya no
            // esté activo (si no, esa configuración quedaría sin dónde verse).
            var extras = ubicaciones.Values.SelectMany(u => u)
                .Concat(conObraAsignada.SelectMany(g => g))
                .Distinct()
                .ToList();
            var proyectos = await ctx.Project.AsNoTracking()
                .Where(p => p.State && (p.Active || extras.Contains(p.ProjectId)))
                .Select(p => new { p.ProjectId, p.ProjectDescription, p.Active })
                .ToListAsync();

            foreach (var area in areas.Where(a => !a.EsGerencia))
            {
                var suyas = ubicaciones.GetValueOrDefault(area.AreaScopeId) ?? new HashSet<int>();
                var asignadas = conObraAsignada[area.AreaScopeId].ToHashSet();

                // Apenas su gente trabaja en dos ubicaciones —o en alguna obra, donde los actores son
                // otros que en oficina—, el área se ve partida por obra. Lo personalizado para una obra
                // también la parte: si no, no habría dónde verlo.
                area.FiltraPorProyecto = suyas.Count >= 2 || suyas.Any(obras.Contains) || asignadas.Count > 0;
                if (!area.FiltraPorProyecto) continue;

                area.Proyectos = proyectos
                    .Where(p => p.Active || suyas.Contains(p.ProjectId) || asignadas.Contains(p.ProjectId))
                    .Select(p => new RevisoresAreaProyectoDto
                    {
                        ProjectId   = p.ProjectId,
                        ProjectName = (p.ProjectDescription ?? string.Empty).Trim(),
                        EsObra      = obras.Contains(p.ProjectId),
                        CasoId      = obras.Contains(p.ProjectId) ? ActorCasoIds.Staff : ActorCasoIds.OficinaCentral,
                    })
                    // OFICINA CENTRAL primero (es la ubicación que no es obra) y después las obras.
                    .OrderBy(p => p.EsObra)
                    .ThenBy(p => p.ProjectName, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }

            // ── Quién le toca hoy a un trabajador normal de cada fila ──────────
            var filas = areas
                .Select(a => new FilaPrevisualizacion(a.AreaScopeId, null, new[] { a.CasoId }))
                .Concat(areas.SelectMany(a => a.Proyectos.Select(
                    p => new FilaPrevisualizacion(a.AreaScopeId, p.ProjectId, new[] { p.CasoId }))))
                .ToList();

            var preview = await _actores.PrevisualizarAsync(filas);
            var categorias = await CategoriasAsync(ctx, preview.Values
                .SelectMany(c => c.Values).SelectMany(a => a.Values).SelectMany(r => r.Personas));

            foreach (var area in areas)
            {
                area.Actores = Celdas(preview, area.AreaScopeId, null, area.CasoId, categorias);
                foreach (var p in area.Proyectos)
                    p.Actores = Celdas(preview, area.AreaScopeId, p.ProjectId, p.CasoId, categorias);
            }

            resultado.Areas = areas;

            // Solo quien edita necesita el selector.
            if (verTodas)
                resultado.Options = await (
                    from w in ctx.Worker.AsNoTracking()
                    where w.EmailCorporativo != null
                          && w.EmailCorporativo.Trim().ToLower().EndsWith(EmailDomainCorp)
                    join per in ctx.Person.AsNoTracking() on w.PersonId equals per.PersonId
                    where per.State == true
                    orderby per.FullName
                    select new PersonaOpcionDto { WorkerId = w.Id, FullName = per.FullName, Email = w.EmailCorporativo }
                ).ToListAsync();

            return resultado;
        }

        // ══ Detalle de una fila ══════════════════════════════════════════════

        public async Task<RevisoresAreaDetalleDto> GetDetalleAsync(
            int userId, bool verTodas, int areaScopeId, int? projectId)
        {
            using var ctx = _factory.CreateDbContext();

            var nodos = await AreaAsignacionNodos.LoadNodosAsync(ctx);
            var nodo = (await ElegiblesAsync(ctx, userId, verTodas, nodos))
                .FirstOrDefault(n => n.AreaScopeId == areaScopeId)
                ?? throw new AbrilException("El área no existe o no la puedes ver.", 404);

            var (fila, casos) = await DescribirFilaAsync(ctx, nodo, projectId);
            var (_, catalogoCasos) = await CatalogosAsync(ctx);
            var nombreCaso = catalogoCasos.ToDictionary(c => c.Id, c => c.Nombre);

            var preview = await _actores.PrevisualizarAsync(
                new[] { new FilaPrevisualizacion(areaScopeId, projectId, casos) });

            // Lo personalizado EXACTAMENTE en esta fila (vivo, activo o no), con sus datos.
            var asignados = await (
                from a in ctx.AreaActorAsignacion.AsNoTracking()
                where a.State && a.AreaScopeId == areaScopeId && a.ProjectId == projectId
                join w in ctx.Worker.AsNoTracking() on a.WorkerId equals w.Id
                orderby a.OrdenPrioridad, a.AreaActorAsignacionId
                select new
                {
                    a.GaActorCasoId,
                    a.GaActorId,
                    Dto = new AsignadoDto
                    {
                        WorkerId       = a.WorkerId,
                        FullName       = w.Person != null ? w.Person.FullName : null,
                        Email          = w.EmailCorporativo,
                        Categoria      = w.PuestoCatalogo != null && w.PuestoCatalogo.Categoria != null
                                             ? w.PuestoCatalogo.Categoria.Nombre : null,
                        OrdenPrioridad = a.OrdenPrioridad,
                        Active         = a.Active,
                    },
                }
            ).ToListAsync();

            var categorias = await CategoriasAsync(ctx, preview.Values
                .SelectMany(c => c.Values).SelectMany(a => a.Values)
                .SelectMany(r => r.Personas.Concat(r.SinPersonalizar?.Personas ?? new List<ActorPersona>())));

            fila.Casos = casos
                .Select(caso => new RevisoresAreaCasoDto
                {
                    CasoId     = caso,
                    CasoNombre = nombreCaso.GetValueOrDefault(caso) ?? string.Empty,
                    Actores    = ActorIds.Todos.Select(actorId =>
                    {
                        var r = preview[(areaScopeId, projectId)][caso][actorId];
                        var celda = new ActorCeldaDetalleDto
                        {
                            ActorId    = actorId,
                            Aplica     = r.Aplica,
                            Personas   = Personas(r, categorias),
                            Descriptor = r.Descriptor,
                            Origen     = OrigenDeFila(r, areaScopeId, projectId),
                            Editable   = Aplica(actorId, caso),
                            Asignados  = asignados
                                .Where(a => a.GaActorCasoId == caso && a.GaActorId == actorId)
                                .Select(a => a.Dto)
                                .ToList(),
                            SinPersonalizar = r.SinPersonalizar == null ? null : new ActorCeldaDto
                            {
                                ActorId    = actorId,
                                Aplica     = r.SinPersonalizar.Aplica,
                                Personas   = Personas(r.SinPersonalizar, categorias),
                                Descriptor = r.SinPersonalizar.Descriptor,
                                Origen     = OrigenDeFila(r.SinPersonalizar, areaScopeId, projectId),
                            },
                        };
                        return celda;
                    }).ToList(),
                })
                .ToList();

            return fila;
        }

        // ══ Guardar una fila ═════════════════════════════════════════════════

        public async Task GuardarAsync(int areaScopeId, RevisoresAreaGuardarDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var nodos = await AreaAsignacionNodos.LoadNodosAsync(ctx);
            var nodo = AreaAsignacionNodos.Configurables(nodos).FirstOrDefault(n => n.AreaScopeId == areaScopeId)
                ?? throw new AbrilException(
                    "El área no existe o no admite configuración (solo áreas de tipo Área de Gerencia o Área Estándar).", 404);

            var projectId = dto?.ProjectId;
            var (_, casosDeLaFila) = await DescribirFilaAsync(ctx, nodo, projectId);
            var celdas = dto?.Celdas ?? new List<CeldaGuardarDto>();

            // ── Validaciones ────────────────────────────────────────────────
            foreach (var celda in celdas)
            {
                if (!ActorIds.Todos.Contains(celda.ActorId))
                    throw new AbrilException("Uno de los actores no existe.", 400);
                if (!casosDeLaFila.Contains(celda.CasoId))
                    throw new AbrilException("Uno de los tipos de trabajador no corresponde a esta fila.", 400);
                if (!Aplica(celda.ActorId, celda.CasoId) && (celda.Asignados?.Count ?? 0) > 0)
                    throw new AbrilException("El jefe notificado solo se asigna para el personal de staff.", 400);
                if ((celda.Asignados ?? new List<AsignadoInputDto>()).GroupBy(a => a.WorkerId).Any(g => g.Count() > 1))
                    throw new AbrilException("No se puede asignar dos veces a la misma persona en un mismo actor.", 400);
            }

            if (celdas.GroupBy(c => (c.CasoId, c.ActorId)).Any(g => g.Count() > 1))
                throw new AbrilException("Una misma celda vino dos veces.", 400);

            var elegidos = celdas.SelectMany(c => c.Asignados ?? new List<AsignadoInputDto>())
                .Select(a => a.WorkerId)
                .Distinct()
                .ToList();
            if (elegidos.Count > 0)
            {
                var validos = await ctx.Worker.AsNoTracking()
                    .Where(w => elegidos.Contains(w.Id)
                                && w.EmailCorporativo != null
                                && w.EmailCorporativo.Trim().ToLower().EndsWith(EmailDomainCorp))
                    .Select(w => w.Id)
                    .ToListAsync();
                if (elegidos.Except(validos).Any())
                    throw new AbrilException(
                        $"Una o más personas no existen o no tienen correo corporativo {EmailDomainCorp}.", 400);
            }

            // ── Diff contra lo vivo de la fila ───────────────────────────────
            var now = DateTimeOffset.UtcNow;
            var vivas = await ctx.AreaActorAsignacion
                .Where(a => a.State && a.AreaScopeId == areaScopeId && a.ProjectId == projectId)
                .ToListAsync();

            foreach (var celda in celdas)
            {
                var deseados = celda.Asignados ?? new List<AsignadoInputDto>();
                var actuales = vivas
                    .Where(a => a.GaActorCasoId == celda.CasoId && a.GaActorId == celda.ActorId)
                    .ToList();

                foreach (var fila in actuales.Where(a => deseados.All(d => d.WorkerId != a.WorkerId)))
                {
                    fila.State = false;
                    fila.UpdatedAt = now;
                }

                for (var i = 0; i < deseados.Count; i++)
                {
                    var d = deseados[i];
                    var orden = i + 1;
                    var fila = actuales.FirstOrDefault(a => a.WorkerId == d.WorkerId);

                    if (fila == null)
                    {
                        ctx.AreaActorAsignacion.Add(new AreaActorAsignacion
                        {
                            AreaScopeId    = areaScopeId,
                            ProjectId      = projectId,
                            GaActorId      = celda.ActorId,
                            GaActorCasoId  = celda.CasoId,
                            WorkerId       = d.WorkerId,
                            OrdenPrioridad = orden,
                            Active         = d.Active,
                            State          = true,
                            CreatedAt      = now,
                        });
                    }
                    else if (fila.OrdenPrioridad != orden || fila.Active != d.Active)
                    {
                        fila.OrdenPrioridad = orden;
                        fila.Active = d.Active;
                        fila.UpdatedAt = now;
                    }
                }
            }

            await ctx.SaveChangesAsync();
        }

        // ══ Helpers ═════════════════════════════════════════════════════════

        /// <summary>
        /// Los nodos que el usuario ve: todos los configurables si administra la pantalla; si no,
        /// solo el área de su jefatura (o ninguno).
        /// </summary>
        private static async Task<List<AreaAsignacionNodos.NodoArea>> ElegiblesAsync(
            AppDbContext ctx, int userId, bool verTodas, List<AreaAsignacionNodos.NodoArea> nodos)
        {
            var elegibles = AreaAsignacionNodos.Configurables(nodos);
            if (verTodas) return elegibles;

            var areaVisible = await AreaAsignacionNodos.AreaVisibleDelUsuarioAsync(ctx, userId, nodos, elegibles);
            return areaVisible == null
                ? new List<AreaAsignacionNodos.NodoArea>()
                : elegibles.Where(n => n.AreaScopeId == areaVisible.Value).ToList();
        }

        /// <summary>
        /// La cabecera del detalle de una fila y los casos que muestra: la fila del área, todos; la de
        /// una obra, los que pueden estar en una obra (no oficina central); la de OFICINA CENTRAL, los
        /// que no son staff. Una gerencia no se parte por obra.
        /// </summary>
        private static async Task<(RevisoresAreaDetalleDto Fila, int[] Casos)> DescribirFilaAsync(
            AppDbContext ctx, AreaAsignacionNodos.NodoArea nodo, int? projectId)
        {
            var fila = new RevisoresAreaDetalleDto
            {
                AreaScopeId = nodo.AreaScopeId,
                AreaName    = nodo.AreaItemName,
                EsGerencia  = nodo.AreaTypeName == AreaAsignacionNodos.AreaTypeGerencia,
                ProjectId   = projectId,
            };

            if (projectId == null) return (fila, ActorCasoIds.Todos);

            if (fila.EsGerencia)
                throw new AbrilException("Una gerencia no se configura por obra.", 400);

            var proyecto = await ctx.Project.AsNoTracking()
                .Where(p => p.ProjectId == projectId.Value && p.State)
                .Select(p => new { p.ProjectDescription })
                .FirstOrDefaultAsync()
                ?? throw new AbrilException("El proyecto no existe.", 404);

            fila.ProjectName = (proyecto.ProjectDescription ?? string.Empty).Trim();
            fila.EsObra = await ObrasLoader.Obras(ctx).AnyAsync(p => p.ProjectId == projectId.Value);

            return (fila, fila.EsObra
                ? new[] { ActorCasoIds.Staff, ActorCasoIds.Jefe, ActorCasoIds.Residente, ActorCasoIds.Subgerente }
                : new[] { ActorCasoIds.OficinaCentral, ActorCasoIds.Jefe, ActorCasoIds.Residente, ActorCasoIds.Subgerente });
        }

        /// <summary>El jefe notificado solo existe para el staff; el resto de los actores, para todos.</summary>
        private static bool Aplica(int actorId, int casoId)
            => actorId != ActorIds.JefeNotificado || casoId == ActorCasoIds.Staff;

        /// <summary>
        /// Para cada área, las obras vigentes de la gente de su rama (el nodo y todo su subárbol):
        /// fichas vivas de gente que está adentro, por su puesto.
        /// </summary>
        private static async Task<Dictionary<int, HashSet<int>>> UbicacionesPorAreaAsync(
            AppDbContext ctx, List<AreaAsignacionNodos.NodoArea> nodos, List<int> areaIds)
        {
            var trabajadores = await ctx.Worker.AsNoTracking()
                .Where(w => w.State
                         && WorkersEstadoIds.EstanAdentro.Contains(w.WorkersEstadoId)
                         && w.PuestoCatalogo != null
                         && w.PuestoCatalogo.AreaDestinoScopeId != null)
                .Select(w => new { w.Id, Area = w.PuestoCatalogo!.AreaDestinoScopeId!.Value })
                .ToListAsync();

            var obraDe = await ObrasLoader.ObraVigentePorTrabajadorAsync(ctx, trabajadores.Select(t => t.Id).ToList());

            var proyectosPorNodo = trabajadores
                .Where(t => obraDe.TryGetValue(t.Id, out var p) && p != null)
                .GroupBy(t => t.Area)
                .ToDictionary(g => g.Key, g => g.Select(t => obraDe[t.Id]!.Value).ToHashSet());

            var hijos = nodos
                .Where(n => n.AreaScopeParentId != null)
                .GroupBy(n => n.AreaScopeParentId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(n => n.AreaScopeId).ToList());

            var resultado = new Dictionary<int, HashSet<int>>();
            foreach (var areaId in areaIds)
            {
                var ubicaciones = new HashSet<int>();
                var pendientes = new Stack<int>(new[] { areaId });
                var vistos = new HashSet<int>();
                while (pendientes.Count > 0)
                {
                    var actual = pendientes.Pop();
                    if (!vistos.Add(actual)) continue;
                    if (proyectosPorNodo.TryGetValue(actual, out var suyos)) ubicaciones.UnionWith(suyos);
                    if (hijos.TryGetValue(actual, out var deEste))
                        foreach (var h in deEste) pendientes.Push(h);
                }
                resultado[areaId] = ubicaciones;
            }
            return resultado;
        }

        private static async Task<(List<CatalogoActorDto> Actores, List<CatalogoActorDto> Casos)> CatalogosAsync(AppDbContext ctx)
        {
            var actores = await ctx.GaActor.AsNoTracking()
                .Where(a => a.State)
                .OrderBy(a => a.DisplayOrder)
                .Select(a => new CatalogoActorDto { Id = a.GaActorId, Nombre = a.Nombre, Multiple = a.Multiple })
                .ToListAsync();

            var casos = await ctx.GaActorCaso.AsNoTracking()
                .Where(c => c.State)
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new CatalogoActorDto { Id = c.GaActorCasoId, Nombre = c.Nombre })
                .ToListAsync();

            return (actores, casos);
        }

        /// <summary>El nombre de la categoría de cada persona resuelta, en una consulta.</summary>
        private static async Task<Dictionary<int, string>> CategoriasAsync(
            AppDbContext ctx, IEnumerable<ActorPersona> personas)
        {
            var ids = personas.Where(p => p.CategoriaId != null).Select(p => p.CategoriaId!.Value).Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<int, string>();

            return await ctx.Categoria.AsNoTracking()
                .Where(c => ids.Contains(c.CategoriaId))
                .ToDictionaryAsync(c => c.CategoriaId, c => c.Nombre);
        }

        private static List<ActorCeldaDto> Celdas(
            Dictionary<(int Nodo, int? Proyecto), Dictionary<int, Dictionary<int, ActorResultado>>> preview,
            int nodo, int? proyecto, int caso, Dictionary<int, string> categorias)
        {
            if (!preview.TryGetValue((nodo, proyecto), out var porCaso) || !porCaso.TryGetValue(caso, out var porActor))
                return new List<ActorCeldaDto>();

            return ActorIds.Todos
                .Where(porActor.ContainsKey)
                .Select(actorId =>
                {
                    var r = porActor[actorId];
                    return new ActorCeldaDto
                    {
                        ActorId    = actorId,
                        Aplica     = r.Aplica,
                        Personas   = Personas(r, categorias),
                        Descriptor = r.Descriptor,
                        Origen     = OrigenDeFila(r, nodo, proyecto),
                    };
                })
                .ToList();
        }

        private static List<ActorPersonaDto> Personas(ActorResultado r, Dictionary<int, string> categorias)
            => r.Personas.Select(p => new ActorPersonaDto
            {
                WorkerId  = p.WorkerId,
                Nombre    = p.Nombre,
                Email     = p.Email,
                Categoria = p.CategoriaId != null ? categorias.GetValueOrDefault(p.CategoriaId.Value) : null,
            }).ToList();

        /// <summary>
        /// De dónde sale el valor, visto desde ESTA fila: solo es personalizado lo que se asignó en
        /// este nodo. Lo asignado más arriba del árbol le llega porque el sistema subió a buscarlo, así
        /// que para esta fila es algoritmo. En una subfila de obra, lo que hereda de la fila de su
        /// área se distingue de lo asignado para la obra misma.
        /// </summary>
        private static string OrigenDeFila(ActorResultado r, int nodo, int? proyecto) => r.Origen switch
        {
            ActorOrigen.Area when r.NodoOrigen == nodo
                => proyecto != null && r.ProyectoOrigen == null ? "PersonalizadoArea" : "Personalizado",
            ActorOrigen.Trabajador => "Personalizado",
            ActorOrigen.Gth        => "Gth",
            _                      => "Algoritmo",
        };
    }
}
