using Abril_Backend.Features.GestionAdministrativa.Rendiciones.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.Rendiciones.Application.Interfaces
{
    /// <summary>
    /// "Mis Rendiciones": el autoservicio del trabajador sobre sus planillas ya rendidas. Lo único
    /// que el trabajador hace acá es enviar la planilla a la PRIMERA revisión de su jefatura y
    /// subsanarla si vuelve observada. Desde la primera revisión aprobada todo es del consolidador
    /// de su área —adjuntar el Consolidado del S10 (Gestión de Rendiciones), avisar a la jefatura y
    /// pedir la corrección al Coordinador ERP (Consolidados)—: la pantalla solo muestra en qué va.
    /// </summary>
    public interface IRendicionService
    {
        /// <summary>Planillas propias ya filtradas, con los números de las tarjetas de ese conjunto.</summary>
        Task<RendicionListResultDto> GetByUserId(int userId, RendicionFiltersDto? filters = null);

        /// <summary>
        /// Datos de arranque de la pantalla: las opciones del filtro de periodo y los correos a los
        /// que de verdad van a salir los avisos de "Enviar a revisión" (para mostrarlos al confirmar
        /// el envío). Van juntos porque ninguno de los dos cambia al mover los filtros.
        /// </summary>
        Task<RendicionFilterDataDto> GetFilterData(int userId);

        /// <summary>Detalle de una planilla propia con el desglose de sus salidas.</summary>
        Task<RendicionDetalleDto> GetDetalle(int rendicionId, int userId);

        /// <summary>
        /// Envía la planilla a la PRIMERA revisión de la jefatura. Es el "Enviar" del requerimiento
        /// funcional (RG-30). Rendir desde Solicitud de Salidas ya pasa por acá en el acto (ver
        /// <see cref="RendirYEnviarAPrimeraRevision"/>); desde Mis Rendiciones se envía lo que quedó
        /// "Lista para enviar": la planilla que se volvió a generar tras una observación, la que
        /// rindió el revisor desde Gestión de Salidas o la que no se pudo enviar al rendir.
        ///
        /// Dispara los dos correos del paso: la confirmación al propio solicitante y el aviso al
        /// jefe con los botones de aprobar y observar.
        /// </summary>
        /// <returns>Mensaje para mostrar en la pantalla.</returns>
        Task<string> EnviarAPrimeraRevision(int rendicionId, int userId);

        /// <summary>
        /// «Rendir» de Solicitud de Salidas: rinde las salidas PROPIAS indicadas —genera la planilla
        /// de gasto por movilidad y la guarda en SharePoint, sin devolver el PDF— y la envía en el
        /// acto a la primera revisión con <see cref="EnviarAPrimeraRevision"/>.
        ///
        /// Si rendir falla no se escribe nada (lanza como siempre). Si lo que falla es el envío, la
        /// rendición igual queda hecha, "Lista para enviar", y el resultado lo dice.
        /// </summary>
        Task<RendirYEnviarResultDto> RendirYEnviarAPrimeraRevision(IReadOnlyCollection<int> solicitudIds, int userId);

        /// <summary>
        /// Vuelve a generar el PDF de una planilla OBSERVADA en primera revisión, con las capturas
        /// y los montos como quedaron después de corregirlos. La rendición es la misma —conserva su
        /// código REN-AAAA-NNNN y su número de planilla— y queda "Lista para enviar" para que el
        /// trabajador la reenvíe.
        /// </summary>
        /// <returns>Los bytes del PDF nuevo, para descargarlo desde la pantalla.</returns>
        Task<byte[]> RegenerarPlanilla(int rendicionId, int userId);
    }
}
