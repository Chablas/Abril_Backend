using Abril_Backend.Features.Habilitacion.Application.Dtos.Dossier;
using Microsoft.AspNetCore.Http;

namespace Abril_Backend.Features.Habilitacion.Application.Interfaces;

public interface IDossierService
{
    Task<List<DossierSemanaDto>> GetSemanasAsync(int? contributorId, int? proyectoId, int? anio);
    Task<DossierSemanaDetalleDto> GetDetalleAsync(int id);
    Task<object> EnsureSemanaAsync(EnsureSemanaRequest req);
    Task SubirDocumentoAsync(int dossierId, string tipoDoc, IFormFile file);
    Task MarcarNaAsync(int docId);
    Task EnviarAsync(int dossierId);
    Task RevisarAsync(int dossierId, RevisarDossierRequest req);
    Task<string> GetDocumentoUrlAsync(int docId);
}
