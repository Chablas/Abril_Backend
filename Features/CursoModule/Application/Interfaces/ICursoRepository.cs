using Abril_Backend.Features.CursoModule.Infrastructure.Models;

namespace Abril_Backend.Features.CursoModule.Application.Interfaces
{
    public interface ICursoRepository
    {
        /// <summary>Cursos activos visibles para los roles indicados (mismo patrón de LearningModule: sin rol destino = visible para todos).</summary>
        Task<List<Curso>> GetActivosPorRolAsync(int[] roleIds);

        Task<Curso?> GetByIdAsync(int id);

        /// <summary>Slides del curso en orden, con su ConfiguracionJson completo (incluye "respuestaCorrecta"). La limpieza antes de exponer al frontend es responsabilidad del controller/servicio consumidor.</summary>
        Task<List<CursoSlide>> GetSlidesOrdenadasAsync(int cursoId);

        Task<CursoSlide?> GetSlideByIdAsync(int slideId);
    }
}
