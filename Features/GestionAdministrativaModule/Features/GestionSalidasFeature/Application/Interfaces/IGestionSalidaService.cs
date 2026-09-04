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
        Task Aprobar(int id, int reviewerUserId);
        Task Rechazar(int id, int reviewerUserId);

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

        /// <summary>Detalle de una solicitud para el modal — devuelve null si no existe.</summary>
        Task<GestionSalidaDetalleDto?> GetDetalle(int id);

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
