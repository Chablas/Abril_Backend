namespace Abril_Backend.Shared.Constants
{
    /// <summary>
    /// IDs del catálogo <c>project_tipo</c>. Se insertan con id explícito en
    /// <c>Migrations/Manual/20260928_ProyectosTipoYCicloVida.sql</c>, así que son idénticos en dev,
    /// demo y prod y se pueden usar como constantes.
    ///
    /// Para «¿se trata como obra?» preferir la columna <c>project_tipo.es_obra</c>
    /// (<c>ProjectTipo.EsObra</c>) antes que enumerar estos ids.
    /// </summary>
    public static class ProjectTipoIds
    {
        /// <summary>Proyecto inmobiliario: edificio que se vende al público. El único proyecto «de verdad».</summary>
        public const int Proyecto = 1;

        /// <summary>
        /// Edificación personal del gerente general (p. ej. su casa de playa): se construye, pero no
        /// está ni estará a la venta al público.
        /// </summary>
        public const int Fft = 2;

        /// <summary>
        /// Sede central de Abril, donde trabaja el personal de oficina (GTH, TI, Post Venta y demás
        /// áreas). No es un proyecto.
        /// </summary>
        public const int OficinaCentral = 3;

        /// <summary>
        /// Área de la empresa registrada como proyecto para que funcionen otras pantallas (Post Venta,
        /// Arquitectura Comercial, Eventos). No es un proyecto.
        /// </summary>
        public const int AreaInterna = 4;

        /// <summary>Proyecto de prueba del sistema (Torre Abril). No es un proyecto.</summary>
        public const int Prueba = 5;
    }
}
