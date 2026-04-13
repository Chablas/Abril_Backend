using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.MicrosoftAuth.MicrosoftLogin.Application.Dtos;
using Abril_Backend.Features.MicrosoftAuth.MicrosoftLogin.Application.Interfaces;
using Abril_Backend.Features.MicrosoftAuth.MicrosoftLogin.Infrastructure.Interfaces;
using Abril_Backend.Features.MicrosoftAuth.MicrosoftProfile.Application.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Features.MicrosoftAuth.MicrosoftLogin.Application.Services
{
    public class MicrosoftLoginService : IMicrosoftLoginService
    {
        private readonly IMicrosoftProfileService _profileService;
        private readonly IMicrosoftLoginRepository _repository;
        private readonly IJWTService _jwtService;
        private readonly IAuthRepository _authRepository;

        public MicrosoftLoginService(
            IMicrosoftProfileService profileService,
            IMicrosoftLoginRepository repository,
            IJWTService jwtService,
            IAuthRepository authRepository)
        {
            _profileService = profileService;
            _repository = repository;
            _jwtService = jwtService;
            _authRepository = authRepository;
        }

        public async Task<MicrosoftLoginResponseDto> Login(string graphAccessToken)
        {
            var profileTask = _profileService.GetProfile(graphAccessToken);
            var photoTask = _profileService.GetPhotoBase64(graphAccessToken);

            await Task.WhenAll(profileTask, photoTask);

            var profile = await profileTask;
            if (profile is null)
                throw new AbrilException("No se pudo obtener el perfil de Microsoft.", 401);

            var email = profile.Mail ?? profile.UserPrincipalName;

            var user = await _repository.GetUserByEmailAsync(email);

            if (user is null)
                user = await _repository.CreateUserFromGraphAsync(profile);

            var accessToken = _jwtService.GenerateToken(user);
            var session = await _authRepository.CreateSessionAsync(user.UserId);

            return new MicrosoftLoginResponseDto
            {
                AccessToken = accessToken,
                SessionToken = session.Token,
                ExpiresAt = session.ExpiresAt,
                DisplayName = profile.DisplayName,
                GivenName = profile.GivenName,
                Surname = profile.Surname,
                UserPrincipalName = profile.UserPrincipalName,
                Mail = profile.Mail,
                JobTitle = profile.JobTitle,
                OfficeLocation = profile.OfficeLocation,
                MobilePhone = profile.MobilePhone,
                BusinessPhones = profile.BusinessPhones,
                PhotoBase64 = await photoTask
            };
        }
    }
}
