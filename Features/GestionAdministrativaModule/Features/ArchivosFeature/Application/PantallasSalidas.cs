namespace Abril_Backend.Features.GestionAdministrativa.Archivos.Application
{
    /// <summary>
    /// Las siete pantallas del ciclo de salidas (<c>feature.feature_key</c>), que son las que
    /// muestran archivos del módulo. Son constantes porque <c>[RequireFeature]</c> las pide así.
    /// </summary>
    public static class PantallasSalidas
    {
        public const string SolicitudSalidas   = "gestion-administrativa.solicitud-salidas";
        public const string Rendiciones        = "gestion-administrativa.rendiciones";
        public const string GestionSalidas     = "gestion-administrativa.gestion-salidas";
        public const string GestionRendiciones = "gestion-administrativa.gestion-rendiciones";
        public const string Consolidados       = "gestion-administrativa.consolidados";
        public const string CorreccionesS10    = "gestion-administrativa.correcciones-s10";
        public const string Reembolsos         = "gestion-administrativa.reembolsos";

        /// <summary>
        /// Las bandejas que miran salidas de OTROS: quien entra a alguna puede ver cualquier archivo
        /// del módulo. Solicitud de Salidas y Mis Rendiciones quedan afuera — son del propio
        /// trabajador, y con solo esas se ven únicamente los archivos propios.
        /// </summary>
        public static readonly string[] DeRevision =
        {
            GestionSalidas, GestionRendiciones, Consolidados, CorreccionesS10, Reembolsos,
        };
    }
}
