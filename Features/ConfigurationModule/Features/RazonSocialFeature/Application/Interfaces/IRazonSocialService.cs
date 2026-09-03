using Abril_Backend.Features.ConfigurationModule.Features.RazonSocialFeature.Application.Dtos;
using Abril_Backend.Shared.Services.Sunat.Dtos;

namespace Abril_Backend.Features.ConfigurationModule.Features.RazonSocialFeature.Application.Interfaces
{
    public interface IRazonSocialService
    {
        /// <summary>Razones sociales + catálogo de bancos: la carga inicial de la pantalla.</summary>
        Task<RazonSocialBandejaDto> GetBandeja();

        /// <summary>Consulta de RUC a SUNAT para el alta.</summary>
        Task<SunatContributorDto?> ConsultarRuc(string ruc);

        Task<RazonSocialDto> Create(RazonSocialCreateDto dto, int? userId);
        Task<RazonSocialDto> Update(int contributorId, RazonSocialUpdateDto dto, int? userId);
    }
}
