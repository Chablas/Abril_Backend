using Abril_Backend.Features.AccountingModule.Features.Configuration.ManagerSignatureFeature.Application.Dtos;

namespace Abril_Backend.Features.AccountingModule.Features.Configuration.ManagerSignatureFeature.Infrastructure.Interfaces
{
    public interface IManagerSignatureRepository
    {
        /// <summary>Firma (como data URL) del usuario indicado, o null si aún no la configuró.</summary>
        Task<ManagerSignatureDto?> GetByUserId(int userId);

        /// <summary>Crea o actualiza (upsert) la firma del usuario indicado.</summary>
        Task Upsert(int userId, byte[] imageBytes, string mime);

        /// <summary>Bytes de la firma del usuario indicado (para estampar) o null si no la configuró.</summary>
        Task<(byte[] Bytes, string Mime)?> GetActiveBytesByUserId(int userId);
    }
}
