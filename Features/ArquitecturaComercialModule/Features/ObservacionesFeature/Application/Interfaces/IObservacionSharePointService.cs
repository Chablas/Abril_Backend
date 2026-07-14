namespace Abril_Backend.Features.ArquitecturaComercialModule.Features.ObservacionesFeature.Application.Interfaces;

public interface IObservacionSharePointService
{
    Task<string> SubirFotoObservacionAsync(Stream stream, string fileName, string contentType, int proyectoId, int observacionId);
    Task<string> SubirFotoLevantamientoAsync(Stream stream, string fileName, string contentType, int proyectoId, int observacionId, int orden);
}
