using System.Text.Json;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Application.Dtos;
using Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Application.Interfaces;
using Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.Convivir.Interfaces;
using Abril_Backend.Shared.Services.Convivir.Services;

namespace Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Application.Services
{
    /// <summary>
    /// Documentos que el propietario ve en «Mis documentos» de la app (RF-09 del documento
    /// funcional): los sube la intranet mientras no haya integración con SPERANT.
    /// </summary>
    public class PropietarioDocumentosService : IPropietarioDocumentosService
    {
        private const int NombreMaximo = 150;
        private const int ArchivoNombreMaximo = 255;

        private static readonly JsonSerializerOptions JsonOpciones = new() { PropertyNameCaseInsensitive = true };

        private readonly IPropietarioDocumentosRepository _repo;
        private readonly IPropietarioDocumentoStorage _storage;

        public PropietarioDocumentosService(IPropietarioDocumentosRepository repo, IPropietarioDocumentoStorage storage)
        {
            _repo = repo;
            _storage = storage;
        }

        public Task<PropietarioDocumentosDto> GetDocumentos(int personId) => _repo.GetDocumentos(personId);

        public async Task<PropietarioDocumentosDto> Guardar(int personId, string? data, List<IFormFile>? archivos, int userId)
        {
            var nuevos = LeerNuevos(data);
            archivos ??= new();

            if (nuevos.Count == 0)
                throw new AbrilException("No hay documentos para guardar.", 400);
            if (nuevos.Count != archivos.Count)
                throw new AbrilException("Cada documento tiene que llevar su archivo.", 400);
            if (nuevos.Count > PropietarioDocumentoStorage.MaxPorGuardado)
                throw new AbrilException($"Se pueden guardar hasta {PropietarioDocumentoStorage.MaxPorGuardado} documentos a la vez.", 400);

            // Todo el lote se valida antes de subir nada a SharePoint.
            foreach (var nuevo in nuevos)
            {
                nuevo.Nombre = (nuevo.Nombre ?? string.Empty).Trim();
                if (nuevo.Nombre.Length == 0)
                    throw new AbrilException("Cada documento necesita un nombre.", 400);
                if (nuevo.Nombre.Length > NombreMaximo)
                    throw new AbrilException($"El nombre «{nuevo.Nombre[..30]}…» pasa de {NombreMaximo} caracteres.", 400);
            }
            archivos.ForEach(_storage.Validar);

            var contexto = await _repo.GetContextoSubida(
                personId,
                nuevos.Select(n => n.PropietarioId).Distinct().ToArray(),
                nuevos.Select(n => n.TipoId).Distinct().ToArray());

            if (contexto.Dni == null && contexto.FullName == null)
                throw new AbrilException("Propietario no encontrado.", 404);

            var propiedades = contexto.Propiedades.ToDictionary(p => p.PropietarioId);
            if (nuevos.Any(n => !propiedades.ContainsKey(n.PropietarioId)))
                throw new AbrilException("Una de las propiedades ya no existe. Vuelve a abrir los documentos.", 409);
            if (nuevos.Any(n => !contexto.TiposVigentes.Contains(n.TipoId)))
                throw new AbrilException("Uno de los tipos de documento ya no está disponible. Elige otro.", 409);

            var carpeta = await _storage.ResolverCarpetaAsync(contexto.LinkCarpeta);

            // En SharePoint: {Proyecto}/{Torre - Dpto} - {DNI} {Nombre}/{archivo}. Una carpeta por
            // propiedad, y dos dueños del mismo departamento no se mezclan.
            var titular = string.Join(' ', new[] { contexto.Dni, contexto.FullName }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var filas = new List<PropietarioDocumentoInsertDto>(nuevos.Count);

            for (var i = 0; i < nuevos.Count; i++)
            {
                var nuevo = nuevos[i];
                var archivo = archivos[i];
                var propiedad = propiedades[nuevo.PropietarioId];

                var subido = await _storage.SubirAsync(
                    carpeta,
                    [propiedad.Proyecto, $"{Ubicacion(propiedad)} - {titular}"],
                    nuevo.Nombre,
                    archivo);

                var archivoNombre = Path.GetFileName(archivo.FileName);
                filas.Add(new PropietarioDocumentoInsertDto
                {
                    PropietarioId = nuevo.PropietarioId,
                    TipoId        = nuevo.TipoId,
                    Nombre        = nuevo.Nombre,
                    ArchivoNombre = archivoNombre.Length > ArchivoNombreMaximo ? archivoNombre[^ArchivoNombreMaximo..] : archivoNombre,
                    ContentType   = _storage.ContentType(archivoNombre),
                    TamanoBytes   = archivo.Length,
                    Url           = subido.Url,
                    DriveId       = subido.DriveId,
                    ItemId        = subido.ItemId,
                });
            }

            return await _repo.InsertarYListar(personId, filas, userId);
        }

        public async Task<PropietarioDocumentosDto> Eliminar(int personId, int documentoId, int userId) =>
            await _repo.EliminarYListar(personId, documentoId, userId)
            ?? throw new AbrilException("El documento ya no existe.", 404);

        public async Task<(byte[] Contenido, string ContentType, string NombreArchivo)> Descargar(int personId, int documentoId)
        {
            var archivo = await _repo.GetArchivo(personId, documentoId)
                ?? throw new AbrilException("El documento ya no existe.", 404);

            var contenido = await _storage.DescargarAsync(archivo.ArchivoDriveId, archivo.ArchivoItemId);
            return (contenido.Contenido, archivo.ArchivoContentType, archivo.ArchivoNombre);
        }

        private static List<PropietarioDocumentoNuevoDto> LeerNuevos(string? data)
        {
            if (string.IsNullOrWhiteSpace(data))
                return new();

            try
            {
                return JsonSerializer.Deserialize<List<PropietarioDocumentoNuevoDto>>(data, JsonOpciones) ?? new();
            }
            catch (JsonException)
            {
                throw new AbrilException("Datos de los documentos no válidos.", 400);
            }
        }

        /// <summary>«Torre A - Dpto 803», o «Dpto 803» en un edificio de una sola torre.</summary>
        private static string Ubicacion(PropiedadDocumentosDto propiedad) =>
            string.IsNullOrWhiteSpace(propiedad.Torre)
                ? $"Dpto {propiedad.Departamento}"
                : $"Torre {propiedad.Torre} - Dpto {propiedad.Departamento}";
    }
}
