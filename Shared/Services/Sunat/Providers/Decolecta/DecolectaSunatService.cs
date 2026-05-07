using System.Net.Http.Json;
using Abril_Backend.Shared.Services.Sunat.Dtos;
using Abril_Backend.Shared.Services.Sunat.Interfaces;

namespace Abril_Backend.Shared.Services.Sunat.Providers.Decolecta
{
    public class DecolectaSunatService : ISunatService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<DecolectaSunatService> _logger;

        public DecolectaSunatService(HttpClient httpClient, ILogger<DecolectaSunatService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<SunatContributorDto?> GetByRucAsync(string ruc)
        {
            var requestUrl = $"/v1/sunat/ruc/full?numero={ruc}";
            _logger.LogInformation("[Sunat] GET {BaseAddress}{Url}", _httpClient.BaseAddress, requestUrl);

            var response = await _httpClient.GetAsync(requestUrl);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[Sunat] HTTP {StatusCode} para RUC {Ruc}. Body: {Body}",
                    (int)response.StatusCode, ruc, body);
                return null;
            }

            var data = await response.Content.ReadFromJsonAsync<DecolectaRucResponse>();
            if (data is null)
            {
                _logger.LogWarning("[Sunat] Respuesta vacía para RUC {Ruc}", ruc);
                return null;
            }

            return DecolectaRucMapper.ToSunatContributorDto(data);
        }
    }
}
