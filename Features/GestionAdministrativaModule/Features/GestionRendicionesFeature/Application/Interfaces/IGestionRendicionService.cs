using Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Application.Interfaces
{
    /// <summary>
    /// "Gestión de Rendiciones": el revisor sobre las planillas de su alcance. Acá vive la PRIMERA
    /// revisión de la planilla, la planilla grupal que prepara el consolidador y el primer
    /// Consolidado del S10, que se sube sobre ella; Gestión de Salidas llega hasta rendir. Decidir y
    /// firmar el reembolso —y reemplazar el consolidado— es de Consolidados, y el pago de Tesorería
    /// (Reembolsos).
    ///
    /// La visibilidad es exactamente la de Gestión de Salidas: mismas salidas, agrupadas por
    /// planilla.
    /// </summary>
    public interface IGestionRendicionService
    {
        Task<GestionRendicionListResultDto> GetAll(GestionRendicionFiltersDto filters);
        Task<GestionRendicionFilterDataDto> GetFilterData(GestionRendicionFiltersDto scope);
        Task<GestionRendicionDetalleDto> GetDetalle(int rendicionId, GestionRendicionFiltersDto scope);

        /// <summary>
        /// El detalle de UNA salida de las planillas del alcance (el ojo de la tabla de salidas del
        /// detalle), para que la jefatura o el consolidador vean sus capturas y montos. Es solo
        /// consulta: 404 si la salida no está rendida o no está en su alcance.
        /// </summary>
        Task<SolicitudSalidaDetalleDto> GetSalidaDetalle(int solicitudId, GestionRendicionFiltersDto scope);

        /// <summary>
        /// Qué correos saldrían si se toma una de las decisiones de la pantalla sobre la selección
        /// indicada, y a quién. Lo piden las confirmaciones —las de los botones masivos y las del
        /// modal de detalle— para nombrar las direcciones reales en vez de prometer "se le avisará
        /// por correo". Lista vacía = esa decisión hoy no manda ningún correo.
        /// </summary>
        Task<List<CorreoAvisoPreviewDto>> GetCorreoPreview(
            CorreoPreviewRequestDto request, GestionRendicionFiltersDto scope);

        /// <summary>
        /// Aprueba u observa la PRIMERA revisión de las planillas seleccionadas (RG-30). Aprobar
        /// habilita al consolidador del área a cargar el Consolidado del S10; observar le pide al
        /// trabajador corregir las capturas y los montos y volver a generar la rendición con el
        /// mismo código.
        ///
        /// La decisión es por planilla y total (RG-19): no se aprueban trayectos por separado.
        /// Avisa a los solicitantes por correo y, al aprobar, también a los consolidadores del área
        /// de que la rendición se suma a las disponibles para consolidar (best-effort).
        /// </summary>
        Task<ReembolsoBulkResultDto> DecidirPrimeraRevision(
            PrimeraRevisionAccionDto accion, bool aprobar, GestionRendicionFiltersDto scope, int reviewerUserId);

        /// <summary>
        /// Prepara la PLANILLA GRUPAL de las planillas indicadas: el papel con el que el consolidador
        /// las registra en el S10, sin firmar. Es el paso anterior a adjuntar el Consolidado del
        /// S10, que después se sube sobre ella. Solo la prepara el consolidador habilitado por TODOS
        /// los trabajadores de esas planillas, igual que el consolidado.
        ///
        /// Las reglas del documento (primera revisión aprobada, sin planilla grupal ni S10, misma
        /// regla de agrupación que el consolidado) las valida el servicio compartido: una planilla
        /// grupal ya preparada no se rehace.
        ///
        /// Le avisa a cada trabajador que su rendición quedó incluida en la planilla grupal
        /// (best-effort, igual que el aviso de rendición consolidada).
        /// </summary>
        Task<PlanillaGrupalDto> PrepararPlanillaGrupal(IReadOnlyCollection<int> rendicionIds, int userId);

        /// <summary>
        /// Adjunta el PRIMER Consolidado del S10 de las planillas indicadas: una o varias, de uno o
        /// de varios trabajadores, de las razones sociales que sean. Solo lo sube el consolidador,
        /// que tiene que estar habilitado por TODOS los trabajadores de esas planillas
        /// (Consolidados → Configuración → Consolidadores).
        ///
        /// Ninguna puede tener ya un consolidado (409): reemplazarlo es de Consolidados
        /// (<c>IConsolidadoService.ReemplazarConsolidado</c>). El resto de las reglas (primera
        /// revisión APROBADA, reembolso por decidir, que sean exactamente las de una planilla grupal
        /// ya preparada) las valida el servicio compartido.
        ///
        /// Adjuntar TAMBIÉN le avisa a la jefatura, en el mismo paso: consolidar es exactamente lo
        /// que deja el reembolso esperando su firma, así que el aviso no es un trámite aparte del
        /// que haya que acordarse. Va best-effort — el resultado dice cómo salió y desde
        /// Consolidados se puede repetir a mano.
        /// </summary>
        /// <param name="montoTotal">
        /// Importe total del consolidado. Tiene que coincidir con la suma de las planillas completas
        /// o se rechaza con 400.
        /// </param>
        /// <param name="numeroReembolso">Número del reembolso del S10 (texto, obligatorio).</param>
        /// <param name="seesAllOverride">Rol que ve toda la organización, para el alcance del aviso.</param>
        Task<ConsolidadoS10UploadResultDto> UploadConsolidadoS10(
            IReadOnlyCollection<int> rendicionIds, IFormFile file, decimal montoTotal, string numeroReembolso,
            int userId, bool seesAllOverride);
    }
}
