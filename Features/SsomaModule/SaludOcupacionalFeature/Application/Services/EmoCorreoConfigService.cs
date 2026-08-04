using System.Text.RegularExpressions;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Configuracion;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services
{
    /// <summary>
    /// Configuración de los destinatarios de los correos de programación de EMO
    /// (programación manual desde /emos y programación automática del cron diario).
    /// </summary>
    public class EmoCorreoConfigService : IEmoCorreoConfigService
    {
        private static readonly Regex EmailRegex =
            new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        private static readonly string[] TiposValidos =
            { EmoCorreoTipoCodigo.Principal, EmoCorreoTipoCodigo.Copia };

        private readonly IEmoCorreoConfigRepository _repo;

        public EmoCorreoConfigService(IEmoCorreoConfigRepository repo)
        {
            _repo = repo;
        }

        public Task<EmoCorreosConfigDto> GetConfig() => _repo.GetConfigAsync();

        public Task<int> Create(EmoCorreoDestinatarioCreateDto dto)
        {
            var tipo = (dto.Tipo ?? string.Empty).Trim().ToUpperInvariant();
            if (!TiposValidos.Contains(tipo))
                throw new AbrilException("El tipo de destinatario debe ser PRINCIPAL o COPIA.", 400);

            var email = ValidarEmail(dto.Email);
            return _repo.CreateAsync(tipo, email, dto.Nombre);
        }

        public Task Update(int id, EmoCorreoDestinatarioUpdateDto dto)
        {
            var email = ValidarEmail(dto.Email);
            return _repo.UpdateAsync(id, email, dto.Nombre);
        }

        public Task SetActive(int id, bool active) => _repo.SetActiveAsync(id, active);

        public Task Delete(int id) => _repo.DeleteAsync(id);

        private static string ValidarEmail(string? email)
        {
            var valor = (email ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(valor))
                throw new AbrilException("El correo es obligatorio.", 400);
            if (valor.Length > 150)
                throw new AbrilException("El correo no puede superar los 150 caracteres.", 400);
            if (!EmailRegex.IsMatch(valor))
                throw new AbrilException("El correo no tiene un formato válido.", 400);
            return valor;
        }
    }
}
