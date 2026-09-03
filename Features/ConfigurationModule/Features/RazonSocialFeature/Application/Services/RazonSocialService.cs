using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.ConfigurationModule.Features.RazonSocialFeature.Application.Dtos;
using Abril_Backend.Features.ConfigurationModule.Features.RazonSocialFeature.Application.Interfaces;
using Abril_Backend.Features.ConfigurationModule.Features.RazonSocialFeature.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.Sunat.Dtos;
using Abril_Backend.Shared.Services.Sunat.Interfaces;

namespace Abril_Backend.Features.ConfigurationModule.Features.RazonSocialFeature.Application.Services
{
    /// <summary>
    /// Configuración → Razones Sociales: el alta (con consulta a SUNAT) y la edición de las
    /// empresas del sistema, propias y de terceros.
    ///
    /// Vive acá y no en el catálogo de empresas de SSOMA porque aquel controller exige el rol
    /// AdministradorSsoma a nivel de clase, y en ASP.NET Core los [Authorize] de clase y de método
    /// se COMBINAN: quien administra Configuración pero no es admin de SSOMA recibía 403.
    /// </summary>
    public class RazonSocialService : IRazonSocialService
    {
        private readonly IRazonSocialRepository _repo;
        private readonly ISunatService _sunat;

        public RazonSocialService(IRazonSocialRepository repo, ISunatService sunat)
        {
            _repo  = repo;
            _sunat = sunat;
        }

        public Task<RazonSocialBandejaDto> GetBandeja() => _repo.GetBandeja();

        public Task<SunatContributorDto?> ConsultarRuc(string ruc) => _sunat.GetByRucAsync(ruc);

        public Task<RazonSocialDto> Create(RazonSocialCreateDto dto, int? userId)
        {
            if (dto == null)
                throw new AbrilException("No se recibieron los datos de la razón social.", 400);

            Exigir(dto.Ruc, "El RUC es obligatorio.");
            if (dto.Ruc.Trim().Length != 11)
                throw new AbrilException("El RUC debe tener 11 dígitos.", 400);

            Exigir(dto.Nombre,        "La razón social es obligatoria.");
            Exigir(dto.Direccion,     "La dirección es obligatoria.");
            Exigir(dto.TipoActividad, "El tipo de actividad es obligatorio.");
            Exigir(dto.Distrito,      "El distrito es obligatorio.");
            Exigir(dto.Provincia,     "La provincia es obligatoria.");
            Exigir(dto.Departamento,  "El departamento es obligatorio.");

            // El banco solo tiene sentido en una razón social del grupo. Si vino sin marcar «Es
            // Abril» se ignora en vez de fallar: la pantalla ya no lo deja elegir, así que un 400
            // acá sería un error por un campo que el usuario no ve.
            if (!dto.EsAbril) dto.BancoId = null;

            return _repo.Create(dto, userId);
        }

        public Task<RazonSocialDto> Update(int contributorId, RazonSocialUpdateDto dto, int? userId)
        {
            if (dto == null)
                throw new AbrilException("No se recibieron los datos de la razón social.", 400);

            Exigir(dto.Direccion, "La dirección es obligatoria.");

            if (!dto.EsAbril) dto.BancoId = null;

            return _repo.Update(contributorId, dto, userId);
        }

        private static void Exigir(string? valor, string mensaje)
        {
            if (string.IsNullOrWhiteSpace(valor)) throw new AbrilException(mensaje, 400);
        }
    }
}
