using Microsoft.AspNetCore.Mvc;
using Abril_Backend.Infrastructure.Repositories;
using Abril_Backend.Infrastructure.ExternalServices;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Abril_Backend.Controllers {

    [ApiController]
    [Route("api/v1/[controller]")]
    public class PersonController : ControllerBase {
        PersonRepository _repository;
        private readonly ReniecService _reniecService;
        public PersonController(PersonRepository repository, ReniecService reniecService) {
            _repository = repository;
            _reniecService = reniecService;
        }

        /*
        [HttpGet]
        public async Task<IActionResult> GetAll() {
            var data = await _repository.GetAll();
            return Ok(data);
        }
        */

        [Authorize]
        [HttpGet("reniec/{dni}")]
        public async Task<IActionResult> GetDNI(string dni)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var person = await _reniecService.GetByDniAsync(dni);
                if (person == null)
                    return BadRequest(new { message = "DNI Inválido" });
                return Ok(person);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}