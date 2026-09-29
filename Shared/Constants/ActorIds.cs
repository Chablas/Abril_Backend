namespace Abril_Backend.Shared.Constants
{
    /// <summary>
    /// IDs del catálogo <c>ga_actor</c>: los cinco papeles que alguien cumple sobre el ciclo de una
    /// salida de un trabajador, desde que la pide hasta que su consolidado queda firmado. Se insertan
    /// con id explícito en <c>Migrations/Manual/20260925_GaActoresUnificados.sql</c>, así que son
    /// idénticos en dev y prod.
    ///
    /// Los cinco se resuelven con el MISMO recorrido (<c>IActoresResolver</c>): lo personalizado
    /// para el trabajador, lo personalizado para su área (Gestión Administrativa → Configuración →
    /// Revisores de Áreas) y, si no hay nada, el algoritmo.
    /// </summary>
    public static class ActorIds
    {
        /// <summary>Aprueba o rechaza la salida (Gestión de Salidas). Uno solo: el primero activo.</summary>
        public const int AprobadorSalida = 1;

        /// <summary>
        /// Se entera de la salida sin poder aprobarla. Solo existe para el personal de staff (la
        /// aprueba su residente y su jefe recibe el aviso). Uno solo.
        /// </summary>
        public const int JefeNotificado = 2;

        /// <summary>Aprueba la primera revisión de la planilla (Gestión de Rendiciones). Uno solo.</summary>
        public const int AprobadorPrimeraRevision = 3;

        /// <summary>
        /// Arma la planilla grupal y sube el Consolidado del S10. Varios: cualquiera de ellos puede
        /// empezar, pero el trámite de cada planilla grupal lo sigue solo quien la preparó.
        /// </summary>
        public const int Consolidador = 4;

        /// <summary>
        /// Firman el consolidado (Consolidados). Varios y en ORDEN: en obra, el administrador de obra
        /// y después el residente.
        /// </summary>
        public const int AprobadorConsolidado = 5;

        /// <summary>Los cinco, en el orden del flujo.</summary>
        public static readonly int[] Todos =
            { AprobadorSalida, JefeNotificado, AprobadorPrimeraRevision, Consolidador, AprobadorConsolidado };

        /// <summary>
        /// true = cuentan TODOS los activos de la lista (consolidadores y firmantes del consolidado);
        /// false = cuenta solo el primero activo y el resto queda de respaldo.
        /// </summary>
        public static bool EsMultiple(int actorId) => actorId is Consolidador or AprobadorConsolidado;

        /// <summary>Los que caen en el buzón de GTH cuando nadie resuelve: los que deciden algo.</summary>
        public static bool TieneRespaldoGth(int actorId) =>
            actorId is AprobadorSalida or AprobadorPrimeraRevision or AprobadorConsolidado;
    }
}
