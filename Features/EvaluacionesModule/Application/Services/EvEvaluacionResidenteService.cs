using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Evaluaciones.Application.Dtos;
using Abril_Backend.Features.Evaluaciones.Application.Interfaces;
using Abril_Backend.Features.Evaluaciones.Infrastructure.Models;

namespace Abril_Backend.Features.Evaluaciones.Application.Services
{
    public class EvEvaluacionResidenteService : IEvEvaluacionResidenteService
    {
        private readonly IEvEvaluacionResidenteRepository _repo;
        private readonly IEvPeriodoRepository _periodoRepo;

        public EvEvaluacionResidenteService(
            IEvEvaluacionResidenteRepository repo,
            IEvPeriodoRepository periodoRepo)
        {
            _repo = repo;
            _periodoRepo = periodoRepo;
        }

        public async Task<EvEvaluacionResidenteResponseDto> CreateAsync(EvEvaluacionCreateDto dto, int evaluadorUserId)
        {
            var periodo = await _periodoRepo.GetActivoAsync()
                ?? throw new AbrilException("No hay período activo.", 400);

            if (DateTime.Today < periodo.FechaApertura.ToDateTime(TimeOnly.MinValue) ||
                DateTime.Today > periodo.FechaCierre.ToDateTime(TimeOnly.MinValue))
                throw new AbrilException("El período de evaluación no está activo.", 400);

            var existe = await _repo.ExisteAsync(periodo.Id, evaluadorUserId, dto.EvaluadoUserId, dto.AreaNombre);
            if (existe)
                throw new AbrilException("Ya evaluaste a este residente en esta área este período.", 409);

            var eval = new EvEvaluacionResidente
            {
                PeriodoId = periodo.Id,
                EvaluadorUserId = evaluadorUserId,
                EvaluadoUserId = dto.EvaluadoUserId,
                ProjectId = dto.ProjectId,
                AreaNombre = dto.AreaNombre,
                Comentario = dto.Comentario,
                NoAplica = dto.NoAplica,
                NoAplicaMotivo = dto.NoAplicaMotivo
            };
            var detalles = dto.Detalles.Select(d => new EvEvaluacionResidenteDetalle
            {
                PlantillaId = d.PlantillaId,
                Criterio = d.Criterio,
                Puntaje = d.Puntaje,
                EsNa = d.EsNa
            }).ToList();

            await _repo.CreateAsync(eval, detalles);

            return await _repo.GetDetalleAsync(eval.Id)
                ?? throw new AbrilException("Error al recuperar la evaluación creada.", 500);
        }

        public async Task<List<EvEvaluacionResidenteResponseDto>> GetByEvaluadorAsync(int evaluadorUserId, int periodoId)
            => await _repo.GetByEvaluadorAsync(evaluadorUserId, periodoId);

        public async Task<List<EvEvaluacionResidenteResponseDto>> GetByEvaluadoAsync(int evaluadoUserId, int periodoId)
            => await _repo.GetByEvaluadoAsync(evaluadoUserId, periodoId);
    }
}
