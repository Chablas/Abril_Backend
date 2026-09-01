namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models
{
    /// <summary>
    /// Carta oferta de un candidato seleccionado (tabla <c>gth_carta_oferta</c>): el ÚLTIMO paso del
    /// proceso de Reclutamiento, el que lo cierra.
    ///
    /// Se abre cuando el EMO de ingreso del seleccionado sale Apto (o Apto con Restricciones): GTH
    /// consigue la carta —la genera acá desde la plantilla Word o la adjunta ya armada en PDF—, se
    /// guarda en el file del colaborador en SharePoint y al candidato le llega un correo con un
    /// enlace por token donde la lee, registra su firma y la firma en línea. La carta NO viaja
    /// adjunta. Cuando GTH aprueba el documento firmado, el requerimiento pasa a CERRADO y recién
    /// ahí el colaborador puede entrar a Onboarding.
    ///
    /// La fila puede existir ANTES del envío: generar el documento crea un BORRADOR
    /// (<see cref="GeneradaUrl"/> llena, <see cref="EnviadaDateTime"/> en null) para que GTH lo
    /// revise. Lo que distingue una carta enviada de un borrador es siempre
    /// <see cref="EnviadaDateTime"/>, nunca la mera existencia de la fila. Generar no mueve la fase
    /// del requerimiento; enviarla sí.
    ///
    /// Vivía en <c>gth_onboarding</c> mientras la carta oferta era el primer paso del onboarding; al
    /// pasar a ser el último de reclutamiento se mudó acá, que es la feature dueña del proceso. El
    /// onboarding conserva solo lo suyo (fase, checklist y el file digital que esta carta ya creó).
    ///
    /// Los datos de la vacante (código, puesto, área, razón social, jefe directo) no se copian: se
    /// leen por <c>gth_candidato → gth_requerimiento</c>, que es donde ya viven.
    /// </summary>
    public class GthCartaOferta
    {
        public int GthCartaOfertaId { get; set; }

        /// <summary>
        /// FK al candidato seleccionado al que se le ofrece el puesto. Único entre los vigentes: un
        /// seleccionado no puede tener dos cartas oferta abiertas.
        /// </summary>
        public int GthCandidatoId { get; set; }

        /// <summary>
        /// FK a <c>person</c>: la ficha de la data maestra del colaborador. Es obligatoria — la firma
        /// que dibuja en el enlace público se guarda ahí (<c>person.signature_image_bytes</c>), así
        /// que sin ficha el enlace llegaría a una página que no puede terminar. Se resuelve del
        /// formulario aprobado del postulante o, en el ingreso directo FFT, del propio requerimiento.
        /// </summary>
        public int PersonId { get; set; }

        /// <summary>
        /// Fecha de ingreso pactada. La escribe GTH al enviar la carta: es una de las condiciones de
        /// la propuesta y viaja en el correo. El onboarding la hereda de acá.
        /// </summary>
        public DateOnly? FechaIngreso { get; set; }

        /// <summary>
        /// Sueldo básico bruto mensual ofrecido, en soles. Lo pone GTH al generar la carta desde la
        /// plantilla —es el <c>{{SUELDO}}</c> del documento— y NO es el sueldo referencial que puso
        /// el solicitante en el requerimiento: por regla de negocio la propuesta la define GTH.
        /// Null en las cartas que se adjuntaron ya armadas.
        /// </summary>
        public decimal? Sueldo { get; set; }

        /// <summary>
        /// Hasta cuándo el candidato puede aceptar la propuesta (<c>{{FECHA_LIMITE_ACEPTACION}}</c>).
        /// Por defecto el día siguiente al de la generación. Null en las cartas adjuntadas ya armadas.
        /// </summary>
        public DateOnly? FechaLimiteAceptacion { get; set; }

        // ── Carta oferta GENERADA desde la plantilla (.docx, borrador) ────────
        // El documento de trabajo: se genera rellenando la plantilla Word, queda en el file del
        // colaborador y GTH lo revisa —y lo corrige en Word si hace falta— antes de mandarlo. Va
        // aparte de los Carta* porque son dos archivos distintos del mismo expediente: este es el
        // Word editable y aquel el PDF que se le envió al candidato. Al enviar, el PDF sale de
        // convertir ESTE archivo tal como esté en SharePoint, no los bytes del momento de generarlo.
        public string? GeneradaNombre { get; set; }
        public string? GeneradaUrl { get; set; }
        public string? GeneradaItemId { get; set; }
        public string? GeneradaDriveId { get; set; }

        /// <summary>
        /// Última generación del .docx. Con valor y <see cref="EnviadaDateTime"/> en null la carta es
        /// un BORRADOR: el documento existe pero al candidato todavía no se le mandó nada.
        /// </summary>
        public DateTimeOffset? GeneradaDateTime { get; set; }
        public int? GeneradaUserId { get; set; }

        // ── Carta oferta (archivo en SharePoint + trazabilidad del envío) ─────
        public string? CartaNombre { get; set; }
        public string? CartaUrl { get; set; }
        public string? CartaItemId { get; set; }
        public string? CartaDriveId { get; set; }

        /// <summary>Correo personal al que se le envió el enlace de la carta oferta.</summary>
        public string? Correo { get; set; }

        public DateTimeOffset? EnviadaDateTime { get; set; }
        public int? EnviadaUserId { get; set; }

        /// <summary>
        /// Token del enlace público con el que el candidato ve y firma su carta oferta. Es la única
        /// credencial de esa página, así que es único entre las cartas vigentes. Se genera al enviar
        /// la carta y NO se rota al reenviar el enlace: un mismo candidato puede recibir el correo
        /// más de una vez y los dos enlaces tienen que seguir funcionando.
        /// </summary>
        public string? Token { get; set; }

        // ── Carta oferta FIRMADA (la que el candidato devuelve) ───────────────
        // Va aparte de la enviada porque son dos documentos distintos del mismo expediente: la
        // enviada es la propuesta y la firmada es la evidencia que cierra el proceso. Se guarda en
        // el mismo file del colaborador, en su propia subcarpeta.
        public string? FirmadaNombre { get; set; }
        public string? FirmadaUrl { get; set; }
        public string? FirmadaItemId { get; set; }
        public string? FirmadaDriveId { get; set; }

        public DateTimeOffset? FirmadaSubidaDateTime { get; set; }
        public int? FirmadaSubidaUserId { get; set; }

        /// <summary>
        /// Momento en que el candidato firmó la carta desde la página pública. Es lo que distingue
        /// las dos procedencias del documento firmado: con fecha la firmó él en la intranet; en null
        /// con <see cref="FirmadaUrl"/> llena la subió GTH a mano (la vía de respaldo, para quien
        /// firma en papel). Cuando firma el candidato <see cref="FirmadaSubidaUserId"/> queda en null
        /// porque no es un usuario del sistema.
        /// </summary>
        public DateTimeOffset? FirmadaPostulanteDateTime { get; set; }

        /// <summary>
        /// Momento en que GTH aprobó la carta firmada. Es lo que cierra el requerimiento: mientras
        /// esté en null el proceso de reclutamiento sigue abierto y el candidato no aparece en
        /// Onboarding.
        /// </summary>
        public DateTimeOffset? AprobadaDateTime { get; set; }
        public int? AprobadaUserId { get; set; }

        // ── File digital del colaborador ──────────────────────────────────────
        // La carpeta de SharePoint donde se guardan los documentos de este colaborador:
        // «{DNI} - {NOMBRE}» dentro de la biblioteca configurada, con una subcarpeta por tipo de
        // documento («Carta Oferta Enviada», «Carta Oferta Firmada»). Lo que se guarda acá es la
        // carpeta del colaborador —la padre—, no las subcarpetas: esas se resuelven al subir. Se
        // resuelve al enviar la carta oferta y se persiste para no volver a derivarla del nombre,
        // que puede cambiar en la base maestra después del envío. El onboarding la hereda de acá.
        public string? FileDigitalDriveId { get; set; }
        public string? FileDigitalItemId { get; set; }

        /// <summary>Ruta legible de esa carpeta, solo para mostrarla en pantalla.</summary>
        public string? FileDigitalRuta { get; set; }

        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;
    }
}
