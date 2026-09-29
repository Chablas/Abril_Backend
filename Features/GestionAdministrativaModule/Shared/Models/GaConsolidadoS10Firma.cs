using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Models
{
    /// <summary>
    /// Una firma estampada sobre un Consolidado del S10.
    ///
    /// Reemplaza a deducir los firmantes de <c>ga_rendicion.firmado_por_id</c>, que admitía UNO por
    /// planilla. Ese mecanismo alcanzaba mientras un consolidado se firmaba por partes —cada
    /// jefatura por sus propias planillas—, pero desde el 2026-09-21 en obra firman el administrador
    /// de obra y el residente sobre LAS MISMAS planillas, y eso ya no entra en una columna.
    ///
    /// El documento no queda firmado —ni pagable por Tesorería— hasta que estén todas las firmas que
    /// su área exige (<c>area_revisores_rendicion.aprueba_consolidado</c>, o el algoritmo).
    /// </summary>
    [Table("ga_consolidado_s10_firma")]
    public class GaConsolidadoS10Firma
    {
        [Column("ga_consolidado_s10_firma_id")]
        public int Id { get; set; }

        [Column("consolidado_s10_id")]
        public int ConsolidadoS10Id { get; set; }

        /// <summary>Usuario que apretó el botón (<c>app_user</c>).</summary>
        [Column("firmado_por_id")]
        public int FirmadoPorId { get; set; }

        /// <summary>
        /// Su ficha. Es con la que se compara contra los aprobadores que el área o el algoritmo
        /// exigen, que se resuelven por <c>workers.id</c> y no por usuario. Null solo si la ficha no
        /// se pudo resolver al firmar.
        /// </summary>
        [Column("worker_id")]
        public int? WorkerId { get; set; }

        /// <summary>
        /// Lugar de la firma en la hoja: 0 la primera, 1 la de al lado. Es el <c>slot</c> con el que
        /// se estampó, guardado para que una firma posterior no lo recalcule mal y pise a otra.
        /// </summary>
        [Column("slot")]
        public int Slot { get; set; }

        [Column("firmado_at")]
        public DateTimeOffset FirmadoAt { get; set; }

        /// <summary>Soft delete: false = eliminada (se conserva para auditoría).</summary>
        [Column("state")]
        public bool State { get; set; } = true;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }
    }
}
