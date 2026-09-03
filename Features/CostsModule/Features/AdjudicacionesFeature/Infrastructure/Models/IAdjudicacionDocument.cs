namespace Abril_Backend.Features.Costs.Adjudicaciones.Infrastructure.Models
{
    /// <summary>
    /// Contrato común de los documentos "uno por adjudicación" del expediente (contrato, hoja
    /// resumen, cronograma, ficha técnica, anexos, …). Cada uno vive en su propia tabla.
    ///
    /// El documento vigente es al que apunta la FK de <c>project_sub_contractor</c>. Al eliminarlo
    /// esa FK se limpia y la fila se marca con <c>State = false</c>: el archivo sigue existiendo y,
    /// gracias a <see cref="ProjectSubContractorId"/>, sigue sabiéndose de qué adjudicación era.
    /// </summary>
    public interface IAdjudicacionDocument
    {
        /// <summary>
        /// Adjudicación a la que pertenece el documento. Null solo en filas anteriores a la
        /// normalización que ninguna adjudicación llegó a referenciar.
        /// </summary>
        int? ProjectSubContractorId { get; set; }
        DateTimeOffset? UpdatedDatetime { get; set; }
        int? UpdatedUserId { get; set; }
        bool State { get; set; }
    }
}
