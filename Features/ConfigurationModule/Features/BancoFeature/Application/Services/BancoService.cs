using System.Text.RegularExpressions;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.ConfigurationModule.Features.BancoFeature.Application.Dtos;
using Abril_Backend.Features.ConfigurationModule.Features.BancoFeature.Application.Interfaces;
using Abril_Backend.Features.ConfigurationModule.Features.BancoFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.ConfigurationModule.Features.BancoFeature.Application.Services
{
    /// <summary>
    /// Catálogo de bancos (Configuración → Bancos). Es la contraparte del banco que cada razón
    /// social del grupo tiene asignado: el formulario de bienvenida le dice al nuevo colaborador
    /// con qué banco trabaja SU razón social antes de preguntarle si quiere su cuenta sueldo.
    /// </summary>
    public class BancoService : IBancoService
    {
        private readonly IBancoRepository _repo;

        public BancoService(IBancoRepository repo) => _repo = repo;

        /// <summary>
        /// El código es la clave estable con la que los scripts reconocen al banco, así que se
        /// restringe a lo que puede vivir en un identificador: letras, dígitos, guion y guion bajo.
        /// </summary>
        private static readonly Regex CodigoRegex = new(@"^[A-Z0-9_-]{2,30}$", RegexOptions.Compiled);

        public Task<List<BancoDto>> List() => _repo.List();

        public Task<BancoDto> Create(BancoUpsertDto dto, int? userId)
        {
            Validar(dto, exigeCodigo: true);
            return _repo.Create(dto, userId);
        }

        public Task<BancoDto> Update(int bancoId, BancoUpsertDto dto, int? userId)
        {
            Validar(dto, exigeCodigo: false);
            return _repo.Update(bancoId, dto, userId);
        }

        public Task Delete(int bancoId, int? userId) => _repo.Delete(bancoId, userId);

        private static void Validar(BancoUpsertDto? dto, bool exigeCodigo)
        {
            if (dto == null)
                throw new AbrilException("No se recibieron los datos del banco.", 400);

            if (string.IsNullOrWhiteSpace(dto.Nombre))
                throw new AbrilException("El nombre del banco es obligatorio.", 400);
            if (dto.Nombre.Trim().Length > 150)
                throw new AbrilException("El nombre del banco no puede pasar de 150 caracteres.", 400);

            if (!exigeCodigo) return;

            if (string.IsNullOrWhiteSpace(dto.Codigo))
                throw new AbrilException("El código del banco es obligatorio.", 400);
            if (!CodigoRegex.IsMatch(dto.Codigo.Trim().ToUpperInvariant()))
                throw new AbrilException(
                    "El código debe tener entre 2 y 30 caracteres y solo letras, números, guion o guion bajo (ej. BCP, BBVA).",
                    400);
        }
    }
}
