using Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Application.Dtos;

namespace Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Infrastructure.Interfaces
{
    public interface IControlLicenciasRepository
    {
        /// <summary>Proyectos activos donde Control de Licencias no está oculto por filtro.</summary>
        Task<List<ProjectOptionDto>> GetProyectos();

        /// <summary>Plantilla completa (tipos base + propios del proyecto) con el estado vigente de cada uno.</summary>
        Task<VecinoLicenciaPlantillaResponseDto> GetPlantilla(int projectId);

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
        /// la versión anterior se archiva en el historial antes de sobrescribir.
        /// </summary>
        Task UploadLicencia(int projectId, int tipoId, string archivoUrl, string? originalFileName,
            DateOnly fechaVencimiento, DateOnly fechaRecordatorio, int diasAntes, int userId);

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

        /// <summary>Licencias con archivo cargado cuya fecha de recordatorio ya llegó y no se ha avisado, de todos los proyectos.</summary>
        Task<List<(int VecinoLicenciaControlId, int ProjectId, string TipoDescripcion, DateOnly FechaVencimiento)>> GetPendientesRecordatorio(DateOnly hoy);

        Task MarcarRecordatorioEnviado(int vecinoLicenciaControlId);
    }
}
