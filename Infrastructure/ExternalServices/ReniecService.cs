using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Infrastructure.ExternalServices
{
    public class ReniecService
    {
        private readonly HttpClient _httpClient;

        public ReniecService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ReniecPersonDto?> GetByDniAsync(string dni)
        {
            var response = await _httpClient.GetAsync($"/v1/reniec/dni?numero={dni}");
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<ReniecPersonDto>();
        }
    }
}