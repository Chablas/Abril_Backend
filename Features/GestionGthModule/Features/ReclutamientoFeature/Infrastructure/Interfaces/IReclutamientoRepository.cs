using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces
{
    public interface IReclutamientoRepository
    {
        Task<ReclutamientoFormDataDto> GetFormData(int? userId);

        /// <summary>
        /// Área (nombre + area_scope) del worker vinculado al usuario. (null, null, null) si no
        /// resuelve. El nombre se deriva del árbol de áreas — el nodo de <c>area_scope_id</c> o su
        /// primer ancestro que no sea gerencia — y no de <c>workers.area</c>, texto congelado.
        /// </summary>
        Task<(string? AreaNombre, int? AreaScopeId, int? WorkerId)> ResolveSolicitante(int userId);

        /// <summary>
        /// Link de la carpeta de SharePoint donde se suben los sustentos (singleton
        /// gth_sustento_folder, primera fila vigente). Se define por BD: dev y prod
        /// apuntan a bibliotecas distintas. null si no está configurada.
        /// </summary>
        Task<string?> GetSustentoFolderUrl();

        /// <summary>
        /// Persiste la solicitud + un requerimiento por vacante, generando el código
        /// REQ-AAAA-NNNN correlativo por año. Devuelve el id de la solicitud y los códigos creados.
        /// </summary>
        Task<SolicitudPersonalCreateResultDto> Create(GthSolicitud solicitud, List<VacanteCreateDto> vacantes, int? userId);

        /// <summary>
        /// Bandeja de la vista de GTH: tarjeta "En proceso" + tabla de solicitudes de contratación de
        /// toda la organización (todos los requerimientos vigentes), en 1 roundtrip.
        /// </summary>
        Task<BandejaReclutamientoDto> GetBandeja();

        /// <summary>
        /// Actualiza la prioridad de un requerimiento vigente. Lanza <see cref="Abril_Backend.Application.Exceptions.AbrilException"/>
        /// 400 si la prioridad no es válida y 404 si el requerimiento no existe.
        /// </summary>
        Task UpdatePrioridad(int requerimientoId, int prioridadId, int? userId);

        /// <summary>
        /// Detalle de seguimiento de un requerimiento del usuario (cabecera + fases del pipeline con su
        /// estado ya calculado), en 1 roundtrip. Devuelve null si el requerimiento no existe o no le
        /// pertenece al usuario.
        /// </summary>
        Task<SeguimientoDto?> GetSeguimiento(int requerimientoId, int userId);

        /// <summary>
        /// Detalle de un requerimiento para la vista de GTH (modal del ojo): cabecera + asignación
        /// interna + catálogos con cupos + canales de publicación, en 1 roundtrip. Devuelve null si
        /// el requerimiento no existe.
        /// </summary>
        Task<DetalleRequerimientoGthDto?> GetDetalleGth(int requerimientoId);

        /// <summary>
        /// Guarda la asignación interna de GTH (responsable, tipo de proceso, prioridad y razón
        /// social) reemplazando los 4 campos. Lanza <see cref="Abril_Backend.Application.Exceptions.AbrilException"/>
        /// 400 si algún id no es válido y 404 si el requerimiento no existe.
        /// </summary>
        Task UpdateAsignacionGth(int requerimientoId, AsignacionGthUpdateDto dto, int? userId);

        /// <summary>
        /// Reemplaza el conjunto de canales donde se registró la publicación de la vacante
        /// (reconciliación: agrega los nuevos, da de baja los quitados) y avanza el
        /// requerimiento a la fase PUBLICACION si aún no la alcanzó. Devuelve el estado resultante.
        /// </summary>
        Task<EstadoRequerimientoResultDto> ReplacePublicaciones(int requerimientoId, List<int> canalIds, int? userId);

        /// <summary>
        /// Avanza el requerimiento de la fase PUBLICACION a LONG_LIST (inicio de la revisión de CV).
        /// Idempotente si ya está en Long list o más adelante. Lanza
        /// <see cref="Abril_Backend.Application.Exceptions.AbrilException"/> 400 si la vacante aún
        /// no está publicada y 404 si el requerimiento no existe. Devuelve el estado resultante.
        /// </summary>
        Task<EstadoRequerimientoResultDto> IniciarRevisionCv(int requerimientoId, int? userId);

        /// <summary>
        /// Cabecera + estado del requerimiento para armar el correo de la long list (solo lectura,
        /// no cambia estado). Valida que la revisión de CV ya inició (fase LONG_LIST o posterior).
        /// Lanza <see cref="Abril_Backend.Application.Exceptions.AbrilException"/> 400 si aún no
        /// hay long list y 404 si el requerimiento no existe.
        /// </summary>
        Task<LongListEnvioContextoDto> GetLongListEnvioContexto(int requerimientoId);

        /// <summary>
        /// Persiste la long list (reemplaza los candidatos vigentes del requerimiento por el nuevo
        /// conjunto, con sus CVs ya subidos a SharePoint) y avanza el requerimiento a
        /// LONG_LIST_ENVIADA (idempotente si ya lo está). Se llama tras enviar el correo con éxito.
        /// Devuelve el estado resultante.
        /// </summary>
        Task<EstadoRequerimientoResultDto> GuardarLongListCandidatos(
            int requerimientoId, List<LongListCandidatoPersistDto> candidatos, int? userId);

        /// <summary>
        /// Panel de la vista del solicitante en 1 roundtrip: tarjetas de "Gestión de candidatos"
        /// (requerimientos del usuario con long list enviada, pendientes de revisar) + la tabla
        /// "Mis solicitudes de vacante".
        /// </summary>
        Task<SolicitantePanelDto> GetSolicitantePanel(int userId);

        /// <summary>
        /// Revisión de la long list de un requerimiento del usuario (cabecera + candidatos con su CV),
        /// en 1 roundtrip. Devuelve null si el requerimiento no existe, no le pertenece al usuario o su
        /// long list aún no fue enviada.
        /// </summary>
        Task<RevisionLongListDto?> GetRevisionLongList(int requerimientoId, int userId);

        /// <summary>
        /// Registra la decisión del solicitante sobre la long list (aprobar/rechazar por candidato) y
        /// avanza el requerimiento: a LONG_LIST_APROBADA si aprobó al menos uno, o de vuelta a LONG_LIST
        /// si rechazó a todos (para que GTH envíe una nueva long list; los rechazados quedan grabados).
        /// Scope: solo el solicitante dueño y solo si el requerimiento está en LONG_LIST_ENVIADA. Lanza
        /// <see cref="Abril_Backend.Application.Exceptions.AbrilException"/> 404 si no existe/no le
        /// pertenece, 409 si ya no está pendiente de revisión y 400 si faltan decisiones. Devuelve el
        /// contexto para el correo (cabecera + candidatos con su decisión) además del estado resultante.
        /// </summary>
        Task<LongListDecisionContextoDto> RegistrarDecisionLongList(
            int requerimientoId, List<CandidatoDecisionDto> decisiones, int userId);

        /// <summary>
        /// Marca/desmarca el check informativo del Multitest de un candidato (con su trazabilidad).
        /// Lanza <see cref="Abril_Backend.Application.Exceptions.AbrilException"/> 404 si el candidato
        /// no existe.
        /// </summary>
        Task SetMultitest(int candidatoId, bool realizado, int? userId);

        /// <summary>
        /// Avanza el requerimiento de LONG_LIST_APROBADA a ENTREVISTAS. Valida los requisitos del
        /// paso: Multitest marcado en todos los candidatos aprobados, todos sus formularios del
        /// postulante ya revisados (aprobados o rechazados) y al menos uno aprobado. Idempotente si
        /// ya está en Entrevistas o más adelante. Lanza
        /// <see cref="Abril_Backend.Application.Exceptions.AbrilException"/> 400 si falta algún
        /// requisito y 404 si el requerimiento no existe. Devuelve el estado resultante.
        /// </summary>
        Task<EstadoRequerimientoResultDto> ContinuarAEntrevistas(int requerimientoId, int? userId);

        /// <summary>
        /// Programa (o reprograma) la entrevista de un candidato con formulario APROBADO: crea o
        /// actualiza su única fila vigente en <c>gth_entrevista</c> y resuelve el correo del
        /// postulante al que se envía la invitación. Lanza
        /// <see cref="Abril_Backend.Application.Exceptions.AbrilException"/> 404 si el candidato no
        /// existe y 400 si su formulario no está aprobado, si el lugar no es válido o si no tiene
        /// correo. Devuelve el contexto para armar el correo.
        /// </summary>
        Task<EntrevistaEnvioContextoDto> GuardarEntrevista(
            int candidatoId, DateOnly fecha, TimeOnly hora, int lugarId, int? userId,
            string nuevoToken);

        /// <summary>
        /// Registra la respuesta del candidato a su citación (CONFIRMADA / RECHAZADA) a partir del
        /// token público del correo. Idempotente: volver a pulsar el mismo botón deja todo igual y
        /// lo avisa con <c>YaHabiaRespondidoLoMismo</c> para no reenviarle a GTH un aviso repetido;
        /// pulsar el otro botón sí cambia la respuesta (el candidato se puede arrepentir). Lanza
        /// <see cref="Abril_Backend.Application.Exceptions.AbrilException"/> 404 si el token no
        /// corresponde a ninguna entrevista vigente y 500 si falta el catálogo de respuestas.
        /// </summary>
        Task<EntrevistaRespuestaContextoDto> RegistrarRespuestaEntrevista(string token, string respuestaCodigo);

        /// <summary>
        /// Guarda la evaluación de la entrevista de un candidato (comentarios del informe),
        /// creando su única fila vigente en <c>gth_candidato_evaluacion</c> si no
        /// existía, y lo deja en PASO: guardar el informe es enviarlo como finalista, así que el
        /// requerimiento avanza de ENTREVISTAS a SELECCION_JEFATURA (la decisión pasa al área
        /// solicitante). No mueve la fase desde ninguna otra, para no retroceder un proceso que ya
        /// cerró o que volvió a long list. Lanza
        /// <see cref="Abril_Backend.Application.Exceptions.AbrilException"/> 404 si
        /// el candidato no existe, 400 si aún no se le envió la invitación a la entrevista y 400
        /// si ya no continúa (resultado NO_PASO: su evaluación quedó cerrada).
        /// </summary>
        Task<EvaluacionGuardadaDto> GuardarEvaluacion(int candidatoId, EvaluacionGuardarDto dto, int? userId);

        /// <summary>
        /// Marca al candidato como NO_PASO (no continúa) y registra la trazabilidad del correo de
        /// agradecimiento (a qué correo, cuándo y quién). Mismas validaciones que
        /// <see cref="GuardarEvaluacion"/> salvo la del resultado cerrado: reenviar el correo es
        /// válido si el envío anterior falló. Devuelve el contexto para armar el correo.
        /// </summary>
        Task<AgradecimientoEnvioContextoDto> RegistrarAgradecimiento(int candidatoId, int? userId);

        /// <summary>
        /// Informe de finalistas de un requerimiento del usuario (cabecera + candidatos evaluados
        /// que siguen en carrera, con sus comentarios y CV), en 1 roundtrip por bloque.
        /// Devuelve null si el requerimiento no existe o no le pertenece al usuario.
        /// </summary>
        Task<RevisionFinalistasDto?> GetRevisionFinalistas(int requerimientoId, int userId);

        /// <summary>
        /// Registra la decisión final del área solicitante sobre un finalista y mueve el
        /// requerimiento: aprobar lo deja en CERRADO (el seleccionado pasa a onboarding); rechazar
        /// lo deja en SELECCION_JEFATURA mientras queden finalistas por decidir y lo devuelve a
        /// LONG_LIST cuando ya no queda ninguno (GTH deberá enviar una nueva long list). Al rechazar
        /// también registra el envío del correo de agradecimiento. Scope: solo el solicitante dueño
        /// y solo con el requerimiento en SELECCION_JEFATURA. Lanza
        /// <see cref="Abril_Backend.Application.Exceptions.AbrilException"/> 404 si no existe o el
        /// candidato no es finalista, y 409 si el proceso ya salió de esa fase o el finalista ya
        /// estaba decidido. Devuelve el contexto para armar los correos.
        /// </summary>
        Task<FinalistaDecisionContextoDto> RegistrarDecisionFinalista(
            int requerimientoId, int candidatoId, bool aprobado, int userId);

        /// <summary>Destinatarios vigentes del correo del tipo indicado (SOLICITUD / LONG_LIST): principales + copias.</summary>
        Task<CorreoDestinatariosDto> GetCorreoDestinatarios(string tipoCodigo);

        /// <summary>
        /// Gerente del área de un solicitante: parte de su <c>workers.area_scope_id</c> y sube por
        /// el árbol de áreas hasta el primer nodo con un trabajador ACTIVO de categoría GERENTE.
        /// Sube por el árbol porque los gerentes están registrados en el nodo "Área de Gerencia"
        /// y no en el área estándar de la que cuelga el solicitante. null si no hay ninguno.
        /// </summary>
        Task<GerenteAreaDto?> GetGerenteDeArea(int? areaScopeId);

        /// <summary>
        /// Reemplaza el conjunto de destinatarios vigentes del tipo indicado por el nuevo
        /// (reconciliación: conserva los que siguen, da de baja los quitados, agrega los nuevos).
        /// Los correos ya vienen normalizados (minúsculas) y sin duplicados desde el servicio.
        /// </summary>
        Task ReplaceCorreoDestinatarios(string tipoCodigo, List<string> principales, List<string> copias, int? userId);
    }
}
