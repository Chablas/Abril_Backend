using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.PetsFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PetsFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.PetsFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Features.SsomaModule.PetsFeature.Application.Services;

public class PetsService : IPetsService
{
    private const string ContainerName = "ssoma-pets-pasos";

    private readonly IPetsRepository _repo;
    private readonly IFileStorageService _storage;

    public PetsService(IPetsRepository repo, IFileStorageService storage)
    {
        _repo = repo;
        _storage = storage;
    }

    public Task<List<PetListItemDto>> GetListAsync() => _repo.GetListAsync();

    public async Task<PetDetalleDto> GetDetalleAsync(int id)
        => await _repo.GetDetalleAsync(id) ?? throw new AbrilException("PETS no encontrado.", 404);

    public Task<List<PetPasoDto>> GetPasosAsync(int petId) => _repo.GetPasosAsync(petId);

    public Task<int> CrearAsync(CrearPetRequest request) => _repo.CrearAsync(request);

    public Task ActualizarAsync(int id, ActualizarPetRequest request) => _repo.ActualizarAsync(id, request);

    // Clona pasos/responsabilidades (respetando jerarquía), secciones narrativas y
    // catálogo (Marco Legal/EPP/Recursos) de un PETS existente hacia uno nuevo.
    // NO copia firmas (son de la revisión del original, no del borrador nuevo) ni
    // anexos (archivos propios, no texto). El nuevo PETS queda INACTIVO a
    // propósito: es un borrador para revisar antes de que OPT/checklists lo vean.
    public async Task<int> DuplicarAsync(int petId)
    {
        var original = await GetDetalleAsync(petId);
        var nombreCopia = $"{original.Nombre} (copia)";

        var nuevoId = await _repo.CrearAsync(new CrearPetRequest
        {
            Nombre = nombreCopia,
            Codigo = original.Codigo,
            SharepointUrl = original.SharepointUrl,
        });
        await _repo.ActualizarAsync(nuevoId, new ActualizarPetRequest
        {
            Nombre = nombreCopia,
            Codigo = original.Codigo,
            SharepointUrl = original.SharepointUrl,
            Activo = false,
        });

        await DuplicarArbolAsync(nuevoId, "procedimiento", original.Pasos);
        await DuplicarArbolAsync(nuevoId, "responsabilidades", original.Responsabilidades);

        foreach (var (seccion, contenido) in original.SeccionesTexto)
        {
            if (!string.IsNullOrWhiteSpace(contenido))
                await _repo.UpsertSeccionTextoAsync(nuevoId, seccion, contenido);
        }

        await DuplicarSeleccionesAsync(nuevoId, original.MarcoLegal);
        await DuplicarSeleccionesAsync(nuevoId, original.Epp);
        await DuplicarSeleccionesAsync(nuevoId, original.Recursos);

        return nuevoId;
    }

