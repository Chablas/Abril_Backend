using Abril_Backend.Features.GestionGthModule.Shared.FileDigital.Dtos;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos
{
    /// <summary>
    /// La carta oferta del seleccionado, tal como la ve GTH en el detalle del requerimiento: el
    /// último paso del proceso. Viaja con el detalle (no en una petición aparte) porque es una
    /// sección más del modal.
    ///
    /// Es null mientras el requerimiento no tenga un seleccionado; en cuanto lo tiene, viene con los
    /// datos de destino resueltos de su ficha de la base maestra —correo, documento y si la ficha
    /// existe—, que es lo que la pantalla necesita para poder enviar. <see cref="CartaOfertaId"/> en
    /// null significa que todavía no se envió nada.
    /// </summary>
    public class CartaOfertaRequerimientoDto
    {
        // ── Destino (siempre, aunque la carta no se haya enviado) ─────────────

        /// <summary>Nombre del colaborador según su ficha de la base maestra (o el del candidato).</summary>
        public string Nombre { get; set; } = string.Empty;

        /// <summary>
        /// Correo personal al que iría la carta oferta. Sale siempre de <c>person.email</c>, igual
        /// para el ingreso normal que para el directo FFT: es el único correo que alguien de GTH
        /// revisó. Null = su ficha no tiene correo y hay que escribirlo a mano en el modal.
        /// </summary>
        public string? CorreoSugerido { get; set; }

        /// <summary>
        /// Documento de identidad de esa misma ficha. Es lo que nombra su carpeta en el file de
        /// colaboradores («80508050 - NOMBRE»), así que null = no se puede enviar la carta.
        /// </summary>
        public string? Dni { get; set; }

        /// <summary>
        /// true si el candidato ya tiene ficha en <c>person</c>. La firma que dibuja en el enlace se
        /// guarda ahí, así que sin ficha el envío queda bloqueado.
        /// </summary>
        public bool TieneFichaMaestra { get; set; }

        // ── Envío (todo null mientras la carta no se haya enviado) ────────────

        /// <summary>Null = la carta oferta todavía no se envió.</summary>
        public int? CartaOfertaId { get; set; }

        /// <summary>Fecha de ingreso pactada, una de las condiciones que viajan en el correo.</summary>
        public DateOnly? FechaIngreso { get; set; }

        public string? CartaNombre { get; set; }
        public string? CartaUrl { get; set; }

        /// <summary>Correo al que se envió el enlace (el histórico del envío).</summary>
        public string? Correo { get; set; }

        /// <summary>Momento del último envío del enlace, ya en hora de Perú.</summary>
        public DateTime? EnviadaEn { get; set; }

        // ── Carta firmada ─────────────────────────────────────────────────────
        public string? FirmadaNombre { get; set; }
        public string? FirmadaUrl { get; set; }

        /// <summary>Momento en que el documento firmado entró al expediente, en hora de Perú.</summary>
        public DateTime? FirmadaSubidaEn { get; set; }

        /// <summary>
        /// Momento en que el CANDIDATO firmó desde el enlace público, en hora de Perú. Con valor, el
        /// documento vino de él y GTH solo revisa; en null con <see cref="FirmadaUrl"/> llena, lo
        /// subió GTH a mano.
        /// </summary>
        public DateTime? FirmadaPostulanteEn { get; set; }

        /// <summary>
        /// Momento en que GTH aprobó la carta firmada, en hora de Perú. Es lo que cierra el
        /// requerimiento: null = todavía pendiente de revisión.
        /// </summary>
        public DateTime? AprobadaEn { get; set; }

        /// <summary>Carpeta de SharePoint donde vive el file digital del colaborador.</summary>
        public string? FileDigitalCarpeta { get; set; }
    }

    /// <summary>Datos del envío de la carta oferta (el JSON del multipart; la carta va como archivo).</summary>
    public class CartaOfertaEnviarDto
    {
        /// <summary>Fecha de ingreso pactada. Opcional: si no se sabe todavía, la carta sale sin ella.</summary>
        public DateOnly? FechaIngreso { get; set; }

        /// <summary>
        /// Correo al que enviar el enlace. Normalmente no viaja: el backend lo resuelve de la base de
        /// datos. Solo se usa si GTH lo corrigió a mano en el modal.
        /// </summary>
        public string? Correo { get; set; }
    }

    /// <summary>Cuerpo del reenvío del enlace de firma. Todo opcional: normalmente va vacío.</summary>
    public class CartaOfertaReenviarDto
    {
        /// <summary>
        /// Correo al que reenviar el enlace. Si no viene se usa el de la base maestra y, en su
        /// defecto, el que ya quedó registrado en el envío anterior.
        /// </summary>
        public string? Correo { get; set; }
    }

    /// <summary>
    /// Resultado de cualquier acción sobre la carta oferta: la carta ya actualizada y, cuando la
    /// acción movió la fase del requerimiento, el estado nuevo. El modal repinta con esto sin volver
    /// a pedir el detalle entero.
    /// </summary>
    public class CartaOfertaAccionResultDto
    {
        public string Message { get; set; } = string.Empty;

        public CartaOfertaRequerimientoDto? CartaOferta { get; set; }

        /// <summary>Fase del requerimiento después de la acción (siempre viene: nunca cambia sola).</summary>
        public string EstadoCodigo { get; set; } = string.Empty;
        public string EstadoNombre { get; set; } = string.Empty;
    }

    /// <summary>
    /// Contexto que devuelve el repositorio al validar el envío de una carta oferta: todo lo que el
    /// servicio necesita para subirla a SharePoint y armar el correo, resuelto en un solo roundtrip y
    /// ANTES de escribir nada. También es lo que devuelve el reenvío del enlace, que necesita
    /// exactamente los mismos datos sobre una carta ya enviada.
    /// </summary>
    public class CartaOfertaContextoDto
    {
        public int RequerimientoId { get; set; }
        public int CandidatoId { get; set; }

        /// <summary>
        /// Ficha del colaborador en la base maestra. Es obligatoria: la firma que dibuja en el enlace
        /// público se guarda en <c>person.signature_image_bytes</c>, así que sin ficha no habría
        /// dónde ponerla.
        /// </summary>
        public int PersonId { get; set; }

        /// <summary>Código del requerimiento (REQ-AAAA-NNNN): nombra el archivo en SharePoint.</summary>
        public string Codigo { get; set; } = string.Empty;

        /// <summary>Nombre con el que se arma la carpeta del file digital.</summary>
        public string Nombre { get; set; } = string.Empty;

        /// <summary>Documento de identidad: primer tramo del nombre de esa carpeta.</summary>
        public string Dni { get; set; } = string.Empty;

        /// <summary>
        /// Token del enlace público. Al enviar la carta lo genera el servicio; al reenviar el enlace
        /// es el que ya está guardado en la fila.
        /// </summary>
        public string Token { get; set; } = string.Empty;

        public string? Puesto { get; set; }
        public string? Area { get; set; }
        public string? Empresa { get; set; }
        public string? ProyectoObra { get; set; }
        public string Correo { get; set; } = string.Empty;
        public DateOnly? FechaIngreso { get; set; }
        public string? JefeDirecto { get; set; }

        /// <summary>File digital ya persistido. Null la primera vez (hay que resolverlo).</summary>
        public FileDigitalCarpetaDto? Carpeta { get; set; }
    }

    /// <summary>
    /// Lo que el servicio necesita para subir la carta FIRMADA de una carta oferta ya enviada: dónde
    /// está su file digital y con qué identificar el archivo. Lo resuelve el repositorio en un solo
    /// roundtrip, antes de tocar SharePoint.
    /// </summary>
    public class CartaOfertaDocumentoContextoDto
    {
        public int CartaOfertaId { get; set; }
        public int RequerimientoId { get; set; }
        public string Codigo { get; set; } = string.Empty;

        /// <summary>Nombre con el que se armó el file digital (para rearmarlo si no está persistido).</summary>
        public string Nombre { get; set; } = string.Empty;

        /// <summary>Documento de identidad, solo para ese mismo caso de rearmado.</summary>
        public string Dni { get; set; } = string.Empty;

        /// <summary>Carpeta ya persistida. Null en las cartas anteriores a que se guardara.</summary>
        public FileDigitalCarpetaDto? Carpeta { get; set; }
    }
}
