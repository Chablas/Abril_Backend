namespace Abril_Backend.Shared.Constants
{
    /// <summary>
    /// IDs del catálogo <c>project_ciclo_vida</c>. Se insertan con id explícito en
    /// <c>Migrations/Manual/20260928_ProyectosTipoYCicloVida.sql</c>, así que son idénticos en dev,
    /// demo y prod y se pueden usar como constantes.
    ///
    /// Sustituyen a las comparaciones por texto que había sobre las tres columnas viejas
    /// (<c>estado == "ACTIVO"</c>, <c>activo == "Finalizado"</c>, <c>operativo</c>).
    /// </summary>
    public static class ProjectCicloVidaIds
    {
        /// <summary>Vigente: en ejecución o por ejecutarse.</summary>
        public const int Activo = 1;

        /// <summary>Terminado.</summary>
        public const int Finalizado = 2;

        /// <summary>Paralizado, cancelado o sin actividad.</summary>
        public const int Inactivo = 3;
    }
}
