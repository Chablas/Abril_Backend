using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces
{
    public interface IReclutamientoService
    {
        Task<ReclutamientoFormDataDto> GetFormData(int? userId);

        /// <summary>
        /// Crea la solicitud de personal: valida, sube el sustento (opcional) a SharePoint y
        /// persiste 1 requerimiento por vacante con su código REQ-AAAA-NNNN, en fase
        /// APROBACION_GG. Luego manda el correo de aprobación al Gerente General (GTH no se
        /// entera hasta que él apruebe); el resultado dice si ese correo pudo salir.
        /// </summary>
        Task<SolicitudPersonalCreateResultDto> Create(SolicitudPersonalCreateDto dto, int? userId, IFormFile? sustento);

        /// <summary>
        /// Panel de la vista del solicitante: tarjetas de "Gestión de candidatos" (long lists que GTH
        /// le envió) + tabla "Mis solicitudes de vacante", en una sola petición. Vacío si no hay usuario.
        /// </summary>
        Task<SolicitantePanelDto> GetSolicitantePanel(int? userId);

        /// <summary>
        /// Revisión de la long list de un requerimiento del solicitante (cabecera + candidatos con su CV).
        /// Lanza <see cref="Abril_Backend.Application.Exceptions.AbrilException"/> 404 si no existe, no le
        /// pertenece o su long list aún no fue enviada.
        /// </summary>
        Task<RevisionLongListDto> GetRevisionLongList(int requerimientoId, int? userId);

        /// <summary>
        /// Registra la decisión del solicitante sobre la long list (aprobar/rechazar por candidato),
        /// avanza el requerimiento (LONG_LIST_APROBADA si hay ≥1 aprobado; de vuelta a LONG_LIST si
        /// rechazó a todos) y notifica a GTH por correo (tipo LONG_LIST_DECISION, best-effort).
        /// Devuelve el estado resultante y los conteos.
        /// </summary>
        Task<LongListDecisionResultDto> RegistrarDecisionLongList(int requerimientoId, LongListDecisionDto dto, int? userId);

        /// <summary>
        /// Bandeja de la vista de GTH: tarjeta "En proceso" + tabla de solicitudes de contratación de
        /// toda la organización, en una sola petición.
        /// </summary>
        Task<BandejaReclutamientoDto> GetBandeja();

        /// <summary>Actualiza la prioridad (Alta/Media/Baja) de un requerimiento desde la bandeja de GTH.</summary>
        Task UpdatePrioridad(int requerimientoId, int prioridadId, int? userId);

        /// <summary>
        /// Detalle de seguimiento de un requerimiento del usuario (cabecera + fases del pipeline).
        /// Lanza <see cref="Abril_Backend.Application.Exceptions.AbrilException"/> 404 si no existe
        /// o no le pertenece al usuario.
        /// </summary>
        Task<SeguimientoDto> GetSeguimiento(int requerimientoId, int? userId);

        /// <summary>
        /// Detalle de un requerimiento para la vista de GTH (modal del ojo de la bandeja).
        /// Lanza <see cref="Abril_Backend.Application.Exceptions.AbrilException"/> 404 si no existe.
        /// </summary>
        Task<DetalleRequerimientoGthDto> GetDetalleGth(int requerimientoId);

        /// <summary>Guarda la asignación interna de GTH de un requerimiento (los 4 desplegables del modal).</summary>
        Task UpdateAsignacionGth(int requerimientoId, AsignacionGthUpdateDto dto, int? userId);

        /// <summary>
        /// Registra los canales donde se publicó la vacante (reemplaza el conjunto) y avanza el
        /// requerimiento a la fase PUBLICACION. Devuelve el estado resultante.
        /// </summary>
        Task<EstadoRequerimientoResultDto> ReplacePublicaciones(int requerimientoId, PublicacionesUpdateDto dto, int? userId);

        /// <summary>Inicia la revisión de CV: avanza el requerimiento de PUBLICACION a LONG_LIST.</summary>
        Task<EstadoRequerimientoResultDto> IniciarRevisionCv(int requerimientoId, int? userId);

        /// <summary>
        /// Envía la long list al solicitante: avanza el requerimiento a LONG_LIST_ENVIADA y manda
        /// el correo a los destinatarios configurados (tipo LONG_LIST) con los CVs adjuntos.
        /// Devuelve el estado resultante.
        /// </summary>
        Task<EstadoRequerimientoResultDto> EnviarLongList(int requerimientoId, List<LongListCandidatoArchivoDto> candidatos, int? userId);

        /// <summary>Marca/desmarca el check informativo del Multitest de un candidato aprobado.</summary>
        Task SetMultitest(int candidatoId, MultitestUpdateDto dto, int? userId);

        /// <summary>
        /// Avanza el requerimiento de LONG_LIST_APROBADA a ENTREVISTAS validando los requisitos del
        /// paso (Multitest completo, formularios revisados y al menos uno aprobado).
        /// </summary>
        Task<EstadoRequerimientoResultDto> ContinuarAEntrevistas(int requerimientoId, int? userId);

        /// <summary>
        /// Programa (o reprograma) la entrevista de un candidato y le envía la invitación por correo.
        /// El envío es best-effort: si el correo falla, la programación queda igual guardada y se
        /// informa en el mensaje para que GTH reintente.
        /// </summary>
        Task<EntrevistaAccionResultDto> GuardarEntrevista(int candidatoId, EntrevistaGuardarDto dto, int? userId);

        /// <summary>
        /// Registra la respuesta del candidato a su citación desde los botones Confirmar/Rechazar
        /// del correo (endpoint público por token) y le avisa a GTH. El aviso es best-effort: la
        /// respuesta ya quedó registrada, así que un fallo del correo interno no le muestra un
        /// error al candidato. <paramref name="respuesta"/> acepta el verbo del enlace
        /// (<c>confirmar</c> / <c>rechazar</c>) o el código del catálogo.
        /// </summary>
        Task<EntrevistaRespuestaPublicaDto> ResponderEntrevista(string token, string respuesta);

        /// <summary>
        /// Guarda la evaluación de la entrevista de un candidato: los tres comentarios del informe
        /// de finalista que verá el área solicitante. Guardarlo es enviarlo como finalista, así que
        /// el requerimiento avanza de ENTREVISTAS a SELECCION_JEFATURA; el resultado trae esa fase
        /// nueva (null si no se movió).
        /// </summary>
        Task<EvaluacionAccionResultDto> GuardarEvaluacion(int candidatoId, EvaluacionGuardarDto dto, int? userId);

        /// <summary>
        /// Envía al candidato el correo de agradecimiento por no continuar en el proceso y deja su
        /// resultado en NO_PASO (RG-12: no se cierra un "No pasó" sin ese correo). El envío es
        /// best-effort: si el correo falla, el resultado igual queda registrado y se informa en el
        /// mensaje para que GTH reintente.
        /// </summary>
        Task<EvaluacionAccionResultDto> EnviarAgradecimiento(int candidatoId, int? userId);

        /// <summary>
        /// Informe de finalistas de un requerimiento del solicitante (vista "Finalistas enviados por
        /// GTH"). Lanza <see cref="Abril_Backend.Application.Exceptions.AbrilException"/> 404 si no
        /// existe o no le pertenece.
        /// </summary>
        Task<RevisionFinalistasDto> GetRevisionFinalistas(int requerimientoId, int? userId);

        /// <summary>
        /// Registra la decisión final del área solicitante sobre un finalista, avanza el
        /// requerimiento (CERRADO al aprobar; de vuelta a LONG_LIST si se rechazó a todos; se queda
        /// en SELECCION_JEFATURA mientras queden finalistas por decidir) y envía
        /// los correos: el de agradecimiento al finalista rechazado y la notificación a GTH
        /// (tipo FINALISTA_DECISION). Ambos envíos son best-effort.
        /// </summary>
        Task<FinalistaDecisionResultDto> RegistrarDecisionFinalista(
            int requerimientoId, FinalistaDecisionDto dto, int? userId);

        /// <summary>Destinatarios configurados de un correo de reclutamiento (principales + copias) por tipo (SOLICITUD/LONG_LIST).</summary>
        Task<CorreoDestinatariosDto> GetCorreoDestinatarios(string tipoCodigo);

        /// <summary>Guarda (valida y normaliza) los destinatarios de un correo de reclutamiento por tipo.</summary>
        Task SaveCorreoDestinatarios(string tipoCodigo, CorreoDestinatariosDto dto, int? userId);
    }
}
