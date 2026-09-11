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
        /// propio trabajador del usuario siempre se incluye en la lista de trabajadores.
        /// </summary>
        Task<GestionSalidaFilterDataDto> GetFilterData(bool seesAll, List<int> visibleAreaScopeIds, int? currentUserId);
        Task Aprobar(int id, int reviewerUserId);

        /// <summary><paramref name="motivoRechazo"/> es opcional; en blanco se guarda null.</summary>
        Task Rechazar(int id, int reviewerUserId, string? motivoRechazo);

        /// <summary>
        /// Crea un registro <c>GaRendicion</c> con la info del PDF subido y marca como rendidas
        /// todas las solicitudes elegibles vinculándolas al rendicion. Todo en una transacción.
        /// </summary>
        Task<List<int>> CrearRendicionYMarcarBulk(
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
        /// Del set dado, devuelve solicitudes que tienen al menos UN trayecto SIN ninguna captura.
        /// (Una solicitud sin trayectos también se incluye como incompleta).
        /// </summary>
        Task<List<int>> GetIdsConTrayectosSinCapturas(IEnumerable<int> ids);

        /// <summary>
        /// Del set dado, devuelve las solicitudes cuyos trayectos NO llevan ningún motivo marcado
        /// como reembolsable en Configuración → Motivos: no generan gasto de movilidad y por lo
        /// tanto no hay nada que rendir. Es el bloqueo duro que acompaña al recorte de la pantalla.
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
        /// Feriados y días no laborables (Configuración → Feriados) ya resueltos, para calcular el
        /// plazo de rendición y las fechas de la planilla fuera del repositorio. Trae también el
        /// tope de movilidad, que sale de la misma fila de config.
        /// </summary>
        Task<CalendarioNoLaborable> GetCalendarioNoLaborable();

        /// <summary>
        /// Tramos que cada trabajador ya rindió en OTRAS planillas dentro del rango dado, tomados
        /// del alcance real de cada una (su primera y su última <c>fecha_salida</c>).
        ///
        /// Solo lo necesita el último recurso de <see cref="ImputacionMovilidadPlanilla"/>: cuando
        /// un gasto ya no entra hacia adelante en el mes y hay que retroceder, no puede caer sobre
        /// una semana o una quincena que el trabajador ya rindió. Por eso se pide recién cuando
        /// hace falta y no en cada rendición.
        /// </summary>
        /// <param name="excluirRendicionId">
        /// La planilla que se está regenerando: sus propias salidas no son un periodo ajeno.
        /// </param>
        Task<Dictionary<int, List<ImputacionMovilidadPlanilla.PeriodoRendido>>> GetPeriodosRendidos(
            IReadOnlyCollection<int> workerIds, DateOnly desde, DateOnly hasta, int? excluirRendicionId);

        /// <summary>
        /// Detalle completo (cabecera + trayectos con capturas + rendición si existe).
        /// <paramref name="currentUserId"/> resuelve <c>PuedeDecidir</c>: si quien abre el detalle
        /// es el revisor de esa salida y por lo tanto puede aprobarla o rechazarla desde el modal.
        /// </summary>
        Task<GestionSalidaDetalleDto?> GetDetalle(int id, int? currentUserId);

        /// <summary>Datos para armar la planilla — una fila por TRAYECTO de las solicitudes dadas.</summary>
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
