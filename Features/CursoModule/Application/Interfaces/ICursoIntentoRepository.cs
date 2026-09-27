using Abril_Backend.Features.CursoModule.Infrastructure.Models;

namespace Abril_Backend.Features.CursoModule.Application.Interfaces
{
    public interface ICursoIntentoRepository
    {
        Task<CursoIntento> CrearIntentoAsync(CursoIntento intento, CursoIntentoEvidencia evidencia);
        Task<CursoIntento?> GetByIdAsync(int intentoId);
        Task<List<CursoIntento>> GetPorUsuarioAsync(int userId);

        /// <summary>Historial de intentos para auditoría (SUNAFIL) — filtros opcionales por
        /// curso y rango de fecha de inicio, orden más reciente primero.</summary>
        Task<List<CursoIntento>> GetHistorialAsync(int? cursoId, DateTime? desde, DateTime? hasta);

        /// <summary>Nombre completo (Person.FullName) de cada userId, para no golpear el
        /// repositorio de personas fila por fila al armar el historial.</summary>
        Task<Dictionary<int, string>> GetNombresTrabajadoresAsync(int[] userIds);
        Task<CursoIntentoRespuesta> GuardarRespuestaAsync(CursoIntentoRespuesta respuesta);
        Task<List<CursoIntentoRespuesta>> GetRespuestasAsync(int intentoId);
        Task<CursoIntentoEvidencia?> GetEvidenciaAsync(int intentoId);
        Task FinalizarAsync(CursoIntento intento, CursoIntentoEvidencia evidencia);
    }
}
