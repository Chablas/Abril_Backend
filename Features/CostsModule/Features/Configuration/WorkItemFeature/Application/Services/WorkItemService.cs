using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemFeature.Application.Dtos;
using Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemFeature.Application.Interfaces;
using Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemFeature.Infrastructure.Interfaces;
using Abril_Backend.Shared.Services.SharePoint.Interfaces;

namespace Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemFeature.Application.Services
{
    public class WorkItemService : IWorkItemService
    {
        private readonly IWorkItemRepository _repository;
        private readonly IGraphSharePointService _graphSharePoint;

        // Quita el prefijo numérico de orden, incluso multi-segmento:
        // "4. ELEVADORES" → "ELEVADORES", "9 MUEBLES DE COCINA" → "MUEBLES DE COCINA",
        // "7.2. LUMBRERAS" → "LUMBRERAS", "8.1 ZUÑIGA" → "ZUÑIGA", "1.2.3) PISOS" → "PISOS".
        // Tolera separadores . ) - – : entre niveles y espacios.
        private static readonly Regex _folderPrefixRegex =
            new(@"^\s*\d+(?:\s*[.)\-–:]\s*\d+)*\s*[.)\-–:]?\s*", RegexOptions.Compiled);

        public WorkItemService(IWorkItemRepository repository, IGraphSharePointService graphSharePoint)
        {
            _repository = repository;
            _graphSharePoint = graphSharePoint;
        }

        public async Task<PagedResult<WorkItemDto>> GetPaged(WorkItemFilterDto filter)
        {
            if (filter.Page < 1) filter.Page = 1;
            return await _repository.GetPaged(filter);
        }

        public async Task<WorkItemFormDataDto> GetFormData()
        {
            return new WorkItemFormDataDto { Specialties = await _repository.GetActiveSpecialties() };
        }

        public async Task Create(WorkItemCreateDto dto, int userId)
        {
            await _repository.Create(dto, userId);
        }

        public async Task Update(WorkItemEditDto dto, int userId)
        {
            await _repository.Update(dto, userId);
        }

        public async Task<bool> Delete(int workItemId, int userId)
        {
            return await _repository.Delete(workItemId, userId);
        }

        public async Task<WorkItemSyncResultDto> SyncPartidasAsync(int userId)
        {
            var result = new WorkItemSyncResultDto();

            var roots = await _repository.GetActiveAdjudicacionFolderRoots();

            // Especialidades registradas → clave normalizada (sin prefijo, sin tildes, mayúsculas).
            var specialties = await _repository.GetActiveSpecialties();
            var specialtyByKey = new Dictionary<string, int>();
            foreach (var s in specialties)
                specialtyByKey[NormalizeKey(s.WorkSpecialtyDescription)] = s.WorkSpecialtyId;

            // Partidas activas ya registradas → mapa por clave normalizada (las soft-deleted no
            // cuentan: el índice único parcial permite recrear ese nombre). Sirve para no duplicar
            // y para rellenar la especialidad de las que la tengan en null.
            var existingByKey = new Dictionary<string, ExistingWorkItemDto>();
            foreach (var p in await _repository.GetActivePartidas())
                existingByKey[NormalizeKey(p.WorkItemDescription)] = p;

            // Partidas descubiertas en este recorrido (dedup entre proyectos):
            // clave normalizada → (descripción a guardar, especialidad). Se prioriza una
            // especialidad concreta sobre null si la misma partida aparece en varias carpetas.
            var discovered = new Dictionary<string, (string Description, int? WorkSpecialtyId)>();

            // Partidas existentes a las que se les completará la especialidad faltante: id → especialidad.
            var specialtyToFill = new Dictionary<int, int>();

            foreach (var root in roots)
            {
                result.ProjectsScanned++;

                // La carpeta configurada en Configuración → Carpeta de adjudicaciones ES directamente
                // la carpeta de "Contratos" del proyecto: sus hijos son las carpetas de especialidad.
                var specialtyFolders = await _graphSharePoint.GetChildFoldersByItemIdAsync(root.DriveId, root.FolderId);

                foreach (var specialtyFolder in specialtyFolders)
                {
                    var specialtyKey = NormalizeKey(specialtyFolder.Name);
                    int? specialtyId = specialtyByKey.TryGetValue(specialtyKey, out var sid) ? sid : null;

                    var partidaFolders = await _graphSharePoint.GetChildFoldersByItemIdAsync(root.DriveId, specialtyFolder.ItemId);

                    foreach (var partidaFolder in partidaFolders)
                    {
                        var description = StripPrefix(partidaFolder.Name);
                        if (string.IsNullOrWhiteSpace(description)) continue;

                        var key = NormalizeKey(partidaFolder.Name);
                        if (existingByKey.TryGetValue(key, out var existing))
                        {
                            result.Existing++;
                            // Rellenar especialidad si la partida existente no la tiene y la carpeta sí matchea una.
                            if (existing.WorkSpecialtyId is null && specialtyId is not null
                                && !specialtyToFill.ContainsKey(existing.WorkItemId))
                                specialtyToFill[existing.WorkItemId] = specialtyId.Value;
                            continue;
                        }

                        if (discovered.TryGetValue(key, out var current))
                        {
                            // Completar la especialidad si la primera aparición no tenía y esta sí.
                            if (current.WorkSpecialtyId is null && specialtyId is not null)
                                discovered[key] = (current.Description, specialtyId);
                        }
                        else
                        {
                            discovered[key] = (description, specialtyId);
                        }
                    }
                }
            }

            if (discovered.Count > 0)
            {
                var created = await _repository.BulkCreate(
                    discovered.Values.Select(v => (v.Description, v.WorkSpecialtyId)),
                    userId);
                result.Created = created.Count;
                result.CreatedDescriptions = created.OrderBy(d => d).ToList();
            }

            if (specialtyToFill.Count > 0)
            {
                result.SpecialtyFilled = await _repository.AssignSpecialties(
                    specialtyToFill.Select(kv => (kv.Key, kv.Value)),
                    userId);
            }

            return result;
        }

        /// <summary>Quita el prefijo numérico de orden y recorta espacios.</summary>
        private static string StripPrefix(string name)
            => _folderPrefixRegex.Replace(name ?? "", "").Trim();

        /// <summary>Clave de comparación: sin prefijo, sin tildes, mayúsculas, espacios colapsados.</summary>
        private static string NormalizeKey(string name)
        {
            var stripped = StripPrefix(name);
            var noDiacritics = RemoveDiacritics(stripped);
            var collapsed = Regex.Replace(noDiacritics, @"\s+", " ").Trim();
            return collapsed.ToUpperInvariant();
        }

        private static string RemoveDiacritics(string text)
        {
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
