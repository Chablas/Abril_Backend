using System.Text.RegularExpressions;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Services
{
    /// <inheritdoc cref="ICorreoConfigService"/>
    public class CorreoConfigService : ICorreoConfigService
    {
        private readonly ICorreoConfigRepository _repo;

        public CorreoConfigService(ICorreoConfigRepository repo)
        {
            _repo = repo;
        }

        /// <summary>
        /// Correos que administra la pantalla de Solicitud de Personal, en el orden del flujo.
        /// LONG_LIST queda fuera a propósito: lo envía GTH y se configura desde Reclutamiento.
        /// </summary>
        private static readonly string[] CorreosDeLaPantalla =
        {
            CorreoTipoReclutamiento.AprobacionGg,
            CorreoTipoReclutamiento.Solicitud,
            CorreoTipoReclutamiento.LongListDecision,
            CorreoTipoReclutamiento.FinalistaDecision,
        };

        private static readonly Regex EmailRegex =
            new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        public Task<CorreoConfigDto> GetConfig() => _repo.GetConfigAsync(CorreosDeLaPantalla);

        public Task<int> CrearAdicional(CorreoAdicionalCreateDto dto, int? userId)
        {
            var tipoCodigo = ResolverTipo(dto?.EventoCodigo);
            var email      = ValidarEmail(dto?.Email);
            return _repo.CreateAdicionalAsync(tipoCodigo, email, dto?.Nombre, dto?.EsCopia ?? false, userId);
        }

        public Task ActualizarAdicional(int destinatarioId, CorreoAdicionalUpdateDto dto, int? userId)
        {
            var email = ValidarEmail(dto?.Email);
            return _repo.UpdateAdicionalAsync(destinatarioId, email, dto?.Nombre, dto?.EsCopia ?? false, userId);
        }

        public Task SetDestinatarioActive(int destinatarioId, bool active, int? userId) =>
            _repo.SetDestinatarioActiveAsync(destinatarioId, active, userId);

        public Task SetCorreoActive(string tipoSlug, bool active, int? userId) =>
            _repo.SetTipoActiveAsync(ResolverTipo(tipoSlug), active, userId);

        public Task EliminarAdicional(int destinatarioId, int? userId) =>
            _repo.DeleteAdicionalAsync(destinatarioId, userId);

        /// <summary>
        /// Acepta tanto el código estable (APROBACION_GG) como el slug de la URL
        /// (aprobacion-gg) y valida que sea uno de los correos de esta pantalla, para que un
        /// id ajeno no pueda tocar la configuración de otra feature.
        /// </summary>
        private static string ResolverTipo(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                throw new AbrilException("Falta indicar de qué correo se trata.", 400);

            var codigo = CorreoTipoReclutamiento.FromSlug(valor) ?? valor.Trim().ToUpperInvariant();
            if (!CorreosDeLaPantalla.Contains(codigo, StringComparer.OrdinalIgnoreCase))
                throw new AbrilException("Ese correo no se configura desde esta pantalla.", 400);

            return codigo;
        }

        private static string ValidarEmail(string? email)
        {
            var e = email?.Trim().ToLowerInvariant() ?? string.Empty;
            if (e.Length == 0)
                throw new AbrilException("El correo es obligatorio.", 400);
            if (!EmailRegex.IsMatch(e))
                throw new AbrilException($"«{e}» no tiene un formato de correo válido.", 400);
            return e;
        }
    }
}
