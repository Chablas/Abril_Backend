using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;

namespace Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Infrastructure.Interfaces
{
    public interface IGestionSalidaRepository
    {
        Task<List<GestionSalidaListItemDto>> GetAll(GestionSalidaFiltersDto filters);

        /// <summary>
        /// Igual que <see cref="GetAll"/> pero ordenado por la columna indicada y paginado, más los
        /// números de las tarjetas contados sobre todo el conjunto filtrado.
        /// </summary>
        Task<GestionSalidaPagedDto> GetPaged(GestionSalidaFiltersDto filters);
        /// <summary>
        /// Datos de los filtros (trabajadores, lugares y árbol de áreas). Cuando
        /// <paramref name="seesAll"/> es false, tanto los trabajadores como el árbol de áreas se
        /// recortan a <paramref name="visibleAreaScopeIds"/> (área del usuario hacia abajo). El
        /// propio trabajador del usuario siempre se incluye en la lista de trabajadores, y también
        /// <paramref name="trabajadoresDeSusObras"/> (con sus áreas en el árbol) cuando el usuario
        /// es residente o administrador de obra.
        /// </summary>
        Task<GestionSalidaFilterDataDto> GetFilterData(
            bool seesAll, List<int> visibleAreaScopeIds, int? currentUserId, List<int> trabajadoresDeSusObras);
        Task Aprobar(int id, int reviewerUserId);

        /// <summary><paramref name="motivoRechazo"/> es opcional; en blanco se guarda null.</summary>
        Task Rechazar(int id, int reviewerUserId, string? motivoRechazo);

        /// <summary>
        /// Crea un registro <c>GaRendicion</c> con la info del PDF subido y marca como rendidas
        /// todas las solicitudes elegibles vinculándolas al rendicion. Todo en una transacción.
        /// Devuelve la planilla creada (id y código REN-AAAA-NNNN) y las solicitudes que se
        /// rindieron: quien rinde desde Solicitud de Salidas la envía a primera revisión en el acto.
        /// </summary>
        Task<(int RendicionId, string Codigo, List<int> SolicitudIds)> CrearRendicionYMarcarBulk(
            IEnumerable<int> ids,
            int userId,
            string pdfUrl,
            string? pdfItemId,
            string pdfFilename,
            int numeroPlanilla);

        /// <summary>Consume el siguiente número de la secuencia <c>seq_planilla_numero</c>.</summary>
        Task<int> GetNextNumeroPlanillaAsync();

        /// <summary>
        /// Lo mínimo para volver a armar el PDF de una planilla existente: su número impreso y
        /// TODAS las salidas que agrupa (de todos sus trabajadores, no solo del que subsana).
        /// Null si la planilla no existe.
        /// </summary>
        Task<RendicionParaRegenerarDto?> GetRendicionParaRegenerar(int rendicionId);

        /// <summary>
        /// Apunta la planilla al PDF nuevo y la deja lista para reenviar a primera revisión
        /// (estado "Lista para enviar", sin sello de envío). El código, el número de planilla y la
        /// observación del jefe se conservan: la rendición es la misma y lo observado sigue siendo
        /// lo que hay que poder contrastar.
        /// </summary>
        Task ReemplazarPdfRendicion(
            int rendicionId, string pdfUrl, string? pdfItemId, string pdfFilename);

        /// <summary>
        /// Link de la carpeta de SharePoint (tabla singleton <c>ga_rendicion_folder</c>) donde se
        /// suben los PDF de planillas de rendición. Null si no hay carpeta configurada.
        /// </summary>
        Task<string?> GetRendicionFolderUrl();

        /// <summary>IDs elegibles (Aprobadas + No rendidas) sin tocar BD. Pre-flight.</summary>
        Task<List<int>> GetEligibleIdsForRendicion(IEnumerable<int> ids);

        /// <summary>
        /// Del set dado, devuelve los IDs que NO pertenecen al trabajador del usuario indicado
        /// (worker → person → user). Se usa como guard cuando el propio trabajador rinde sus salidas
        /// desde el autoservicio: solo puede rendir lo suyo.
        /// </summary>
        Task<List<int>> GetIdsNotOwnedByUser(IEnumerable<int> ids, int userId);

        /// <summary>
        /// Del set dado, devuelve solicitudes que tienen al menos UN trayecto REEMBOLSABLE sin
        /// ninguna captura. A los trayectos sin reembolso no se les pide nada: no entran en la
        /// planilla. (Una solicitud sin trayectos también se incluye como incompleta).
        /// </summary>
        Task<List<int>> GetIdsConTrayectosSinCapturas(IEnumerable<int> ids);

        /// <summary>
        /// Del set dado, devuelve las solicitudes sin NINGÚN trayecto que deje gasto que rendir:
        /// motivo no marcado en Configuración → Motivos, recorrido excluido en Configuración →
        /// Trayectos, o importe resuelto en S/ 0.00 (el tarifario de TI en cero). La planilla no
        /// tendría ni una fila de ellas, así que no hay nada que rendir. Es el bloqueo duro que
        /// acompaña al recorte de la pantalla.
        /// </summary>
        Task<List<int>> GetIdsNoReembolsables(IEnumerable<int> ids);

