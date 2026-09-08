using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.CorreosSalida.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.CorreosSalida.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.CorreosSalida.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;

namespace Abril_Backend.Features.GestionAdministrativa.CorreosSalida.Application.Services
{
    /// <summary>
    /// Wrapper delgado sobre el repositorio de configuración de correos. Lo único propio es
    /// traducir el segmento de URL de la pantalla al código del catálogo: un segmento que no
    /// corresponde a ninguna pantalla sale como 404 en vez de listar o escribir de más.
    /// </summary>
    public class CorreoConfigService : ICorreoConfigService
    {
        private readonly ICorreoConfigRepository _repo;

        public CorreoConfigService(ICorreoConfigRepository repo) => _repo = repo;

        private static string Pantalla(string segmento) =>
            CorreoPantallaCodigos.DesdeSegmento(segmento)
            ?? throw new AbrilException($"La pantalla indicada no existe: '{segmento}'.", 404);

        public Task<CorreoConfigInicialDto> GetInicialAsync(string pantallaSegmento) =>
            _repo.GetInicialAsync(Pantalla(pantallaSegmento));

        public Task SetEventoActiveAsync(string pantallaSegmento, string eventoCodigo, bool active) =>
            _repo.SetEventoActiveAsync(Pantalla(pantallaSegmento), eventoCodigo, active);

        public Task SetPrincipalActiveAsync(string pantallaSegmento, string eventoCodigo, bool active) =>
            _repo.SetPrincipalActiveAsync(Pantalla(pantallaSegmento), eventoCodigo, active);

        public Task<int> CrearDestinatarioAsync(string pantallaSegmento, string eventoCodigo, CorreoDestinatarioInputDto dto) =>
            _repo.CrearDestinatarioAsync(Pantalla(pantallaSegmento), eventoCodigo, dto);

        public Task ActualizarDestinatarioAsync(string pantallaSegmento, int id, CorreoDestinatarioInputDto dto) =>
            _repo.ActualizarDestinatarioAsync(Pantalla(pantallaSegmento), id, dto);

        public Task SetDestinatarioActiveAsync(string pantallaSegmento, int id, bool active) =>
            _repo.SetDestinatarioActiveAsync(Pantalla(pantallaSegmento), id, active);

        public Task EliminarDestinatarioAsync(string pantallaSegmento, int id) =>
            _repo.EliminarDestinatarioAsync(Pantalla(pantallaSegmento), id);
    }
}
