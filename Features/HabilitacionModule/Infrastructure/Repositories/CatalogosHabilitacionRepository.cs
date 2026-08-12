using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.Catalogos;
using Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Models;
using Abril_Backend.Shared.Services.AreaScope.Interfaces;
using Abril_Backend.Shared.Services.Revisores.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Repositories
{
    public class CatalogosHabilitacionRepository : ICatalogosHabilitacionRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IAreaScopeLegacyResolver _legacyResolver;
        private readonly IJefeRevisorResolver _revisorResolver;

        public CatalogosHabilitacionRepository(
            IDbContextFactory<AppDbContext> factory,
            IAreaScopeLegacyResolver legacyResolver,
            IJefeRevisorResolver revisorResolver)
        {
            _factory = factory;
            _legacyResolver = legacyResolver;
            _revisorResolver = revisorResolver;
        }

        /// <summary>
        /// Árbol de áreas para los desplegables del formulario de trabajadores, con la equivalencia
        /// legacy y el revisor ya resueltos por nodo. Una sola petición alimenta toda la cascada y
        /// el campo de revisor, sin ir al servidor cada vez que se cambia de área.
        /// </summary>
        public async Task<List<AreaArbolNodoDto>> GetAreaArbolAsync()
        {
            using var ctx = _factory.CreateDbContext();

            var nodos = await (
                from s in ctx.AreaScope.AsNoTracking()
                join ai in ctx.AreaItem.AsNoTracking() on s.AreaItemId equals ai.AreaItemId
                join at in ctx.AreaType.AsNoTracking() on ai.AreaTypeId equals at.AreaTypeId
                where s.State && ai.State
                orderby s.DisplayOrder, ai.AreaItemName
                select new AreaArbolNodoDto
                {
                    AreaScopeId = s.AreaScopeId,
                    AreaScopeParentId = s.AreaScopeParentId,
                    AreaItemName = ai.AreaItemName,
                    AreaTypeName = at.AreaTypeName,
                    DisplayOrder = s.DisplayOrder,
                }
            ).ToListAsync();

            if (nodos.Count == 0) return nodos;

            var ids = nodos.Select(n => n.AreaScopeId).ToList();
            var legacy = await _legacyResolver.ResolveTodosAsync();
            var revisores = await _revisorResolver.ResolveByAreaScopeManyAsync(ids);

            foreach (var nodo in nodos)
            {
                if (legacy.TryGetValue(nodo.AreaScopeId, out var eq))
                {
                    nodo.Area = eq.Area;
                    nodo.Subarea = eq.Subarea;
                    nodo.Jefatura = eq.Jefatura;
                }

                if (!revisores.TryGetValue(nodo.AreaScopeId, out var rev)) continue;

                nodo.RevisorNombre = rev.Area?.Nombre;
                nodo.RevisorEmail = rev.Area?.Email;
                nodo.RevisoresPorProyecto = rev.PorProyecto
                    .Select(kv => new AreaArbolRevisorProyectoDto
                    {
                        ProyectoId = kv.Key,
                        RevisorNombre = kv.Value.Nombre,
                        RevisorEmail = kv.Value.Email,
                    })
                    .ToList();
            }

            return nodos;
        }

        public async Task<List<SsItemTrabajador>> GetItemsTrabajadorAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsItemTrabajador
                .Where(x => x.Activo)
                .OrderBy(x => x.Orden)
                .ToListAsync();
        }

        public async Task<List<SsItemEmpresa>> GetItemsEmpresaAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsItemEmpresa
                .Where(x => x.Activo)
                .OrderBy(x => x.Orden)
                .ToListAsync();
        }

        public async Task<List<SsItemEquipo>> GetItemsEquipoAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsItemEquipo
                .Where(x => x.Activo)
                .OrderBy(x => x.Orden)
                .ToListAsync();
        }

        public async Task<List<SsCriterioEvaluacion>> GetCriteriosEvaluacionAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsCriterioEvaluacion
                .Where(x => x.Activo)
                .OrderBy(x => x.Orden)
                .ToListAsync();
        }

        public async Task<List<ObraOficinaStaffDto>> GetObraOficinaStaffAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.WorkersObraOficinaStaff
                .Where(x => x.State && x.Active)
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new ObraOficinaStaffDto
                {
                    ObraOficinaStaffId = x.WorkersObraOficinaStaffId,
                    Name = x.Name
                })
                .ToListAsync();
        }

        public async Task<List<string>> GetAreasAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.CatSubarea
                .Where(x => x.Activo)
                .Select(x => x.Area)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();
        }

        public async Task<List<CatSubarea>> GetSubareasAsync(string? area)
        {
            using var ctx = _factory.CreateDbContext();
            var query = ctx.CatSubarea.Where(x => x.Activo);
            if (!string.IsNullOrWhiteSpace(area))
                query = query.Where(x => x.Area == area);
            return await query
                .OrderBy(x => x.Subarea)
                .ToListAsync();
        }

        public async Task<List<Categoria>> GetCategoriasAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Categoria
                .Where(x => x.State && x.Active)
                .OrderBy(x => x.Nombre)
                .ToListAsync();
        }

        public async Task<List<Puesto>> GetPuestosAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Puesto
                .Where(x => x.State && x.Active)
                .OrderBy(x => x.Nombre)
                .ToListAsync();
        }

        // ── Categorías CRUD ──────────────────────────────────────────
        public async Task<List<Categoria>> GetCategoriasTodasAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Categoria
                .Where(x => x.State)
                .OrderBy(x => x.Nombre)
                .ToListAsync();
        }

        public async Task<Categoria> CrearCategoriaAsync(string nombre)
        {
            using var ctx = _factory.CreateDbContext();
            var nombreNorm = NormalizarNombre(nombre);
            if (await ctx.Categoria.AnyAsync(x => x.State && x.Nombre == nombreNorm))
                throw new AbrilException("Ya existe una categoría con ese nombre.", 400);

            var maxOrden = await ctx.Categoria.MaxAsync(x => (int?)x.Orden) ?? 0;
            var cat = new Categoria { Nombre = nombreNorm, Orden = maxOrden + 1, CreatedDateTime = DateTime.UtcNow };
            ctx.Categoria.Add(cat);
            await ctx.SaveChangesAsync();
            return cat;
        }

        public async Task<Categoria> ActualizarCategoriaAsync(int id, string nombre)
        {
            using var ctx = _factory.CreateDbContext();
            var cat = await ctx.Categoria.FirstOrDefaultAsync(x => x.CategoriaId == id && x.State)
                ?? throw new AbrilException("Categoría no encontrada.", 404);

            var nombreNorm = NormalizarNombre(nombre);
            if (await ctx.Categoria.AnyAsync(x => x.State && x.Nombre == nombreNorm && x.CategoriaId != id))
                throw new AbrilException("Ya existe una categoría con ese nombre.", 400);

            cat.Nombre = nombreNorm;
            cat.UpdatedDateTime = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
            return cat;
        }

        public async Task ToggleCategoriaAsync(int id, bool activo)
        {
            using var ctx = _factory.CreateDbContext();
            var cat = await ctx.Categoria.FirstOrDefaultAsync(x => x.CategoriaId == id && x.State)
                ?? throw new AbrilException("Categoría no encontrada.", 404);
            cat.Active = activo;
            cat.UpdatedDateTime = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
        }

        // ── Puestos CRUD ─────────────────────────────────────────────

        /// <summary>
        /// Puestos vivos con su categoría y el uso real (fichas de <c>workers</c> que
        /// apuntan al puesto) resueltos en una sola consulta: el conteo va como subconsulta
        /// correlacionada en vez de un segundo viaje a la base de datos.
        /// El uso se cuenta sobre TODAS las fichas del trabajador, sin filtrar por estado:
        /// <c>workers</c> no tiene soft delete, así que cada fila es un uso real del puesto.
        /// </summary>
        public async Task<List<PuestoAdminDto>> GetPuestosTodosAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Puesto
                .AsNoTracking()
                .Where(x => x.State)
                .OrderBy(x => x.Nombre)
                .Select(x => new PuestoAdminDto
                {
                    Id = x.PuestoId,
                    Nombre = x.Nombre,
                    CategoriaId = x.CategoriaId,
                    CategoriaNombre = x.Categoria == null ? null : x.Categoria.Nombre,
                    Orden = x.Orden,
                    Activo = x.Active,
                    CantidadTrabajadores = ctx.Worker.Count(w => w.PuestoId == x.PuestoId)
                })
                .ToListAsync();
        }

        /// <summary>
        /// Fichas de <c>workers</c> que apuntan al puesto, para el detalle que abre la fila
        /// de la tabla. Se listan con el mismo criterio con el que
        /// <see cref="GetPuestosTodosAsync"/> las cuenta (todas las fichas, sin filtrar por
        /// estado): así la lista siempre cuadra con el número que muestra la fila.
        ///
        /// El nombre se lee de <c>person.full_name</c>; se cae a <c>workers.apellido_nombre</c>
        /// solo si la ficha no tiene persona (la FK es nullable) o la persona no lo tiene.
        /// El join va por la navegación (LEFT JOIN) justamente para no perder esas fichas y
        /// desalinear el conteo.
        /// </summary>
        public async Task<List<PuestoTrabajadorDto>> GetTrabajadoresPorPuestoAsync(int puestoId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Worker
                .AsNoTracking()
                .Where(w => w.PuestoId == puestoId)
                .OrderBy(w => w.Person!.FullName ?? w.ApellidoNombre)
                .ThenBy(w => w.Id)
                .Select(w => new PuestoTrabajadorDto
                {
                    WorkerId = w.Id,
                    NombreCompleto = (w.Person!.FullName ?? w.ApellidoNombre) ?? "",
                    EmailCorporativo = w.EmailCorporativo
                })
                .ToListAsync();
        }

        public async Task<Puesto> CrearPuestoAsync(string nombre, int? categoriaId)
        {
            using var ctx = _factory.CreateDbContext();
            var nombreNorm = NormalizarNombre(nombre);
            if (await ctx.Puesto.AnyAsync(x => x.State && x.Nombre == nombreNorm))
                throw new AbrilException("Ya existe un puesto con ese nombre.", 400);
            await ValidarCategoriaAsync(ctx, categoriaId);

            var maxOrden = await ctx.Puesto.MaxAsync(x => (int?)x.Orden) ?? 0;
            var puesto = new Puesto
            {
                Nombre = nombreNorm,
                CategoriaId = categoriaId,
                Orden = maxOrden + 1,
                CreatedDateTime = DateTime.UtcNow
            };
            ctx.Puesto.Add(puesto);
            await ctx.SaveChangesAsync();
            return puesto;
        }

        public async Task<Puesto> ActualizarPuestoAsync(int id, string nombre, int? categoriaId)
        {
            using var ctx = _factory.CreateDbContext();
            var puesto = await ctx.Puesto.FirstOrDefaultAsync(x => x.PuestoId == id && x.State)
                ?? throw new AbrilException("Puesto no encontrado.", 404);

            var nombreNorm = NormalizarNombre(nombre);
            if (await ctx.Puesto.AnyAsync(x => x.State && x.Nombre == nombreNorm && x.PuestoId != id))
                throw new AbrilException("Ya existe un puesto con ese nombre.", 400);
            await ValidarCategoriaAsync(ctx, categoriaId);

            puesto.Nombre = nombreNorm;
            puesto.CategoriaId = categoriaId;
            puesto.UpdatedDateTime = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
            return puesto;
        }

        public async Task TogglePuestoAsync(int id, bool activo)
        {
            using var ctx = _factory.CreateDbContext();
            var puesto = await ctx.Puesto.FirstOrDefaultAsync(x => x.PuestoId == id && x.State)
                ?? throw new AbrilException("Puesto no encontrado.", 404);
            puesto.Active = activo;
            puesto.UpdatedDateTime = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
        }

        /// <summary>
        /// Soft delete del puesto: se marca <c>state = false</c> (y se desactiva, para que
        /// desaparezca de los desplegables aunque una consulta filtre solo por <c>active</c>).
        /// La fila se conserva para el histórico y el índice único de nombre solo aplica a
        /// los vivos, así que el nombre queda libre para volver a usarse.
        ///
        /// Un puesto en uso no se puede eliminar: las fichas que lo apuntan seguirían
        /// mostrándolo pero ya no existiría en el desplegable del formulario de trabajadores,
        /// y la siguiente edición de esas fichas lo perdería. Para sacarlo de circulación sin
        /// romper nada está "Desactivar".
        /// </summary>
        public async Task EliminarPuestoAsync(int id)
        {
            using var ctx = _factory.CreateDbContext();
            var puesto = await ctx.Puesto.FirstOrDefaultAsync(x => x.PuestoId == id && x.State)
                ?? throw new AbrilException("Puesto no encontrado.", 404);

            var enUso = await ctx.Worker.CountAsync(w => w.PuestoId == id);
            if (enUso > 0)
                throw new AbrilException(
                    $"No se puede eliminar: {enUso} trabajador(es) usan este puesto. " +
                    "Si solo quieres que deje de aparecer en los desplegables, desactívalo.", 400);

            puesto.State = false;
            puesto.Active = false;
            puesto.UpdatedDateTime = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
        }

        /// <summary>
        /// Soft delete en bloque de la selección de la tabla, en tres viajes a la base de
        /// datos para todo el lote (traer los puestos, ver cuáles están en uso, guardar).
        ///
        /// A diferencia del borrado de una sola fila, acá los puestos en uso se omiten en
        /// vez de tumbar el lote entero: la pantalla ya filtró la selección con el conteo
        /// que trajo, así que un puesto en uso solo puede aparecer si alguien lo asignó
        /// mientras tanto — y en ese caso lo correcto es eliminar el resto e informarlo.
        /// </summary>
        public async Task<PuestosEliminarResultDto> EliminarPuestosAsync(IReadOnlyCollection<int> ids)
        {
            if (ids.Count == 0)
                throw new AbrilException("No se recibió ningún puesto para eliminar.", 400);

            using var ctx = _factory.CreateDbContext();
            var puestos = await ctx.Puesto
                .Where(x => ids.Contains(x.PuestoId) && x.State)
                .ToListAsync();
            if (puestos.Count == 0)
                throw new AbrilException("Los puestos seleccionados ya no existen.", 404);

            var enUso = await ctx.Worker
                .Where(w => w.PuestoId != null && ids.Contains(w.PuestoId.Value))
                .Select(w => w.PuestoId!.Value)
                .Distinct()
                .ToListAsync();

            var now = DateTime.UtcNow;
            var eliminados = 0;
            foreach (var puesto in puestos)
            {
                if (enUso.Contains(puesto.PuestoId)) continue;
                puesto.State = false;
                puesto.Active = false;
                puesto.UpdatedDateTime = now;
                eliminados++;
            }

            if (eliminados > 0) await ctx.SaveChangesAsync();

            return new PuestosEliminarResultDto
            {
                Eliminados = eliminados,
                Omitidos = ids.Count - eliminados
            };
        }

        /// <summary>Categorías y puestos se guardan siempre en MAYÚSCULAS.</summary>
        private static string NormalizarNombre(string nombre) =>
            nombre.Trim().ToUpperInvariant();

        private static async Task ValidarCategoriaAsync(AppDbContext ctx, int? categoriaId)
        {
            if (categoriaId is null) return;
            if (!await ctx.Categoria.AnyAsync(c => c.CategoriaId == categoriaId && c.State))
                throw new AbrilException("La categoría indicada no existe.", 400);
        }
    }
}
