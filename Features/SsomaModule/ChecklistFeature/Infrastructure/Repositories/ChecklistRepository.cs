using Abril_Backend.Features.SsomaModule.ChecklistFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ChecklistFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.ChecklistFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.ChecklistFeature.Infrastructure.Repositories
{
    public class ChecklistRepository : IChecklistRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ChecklistRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        // Proyecto actual del usuario logueado, resuelto vía su Worker: misma
        // resolución que usa AlertaLoginSsomaService (asignación activa en
        // ss_hab_worker_proyecto, si no hay ninguna se cae a la última
        // vinculación activa). Null si el usuario no tiene perfil de trabajador
        // o no tiene proyecto asignado actualmente.
        public async Task<int?> GetProyectoActualDeUsuarioAsync(int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var workerId = await ctx.Person
                .Where(p => p.UserId == userId)
                .Join(ctx.Worker, p => p.PersonId, w => w.PersonId, (p, w) => (int?)w.Id)
                .FirstOrDefaultAsync();
            if (workerId == null) return null;

            var proyectoAsignado = await ctx.WorkerProyecto
                .Where(wp => wp.WorkerId == workerId && wp.FechaFin == null)
                .OrderByDescending(wp => wp.FechaInicio).ThenByDescending(wp => wp.Id)
                .Select(wp => (int?)wp.ProyectoId)
                .FirstOrDefaultAsync();
            if (proyectoAsignado != null) return proyectoAsignado;

            return await ctx.WorkerVinculacion
                .Where(v => v.WorkerId == workerId && v.FechaFin == null)
                .OrderByDescending(v => v.CreatedAt).ThenByDescending(v => v.Id)
                .Select(v => (int?)v.ProyectoId)
                .FirstOrDefaultAsync();
        }

        // ─────────────────────────────────────────────────────────────────
        // PLANTILLAS
        // ─────────────────────────────────────────────────────────────────

        public async Task<List<ChecklistPlantillaListDto>> GetPlantillasAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsChecklistPlantilla
                .Include(p => p.Partida)
                .OrderBy(p => p.Orden)
                .ThenBy(p => p.Nombre)
                .Select(p => new ChecklistPlantillaListDto
                {
                    Id               = p.Id,
                    Nombre           = p.Nombre,
                    Descripcion      = p.Descripcion,
                    TipoActivacion   = p.TipoActivacion,
                    EventoActivacion = p.EventoActivacion,
                    EsObligatorio    = p.EsObligatorio,
                    Orden            = p.Orden,
                    Activo           = p.Activo,
                    TotalItems       = p.Items.Count(i => i.Activo),
                    PartidaId        = p.PartidaId,
                    PartidaNombre    = p.Partida != null ? p.Partida.Nombre : null
                })
                .ToListAsync();
        }

        public async Task<ChecklistPlantillaDetalleDto?> GetPlantillaDetalleAsync(int plantillaId)
        {
            using var ctx = _factory.CreateDbContext();
            var p = await ctx.SsChecklistPlantilla
                .Include(x => x.Partida)
                .Include(x => x.Items.OrderBy(i => i.Orden))
                    .ThenInclude(i => i.ImagenesReferencia.OrderBy(img => img.Orden))
                .FirstOrDefaultAsync(x => x.Id == plantillaId);

            if (p == null) return null;

            return new ChecklistPlantillaDetalleDto
            {
                Id               = p.Id,
                Nombre           = p.Nombre,
                Descripcion      = p.Descripcion,
                TipoActivacion   = p.TipoActivacion,
                EventoActivacion = p.EventoActivacion,
                EsObligatorio    = p.EsObligatorio,
                Orden            = p.Orden,
                Activo           = p.Activo,
                TotalItems       = p.Items.Count(i => i.Activo),
                PartidaId        = p.PartidaId,
                PartidaNombre    = p.Partida?.Nombre,
                Items = p.Items.Select(i => new ChecklistPlantillaItemDto
                {
                    Id               = i.Id,
                    Descripcion      = i.Descripcion,
                    Orden            = i.Orden,
                    TieneAdjuntoRef  = i.TieneAdjuntoRef,
                    Activo           = i.Activo,
                    ImagenesReferencia = i.ImagenesReferencia.Select(img => new ChecklistItemImagenDto
                    {
                        Id    = img.Id,
                        Url   = img.Url,
                        Orden = img.Orden
                    }).ToList()
                }).ToList()
            };
        }

        public async Task<SsChecklistPlantilla> CreatePlantillaAsync(ChecklistPlantillaUpsertDto dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();
            var now = DateTimeOffset.UtcNow;
            var entity = new SsChecklistPlantilla
            {
                Nombre           = dto.Nombre,
                Descripcion      = dto.Descripcion,
                TipoActivacion   = dto.TipoActivacion,
                EventoActivacion = dto.EventoActivacion,
                EsObligatorio    = dto.EsObligatorio,
                Orden            = dto.Orden,
                PartidaId        = dto.PartidaId,
                Activo           = true,
                CreatedAt        = now,
                UpdatedAt        = now
            };
            ctx.SsChecklistPlantilla.Add(entity);
            await ctx.SaveChangesAsync();
            return entity;
        }

        public async Task UpdatePlantillaAsync(int plantillaId, ChecklistPlantillaUpsertDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsChecklistPlantilla.FindAsync(plantillaId)
                ?? throw new KeyNotFoundException($"Plantilla {plantillaId} no encontrada.");

            entity.Nombre           = dto.Nombre;
            entity.Descripcion      = dto.Descripcion;
            entity.TipoActivacion   = dto.TipoActivacion;
            entity.EventoActivacion = dto.EventoActivacion;
            entity.EsObligatorio    = dto.EsObligatorio;
            entity.Orden            = dto.Orden;
            entity.PartidaId        = dto.PartidaId;
            entity.UpdatedAt        = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        // ─────────────────────────────────────────────────────────────────
        // PARTIDAS (etapas constructivas)
        // ─────────────────────────────────────────────────────────────────

        public async Task<List<ChecklistPartidaDto>> GetPartidasAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsChecklistPartida
                .OrderBy(p => p.Orden)
                .ThenBy(p => p.Nombre)
                .Select(p => new ChecklistPartidaDto
                {
                    Id              = p.Id,
                    Nombre          = p.Nombre,
                    Descripcion     = p.Descripcion,
                    Orden           = p.Orden,
                    Activo          = p.Activo,
                    TotalPlantillas = p.Plantillas.Count(pl => pl.Activo)
                })
                .ToListAsync();
        }

        public async Task<SsChecklistPartida> CreatePartidaAsync(ChecklistPartidaUpsertDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var now = DateTimeOffset.UtcNow;
            var entity = new SsChecklistPartida
            {
                Nombre      = dto.Nombre,
                Descripcion = dto.Descripcion,
                Orden       = dto.Orden,
                Activo      = true,
                CreatedAt   = now,
                UpdatedAt   = now
            };
            ctx.SsChecklistPartida.Add(entity);
            await ctx.SaveChangesAsync();
            return entity;
        }

        public async Task UpdatePartidaAsync(int partidaId, ChecklistPartidaUpsertDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var entity = await ctx.SsChecklistPartida.FindAsync(partidaId)
                ?? throw new KeyNotFoundException($"Partida {partidaId} no encontrada.");

            entity.Nombre      = dto.Nombre;
            entity.Descripcion = dto.Descripcion;
            entity.Orden       = dto.Orden;
            entity.UpdatedAt   = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        // Borra la partida y su(s) plantilla(s) por completo — solo si ningún
        // proyecto tiene ya ítems completados en ellas (si los hay, se conserva
        // como historial y hay que desactivar/marcar "no aplica" en vez de borrar).
        public async Task DeletePartidaAsync(int partidaId)
        {
            using var ctx = _factory.CreateDbContext();

            var plantillas = await ctx.SsChecklistPlantilla.Where(p => p.PartidaId == partidaId).ToListAsync();
            var plantillaIds = plantillas.Select(p => p.Id).ToList();

            var checklistsProyecto = await ctx.SsChecklistProyecto
                .Include(c => c.Items)
                .Where(c => plantillaIds.Contains(c.PlantillaId))
                .ToListAsync();

            if (checklistsProyecto.Any(c => c.Items.Any(i => i.Completado)))
                throw new InvalidOperationException("No se puede eliminar: ya hay ítems completados en algún proyecto para esta partida.");

            foreach (var c in checklistsProyecto)
                ctx.SsChecklistProyectoItem.RemoveRange(c.Items);
            ctx.SsChecklistProyecto.RemoveRange(checklistsProyecto);

            var plantillaItems = await ctx.SsChecklistPlantillaItem.Where(i => plantillaIds.Contains(i.PlantillaId)).ToListAsync();
            var plantillaItemIds = plantillaItems.Select(i => i.Id).ToList();
            var imagenes = await ctx.SsChecklistPlantillaItemImagen.Where(img => plantillaItemIds.Contains(img.PlantillaItemId)).ToListAsync();
            ctx.SsChecklistPlantillaItemImagen.RemoveRange(imagenes);
            ctx.SsChecklistPlantillaItem.RemoveRange(plantillaItems);

            ctx.SsChecklistPlantilla.RemoveRange(plantillas);

            var partida = await ctx.SsChecklistPartida.FindAsync(partidaId)
                ?? throw new KeyNotFoundException($"Partida {partidaId} no encontrada.");
            ctx.SsChecklistPartida.Remove(partida);

            await ctx.SaveChangesAsync();
        }

        // ─────────────────────────────────────────────────────────────────
        // IMÁGENES DE REFERENCIA DE UN ITEM DE PLANTILLA
        // ─────────────────────────────────────────────────────────────────

        public async Task<ChecklistItemImagenDto> AddImagenReferenciaAsync(int plantillaItemId, string url)
        {
            using var ctx = _factory.CreateDbContext();

            var maxOrden = await ctx.SsChecklistPlantillaItemImagen
                .Where(i => i.PlantillaItemId == plantillaItemId)
                .Select(i => (int?)i.Orden)
                .MaxAsync() ?? 0;

            var imagen = new SsChecklistPlantillaItemImagen
            {
                PlantillaItemId = plantillaItemId,
                Url             = url,
                Orden           = maxOrden + 1,
                CreatedAt       = DateTimeOffset.UtcNow
            };
            ctx.SsChecklistPlantillaItemImagen.Add(imagen);
            await ctx.SaveChangesAsync();

            return new ChecklistItemImagenDto { Id = imagen.Id, Url = imagen.Url, Orden = imagen.Orden };
        }

        public async Task DeleteImagenReferenciaAsync(int imagenId)
        {
            using var ctx = _factory.CreateDbContext();
            var imagen = await ctx.SsChecklistPlantillaItemImagen.FindAsync(imagenId);
            if (imagen == null) return;
            ctx.SsChecklistPlantillaItemImagen.Remove(imagen);
            await ctx.SaveChangesAsync();
        }

        public async Task<SsChecklistPlantillaItem> AddItemToPlantillaAsync(int plantillaId, ChecklistPlantillaItemCreateDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var now = DateTimeOffset.UtcNow;

            // Orden: al final de los items existentes
            var maxOrden = await ctx.SsChecklistPlantillaItem
                .Where(i => i.PlantillaId == plantillaId)
                .Select(i => (int?)i.Orden)
                .MaxAsync() ?? 0;

            var item = new SsChecklistPlantillaItem
            {
                PlantillaId     = plantillaId,
                Descripcion     = dto.Descripcion,
                TieneAdjuntoRef = dto.TieneAdjuntoRef,
                Orden           = maxOrden + 1,
                Activo          = true,
                CreatedAt       = now,
                UpdatedAt       = now
            };
            ctx.SsChecklistPlantillaItem.Add(item);
            await ctx.SaveChangesAsync();

            // Propagar a todos los proyectos que ya tienen esta plantilla activa
            var checklistsActivos = await ctx.SsChecklistProyecto
                .Where(c => c.PlantillaId == plantillaId)
                .ToListAsync();

            if (checklistsActivos.Count > 0)
            {
                var nuevosItems = checklistsActivos.Select(c => new SsChecklistProyectoItem
                {
                    ChecklistProyectoId = c.Id,
                    PlantillaItemId     = item.Id,
                    Completado          = false,
                    CreatedAt           = now,
                    UpdatedAt           = now
                }).ToList();

                ctx.SsChecklistProyectoItem.AddRange(nuevosItems);
                await ctx.SaveChangesAsync();
            }

            return item;
        }

        public async Task UpdatePlantillaItemAsync(int itemId, ChecklistPlantillaItemEditDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var item = await ctx.SsChecklistPlantillaItem.FindAsync(itemId)
                ?? throw new KeyNotFoundException($"Item {itemId} no encontrado.");

            item.Descripcion     = dto.Descripcion;
            item.TieneAdjuntoRef = dto.TieneAdjuntoRef;
            item.Activo          = dto.Activo;
            item.UpdatedAt       = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        // Reordenar ítems: el orden importa porque va reflejando la secuencia real
        // de avance de obra (qué requisito corresponde antes en el proceso).
        public async Task SetOrdenItemAsync(int itemId, int nuevoOrden)
        {
            using var ctx = _factory.CreateDbContext();
            var item = await ctx.SsChecklistPlantillaItem.FindAsync(itemId)
                ?? throw new KeyNotFoundException($"Item {itemId} no encontrado.");
            item.Orden     = nuevoOrden;
            item.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        // ─────────────────────────────────────────────────────────────────
        // CHECKLISTS DE PROYECTO
        // ─────────────────────────────────────────────────────────────────

        public async Task<ChecklistProyectoResumenDto> GetResumenProyectoAsync(int proyectoId)
        {
            using var ctx = _factory.CreateDbContext();

            var checklists = await ctx.SsChecklistProyecto
                .Include(c => c.Plantilla)
                    .ThenInclude(p => p!.Partida)
                .Include(c => c.Items)
                .Where(c => c.ProyectoId == proyectoId)
                .OrderBy(c => c.Plantilla!.Orden)
                .ToListAsync();

            // Resolver nombres de quienes activaron
            var activadoPorIds = checklists
                .Where(c => c.ActivadoPorId.HasValue)
                .Select(c => c.ActivadoPorId!.Value)
                .Distinct()
                .ToList();

            var usuarios = await ctx.User
                .Where(u => activadoPorIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.Person.FullName })
                .ToDictionaryAsync(u => u.UserId, u => u.FullName);

            var cards = checklists.Select(c => new ChecklistProyectoCardDto
            {
                ChecklistProyectoId  = c.Id,
                PlantillaId          = c.PlantillaId,
                NombrePlantilla      = c.Plantilla?.Nombre ?? "",
                EsObligatorio        = c.Plantilla?.EsObligatorio ?? false,
                PartidaId            = c.Plantilla?.PartidaId,
                PartidaNombre        = c.Plantilla?.Partida?.Nombre,
                Estado               = c.Estado,
                PorcentajeCompletado = c.PorcentajeCompletado,
                TotalItems           = c.Items.Count,
                ItemsCompletados     = c.Items.Count(i => i.Completado),
                FechaActivacion      = c.FechaActivacion,
                FechaCompletado      = c.FechaCompletado,
                ActivadoPor          = c.ActivadoPorId.HasValue && usuarios.TryGetValue(c.ActivadoPorId.Value, out var nombre) ? nombre : null,
                NoAplicaMotivo       = c.NoAplicaMotivo
            }).ToList();

            return new ChecklistProyectoResumenDto
            {
                ProyectoId  = proyectoId,
                Checklists  = cards
            };
        }

        public async Task<ChecklistProyectoDetalleDto?> GetChecklistDetalleAsync(int checklistProyectoId)
        {
            using var ctx = _factory.CreateDbContext();

            var c = await ctx.SsChecklistProyecto
                .Include(x => x.Plantilla)
                .Include(x => x.Proyecto)
                .Include(x => x.Items)
                    .ThenInclude(i => i.PlantillaItem)
                        .ThenInclude(pi => pi!.ImagenesReferencia)
                .Include(x => x.Items)
                    .ThenInclude(i => i.CompletadoPor)
                        .ThenInclude(u => u!.Person)
                .FirstOrDefaultAsync(x => x.Id == checklistProyectoId);

            if (c == null) return null;

            string? noAplicaPorNombre = null;
            if (c.NoAplicaPorId.HasValue)
            {
                noAplicaPorNombre = await ctx.User
                    .Where(u => u.UserId == c.NoAplicaPorId.Value)
                    .Select(u => u.Person.FullName)
                    .FirstOrDefaultAsync();
            }

            return new ChecklistProyectoDetalleDto
            {
                Id                   = c.Id,
                ProyectoId           = c.ProyectoId,
                ProyectoNombre       = c.Proyecto?.ProjectDescription ?? "",
                PlantillaId          = c.PlantillaId,
                NombrePlantilla      = c.Plantilla?.Nombre ?? "",
                EsObligatorio        = c.Plantilla?.EsObligatorio ?? false,
                Estado               = c.Estado,
                PorcentajeCompletado = c.PorcentajeCompletado,
                FechaActivacion      = c.FechaActivacion,
                FechaCompletado      = c.FechaCompletado,
                NoAplicaMotivo       = c.NoAplicaMotivo,
                NoAplicaPor          = noAplicaPorNombre,
                NoAplicaFecha        = c.NoAplicaFecha,
                Items = c.Items
                    .OrderBy(i => i.PlantillaItem?.Orden ?? 0)
                    .Select(i => new ChecklistProyectoItemDto
                    {
                        Id              = i.Id,
                        PlantillaItemId = i.PlantillaItemId,
                        Descripcion     = i.PlantillaItem?.Descripcion ?? "",
                        Orden           = i.PlantillaItem?.Orden ?? 0,
                        TieneAdjuntoRef = i.PlantillaItem?.TieneAdjuntoRef ?? false,
                        Completado      = i.Completado,
                        FechaCompletado = i.FechaCompletado,
                        CompletadoPor   = i.CompletadoPor?.Person?.FullName,
                        Observacion     = i.Observacion,
                        UrlAdjunto      = i.UrlAdjunto,
                        ImagenesReferencia = (i.PlantillaItem?.ImagenesReferencia ?? new List<SsChecklistPlantillaItemImagen>())
                            .OrderBy(img => img.Orden)
                            .Select(img => new ChecklistItemImagenDto { Id = img.Id, Url = img.Url, Orden = img.Orden })
                            .ToList()
                    }).ToList()
            };
        }

        public async Task<SsChecklistProyecto> ActivarChecklistAsync(int proyectoId, int plantillaId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            // Idempotente: si ya existe, lo retorna
            var existente = await ctx.SsChecklistProyecto
                .FirstOrDefaultAsync(c => c.ProyectoId == proyectoId && c.PlantillaId == plantillaId);
            if (existente != null)
                return existente;

            var items = await ctx.SsChecklistPlantillaItem
                .Where(i => i.PlantillaId == plantillaId && i.Activo)
                .OrderBy(i => i.Orden)
                .ToListAsync();

            var now = DateTimeOffset.UtcNow;
            var checklist = new SsChecklistProyecto
            {
                ProyectoId       = proyectoId,
                PlantillaId      = plantillaId,
                Estado           = "pendiente",
                PorcentajeCompletado = 0,
                FechaActivacion  = now,
                ActivadoPorId    = userId,
                NotificacionEnviada = false,
                CreatedAt        = now,
                UpdatedAt        = now
            };
            ctx.SsChecklistProyecto.Add(checklist);
            await ctx.SaveChangesAsync();

            var proyectoItems = items.Select(i => new SsChecklistProyectoItem
            {
                ChecklistProyectoId = checklist.Id,
                PlantillaItemId     = i.Id,
                Completado          = false,
                CreatedAt           = now,
                UpdatedAt           = now
            }).ToList();

            ctx.SsChecklistProyectoItem.AddRange(proyectoItems);
            await ctx.SaveChangesAsync();

            return checklist;
        }

        // Solo permite desactivar un checklist opcional que aún no tiene ningún ítem
        // completado — si ya se llenó algo, hay que conservarlo como historial.
        public async Task DesactivarChecklistAsync(int checklistProyectoId)
        {
            using var ctx = _factory.CreateDbContext();

            var checklist = await ctx.SsChecklistProyecto
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == checklistProyectoId)
                ?? throw new KeyNotFoundException($"Checklist {checklistProyectoId} no encontrado.");

            if (checklist.Items.Any(i => i.Completado))
                throw new InvalidOperationException("No se puede desactivar: ya tiene ítems completados.");

            ctx.SsChecklistProyectoItem.RemoveRange(checklist.Items);
            ctx.SsChecklistProyecto.Remove(checklist);
            await ctx.SaveChangesAsync();
        }

        // Para proyectos avanzados que ya pasaron esa etapa antes de que este
        // checklist existiera (o que genuinamente no les corresponde): se marca
        // con motivo en vez de forzar a completar algo que ya no tiene sentido.
        public async Task MarcarNoAplicaAsync(int checklistProyectoId, string motivo, int? userId)
        {
            using var ctx = _factory.CreateDbContext();
            var checklist = await ctx.SsChecklistProyecto.FindAsync(checklistProyectoId)
                ?? throw new KeyNotFoundException($"Checklist {checklistProyectoId} no encontrado.");

            var now = DateTimeOffset.UtcNow;
            checklist.Estado = "no_aplica";
            checklist.NoAplicaMotivo = motivo;
            checklist.NoAplicaPorId = userId;
            checklist.NoAplicaFecha = now;
            checklist.UpdatedAt = now;
            await ctx.SaveChangesAsync();
        }

        public async Task ReactivarChecklistAsync(int checklistProyectoId)
        {
            using var ctx = _factory.CreateDbContext();
            var checklist = await ctx.SsChecklistProyecto
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == checklistProyectoId)
                ?? throw new KeyNotFoundException($"Checklist {checklistProyectoId} no encontrado.");

            var total = checklist.Items.Count;
            var completados = checklist.Items.Count(i => i.Completado);
            var porcentaje = total == 0 ? 0m : Math.Round((decimal)completados / total * 100, 2);

            checklist.Estado = porcentaje == 0 ? "pendiente" : porcentaje < 100 ? "en_progreso" : "completado";
            checklist.NoAplicaMotivo = null;
            checklist.NoAplicaPorId = null;
            checklist.NoAplicaFecha = null;
            checklist.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task SeedChecklistsObligatoriosAsync(int proyectoId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var plantillasObligatorias = await ctx.SsChecklistPlantilla
                .Where(p => p.EsObligatorio && p.Activo && p.TipoActivacion == "automatico")
                .ToListAsync();

            var now = DateTimeOffset.UtcNow;
            foreach (var plantilla in plantillasObligatorias)
            {
                var yaExiste = await ctx.SsChecklistProyecto
                    .AnyAsync(c => c.ProyectoId == proyectoId && c.PlantillaId == plantilla.Id);
                if (yaExiste) continue;

                var items = await ctx.SsChecklistPlantillaItem
                    .Where(i => i.PlantillaId == plantilla.Id && i.Activo)
                    .OrderBy(i => i.Orden)
                    .ToListAsync();

                var checklist = new SsChecklistProyecto
                {
                    ProyectoId          = proyectoId,
                    PlantillaId         = plantilla.Id,
                    Estado              = "pendiente",
                    PorcentajeCompletado = 0,
                    FechaActivacion     = now,
                    ActivadoPorId       = userId,
                    NotificacionEnviada = false,
                    CreatedAt           = now,
                    UpdatedAt           = now
                };
                ctx.SsChecklistProyecto.Add(checklist);
                await ctx.SaveChangesAsync();

                var proyectoItems = items.Select(i => new SsChecklistProyectoItem
                {
                    ChecklistProyectoId = checklist.Id,
                    PlantillaItemId     = i.Id,
                    Completado          = false,
                    CreatedAt           = now,
                    UpdatedAt           = now
                }).ToList();

                ctx.SsChecklistProyectoItem.AddRange(proyectoItems);
                await ctx.SaveChangesAsync();
            }
        }

        // Igual que SeedChecklistsObligatoriosAsync pero al revés: en vez de "todas
        // las plantillas obligatorias para UN proyecto nuevo", es "UNA plantilla
        // obligatoria para TODOS los proyectos activos que aún no la tienen" — se
        // dispara al crear/editar un checklist por partida, para no depender de un
        // script manual cada vez que se agrega uno.
        public async Task PropagarATodosLosProyectosAsync(int plantillaId, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var proyectoIds = await ctx.Project
                .Where(p => p.Active && p.Estado == "ACTIVO")
                .Select(p => p.ProjectId)
                .ToListAsync();

            var items = await ctx.SsChecklistPlantillaItem
                .Where(i => i.PlantillaId == plantillaId && i.Activo)
                .OrderBy(i => i.Orden)
                .ToListAsync();

            var now = DateTimeOffset.UtcNow;
            foreach (var proyectoId in proyectoIds)
            {
                var yaExiste = await ctx.SsChecklistProyecto
                    .AnyAsync(c => c.ProyectoId == proyectoId && c.PlantillaId == plantillaId);
                if (yaExiste) continue;

                var checklist = new SsChecklistProyecto
                {
                    ProyectoId          = proyectoId,
                    PlantillaId         = plantillaId,
                    Estado              = "pendiente",
                    PorcentajeCompletado = 0,
                    FechaActivacion     = now,
                    ActivadoPorId       = userId,
                    NotificacionEnviada = false,
                    CreatedAt           = now,
                    UpdatedAt           = now
                };
                ctx.SsChecklistProyecto.Add(checklist);
                await ctx.SaveChangesAsync();

                var proyectoItems = items.Select(i => new SsChecklistProyectoItem
                {
                    ChecklistProyectoId = checklist.Id,
                    PlantillaItemId     = i.Id,
                    Completado          = false,
                    CreatedAt           = now,
                    UpdatedAt           = now
                }).ToList();

                ctx.SsChecklistProyectoItem.AddRange(proyectoItems);
                await ctx.SaveChangesAsync();
            }
        }

        public async Task<(decimal porcentaje, bool recienCompletado)> ToggleItemAsync(
            int checklistProyectoItemId, ChecklistItemToggleDto dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();
            var now = DateTimeOffset.UtcNow;

            var item = await ctx.SsChecklistProyectoItem
                .Include(i => i.ChecklistProyecto)
                .FirstOrDefaultAsync(i => i.Id == checklistProyectoItemId)
                ?? throw new KeyNotFoundException($"Item {checklistProyectoItemId} no encontrado.");

            item.Completado     = dto.Completado;
            item.FechaCompletado = dto.Completado ? now : null;
            item.CompletadoPorId = dto.Completado ? userId : null;
            item.Observacion    = dto.Observacion;
            item.UrlAdjunto     = dto.UrlAdjunto;
            item.UpdatedAt      = now;

            await ctx.SaveChangesAsync();

            // Recalcular porcentaje del checklist padre
            var checklist = item.ChecklistProyecto!;
            var todos = await ctx.SsChecklistProyectoItem
                .Where(i => i.ChecklistProyectoId == checklist.Id)
                .ToListAsync();

            var total = todos.Count;
            var completados = todos.Count(i => i.Completado);
            var porcentaje = total == 0 ? 0m : Math.Round((decimal)completados / total * 100, 2);

            var estabaCompletado = checklist.Estado == "completado";
            checklist.PorcentajeCompletado = porcentaje;
            checklist.Estado = porcentaje == 0 ? "pendiente"
                             : porcentaje < 100 ? "en_progreso"
                             : "completado";

            if (porcentaje == 100 && checklist.FechaCompletado == null)
                checklist.FechaCompletado = now;
            else if (porcentaje < 100)
                checklist.FechaCompletado = null;

            checklist.UpdatedAt = now;
            await ctx.SaveChangesAsync();

            // recienCompletado = acaba de llegar al 100% y notificación aún no enviada
            var recienCompletado = porcentaje == 100 && !estabaCompletado && !checklist.NotificacionEnviada;
            return (porcentaje, recienCompletado);
        }

        public async Task<(string? emailGerente, string nombreProyecto, string nombreChecklist)> GetDatosNotificacionAsync(int checklistProyectoId)
        {
            using var ctx = _factory.CreateDbContext();
            var data = await ctx.SsChecklistProyecto
                .Include(c => c.Proyecto)
                .Include(c => c.Plantilla)
                .Where(c => c.Id == checklistProyectoId)
                .Select(c => new
                {
                    EmailGerente    = c.Proyecto != null ? c.Proyecto.EmailResponsable : null,
                    NombreProyecto  = c.Proyecto != null ? c.Proyecto.ProjectDescription : "",
                    NombreChecklist = c.Plantilla != null ? c.Plantilla.Nombre : ""
                })
                .FirstOrDefaultAsync();

            if (data == null) return (null, "", "");
            return (data.EmailGerente, data.NombreProyecto, data.NombreChecklist);
        }

        public async Task<int> GetChecklistProyectoIdByItemAsync(int checklistProyectoItemId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsChecklistProyectoItem
                .Where(i => i.Id == checklistProyectoItemId)
                .Select(i => i.ChecklistProyectoId)
                .FirstOrDefaultAsync();
        }

        public async Task MarcarNotificacionEnviadaAsync(int checklistProyectoId)
        {
            using var ctx = _factory.CreateDbContext();
            var c = await ctx.SsChecklistProyecto.FindAsync(checklistProyectoId);
            if (c == null) return;
            c.NotificacionEnviada = true;
            c.UpdatedAt = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync();
        }
    }
}
