using System.Net.Mail;
using System.Text.RegularExpressions;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Application.Dtos;
using Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Application.Interfaces;
using Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.Convivir.Interfaces;
using Abril_Backend.Shared.Services.Reniec.Interfaces;

namespace Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Application.Services
{
    /// <summary>
    /// Alta y mantenimiento de los propietarios que entran a la app Convivir Abril. Reusa person y
    /// app_user: una persona que ya existe (trabajador, usuario externo) no se duplica, y un usuario
    /// vigente se reusa agregándole el rol PROPIETARIO. La invitación a la app sale solo si la
    /// cuenta todavía no tiene contraseña.
    /// </summary>
    public class GestionPropietariosService : IGestionPropietariosService
    {
        private static readonly Regex DniValido = new(@"^\d{8}$");
        private static readonly Regex PrefijoTorre = new(@"^TORRE\s+", RegexOptions.IgnoreCase);
        private static readonly Regex PrefijoDepartamento = new(@"^(DEPARTAMENTO|DEPTO\.?|DPTO\.?)\s*", RegexOptions.IgnoreCase);
        private static readonly Regex Espacios = new(@"\s+");

        private const int LargoMaximoTorre = 50;
        private const int LargoMaximoDepartamento = 20;

        private readonly IGestionPropietariosRepository _repo;
        private readonly IConvivirEnlaceService _enlace;
        private readonly IReniecService _reniec;
        private readonly ILogger<GestionPropietariosService> _logger;

        public GestionPropietariosService(
            IGestionPropietariosRepository repo,
            IConvivirEnlaceService enlace,
            IReniecService reniec,
            ILogger<GestionPropietariosService> logger)
        {
            _repo = repo;
            _enlace = enlace;
            _reniec = reniec;
            _logger = logger;
        }

        /// <summary>Secuencial y no con Task.WhenAll: una sola conexión a la vez.</summary>
        public async Task<PropietariosInitDto> GetInit(int pageSize) => new()
        {
            Proyectos = await _repo.GetProyectos(),
            Propietarios = await _repo.GetPaged(1, pageSize, null, null),
        };

        public Task<PagedResult<PropietarioListItemDto>> GetPaged(int page, int pageSize, string? search, int? projectId) =>
            _repo.GetPaged(page, pageSize, search, projectId);

        public async Task<PropietarioPersonaDto> BuscarPersona(string dni)
        {
            var dniValido = NormalizarDni(dni);

            var enSistema = await _repo.GetPersonaPorDni(dniValido);
            if (enSistema != null)
                return enSistema;

            try
            {
                var reniec = await _reniec.GetByDniAsync(dniValido);
                if (reniec != null)
                {
                    return new PropietarioPersonaDto
                    {
                        Fuente = "RENIEC",
                        FirstNames = reniec.FirstName,
                        FirstLastName = reniec.FirstLastName,
                        SecondLastName = reniec.SecondLastName,
                    };
                }
            }
            catch (Exception ex)
            {
                // Sin RENIEC se puede seguir escribiendo los nombres a mano.
                _logger.LogWarning(ex, "RENIEC no respondió al buscar el DNI de un propietario");
            }

            return new PropietarioPersonaDto { Fuente = "NINGUNA" };
        }

        public async Task<PropietarioGuardadoDto> Crear(PropietarioCreateDto dto, int userId)
        {
            var normalizado = new PropietarioCreateDto
            {
                Dni = NormalizarDni(dto.Dni),
                FirstNames = Requerido(dto.FirstNames, "los nombres"),
                FirstLastName = Requerido(dto.FirstLastName, "el primer apellido"),
                SecondLastName = Texto(dto.SecondLastName),
                Email = NormalizarCorreo(dto.Email),
                PhoneNumber = ValidarCelular(dto.PhoneNumber),
                Propiedades = NormalizarPropiedades(dto.Propiedades),
            };

            return await ConInvitacion(await _repo.Crear(normalizado, userId));
        }

        public async Task<PropietarioGuardadoDto> Actualizar(int personId, PropietarioUpdateDto dto, int userId)
        {
            var normalizado = new PropietarioUpdateDto
            {
                FirstNames = Requerido(dto.FirstNames, "los nombres"),
                FirstLastName = Requerido(dto.FirstLastName, "el primer apellido"),
                SecondLastName = Texto(dto.SecondLastName),
                Email = NormalizarCorreo(dto.Email),
                PhoneNumber = ValidarCelular(dto.PhoneNumber),
                Propiedades = NormalizarPropiedades(dto.Propiedades),
            };

            return await ConInvitacion(await _repo.Actualizar(personId, normalizado, userId));
        }

