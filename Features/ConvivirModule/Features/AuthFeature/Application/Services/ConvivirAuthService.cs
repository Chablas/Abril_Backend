using System.Text.Encodings.Web;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Application.Interfaces;
using Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Application.Dtos;
using Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Application.Interfaces;
using Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.Convivir.Configuration;
using Abril_Backend.Shared.Services.Convivir.Interfaces;
using Microsoft.Extensions.Options;

namespace Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Application.Services
{
    /// <summary>
    /// Login de la app Convivir Abril. Reusa todo lo de la intranet (app_user, user_role,
    /// user_session, user_password_token, el JWT y el set-password); lo propio es que se entra con
    /// el DNI (no con el correo), que solo entran los usuarios con el rol PROPIETARIO y que la
    /// sesión nace más larga.
    /// </summary>
    public class ConvivirAuthService : IConvivirAuthService
    {
        private const int LargoMinimoPassword = 8;
        private const string EnlaceInvalido = "El enlace venció o ya fue usado. Pide uno nuevo desde «¿La olvidaste?».";
        private const string SinAcceso = "Tu cuenta no tiene acceso a Convivir Abril.";
        private const string CredencialesInvalidas = "DNI o contraseña incorrectos.";

        private readonly IConvivirAuthRepository _repo;
        private readonly IAuthRepository _authRepo;
        private readonly IAuthService _authService;
        private readonly IJWTService _jwtService;
        private readonly IUserPasswordTokenRepository _tokenRepo;
        private readonly IConvivirEnlaceService _enlaceService;
        private readonly ConvivirOptions _options;

        public ConvivirAuthService(
            IConvivirAuthRepository repo,
            IAuthRepository authRepo,
            IAuthService authService,
            IJWTService jwtService,
            IUserPasswordTokenRepository tokenRepo,
            IConvivirEnlaceService enlaceService,
            IOptions<ConvivirOptions> options)
        {
            _repo = repo;
            _authRepo = authRepo;
            _authService = authService;
            _jwtService = jwtService;
            _tokenRepo = tokenRepo;
            _enlaceService = enlaceService;
            _options = options.Value;
        }

        public async Task<ConvivirLoginResponseDto> Login(ConvivirLoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Dni) || string.IsNullOrEmpty(dto.Password))
                throw new AbrilException("Ingresa tu DNI y tu contraseña.", 400);

            var cuenta = await _repo.GetCuentaPorDni(dto.Dni)
                ?? throw new AbrilException(CredencialesInvalidas, 401);

            // Sin este corte, ValidateUserAsync revienta con un 500 al verificar contra un hash nulo.
            if (!cuenta.TienePassword)
                throw new AbrilException("Todavía no creaste tu contraseña. Usa el enlace del correo de invitación.", 401);

            // Con contraseña y sin active: la desactivaron desde Seguridad (crear la contraseña la activa).
            if (!cuenta.Activa)
                throw new AbrilException(SinAcceso, 403);

            var user = await _authRepo.ValidateUserAsync(cuenta.Email, dto.Password)
                ?? throw new AbrilException(CredencialesInvalidas, 401);

            if (!await _enlaceService.EsPropietarioAsync(user.UserId))
                throw new AbrilException(SinAcceso, 403);

