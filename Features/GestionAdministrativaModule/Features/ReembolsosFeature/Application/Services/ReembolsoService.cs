using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Reembolsos.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Reembolsos.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Reembolsos.Infrastructure.Interfaces;

namespace Abril_Backend.Features.GestionAdministrativa.Reembolsos.Application.Services
{
    public class ReembolsoService : IReembolsoService
    {
        private readonly IReembolsoRepository      _repo;
        private readonly ISalidaVisibilityResolver _visibilityResolver;

        public ReembolsoService(IReembolsoRepository repo, ISalidaVisibilityResolver visibilityResolver)
        {
            _repo               = repo;
            _visibilityResolver = visibilityResolver;
        }

        /// <summary>
        /// La segunda mitad del criterio de Tesorería: el rol lo verificó el controller contra el
        /// token, acá se verifica que el puesto sea de categoría Tesorero, que vive en la base.
        /// Mirando solo el rol se le habría abierto la bandeja a alguien con el rol pero sin el
        /// puesto, con un botón de pagar que el backend rechaza.
        /// </summary>
        public async Task EnsureTesoreroAsync(int userId)
        {
            var vis = await _visibilityResolver.ResolveAsync(userId);
            if (!vis.EsCategoriaTesorero)
                throw new AbrilException(
                    "Reembolsos es de Tesorería: tu puesto no es de categoría Tesorero.", 403);
        }

        public async Task<ReembolsoListResultDto> GetAll(ReembolsoFiltersDto filters, int userId)
        {
            await EnsureTesoreroAsync(userId);
            var data = await _repo.GetAll(filters);
            return new ReembolsoListResultDto
            {
                Data    = data,
                Resumen = ResumenReembolsosDto.De(data),
            };
        }

        public async Task<ReembolsoFilterDataDto> GetFilterData(int userId)
        {
            await EnsureTesoreroAsync(userId);
            return await _repo.GetFilterData();
        }

        public async Task<ReembolsoDetalleDto> GetDetalle(int rendicionId, int userId)
        {
            await EnsureTesoreroAsync(userId);
            return await _repo.GetDetalle(rendicionId)
                ?? throw new AbrilException(
                    "La planilla no existe o todavía no está firmada por la jefatura.", 404);
        }

        public async Task<ReembolsoBulkResultDto> MarcarPagadas(PagarDto dto, int tesoreroUserId)
        {
            await EnsureTesoreroAsync(tesoreroUserId);

            var ids = await _repo.ResolverSolicitudIds(dto.RendicionIds, dto.SolicitudIds);
            if (ids.Count == 0)
                throw new AbrilException(
                    "No hay salidas firmadas en la selección: solo se puede marcar como pagado lo que la " +
                    "jefatura ya firmó.", 400);

            var pagadas = await _repo.MarcarPagadas(ids, tesoreroUserId);
            return new ReembolsoBulkResultDto
            {
                Procesadas = pagadas.Count,
                Message    = $"{pagadas.Count} reembolso(s) marcado(s) como pagado(s).",
            };
        }
    }
}
