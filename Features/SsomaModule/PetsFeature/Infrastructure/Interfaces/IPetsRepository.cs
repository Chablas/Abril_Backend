using Abril_Backend.Features.SsomaModule.PetsFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.PetsFeature.Infrastructure.Interfaces;

public interface IPetsRepository
{
    Task<List<PetListItemDto>> GetListAsync();
    Task<PetDetalleDto?> GetDetalleAsync(int id);
    Task<List<PetPasoDto>> GetPasosAsync(int petId);
    Task<string> ObtenerSiguienteCodigoAbrilAsync();
    Task<int> CrearAsync(CrearPetRequest request);
    Task ActualizarAsync(int id, ActualizarPetRequest request);

    // Cuántas veces está referenciado este PETS fuera de su propio módulo (OPT,
    // Accidentes/Incidentes) — un borrado real solo procede si esto da 0.
    Task<int> ContarReferenciasExternasAsync(int petId);

    // Borrado real: elimina el PETS y TODO su contenido propio (pasos, imágenes,
    // secciones de texto, selecciones de catálogo, anexos, firmas, versiones
    // aprobadas). El llamador ya validó que no tiene referencias externas.
    Task EliminarAsync(int id);
    Task<int> AgregarPasoAsync(int petId, CrearPetPasoRequest request);

    // Inserta muchos pasos de un tirón (import de Word): agrupa por profundidad del árbol
    // y hace un solo SaveChanges por nivel en vez de uno por paso — evita las decenas/
    // cientos de viajes redondos a la base de datos que hacían que importar un PETS grande
    // tomara varios minutos. Devuelve el id real asignado a cada "Indice" del preview.
    Task<Dictionary<int, int>> AgregarPasosBulkAsync(int petId, string seccion, List<ImportPasoConfirmDto> pasos);
    Task ActualizarPasoAsync(int petId, int pasoId, ActualizarPetPasoRequest request);
    Task EliminarPasoAsync(int petId, int pasoId);
    Task ReordenarPasosAsync(int petId, ReordenarPasosRequest request);
    Task CambiarNivelPasoAsync(int petId, int pasoId, int? nuevoParentId);
    // Reemplazado por AgregarImagenPasoAsync (una imagen NUEVA, no la única) — se
    // mantiene solo para no romper filas ya guardadas con el campo viejo.
    Task SetImagenPasoAsync(int petId, int pasoId, string? imagenUrl);

    // Agrega una imagen MÁS al paso (varias por paso — ver SsomaPetPasoImagen).
    // Devuelve el Id de la imagen nueva.
    Task<int> AgregarImagenPasoAsync(int petId, int pasoId, string url);
    Task EliminarImagenPasoAsync(int petId, int pasoId, int imagenId);
    Task ActualizarCategoriaPasoAsync(int petId, int pasoId, string? categoria);
    Task DesactivarSeccionAsync(int petId, string seccion);
    Task UpsertSeccionTextoAsync(int petId, string seccion, string contenido);

    // Catálogo (Marco Legal / EPP / Recursos)
    Task<List<CatalogoItemDto>> GetCatalogoAsync(string grupo, string? tipo);
    Task<int> CrearCatalogoItemAsync(CrearCatalogoItemRequest request);
    Task DesactivarCatalogoItemAsync(int catalogoItemId);
    Task<int> SeleccionarCatalogoItemAsync(int petId, SeleccionarItemCatalogoRequest request);
    Task<int> AgregarItemPersonalizadoAsync(int petId, AgregarItemPersonalizadoRequest request);

    // Import de Word: agrega MUCHOS ítems personalizados de un tirón (un solo DbContext,
    // una sola consulta de duplicados y una sola de "siguiente orden" para todo el lote,
    // un solo SaveChanges) — la versión de a uno hacía ~3 round-trips POR ítem, así que un
    // catálogo mal-taggeado de 200+ materiales (ver PetsImportService) tardaba minutos.
    Task AgregarItemsPersonalizadosBulkAsync(int petId, List<AgregarItemPersonalizadoRequest> items);
    Task EliminarSeleccionAsync(int petId, int seleccionId);

    // Usado al reimportar un Word con "Reemplazar" marcado: desactiva TODAS las
    // selecciones activas de un grupo (marco_legal/epp/recurso) de este PETS antes de
    // insertar las nuevas — para que un ítem cuyo texto cambió en el documento no quede
    // conviviendo con su versión vieja (agregar sin reemplazar solo evita el duplicado
    // EXACTO, no la versión desactualizada).
    Task DesactivarSeleccionesGrupoAsync(int petId, string grupo);

    // Anexos
    Task<int> AgregarAnexoAsync(int petId, string nombre, string archivoUrl);
    Task EliminarAnexoAsync(int petId, int anexoId);

    // Firmas
    Task UpsertFirmaAsync(int petId, string rol, string? nombre, string? cargo, DateOnly? fecha);
    Task SetFirmaUrlAsync(int petId, string rol, string firmaUrl);

    // Versionado y aprobación
    Task<PetVersionDto> AprobarVersionAsync(int petId, string motivo, int? aprobadoPorId, string aprobadoPorNombre);
    Task<List<PetVersionDto>> GetVersionesAsync(int petId);
    Task<PetDetalleDto?> GetVersionSnapshotAsync(int petId, int numeroVersion);
}
