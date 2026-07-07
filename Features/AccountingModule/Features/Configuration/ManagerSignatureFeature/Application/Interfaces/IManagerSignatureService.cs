using Abril_Backend.Features.AccountingModule.Features.Configuration.ManagerSignatureFeature.Application.Dtos;

namespace Abril_Backend.Features.AccountingModule.Features.Configuration.ManagerSignatureFeature.Application.Interfaces
{
    public interface IManagerSignatureService
    {
        /// <summary>Firma del usuario indicado (o null si aún no la configuró).</summary>
        Task<ManagerSignatureDto?> Get(int userId);

        /// <summary>Valida y guarda/actualiza la firma del usuario indicado a partir del PNG del canvas. Devuelve la firma resultante.</summary>
        Task<ManagerSignatureDto> Save(ManagerSignatureSaveDto dto, int userId);
    }
}
