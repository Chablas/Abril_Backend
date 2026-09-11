namespace Abril_Backend.Shared.Constants
{
    /// <summary>
    /// IDs del catálogo <c>ga_visibilidad_ambito</c>: sobre qué pantalla aplica una fila de
    /// <c>ga_visibilidad_area</c> (el override manual de "qué áreas ve este trabajador").
    ///
    /// Son dos alcances independientes a propósito: quien ve todas las solicitudes de salida no
    /// tiene por qué ver todas las planillas de rendición, y al revés. Cada pantalla administra el
    /// suyo desde su propia Configuración → Visibilidad.
    /// </summary>
    public static class VisibilidadAmbitoIds
    {
        /// <summary>Gestión de Salidas: qué solicitudes de salida ve el trabajador.</summary>
        public const int Salidas = 1;

        /// <summary>Gestión de Rendiciones: qué planillas de rendición ve el trabajador.</summary>
        public const int Rendiciones = 2;
    }
}
