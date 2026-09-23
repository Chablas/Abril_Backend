using Abril_Backend.Features.CursoModule.Infrastructure.Models;

namespace Abril_Backend.Features.CursoModule.Application.Interfaces
{
    public interface ICursoIntentoRepository
    {
        Task<CursoIntento> CrearIntentoAsync(CursoIntento intento, CursoIntentoEvidencia evidencia);
        Task<CursoIntento?> GetByIdAsync(int intentoId);
        Task<CursoIntentoRespuesta> GuardarRespuestaAsync(CursoIntentoRespuesta respuesta);
        Task<List<CursoIntentoRespuesta>> GetRespuestasAsync(int intentoId);
        Task<CursoIntentoEvidencia?> GetEvidenciaAsync(int intentoId);
        Task FinalizarAsync(CursoIntento intento, CursoIntentoEvidencia evidencia);
    }
}
