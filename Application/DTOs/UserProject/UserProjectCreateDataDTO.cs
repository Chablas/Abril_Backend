using Abril_Backend.Infrastructure.Models;

namespace Abril_Backend.Application.DTOs
{
    public class UserProjectCreateDataDTO
    {
        public List<UserPersonFilterDTO>? UserPersons { get; set; }
        public List<ProjectSimpleDTO>? Projects { get; set; }
    }
}
