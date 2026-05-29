using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Catalogos;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services
{
    public class CatalogosService : ICatalogosService
    {
        private readonly ICatalogosRepository _repo;

        public CatalogosService(ICatalogosRepository repo)
        {
            _repo = repo;
        }

        public Task<List<ClinicaDto>> ListClinicas(bool soloActivos) => _repo.ListClinicas(soloActivos);

        public Task<ClinicaDto> GetClinicaById(int id) => _repo.GetClinicaById(id);

        public Task<ClinicaDto> CreateClinica(ClinicaUpsertDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre))
                throw new AbrilException("El nombre de la clínica es obligatorio.", 400);
            return _repo.CreateClinica(dto);
        }

        public Task<ClinicaDto> UpdateClinica(int id, ClinicaUpsertDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre))
                throw new AbrilException("El nombre de la clínica es obligatorio.", 400);
            return _repo.UpdateClinica(id, dto);
        }

        public Task<List<MedicoOcupacionalDto>> ListMedicos(bool soloActivos) => _repo.ListMedicos(soloActivos);

        public Task<MedicoOcupacionalDto> CreateMedico(MedicoOcupacionalUpsertDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.ApellidoNombre))
                throw new AbrilException("El nombre del médico es obligatorio.", 400);
            return _repo.CreateMedico(dto);
        }

        public Task<MedicoOcupacionalDto> UpdateMedico(int id, MedicoOcupacionalUpsertDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.ApellidoNombre))
                throw new AbrilException("El nombre del médico es obligatorio.", 400);
            return _repo.UpdateMedico(id, dto);
        }

        public Task<List<EmoTipoDto>> ListEmoTipos(bool soloActivos) => _repo.ListEmoTipos(soloActivos);

        public Task<EmoTipoDto> CreateEmoTipo(EmoTipoUpsertDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre))
                throw new AbrilException("El nombre del tipo de EMO es obligatorio.", 400);
            if (dto.VigenciaMeses < 0)
                throw new AbrilException("La vigencia en meses debe ser mayor o igual a 0.", 400);
            return _repo.CreateEmoTipo(dto);
        }

        public Task<EmoTipoDto> UpdateEmoTipo(int id, EmoTipoUpsertDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre))
                throw new AbrilException("El nombre del tipo de EMO es obligatorio.", 400);
            if (dto.VigenciaMeses < 0)
                throw new AbrilException("La vigencia en meses debe ser mayor o igual a 0.", 400);
            return _repo.UpdateEmoTipo(id, dto);
        }

        public Task<List<ExamenTipoDto>> ListExamenTipos(bool soloActivos) => _repo.ListExamenTipos(soloActivos);

        public Task<ExamenTipoDto> CreateExamenTipo(ExamenTipoUpsertDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre))
                throw new AbrilException("El nombre del tipo de examen es obligatorio.", 400);
            return _repo.CreateExamenTipo(dto);
        }

        public Task<ExamenTipoDto> UpdateExamenTipo(int id, ExamenTipoUpsertDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre))
                throw new AbrilException("El nombre del tipo de examen es obligatorio.", 400);
            return _repo.UpdateExamenTipo(id, dto);
        }

        public Task<List<RestriccionTipoDto>> ListRestriccionTipos(bool soloActivos) => _repo.ListRestriccionTipos(soloActivos);

        public Task<RestriccionTipoDto> CreateRestriccionTipo(RestriccionTipoUpsertDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Descripcion))
                throw new AbrilException("La descripción de la restricción es obligatoria.", 400);
            return _repo.CreateRestriccionTipo(dto);
        }

        public Task<RestriccionTipoDto> UpdateRestriccionTipo(int id, RestriccionTipoUpsertDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Descripcion))
                throw new AbrilException("La descripción de la restricción es obligatoria.", 400);
            return _repo.UpdateRestriccionTipo(id, dto);
        }

        public Task<List<EmpresaCatalogoDto>> ListEmpresas(bool soloActivas) => _repo.ListEmpresas(soloActivas);

        public Task<List<ClinicaEmailDto>> ListClinicaEmails(int clinicaId) =>
            _repo.ListClinicaEmails(clinicaId);

        public Task<ClinicaEmailDto> CreateClinicaEmail(int clinicaId, ClinicaEmailCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
                throw new AbrilException("El email es obligatorio.", 400);
            return _repo.CreateClinicaEmail(clinicaId, dto);
        }

        public Task DeleteClinicaEmail(int clinicaId, int emailId) =>
            _repo.DeleteClinicaEmail(clinicaId, emailId);
    }
}
