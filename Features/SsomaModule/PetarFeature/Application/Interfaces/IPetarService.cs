using Abril_Backend.Features.SsomaModule.PetarFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.PetarFeature.Application.Interfaces;

public interface IPetarService
{
    Task<int> ResolverWorkerId(int userId);
    Task<PetarInitDto> GetInit(int atsId);
    /// <summary>Catálogo de tipos+checklist sin requerir un ATS individual — para el PETAR
    /// grupal, que nace de un ATS grupal, no de un ATS de una sola persona.</summary>
    Task<List<PetarTipoDto>> GetTiposCatalogo();

    Task<int> Crear(int workerId, PetarGuardarRequestDto dto);
    Task Editar(int id, int workerId, PetarGuardarRequestDto dto);
    Task<PetarResponseDto> GetPorId(int id, int workerId, bool esAdmin);
    Task<PetarListResponseDto> Listar(PetarFiltroDto filtro, int workerId, bool esAdmin);

    Task Firmar(int id, int workerId, PetarFirmarRequestDto body, string? ipOrigen, string? userAgent);
    Task FirmarSupervisor(int id, int callerUserId, bool esAdmin, PetarFirmarVistoRequestDto body);
    Task FirmarVistoSsoma(int id, int callerUserId, bool esAdmin, PetarFirmarVistoRequestDto body);
    Task Cerrar(int id, int workerId, PetarCerrarRequestDto body);

    Task<byte[]> GenerarPdf(int id);
    Task<PetarVerificacionPublicaDto> VerificarPublico(int id, string? hash);

    // ── PETAR Grupal ──────────────────────────────────────────────────────
    Task<PetarGrupoCrearResponseDto> CrearGrupo(int workerId, PetarGrupoCrearRequestDto dto);
    Task<List<PetarGrupoResumenPublicoDto>> GetGruposPublicoPorAtsToken(Guid atsToken);
    Task<List<PetarGrupoEstadoDto>> GetEstadosPorAtsGrupo(int atsGrupoId, int workerId, bool esAdmin);
    Task<PetarGrupoEstadoDto> GetEstadoGrupo(int id, int workerId, bool esAdmin);
    Task CerrarGrupo(int id, int workerId, bool esAdmin);
    Task FirmarSupervisorGrupo(int id, int callerUserId, bool esAdmin, PetarFirmarVistoRequestDto body);
    Task FirmarSsomaGrupo(int id, int callerUserId, bool esAdmin, PetarFirmarVistoRequestDto body);
    Task<int> UnirseAGrupo(int petarGrupoId, PetarGrupoUnirseRequestDto body, string? ipOrigen, string? userAgent);
}
