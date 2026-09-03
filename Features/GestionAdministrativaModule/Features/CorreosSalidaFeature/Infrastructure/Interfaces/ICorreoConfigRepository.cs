using Abril_Backend.Features.GestionAdministrativa.CorreosSalida.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.CorreosSalida.Infrastructure.Interfaces
{
    public interface ICorreoConfigRepository
    {
        Task<CorreoConfigInicialDto> GetInicialAsync();

        Task SetEventoActiveAsync(string eventoCodigo, bool active);
        Task SetPrincipalActiveAsync(string eventoCodigo, bool active);

        Task<int> CrearDestinatarioAsync(string eventoCodigo, CorreoDestinatarioInputDto dto);
        Task ActualizarDestinatarioAsync(int id, CorreoDestinatarioInputDto dto);
        Task SetDestinatarioActiveAsync(int id, bool active);
        Task EliminarDestinatarioAsync(int id);
    }
}
