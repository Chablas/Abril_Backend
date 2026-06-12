using Abril_Backend.Features.Ssoma.Rac.Dtos;

namespace Abril_Backend.Features.Ssoma.Rac.Services;

public class PenalidadService : IPenalidadService
{
    public Task<RacPagedResult<PenalidadListItemDto>> GetListAsync(PenalidadListQuery q)             => throw new NotImplementedException();
    public Task<PenalidadDetalleDto?> GetDetalleAsync(int id)                                        => throw new NotImplementedException();
    public Task PresentarDescargaAsync(int id, PenalidadDescargaRequest req, int userId)             => throw new NotImplementedException();
    public Task<PenalidadDetalleDto> ResolverAsync(int id, PenalidadResolverRequest req, int userId) => throw new NotImplementedException();
    public Task<byte[]> GetPdfResolucionAsync(int id)                                                => throw new NotImplementedException();
}
