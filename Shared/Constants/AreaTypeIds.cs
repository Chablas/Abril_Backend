namespace Abril_Backend.Shared.Constants
{
    /// <summary>
    /// IDs del catálogo <c>area_type</c> (<c>area_item.area_type_id</c>). Los ids son idénticos en
    /// dev y prod (verificado el 2026-09-10), así que se pueden usar como constantes — mismo
    /// criterio que <see cref="CategoriaIds"/>.
    ///
    /// Comparar por id y no por el NOMBRE del tipo: el nombre se edita desde Configuración → Áreas
    /// y lleva tilde ("Área de Gerencia"), así que una comparación por texto se rompe con un
    /// renombre o con una diferencia de codificación, y en silencio.
    /// </summary>
    public static class AreaTypeIds
    {
        /// <summary>
        /// "Área de Gerencia". El nodo de una gerencia: su revisor por algoritmo es el trabajador
        /// de categoría <see cref="CategoriaIds.Gerente"/>, no el Jefe.
        /// </summary>
        public const int AreaDeGerencia = 1;

        /// <summary>
        /// "Área Estándar". El área normal: su revisor por algoritmo es el trabajador de categoría
        /// <see cref="CategoriaIds.Jefe"/>.
        /// </summary>
        public const int AreaEstandar = 2;

        /// <summary>
        /// "Área Obra_Oficina". Dada de baja (active y state en false) cuando esa clasificación se
        /// normalizó a <c>workers_obra_oficina_staff</c>. No debería aparecer en data viva.
        /// </summary>
        public const int AreaObraOficina = 3;
    }
}
