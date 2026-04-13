using Abril_Backend.Features.MicrosoftAuth.MicrosoftProfile.Application.Dtos;

namespace Abril_Backend.Features.MicrosoftAuth.MicrosoftProfile.Application.Interfaces
{
    public interface IMicrosoftProfileService
    {
        Task<MicrosoftProfileDto?> GetProfile(string accessToken);
        Task<string?> GetPhotoBase64(string accessToken);
    }
}
