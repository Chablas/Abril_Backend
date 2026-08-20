namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application
{
    /// <summary>
    /// Códigos estables de los tipos de correo configurables del módulo de Reclutamiento
    /// (espejo de <c>gth_correo_tipo.codigo</c>). Se usan para scopear los destinatarios.
    /// </summary>
    public static class CorreoTipoReclutamiento
    {
        /// <summary>
        /// Correo de "solicitud de personal por aprobar" (va al Gerente General). Es el primer
        /// correo del flujo: hasta que el GG no aprueba, GTH no recibe nada.
        /// </summary>
        public const string AprobacionGg = "APROBACION_GG";

        /// <summary>
        /// Correo de "aprobación de Gerencia a GTH". Sale recién cuando Gerencia General aprueba, y
        /// solo con las vacantes aprobadas. Como lo dispara la decisión de Gerencia, se configura
        /// desde Aprobaciones y no desde Solicitud de Personal.
        /// </summary>
        public const string Solicitud = "SOLICITUD";

        /// <summary>
        /// Correo de "vacantes aprobadas a TI". Sale junto con el de GTH, en la misma decisión de
        /// Gerencia General y con las mismas vacantes aprobadas, pero es un aviso de preparación:
        /// TI necesita la anticipación para alistar equipo, usuario y accesos de cada ingreso. Es
        /// independiente del de GTH — apagar uno no apaga el otro.
        /// </summary>
        public const string Ti = "TI_VACANTES";

        /// <summary>Correo de "long list enviada" (va al solicitante).</summary>
        public const string LongList = "LONG_LIST";

        /// <summary>Correo de "decisión de long list" (lo envía el solicitante y va a GTH).</summary>
        public const string LongListDecision = "LONG_LIST_DECISION";

        /// <summary>
        /// Correo de "finalista enviado al solicitante". Sale cuando GTH guarda el informe de la
        /// entrevista de un candidato, que es el acto de mandarlo como finalista: el solicitante
        /// tiene que entrar a decidir. El destinatario principal es SIEMPRE el solicitante que
        /// registró la solicitud; la configuración solo aporta principales adicionales y copias.
        /// </summary>
        public const string FinalistaEnvio = "FINALISTA_ENVIO";

        /// <summary>
        /// Correo de "decisión de finalista" (lo envía el solicitante al aprobar o rechazar a un
        /// finalista y va a GTH).
        /// </summary>
        public const string FinalistaDecision = "FINALISTA_DECISION";

        /// <summary>
        /// Correo de "formulario del postulante completado" (va a GTH). Sale solo, sin que nadie lo
        /// dispare, en cuanto el postulante envía su formulario desde la página pública, para que
        /// GTH sepa que ya lo puede revisar.
        /// </summary>
        public const string FormularioCompletado = "FORMULARIO_COMPLETADO";

        /// <summary>
        /// Correo de "formulario del postulante enviado" (va al postulante). Lo dispara GTH desde
        /// la bandeja, uno por uno o en lote, y lleva el enlace público del formulario. El
        /// destinatario principal es SIEMPRE el postulante; la configuración solo aporta
        /// principales adicionales y copias.
        /// </summary>
        public const string FormularioEnvio = "FORMULARIO_ENVIO";

        /// <summary>
        /// Correo de "correcciones del formulario" (va al postulante). Sale cuando GTH rechaza un
        /// formulario ya llenado: lleva las observaciones y el MISMO enlace del envío original.
        /// Es otro correo que el de invitación —otro asunto y otro cuerpo—, por eso se configura
        /// aparte de <see cref="FormularioEnvio"/>.
        /// </summary>
        public const string FormularioCorreccion = "FORMULARIO_CORRECCION";

        /// <summary>
        /// Correo de "invitación a la entrevista" (va al postulante citado). El destinatario
        /// principal es SIEMPRE el postulante; la configuración solo aporta principales adicionales
        /// y copias, por si GTH quiere quedarse con el registro de cada citación.
        /// </summary>
        public const string Entrevista = "ENTREVISTA";

        /// <summary>
        /// Correo de "respuesta del candidato a la entrevista" (va a GTH). Lo dispara el propio
        /// candidato al pulsar Confirmar o Rechazar en el correo de invitación. El destinatario
        /// principal es SIEMPRE el área de GTH (<c>area_scope.email</c> de su nodo, el mismo buzón
        /// que resuelve <see cref="CorreoDestinatarioCodigo.GthArea"/>), así que la configuración
        /// solo aporta principales adicionales y copias.
        /// </summary>
        public const string EntrevistaRespuesta = "ENTREVISTA_RESPUESTA";

        /// <summary>
        /// Correo de "fin de proceso" (va al candidato que no continúa). Sale desde cuatro lados
        /// con el mismo cuerpo: cuando GTH rechaza al postulante tras rechazarle el formulario,
        /// cuando lo marca como "no continúa" tras la entrevista, cuando el solicitante rechaza a
        /// un finalista y cuando aprueba a uno y los demás quedan sin elegir. Es un solo correo,
        /// así que un solo tipo gobierna los cuatro. El destinatario principal es SIEMPRE el
        /// candidato.
        /// </summary>
        public const string Agradecimiento = "AGRADECIMIENTO";

        /// <summary>
        /// Traduce el slug de la URL (<c>aprobacion-gg</c> / <c>solicitud</c> / <c>ti-vacantes</c> /
        /// <c>long-list</c> / <c>decision-long-list</c> / <c>finalista-envio</c> /
        /// <c>decision-finalista</c> / <c>formulario-envio</c> / <c>formulario-completado</c> /
        /// <c>formulario-correccion</c> / <c>entrevista</c> / <c>entrevista-respuesta</c> /
        /// <c>agradecimiento</c>) al código estable. Devuelve null si el slug no corresponde a un
        /// tipo conocido.
        /// </summary>
        public static string? FromSlug(string? slug) => slug?.Trim().ToLowerInvariant() switch
        {
            "aprobacion-gg"          => AprobacionGg,
            "solicitud"              => Solicitud,
            "ti-vacantes"            => Ti,
            "long-list"              => LongList,
            "decision-long-list"     => LongListDecision,
            "finalista-envio"        => FinalistaEnvio,
            "decision-finalista"     => FinalistaDecision,
            "formulario-envio"       => FormularioEnvio,
            "formulario-completado"  => FormularioCompletado,
            "formulario-correccion"  => FormularioCorreccion,
            "entrevista"             => Entrevista,
            "entrevista-respuesta"   => EntrevistaRespuesta,
            "agradecimiento"         => Agradecimiento,
            _ => null,
        };
    }
}
