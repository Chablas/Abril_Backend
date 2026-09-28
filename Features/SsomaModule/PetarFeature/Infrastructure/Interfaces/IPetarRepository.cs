using Abril_Backend.Features.SsomaModule.PetarFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Interfaces;

public interface IPetarRepository
{
    Task<List<PetarTipoDto>> GetTiposConItems();

    Task<(int ProyectoId, string? Lugar, string Actividad, string Estado)> GetDatosAts(int atsId);

    Task<int> Crear(int workerId, PetarGuardarRequestDto dto);
    Task Editar(int id, int workerId, PetarGuardarRequestDto dto);
    Task<SsPetar?> GetEntidad(int id);
    Task<PetarResponseDto?> GetPorId(int id);
    Task<PetarListResponseDto> Listar(PetarFiltroDto filtro);

    Task Firmar(SsPetar petar, string selfieUrl, string selfieHash, string firmaUrl, string firmaHash, DateTime horaServidor, PetarFirmarRequestDto body, string? ipOrigen, string? userAgent);
    Task FirmarVisto(int petarId, string rol, int workerId, string nombre, string? cargo, string firmaUrl, string firmaHash, DateTime horaServidor);
    Task Cerrar(int petarId, int workerId, string firmaUrl, string firmaHash, string? observaciones, DateTime horaServidor);

    Task GuardarPdf(int id, string pdfUrl, string pdfHash);
    Task<string?> GetUltimoHashAuditLog(int petarId);
    Task AgregarAuditLog(int petarId, string evento, int? userId, string? ipOrigen, string? detalle, string hashAnterior, string hash);
    Task<bool> ExisteSelfieHash(string selfieHash);
}
