using Abril_Backend.Features.MicrosoftAuth.MicrosoftLogin.Application.Interfaces;
using Abril_Backend.Features.MicrosoftAuth.MicrosoftLogin.Application.Services;
using Abril_Backend.Features.MicrosoftAuth.MicrosoftLogin.Infrastructure.Interfaces;
using Abril_Backend.Features.MicrosoftAuth.MicrosoftLogin.Infrastructure.Repositories;
using Abril_Backend.Features.MicrosoftAuth.MicrosoftProfile.Application.Interfaces;
using Abril_Backend.Features.MicrosoftAuth.MicrosoftProfile.Application.Services;

using Abril_Backend.Features.MicrosoftAuth.ContractorCredentials.Application.Interfaces;
using Abril_Backend.Features.MicrosoftAuth.ContractorCredentials.Application.Services;
using Abril_Backend.Features.MicrosoftAuth.ContractorCredentials.Infrastructure.Interfaces;
using Abril_Backend.Features.MicrosoftAuth.ContractorCredentials.Infrastructure.Repositories;

namespace Abril_Backend.Features.MicrosoftAuth
{
    public static class AuthModule
    {
        public static IServiceCollection AddAuthModule(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHttpClient<IMicrosoftProfileService, MicrosoftProfileService>(client =>
            {
                client.BaseAddress = new Uri("https://graph.microsoft.com/");
            });

            services.AddScoped<IMicrosoftLoginRepository, MicrosoftLoginRepository>();
            services.AddScoped<IMicrosoftLoginService, MicrosoftLoginService>();

            // ContractorCredentials
            services.AddScoped<IContractorCredentialsRepository, ContractorCredentialsRepository>();
            services.AddScoped<IContractorCredentialsService, ContractorCredentialsService>();

            return services;
        }
    }
}
