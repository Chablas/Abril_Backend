using Abril_Backend.Features.AccountingModule.Features.Configuration.ManagerSignatureFeature.Application.Dtos;

namespace Abril_Backend.Features.AccountingModule.Features.Configuration.ManagerSignatureFeature.Application.Interfaces
{
    public interface IManagerSignatureService
    {
        /// <summary>Firma única configurada (o null si aún no se configuró).</summary>
        Task<ManagerSignatureDto?> GetSingleton();

        /// <summary>Valida y guarda/actualiza la firma única a partir del PNG del canvas. Devuelve la firma resultante.</summary>
        Task<ManagerSignatureDto> Save(ManagerSignatureSaveDto dto, int userId);
    }
}
