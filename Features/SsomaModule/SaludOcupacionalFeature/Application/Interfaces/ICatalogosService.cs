using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Catalogos;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces
{
    public interface ICatalogosService
    {
        Task<List<ClinicaDto>> ListClinicas(bool soloActivos);
        Task<ClinicaDto> CreateClinica(ClinicaUpsertDto dto);
        Task<ClinicaDto> UpdateClinica(int id, ClinicaUpsertDto dto);

        Task<List<MedicoOcupacionalDto>> ListMedicos(bool soloActivos);
        Task<MedicoOcupacionalDto> CreateMedico(MedicoOcupacionalUpsertDto dto);
        Task<MedicoOcupacionalDto> UpdateMedico(int id, MedicoOcupacionalUpsertDto dto);

        Task<List<EmoTipoDto>> ListEmoTipos(bool soloActivos);
        Task<EmoTipoDto> CreateEmoTipo(EmoTipoUpsertDto dto);
        Task<EmoTipoDto> UpdateEmoTipo(int id, EmoTipoUpsertDto dto);

        Task<List<ExamenTipoDto>> ListExamenTipos(bool soloActivos);
        Task<ExamenTipoDto> CreateExamenTipo(ExamenTipoUpsertDto dto);
        Task<ExamenTipoDto> UpdateExamenTipo(int id, ExamenTipoUpsertDto dto);

        Task<List<RestriccionTipoDto>> ListRestriccionTipos(bool soloActivos);
        Task<RestriccionTipoDto> CreateRestriccionTipo(RestriccionTipoUpsertDto dto);
        Task<RestriccionTipoDto> UpdateRestriccionTipo(int id, RestriccionTipoUpsertDto dto);

        Task<List<EmpresaCatalogoDto>> ListEmpresas(bool soloActivas);
    }
}
