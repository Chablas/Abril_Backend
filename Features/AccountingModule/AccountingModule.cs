using Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Application.Interfaces;
using Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Application.Services;
using Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.AccountingModule.Features.InvoicesFeature.Infrastructure.Repositories;

namespace Abril_Backend.Features.AccountingModule
{
    public static class AccountingModule
    {
        public static IServiceCollection AddAccountingModule(this IServiceCollection services)
        {
            // Feature: Facturas
            services.AddScoped<IInvoiceRepository, InvoiceRepository>();
            services.AddScoped<IInvoiceService, InvoiceService>();

            return services;
        }
    }
}
