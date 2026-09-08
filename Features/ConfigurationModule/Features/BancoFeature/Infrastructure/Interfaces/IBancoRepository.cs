using Abril_Backend.Features.ConfigurationModule.Features.BancoFeature.Application.Dtos;

namespace Abril_Backend.Features.ConfigurationModule.Features.BancoFeature.Infrastructure.Interfaces
{
    public interface IBancoRepository
    {
        /// <summary>
        /// Todos los bancos vigentes (activos e inactivos), con cuántas razones sociales usa cada
        /// uno. La pantalla filtra y pagina en memoria: es un catálogo de pocas filas y traerlo
        /// entero evita una petición por cada cambio de filtro.
        /// </summary>
        Task<List<BancoDto>> List();

        Task<BancoDto> Create(BancoUpsertDto dto, int? userId);
        Task<BancoDto> Update(int bancoId, BancoUpsertDto dto, int? userId);

        /// <summary>Soft delete. Falla si alguna razón social todavía lo tiene asignado.</summary>
        Task Delete(int bancoId, int? userId);
    }
}
