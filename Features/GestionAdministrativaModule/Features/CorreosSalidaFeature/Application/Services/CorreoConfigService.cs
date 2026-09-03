using Abril_Backend.Features.GestionAdministrativa.CorreosSalida.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.CorreosSalida.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.CorreosSalida.Infrastructure.Interfaces;

namespace Abril_Backend.Features.GestionAdministrativa.CorreosSalida.Application.Services
{
    /// <summary>Wrapper delgado sobre el repositorio de configuración de correos.</summary>
    public class CorreoConfigService : ICorreoConfigService
    {
        private readonly ICorreoConfigRepository _repo;

        public CorreoConfigService(ICorreoConfigRepository repo) => _repo = repo;

        public Task<CorreoConfigInicialDto> GetInicialAsync() => _repo.GetInicialAsync();

        public Task SetEventoActiveAsync(string eventoCodigo, bool active) =>
            _repo.SetEventoActiveAsync(eventoCodigo, active);

        public Task SetPrincipalActiveAsync(string eventoCodigo, bool active) =>
            _repo.SetPrincipalActiveAsync(eventoCodigo, active);

        public Task<int> CrearDestinatarioAsync(string eventoCodigo, CorreoDestinatarioInputDto dto) =>
            _repo.CrearDestinatarioAsync(eventoCodigo, dto);

        public Task ActualizarDestinatarioAsync(int id, CorreoDestinatarioInputDto dto) =>
            _repo.ActualizarDestinatarioAsync(id, dto);

        public Task SetDestinatarioActiveAsync(int id, bool active) =>
            _repo.SetDestinatarioActiveAsync(id, active);

        public Task EliminarDestinatarioAsync(int id) => _repo.EliminarDestinatarioAsync(id);
    }
}
