using Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Application.Dtos;

namespace Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Infrastructure.Interfaces
{
    public interface IControlVencimientosRepository
    {
        Task<List<VecinoLicenciaDto>> GetLicencias();
        Task<VecinoLicenciaDto> CreateLicencia(VecinoLicenciaCreateDto dto, string archivoUrl, string? originalFileName, int userId);

        /// <summary>Licencias cuya fecha de recordatorio ya llegó y aún no se les envió el correo.</summary>
        Task<List<VecinoLicenciaDto>> GetPendientesRecordatorio(DateOnly hoy);

        /// <summary>Marca la licencia como recordatorio enviado (para no reenviar en el siguiente cron).</summary>
        Task MarcarRecordatorioEnviado(int vecinoLicenciaId);
    }
}
