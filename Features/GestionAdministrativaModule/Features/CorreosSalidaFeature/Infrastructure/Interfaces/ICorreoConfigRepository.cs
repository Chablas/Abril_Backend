using Abril_Backend.Features.GestionAdministrativa.CorreosSalida.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.CorreosSalida.Infrastructure.Interfaces
{
    /// <summary>
    /// Todas las operaciones reciben el código de la pantalla (ga_correo_pantalla) porque cada
    /// pantalla del flujo administra solo los correos que se originan en ella: sin ese filtro, la
    /// Configuración de una pantalla podría prender o borrar un destinatario de otra.
    /// </summary>
    public interface ICorreoConfigRepository
    {
        Task<CorreoConfigInicialDto> GetInicialAsync(string pantallaCodigo);

        Task SetEventoActiveAsync(string pantallaCodigo, string eventoCodigo, bool active);
        Task SetPrincipalActiveAsync(string pantallaCodigo, string eventoCodigo, bool active);

        Task<int> CrearDestinatarioAsync(string pantallaCodigo, string eventoCodigo, CorreoDestinatarioInputDto dto);
        Task ActualizarDestinatarioAsync(string pantallaCodigo, int id, CorreoDestinatarioInputDto dto);
        Task SetDestinatarioActiveAsync(string pantallaCodigo, int id, bool active);
        Task EliminarDestinatarioAsync(string pantallaCodigo, int id);
    }
}
