using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Application.Interfaces;

namespace Abril_Backend.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IAuthRepository _authRepository;
        private readonly IJWTService _jwtService;
        public AuthService(
            IAuthRepository authRepository,
            IJWTService jwtService
            )
        {
            _authRepository = authRepository;
            _jwtService = jwtService;
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
    }
}