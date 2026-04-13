using Abril_Backend.Features.MicrosoftAuth.MicrosoftLogin.Application.Interfaces;
using Abril_Backend.Features.MicrosoftAuth.MicrosoftLogin.Application.Services;
using Abril_Backend.Features.MicrosoftAuth.MicrosoftLogin.Infrastructure.Interfaces;
using Abril_Backend.Features.MicrosoftAuth.MicrosoftLogin.Infrastructure.Repositories;
using Abril_Backend.Features.MicrosoftAuth.MicrosoftProfile.Application.Interfaces;
using Abril_Backend.Features.MicrosoftAuth.MicrosoftProfile.Application.Services;

namespace Abril_Backend.Features.MicrosoftAuth
{
    public static class MicrosoftAuthModule
    {
        public static IServiceCollection AddMicrosoftAuthModule(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHttpClient<IMicrosoftProfileService, MicrosoftProfileService>(client =>
            {
                client.BaseAddress = new Uri("https://graph.microsoft.com/");
            });

            services.AddScoped<IMicrosoftLoginRepository, MicrosoftLoginRepository>();
            services.AddScoped<IMicrosoftLoginService, MicrosoftLoginService>();

            return services;
        }
    }
}
