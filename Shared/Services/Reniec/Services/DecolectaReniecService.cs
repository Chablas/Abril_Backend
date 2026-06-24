using System.Net;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
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
            {
                // 401/403/429 = problema de credencial o de cuota del proveedor (no es un "no encontrado").
                if (response.StatusCode is HttpStatusCode.Unauthorized
                    or HttpStatusCode.Forbidden
                    or HttpStatusCode.TooManyRequests)
                {
                    throw new AbrilException(
                        "Se agotaron las consultas disponibles del servicio de consulta de DNI. Por favor, contacte con el administrador del sistema.",
                        503);
                }

                return null;
            }

            return await response.Content.ReadFromJsonAsync<ReniecPersonDto>();
        }
    }
}