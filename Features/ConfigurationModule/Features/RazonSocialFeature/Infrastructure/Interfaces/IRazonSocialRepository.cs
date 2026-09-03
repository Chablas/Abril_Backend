using Abril_Backend.Features.ConfigurationModule.Features.RazonSocialFeature.Application.Dtos;

namespace Abril_Backend.Features.ConfigurationModule.Features.RazonSocialFeature.Infrastructure.Interfaces
{
    public interface IRazonSocialRepository
    {
        /// <summary>Razones sociales vigentes + catálogo de bancos, en una sola petición.</summary>
        Task<RazonSocialBandejaDto> GetBandeja();

        Task<RazonSocialDto> Create(RazonSocialCreateDto dto, int? userId);
        Task<RazonSocialDto> Update(int contributorId, RazonSocialUpdateDto dto, int? userId);
    }
}
