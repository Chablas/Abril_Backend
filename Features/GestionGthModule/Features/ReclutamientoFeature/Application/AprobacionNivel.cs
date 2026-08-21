namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application
{
    /// <summary>
    /// Nivel con el que un usuario entra a la pantalla «Aprobaciones». Sale de la CATEGORÍA de su
    /// ficha de trabajador (<c>workers.puesto_id → puesto.categoria_id</c>), no de su rol: el rol solo abre la
    /// pantalla; la categoría define qué solicitudes ve y con qué poder decide.
    ///
    /// No es un catálogo de base de datos: son los dos actores del flujo, fijos por diseño, y el
    /// tercer valor es la ausencia de ambos. Lo que sí vive en BD son las categorías a las que
    /// mapean (ver <see cref="Abril_Backend.Shared.Constants.CategoriaIds"/>).
    /// </summary>
    public static class AprobacionNivel
    {
        /// <summary>
        /// Gerencia General (<c>categoria_id</c> = GERENTE GENERAL). Ve TODAS las solicitudes y su
        /// decisión es la obligatoria: mueve las vacantes y dispara el correo a GTH.
        /// </summary>
        public const string GerenteGeneral = "GERENTE_GENERAL";

        /// <summary>
        /// Gerente de área (<c>categoria_id</c> = GERENTE). Ve y decide solo las solicitudes de su
        /// <c>area_scope</c> hacia abajo. Su decisión es un visto bueno: no mueve el flujo.
        /// </summary>
        public const string GerenteArea = "GERENTE_AREA";

        /// <summary>
        /// Cualquier otra categoría. Tiene acceso a la pantalla por su rol, pero no hay solicitudes
        /// bajo su alcance: la ve vacía y no puede decidir nada.
        /// </summary>
        public const string Ninguno = "NINGUNO";
    }
}
