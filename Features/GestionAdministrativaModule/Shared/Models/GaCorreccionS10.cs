using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Models
{
    /// <summary>
    /// Una solicitud de corrección del Consolidado del S10 dirigida al Coordinador ERP (§10.5 del
    /// requerimiento funcional, RG-21 / RG-22).
    ///
    /// Existe porque la corrección que pide la jefatura en la segunda revisión suele tener que
    /// hacerse DENTRO del S10, y el trabajador no tiene permiso ahí: el Coordinador ERP sí. Abril
    /// One no toca el S10 — solo registra el pedido, el motivo y la confirmación de que se atendió
    /// (§2.1: "la corrección se ejecuta en S10 por el responsable ERP").
    ///
    /// La unidad es la PLANILLA y no la salida, igual que el Consolidado del S10: el documento que
    /// hay que corregir es uno solo y cubre todas las salidas que la planilla agrupa.
    ///
    /// Es un eje de estado aparte del reembolso: mientras la corrección está viva, las salidas se
    /// quedan en <see cref="EstadosSalida.Reembolso.Observado"/>. Lo que se mueve acá es de quién
    /// es la pelota (ver <see cref="EstadosSalida.CorreccionS10"/>). Cuando el trabajador vuelve a
    /// adjuntar el consolidado, la corrección se da de baja (<see cref="State"/> = false) y el
    /// reembolso vuelve a Pendiente: el ciclo se cierra donde empezó.
    /// </summary>
    public class GaCorreccionS10
    {
        public int Id { get; set; }

        /// <summary>FK a <c>ga_rendicion</c>: la planilla cuyo Consolidado del S10 hay que corregir.</summary>
        public int RendicionId { get; set; }

        /// <summary>
        /// FK a <c>ga_consolidado_s10</c>: el consolidado OBSERVADO, el que se está pidiendo
        /// corregir. Se guarda para que la bandeja del ERP pueda abrir el PDF exacto que la
        /// jefatura miró, aunque después se reemplace por el corregido.
        ///
        /// Nullable por defensa: si la planilla llegara sin consolidado vigente la solicitud igual
        /// se registra (el motivo es lo que importa), pero en el flujo normal siempre viene.
        /// </summary>
        public int? ConsolidadoS10Id { get; set; }

        /// <summary>
        /// El campo «MOTIVO *» del requerimiento (RG-21): lo que el trabajador le pide al
        /// Coordinador ERP. Obligatorio — sin esto la solicitud no se puede enviar (CA-17).
        /// </summary>
        public string Motivo { get; set; } = string.Empty;

        /// <summary>
        /// Copia de la observación con la que la jefatura devolvió el reembolso, tomada en el
        /// momento de solicitar. Se guarda acá y no se lee de la salida a propósito: el ERP
        /// necesita saber qué se observó, y la observación de la salida se sobrescribe si la
        /// jefatura vuelve a observar más adelante.
        /// </summary>
        public string? MotivoJefatura { get; set; }

        /// <summary>
        /// Número de guía del consolidado observado, copiado al solicitar. Es el dato con el que el
        /// ERP encuentra el registro en el S10 (§10.5: se envía "la rendición, guía, motivo de
        /// jefatura y MOTIVO").
        /// </summary>
        public string? NumeroGuia { get; set; }

        /// <summary>FK a <c>ga_estado_correccion_s10</c>. Ver <see cref="EstadosSalida.CorreccionS10"/>.</summary>
        public int EstadoId { get; set; } = EstadosSalida.CorreccionS10.Solicitada;

        // ── Quién pidió ──────────────────────────────────────────────────────
        /// <summary>FK a <c>app_user.user_id</c> del trabajador que pidió la corrección.</summary>
        public int SolicitadaPorId { get; set; }
        public DateTimeOffset SolicitadaAt { get; set; }

        // ── Quién atendió ────────────────────────────────────────────────────
        /// <summary>FK a <c>app_user.user_id</c> del Coordinador ERP que marcó el check (RG-22).</summary>
        public int? AtendidaPorId { get; set; }
        public DateTimeOffset? AtendidaAt { get; set; }

        /// <summary>
        /// Comentario opcional del Coordinador ERP al confirmar: qué hizo en el S10. No es
        /// obligatorio porque el requerimiento solo exige el check (RF-OBS-07), pero cuando el
        /// consolidado se anuló y hay que sacar una guía nueva, es donde lo explica.
        /// </summary>
        public string? ComentarioAtencion { get; set; }

        /// <summary>
        /// True cuando el ERP anuló el registro del S10 en vez de corregirlo: el trabajador tiene
        /// que generar una guía NUEVA y la anterior queda inservible (HU-ERP-03 / CA-19). Con esto
        /// en true, volver a adjuntar el consolidado con la misma guía se rechaza.
        /// </summary>
        public bool GuiaAnulada { get; set; }

        /// <summary>
        /// Soft delete. Pasa a false cuando el trabajador recarga el Consolidado del S10: la
        /// gestión terminó y la planilla queda libre para pedir otra corrección si la jefatura
        /// vuelve a observar. La fila se conserva para la auditoría del ciclo (RF-TRZ-08/09).
        /// </summary>
        public bool State { get; set; } = true;

        public DateTimeOffset CreatedDateTime { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
    }
}
