using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Catalogos;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Interfaces
{
    public interface ICatalogosRepository
    {
        // Clinicas
        Task<List<ClinicaDto>> ListClinicas(bool soloActivos);
        Task<ClinicaDto> CreateClinica(ClinicaUpsertDto dto);
        Task<ClinicaDto> UpdateClinica(int id, ClinicaUpsertDto dto);

        // Medicos
        Task<List<MedicoOcupacionalDto>> ListMedicos(bool soloActivos);
        Task<MedicoOcupacionalDto> CreateMedico(MedicoOcupacionalUpsertDto dto);
        Task<MedicoOcupacionalDto> UpdateMedico(int id, MedicoOcupacionalUpsertDto dto);

        // EMO Tipos
        Task<List<EmoTipoDto>> ListEmoTipos(bool soloActivos);
        Task<EmoTipoDto> CreateEmoTipo(EmoTipoUpsertDto dto);
        Task<EmoTipoDto> UpdateEmoTipo(int id, EmoTipoUpsertDto dto);

        // Examen Tipos
        Task<List<ExamenTipoDto>> ListExamenTipos(bool soloActivos);
        Task<ExamenTipoDto> CreateExamenTipo(ExamenTipoUpsertDto dto);
        Task<ExamenTipoDto> UpdateExamenTipo(int id, ExamenTipoUpsertDto dto);

        // Restriccion Tipos
        Task<List<RestriccionTipoDto>> ListRestriccionTipos(bool soloActivos);
        Task<RestriccionTipoDto> CreateRestriccionTipo(RestriccionTipoUpsertDto dto);
        Task<RestriccionTipoDto> UpdateRestriccionTipo(int id, RestriccionTipoUpsertDto dto);

        // Empresas
        Task<List<EmpresaCatalogoDto>> ListEmpresas(bool soloActivas);

        // Clinica Emails
        Task<List<ClinicaEmailDto>> ListClinicaEmails(int clinicaId);
        Task<ClinicaEmailDto> CreateClinicaEmail(int clinicaId, ClinicaEmailCreateDto dto);
        Task DeleteClinicaEmail(int clinicaId, int emailId);
    }
}
