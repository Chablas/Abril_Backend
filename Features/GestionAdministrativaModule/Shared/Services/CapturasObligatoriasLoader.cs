using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Resuelve qué áreas tienen las capturas de movilidad en OPCIONAL
    /// (<c>ga_salidas_area_config.capturas_obligatorias = false</c>), configurado en
    /// Gestión Administrativa → Configuración → Capturas.
    ///
    /// Loader estático sobre el contexto —igual que <see cref="ConsolidadoS10Loader"/>— porque la
    /// misma regla la evalúan tres lugares distintos (la lista de Solicitud de Salidas, la de
    /// Gestión de Salidas y el bloqueo del rendir en lote) y no puede divergir entre ellos.
    ///
    /// Dos decisiones que hacen que la funcionalidad no dependa de que alguien registre las áreas:
    /// <list type="bullet">
    ///   <item>El default es OBLIGATORIO: un área sin fila en <c>ga_salidas_area_config</c> —el
    ///   caso de toda área recién creada— exige capturas. Solo marcarla como opcional escribe en BD.</item>
    ///   <item>Cada nodo es INDEPENDIENTE: no se hereda nada por el árbol de <c>area_scope</c>, así
    ///   que "Unidad de Proyectos" puede ser opcional e "Ingeniería BIM" (su hija) obligatoria.</item>
    /// </list>
    /// </summary>
    public static class CapturasObligatoriasLoader
    {
        /// <summary>
        /// area_scope_id de las áreas con capturas OPCIONALES. Devuelve solo las excepciones (que
        /// son pocas), así que el consumidor pregunta <c>Contains(areaScopeId)</c> y cualquier área
        /// que no esté —incluida la de un trabajador sin área— queda como obligatoria.
        /// </summary>
        public static async Task<HashSet<int>> LoadAreasOpcionalesAsync(AppDbContext ctx)
        {
            var ids = await ctx.GaSalidasAreaConfig
                .Where(c => c.State && !c.CapturasObligatorias)
                .Select(c => c.AreaScopeId)
                .ToListAsync();

            return ids.ToHashSet();
        }

        /// <summary>
        /// true si el trabajador de esa área puede rendir sin subir capturas. Sin área
        /// (puesto sin área de destino) se exigen capturas: es el comportamiento previo.
        /// </summary>
        public static bool CapturasOpcionales(HashSet<int> areasOpcionales, int? areaScopeId)
            => areaScopeId.HasValue && areasOpcionales.Contains(areaScopeId.Value);

        /// <summary>
        /// Igual que <see cref="CapturasOpcionales"/> pero para UNA sola área (la del usuario que
        /// mira su propia lista): pregunta con un EXISTS por el índice en vez de traerse el conjunto.
        /// </summary>
        public static async Task<bool> SonOpcionalesAsync(AppDbContext ctx, int? areaScopeId)
        {
            if (!areaScopeId.HasValue) return false;

            return await ctx.GaSalidasAreaConfig
                .AnyAsync(c => c.State && c.AreaScopeId == areaScopeId.Value && !c.CapturasObligatorias);
        }
    }
}