        public async Task<string> ReenviarInvitacion(int personId)
        {
            var cuenta = await _repo.GetCuenta(personId)
                ?? throw new AbrilException("No tiene una cuenta vigente. Guárdalo desde «Editar» para crearle una.", 409);

            if (cuenta.TienePassword)
                throw new AbrilException("Ya creó su contraseña. Si la olvidó, puede pedir una nueva desde la app.", 409);

            return await _enlace.EnviarEnlaceSiEsPropietarioAsync(cuenta.UserId, ConvivirEnlaceTipo.Invitacion)
                ?? throw new AbrilException("No tiene el rol PROPIETARIO. Guárdalo desde «Editar» para devolvérselo.", 409);
        }

        public Task Eliminar(int personId, int userId) => _repo.Eliminar(personId, userId);

        /// <summary>
        /// Manda la invitación después de guardar. Si el correo falla, lo guardado se queda: si el
        /// error se propagara, la pantalla lo mostraría como fallido, lo volverían a crear y chocaría
        /// con «ya está en la lista». La pantalla avisa y queda el botón de reenviar.
        /// </summary>
        private async Task<PropietarioGuardadoDto> ConInvitacion(PropietarioGuardadoRepoDto guardado)
        {
            var resultado = new PropietarioGuardadoDto { PersonId = guardado.PersonId };
            if (!guardado.EnviarInvitacion)
                return resultado;

            try
            {
                resultado.InvitacionEnviadaA = await _enlace.EnviarEnlaceSiEsPropietarioAsync(
                    guardado.UserId, ConvivirEnlaceTipo.Invitacion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar la invitación de Convivir Abril al usuario {UserId}", guardado.UserId);
            }

            resultado.InvitacionFallida = resultado.InvitacionEnviadaA == null;
            return resultado;
        }

        private static string NormalizarDni(string? dni)
        {
            var limpio = (dni ?? string.Empty).Trim();
            if (!DniValido.IsMatch(limpio))
                throw new AbrilException("El DNI debe tener 8 dígitos.", 400);
            return limpio;
        }

        /// <summary>Recortado, con un solo espacio entre palabras y en MAYÚSCULAS; null si queda vacío.</summary>
        private static string? Texto(string? valor)
        {
            var limpio = Espacios.Replace((valor ?? string.Empty).Trim(), " ");
            return limpio.Length == 0 ? null : limpio.ToUpperInvariant();
        }

        private static string Requerido(string? valor, string nombreCampo) =>
            Texto(valor) ?? throw new AbrilException($"Escribe {nombreCampo}.", 400);

        private static string NormalizarCorreo(string? email)
        {
            var limpio = (email ?? string.Empty).Trim().ToLowerInvariant();
            if (limpio.Length == 0)
                throw new AbrilException("Escribe el correo: ahí le llega la invitación a la app.", 400);

            if (!MailAddress.TryCreate(limpio, out var direccion)
                || direccion.Address != limpio
                || !direccion.Host.Contains('.'))
                throw new AbrilException("El correo no es válido.", 400);

            return limpio;
        }

        private static int? ValidarCelular(int? celular)
        {
            if (celular is null)
                return null;
            if (celular is < 1 or > 999_999_999)
                throw new AbrilException("El celular no es válido.", 400);
            return celular;
        }

        /// <summary>
        /// Torre y departamento sin la palabra «Torre»/«Dpto.» (la app la antepone al mostrarlos) y
        /// en MAYÚSCULAS, así el índice único los compara igual se escriban como se escriban.
        /// </summary>
        private static List<PropiedadGuardarDto> NormalizarPropiedades(List<PropiedadGuardarDto>? propiedades)
        {
            if (propiedades == null || propiedades.Count == 0)
                throw new AbrilException("Agrega al menos una propiedad.", 400);

            var resultado = new List<PropiedadGuardarDto>();
            var vistas = new HashSet<string>();

            foreach (var propiedad in propiedades)
            {
                if (propiedad.ProjectId <= 0)
                    throw new AbrilException("Elige el proyecto de cada propiedad.", 400);

                var torre = Texto(propiedad.Torre);
                if (torre != null)
                    torre = Texto(PrefijoTorre.Replace(torre, string.Empty));

                var departamento = Texto(propiedad.Departamento);
                if (departamento != null)
                    departamento = Texto(PrefijoDepartamento.Replace(departamento, string.Empty));

                if (departamento == null)
                    throw new AbrilException("Escribe el departamento de cada propiedad.", 400);
                if (torre?.Length > LargoMaximoTorre)
                    throw new AbrilException($"La torre admite hasta {LargoMaximoTorre} caracteres.", 400);
                if (departamento.Length > LargoMaximoDepartamento)
                    throw new AbrilException($"El departamento admite hasta {LargoMaximoDepartamento} caracteres.", 400);

                if (!vistas.Add($"{propiedad.ProjectId}|{torre}|{departamento}"))
                    throw new AbrilException($"El departamento {departamento} está repetido.", 400);

                resultado.Add(new PropiedadGuardarDto
                {
                    PropietarioId = propiedad.PropietarioId,
                    ProjectId = propiedad.ProjectId,
                    Torre = torre,
                    Departamento = departamento,
                });
            }

            return resultado;
        }
    }
}
