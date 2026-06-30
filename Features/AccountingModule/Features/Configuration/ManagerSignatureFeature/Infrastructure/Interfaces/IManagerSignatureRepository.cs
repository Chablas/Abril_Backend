using Abril_Backend.Features.AccountingModule.Features.Configuration.ManagerSignatureFeature.Application.Dtos;

namespace Abril_Backend.Features.AccountingModule.Features.Configuration.ManagerSignatureFeature.Infrastructure.Interfaces
{
    public interface IManagerSignatureRepository
    {
        /// <summary>Firma única vigente (como data URL) o null si aún no se configuró.</summary>
        Task<ManagerSignatureDto?> GetSingleton();

        /// <summary>Crea o actualiza (upsert) la firma única.</summary>
        Task Upsert(byte[] imageBytes, string mime, int userId);

        /// <summary>Bytes de la firma vigente (para estampar) o null si no hay firma configurada.</summary>
        Task<(byte[] Bytes, string Mime)?> GetActiveBytes();
    }
}
