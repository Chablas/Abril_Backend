using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.SsomaModule.ChecklistFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ChecklistFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.ChecklistFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Features.SsomaModule.ChecklistFeature.Application.Services
{
    public class ChecklistService : IChecklistService
    {
        private const string ContainerImagenesReferencia = "ssoma-checklist-referencias";
        private const string ContainerEvidenciasCumplimiento = "ssoma-checklist-evidencias";

        private readonly IChecklistRepository _repo;
        private readonly IEmailService _emailService;
        private readonly IFileStorageService _storage;
        private readonly ILogger<ChecklistService> _logger;

        public ChecklistService(
            IChecklistRepository repo,
            IEmailService emailService,
            IFileStorageService storage,
            ILogger<ChecklistService> logger)
        {
            _repo         = repo;
            _emailService = emailService;
            _storage      = storage;
            _logger       = logger;
        }

        public Task<int?> GetProyectoActualDeUsuarioAsync(int userId)
            => _repo.GetProyectoActualDeUsuarioAsync(userId);

        // ── PLANTILLAS ───────────────────────────────────────────────────

        public Task<List<ChecklistPlantillaListDto>> GetPlantillasAsync()
            => _repo.GetPlantillasAsync();

        public Task<ChecklistPlantillaDetalleDto?> GetPlantillaDetalleAsync(int plantillaId)
            => _repo.GetPlantillaDetalleAsync(plantillaId);

        // Regla de negocio: un checklist con partida asignada es, por definición,
        // obligatorio para TODOS los proyectos desde el día 1 — no algo que se
        // active manualmente por proyecto. Se fuerzan los flags acá para que no
        // dependa de que quien lo edite recuerde marcarlos a mano.
        private static void AplicarReglaPartidaObligatoria(ChecklistPlantillaUpsertDto dto)
        {
            if (dto.PartidaId.HasValue)
            {
                dto.EsObligatorio = true;
                dto.TipoActivacion = "automatico";
            }
        }

        public async Task<ChecklistPlantillaDetalleDto> CreatePlantillaAsync(ChecklistPlantillaUpsertDto dto, int userId)
        {
            AplicarReglaPartidaObligatoria(dto);
            var entity = await _repo.CreatePlantillaAsync(dto, userId);

            // Obligatorio + automático: no esperar al próximo proyecto que se cree —
            // se propaga de inmediato a todos los proyectos activos existentes.
            if (dto.EsObligatorio && dto.TipoActivacion == "automatico")
                await _repo.PropagarATodosLosProyectosAsync(entity.Id, userId);

            return (await _repo.GetPlantillaDetalleAsync(entity.Id))!;
        }

        public async Task UpdatePlantillaAsync(int plantillaId, ChecklistPlantillaUpsertDto dto)
        {
            AplicarReglaPartidaObligatoria(dto);
            await _repo.UpdatePlantillaAsync(plantillaId, dto);

            if (dto.EsObligatorio && dto.TipoActivacion == "automatico")
                await _repo.PropagarATodosLosProyectosAsync(plantillaId, null);
        }

        public async Task<ChecklistPlantillaItemDto> AddItemToPlantillaAsync(int plantillaId, ChecklistPlantillaItemCreateDto dto)
        {
            var item = await _repo.AddItemToPlantillaAsync(plantillaId, dto);
            return new ChecklistPlantillaItemDto
            {
                Id              = item.Id,
                Descripcion     = item.Descripcion,
                Orden           = item.Orden,
                TieneAdjuntoRef = item.TieneAdjuntoRef,
                Activo          = item.Activo
            };
        }

        public Task UpdatePlantillaItemAsync(int itemId, ChecklistPlantillaItemEditDto dto)
            => _repo.UpdatePlantillaItemAsync(itemId, dto);

        public Task SetOrdenItemAsync(int itemId, int nuevoOrden)
            => _repo.SetOrdenItemAsync(itemId, nuevoOrden);

        // ── PARTIDAS ─────────────────────────────────────────────────────

        public Task<List<ChecklistPartidaDto>> GetPartidasAsync()
            => _repo.GetPartidasAsync();

        // Crear una partida debe dejar de inmediato su checklist listo (aunque
        // vacío) para todos los proyectos activos — no un paso manual aparte que
        // alguien puede olvidar. Se va llenando de ítems con el tiempo.
        public async Task<ChecklistPartidaDto> CreatePartidaAsync(ChecklistPartidaUpsertDto dto, int userId)
        {
            var entity = await _repo.CreatePartidaAsync(dto);

            var plantilla = await _repo.CreatePlantillaAsync(new ChecklistPlantillaUpsertDto
            {
                Nombre = entity.Nombre,
                Descripcion = null,
                TipoActivacion = "automatico",
                EsObligatorio = true,
                Orden = entity.Orden,
                PartidaId = entity.Id,
            }, userId);
            await _repo.PropagarATodosLosProyectosAsync(plantilla.Id, userId);

            return new ChecklistPartidaDto
            {
                Id = entity.Id,
                Nombre = entity.Nombre,
                Descripcion = entity.Descripcion,
                Orden = entity.Orden,
                Activo = entity.Activo,
                TotalPlantillas = 1
            };
        }

        public Task UpdatePartidaAsync(int partidaId, ChecklistPartidaUpsertDto dto)
            => _repo.UpdatePartidaAsync(partidaId, dto);

        public Task DeletePartidaAsync(int partidaId)
            => _repo.DeletePartidaAsync(partidaId);

        // ── IMÁGENES DE REFERENCIA ──────────────────────────────────────

        public async Task<ChecklistItemImagenDto> SubirImagenReferenciaAsync(int plantillaItemId, Stream fileStream, string fileName)
        {
            var urls = await _storage.UploadFilesAsync([(fileStream, fileName)], ContainerImagenesReferencia);
            var url = urls.FirstOrDefault()
                ?? throw new InvalidOperationException("No se pudo subir la imagen de referencia.");

            return await _repo.AddImagenReferenciaAsync(plantillaItemId, url);
        }

        public Task EliminarImagenReferenciaAsync(int imagenId)
            => _repo.DeleteImagenReferenciaAsync(imagenId);

        // Evidencia de cumplimiento (adjunto del propio ítem de proyecto, no la
        // foto de referencia de la plantilla): sube el archivo y devuelve la URL,
        // que el frontend recién manda junto con el toggle de "completado".
        public async Task<string> SubirAdjuntoItemAsync(Stream fileStream, string fileName)
        {
            var urls = await _storage.UploadFilesAsync([(fileStream, fileName)], ContainerEvidenciasCumplimiento);
            return urls.FirstOrDefault()
                ?? throw new InvalidOperationException("No se pudo subir el adjunto.");
        }

        // ── PROYECTO ─────────────────────────────────────────────────────

        public Task<ChecklistProyectoResumenDto> GetResumenProyectoAsync(int proyectoId)
            => _repo.GetResumenProyectoAsync(proyectoId);

        public Task<ChecklistProyectoDetalleDto?> GetChecklistDetalleAsync(int checklistProyectoId)
            => _repo.GetChecklistDetalleAsync(checklistProyectoId);

        public async Task<ChecklistProyectoDetalleDto> ActivarChecklistAsync(int proyectoId, int plantillaId, int userId)
        {
            var entity = await _repo.ActivarChecklistAsync(proyectoId, plantillaId, userId);
            return (await _repo.GetChecklistDetalleAsync(entity.Id))!;
        }

        public Task DesactivarChecklistAsync(int checklistProyectoId)
            => _repo.DesactivarChecklistAsync(checklistProyectoId);

        public Task MarcarNoAplicaAsync(int checklistProyectoId, string motivo, int? userId)
            => _repo.MarcarNoAplicaAsync(checklistProyectoId, motivo, userId);

        public Task ReactivarChecklistAsync(int checklistProyectoId)
            => _repo.ReactivarChecklistAsync(checklistProyectoId);

        public Task SeedChecklistsObligatoriosAsync(int proyectoId, int userId)
            => _repo.SeedChecklistsObligatoriosAsync(proyectoId, userId);

        public Task PropagarATodosLosProyectosAsync(int plantillaId, int? userId)
            => _repo.PropagarATodosLosProyectosAsync(plantillaId, userId);

        // ── ITEMS ────────────────────────────────────────────────────────

        public async Task<(decimal porcentaje, string estado)> ToggleItemAsync(
            int checklistProyectoItemId, ChecklistItemToggleDto dto, int userId)
        {
            var checklistProyectoId = await _repo.GetChecklistProyectoIdByItemAsync(checklistProyectoItemId);
            var (porcentaje, recienCompletado) = await _repo.ToggleItemAsync(checklistProyectoItemId, dto, userId);

            if (recienCompletado)
                await EnviarNotificacionCompletadoAsync(checklistProyectoId);

            var estado = porcentaje == 0 ? "pendiente"
                       : porcentaje < 100 ? "en_progreso"
                       : "completado";

            return (porcentaje, estado);
        }

        public async Task EnviarNotificacionCompletadoAsync(int checklistProyectoId, bool force = false)
        {
            try
            {
                var (email, proyecto, checklist) = await _repo.GetDatosNotificacionAsync(checklistProyectoId);

                if (string.IsNullOrEmpty(email))
                {
                    _logger.LogWarning("ChecklistId {Id}: sin email de Gerente configurado en el proyecto.", checklistProyectoId);
                    return;
                }

                var asunto = $"✅ Checklist completado: {checklist} — {proyecto}";
                var cuerpo = $@"
                    <h2>Checklist completado al 100%</h2>
                    <p>El checklist <strong>{checklist}</strong> del proyecto <strong>{proyecto}</strong>
                    ha sido completado en su totalidad.</p>
                    <p>Fecha: {DateTimeOffset.UtcNow.AddHours(-5):dd/MM/yyyy HH:mm} (hora Lima)</p>
                    <hr/>
                    <p style='color:#888;font-size:12px'>Mensaje automático del Sistema SSOMA — Abril Grupo Inmobiliario</p>";

                await _emailService.SendAsync(
                    to: new List<string> { email },
                    subject: asunto,
                    body: cuerpo,
                    isHtml: true);

                await _repo.MarcarNotificacionEnviadaAsync(checklistProyectoId);
                _logger.LogInformation("Notificación enviada para checklist {Id} al gerente {Email}.", checklistProyectoId, email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación de checklist {Id}.", checklistProyectoId);
            }
        }
    }
}
