using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.ConvivirModule.Features.DocumentosFeature.Application.Dtos;
using Abril_Backend.Features.ConvivirModule.Features.DocumentosFeature.Application.Interfaces;
using Abril_Backend.Features.ConvivirModule.Features.DocumentosFeature.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.Convivir.Interfaces;

namespace Abril_Backend.Features.ConvivirModule.Features.DocumentosFeature.Application.Services
{
    /// <summary>
    /// «Mis documentos» (RF-09): la minuta, los contratos, los comprobantes y los manuales de la
    /// propiedad elegida. Los sube la intranet (módulo Propietarios).
    /// </summary>
    public class ConvivirDocumentosService : IConvivirDocumentosService
    {
        private readonly IConvivirDocumentosRepository _repo;
        private readonly IPropietarioDocumentoStorage _storage;

        public ConvivirDocumentosService(IConvivirDocumentosRepository repo, IPropietarioDocumentoStorage storage)
        {
            _repo = repo;
            _storage = storage;
        }

        public async Task<ConvivirDocumentosDto> GetDocumentos(int userId, int? propietarioId)
        {
            var (propiedad, filas) = await _repo.GetDocumentos(userId, propietarioId);

            return new ConvivirDocumentosDto
            {
                Propiedad = propiedad,
                Tipos = filas
                    .GroupBy(f => f.Tipo)
                    .OrderBy(g => g.First().TipoOrden)
                    .ThenBy(g => g.Key)
                    .Select(g => g.Key)
                    .ToList(),
                Documentos = filas.Select(f => new ConvivirDocumentoDto
                {
                    DocumentoId = f.DocumentoId,
                    Tipo        = f.Tipo,
                    Nombre      = f.Nombre,
                    Formato     = Path.GetExtension(f.ArchivoNombre).TrimStart('.').ToUpperInvariant(),
                    ContentType = f.ContentType,
                    TamanoBytes = f.TamanoBytes,
                    Fecha       = DateOnly.FromDateTime(f.SubidoEl),
                    Nuevo       = f.Nuevo,
                }).ToList(),
            };
        }

        public async Task<(byte[] Contenido, string ContentType, string NombreArchivo)> Descargar(int userId, int documentoId)
        {
            var archivo = await _repo.GetArchivo(userId, documentoId)
                ?? throw new AbrilException("Este documento ya no está disponible.", 404);

            var contenido = await _storage.DescargarAsync(archivo.ArchivoDriveId, archivo.ArchivoItemId);

            // Recién con el archivo en la mano: si SharePoint falló, sigue «Nuevo».
            await _repo.MarcarLeido(documentoId);

            return (contenido.Contenido, archivo.ArchivoContentType, archivo.ArchivoNombre);
        }
    }
}
