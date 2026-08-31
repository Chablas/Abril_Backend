using Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Application.Dtos;
using Microsoft.AspNetCore.Http;

namespace Abril_Backend.Features.VecinosModule.Features.ControlLicenciasFeature.Application.Interfaces
{
    public interface IControlLicenciasService
    {
        Task<List<ProjectOptionDto>> GetProyectos();
        Task<VecinoLicenciaPlantillaResponseDto> GetPlantilla(int projectId);

        /// <summary>Plantilla combinada de todos los proyectos (o los indicados), para la vista "todos" de Plantilla.</summary>
        Task<VecinoLicenciaPlantillaResponseDto> GetPlantillaTodos(List<int>? projectIds);
        Task<VecinoLicenciaTipoDto> AddTipo(int projectId, VecinoLicenciaTipoCreateDto dto, int userId);
        Task UploadLicencia(int projectId, int tipoId, VecinoLicenciaUploadDto dto, IFormFile file, int userId);
        Task<VecinoLicenciaRecordatorioDto> AddRecordatorio(int projectId, int tipoId, VecinoLicenciaRecordatorioCreateDto dto, int userId);
        Task DeleteRecordatorio(int recordatorioId, int userId);
        Task SetNoAplica(int projectId, int tipoId, bool noAplica, int userId);
        Task<List<VecinoLicenciaHistorialItemDto>> GetHistorial(int projectId, int tipoId);

        /// <summary>Catálogo base (plantilla común a todos los proyectos), para administrarlo.</summary>
        Task<List<VecinoLicenciaTipoDto>> GetCatalogoBase();
        Task<VecinoLicenciaTipoDto> AddTipoBase(VecinoLicenciaTipoBaseUpsertDto dto, int userId);
        Task<VecinoLicenciaTipoDto> UpdateTipo(int tipoId, VecinoLicenciaTipoBaseUpsertDto dto, int userId);
        Task DeleteTipo(int tipoId, int userId);

        Task<VecinoLicenciaDestinatariosResponseDto> GetDestinatarios(int projectId);
        Task<VecinoLicenciaDestinatarioDto> AddDestinatario(int projectId, VecinoLicenciaDestinatarioUpsertDto dto, int userId);
        Task DeleteDestinatario(int destinatarioId, int userId);

        /// <summary>Agrega una fecha de visita al Anexo H de la licencia vigente de un tipo. Recordatorio fijo: 2 días antes.</summary>
        Task<VecinoLicenciaVisitaDto> AddVisita(int projectId, int tipoId, VecinoLicenciaVisitaCreateDto dto, int userId);
        Task DeleteVisita(int visitaId, int userId);

        Task UpdateFechas(int projectId, int tipoId, VecinoLicenciaFechasUpdateDto dto, int userId);

        /// <summary>Dashboard gerencial: todos los proyectos (o los indicados), ordenado de más a menos crítico.</summary>
        Task<VecinoLicenciaDashboardResponseDto> GetDashboard(List<int>? projectIds);

        /// <summary>Cron: envía los recordatorios de licencias cuya fecha de recordatorio ya llegó, en todos los proyectos.</summary>
        Task<RecordatoriosResultDto> ProcesarRecordatorios();
    }
}
