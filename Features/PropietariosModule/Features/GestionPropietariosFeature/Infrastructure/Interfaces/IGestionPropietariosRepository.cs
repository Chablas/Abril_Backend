using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Application.Dtos;

namespace Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Infrastructure.Interfaces
{
    public interface IGestionPropietariosRepository
    {
        Task<List<PropietarioProyectoDto>> GetProyectos();

        /// <summary><paramref name="projectId"/> null = todos los proyectos.</summary>
        Task<PagedResult<PropietarioListItemDto>> GetPaged(int page, int pageSize, string? search, int? projectId);

        /// <summary>
        /// La persona con ese DNI (con cualquier state: el DNI es único en toda la tabla), o null.
        /// Lanza 409 si está dada de baja.
        /// </summary>
        Task<PropietarioPersonaDto?> GetPersonaPorDni(string dni);

        /// <summary>
        /// Persona (reusada si el DNI ya existe), usuario (reusado si ya tiene uno vigente), rol
        /// PROPIETARIO e inmuebles, en una sola transacción. Los datos ya vienen normalizados.
        /// </summary>
        Task<PropietarioGuardadoRepoDto> Crear(PropietarioCreateDto dto, int userId);

        /// <summary>Datos personales, correo de la cuenta e inmuebles (altas, cambios y bajas).</summary>
        Task<PropietarioGuardadoRepoDto> Actualizar(int personId, PropietarioUpdateDto dto, int userId);

        /// <summary>Null si la persona no es propietaria o no tiene un usuario vigente.</summary>
        Task<PropietarioCuentaDto?> GetCuenta(int personId);

        /// <summary>Da de baja sus inmuebles y le quita el rol PROPIETARIO (sale de la app).</summary>
        Task Eliminar(int personId, int userId);
    }
}
