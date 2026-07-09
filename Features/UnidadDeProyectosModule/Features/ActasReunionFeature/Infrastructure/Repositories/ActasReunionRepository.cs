using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Repositories
{
    public class ActasReunionRepository : IActasReunionRepository
    {
        public const string EstadoProgramada = "PROGRAMADA";
        public const string EstadoRealizada = "REALIZADA";
        public const string EstadoCancelada = "CANCELADA";
        public const string AcuerdoPendiente = "PENDIENTE";
        public const string AcuerdoCumplido = "CUMPLIDO";

        private readonly IDbContextFactory<AppDbContext> _factory;

        public ActasReunionRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        // ── Listado ──────────────────────────────────────────────────────────
        public async Task<ReunionPaginaInicialDto> GetPaginaInicial(ReunionFiltroRequest filtro)
        {
            using var ctx = _factory.CreateDbContext();

            var proyectos = await ctx.Project
                .Where(p => p.State && p.Active)
                .OrderBy(p => p.ProjectDescription)
                .Select(p => new ProyectoFiltroDto
                {
                    ProjectId = p.ProjectId,
                    ProjectDescription = p.ProjectDescription,
                })
                .ToListAsync();

            var estados = await ctx.ReunionEstado
                .Where(e => e.State && e.Active)
                .OrderBy(e => e.ReunionEstadoId)
                .Select(e => new CatalogoDto { Id = e.ReunionEstadoId, Descripcion = e.Descripcion })
                .ToListAsync();

            var trabajadores = await GetTrabajadoresAbril(ctx);

            var reuniones = await GetReunionesInterno(ctx, filtro);

            return new ReunionPaginaInicialDto
            {
                Proyectos = proyectos,
                ReunionEstados = estados,
                Trabajadores = trabajadores,
                Reuniones = reuniones,
            };
        }

        public async Task<PagedResultDto<ReunionListItemDto>> GetReuniones(ReunionFiltroRequest filtro)
        {
            using var ctx = _factory.CreateDbContext();
            return await GetReunionesInterno(ctx, filtro);
        }

        private static async Task<PagedResultDto<ReunionListItemDto>> GetReunionesInterno(AppDbContext ctx, ReunionFiltroRequest filtro)
        {
            var query = ctx.Reunion.Where(r => r.State);

            if (filtro.ProjectId.HasValue)
                query = query.Where(r => r.ProjectId == filtro.ProjectId.Value);
            if (filtro.ReunionEstadoId.HasValue)
                query = query.Where(r => r.ReunionEstadoId == filtro.ReunionEstadoId.Value);
            if (filtro.Desde.HasValue)
                query = query.Where(r => r.Fecha >= filtro.Desde.Value);
            if (filtro.Hasta.HasValue)
                query = query.Where(r => r.Fecha <= filtro.Hasta.Value);

            var page = filtro.Page < 1 ? 1 : filtro.Page;
            var pageSize = filtro.PageSize < 1 ? 10 : filtro.PageSize;

            var total = await query.CountAsync();

            var data = await query
                .OrderByDescending(r => r.Fecha)
                .ThenByDescending(r => r.ReunionId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new ReunionListItemDto
                {
                    ReunionId = r.ReunionId,
                    ProjectId = r.ProjectId,
                    ProjectDescription = ctx.Project
                        .Where(p => p.ProjectId == r.ProjectId)
                        .Select(p => p.ProjectDescription)
                        .First(),
                    Numero = r.Numero,
                    Tema = r.Tema,
                    Lugar = r.Lugar,
                    Fecha = r.Fecha,
                    HoraInicio = r.HoraInicio,
                    HoraFin = r.HoraFin,
                    ReunionEstadoId = r.ReunionEstadoId,
                    ReunionEstado = ctx.ReunionEstado
                        .Where(e => e.ReunionEstadoId == r.ReunionEstadoId)
                        .Select(e => e.Descripcion)
                        .First(),
                    TotalAcuerdos = ctx.ReunionAcuerdo
                        .Count(a => a.ReunionId == r.ReunionId && a.State),
                    AcuerdosCumplidos = ctx.ReunionAcuerdo
                        .Count(a => a.ReunionId == r.ReunionId && a.State
                            && ctx.ReunionAcuerdoEstado
                                .Any(e => e.ReunionAcuerdoEstadoId == a.ReunionAcuerdoEstadoId
                                    && e.Descripcion == AcuerdoCumplido)),
                    VecesReprogramada = ctx.ReunionReprogramacion
                        .Count(x => x.ReunionId == r.ReunionId && x.State),
                    TotalArchivos = ctx.ReunionArchivo
                        .Count(x => x.ReunionId == r.ReunionId && x.State),
                })
                .ToListAsync();

            return new PagedResultDto<ReunionListItemDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalRecords = total,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                Data = data,
            };
        }

        // ── Detalle ──────────────────────────────────────────────────────────
        public async Task<ReunionDetalleDto> GetDetalle(int reunionId)
        {
            using var ctx = _factory.CreateDbContext();

            var detalle = await ctx.Reunion
                .Where(r => r.ReunionId == reunionId && r.State)
                .Select(r => new ReunionDetalleDto
                {
                    ReunionId = r.ReunionId,
                    ProjectId = r.ProjectId,
                    ProjectDescription = ctx.Project
                        .Where(p => p.ProjectId == r.ProjectId)
                        .Select(p => p.ProjectDescription)
                        .First(),
                    Numero = r.Numero,
                    Tema = r.Tema,
                    ConvocadoPor = r.ConvocadoPor,
                    Lugar = r.Lugar,
                    Fecha = r.Fecha,
                    HoraInicio = r.HoraInicio,
                    HoraFin = r.HoraFin,
                    ReunionEstadoId = r.ReunionEstadoId,
                    ReunionEstado = ctx.ReunionEstado
                        .Where(e => e.ReunionEstadoId == r.ReunionEstadoId)
                        .Select(e => e.Descripcion)
                        .First(),
                    Observaciones = r.Observaciones,
                    ReunionAnteriorId = r.ReunionAnteriorId,
                })
                .FirstOrDefaultAsync();

            if (detalle is null)
                throw new AbrilException("El acta de reunión no existe.", 404);

            if (detalle.ReunionAnteriorId.HasValue)
            {
                var anterior = await ctx.Reunion
                    .Where(r => r.ReunionId == detalle.ReunionAnteriorId.Value && r.State)
                    .Select(r => new { r.Numero, r.Tema })
                    .FirstOrDefaultAsync();
                detalle.ReunionAnteriorNumero = anterior?.Numero;
                detalle.ReunionAnteriorTema = anterior?.Tema;
            }

            var siguiente = await ctx.Reunion
                .Where(r => r.ReunionAnteriorId == reunionId && r.State)
                .OrderBy(r => r.ReunionId)
                .Select(r => new { r.ReunionId, r.Numero, r.Tema })
                .FirstOrDefaultAsync();
            detalle.ReunionSiguienteId = siguiente?.ReunionId;
            detalle.ReunionSiguienteNumero = siguiente?.Numero;
            detalle.ReunionSiguienteTema = siguiente?.Tema;

            detalle.Participantes = await ctx.ReunionParticipante
                .Where(p => p.ReunionId == reunionId && p.State)
                .OrderBy(p => p.Orden).ThenBy(p => p.ReunionParticipanteId)
                .Select(p => new ReunionParticipanteDto
                {
                    ReunionParticipanteId = p.ReunionParticipanteId,
                    Nombre = p.Nombre,
                    Cargo = p.Cargo,
                    Iniciales = p.Iniciales,
                    Asistio = p.Asistio,
                    Orden = p.Orden,
                })
                .ToListAsync();

            detalle.Acuerdos = await ctx.ReunionAcuerdo
                .Where(a => a.ReunionId == reunionId && a.State)
                .OrderBy(a => a.Orden).ThenBy(a => a.ReunionAcuerdoId)
                .Select(a => new ReunionAcuerdoDto
                {
                    ReunionAcuerdoId = a.ReunionAcuerdoId,
                    Descripcion = a.Descripcion,
                    Acciones = a.Acciones,
                    FechaProgramada = a.FechaProgramada,
                    FechaReprogramacion = a.FechaReprogramacion,
                    FechaCumplimiento = a.FechaCumplimiento,
                    ReunionAcuerdoEstadoId = a.ReunionAcuerdoEstadoId,
                    ReunionAcuerdoEstado = ctx.ReunionAcuerdoEstado
                        .Where(e => e.ReunionAcuerdoEstadoId == a.ReunionAcuerdoEstadoId)
                        .Select(e => e.Descripcion)
                        .First(),
                    Orden = a.Orden,
                })
                .ToListAsync();

            // Responsables de todos los acuerdos en una sola consulta
            if (detalle.Acuerdos.Count > 0)
            {
                var acuerdoIds = detalle.Acuerdos.Select(a => a.ReunionAcuerdoId).ToList();
                var responsables = await ctx.ReunionAcuerdoResponsable
                    .Where(x => acuerdoIds.Contains(x.ReunionAcuerdoId) && x.State)
                    .Select(x => new { x.ReunionAcuerdoId, x.ReunionParticipanteId })
                    .ToListAsync();
                var porAcuerdo = responsables
                    .GroupBy(x => x.ReunionAcuerdoId)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.ReunionParticipanteId).ToList());
                foreach (var acuerdo in detalle.Acuerdos)
                    acuerdo.ResponsableIds = porAcuerdo.TryGetValue(acuerdo.ReunionAcuerdoId, out var ids)
                        ? ids
                        : new List<int>();
            }

            detalle.Archivos = await ctx.ReunionArchivo
                .Where(x => x.ReunionId == reunionId && x.State)
                .OrderBy(x => x.ReunionArchivoId)
                .Select(x => new ReunionArchivoDto
                {
                    ReunionArchivoId = x.ReunionArchivoId,
                    ArchivoUrl = x.ArchivoUrl,
                    OriginalFileName = x.OriginalFileName,
                    CreatedDateTime = x.CreatedDateTime,
                })
                .ToListAsync();

            detalle.Reprogramaciones = await ctx.ReunionReprogramacion
                .Where(x => x.ReunionId == reunionId && x.State)
                .OrderByDescending(x => x.ReunionReprogramacionId)
                .Select(x => new ReunionReprogramacionDto
                {
                    ReunionReprogramacionId = x.ReunionReprogramacionId,
                    FechaAnterior = x.FechaAnterior,
                    HoraInicioAnterior = x.HoraInicioAnterior,
                    HoraFinAnterior = x.HoraFinAnterior,
                    FechaNueva = x.FechaNueva,
                    HoraInicioNueva = x.HoraInicioNueva,
                    HoraFinNueva = x.HoraFinNueva,
                    Motivo = x.Motivo,
                    CreatedDateTime = x.CreatedDateTime,
                    CreatedUserName = ctx.Person
                        .Where(p => p.UserId == x.CreatedUserId)
                        .Select(p => p.FullName)
                        .FirstOrDefault(),
                })
                .ToListAsync();

            detalle.AcuerdoEstados = await ctx.ReunionAcuerdoEstado
                .Where(e => e.State && e.Active)
                .OrderBy(e => e.ReunionAcuerdoEstadoId)
                .Select(e => new CatalogoDto { Id = e.ReunionAcuerdoEstadoId, Descripcion = e.Descripcion })
                .ToListAsync();

            detalle.Trabajadores = await GetTrabajadoresAbril(ctx);

            return detalle;
        }

        // ── Creación / edición ───────────────────────────────────────────────
        public async Task<int> Create(ReunionCreateRequest request, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var proyectoExiste = await ctx.Project.AnyAsync(p => p.ProjectId == request.ProjectId && p.State);
            if (!proyectoExiste)
                throw new AbrilException("El proyecto seleccionado no existe.", 400);

            if (request.ReunionAnteriorId.HasValue)
            {
                var anteriorValida = await ctx.Reunion.AnyAsync(r =>
                    r.ReunionId == request.ReunionAnteriorId.Value
                    && r.ProjectId == request.ProjectId
                    && r.State);
                if (!anteriorValida)
                    throw new AbrilException("La reunión anterior indicada no pertenece al proyecto.", 400);
            }

            var estadoProgramadaId = await GetEstadoReunionId(ctx, EstadoProgramada);

            var numero = (await ctx.Reunion
                .Where(r => r.ProjectId == request.ProjectId && r.State)
                .MaxAsync(r => (int?)r.Numero) ?? 0) + 1;

            var now = DateTime.UtcNow;
            var reunion = new Reunion
            {
                ProjectId = request.ProjectId,
                Numero = numero,
                Tema = request.Tema.Trim(),
                ConvocadoPor = request.ConvocadoPor?.Trim(),
                Lugar = request.Lugar?.Trim(),
                Fecha = request.Fecha,
                HoraInicio = request.HoraInicio,
                HoraFin = request.HoraFin,
                ReunionEstadoId = estadoProgramadaId,
                ReunionAnteriorId = request.ReunionAnteriorId,
                CreatedDateTime = now,
                CreatedUserId = userId,
                Active = true,
                State = true,
            };
            ctx.Reunion.Add(reunion);
            await ctx.SaveChangesAsync();

            var entrantes = request.Participantes.Where(p => !string.IsNullOrWhiteSpace(p.Nombre)).ToList();
            var orden = 0;
            foreach (var p in entrantes)
            {
                ctx.ReunionParticipante.Add(new ReunionParticipante
                {
                    ReunionId = reunion.ReunionId,
                    Nombre = p.Nombre.Trim(),
                    Cargo = p.Cargo?.Trim(),
                    Iniciales = p.Iniciales?.Trim(),
                    Asistio = p.Asistio,
                    Orden = orden++,
                    CreatedDateTime = now,
                    CreatedUserId = userId,
                    Active = true,
                    State = true,
                });
            }
            await BackfillPuestoTrabajadores(ctx, entrantes);
            if (orden > 0 || ctx.ChangeTracker.HasChanges())
                await ctx.SaveChangesAsync();

            return reunion.ReunionId;
        }

        public async Task Update(int reunionId, ReunionUpdateRequest request, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var reunion = await GetReunionOrThrow(ctx, reunionId);

            var now = DateTime.UtcNow;
            reunion.Tema = request.Tema.Trim();
            reunion.ConvocadoPor = request.ConvocadoPor?.Trim();
            reunion.Lugar = request.Lugar?.Trim();
            reunion.HoraInicio = request.HoraInicio;
            reunion.HoraFin = request.HoraFin;
            reunion.Observaciones = request.Observaciones?.Trim();
            reunion.UpdatedDateTime = now;
            reunion.UpdatedUserId = userId;

            var existentes = await ctx.ReunionParticipante
                .Where(p => p.ReunionId == reunionId && p.State)
                .ToListAsync();

            var entrantes = request.Participantes
                .Where(p => !string.IsNullOrWhiteSpace(p.Nombre))
                .ToList();
            var idsEntrantes = entrantes
                .Where(p => p.ReunionParticipanteId.HasValue)
                .Select(p => p.ReunionParticipanteId!.Value)
                .ToHashSet();

            // Participantes quitados: soft delete + soft delete de sus responsabilidades
            var eliminados = existentes.Where(p => !idsEntrantes.Contains(p.ReunionParticipanteId)).ToList();
            if (eliminados.Count > 0)
            {
                var idsEliminados = eliminados.Select(p => p.ReunionParticipanteId).ToList();
                var responsabilidades = await ctx.ReunionAcuerdoResponsable
                    .Where(x => idsEliminados.Contains(x.ReunionParticipanteId) && x.State)
                    .ToListAsync();
                foreach (var resp in responsabilidades)
                {
                    resp.State = false;
                    resp.UpdatedDateTime = now;
                    resp.UpdatedUserId = userId;
                }
                foreach (var p in eliminados)
                {
                    p.State = false;
                    p.UpdatedDateTime = now;
                    p.UpdatedUserId = userId;
                }
            }

            var orden = 0;
            foreach (var input in entrantes)
            {
                if (input.ReunionParticipanteId.HasValue)
                {
                    var existente = existentes.FirstOrDefault(p => p.ReunionParticipanteId == input.ReunionParticipanteId.Value);
                    if (existente is null)
                        throw new AbrilException("Uno de los participantes enviados no pertenece a la reunión.", 400);
                    existente.Nombre = input.Nombre.Trim();
                    existente.Cargo = input.Cargo?.Trim();
                    existente.Iniciales = input.Iniciales?.Trim();
                    existente.Asistio = input.Asistio;
                    existente.Orden = orden++;
                    existente.UpdatedDateTime = now;
                    existente.UpdatedUserId = userId;
                }
                else
                {
                    ctx.ReunionParticipante.Add(new ReunionParticipante
                    {
                        ReunionId = reunionId,
                        Nombre = input.Nombre.Trim(),
                        Cargo = input.Cargo?.Trim(),
                        Iniciales = input.Iniciales?.Trim(),
                        Asistio = input.Asistio,
                        Orden = orden++,
                        CreatedDateTime = now,
                        CreatedUserId = userId,
                        Active = true,
                        State = true,
                    });
                }
            }

            await BackfillPuestoTrabajadores(ctx, entrantes);

            await ctx.SaveChangesAsync();
        }

        public async Task Reprogramar(int reunionId, ReunionReprogramarRequest request, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var reunion = await GetReunionOrThrow(ctx, reunionId);

            var estadoActual = await ctx.ReunionEstado
                .Where(e => e.ReunionEstadoId == reunion.ReunionEstadoId)
                .Select(e => e.Descripcion)
                .FirstAsync();
            if (estadoActual == EstadoRealizada)
                throw new AbrilException("No se puede reprogramar una reunión que ya fue realizada.", 400);

            var now = DateTime.UtcNow;
            ctx.ReunionReprogramacion.Add(new ReunionReprogramacion
            {
                ReunionId = reunionId,
                FechaAnterior = reunion.Fecha,
                HoraInicioAnterior = reunion.HoraInicio,
                HoraFinAnterior = reunion.HoraFin,
                FechaNueva = request.Fecha,
                HoraInicioNueva = request.HoraInicio,
                HoraFinNueva = request.HoraFin,
                Motivo = request.Motivo?.Trim(),
                CreatedDateTime = now,
                CreatedUserId = userId,
                Active = true,
                State = true,
            });

            reunion.Fecha = request.Fecha;
            reunion.HoraInicio = request.HoraInicio;
            reunion.HoraFin = request.HoraFin;
            reunion.UpdatedDateTime = now;
            reunion.UpdatedUserId = userId;

            // Reprogramar una reunión cancelada la vuelve a dejar programada.
            if (estadoActual == EstadoCancelada)
                reunion.ReunionEstadoId = await GetEstadoReunionId(ctx, EstadoProgramada);

            await ctx.SaveChangesAsync();
        }

        public async Task CambiarEstado(int reunionId, string estado, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var reunion = await GetReunionOrThrow(ctx, reunionId);
            var estadoId = await GetEstadoReunionId(ctx, estado);

            reunion.ReunionEstadoId = estadoId;
            reunion.UpdatedDateTime = DateTime.UtcNow;
            reunion.UpdatedUserId = userId;
            await ctx.SaveChangesAsync();
        }

        public async Task Eliminar(int reunionId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var reunion = await GetReunionOrThrow(ctx, reunionId);

            var tieneSiguiente = await ctx.Reunion.AnyAsync(r => r.ReunionAnteriorId == reunionId && r.State);
            if (tieneSiguiente)
                throw new AbrilException("No se puede eliminar: otra reunión promovió su tema desde esta acta.", 400);

            reunion.State = false;
            reunion.UpdatedDateTime = DateTime.UtcNow;
            reunion.UpdatedUserId = userId;
            await ctx.SaveChangesAsync();
        }

        // ── Acuerdos ─────────────────────────────────────────────────────────
        public async Task<int> CrearAcuerdo(int reunionId, ReunionAcuerdoRequest request, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            await GetReunionOrThrow(ctx, reunionId);
            await ValidarResponsables(ctx, reunionId, request.ResponsableIds);

            var estadoId = request.ReunionAcuerdoEstadoId
                ?? await GetEstadoAcuerdoId(ctx, AcuerdoPendiente);

            var now = DateTime.UtcNow;
            var orden = (await ctx.ReunionAcuerdo
                .Where(a => a.ReunionId == reunionId && a.State)
                .MaxAsync(a => (int?)a.Orden) ?? 0) + 1;

            var acuerdo = new ReunionAcuerdo
            {
                ReunionId = reunionId,
                Descripcion = request.Descripcion.Trim(),
                Acciones = request.Acciones?.Trim(),
                FechaProgramada = request.FechaProgramada,
                FechaReprogramacion = request.FechaReprogramacion,
                FechaCumplimiento = request.FechaCumplimiento,
                ReunionAcuerdoEstadoId = estadoId,
                Orden = orden,
                CreatedDateTime = now,
                CreatedUserId = userId,
                Active = true,
                State = true,
            };
            ctx.ReunionAcuerdo.Add(acuerdo);
            await ctx.SaveChangesAsync();

            foreach (var participanteId in request.ResponsableIds.Distinct())
            {
                ctx.ReunionAcuerdoResponsable.Add(new ReunionAcuerdoResponsable
                {
                    ReunionAcuerdoId = acuerdo.ReunionAcuerdoId,
                    ReunionParticipanteId = participanteId,
                    CreatedDateTime = now,
                    CreatedUserId = userId,
                    Active = true,
                    State = true,
                });
            }
            if (request.ResponsableIds.Count > 0)
                await ctx.SaveChangesAsync();

            return acuerdo.ReunionAcuerdoId;
        }

        public async Task ActualizarAcuerdo(int reunionAcuerdoId, ReunionAcuerdoRequest request, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var acuerdo = await ctx.ReunionAcuerdo
                .FirstOrDefaultAsync(a => a.ReunionAcuerdoId == reunionAcuerdoId && a.State);
            if (acuerdo is null)
                throw new AbrilException("El acuerdo no existe.", 404);

            await ValidarResponsables(ctx, acuerdo.ReunionId, request.ResponsableIds);

            var now = DateTime.UtcNow;
            acuerdo.Descripcion = request.Descripcion.Trim();
            acuerdo.Acciones = request.Acciones?.Trim();
            acuerdo.FechaProgramada = request.FechaProgramada;
            acuerdo.FechaReprogramacion = request.FechaReprogramacion;
            acuerdo.FechaCumplimiento = request.FechaCumplimiento;
            if (request.ReunionAcuerdoEstadoId.HasValue)
                acuerdo.ReunionAcuerdoEstadoId = request.ReunionAcuerdoEstadoId.Value;
            acuerdo.UpdatedDateTime = now;
            acuerdo.UpdatedUserId = userId;

            // Sincroniza responsables: agrega los nuevos y desactiva los quitados.
            var actuales = await ctx.ReunionAcuerdoResponsable
                .Where(x => x.ReunionAcuerdoId == reunionAcuerdoId && x.State)
                .ToListAsync();
            var idsNuevos = request.ResponsableIds.Distinct().ToHashSet();

            foreach (var actual in actuales.Where(x => !idsNuevos.Contains(x.ReunionParticipanteId)))
            {
                actual.State = false;
                actual.UpdatedDateTime = now;
                actual.UpdatedUserId = userId;
            }
            var idsActuales = actuales.Select(x => x.ReunionParticipanteId).ToHashSet();
            foreach (var participanteId in idsNuevos.Where(id => !idsActuales.Contains(id)))
            {
                ctx.ReunionAcuerdoResponsable.Add(new ReunionAcuerdoResponsable
                {
                    ReunionAcuerdoId = reunionAcuerdoId,
                    ReunionParticipanteId = participanteId,
                    CreatedDateTime = now,
                    CreatedUserId = userId,
                    Active = true,
                    State = true,
                });
            }

            await ctx.SaveChangesAsync();
        }

        public async Task EliminarAcuerdo(int reunionAcuerdoId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var acuerdo = await ctx.ReunionAcuerdo
                .FirstOrDefaultAsync(a => a.ReunionAcuerdoId == reunionAcuerdoId && a.State);
            if (acuerdo is null)
                throw new AbrilException("El acuerdo no existe.", 404);

            var now = DateTime.UtcNow;
            acuerdo.State = false;
            acuerdo.UpdatedDateTime = now;
            acuerdo.UpdatedUserId = userId;

            var responsables = await ctx.ReunionAcuerdoResponsable
                .Where(x => x.ReunionAcuerdoId == reunionAcuerdoId && x.State)
                .ToListAsync();
            foreach (var resp in responsables)
            {
                resp.State = false;
                resp.UpdatedDateTime = now;
                resp.UpdatedUserId = userId;
            }

            await ctx.SaveChangesAsync();
        }

        // ── Archivos ─────────────────────────────────────────────────────────
        public async Task<List<ReunionArchivoDto>> AgregarArchivos(int reunionId, List<(string Url, string? OriginalFileName)> archivos, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            await GetReunionOrThrow(ctx, reunionId);

            var now = DateTime.UtcNow;
            var entidades = archivos.Select(a => new ReunionArchivo
            {
                ReunionId = reunionId,
                ArchivoUrl = a.Url,
                OriginalFileName = a.OriginalFileName,
                CreatedDateTime = now,
                CreatedUserId = userId,
                Active = true,
                State = true,
            }).ToList();

            ctx.ReunionArchivo.AddRange(entidades);
            await ctx.SaveChangesAsync();

            return entidades.Select(e => new ReunionArchivoDto
            {
                ReunionArchivoId = e.ReunionArchivoId,
                ArchivoUrl = e.ArchivoUrl,
                OriginalFileName = e.OriginalFileName,
                CreatedDateTime = e.CreatedDateTime,
            }).ToList();
        }

        public async Task EliminarArchivo(int reunionArchivoId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var archivo = await ctx.ReunionArchivo
                .FirstOrDefaultAsync(x => x.ReunionArchivoId == reunionArchivoId && x.State);
            if (archivo is null)
                throw new AbrilException("El archivo no existe.", 404);

            archivo.State = false;
            archivo.UpdatedDateTime = DateTime.UtcNow;
            archivo.UpdatedUserId = userId;
            await ctx.SaveChangesAsync();
        }

        // ── Carpeta de SharePoint para adjuntos (singleton) ──────────────────
        public async Task<ReunionFolderDto?> GetFolderSingleton()
        {
            using var ctx = _factory.CreateDbContext();

            return await ctx.ReunionFolder
                .Where(f => f.State)
                .OrderBy(f => f.ReunionFolderId)
                .Select(f => new ReunionFolderDto
                {
                    ReunionFolderId = f.ReunionFolderId,
                    LinkUrl = f.LinkUrl,
                    DriveId = f.DriveId,
                    FolderId = f.FolderId,
                    FolderName = f.FolderName,
                    WebUrl = f.WebUrl,
                    Active = f.Active,
                    CreatedDateTime = f.CreatedDateTime.ToOffset(TimeSpan.FromHours(-5)).DateTime,
                    CreatedUserId = f.CreatedUserId,
                })
                .FirstOrDefaultAsync();
        }

        public async Task UpsertFolder(string linkUrl, string driveId, string folderId, string? folderName, string? webUrl, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var record = await ctx.ReunionFolder
                .Where(f => f.State)
                .OrderBy(f => f.ReunionFolderId)
                .FirstOrDefaultAsync();

            if (record == null)
            {
                ctx.ReunionFolder.Add(new ReunionFolder
                {
                    LinkUrl = linkUrl,
                    DriveId = driveId,
                    FolderId = folderId,
                    FolderName = folderName,
                    WebUrl = webUrl,
                    Active = true,
                    State = true,
                    CreatedDateTime = DateTimeOffset.UtcNow,
                    CreatedUserId = userId,
                });
            }
            else
            {
                record.LinkUrl = linkUrl;
                record.DriveId = driveId;
                record.FolderId = folderId;
                record.FolderName = folderName;
                record.WebUrl = webUrl;
                record.Active = true;
                record.UpdatedDateTime = DateTimeOffset.UtcNow;
                record.UpdatedUserId = userId;
            }

            await ctx.SaveChangesAsync();
        }

        public async Task<(string DriveId, string FolderId)?> GetFolderDestination()
        {
            using var ctx = _factory.CreateDbContext();

            var f = await ctx.ReunionFolder
                .Where(x => x.State && x.Active)
                .OrderBy(x => x.ReunionFolderId)
                .Select(x => new { x.DriveId, x.FolderId })
                .FirstOrDefaultAsync();

            return f == null ? null : (f.DriveId, f.FolderId);
        }

        /// <summary>Proyecto y número de la reunión, para nombrar su subcarpeta en SharePoint.</summary>
        public async Task<(string ProjectDescription, int Numero)> GetDatosCarpetaReunion(int reunionId)
        {
            using var ctx = _factory.CreateDbContext();

            var datos = await ctx.Reunion
                .Where(r => r.ReunionId == reunionId && r.State)
                .Select(r => new
                {
                    r.Numero,
                    ProjectDescription = ctx.Project
                        .Where(p => p.ProjectId == r.ProjectId)
                        .Select(p => p.ProjectDescription)
                        .First(),
                })
                .FirstOrDefaultAsync();

            if (datos is null)
                throw new AbrilException("El acta de reunión no existe.", 404);

            return (datos.ProjectDescription, datos.Numero);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Trabajadores de Abril (workers con email_corporativo @abril.pe) para los desplegables
        /// de "Convocado por" y de participantes. El cargo sale de workers.puesto con fallback
        /// a workers.ocupacion.
        /// </summary>
        private static async Task<List<TrabajadorAbrilDto>> GetTrabajadoresAbril(AppDbContext ctx)
        {
            return await (
                from w in ctx.Worker
                where w.EmailCorporativo != null && w.EmailCorporativo.ToLower().Contains("@abril.pe")
                join p in ctx.Person on w.PersonId equals p.PersonId
                where p.State == true
                orderby p.FullName
                select new TrabajadorAbrilDto
                {
                    WorkerId = w.Id,
                    FullName = p.FullName,
                    Cargo = w.Puesto != null && w.Puesto.Trim() != ""
                        ? w.Puesto
                        : (w.Ocupacion != null && w.Ocupacion.Trim() != "" ? w.Ocupacion : null),
                }
            ).ToListAsync();
        }

        /// <summary>
        /// Si un participante se eligió del desplegable de trabajadores (trae WorkerId) y su cargo
        /// se ingresó a mano porque el worker no tenía puesto ni ocupacion, ese texto se guarda en
        /// workers.puesto para completar la ficha del trabajador. No pisa datos existentes.
        /// </summary>
        private static async Task BackfillPuestoTrabajadores(AppDbContext ctx, List<ReunionParticipanteInput> participantes)
        {
            var cargoPorWorker = participantes
                .Where(p => p.WorkerId.HasValue && !string.IsNullOrWhiteSpace(p.Cargo))
                .GroupBy(p => p.WorkerId!.Value)
                .ToDictionary(g => g.Key, g => g.First().Cargo!.Trim());
            if (cargoPorWorker.Count == 0) return;

            var ids = cargoPorWorker.Keys.ToList();
            var workers = await ctx.Worker
                .Where(w => ids.Contains(w.Id)
                    && (w.Puesto == null || w.Puesto.Trim() == "")
                    && (w.Ocupacion == null || w.Ocupacion.Trim() == ""))
                .ToListAsync();
            foreach (var worker in workers)
            {
                worker.Puesto = cargoPorWorker[worker.Id];
                worker.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        private static async Task<Reunion> GetReunionOrThrow(AppDbContext ctx, int reunionId)
        {
            var reunion = await ctx.Reunion.FirstOrDefaultAsync(r => r.ReunionId == reunionId && r.State);
            if (reunion is null)
                throw new AbrilException("El acta de reunión no existe.", 404);
            return reunion;
        }

        private static async Task<int> GetEstadoReunionId(AppDbContext ctx, string descripcion)
        {
            var id = await ctx.ReunionEstado
                .Where(e => e.Descripcion == descripcion && e.State)
                .Select(e => (int?)e.ReunionEstadoId)
                .FirstOrDefaultAsync();
            if (id is null)
                throw new AbrilException($"El estado de reunión '{descripcion}' no está configurado.", 400);
            return id.Value;
        }

        private static async Task<int> GetEstadoAcuerdoId(AppDbContext ctx, string descripcion)
        {
            var id = await ctx.ReunionAcuerdoEstado
                .Where(e => e.Descripcion == descripcion && e.State)
                .Select(e => (int?)e.ReunionAcuerdoEstadoId)
                .FirstOrDefaultAsync();
            if (id is null)
                throw new AbrilException($"El estado de acuerdo '{descripcion}' no está configurado.", 400);
            return id.Value;
        }

        private static async Task ValidarResponsables(AppDbContext ctx, int reunionId, List<int> responsableIds)
        {
            if (responsableIds.Count == 0) return;
            var ids = responsableIds.Distinct().ToList();
            var validos = await ctx.ReunionParticipante
                .CountAsync(p => ids.Contains(p.ReunionParticipanteId) && p.ReunionId == reunionId && p.State);
            if (validos != ids.Count)
                throw new AbrilException("Uno o más responsables no son participantes de la reunión.", 400);
        }
    }
}
