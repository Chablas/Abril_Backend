using Abril_Backend.Features.GestionGthModule.Shared.FileDigital.Dtos;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos
{
    /// <summary>
    /// Todo lo que la página PÚBLICA de firma de la carta oferta necesita al abrirse, en una sola
    /// petición: de qué puesto es la propuesta, si ya hay una firma registrada para reusarla y en qué
    /// estado está el documento. La carta en sí no viaja acá — es un binario y va por su propio
    /// endpoint (<c>/publico/documento</c>), que es el que alimenta el visor.
    /// </summary>
    public class CartaOfertaFirmaPublicoDto
    {
        /// <summary>Nombre del postulante, para saludarlo y para que confirme que la carta es suya.</summary>
        public string Nombre { get; set; } = string.Empty;

        public string? Puesto { get; set; }
        public string? Area { get; set; }
        public string? Empresa { get; set; }
        public string? ProyectoObra { get; set; }
        public string? JefeDirecto { get; set; }
        public DateOnly? FechaIngreso { get; set; }

        /// <summary>Nombre del archivo de la carta oferta que subió GTH (el que se está viendo).</summary>
        public string? CartaNombre { get; set; }

        /// <summary>
        /// Firma ya guardada en su ficha de la base maestra, como data URL para mostrarla directo en
        /// un <c>&lt;img&gt;</c>. Null = todavía no registró ninguna, y el botón «Firmar» sigue
        /// bloqueado hasta que lo haga.
        /// </summary>
        public string? FirmaDataUrl { get; set; }

        /// <summary>Cuándo registró (o reemplazó) su firma, ya en hora de Perú.</summary>
        public DateTime? FirmaActualizadaEn { get; set; }

        /// <summary>true si la carta ya quedó firmada: la página pasa a solo lectura.</summary>
        public bool YaFirmada { get; set; }

        /// <summary>Cuándo firmó la carta, ya en hora de Perú.</summary>
        public DateTime? FirmadaEn { get; set; }

        /// <summary>
        /// true si GTH ya revisó y aprobó la carta firmada. Desde ese momento el documento es
        /// definitivo —y el proceso de reclutamiento quedó cerrado—, así que no se puede volver a
        /// firmar ni siquiera para corregir la firma.
        /// </summary>
        public bool Aprobada { get; set; }

        /// <summary>
        /// true si ya pulsó «Finalizar». La página pasa a su pantalla de cierre: no hay nada más que
        /// hacer y se lo decimos, que es justo para lo que existe ese paso.
        /// </summary>
        public bool Finalizada { get; set; }

        /// <summary>Cuándo finalizó el trámite, ya en hora de Perú.</summary>
        public DateTime? FinalizadaEn { get; set; }

        /// <summary>
        /// Cuándo abrió este enlace por primera vez, en hora de Perú. Es la fecha de conformidad que
        /// lleva impresa su carta. Viaja para que el servicio sepa, sin una consulta de más, si esta
        /// visita es la primera y hay que sellarla.
        /// </summary>
        public DateTime? PrimeraAperturaEn { get; set; }
    }

    /// <summary>Firma que el postulante dibujó en el canvas de la página pública.</summary>
    public class CartaOfertaFirmaGuardarDto
    {
        /// <summary>data:image/png;base64,… (o solo el base64) generado con canvas.toDataURL('image/png').</summary>
        public string ImageBase64 { get; set; } = null!;
    }

    /// <summary>Resultado de guardar la firma: la firma que quedó, para repintarla en la página.</summary>
    public class CartaOfertaFirmaGuardarResultDto
    {
        public string Message { get; set; } = string.Empty;
        public string? FirmaDataUrl { get; set; }
        public DateTime? FirmaActualizadaEn { get; set; }
    }

    /// <summary>Resultado de firmar la carta oferta desde la página pública.</summary>
    public class CartaOfertaFirmarResultDto
    {
        public string Message { get; set; } = string.Empty;

        /// <summary>Cuándo quedó firmada, ya en hora de Perú.</summary>
        public DateTime? FirmadaEn { get; set; }
    }

    /// <summary>
    /// Resultado de finalizar la carta oferta: el paso con el que el colaborador cierra su trámite
    /// después de firmar.
    /// </summary>
    public class CartaOfertaFinalizarResultDto
    {
        public string Message { get; set; } = string.Empty;

        /// <summary>Cuándo quedó finalizada, ya en hora de Perú.</summary>
        public DateTime? FinalizadaEn { get; set; }
    }

    /// <summary>
    /// Lo que el servicio necesita para resolver un token: a qué carta oferta apunta, de qué ficha es
    /// la firma, dónde está el documento que hay que mostrar o estampar y en qué estado quedó. Lo
    /// arma el repositorio en un solo roundtrip y no sale nunca al frontend.
    /// </summary>
    public class CartaOfertaFirmaContextoDto
    {
        public int CartaOfertaId { get; set; }

        /// <summary>Requerimiento al que pertenece: firmar lo mueve de fase.</summary>
        public int RequerimientoId { get; set; }

        /// <summary>Ficha de la base maestra donde vive la firma del postulante.</summary>
        public int PersonId { get; set; }

        /// <summary>Código del requerimiento (REQ-AAAA-NNNN): nombra el archivo firmado.</summary>
        public string Codigo { get; set; } = string.Empty;

        /// <summary>Nombre con el que se armó el file digital, para rearmarlo si no está persistido.</summary>
        public string Nombre { get; set; } = string.Empty;

        /// <summary>Documento de identidad, solo para ese mismo caso de rearmado.</summary>
        public string Dni { get; set; } = string.Empty;

        // ── Datos del colaborador y de su vacante (solo para el aviso a GTH) ───
        // Viajan en la misma consulta que resuelve el token en vez de pedirse aparte al firmar:
        // son joins de una fila sobre las llaves que la consulta ya recorre.

        /// <summary>
        /// Nombre del colaborador tal como lo ve GTH (el de la base maestra y, si todavía no tiene
        /// ficha, el que registró GTH). Es otro que <see cref="Nombre"/>, que es el del file digital
        /// y NO puede cambiar.
        /// </summary>
        public string NombreColaborador { get; set; } = string.Empty;

        public string? Puesto { get; set; }
        public string? Area { get; set; }
        public string? Empresa { get; set; }
        public string? ProyectoObra { get; set; }
        public string? JefeDirecto { get; set; }
        public DateOnly? FechaIngreso { get; set; }

        /// <summary>Correo personal al que se le envió la carta oferta.</summary>
        public string? Correo { get; set; }

        // ── Solicitante de la vacante (solo para el aviso de «finalizada») ────
        // Es quien pidió el puesto hace semanas y, hasta ahora, lo último que supo fue del
        // finalista: el aviso de que el colaborador cerró su carta le confirma el ingreso.

        /// <summary>Correo del usuario que registró la solicitud. Null si la solicitud quedó sin usuario.</summary>
        public string? SolicitanteEmail { get; set; }

        /// <summary>Su nombre, para encabezar el correo. Null si no tiene ficha en la base maestra.</summary>
        public string? SolicitanteNombre { get; set; }

        // ── Carta oferta que subió GTH (la que se muestra y se firma) ──────────
        public string? CartaNombre { get; set; }
        public string? CartaUrl { get; set; }
        public string? CartaDriveId { get; set; }
        public string? CartaItemId { get; set; }

        // ── .docx generado, del que sale el PDF ───────────────────────────────
        // Se necesita en la PRIMERA apertura: ahí hay que rellenarle la fecha de conformidad —el
        // único marcador que la generación deja sin resolver, porque su valor es justamente ese
        // momento— y volver a convertirlo a PDF. Null en las cartas viejas que GTH adjuntó ya
        // armadas: esas no tienen nada que rellenar.

        public string? GeneradaDriveId { get; set; }
        public string? GeneradaItemId { get; set; }
        public string? GeneradaNombre { get; set; }

        /// <summary>Momento de la primera apertura del enlace, si ya está sellada.</summary>
        public DateTimeOffset? PrimeraApertura { get; set; }

        // ── Carta ya firmada, si existe ────────────────────────────────────────
        // Es la que el visor muestra después de firmar: al postulante le interesa revisar y descargar
        // el documento con su firma, no el original.
        public string? FirmadaNombre { get; set; }
        public string? FirmadaUrl { get; set; }
        public string? FirmadaDriveId { get; set; }
        public string? FirmadaItemId { get; set; }

        /// <summary>File digital del colaborador, si ya está persistido en la fila.</summary>
        public FileDigitalCarpetaDto? Carpeta { get; set; }

        public bool YaFirmada { get; set; }
        public bool Aprobada { get; set; }

        /// <summary>
        /// true si el colaborador ya pulsó «Finalizar»: el trámite quedó cerrado de su lado y el
        /// documento firmado es el definitivo.
        /// </summary>
        public bool Finalizada { get; set; }
    }
}
