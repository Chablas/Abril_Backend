using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.AuthModule.MicrosoftProfile.Application.Dtos;

namespace Abril_Backend.Features.AuthModule.MicrosoftLogin.Infrastructure.Interfaces
{
    public interface IMicrosoftLoginRepository
    {
        Task<UserDTO?> GetUserByEmailAsync(string email);
        /// <summary>
        /// La person del trabajador cuyo <c>workers.email_corporativo</c> es este correo, o null
        /// si ninguna ficha lo tiene. Es el único origen válido de una cuenta de Abril: sin ficha
        /// no hay acceso por SSO (ver <c>MicrosoftLoginService.Login</c>). Ya no existe un
        /// "crear person con los datos de Graph" al que caer.
        /// </summary>
        Task<PersonDTO?> GetPersonByWorkerEmailAsync(string email);
        Task<UserDTO> CreateUserAndLinkPersonAsync(MicrosoftProfileDto profile, int personId);
        Task<PersonDTO> LinkPersonToUserAsync(int userId, int personId, string email);
        Task<RoleSimpleDTO?> AssignRoleAsync(int userId, int roleId);
        Task<string?> GetWorkerAreaByPersonIdAsync(int personId);

        /// <summary>
        /// true si alguno de los workers de la persona está designado como revisor vivo y
        /// activo de algún área (area_revisores). Se usa en el primer login para darle el rol
        /// que le abre Gestión de Salidas.
        /// </summary>
        Task<bool> IsAreaRevisorByPersonIdAsync(int personId);
    }
}
