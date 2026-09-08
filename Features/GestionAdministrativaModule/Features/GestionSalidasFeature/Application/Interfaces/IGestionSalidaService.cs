using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Interfaces
{
    public interface IGestionSalidaService
    {
        Task<List<GestionSalidaListItemDto>> GetAll(GestionSalidaFiltersDto filters);

        /// <summary>
        /// Tabla ordenada y paginada (la vista principal de gestión de salidas), más los números de
        /// las tarjetas contados sobre todo el conjunto filtrado.
        /// </summary>
        Task<GestionSalidaPagedDto> GetPaged(GestionSalidaFiltersDto filters);
        /// <summary>
        /// Datos de los filtros. El árbol de áreas se recorta al alcance de visibilidad del
        /// usuario: quien ve todo (GTH / recepción) recibe el árbol completo; un gerente recibe
        /// su gerencia + descendientes; un jefe recibe su área + subáreas. Así el desplegable en
        /// cascada arranca en el nodo tope que cada usuario controla, no siempre en la gerencia.
        /// </summary>
        Task<GestionSalidaFilterDataDto> GetFilterData(int? currentUserId, bool seesAllOverride);
        Task<byte[]> GetExcel(GestionSalidaFiltersDto filters);
        /// <summary>
        /// Aprueba una solicitud Pendiente. Solo la puede aprobar su revisor (403 en caso
        /// contrario) — ver la regla en <c>GestionSalidaRepository.EnsureEsElRevisorAsync</c>.
        /// </summary>
        Task Aprobar(int id, int reviewerUserId);

        /// <summary>
        /// Rechaza una solicitud Pendiente o Aprobada aún no rendida. Mismo guard de revisor que
        /// <see cref="Aprobar"/>. <paramref name="motivoRechazo"/> es opcional: si viene, se guarda
        /// y sale en el correo de rechazo al solicitante; en blanco se guarda null.
        /// </summary>
        Task Rechazar(int id, int reviewerUserId, string? motivoRechazo);

        /// <summary>
        /// El propio solicitante cancela una salida SUYA que esté Pendiente. Reutiliza la misma
        /// lógica de Solicitud de Salidas (guard de propiedad + estado). 403 si es de otro, 400 si
        /// no está Pendiente.
        /// </summary>
        Task Cancelar(int id, int userId);
        /// <summary>
        /// Marca solicitudes elegibles como Rendidas y genera la planilla de gasto por movilidad (PDF).
        /// Devuelve los bytes del PDF + cuántas se procesaron.
        /// </summary>
        /// <param name="ownerUserId">
        /// Si se indica, actúa como guard: todas las solicitudes deben pertenecer al trabajador de ese
        /// usuario (rendición desde el autoservicio del trabajador). Null = sin restricción (Gestión de Salidas).
        /// </param>
        Task<(byte[] Pdf, int Count)> RendirYGenerarPlanilla(IEnumerable<int> ids, int userId, int? ownerUserId = null);

        /// <summary>
        /// Rinde de una sola vez todas las salidas del MES ANTERIOR al actual (por fecha de salida,
        /// en hora de Perú) que estén listas: aprobadas, no rendidas y con todos sus trayectos
        /// cubiertos (captura con monto, o catálogo para TI). Las que no cumplen se ignoran.
        /// Devuelve la planilla generada + cuántas se rindieron.
        /// </summary>
        /// <param name="filters">
        /// Filtros vigentes de la pantalla (trabajador, área, proyecto). Se respetan tal cual: la
        /// acción rinde lo que el usuario está viendo. El estado, el rango de fechas y el filtro
        /// "Hoy" los fija el propio método.
        /// </param>
        Task<(byte[] Pdf, int Count)> RendirMes(GestionSalidaFiltersDto filters, int? anio, int? mes, int userId);

        /// <summary>
        /// Vuelve a generar el PDF de una planilla que YA existe, con los montos y las capturas
        /// como están ahora. Es lo que cierra la subsanación de una rendición observada en primera
        /// revisión: la fila de <c>ga_rendicion</c> es la misma —así conserva su código
        /// REN-AAAA-NNNN y su número de planilla— y lo que se reemplaza es el archivo.
        ///
        /// Vive acá y no en Mis Rendiciones porque el armado del PDF es de esta feature: el
        /// documento cubre la planilla entera (todas sus salidas, de todos sus trabajadores),
        /// aunque la subsanación la dispare un trabajador sobre sus propias salidas.
        ///
        /// No valida el estado ni la propiedad: eso lo hace quien la llama (ver
        /// <c>IRendicionService.RegenerarPlanilla</c>). Deja la planilla lista para reenviar.
        /// </summary>
        /// <returns>Los bytes del PDF nuevo, para que la pantalla lo pueda descargar.</returns>
        Task<byte[]> RegenerarPlanilla(int rendicionId, int userId);

        /// <summary>
        /// Detalle de una solicitud para el modal — devuelve null si no existe.
        /// <paramref name="currentUserId"/> solo se usa para resolver <c>PuedeDecidir</c> (si quien
        /// mira es el revisor); el detalle en sí es el mismo para todos.
        /// </summary>
        Task<GestionSalidaDetalleDto?> GetDetalle(int id, int? currentUserId);

        /// <summary>Registra (o limpia) la hora real de salida. Para uso del rol USUARIO DE RECEPCIÓN.</summary>
        Task SetHoraSalidaReal(int id, TimeOnly? hora, int registradaPorUserId);

        /// <summary>Registra (o limpia) la hora real de retorno. Para uso del rol USUARIO DE RECEPCIÓN.</summary>
        Task SetHoraRetornoReal(int id, TimeOnly? hora, int registradaPorUserId);

        // El Consolidado del S10, la decisión del reembolso y la firma ya no viven acá: son
        // pasos POSTERIORES a rendir y los expone IGestionRendicionService (Gestión de
        // Rendiciones). El pago es de Tesorería y vive en Reembolsos (IReembolsoService). Esta
        // pantalla llega hasta rendir.
    }
}
