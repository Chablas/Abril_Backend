using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Application.Interfaces;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using System.Security.Cryptography;

namespace Abril_Backend.Application.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IUserRegistrationTokenRepository _tokenRepository;
        private readonly IEmailService _emailService;
        public UserService(
            IUserRepository userRepository,
            IUserRegistrationTokenRepository tokenRepository,
            IEmailService emailService
            )
        {
            _userRepository = userRepository;
            _tokenRepository = tokenRepository;
            _emailService = emailService;
        }
        public async Task<PagedResult<UserDTO>> GetPagedFactory(int page, int pageSize)
        {
            var registros = await _userRepository.GetPagedFactory(page, pageSize);
            return registros;
        }

        public async Task Create(UserCreateDTO dto)
        {
            var user = await _userRepository.Create(dto);
            if (user == null)
                throw new AbrilException("La persona ya tiene un usuario registrado.");
            var token = GenerateToken();

            await _tokenRepository.CreateAsync(new UserRegistrationTokenDTO
            {
                UserId = user.UserId,
                Token = token,
                CreatedDateTime = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                Used = false
            });

            var link = $"https://abril-frontend.onrender.com/auth/complete-registration?token={token}";

            await _emailService.SendAsync(
                to: new List<string> { user.Person.Email },
                subject: "Completa tu registro",
                body: $"Hola,\n\nCompleta tu registro aquí:\n{link}\n\nEste enlace expirará en 24 horas.",
                isHtml: false
            );
        }

        public async Task CompleteRegistration(CompleteRegistrationDTO dto)
        {
            var tokenEntity = await _tokenRepository.GetValidTokenAsync(dto.Token);
            if (tokenEntity == null)
                throw new AbrilException("Token inválido o expirado.");

            await _userRepository.SetPassword(
                tokenEntity.UserId,
                dto.Password
            );

            tokenEntity.Used = true;
            await _tokenRepository.SaveAsync();
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