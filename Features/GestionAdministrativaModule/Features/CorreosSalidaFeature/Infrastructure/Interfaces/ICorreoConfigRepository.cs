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
        /// <summary>
        /// Los correos de una SECCIÓN de esa pantalla (ga_correo_grupo): CORREOS los del flujo,
        /// RECORDATORIOS los del plazo de rendición. Solo la lectura se acota por sección — las
        /// escrituras siguen yendo por pantalla, porque las dos secciones de una misma pantalla
        /// las administra la misma persona con la misma feature.
        /// </summary>
        Task<CorreoConfigInicialDto> GetInicialAsync(string pantallaCodigo, string grupoCodigo);

        Task SetEventoActiveAsync(string pantallaCodigo, string eventoCodigo, bool active);
        Task SetPrincipalActiveAsync(string pantallaCodigo, string eventoCodigo, bool active);

        Task<int> CrearDestinatarioAsync(string pantallaCodigo, string eventoCodigo, CorreoDestinatarioInputDto dto);
        Task ActualizarDestinatarioAsync(string pantallaCodigo, int id, CorreoDestinatarioInputDto dto);
        Task SetDestinatarioActiveAsync(string pantallaCodigo, int id, bool active);
        Task EliminarDestinatarioAsync(string pantallaCodigo, int id);
    }
}
