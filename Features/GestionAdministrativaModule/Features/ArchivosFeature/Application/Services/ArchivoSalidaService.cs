using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.Archivos.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Archivos.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Archivos.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;
using Microsoft.AspNetCore.StaticFiles;

namespace Abril_Backend.Features.GestionAdministrativa.Archivos.Application.Services
{
    public class ArchivoSalidaService : IArchivoSalidaService
    {
        private static readonly FileExtensionContentTypeProvider Tipos = new();

        private readonly IArchivoSalidaRepository      _repo;
        private readonly IGraphSharePointService       _sharePoint;
        private readonly ILogger<ArchivoSalidaService> _logger;

        public ArchivoSalidaService(
            IArchivoSalidaRepository repo,
            IGraphSharePointService sharePoint,
            ILogger<ArchivoSalidaService> logger)
        {
            _repo       = repo;
            _sharePoint = sharePoint;
            _logger     = logger;
        }

        public async Task<ArchivoSalidaDto> Get(string url, int userId, int[] roleIds)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                throw new AbrilException("No se encontró el archivo.", 404);

            // El mismo 404 para lo que no es del módulo y para lo que no le toca: no se confirma
            // que exista un archivo que el usuario no puede ver.
            if (await _repo.GetAcceso(url, userId, roleIds) != AccesoArchivoSalida.Permitido)
                throw new AbrilException("No se encontró el archivo o no tienes acceso a él.", 404);

            var nombre = Uri.UnescapeDataString(Path.GetFileName(uri.AbsolutePath));
            try
            {
                return new ArchivoSalidaDto
                {
                    Contenido   = await _sharePoint.DownloadOneDriveFileByWebUrlAsync(url),
                    ContentType = Tipos.TryGetContentType(nombre, out var tipo)
                        ? tipo
                        : "application/octet-stream",
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo descargar {Archivo} de SharePoint.", nombre);
                throw new AbrilException($"No se pudo descargar {nombre} de SharePoint.", 502);
            }
        }
    }
}
