using System.Security.Cryptography;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Contractors.ContractorManagement.Application.Dtos;
using Abril_Backend.Features.Contractors.ContractorManagement.Application.Interfaces;
using Abril_Backend.Features.Contractors.ContractorManagement.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Interfaces;
using Microsoft.Extensions.Options;

namespace Abril_Backend.Features.Contractors.ContractorManagement.Application.Services
{
    public class ContractorManagementService : IContractorManagementService
    {
        private const int ApprovedContractorStateId = 2;

        private readonly IContractorManagementRepository _repository;
        private readonly IEmailService _emailService;
        private readonly FrontendSettings _frontendSettings;

        public ContractorManagementService(
            IContractorManagementRepository repository,
            IEmailService emailService,
            IOptions<FrontendSettings> frontendSettings)
        {
            _repository = repository;
            _emailService = emailService;
            _frontendSettings = frontendSettings.Value;
        }

        public async Task<PagedResult<ContributorPagedDto>> GetPaged(ContributorFilterDto filter)
        {
            return await _repository.GetPaged(filter);
        }

        public async Task Approve(int contractorId, int userId)
        {
            await _repository.Approve(contractorId, userId);
        }

        public async Task Reject(int contractorId, int userId)
        {
            await _repository.Reject(contractorId, userId);
        }

        public async Task SendCredentials(int contractorId, int adminUserId)
        {
            var contractor = await _repository.GetWithEmails(contractorId);
            if (contractor == null)
                throw new AbrilException("Contratista no encontrado.", 404);

            if (contractor.ContractorStateId != ApprovedContractorStateId)
                throw new AbrilException("Solo se pueden enviar credenciales a contratistas aprobados.", 400);

            if (contractor.Emails.Count == 0)
                throw new AbrilException("El contratista no tiene correos registrados.", 400);

            var token = GenerateToken();
            var expiry = DateTime.UtcNow.AddHours(24);
            await _repository.SetActivationToken(contractorId, token, expiry);

            var link = $"{_frontendSettings.ContractorCredentialsUrl}?token={token}";

            var body = $@"
                <p>Estimado representante de <strong>{contractor.ContributorName}</strong>,</p>
                <p>Su empresa ha sido aprobada en el proceso de homologación de contratistas de <strong>Abril Grupo Inmobiliario</strong>.</p>
                <p>Para activar su acceso al sistema, haga clic en el siguiente enlace y registre sus credenciales:</p>
                <p>
                    <a href='{link}' target='_blank'
                    style='display:inline-block; padding:10px 20px; background-color:#64BC04; color:#ffffff; text-decoration:none; border-radius:6px; font-weight:bold;'>
                        Registrar credenciales
                    </a>
                </p>
                <p style='font-size: 12px; color: #666;'>
                    Este enlace expirará en 24 horas. Si no solicitó este acceso, puede ignorar este correo.
                </p>
            ";

            await _emailService.SendAsync(
                to: contractor.Emails,
                subject: "Activa tu cuenta de contratista — Abril Grupo Inmobiliario",
                body: body,
                isHtml: true
            );
        }

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
