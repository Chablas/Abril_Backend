using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.AuthModule.Role.Application.Dtos;
using Abril_Backend.Features.AuthModule.Role.Application.Interfaces;
using Abril_Backend.Features.AuthModule.Role.Infrastructure.Interfaces;

namespace Abril_Backend.Features.AuthModule.Role.Application.Services
{
    public class RoleFeatureService : IRoleFeatureService
    {
        private readonly IRoleFeatureRepository _repository;

        public RoleFeatureService(IRoleFeatureRepository repository)
        {
            _repository = repository;
        }

        public async Task<PagedResult<RoleDto>> GetPaged(int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            return await _repository.GetPaged(page, pageSize);
        }

        public async Task Create(RoleCreateDto dto, int userId)
        {
            await _repository.Create(dto, userId);
        }

        public async Task<List<FeatureDto>> GetAllFeatures()
        {
            return await _repository.GetAllFeatures();
        }

        public async Task<List<int>> GetRoleFeatureIds(int roleId)
        {
            return await _repository.GetRoleFeatureIds(roleId);
        }

        public async Task UpdateRoleFeatures(int roleId, List<int> featureIds)
        {
            await _repository.UpdateRoleFeatures(roleId, featureIds);
        }

        public async Task UpdateRoleDescription(int roleId, RoleUpdateDescriptionDto dto, int userId)
        {
            if (string.IsNullOrWhiteSpace(dto.RoleDescription))
                throw new AbrilException("La descripción del rol es obligatoria.");

            await _repository.UpdateRoleDescription(roleId, dto.RoleDescription, userId);
        }
    }
}
