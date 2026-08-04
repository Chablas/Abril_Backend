using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Services
{
    public class PostulanteFormularioService : IPostulanteFormularioService
    {
        private readonly IPostulanteFormularioRepository _repo;
        private readonly IEmailService _email;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PostulanteFormularioService> _logger;

        public PostulanteFormularioService(
            IPostulanteFormularioRepository repo,
            IEmailService email,
            IConfiguration configuration,
            ILogger<PostulanteFormularioService> logger)
        {
            _repo          = repo;
            _email         = email;
            _configuration = configuration;
            _logger        = logger;
        }

        private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        public async Task<PostulanteFormularioPublicoDto> GetPublico(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new AbrilException("Enlace del formulario no válido.", 400);

            var dto = await _repo.GetByToken(token.Trim());
            if (dto == null)
                throw new AbrilException("El enlace del formulario no es válido o ya no está disponible.", 404);
            return dto;
        }

        public Task GuardarPublico(string token, PostulanteFormularioRespuestasDto respuestas)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new AbrilException("Enlace del formulario no válido.", 400);
            if (respuestas == null)
                throw new AbrilException("No se recibieron los datos del formulario.", 400);
            return _repo.GuardarRespuestasByToken(token.Trim(), respuestas);
        }

        public async Task<FormularioAccionResultDto> Enviar(int candidatoId, EnviarFormularioDto dto, int? userId)
        {
            var correo = dto?.Correo?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(correo) || !EmailRegex.IsMatch(correo))
                throw new AbrilException("Ingresa un correo electrónico válido para enviar el formulario.", 400);

            // Token de acceso público (hex, url-safe). Se usa solo si el formulario aún no existía.
            var nuevoToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

            var ctx = await _repo.PrepararEnvio(candidatoId, correo, nuevoToken, userId);

            // Enviar el correo con el enlace del formulario. Bloqueante: si falla, se informa (el
            // formulario ya quedó en ENVIADO y GTH puede reintentar el envío).
            var frontendUrl = _configuration["App:FrontendUrl"]?.TrimEnd('/') ?? string.Empty;
            var link = $"{frontendUrl}/postulante/formulario?token={Uri.EscapeDataString(ctx.Token)}";

            try
            {
                await _email.SendAsync(
                    to:      new List<string> { ctx.Correo },
                    subject: $"Formulario de postulante — {ctx.Puesto} · Abril Grupo Inmobiliario",
                    body:    ConstruirCuerpoEnvio(ctx, link),
                    isHtml:  true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló el correo del formulario del postulante (candidato {CandidatoId})", candidatoId);
                throw new AbrilException(
                    "El formulario quedó registrado, pero no se pudo enviar el correo al postulante. Reintenta el envío.", 502);
            }

            return new FormularioAccionResultDto
            {
                Message    = $"Formulario enviado a {ctx.Correo}.",
                Formulario = ctx.Resumen,
            };
        }

        public Task<FormularioRevisionDto> GetRevision(int candidatoId) => _repo.GetRevision(candidatoId);

        public async Task<FormularioAccionResultDto> Decision(int candidatoId, FormularioDecisionDto dto, int? userId)
        {
            if (dto == null)
                throw new AbrilException("No se recibió la decisión del formulario.", 400);

            var resumen = await _repo.RegistrarDecision(candidatoId, dto.Aprobado, dto.Motivo, userId);
            var message = dto.Aprobado ? "Formulario aprobado." : "Formulario rechazado.";
            return new FormularioAccionResultDto { Message = message, Formulario = resumen };
        }

        private static string ConstruirCuerpoEnvio(EnviarFormularioContextoDto ctx, string link)
        {
            static string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
            var nombre = string.IsNullOrWhiteSpace(ctx.CandidatoNombre) ? "postulante" : Esc(ctx.CandidatoNombre);

            return $"""
                <div style="font-family:Arial,sans-serif;max-width:640px">
                  <div style="background:#005D9D;padding:14px 18px">
                    <h2 style="color:#fff;margin:0;font-size:18px">Formulario del postulante</h2>
                  </div>
                  <div style="padding:18px;border:1px solid #e5e7eb;border-top:none">
                    <p style="font-size:13px;margin-top:0">Estimado(a) {nombre},</p>
                    <p style="font-size:13px">
                      Gracias por participar del proceso de selección en <b>Abril Grupo Inmobiliario</b>
                      para la posición <b>{Esc(ctx.Puesto)}</b>. Para continuar, completa el formulario de
                      información del postulante en el siguiente enlace:
                    </p>
                    <p style="margin:18px 0">
                      <a href="{link}" style="background:#005D9D;color:#fff;padding:12px 22px;border-radius:8px;text-decoration:none;display:inline-block;font-size:14px">Completar formulario</a>
                    </p>
                    <p style="font-size:12px;color:#555">Si el botón no funciona, copia y pega este enlace en tu navegador:<br>
                      <span style="color:#005D9D;word-break:break-all">{Esc(link)}</span>
                    </p>
                    <p style="font-size:11px;color:#888;margin-top:18px">Correo automático de Abril One · Gestión GTH · Reclutamiento.</p>
                  </div>
                </div>
                """;
        }
    }
}
