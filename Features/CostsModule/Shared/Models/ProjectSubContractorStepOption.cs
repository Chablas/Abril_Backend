namespace Abril_Backend.Features.CostsModule.Shared.Models
{
    /// <summary>
    /// Opción configurable de un paso del flujo de adjudicaciones (sección "Pasos" de
    /// Configuración de Costos). Cada fila ES una opción: la descripción es la etiqueta que
    /// se muestra como checkbox y <see cref="Enabled"/> es su valor.
    ///
    /// Agregar una opción nueva es una fila más (dato), no una columna ni un deploy: la
    /// pantalla de configuración agrupa por paso y renderiza lo que haya. El código que
    /// consume una opción la busca por <see cref="OptionKey"/>, que tiene una constante en
    /// <see cref="Abril_Backend.Features.CostsModule.Shared.Constants.CostsStepOptionKeys"/>.
    /// </summary>
    public class ProjectSubContractorStepOption
    {
        public int ProjectSubContractorStepOptionId { get; set; }
        /// <summary>Paso al que pertenece la opción (FK a project_sub_contractor_status).</summary>
        public int ProjectSubContractorStatusId { get; set; }
        /// <summary>Clave estable con la que el código pide la opción. No se muestra al usuario.</summary>
        public string OptionKey { get; set; } = null!;
        /// <summary>Etiqueta del checkbox en la pantalla de configuración.</summary>
        public string OptionDescription { get; set; } = null!;
        /// <summary>Valor de la opción: es lo que el checkbox prende y apaga.</summary>
        public bool Enabled { get; set; }
        /// <summary>Orden dentro de su paso.</summary>
        public int DisplayOrder { get; set; }
        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        /// <summary>False = la opción no se muestra en la pantalla de configuración.</summary>
        public bool Active { get; set; }
        public bool State { get; set; }
    }
}
