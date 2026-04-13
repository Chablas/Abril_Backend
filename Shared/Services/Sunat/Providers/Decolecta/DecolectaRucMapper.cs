using Abril_Backend.Shared.Services.Sunat.Dtos;

namespace Abril_Backend.Shared.Services.Sunat.Providers.Decolecta
{
    internal static class DecolectaRucMapper
    {
        internal static SunatCompanyDto ToSunatCompanyDto(DecolectaRucResponse response) => new()
        {
            CompanyRuc = response.NumeroDocumento,
            CompanyName = response.RazonSocial,
            CompanyAddress = response.Direccion,
            CompanyEconomicActivityDescription = response.ActividadEconomica
        };
    }
}
