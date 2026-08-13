using Abril_Backend.Application.DTOs.ArquitecturaComercial;

namespace Abril_Backend.Infrastructure.Interfaces
{
    public interface IArquitecturaComercialTareoRepository
    {
        Task<TareoEnrolamientoEstadoDTO> GetEnrolamientoEstado(int workerId);
        Task EnrolarWorker(int workerId, string fotoUrl, float[] embedding);
        Task<TareoRegistroDTO> Marcar(int workerId, Guid idempotencyKey, TareoMarcarRequestDTO body, string fotoUrl, string fotoHash, string? ipOrigen);
        Task<TareoMiTareoHoyDTO> GetMiTareoHoy(int workerId);
        Task<TareoRegistroListResponseDTO> GetRegistros(TareoFiltroDTO filtro);
        Task<bool> Revisar(int id, int revisorUserId, TareoRevisarRequestDTO body);
        Task<List<TareoReporteSemanalDTO>> GetReporteSemanal(int? proyectoId, DateOnly semanaLunes);
    }
}
