namespace Abril_Backend.Features.GestionAdministrativa.Shared.Dtos
{
    /// <summary>
    /// La solicitud de corrección del Consolidado del S10 que está viva en una planilla, tal como
    /// la ven las DOS pantallas del paso: Consolidados (el consolidador que la pidió, para saber si
    /// la pelota sigue en el ERP o ya volvió a él) y Correcciones S10 (el Coordinador ERP, que la
    /// atiende).
    ///
    /// Por eso vive en el Shared del módulo y no dentro de una de las dos features.
    /// </summary>
    public class CorreccionS10Dto
    {
        public int Id { get; set; }
        public int RendicionId { get; set; }

        /// <summary>"Pendiente de corrección S10" | "Atendido".</summary>
        public string Estado { get; set; } = string.Empty;

        /// <summary>El «MOTIVO *» que escribió el consolidador: qué necesita del ERP.</summary>
        public string Motivo { get; set; } = string.Empty;

        /// <summary>Con qué se observó el reembolso, copiada al solicitar.</summary>
        public string? MotivoJefatura { get; set; }

        /// <summary>
        /// Quién escribió esa observación: "Jefatura" o "Tesorería" (RG-49). Vacío en las
        /// correcciones anteriores a la columna, que son todas de jefatura.
        /// </summary>
        public string MotivoOrigen { get; set; } = string.Empty;

        /// <summary>Número de reembolso del consolidado observado — con esto el ERP lo encuentra en el S10.</summary>
        public string? NumeroReembolso { get; set; }

        public string SolicitadaPor { get; set; } = string.Empty;
        public DateTimeOffset SolicitadaAt { get; set; }

        /// <summary>Coordinador ERP que confirmó la atención. Null mientras nadie la atienda.</summary>
        public string? AtendidaPor { get; set; }
        public DateTimeOffset? AtendidaAt { get; set; }
        public string? ComentarioAtencion { get; set; }

        /// <summary>True mientras el ERP no la haya atendido: la pelota está en el Coordinador.</summary>
        public bool EsperandoErp { get; set; }
    }
}
