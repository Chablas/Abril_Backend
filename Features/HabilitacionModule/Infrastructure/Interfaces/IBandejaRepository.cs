using Abril_Backend.Features.Habilitacion.Application.Dtos.Bandeja;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Shared.DTOs;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces
{
    public interface IBandejaRepository
    {
        Task<(List<BandejaItemDto> Items, int Total)> GetPendientesAsync(
            string? tipo, int? proyectoId, int? empresaId,
            string? responsable, int page, int pageSize);

        Task<CursorPagedResult<BandejaItemDto>> GetPendientesCursorAsync(
            string? tipo, int? proyectoId, int? empresaId,
            string? responsable, string? cursor, int pageSize);

        Task<SsHabTrabajador?> AprobarTrabajadorAsync(int id, BandejaAprobarDto dto, int userId);
        Task<SsHabEmpresa?> AprobarEmpresaAsync(int id, BandejaAprobarDto dto, int userId);
        Task<SsHabEquipo?> AprobarEquipoAsync(int id, BandejaAprobarDto dto, int userId);
    }
}
