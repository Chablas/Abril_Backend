using Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Application.Dtos;

namespace Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Infrastructure.Interfaces
{
    public interface IControlLicenciasRepository
    {
        /// <summary>Proyectos activos donde Control de Licencias no está oculto por filtro.</summary>
        Task<List<ProjectOptionDto>> GetProyectos();

        /// <summary>Plantilla completa (tipos base + propios del proyecto) con el estado vigente de cada uno.</summary>
        Task<VecinoLicenciaPlantillaResponseDto> GetPlantilla(int projectId);

        /// <summary>
        /// Plantilla combinada de los proyectos indicados (o todos si <paramref name="projectIds"/> es null/vacío),
        /// con cada item marcado con su ProjectId/ProjectDescription. Para la vista "todos los proyectos" de Plantilla.
        /// </summary>
        Task<VecinoLicenciaPlantillaResponseDto> GetPlantillaTodos(List<int>? projectIds);

        /// <summary>true si el tipo es base o pertenece a este proyecto (evita subir a un tipo de otro proyecto).</summary>
        Task<bool> TipoAplicaAProyecto(int projectId, int tipoId);

        /// <summary>Agrega un tipo de licencia propio de este proyecto (no se ve en otros proyectos).</summary>
        Task<VecinoLicenciaTipoDto> AddTipo(int projectId, string descripcion, int? diasAntesDefault, int userId);

        /// <summary>Catálogo base (visible en todos los proyectos), para administrarlo.</summary>
        Task<List<VecinoLicenciaTipoDto>> GetCatalogoBase();

        /// <summary>Agrega un tipo a la plantilla base: aparece de inmediato en todos los proyectos.</summary>
        Task<VecinoLicenciaTipoDto> AddTipoBase(string descripcion, int? diasAntesDefault, int userId);

        /// <summary>Edita un tipo (base o de un proyecto): descripción y días de antelación por defecto.</summary>
        Task<VecinoLicenciaTipoDto> UpdateTipo(int tipoId, string descripcion, int? diasAntesDefault, int userId);

        /// <summary>Elimina (soft delete) un tipo del catálogo (base o propio de un proyecto).</summary>
        Task DeleteTipo(int tipoId, int userId);

        /// <summary>
        /// Sube/reemplaza el documento vigente de un tipo para un proyecto. Si ya había un archivo,
        /// la versión anterior se archiva en el historial antes de sobrescribir. Crea un recordatorio
        /// por cada valor de <paramref name="diasAntesRecordatorio"/>.
        /// </summary>
        Task UploadLicencia(int projectId, int tipoId, string archivoUrl, string? originalFileName,
            DateOnly fechaVencimiento, List<int> diasAntesRecordatorio, int userId);

        /// <summary>Agrega un recordatorio adicional a la licencia vigente de un tipo.</summary>
        Task<VecinoLicenciaRecordatorioDto> AddRecordatorio(int projectId, int tipoId, int diasAntes, int userId);

        /// <summary>Elimina (soft delete) un recordatorio puntual.</summary>
        Task DeleteRecordatorio(int recordatorioId, int userId);

        /// <summary>Marca/desmarca "No aplica" para un tipo de un proyecto.</summary>
        Task SetNoAplica(int projectId, int tipoId, bool noAplica, int userId);

        /// <summary>Historial de versiones anteriores de la licencia vigente de un tipo en un proyecto.</summary>
        Task<List<VecinoLicenciaHistorialItemDto>> GetHistorial(int projectId, int tipoId);

        /// <summary>
        /// Destinatarios de un proyecto: automáticos (Residente/Coordinador SSOMA/Administración,
        /// resueltos desde la ficha del proyecto — mismo criterio que EMOs) + adicionales a mano.
        /// </summary>
        Task<VecinoLicenciaDestinatariosResponseDto> GetDestinatarios(int projectId);

        /// <summary>Solo los correos automáticos ya resueltos (para el envío del cron).</summary>
        Task<List<string>> ResolverDestinatariosAutomaticos(int projectId);

        Task<VecinoLicenciaDestinatarioDto> AddDestinatario(int projectId, string rol, string email, int userId);

        /// <summary>Elimina (soft delete) un destinatario adicional; queda registrado en auditoría quién lo borró.</summary>
        Task DeleteDestinatario(int destinatarioId, int userId);

        /// <summary>Correos adicionales de un proyecto (sin los automáticos), para el envío del cron.</summary>
        Task<List<string>> GetDestinatariosAdicionales(int projectId);

        /// <summary>Recordatorios de licencias con archivo cargado cuya fecha ya llegó y no se han avisado, de todos los proyectos.</summary>
        Task<List<VecinoLicenciaRecordatorioPendienteDto>> GetPendientesRecordatorio(DateOnly hoy);

        Task MarcarRecordatorioEnviado(int recordatorioId);

        /// <summary>Agrega una fecha de visita de Anexo H a la licencia vigente de un tipo. Recordatorio fijo: 2 días antes.</summary>
        Task<VecinoLicenciaVisitaDto> AddVisita(int projectId, int tipoId, DateOnly fechaVisita, string? observacion, int userId);

        /// <summary>Elimina (soft delete) una visita puntual.</summary>
        Task DeleteVisita(int visitaId, int userId);

        /// <summary>Visitas de Anexo H con recordatorio (2 días antes) cuya fecha ya llegó y no se han avisado, de todos los proyectos.</summary>
        Task<List<VecinoLicenciaVisitaPendienteDto>> GetPendientesVisita(DateOnly hoy);

        Task MarcarVisitaRecordatorioEnviado(int visitaId);

        /// <summary>Residente y Administrador del proyecto (project.email_residente / project.email_coord_admin), para el recordatorio de visita.</summary>
        Task<List<string>> ResolverDestinatariosVisita(int projectId);

        /// <summary>Edita las fechas ampliadas del dashboard (inscripción/inicio/renovación) y Mes Activo.</summary>
        Task UpdateFechas(int projectId, int tipoId, VecinoLicenciaFechasUpdateDto dto, int userId);

        /// <summary>
        /// Dashboard gerencial: un renglón por (proyecto, tipo) de los proyectos indicados (o todos si
        /// <paramref name="projectIds"/> es null/vacío), con semáforo de criticidad ya calculado.
        /// </summary>
        Task<VecinoLicenciaDashboardResponseDto> GetDashboard(List<int>? projectIds);
    }
}
