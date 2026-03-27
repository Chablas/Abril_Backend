using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Application.Interfaces;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace Abril_Backend.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IAuthRepository _authRepository;
        private readonly IJWTService _jwtService;
        private readonly IUserPasswordResetTokenRepository _resetTokenRepository;
        private readonly IUserRepository _userRepository;
        private readonly IEmailService _emailService;
        private readonly FrontendSettings _frontendSettings;

        public AuthService(
            IAuthRepository authRepository,
            IJWTService jwtService,
            IUserPasswordResetTokenRepository resetTokenRepository,
            IUserRepository userRepository,
            IEmailService emailService,
            IOptions<FrontendSettings> frontendSettings
            )
        {
            _authRepository = authRepository;
            _jwtService = jwtService;
            _resetTokenRepository = resetTokenRepository;
            _userRepository = userRepository;
            _emailService = emailService;
            _frontendSettings = frontendSettings.Value;
        }

        public async Task<LoginResponseDTO> Login(LoginDTO dto)
        {
            var user = await _authRepository.ValidateUserAsync(
                dto.Email,
                dto.Password
            );

            if (user == null)
                throw new AbrilException("Credenciales inválidas.", 401);

            var accessToken = _jwtService.GenerateToken(user);
            var session = await _authRepository.CreateSessionAsync(user.UserId);

            return new LoginResponseDTO
            {
                AccessToken = accessToken,
                SessionToken = session.Token,
                ExpiresAt = session.ExpiresAt
            };
        }

        public async Task ForgotPassword(ForgotPasswordDTO dto)
        {
            var user = await _authRepository.GetUserByIdAsync(dto.UserId);

            if (user == null)
                return;

            await _resetTokenRepository.InvalidatePreviousTokensAsync(user.Value.UserId);

            var token = GenerateToken();

            await _resetTokenRepository.CreateAsync(new UserPasswordResetToken
            {
                UserId = user.Value.UserId,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                Used = false,
                CreatedDateTime = DateTime.UtcNow
            });

            var link = $"{_frontendSettings.ResetPasswordUrl}?token={token}";

            var body = $@"
                <p>Hola,</p>
                <p>Hemos recibido una solicitud para restablecer tu contraseña.</p>
                <p>Haz clic en el siguiente enlace para continuar:</p>
                <p>
                    👉 <a href='{link}' target='_blank'>Restablecer contraseña</a>
                </p>
                <p style='font-size: 12px; color: #666;'>
                    Este enlace expirará en 1 hora. Si no solicitaste este cambio, ignora este correo.
                </p>
            ";

            await _emailService.SendAsync(
                to: new List<string> { user.Value.Email },
                subject: "Restablece tu contraseña",
                body: body,
                isHtml: true,
                bcc: new List<string> { "calvarez@abril.pe" }
            );
        }

        public async Task ResetPassword(ResetPasswordDTO dto)
        {
            if (dto.Password != dto.ConfirmPassword)
                throw new AbrilException("Las contraseñas no coinciden.", 400);

            var tokenEntity = await _resetTokenRepository.GetValidTokenAsync(dto.Token);

            if (tokenEntity == null)
                throw new AbrilException("Token inválido o expirado.", 400);

            await _userRepository.SetPassword(tokenEntity.UserId, dto.Password);

            tokenEntity.Used = true;
            await _resetTokenRepository.SaveAsync();

        }

        private string GenerateToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }
    }
}