using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Application.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Infrastructure.Interfaces;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Application.Services
{
    public class PlantillaCronogramaService : IPlantillaCronogramaService
    {
        private static readonly HashSet<string> TiposValidos = new()
        {
            "ANTEPROYECTO", "PROYECTO", "PROYECTO_ACTUALIZACION"
        };

        private readonly IPlantillaCronogramaRepository _repository;

        public PlantillaCronogramaService(IPlantillaCronogramaRepository repository)
        {
            _repository = repository;
        }

        public Task<List<PlantillaItemDto>> GetByTipoAsync(string tipoCronograma)
        {
            ValidarTipo(tipoCronograma);
            return _repository.GetByTipoAsync(tipoCronograma);
        }

        public Task<PlantillaItemDto> CrearItemAsync(CrearPlantillaItemRequest request, int userId)
        {
            ValidarTipo(request.TipoCronograma);
            ValidarCampos(request.Codigo, request.Nombre);
            return _repository.CrearItemAsync(request, userId);
        }

        public Task<PlantillaItemDto> EditarItemAsync(int id, EditarPlantillaItemRequest request, int userId)
        {
            ValidarCampos(request.Codigo, request.Nombre);
            return _repository.EditarItemAsync(id, request, userId);
        }

        public Task EliminarItemAsync(int id, int userId) => _repository.EliminarItemAsync(id, userId);

        private static void ValidarTipo(string tipoCronograma)
        {
            if (string.IsNullOrWhiteSpace(tipoCronograma) || !TiposValidos.Contains(tipoCronograma))
                throw new AbrilException("El tipo de cronograma debe ser ANTEPROYECTO, PROYECTO o PROYECTO_ACTUALIZACION.", 400);
        }

        private static void ValidarCampos(string codigo, string nombre)
        {
            if (string.IsNullOrWhiteSpace(codigo))
                throw new AbrilException("El código del ítem es obligatorio.", 400);
            if (string.IsNullOrWhiteSpace(nombre))
                throw new AbrilException("El nombre del ítem es obligatorio.", 400);
        }
    }
}
