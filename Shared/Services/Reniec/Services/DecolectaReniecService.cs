using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Shared.Services.Reniec.Interfaces;
using Abril_Backend.Shared.Services.Reniec.Dtos;

namespace Abril_Backend.Shared.Services.Reniec.Services
{
    public class ReniecService : IReniecService
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