namespace Abril_Backend.Features.GestionAdministrativa.Shared.Dtos
{
    /// <summary>
    /// La solicitud de corrección del Consolidado del S10 que está viva en una planilla, tal como
    /// la ven las DOS pantallas del paso: Mis Rendiciones (el trabajador, para saber si la pelota
    /// sigue en el ERP o ya volvió a él) y Correcciones S10 (el Coordinador ERP, que la atiende).
    ///
    /// Por eso vive en el Shared del módulo y no dentro de una de las dos features.
    /// </summary>
    public class CorreccionS10Dto
    {
        public int Id { get; set; }
        public int RendicionId { get; set; }

        /// <summary>"Pendiente de corrección S10" | "Pendiente de recarga S10".</summary>
        public string Estado { get; set; } = string.Empty;

        /// <summary>El «MOTIVO *» que escribió el trabajador: qué necesita del ERP.</summary>
        public string Motivo { get; set; } = string.Empty;

        /// <summary>Con qué observó la jefatura el reembolso, copiada al solicitar.</summary>
        public string? MotivoJefatura { get; set; }

        /// <summary>Guía del consolidado observado — con esto el ERP lo encuentra en el S10.</summary>
        public string? NumeroGuia { get; set; }

        public string SolicitadaPor { get; set; } = string.Empty;
        public DateTimeOffset SolicitadaAt { get; set; }

        /// <summary>Coordinador ERP que confirmó la atención. Null mientras nadie la atienda.</summary>
        public string? AtendidaPor { get; set; }
        public DateTimeOffset? AtendidaAt { get; set; }
        public string? ComentarioAtencion { get; set; }

        /// <summary>
        /// True si el ERP anuló el registro del S10: hace falta una guía NUEVA y la anterior ya no
        /// se puede reutilizar (CA-19). La pantalla lo dice y el backend lo hace cumplir al
        /// recargar el consolidado.
        /// </summary>
        public bool GuiaAnulada { get; set; }

        /// <summary>True mientras el ERP no la haya atendido: la pelota está en el Coordinador.</summary>
        public bool EsperandoErp { get; set; }
    }
}
