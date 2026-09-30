using System.Security.Cryptography;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Services.Convivir.Configuration;
using Abril_Backend.Shared.Services.Convivir.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Abril_Backend.Shared.Services.Convivir.Services
{
    public class ConvivirEnlaceService : IConvivirEnlaceService
    {
        private static readonly int RolPropietarioId = int.Parse(Roles.Propietario);

        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IUserPasswordTokenRepository _tokenRepo;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ConvivirOptions _options;

        public ConvivirEnlaceService(
            IDbContextFactory<AppDbContext> factory,
            IUserPasswordTokenRepository tokenRepo,
            IEmailService emailService,
            IConfiguration configuration,
            IOptions<ConvivirOptions> options)
        {
            _factory = factory;
            _tokenRepo = tokenRepo;
            _emailService = emailService;
            _configuration = configuration;
            _options = options.Value;
        }

        public async Task<bool> EsPropietarioAsync(int userId) =>
            await GetEmailSiEsPropietarioAsync(userId) != null;

        public async Task<string?> EnviarEnlaceSiEsPropietarioAsync(int userId, ConvivirEnlaceTipo tipo)
        {
            var email = await GetEmailSiEsPropietarioAsync(userId);
            if (email == null)
                return null;

            // Un enlace vivo a la vez: el anterior deja de servir (mismo criterio que la intranet).
            await _tokenRepo.InvalidateTokensByUserAsync(userId);

            var token = GenerarToken();
            var horas = tipo == ConvivirEnlaceTipo.Invitacion ? _options.InvitacionHoras : _options.RecuperacionHoras;

            // CreateAsync guarda también la anulación de arriba (mismo contexto).
            await _tokenRepo.CreateAsync(new UserPasswordTokenDTO
            {
                UserId = userId,
                Token = token,
                CreatedDateTime = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(horas),
                Used = false
            });

            // El correo no enlaza directo al deep link de la app: Gmail y Outlook no vuelven
            // clicables los esquemas propios (abrilconvivir://, exp://). Enlaza a una página del
            // backend que abre la app (ver ConvivirAuthController.Abrir).
            var baseUrl = (_options.UrlPublica ?? _configuration["BackendSettings:PublicUrl"] ?? "").TrimEnd('/');
            var link = $"{baseUrl}/api/v1/convivir/auth/abrir?token={Uri.EscapeDataString(token)}";

            var (asunto, titulo, texto, boton, vigencia) = tipo == ConvivirEnlaceTipo.Invitacion
                ? ("Tu cuenta de Convivir Abril",
                   "Bienvenido a Convivir Abril",
                   "Se creó tu cuenta en Convivir Abril, la app donde podrás seguir el avance de tu nuevo hogar. Ingresarás con tu DNI y la contraseña que crees con este enlace.",
                   "Crear mi contraseña",
                   VigenciaTexto(horas))
                : ("Restablece tu contraseña de Convivir Abril",
                   "Restablece tu contraseña",
                   "Recibimos una solicitud para restablecer la contraseña de tu cuenta de Convivir Abril.",
                   "Crear nueva contraseña",
                   VigenciaTexto(horas));

            var body = $@"
                <div style='font-family: Arial, Helvetica, sans-serif; max-width: 520px; margin: 0 auto; color: #1f2937;'>
                    <h2 style='color: #005D9D; margin-bottom: 8px;'>{titulo}</h2>
                    <p style='font-size: 15px; line-height: 1.5;'>{texto}</p>
                    <p style='margin: 28px 0;'>
                        <a href='{link}' target='_blank'
                           style='display:inline-block; padding:12px 24px; background-color:#005D9D; color:#ffffff; text-decoration:none; border-radius:8px; font-weight:bold;'>
                            {boton}
                        </a>
                    </p>
                    <p style='font-size: 13px; color: #6b7280;'>
                        Abre este correo desde el celular donde tienes instalada la app. El enlace vence en {vigencia}.
                        Si no esperabas este correo, puedes ignorarlo.
                    </p>
                </div>";

            await _emailService.SendAsync(
                to: new List<string> { email },
                subject: asunto,
                body: body,
                isHtml: true);

            return email;
        }

        /// <summary>
        /// Correo del usuario si tiene el rol PROPIETARIO vigente. Filtra <c>user_role.state</c> y
        /// <c>role.state</c> (el login de la intranet no lo hace). No mira <c>app_user.active</c>:
        /// una cuenta creada con estado inicial INACTIVO igual recibe su invitación.
        /// </summary>
        private async Task<string?> GetEmailSiEsPropietarioAsync(int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var emails = await ctx.Database
                .SqlQuery<string>($"""
                    SELECT u.email AS "Value"
                    FROM app_user u
                    JOIN user_role ur ON ur.user_id = u.user_id
                    JOIN role r       ON r.role_id  = ur.role_id
                    WHERE u.user_id = {userId}
                      AND ur.role_id = {RolPropietarioId}
                      AND u.state
                      AND ur.state
                      AND r.state
                      AND u.email IS NOT NULL
                    """)
                .ToListAsync();

            return emails.FirstOrDefault();
        }

        private static string VigenciaTexto(int horas) =>
            horas % 24 == 0
                ? (horas == 24 ? "1 día" : $"{horas / 24} días")
                : (horas == 1 ? "1 hora" : $"{horas} horas");

        private static string GenerarToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }
    }
}
