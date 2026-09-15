using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Infrastructure.Interfaces
{
    public interface IGestionRendicionRepository
    {
        /// <summary>Planillas visibles para el usuario, ya filtradas.</summary>
        Task<List<GestionRendicionListItemDto>> GetAll(GestionRendicionFiltersDto filters);

        /// <summary>Una planilla con el desglose de sus salidas visibles. Null si no ve ninguna.</summary>
        Task<GestionRendicionDetalleDto?> GetDetalle(int rendicionId, GestionRendicionFiltersDto scope);

        /// <summary>Opciones de los filtros (trabajadores, árbol de áreas y periodos) del alcance.</summary>
        Task<GestionRendicionFilterDataDto> GetFilterData(GestionRendicionFiltersDto scope);

        // ── Primera revisión (el paso anterior al Consolidado del S10) ──

        /// <summary>
        /// Aprueba u observa la PRIMERA revisión de las planillas indicadas. Solo mueve las que
        /// están "En primera revisión" y de las que el usuario ve alguna salida; el resto se ignora
        /// en silencio (la selección de la pantalla puede traer de todo). Devuelve las que sí se
        /// movieron, para avisarles a sus solicitantes.
        ///
        /// Nadie revisa una planilla con salidas propias, salvo que sea su propio revisor (jefe
        /// personalizado apuntándose a sí mismo): en ese caso lanza 403 sin mover nada.
        /// </summary>
        /// <param name="aprobar">true = Aprobada; false = Observada (exige observación, RG-20).</param>
        Task<List<int>> DecidirPrimeraRevision(
            IEnumerable<int> rendicionIds, bool aprobar, string? observacion,
            GestionRendicionFiltersDto scope, int reviewerUserId);

        /// <summary>
        /// Lo que necesitan los correos de la decisión de la primera revisión. Devuelve UNA entrada
        /// por trabajador de la planilla: el aviso va al dueño de las salidas, y una planilla
        /// generada por el revisor puede agrupar a varios. Vacío si no hay a quién avisarle.
        /// </summary>
        Task<List<PrimeraRevisionCorreoInfoDto>> GetPrimeraRevisionCorreoInfo(int rendicionId);

        /// <summary>
        /// Correos de los solicitantes a los que llegaría el aviso de la decisión de la PRIMERA
        /// REVISIÓN de las planillas seleccionadas. Mismo criterio que
        /// <see cref="GetCorreosSolicitantesPorDecidir"/>: recorta por visibilidad y por la
        /// elegibilidad de la escritura (solo planillas esperando la primera revisión).
        /// </summary>
        Task<List<string>> GetCorreosSolicitantesPrimeraRevision(
            IEnumerable<int> rendicionIds, GestionRendicionFiltersDto scope);

        /// <summary>
        /// Trabajadores de las salidas de las planillas indicadas, SIN recortar por visibilidad. Lo
        /// pide la validación de quién puede adjuntarles un Consolidado del S10: el documento cubre
        /// las planillas enteras, así que hay que poder consolidar por todos — también por los que
        /// el usuario no ve.
        /// </summary>
        Task<List<int>> GetWorkerIdsDePlanillas(IReadOnlyCollection<int> rendicionIds);
    }
}
