using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.AccountingModule.Features.Configuration.ManagerSignatureFeature.Application.Dtos;
using Abril_Backend.Features.AccountingModule.Features.Configuration.ManagerSignatureFeature.Application.Interfaces;
using Abril_Backend.Features.AccountingModule.Features.Configuration.ManagerSignatureFeature.Infrastructure.Interfaces;
using Abril_Backend.Shared.Helpers;

namespace Abril_Backend.Features.AccountingModule.Features.Configuration.ManagerSignatureFeature.Application.Services
{
    public class ManagerSignatureService : IManagerSignatureService
    {
        private readonly IManagerSignatureRepository _repository;

        public ManagerSignatureService(IManagerSignatureRepository repository)
        {
            _repository = repository;
        }

        public Task<ManagerSignatureDto?> Get(int userId) => _repository.GetByUserId(userId);

        public async Task<ManagerSignatureDto> Save(ManagerSignatureSaveDto dto, int userId)
        {
            // Mismas reglas que la firma del postulante en Onboarding: las dos van a las mismas
            // columnas person.signature_* y las dos se estampan con el mismo helper de PDF.
            var bytes = FirmaImagenHelper.DecodePng(dto?.ImageBase64);

            await _repository.Upsert(userId, bytes, FirmaImagenHelper.Mime);

            return await _repository.GetByUserId(userId)
                ?? throw new AbrilException("No se pudo guardar la firma.", 500);
        }
    }
}
