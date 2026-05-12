using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.AuthModule.MicrosoftLogin.Application.Dtos;
using Abril_Backend.Features.AuthModule.MicrosoftLogin.Application.Interfaces;
using Abril_Backend.Features.AuthModule.MicrosoftLogin.Infrastructure.Interfaces;
using Abril_Backend.Features.AuthModule.MicrosoftProfile.Application.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Features.AuthModule.MicrosoftLogin.Application.Services
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
            {
                var existingPerson = await _repository.GetPersonByWorkerEmailAsync(email);
                user = existingPerson is not null
                    ? await _repository.CreateUserAndLinkPersonAsync(profile, existingPerson.PersonId)
                    : await _repository.CreateUserFromGraphAsync(profile);
            }
            else if (user.Person is null || user.Person.PersonId == 0)
            {
                var existingPerson = await _repository.GetPersonByWorkerEmailAsync(email);
                user.Person = existingPerson is not null
                    ? await _repository.LinkPersonToUserAsync(user.UserId, existingPerson.PersonId, email)
                    : await _repository.CreatePersonForUserAsync(user.UserId, profile);
            }

            var accessToken     = _jwtService.GenerateToken(user);
            var session         = await _authRepository.CreateSessionAsync(user.UserId);
            var allowedFeatures = await _authRepository.GetAllowedFeaturesAsync(user.UserId);

            return new MicrosoftLoginResponseDto
            {
                AccessToken     = accessToken,
                SessionToken    = session.Token,
                ExpiresAt       = session.ExpiresAt,
                AllowedFeatures = allowedFeatures,
                DisplayName       = profile.DisplayName,
                GivenName         = profile.GivenName,
                Surname           = profile.Surname,
                UserPrincipalName = profile.UserPrincipalName,
                Mail              = profile.Mail,
                JobTitle          = profile.JobTitle,
                OfficeLocation    = profile.OfficeLocation,
                MobilePhone       = profile.MobilePhone,
                BusinessPhones    = profile.BusinessPhones,
                Department        = profile.Department,
                PhotoBase64       = await photoTask
            };
        }
    }
}