    // "Orden" es por grupo de hermanos (ParentId), no un contador global — el
    // listado plano NO garantiza que un subtítulo aparezca antes que sus hijos.
    // Se arma el árbol por ParentId primero y se recorre de raíz hacia hojas, para
    // crear siempre al padre antes que su hijo (el hijo necesita el Id nuevo del padre).
    private async Task DuplicarArbolAsync(int nuevoPetId, string seccion, List<PetPasoDto> pasos)
    {
        if (pasos.Count == 0) return;

        var hijosPorPadre = pasos
            .GroupBy(p => p.ParentId)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Orden).ToList());

        async Task DuplicarNivelAsync(int? parentIdOriginal, int? parentIdNuevo)
        {
            if (!hijosPorPadre.TryGetValue(parentIdOriginal, out var nivel)) return;

            foreach (var paso in nivel)
            {
                var nuevoPasoId = await _repo.AgregarPasoAsync(nuevoPetId, new CrearPetPasoRequest
                {
                    Descripcion = paso.Descripcion,
                    Seccion = seccion,
                    ParentId = parentIdNuevo,
                    Tipo = paso.Tipo,
                });

                // Se reusan las MISMAS URLs (todas las imágenes del paso, no solo la
                // primera) en vez de descargar/resubir el archivo — cambiar una imagen en
                // cualquiera de los dos PETS sube un blob nuevo y solo repunta su propio
                // paso, nunca borra el original.
                foreach (var url in paso.Imagenes.Select(i => i.Url))
                    await _repo.AgregarImagenPasoAsync(nuevoPetId, nuevoPasoId, url);

                await DuplicarNivelAsync(paso.Id, nuevoPasoId);
            }
        }

        await DuplicarNivelAsync(null, null);
    }

    private async Task DuplicarSeleccionesAsync(int nuevoPetId, List<PetItemSeleccionadoDto> items)
    {
        foreach (var item in items)
        {
            if (item.CatalogoItemId.HasValue)
            {
                await _repo.SeleccionarCatalogoItemAsync(nuevoPetId, new SeleccionarItemCatalogoRequest
                {
                    Grupo = item.Grupo,
                    Tipo = item.Tipo,
                    CatalogoItemId = item.CatalogoItemId.Value,
                });
            }
            else
            {
                await _repo.AgregarItemPersonalizadoAsync(nuevoPetId, new AgregarItemPersonalizadoRequest
                {
                    Grupo = item.Grupo,
                    Tipo = item.Tipo,
                    Descripcion = item.Descripcion,
                    AgregarAlCatalogoGlobal = false,
                });
            }
        }
    }

    public Task<int> AgregarPasoAsync(int petId, CrearPetPasoRequest request) => _repo.AgregarPasoAsync(petId, request);

    public Task<Dictionary<int, int>> AgregarPasosBulkAsync(int petId, string seccion, List<ImportPasoConfirmDto> pasos)
        => _repo.AgregarPasosBulkAsync(petId, seccion, pasos);

    public Task ActualizarPasoAsync(int petId, int pasoId, ActualizarPetPasoRequest request)
        => _repo.ActualizarPasoAsync(petId, pasoId, request);

    public Task EliminarPasoAsync(int petId, int pasoId) => _repo.EliminarPasoAsync(petId, pasoId);

    public Task ReordenarPasosAsync(int petId, ReordenarPasosRequest request) => _repo.ReordenarPasosAsync(petId, request);

    public Task DesactivarSeccionAsync(int petId, string seccion) => _repo.DesactivarSeccionAsync(petId, seccion);

    public Task UpsertSeccionTextoAsync(int petId, string seccion, string contenido)
        => _repo.UpsertSeccionTextoAsync(petId, seccion, contenido);

    // Agrega una imagen MÁS al paso (no reemplaza las que ya tenía) — un paso puede
    // traer varias fotos del Word original o el usuario puede ir sumando evidencia.
    public async Task<(int Id, string Url)> SubirImagenPasoAsync(int petId, int pasoId, Stream fileStream, string fileName)
    {
        var urls = await _storage.UploadFilesAsync([(fileStream, fileName)], ContainerName);
        var url = urls.FirstOrDefault()
            ?? throw new AbrilException("No se pudo subir la imagen.", 500);

        var id = await _repo.AgregarImagenPasoAsync(petId, pasoId, url);
        return (id, url);
    }

    public Task EliminarImagenPasoAsync(int petId, int pasoId, int imagenId) => _repo.EliminarImagenPasoAsync(petId, pasoId, imagenId);

    public Task ActualizarCategoriaPasoAsync(int petId, int pasoId, string? categoria) => _repo.ActualizarCategoriaPasoAsync(petId, pasoId, categoria);

    public Task<List<CatalogoItemDto>> GetCatalogoAsync(string grupo, string? tipo) => _repo.GetCatalogoAsync(grupo, tipo);

    public Task<int> CrearCatalogoItemAsync(CrearCatalogoItemRequest request) => _repo.CrearCatalogoItemAsync(request);

    public Task DesactivarCatalogoItemAsync(int catalogoItemId) => _repo.DesactivarCatalogoItemAsync(catalogoItemId);

    public Task<int> SeleccionarCatalogoItemAsync(int petId, SeleccionarItemCatalogoRequest request)
        => _repo.SeleccionarCatalogoItemAsync(petId, request);

    public Task<int> AgregarItemPersonalizadoAsync(int petId, AgregarItemPersonalizadoRequest request)
        => _repo.AgregarItemPersonalizadoAsync(petId, request);

    public Task EliminarSeleccionAsync(int petId, int seleccionId) => _repo.EliminarSeleccionAsync(petId, seleccionId);

    public Task DesactivarSeleccionesGrupoAsync(int petId, string grupo) => _repo.DesactivarSeleccionesGrupoAsync(petId, grupo);

    private const string ContainerNameAnexos = "ssoma-pets-anexos";

    public async Task<string> SubirAnexoAsync(int petId, string nombre, Stream fileStream, string fileName)
    {
        var urls = await _storage.UploadFilesAsync([(fileStream, fileName)], ContainerNameAnexos);
        var url = urls.FirstOrDefault()
            ?? throw new AbrilException("No se pudo subir el anexo.", 500);

        await _repo.AgregarAnexoAsync(petId, nombre, url);
        return url;
    }

    public Task EliminarAnexoAsync(int petId, int anexoId) => _repo.EliminarAnexoAsync(petId, anexoId);

    public Task UpsertFirmaAsync(int petId, string rol, string? nombre, string? cargo, DateOnly? fecha)
        => _repo.UpsertFirmaAsync(petId, rol, nombre, cargo, fecha);

    private const string ContainerNameFirmas = "ssoma-pets-firmas";

    public async Task<string> SubirFirmaAsync(int petId, string rol, Stream fileStream, string fileName)
    {
        var urls = await _storage.UploadFilesAsync([(fileStream, fileName)], ContainerNameFirmas);
        var url = urls.FirstOrDefault()
            ?? throw new AbrilException("No se pudo subir la firma.", 500);

        await _repo.SetFirmaUrlAsync(petId, rol, url);
        return url;
    }

    // "Vista previa" — refleja el estado ACTUAL en edición, sea borrador o no. Nunca
    // se entrega por QR (para eso está ExportarPdfVersionAsync, la versión aprobada).
    public async Task<byte[]> ExportarPdfAsync(int petId)
    {
        var pet = await GetDetalleAsync(petId);
        return await PetsPdfService.GenerarPdfAsync(pet, version: null);
    }

    public Task<PetVersionDto> AprobarVersionAsync(int petId, string motivo, int? aprobadoPorId, string aprobadoPorNombre)
        => _repo.AprobarVersionAsync(petId, motivo, aprobadoPorId, aprobadoPorNombre);

    public Task<List<PetVersionDto>> GetVersionesAsync(int petId) => _repo.GetVersionesAsync(petId);

    // PDF "oficial" — el que se entrega por QR/impreso. Sale del SNAPSHOT de esa
    // versión, no del estado en vivo, para que no cambie bajo los pies de alguien en
    // campo mientras otra persona sigue editando el borrador siguiente.
    public async Task<byte[]> ExportarPdfVersionAsync(int petId, int numeroVersion)
    {
        var pet = await _repo.GetVersionSnapshotAsync(petId, numeroVersion)
            ?? throw new AbrilException("Versión no encontrada.", 404);
        var versiones = await _repo.GetVersionesAsync(petId);
        var version = versiones.FirstOrDefault(v => v.NumeroVersion == numeroVersion)
            ?? throw new AbrilException("Versión no encontrada.", 404);
        return await PetsPdfService.GenerarPdfAsync(pet, version);
    }
}