            return await CrearRespuestaLogin(user);
        }

        public async Task<ConvivirRefreshResponseDto> Refresh(string sessionToken)
        {
            if (string.IsNullOrWhiteSpace(sessionToken))
                throw new AbrilException("Sesión inválida.", 401);

            var userId = await _authRepo.GetUserIdByValidSessionAsync(sessionToken)
                ?? throw new AbrilException("Tu sesión venció. Vuelve a ingresar.", 401);

            // Se vuelve a mirar el rol en cada refresh: si le quitan PROPIETARIO, sale en ≤ 2 minutos.
            if (!await _enlaceService.EsPropietarioAsync(userId))
                throw new AbrilException(SinAcceso, 401);

            var user = await _authRepo.GetUserForTokenAsync(userId)
                ?? throw new AbrilException("Tu sesión venció. Vuelve a ingresar.", 401);

            return new ConvivirRefreshResponseDto { AccessToken = _jwtService.GenerateToken(user) };
        }

        public Task Logout(string sessionToken) =>
            string.IsNullOrWhiteSpace(sessionToken) ? Task.CompletedTask : _repo.RevocarSesion(sessionToken);

        public async Task<ConvivirInvitacionDto> GetInvitacion(string token)
        {
            var enlace = await _tokenRepo.GetValidTokenAsync(token ?? "")
                ?? throw new AbrilException(EnlaceInvalido, 404);

            if (!await _enlaceService.EsPropietarioAsync(enlace.UserId))
                throw new AbrilException(SinAcceso, 403);

            var user = await _authRepo.GetUserByIdAsync(enlace.UserId)
                ?? throw new AbrilException(EnlaceInvalido, 404);

            var persona = await _repo.GetPersona(enlace.UserId);

            return new ConvivirInvitacionDto
            {
                Dni = persona.Dni,
                Email = user.Email,
                Nombres = persona.Nombres
            };
        }

        public async Task<ConvivirLoginResponseDto> CrearContrasena(ConvivirCrearContrasenaDto dto)
        {
            if (string.IsNullOrEmpty(dto.Password) || dto.Password.Length < LargoMinimoPassword)
                throw new AbrilException($"La contraseña debe tener al menos {LargoMinimoPassword} caracteres.", 400);

            if (dto.Password != dto.ConfirmPassword)
                throw new AbrilException("Las contraseñas no coinciden.", 400);

            var enlace = await _tokenRepo.GetValidTokenAsync(dto.Token ?? "")
                ?? throw new AbrilException(EnlaceInvalido, 400);

            if (!await _enlaceService.EsPropietarioAsync(enlace.UserId))
                throw new AbrilException(SinAcceso, 403);

            // El mismo set-password de la intranet: guarda el hash, activa la cuenta, confirma el
            // correo (tener el enlace prueba que es suyo) y anula el enlace.
            await _authService.SetPassword(new SetPasswordDTO
            {
                Token = dto.Token!,
                Password = dto.Password,
                ConfirmPassword = dto.ConfirmPassword
            });

            // Entra directo: acaba de escribir la contraseña, no se la volvemos a pedir.
            var user = await _authRepo.GetUserForTokenAsync(enlace.UserId)
                ?? throw new AbrilException(SinAcceso, 403);

            return await CrearRespuestaLogin(user);
        }

        public async Task OlvideContrasena(ConvivirOlvideContrasenaDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Dni))
                throw new AbrilException("Ingresa tu DNI.", 400);

            // Siempre responde lo mismo (lo arma el controller): no se revela si el DNI tiene
            // cuenta ni si es de un propietario. El enlace va al correo de la cuenta.
            var cuenta = await _repo.GetCuentaPorDni(dto.Dni);
            if (cuenta != null)
                await _enlaceService.EnviarEnlaceSiEsPropietarioAsync(cuenta.UserId, ConvivirEnlaceTipo.Recuperacion);
        }

        public async Task CambiarContrasena(int userId, ConvivirCambiarContrasenaDto dto)
        {
            if (string.IsNullOrEmpty(dto.PasswordActual))
                throw new AbrilException("Ingresa tu contraseña actual.", 400);

            if (string.IsNullOrEmpty(dto.Password) || dto.Password.Length < LargoMinimoPassword)
                throw new AbrilException($"La contraseña nueva debe tener al menos {LargoMinimoPassword} caracteres.", 400);

            if (dto.Password != dto.ConfirmPassword)
                throw new AbrilException("Las contraseñas nuevas no coinciden.", 400);

            if (dto.Password == dto.PasswordActual)
                throw new AbrilException("La contraseña nueva tiene que ser distinta de la actual.", 400);

            // La sesión que se queda tiene que ser de este usuario: si no, las cerraría todas.
            var userIdSesion = string.IsNullOrWhiteSpace(dto.SessionToken)
                ? null
                : await _authRepo.GetUserIdByValidSessionAsync(dto.SessionToken);
            if (userIdSesion != userId)
                throw new AbrilException("Tu sesión venció. Vuelve a ingresar.", 401);

            var user = await _authRepo.GetUserByIdAsync(userId)
                ?? throw new AbrilException("Tu sesión venció. Vuelve a ingresar.", 401);

            // 400 y no 401: la app renueva el JWT y reintenta ante un 401, y acá la sesión está bien.
            if (await _authRepo.ValidateUserAsync(user.Email, dto.PasswordActual) == null)
                throw new AbrilException("La contraseña actual no es correcta.", 400);

            await _repo.CambiarContrasena(userId, dto.Password, dto.SessionToken);
        }

        public string PaginaAbrirApp(string token)
        {
            var enlace = $"{_options.EnlaceApp}crear-contrasena?token={Uri.EscapeDataString(token ?? "")}";
            var href = HtmlEncoder.Default.Encode(enlace);
            var js = JavaScriptEncoder.Default.Encode(enlace);

            return $$"""
                <!doctype html>
                <html lang="es">
                <head>
                  <meta charset="utf-8">
                  <meta name="viewport" content="width=device-width, initial-scale=1">
                  <title>Convivir Abril</title>
                  <style>
                    body { margin: 0; min-height: 100vh; display: flex; align-items: center; justify-content: center;
                           font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Arial, sans-serif;
                           background: #f3f6f9; color: #1f2937; }
                    main { background: #fff; border-radius: 16px; padding: 32px 24px; margin: 16px; max-width: 360px;
                           width: 100%; text-align: center; box-shadow: 0 4px 24px rgba(0,0,0,.08); }
                    h1 { color: #005D9D; font-size: 22px; margin: 0 0 24px; }
                    a { display: block; background: #005D9D; color: #fff; text-decoration: none; padding: 14px;
                        border-radius: 10px; font-weight: 600; }
                  </style>
                </head>
                <body>
                  <main>
                    <h1>Convivir Abril</h1>
                    <a href="{{href}}">Abrir la app</a>
                  </main>
                  <script>window.location.href = "{{js}}";</script>
                </body>
                </html>
                """;
        }

        private async Task<ConvivirLoginResponseDto> CrearRespuestaLogin(UserDTO user)
        {
            var accessToken = _jwtService.GenerateToken(user);
            var sesion = await _repo.CrearSesion(user.UserId, DateTime.UtcNow.AddDays(_options.SesionDias));

            return new ConvivirLoginResponseDto
            {
                AccessToken = accessToken,
                SessionToken = sesion.Token,
                ExpiresAt = sesion.ExpiresAt,
                Usuario = new ConvivirUsuarioDto
                {
                    UserId = user.UserId,
                    Email = user.Person.Email,
                    Nombres = (await _repo.GetPersona(user.UserId)).Nombres
                }
            };
        }
    }
}
