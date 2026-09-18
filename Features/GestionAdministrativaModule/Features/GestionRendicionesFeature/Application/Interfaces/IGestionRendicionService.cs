using Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Application.Interfaces
{
    /// <summary>
    /// "Gestión de Rendiciones": el revisor sobre las planillas de su alcance. Acá vive la PRIMERA
    /// revisión de la planilla y el Consolidado del S10 que la respalda —adjuntarlo o
    /// reemplazarlo—; Gestión de Salidas llega hasta rendir. Decidir y firmar el reembolso es de
    /// Consolidados, y el pago de Tesorería (Reembolsos).
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
        /// Adjunta (o reemplaza) UN Consolidado del S10 para las planillas indicadas: una o varias,
        /// de uno o de varios trabajadores, de las razones sociales que sean. Solo lo sube el
        /// consolidador, que tiene que estar habilitado por TODOS los trabajadores de esas planillas
        /// (Consolidados → Configuración → Consolidadores).
        ///
        /// Las reglas de qué puede ir junto (primera revisión APROBADA, reembolso por decidir, el
        /// documento compartido se reemplaza entero) las valida el servicio compartido.
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
