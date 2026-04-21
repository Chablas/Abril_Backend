using System.Net.Http.Json;
using Abril_Backend.Shared.Services.Sunat.Dtos;
using Abril_Backend.Shared.Services.Sunat.Interfaces;

namespace Abril_Backend.Shared.Services.Sunat.Providers.Decolecta
{
    public class DecolectaSunatService : ISunatService
    {
        private readonly HttpClient _httpClient;

        public DecolectaSunatService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<SunatContributorDto?> GetByRucAsync(string ruc)
        {
            var response = await _httpClient.GetAsync($"/v1/sunat/ruc/full?numero={ruc}");
            if (!response.IsSuccessStatusCode)
                return null;

            var data = await response.Content.ReadFromJsonAsync<DecolectaRucResponse>();
            if (data is null)
                return null;

            return DecolectaRucMapper.ToSunatContributorDto(data);
        }
    }
}
