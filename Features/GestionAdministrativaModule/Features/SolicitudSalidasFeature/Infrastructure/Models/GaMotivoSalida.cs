namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models
{
    public class GaMotivoSalida
    {
        public int Id { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public bool Activo { get; set; } = true;
        /// <summary>Si true, al solicitar una salida con este motivo se exige un documento
        /// adjunto (a modo de prueba, ej. constancia de capacitación o cita médica).</summary>
        public bool RequiereAdjunto { get; set; }
        /// <summary>Si true, las horas declaradas con este motivo son estimadas: recepción
        /// no registra la hora real de salida/retorno para estas solicitudes.</summary>
        public bool EsHoraEstimada { get; set; }
        /// <summary>Si true, al elegir este motivo el formulario exige escribir un detalle
        /// obligatorio (ej. "Visita a obra" → a qué se va). Se guarda en
        /// <c>ga_solicitud_trayecto.motivo_adicional</c>.</summary>
        public bool RequiereMotivoAdicional { get; set; }
        /// <summary>
        /// Si false, al elegir este motivo la solicitud no pide horas, ni lugares, ni trayectos
        /// adicionales: queda un único trayecto que solo lleva el motivo (ej. "Licencia sin goce
        /// de haber", que es una ausencia de día completo y no un desplazamiento). Default true =
        /// comportamiento normal de una salida.
        /// </summary>
        public bool PideHorasLugares { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; }
    }
}
