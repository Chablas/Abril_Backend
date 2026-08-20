using Abril_Backend.Features.Evaluaciones.Application.Dtos;

namespace Abril_Backend.Features.Evaluaciones.Application.Interfaces
{
    public interface IEvJefeSsomaRepository
    {
        Task<EvJefeSsomaInicioDto> GetInicioAsync(int evaluadorUserId);
        Task<bool> YaEvaluoAsync(int periodoId, int evaluadorUserId);

        /// <summary>
        /// Inserta la evaluación (sin autor) y la marca de cumplimiento (sin nota) en la
        /// misma transacción. Deliberadamente no hay forma de unir ambas filas después.
        /// </summary>
        Task RegistrarAsync(
            int periodoId, int evaluadorUserId, string? comentario,
            List<(int? plantillaId, string criterio, int puntaje)> detalles, decimal nota);

        Task<EvJefeSsomaCumplimientoDto> GetCumplimientoAsync(int periodoId);
        Task<EvJefeSsomaResultadosDto> GetResultadosAsync(int? periodoId);
    }
}
