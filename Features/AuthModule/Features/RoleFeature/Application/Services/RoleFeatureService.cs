using Abril_Backend.Application.DTOs;
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
    }
}
