using Abril_Backend.Features.Ssoma.Penalidad.Dtos;
using Microsoft.AspNetCore.Http;

namespace Abril_Backend.Features.Ssoma.Penalidad.Services;

/// <summary>
/// Bitácora de gestión previa a una penalidad (correos de advertencia, cartas de preocupación,
/// reuniones, llamadas) que SSOMA registra por empresa antes de llegar a una sanción formal.
/// </summary>
public interface IGestionPreviaService
{
    Task<GestionPreviaDto> RegistrarAsync(GestionPreviaRegistrarRequest req, int userId);
    Task<List<GestionPreviaDto>> GetListAsync(int empresaId);
    Task<string> SubirAdjuntoAsync(int empresaId, IFormFile file);

    /// <summary>Contexto para la tipificación: historial de gestión previa + reincidencia de
    /// penalidades ya aplicadas a esta empresa, para que Residente/Gerencia decidan informados.</summary>
    Task<ContextoEmpresaDto> GetContextoEmpresaAsync(int empresaId);
}
