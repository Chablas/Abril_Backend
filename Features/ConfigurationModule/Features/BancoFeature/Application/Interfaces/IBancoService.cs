using Abril_Backend.Features.ConfigurationModule.Features.BancoFeature.Application.Dtos;

namespace Abril_Backend.Features.ConfigurationModule.Features.BancoFeature.Application.Interfaces
{
    public interface IBancoService
    {
        /// <summary>Catálogo completo de bancos vigentes, con cuántas razones sociales usa cada uno.</summary>
        Task<List<BancoDto>> List();

        Task<BancoDto> Create(BancoUpsertDto dto, int? userId);
        Task<BancoDto> Update(int bancoId, BancoUpsertDto dto, int? userId);
        Task Delete(int bancoId, int? userId);
    }
}
