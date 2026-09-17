using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Services
{
    /// <inheritdoc cref="ISolicitudPersonalVisibilidadService"/>
    public class SolicitudPersonalVisibilidadService : ISolicitudPersonalVisibilidadService
    {
        private readonly ISolicitudPersonalVisibilidadRepository _repo;
        private readonly ISolicitudPersonalScopeResolver _scopes;

        public SolicitudPersonalVisibilidadService(
            ISolicitudPersonalVisibilidadRepository repo,
            ISolicitudPersonalScopeResolver scopes)
        {
            _repo   = repo;
            _scopes = scopes;
        }

        public Task<SolicitudPersonalVisibilidadInicialDto> GetInitialData() => _repo.GetInitialData();

        public async Task<SolicitudPersonalVisibilidadDetalleDto> GetWorkerDetalle(int workerId)
        {
            // Lo que ve hoy sale del MISMO resolver que recorta la pantalla, así que el modal no
            // puede mostrar un alcance que después no se cumpla.
            var ficha = await _scopes.ResolveByWorkerAsync(workerId)
                        ?? throw new AbrilException("El trabajador no existe o ya no está activo.", 404);

            return new SolicitudPersonalVisibilidadDetalleDto
            {
                Asignaciones = ficha.Asignadas
                    .Select(id => new SolicitudPersonalVisibilidadAsignacionDto { AreaScopeId = id })
                    .ToList(),
                Efectivas       = ficha.Efectivas.ToList(),
                EsPersonalizado = ficha.Asignadas.Count > 0,
                VeTodo          = ficha.VeTodo,
            };
        }

        public Task UpdateWorkerAsignaciones(
            int workerId, List<SolicitudPersonalVisibilidadAsignacionDto>? areas, int? userId) =>
            _repo.UpdateWorkerAsignaciones(
                workerId,
                (areas ?? new List<SolicitudPersonalVisibilidadAsignacionDto>())
                    .Select(a => a.AreaScopeId)
                    .Distinct()
                    .ToList(),
                userId);
    }
}