        /// <summary>
        /// Correos de los dueños de las salidas indicadas, recortadas al alcance de visibilidad
        /// del usuario. Son los destinatarios principales de los avisos de la decisión (aprobada /
        /// rechazada) y la pantalla los usa para nombrarlos en la confirmación.
        /// </summary>
        Task<List<string>> GetCorreosSolicitantes(IEnumerable<int> ids, GestionSalidaFiltersDto scope);

        /// <summary>
        /// Los meses (año, mes de <c>fecha_salida</c>) distintos que abarca el set dado, ordenados.
        /// Una planilla de rendición es de UN solo mes, así que más de un elemento es un error.
        /// </summary>
        Task<List<(int Anio, int Mes)>> GetMesesDeSolicitudes(IEnumerable<int> ids);

        /// <summary>
        /// Valida que las salidas dadas puedan ir en una MISMA planilla: sin mezclar jefaturas con
        /// el resto del equipo y todas bajo un mismo nodo de área por debajo de la gerencia. La
        /// regla vive en <c>AgrupacionRendicionRule</c>, compartida con el Consolidado del S10;
        /// lanza <c>AbrilException</c> 400 con el motivo concreto si el conjunto no se puede juntar.
        /// </summary>
        Task ValidarAgrupacionDeSolicitudes(IEnumerable<int> ids);

        /// <summary>
        /// Feriados y días no laborables (Configuración → Feriados) ya resueltos, para calcular el
        /// plazo de rendición y las fechas de la planilla fuera del repositorio. Trae también el
        /// tope de movilidad, que sale de la misma fila de config.
        /// </summary>
        Task<CalendarioNoLaborable> GetCalendarioNoLaborable();

        /// <summary>
        /// Periodos que cada trabajador ya rindió en OTRAS planillas dentro del rango dado, tomados
        /// del alcance real de cada una (su primera y su última <c>fecha_salida</c>).
        ///
        /// Solo lo necesita el último recurso de <see cref="ImputacionMovilidadPlanilla"/>: cuando
        /// un gasto ya no entra hacia adelante en el mes y hay que retroceder, no puede caer sobre
        /// una semana o una quincena que el trabajador ya rindió. Por eso se pide recién cuando
        /// hace falta y no en cada rendición.
        /// </summary>
        /// <param name="excluirRendicionIds">
        /// Las planillas que se están (re)generando: sus propias salidas no son un periodo ajeno.
        /// Es una colección y no un id porque la planilla grupal cubre varias a la vez.
        /// </param>
        Task<Dictionary<int, List<ImputacionMovilidadPlanilla.PeriodoRendido>>> GetPeriodosRendidos(
            IReadOnlyCollection<int> workerIds, DateOnly desde, DateOnly hasta,
            IReadOnlyCollection<int> excluirRendicionIds);

        /// <summary>
        /// Las salidas de N planillas de rendición, cada una con el código (REN-AAAA-NNNN) de la
        /// planilla a la que pertenece, en un solo roundtrip. Es lo que necesita la planilla de
        /// reembolso, que se arma con todo lo que cubre el Consolidado del S10 y dice en cada fila
        /// de qué rendición sale.
        /// </summary>
        Task<Dictionary<int, string>> GetCodigoRendicionPorSolicitud(IReadOnlyCollection<int> rendicionIds);

        /// <summary>
        /// Detalle completo (cabecera + trayectos con capturas + rendición si existe).
        /// <paramref name="currentUserId"/> resuelve <c>PuedeDecidir</c>: si quien abre el detalle
        /// es el revisor de esa salida y por lo tanto puede aprobarla o rechazarla desde el modal.
        /// </summary>
        Task<GestionSalidaDetalleDto?> GetDetalle(int id, int? currentUserId);

        /// <summary>
        /// Datos para armar la planilla — una fila por TRAYECTO REEMBOLSABLE de las solicitudes
        /// dadas. Los trayectos que no generan reembolso no se imprimen ni suman.
        /// </summary>
        Task<List<RendicionItemDto>> GetRendicionData(List<int> solicitudIds);

        /// <summary>Registra (o limpia) la hora real en la que la persona salió. Solo se actualiza el campo extra; no afecta el flujo principal.</summary>
        Task SetHoraSalidaReal(int solicitudId, TimeOnly? hora, int registradaPorUserId);

        /// <summary>Registra (o limpia) la hora real en la que la persona retornó. Solo se actualiza el campo extra; no afecta el flujo principal.</summary>
        Task SetHoraRetornoReal(int solicitudId, TimeOnly? hora, int registradaPorUserId);

        // La decisión del reembolso, la firma y el pago ya no viven acá: son pasos posteriores
        // a rendir. Los expone IGestionRendicionRepository (revisor) e IReembolsoRepository
        // (Tesorería).
    }
}
