using Abril_Backend.Features.SsomaModule.PetarFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.PetarFeature.Application.Interfaces;

public interface IPetarService
{
    Task<int> ResolverWorkerId(int userId);
    Task<PetarInitDto> GetInit(int atsId);

    Task<int> Crear(int workerId, PetarGuardarRequestDto dto);
    Task Editar(int id, int workerId, PetarGuardarRequestDto dto);
    Task<PetarResponseDto> GetPorId(int id, int callerUserId, int workerId, bool esAdmin);
    Task<PetarListResponseDto> Listar(PetarFiltroDto filtro, int callerUserId, int workerId, bool esAdmin);

    Task Firmar(int id, int workerId, PetarFirmarRequestDto body, string? ipOrigen, string? userAgent);
    Task FirmarSupervisor(int id, int callerUserId, bool esAdmin, PetarFirmarVistoRequestDto body);
    Task FirmarVistoSsoma(int id, int callerUserId, bool esAdmin, PetarFirmarVistoRequestDto body);
    Task Cerrar(int id, int workerId, PetarCerrarRequestDto body);

    Task<byte[]> GenerarPdf(int id);
    Task<PetarVerificacionPublicaDto> VerificarPublico(int id, string? hash);
}
