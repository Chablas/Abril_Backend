using System.Security.Cryptography;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.AuthModule.UserFeature.Application.Dtos;
using Abril_Backend.Features.AuthModule.UserFeature.Application.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Models;
using Microsoft.Extensions.Options;

namespace Abril_Backend.Features.AuthModule.UserFeature.Application.Services
{
    public class UserFeatureService : IUserFeatureService
    {
        private readonly IUserFeatureRepository _repo;
        private readonly IUserPasswordTokenRepository _tokenRepo;
        private readonly IEmailService _emailService;
        private readonly FrontendSettings _frontendSettings;

        public UserFeatureService(
            IUserFeatureRepository repo,
            IUserPasswordTokenRepository tokenRepo,
            IEmailService emailService,
            IOptions<FrontendSettings> frontendSettings)
        {
            _repo = repo;
            _tokenRepo = tokenRepo;
            _emailService = emailService;
            _frontendSettings = frontendSettings.Value;
        }

        public Task<PagedResult<UserListItemDto>> GetPaged(int page, int pageSize) =>
            _repo.GetPaged(page, pageSize);

        public async Task Create(UserFeatureCreateDto dto)
        {
            var user = await _repo.Create(dto);

            var token = GenerateToken();
            await _tokenRepo.CreateAsync(new UserPasswordTokenDTO
            {
                UserId = user.UserId,
                Token = token,
                CreatedDateTime = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                Used = false
            });

            var link = $"{_frontendSettings.SetPasswordUrl}?token={token}";
            var body = $@"
                <p>Estimado usuario,</p>
                <p>Le informamos que se ha creado una cuenta a su nombre en nuestro sistema.</p>
                <p>Para activar su cuenta y establecer su contraseña, haga clic en el siguiente enlace:</p>
                <p>
                    <a href='{link}' target='_blank'
                        style='display:inline-block; padding:10px 20px; background-color:#1a73e8; color:#ffffff; text-decoration:none; border-radius:4px;'>
                        Activar cuenta
                    </a>
                </p>
                <p style='font-size: 12px; color: #666;'>
                    Este enlace expirará en 24 horas. Si usted no esperaba este correo, puede ignorarlo o contactar a su administrador.
                </p>
            ";

            await _emailService.SendAsync(
                to: new List<string> { user.Email },
                subject: "Completa tu registro",
                body: body,
                isHtml: true,
                bcc: new List<string> { "calvarez@abril.pe" });
        }

        public Task Update(int userId, UserFeatureUpdateDto dto, int updatedUserId) =>
            _repo.Update(userId, dto, updatedUserId);

        public Task ToggleActive(int userId, int updatedUserId) =>
            _repo.ToggleActive(userId, updatedUserId);

        public Task Delete(int userId, int updatedUserId) =>
            _repo.Delete(userId, updatedUserId);

        private static string GenerateToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }
    }
}
