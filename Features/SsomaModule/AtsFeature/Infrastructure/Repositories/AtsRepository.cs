using System.Security.Cryptography;
using System.Text;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.AtsFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.AtsFeature.Infrastructure.Repositories;

public class AtsRepository : IAtsRepository
{
    private const int PageSize = 20;

    /// <summary>Categorías que le tocan a CUALQUIER puesto, sin importar staff/oficina central —
    /// el resto solo aparece si hay un mapeo explícito en ss_ats_paso_puesto.</summary>
    private static readonly string[] CategoriasUniversales = ["Trabajos de gabinete", "Supervisión y liberación en campo"];

    private readonly IDbContextFactory<AppDbContext> _factory;

    public AtsRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<int> ResolverWorkerIdAsync(int userId)
    {
        using var ctx = _factory.CreateDbContext();

        var person = await ctx.Person.FirstOrDefaultAsync(p => p.UserId == userId)
            ?? throw new AbrilException("No tienes un perfil de persona asociado a tu usuario.", 403);

        var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.PersonId == person.PersonId)
            ?? throw new AbrilException("No tienes un perfil de trabajador registrado en el sistema.", 403);

        return worker.Id;
    }

    public async Task<(int? PuestoId, int? ProyectoActualId)> GetPuestoYProyectoActual(int workerId)
    {
        using var ctx = _factory.CreateDbContext();

        var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.Id == workerId);

        var vinculacionVigente = await ctx.Set<Abril_Backend.Infrastructure.Models.WorkerVinculacion>()
            .Where(v => v.WorkerId == workerId && v.FechaFin == null)
            .OrderByDescending(v => v.Id)
            .FirstOrDefaultAsync();

        return (worker?.PuestoId, vinculacionVigente?.ProyectoId);
    }

    public async Task<List<AtsCategoriaPasoDto>> GetPasosParaPuesto(int? puestoId)
    {
        using var ctx = _factory.CreateDbContext();

        var categorias = await ctx.SsAtsCategoriaPaso
            .Where(c => c.Activo)
            .OrderBy(c => c.Orden)
            .Include(c => c.Pasos.Where(p => p.Activo))
            .ToListAsync();

        var pasoIdsPermitidos = puestoId.HasValue
            ? await ctx.SsAtsPasoPuesto.Where(pp => pp.PuestoId == puestoId).Select(pp => pp.PasoId).ToListAsync()
            : [];

        return categorias
            .Select(c => new AtsCategoriaPasoDto
            {
                Id = c.Id,
                Nombre = c.Nombre,
                Pasos = c.Pasos
                    .Where(p => CategoriasUniversales.Contains(c.Nombre) || pasoIdsPermitidos.Contains(p.Id))
                    .OrderBy(p => p.Orden)
                    .Select(p => new AtsPasoDto { Id = p.Id, Texto = p.Texto, RequierePetar = p.RequierePetar })
                    .ToList(),
            })
            .Where(c => c.Pasos.Count > 0)
            .ToList();
    }

    /// <summary>
    /// Agrega un paso al catálogo sobre la marcha — el ATS es dinámico, así que un paso que un
    /// trabajador necesita y no está listado queda disponible para todos desde ese momento, no
    /// solo para este ATS puntual. Nace SIN mapeo de puesto (visible para nadie más hasta que
    /// alguien lo asigne en "Pasos por puesto"), salvo que su categoría sea universal.
    /// </summary>
    public async Task<AtsPasoDto> CrearPasoPersonalizado(int categoriaId, string texto)
    {
        using var ctx = _factory.CreateDbContext();
        var maxOrden = await ctx.SsAtsPaso.Where(p => p.CategoriaId == categoriaId).Select(p => (short?)p.Orden).MaxAsync() ?? 0;

        var paso = new SsAtsPaso { CategoriaId = categoriaId, Texto = texto, Orden = (short)(maxOrden + 1), Activo = true };
        ctx.SsAtsPaso.Add(paso);
        await ctx.SaveChangesAsync();

        return new AtsPasoDto { Id = paso.Id, Texto = paso.Texto };
    }

    public async Task<List<AtsPeligroDto>> GetPeligrosConRiesgos()
    {
        using var ctx = _factory.CreateDbContext();
        var peligros = await ctx.SsAtsPeligro
            .Where(p => p.Activo)
            .OrderBy(p => p.Orden)
            .Include(p => p.Riesgos.Where(r => r.Activo))
            .ToListAsync();

        return peligros.Select(p => new AtsPeligroDto
        {
            Id = p.Id,
            Nombre = p.Nombre,
            Riesgos = p.Riesgos.OrderBy(r => r.Orden)
                .Select(r => new AtsRiesgoDto { Id = r.Id, Nombre = r.Nombre, RequierePetar = r.RequierePetar })
                .ToList(),
        }).ToList();
    }

    public async Task SetRiesgoRequierePetar(int riesgoId, bool requierePetar)
    {
        using var ctx = _factory.CreateDbContext();
        var riesgo = await ctx.SsAtsRiesgo.FirstOrDefaultAsync(r => r.Id == riesgoId)
            ?? throw new AbrilException("Riesgo no encontrado.", 404);
        riesgo.RequierePetar = requierePetar;
        await ctx.SaveChangesAsync();
    }

    public async Task<List<AtsEppDto>> GetEppActivos()
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsAtsEpp.Where(e => e.Activo).OrderBy(e => e.Orden)
            .Select(e => new AtsEppDto { Id = e.Id, Nombre = e.Nombre, Categoria = e.Categoria }).ToListAsync();
    }

    public async Task<List<AtsHerramientaDto>> GetHerramientasActivas()
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsAtsHerramienta.Where(h => h.Activo).OrderBy(h => h.Categoria).ThenBy(h => h.Orden)
            .Select(h => new AtsHerramientaDto { Id = h.Id, Nombre = h.Nombre, Categoria = h.Categoria }).ToListAsync();
    }

    public async Task<List<AtsProyectoDto>> GetProyectosActivos()
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.Project.Where(p => p.Active).OrderBy(p => p.ProjectDescription)
            .Select(p => new AtsProyectoDto { Id = p.ProjectId, Nombre = p.ProjectDescription }).ToListAsync();
    }

    public async Task<List<AtsPlantillaDto>> GetPlantillasActivas()
    {
        using var ctx = _factory.CreateDbContext();
        var plantillas = await ctx.SsAtsPlantilla
            .Where(p => p.Activo)
            .Include(p => p.Peligros)
            .Include(p => p.Epps)
            .Include(p => p.Herramientas)
            .ToListAsync();

        return plantillas.Select(p => new AtsPlantillaDto
        {
            Id = p.Id,
            Nombre = p.Nombre,
            PuestoId = p.PuestoId,
            PeligroIds = p.Peligros.Select(x => x.PeligroId).ToList(),
            EppIds = p.Epps.Select(x => x.EppId).ToList(),
            HerramientaIds = p.Herramientas.Select(x => x.HerramientaId).ToList(),
        }).ToList();
    }

    public async Task<List<AtsPuestoDto>> GetPuestos()
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.Set<Abril_Backend.Shared.Models.Puesto>()
            .Where(p => p.Active && p.State)
            .OrderBy(p => p.Nombre)
            .Select(p => new AtsPuestoDto { Id = p.PuestoId, Nombre = p.Nombre })
            .ToListAsync();
    }

    public async Task<List<AtsPasoPuestoDto>> GetPasoPuestoMapeo()
    {
        using var ctx = _factory.CreateDbContext();

        var pasos = await ctx.SsAtsPaso
            .Include(p => p.Categoria)
            .Where(p => p.Activo && !CategoriasUniversales.Contains(p.Categoria!.Nombre))
            .OrderBy(p => p.Categoria!.Orden).ThenBy(p => p.Orden)
            .ToListAsync();

        var mapeos = await ctx.SsAtsPasoPuesto.ToListAsync();

        return pasos.Select(p => new AtsPasoPuestoDto
        {
            PasoId = p.Id,
            CategoriaNombre = p.Categoria!.Nombre,
            Texto = p.Texto,
            PuestoIds = mapeos.Where(m => m.PasoId == p.Id).Select(m => m.PuestoId).ToList(),
        }).ToList();
    }

    public async Task SetPasoPuestos(int pasoId, List<int> puestoIds)
    {
        using var ctx = _factory.CreateDbContext();

        var existentes = await ctx.SsAtsPasoPuesto.Where(m => m.PasoId == pasoId).ToListAsync();
        ctx.SsAtsPasoPuesto.RemoveRange(existentes);
        foreach (var puestoId in puestoIds.Distinct())
            ctx.SsAtsPasoPuesto.Add(new SsAtsPasoPuesto { PasoId = pasoId, PuestoId = puestoId });

        await ctx.SaveChangesAsync();
    }

    public async Task<int?> GetPlantillaSugerida(int? puestoId)
    {
        if (!puestoId.HasValue) return null;
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsAtsPlantillaPuesto
            .Where(pp => pp.PuestoId == puestoId && pp.Plantilla!.Activo)
            .Select(pp => (int?)pp.PlantillaId)
            .FirstOrDefaultAsync();
    }

    public async Task<List<AtsPlantillaPuestoDto>> GetPlantillaPuestoMapeo()
    {
        using var ctx = _factory.CreateDbContext();
        var plantillas = await ctx.SsAtsPlantilla
            .Where(p => p.Activo)
            .OrderBy(p => p.Nombre)
            .Select(p => new { p.Id, p.Nombre })
            .ToListAsync();
        var plantillaIds = plantillas.Select(p => p.Id).ToList();
        var mapeos = await ctx.SsAtsPlantillaPuesto
            .Where(pp => plantillaIds.Contains(pp.PlantillaId))
            .ToListAsync();
        var puestosPorPlantilla = mapeos.GroupBy(m => m.PlantillaId).ToDictionary(g => g.Key, g => g.Select(m => m.PuestoId).ToList());

        return plantillas.Select(p => new AtsPlantillaPuestoDto
        {
            PlantillaId = p.Id,
            PlantillaNombre = p.Nombre,
            PuestoIds = puestosPorPlantilla.GetValueOrDefault(p.Id, []),
        }).ToList();
    }

    public async Task SetPlantillaPuestos(int plantillaId, List<int> puestoIds)
    {
        using var ctx = _factory.CreateDbContext();
        var existentes = await ctx.SsAtsPlantillaPuesto.Where(pp => pp.PlantillaId == plantillaId).ToListAsync();
        ctx.SsAtsPlantillaPuesto.RemoveRange(existentes);
        foreach (var pid in puestoIds.Distinct())
            ctx.SsAtsPlantillaPuesto.Add(new SsAtsPlantillaPuesto { PlantillaId = plantillaId, PuestoId = pid });
        await ctx.SaveChangesAsync();
    }

    public async Task<int> Crear(int workerId, AtsGuardarRequestDto dto)
    {
        using var ctx = _factory.CreateDbContext();

        var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.Id == workerId);

        int? atsAnteriorId = null;
        if (dto.AtsAnteriorId.HasValue)
        {
            var anterior = await ctx.SsAts.FirstOrDefaultAsync(a => a.Id == dto.AtsAnteriorId.Value)
                ?? throw new AbrilException("El ATS que intentas corregir no existe.", 404);
            if (anterior.WorkerId != workerId)
                throw new AbrilException("Ese ATS no te pertenece.", 403);
            if (anterior.Estado != "Firmado")
                throw new AbrilException("Solo se puede corregir un ATS ya firmado.", 409);
            if (anterior.Fecha != DateOnly.FromDateTime(DateTime.Today))
                throw new AbrilException("Solo se puede corregir un ATS firmado el mismo día. Si es otro día, crea un ATS nuevo.", 409);
            atsAnteriorId = anterior.Id;
        }

        var ats = new SsAts
        {
            WorkerId = workerId,
            ProyectoId = dto.ProyectoId,
            PuestoId = worker?.PuestoId,
            PlantillaId = dto.PlantillaId,
            Actividad = dto.Actividad,
            TorreNombre = dto.TorreNombre,
            Pisos = dto.Pisos,
            Lugar = dto.Lugar,
            Fecha = DateOnly.FromDateTime(DateTime.Today),
            Estado = "Borrador",
            AtsAnteriorId = atsAnteriorId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await LlenarDetalle(ctx, ats, dto);

        ctx.SsAts.Add(ats);
        await ctx.SaveChangesAsync();

        var hash = ComputeHash(null, "Creado", ats.Id, workerId);
        ctx.SsAtsAuditLog.Add(new SsAtsAuditLog
        {
            AtsId = ats.Id,
            Evento = "Creado",
            Detalle = $"proyecto_id={dto.ProyectoId}",
            HashAnterior = null,
            Hash = hash,
        });
        await ctx.SaveChangesAsync();

        return ats.Id;
    }

    public async Task Editar(int id, int workerId, AtsGuardarRequestDto dto)
    {
        using var ctx = _factory.CreateDbContext();

        var ats = await ctx.SsAts
            .Include(a => a.Pasos).Include(a => a.Epps).Include(a => a.Herramientas).Include(a => a.RiesgosDetalle)
            .FirstOrDefaultAsync(a => a.Id == id)
            ?? throw new AbrilException("ATS no encontrado.", 404);

        if (ats.WorkerId != workerId)
            throw new AbrilException("Este ATS no te pertenece.", 403);
        if (ats.Estado != "Borrador")
            throw new AbrilException("Un ATS firmado no se puede editar. Crea uno nuevo si necesitas corregirlo.", 409);

        ats.ProyectoId = dto.ProyectoId;
        ats.PlantillaId = dto.PlantillaId;
        ats.Actividad = dto.Actividad;
        ats.TorreNombre = dto.TorreNombre;
        ats.Pisos = dto.Pisos;
        ats.Lugar = dto.Lugar;
        ats.UpdatedAt = DateTime.UtcNow;

        ctx.SsAtsPasoSeleccionado.RemoveRange(ats.Pasos);
        ctx.SsAtsEppSeleccionado.RemoveRange(ats.Epps);
        ctx.SsAtsHerramientaSeleccionada.RemoveRange(ats.Herramientas);
        ctx.SsAtsRiesgoDetalle.RemoveRange(ats.RiesgosDetalle);
        ats.Pasos.Clear();
        ats.Epps.Clear();
        ats.Herramientas.Clear();
        ats.RiesgosDetalle.Clear();

        await LlenarDetalle(ctx, ats, dto);

        await ctx.SaveChangesAsync();

        var hashAnterior = await GetUltimoHashAuditLog(id);
        var hash = ComputeHash(hashAnterior, "Editado", id, workerId);
        ctx.SsAtsAuditLog.Add(new SsAtsAuditLog { AtsId = id, Evento = "Editado", HashAnterior = hashAnterior, Hash = hash });
        await ctx.SaveChangesAsync();
    }

    /// <summary>Arma el detalle (pasos/EPP/herramientas/riesgos) como snapshot del catálogo — un
    /// cambio posterior al catálogo nunca reescribe un ATS ya creado, ni siquiera en borrador.</summary>
    private static async Task LlenarDetalle(AppDbContext ctx, SsAts ats, AtsGuardarRequestDto dto)
    {
        if (dto.Pasos.Count > 0)
        {
            var pasoIds = dto.Pasos.Where(p => p.PasoId.HasValue).Select(p => p.PasoId!.Value).ToList();
            var pasosCatalogo = await ctx.SsAtsPaso.Include(p => p.Categoria)
                .Where(p => pasoIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

            short orden = 0;
            foreach (var p in dto.Pasos)
            {
                if (p.PasoId.HasValue)
                {
                    if (!pasosCatalogo.TryGetValue(p.PasoId.Value, out var cat)) continue;
                    ats.Pasos.Add(new SsAtsPasoSeleccionado
                    {
                        PasoId = p.PasoId,
                        CategoriaNombre = cat.Categoria?.Nombre ?? string.Empty,
                        Texto = cat.Texto,
                        Aplica = p.Aplica,
                        Orden = orden++,
                    });
                }
                else if (!string.IsNullOrWhiteSpace(p.Texto))
                {
                    // Paso "de una sola vez": escrito a mano para este ATS puntual, no toca el
                    // catálogo ni la plantilla — no toda actividad se repite en otro ATS.
                    ats.Pasos.Add(new SsAtsPasoSeleccionado
                    {
                        PasoId = null,
                        CategoriaNombre = p.CategoriaNombre ?? string.Empty,
                        Texto = p.Texto.Trim(),
                        Aplica = p.Aplica,
                        Orden = orden++,
                    });
                }
            }
        }

        if (dto.EppIds.Count > 0)
        {
            var epps = await ctx.SsAtsEpp.Where(e => dto.EppIds.Contains(e.Id)).ToListAsync();
            foreach (var e in epps)
                ats.Epps.Add(new SsAtsEppSeleccionado { EppId = e.Id, Nombre = e.Nombre });
        }

        if (dto.HerramientaIds.Count > 0)
        {
            var herramientas = await ctx.SsAtsHerramienta.Where(h => dto.HerramientaIds.Contains(h.Id)).ToListAsync();
            foreach (var h in herramientas)
                ats.Herramientas.Add(new SsAtsHerramientaSeleccionada { HerramientaId = h.Id, Nombre = h.Nombre });
        }

        foreach (var nombre in dto.HerramientasPersonalizadas)
        {
            if (string.IsNullOrWhiteSpace(nombre)) continue;
            ats.Herramientas.Add(new SsAtsHerramientaSeleccionada { HerramientaId = null, Nombre = nombre.Trim() });
        }

        if (dto.Riesgos.Count > 0)
        {
            var riesgoIds = dto.Riesgos.Select(r => r.RiesgoId).ToList();
            var riesgosCatalogo = await ctx.SsAtsRiesgo.Include(r => r.Peligro)
                .Where(r => riesgoIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id);

            short orden = 0;
            foreach (var r in dto.Riesgos)
            {
                if (!riesgosCatalogo.TryGetValue(r.RiesgoId, out var cat)) continue;
                ats.RiesgosDetalle.Add(new SsAtsRiesgoDetalle
                {
                    PeligroId = r.PeligroId,
                    RiesgoId = r.RiesgoId,
                    PeligroNombre = cat.Peligro?.Nombre ?? string.Empty,
                    RiesgoNombre = cat.Nombre,
                    RiesgoBase = r.RiesgoBase,
                    Controles = r.Controles,
                    RiesgoResidual = r.RiesgoResidual,
                    Orden = orden++,
                });
            }
        }
    }

    public async Task<SsAts?> GetEntidad(int id)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsAts.FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<AtsResponseDto?> GetPorId(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var ats = await ctx.SsAts
            .Include(a => a.Worker).ThenInclude(w => w!.Person)
            .Include(a => a.Proyecto)
            .Include(a => a.Puesto)
            .Include(a => a.Plantilla)
            .Include(a => a.Pasos)
            .Include(a => a.Epps)
            .Include(a => a.Herramientas)
            .Include(a => a.RiesgosDetalle)
            .FirstOrDefaultAsync(a => a.Id == id);

        return ats == null ? null : ToDto(ats);
    }

    private static AtsResponseDto ToDto(SsAts ats) => new()
    {
        Id = ats.Id,
        WorkerId = ats.WorkerId,
        WorkerNombre = ats.Worker?.Person?.FullName,
        ProyectoId = ats.ProyectoId,
        ProyectoNombre = ats.Proyecto?.ProjectDescription,
        PuestoId = ats.PuestoId,
        PuestoNombre = ats.Puesto?.Nombre,
        PlantillaId = ats.PlantillaId,
        PlantillaNombre = ats.Plantilla?.Nombre,
        Actividad = ats.Actividad,
        TorreNombre = ats.TorreNombre,
        Pisos = ats.Pisos,
        Lugar = ats.Lugar,
        Fecha = ats.Fecha,
        HoraServidorFirma = ats.HoraServidorFirma,
        Lat = ats.Lat,
        Lng = ats.Lng,
        PrecisionMetros = ats.PrecisionMetros,
        SelfieUrl = ats.SelfieUrl,
        FirmaUrl = ats.FirmaUrl,
        Estado = ats.Estado,
        AtsAnteriorId = ats.AtsAnteriorId,
        PdfHash = ats.PdfHash,
        AutorizaNombre = ats.AutorizaNombre,
        AutorizaCargo = ats.AutorizaCargo,
        AutorizaFirmaUrl = ats.AutorizaFirmaUrl,
        AutorizaHoraServidor = ats.AutorizaHoraServidor,
        SsomaNombre = ats.SsomaNombre,
        SsomaCargo = ats.SsomaCargo,
        SsomaFirmaUrl = ats.SsomaFirmaUrl,
        SsomaHoraServidor = ats.SsomaHoraServidor,
        Pasos = ats.Pasos.OrderBy(p => p.Orden).Select(p => new AtsPasoResponseDto
        {
            PasoId = p.PasoId,
            CategoriaNombre = p.CategoriaNombre,
            Texto = p.Texto,
            Aplica = p.Aplica,
        }).ToList(),
        Epps = ats.Epps.Select(e => e.Nombre).ToList(),
        Herramientas = ats.Herramientas.Select(h => h.Nombre).ToList(),
        Riesgos = ats.RiesgosDetalle.OrderBy(r => r.Orden).Select(r => new AtsRiesgoDetalleResponseDto
        {
            PeligroId = r.PeligroId,
            RiesgoId = r.RiesgoId,
            PeligroNombre = r.PeligroNombre,
            RiesgoNombre = r.RiesgoNombre,
            RiesgoBase = r.RiesgoBase,
            Controles = r.Controles,
            RiesgoResidual = r.RiesgoResidual,
        }).ToList(),
    };

    public async Task CompletarInfoPetar(List<AtsResponseDto> ats, Dictionary<int, (bool EsResidente, bool EsSsoma)> permisosPorAtsId)
    {
        if (ats.Count == 0) return;
        using var ctx = _factory.CreateDbContext();

        var riesgoIdsQueRequierenPetar = await ctx.SsAtsRiesgo.Where(r => r.RequierePetar).Select(r => r.Id).ToListAsync();

        var atsIds = ats.Select(a => a.Id).ToList();

        // dto.Riesgos viene vacío en el listado (ToDtoResumen, ver AtsRepository.Listar) — se
        // consulta aparte, liviano (solo AtsId+RiesgoId), en vez de depender del include pesado.
        var atsIdsConRiesgoPetar = (await ctx.SsAtsRiesgoDetalle
            .Where(r => atsIds.Contains(r.AtsId) && riesgoIdsQueRequierenPetar.Contains(r.RiesgoId))
            .Select(r => r.AtsId)
            .Distinct()
            .ToListAsync())
            .ToHashSet();

        var petares = await ctx.SsPetar
            .Include(p => p.Tipo)
            .Include(p => p.PetarGrupo)
            .Where(p => atsIds.Contains(p.AtsId))
            .ToListAsync();
        var petaresPorAts = petares.ToLookup(p => p.AtsId);

        foreach (var dto in ats)
        {
            dto.RequierePetar = atsIdsConRiesgoPetar.Contains(dto.Id);
            var (esResidente, esSsoma) = permisosPorAtsId.GetValueOrDefault(dto.Id);
            dto.Petares = petaresPorAts[dto.Id]
                .Select(p =>
                {
                    // Un PETAR que nació de un PETAR grupal NO se firma desde acá — Supervisor/
                    // SSOMA firman UNA vez para todo el grupo, desde el dashboard del ATS grupal
                    // (ver PetarGrupo). Acá solo se refleja si ya quedó autorizado, nunca se
                    // habilita el botón de firmar individual (evita firmar 20 veces lo mismo).
                    var supervisorFirmaUrl = p.PetarGrupoId.HasValue ? p.PetarGrupo?.SupervisorFirmaUrl : p.SupervisorFirmaUrl;
                    var ssomaFirmaUrl = p.PetarGrupoId.HasValue ? p.PetarGrupo?.SsomaFirmaUrl : p.SsomaFirmaUrl;
                    return new AtsPetarResumenDto
                    {
                        Id = p.Id,
                        TipoNombre = p.Tipo?.Nombre,
                        Estado = p.Estado,
                        TieneFirmaEjecutante = p.FirmaUrl != null,
                        SupervisorFirmado = supervisorFirmaUrl != null,
                        SsomaFirmado = ssomaFirmaUrl != null,
                        PuedeFirmarSupervisor = !p.PetarGrupoId.HasValue && esResidente && p.FirmaUrl != null && p.SupervisorFirmaUrl == null && p.Estado == "Borrador",
                        PuedeFirmarSsoma = !p.PetarGrupoId.HasValue && esSsoma && p.FirmaUrl != null && p.SsomaFirmaUrl == null && p.Estado == "Borrador",
                    };
                })
                .ToList();
        }
    }

    public async Task<bool> TieneConsentimiento(int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsAtsConsentimiento.AnyAsync(c => c.WorkerId == workerId);
    }

    public async Task RegistrarConsentimiento(int workerId, string? ipOrigen)
    {
        using var ctx = _factory.CreateDbContext();
        ctx.SsAtsConsentimiento.Add(new SsAtsConsentimiento { WorkerId = workerId, IpOrigen = ipOrigen, AceptadoEn = DateTime.UtcNow });
        await ctx.SaveChangesAsync();
    }

    public async Task<bool> TieneAutorizacionPermiso(int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsAtsAutorizacionPermiso.AnyAsync(a => a.WorkerId == workerId && a.ArchivoUrl != null);
    }

    public async Task<(string? FirmaDigitalUrl, string? Nombre, string? Dni)> GetFirmaDigitalAutorizacion(int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        var permiso = await ctx.SsAtsAutorizacionPermiso.FirstOrDefaultAsync(a => a.WorkerId == workerId);
        var (nombre, dni) = await GetNombreYDni(workerId);
        return (permiso?.FirmaDigitalUrl, nombre, dni);
    }

    public async Task CapturarFirmaDigitalAutorizacion(int workerId, string firmaUrl, string firmaHash, int capturadoPorUserId)
    {
        using var ctx = _factory.CreateDbContext();
        var existente = await ctx.SsAtsAutorizacionPermiso.FirstOrDefaultAsync(a => a.WorkerId == workerId);

        if (existente != null)
        {
            existente.FirmaDigitalUrl = firmaUrl;
            existente.FirmaDigitalHash = firmaHash;
            existente.FirmadoDigitalEn = DateTime.UtcNow;
            existente.FirmadoDigitalPorUserId = capturadoPorUserId;
        }
        else
        {
            ctx.SsAtsAutorizacionPermiso.Add(new SsAtsAutorizacionPermiso
            {
                WorkerId = workerId,
                FirmaDigitalUrl = firmaUrl,
                FirmaDigitalHash = firmaHash,
                FirmadoDigitalEn = DateTime.UtcNow,
                FirmadoDigitalPorUserId = capturadoPorUserId,
            });
        }

        await ctx.SaveChangesAsync();
    }

    public async Task SubirAutorizacionPermiso(int workerId, string archivoUrl, int? subidoPorUserId)
    {
        using var ctx = _factory.CreateDbContext();
        var existente = await ctx.SsAtsAutorizacionPermiso.FirstOrDefaultAsync(a => a.WorkerId == workerId);

        if (existente != null)
        {
            existente.ArchivoUrl = archivoUrl;
            existente.SubidoPorUserId = subidoPorUserId;
            existente.SubidoEn = DateTime.UtcNow;
        }
        else
        {
            ctx.SsAtsAutorizacionPermiso.Add(new SsAtsAutorizacionPermiso
            {
                WorkerId = workerId,
                ArchivoUrl = archivoUrl,
                SubidoPorUserId = subidoPorUserId,
                SubidoEn = DateTime.UtcNow,
            });
        }

        await ctx.SaveChangesAsync();
    }

    /// <summary>Universo: trabajadores activos de Staff/Oficina Central — los obreros de obra
    /// llenan su ATS en físico y no necesitan esta autorización de firma digital.</summary>
    public async Task<List<AtsAutorizacionTrabajadorDto>> GetTrabajadoresParaAutorizacion()
    {
        using var ctx = _factory.CreateDbContext();

        var workers = await ctx.Worker
            .Where(w => w.WorkersEstadoId == Abril_Backend.Shared.Constants.WorkersEstadoIds.Activo
                && w.ObraOficinaStaffId != null
                && Abril_Backend.Shared.Constants.ObraOficinaStaffIds.StaffUOficinaCentral.Contains(w.ObraOficinaStaffId.Value))
            .OrderBy(w => w.Person != null ? w.Person.FullName : null)
            .Select(w => new
            {
                w.Id,
                Nombre = w.Person != null ? w.Person.FullName : null,
                Dni = w.Person != null ? w.Person.DocumentIdentityCode : null,
                ObraOficinaStaffId = w.ObraOficinaStaffId,
            })
            .ToListAsync();

        var workerIds = workers.Select(w => w.Id).ToList();

        var autorizaciones = await ctx.SsAtsAutorizacionPermiso
            .Where(a => workerIds.Contains(a.WorkerId))
            .ToListAsync();
        var autorizacionMap = autorizaciones.ToDictionary(a => a.WorkerId, a => a);

        // El proyecto vigente de un worker no vive en workers (no hay workers.project_id): se lee
        // de worker_vinculaciones sin fecha_fin, mismo criterio que EvGestionSsomaRepository.
        var proyectoPorWorker = await ctx.WorkerVinculacion
            .Where(v => workerIds.Contains(v.WorkerId) && v.FechaFin == null && v.ProyectoId != null)
            .Select(v => new { v.WorkerId, v.ProyectoId, ProyectoNombre = v.Proyecto != null ? v.Proyecto.ProjectDescription : null })
            .ToListAsync();
        var proyectoMap = proyectoPorWorker
            .GroupBy(v => v.WorkerId)
            .ToDictionary(g => g.Key, g => g.First());

        return workers.Select(w =>
        {
            proyectoMap.TryGetValue(w.Id, out var proy);
            return new AtsAutorizacionTrabajadorDto
            {
                WorkerId = w.Id,
                Nombre = w.Nombre ?? $"Worker {w.Id}",
                Dni = w.Dni,
                ProyectoId = proy?.ProyectoId,
                ProyectoNombre = proy?.ProyectoNombre,
                ObraOficinaStaff = Abril_Backend.Shared.Constants.ObraOficinaStaffIds.Nombre(w.ObraOficinaStaffId),
                TieneFirmaDigital = autorizacionMap.GetValueOrDefault(w.Id)?.FirmaDigitalUrl != null,
                TieneAutorizacion = autorizacionMap.GetValueOrDefault(w.Id)?.ArchivoUrl != null,
                SubidoEn = autorizacionMap.GetValueOrDefault(w.Id)?.SubidoEn,
                ArchivoUrl = autorizacionMap.GetValueOrDefault(w.Id)?.ArchivoUrl,
            };
        }).ToList();
    }

    public async Task Firmar(
        SsAts ats, string selfieUrl, string selfieHash, string firmaUrl, string firmaHash,
        DateTime horaServidor, AtsFirmarRequestDto body, string? ipOrigen, string? userAgent)
    {
        using var ctx = _factory.CreateDbContext();

        var entidad = await ctx.SsAts.FirstOrDefaultAsync(a => a.Id == ats.Id)
            ?? throw new AbrilException("ATS no encontrado.", 404);
        if (entidad.Estado != "Borrador")
            throw new AbrilException("Este ATS ya fue firmado.", 409);

        entidad.SelfieUrl = selfieUrl;
        entidad.SelfieHash = selfieHash;
        entidad.FirmaUrl = firmaUrl;
        entidad.FirmaHash = firmaHash;
        entidad.HoraServidorFirma = horaServidor;
        entidad.HoraDispositivo = body.HoraDispositivo;
        entidad.Lat = body.Lat;
        entidad.Lng = body.Lng;
        entidad.PrecisionMetros = body.PrecisionMetros;
        entidad.IpOrigen = ipOrigen;
        entidad.UserAgent = userAgent;
        entidad.Estado = "Firmado";
        entidad.UpdatedAt = DateTime.UtcNow;

        // Hash de verificación pública (QR del PDF) — se fija ACÁ, al firmar, y nunca se
        // vuelve a tocar. Antes se recalculaba en AtsService.GenerarPdf a partir de los bytes
        // del PDF ya generado, después de que esos mismos bytes ya llevaban impreso el QR con
        // el hash ANTERIOR (o vacío, la primera vez) — un huevo-y-gallina que garantizaba que
        // el hash del QR nunca coincidiera con el que terminaba guardado. Al depender solo de
        // datos que quedan fijos desde la firma (no de la renderización del PDF, que puede
        // variar entre exportaciones del mismo documento), el QR y el valor guardado siempre
        // coinciden, sin importar cuántas veces se vuelva a exportar el PDF.
        entidad.PdfHash = ComputeHashVerificacion(ats.Id, entidad.WorkerId, firmaHash, selfieHash, horaServidor);

        await ctx.SaveChangesAsync();

        var hashAnterior = await GetUltimoHashAuditLog(ats.Id);
        var hash = ComputeHash(hashAnterior, "Firmado", ats.Id, entidad.WorkerId, firmaHash);
        ctx.SsAtsAuditLog.Add(new SsAtsAuditLog
        {
            AtsId = ats.Id,
            Evento = "Firmado",
            IpOrigen = ipOrigen,
            Detalle = $"lat={body.Lat};lng={body.Lng};precision={body.PrecisionMetros};firma_hash={firmaHash}",
            HashAnterior = hashAnterior,
            Hash = hash,
        });
        await ctx.SaveChangesAsync();
    }

    public async Task GuardarPdf(int id, string pdfUrl, string pdfHash)
    {
        using var ctx = _factory.CreateDbContext();
        var ats = await ctx.SsAts.FirstOrDefaultAsync(a => a.Id == id) ?? throw new AbrilException("ATS no encontrado.", 404);

        ats.PdfUrl = pdfUrl;
        ats.PdfHash = pdfHash;
        await ctx.SaveChangesAsync();

        var hashAnterior = await GetUltimoHashAuditLog(id);
        var hash = ComputeHash(hashAnterior, "PdfExportado", id, ats.WorkerId, pdfHash);
        ctx.SsAtsAuditLog.Add(new SsAtsAuditLog { AtsId = id, Evento = "PdfExportado", Detalle = $"pdf_hash={pdfHash}", HashAnterior = hashAnterior, Hash = hash });
        await ctx.SaveChangesAsync();
    }

    public async Task<AtsResponsablesDto> GetResponsables(int proyectoId)
    {
        using var ctx = _factory.CreateDbContext();

        var proyecto = await ctx.Project.Include(p => p.CoordAdmin)
            .FirstOrDefaultAsync(p => p.ProjectId == proyectoId)
            ?? throw new AbrilException("Proyecto no encontrado.", 404);

        Abril_Backend.Infrastructure.Models.Worker? residente = proyecto.ResidenteWorkersId.HasValue
            ? await ctx.Worker.Include(w => w.Person).FirstOrDefaultAsync(w => w.Id == proyecto.ResidenteWorkersId)
            : null;

        var ssomaEmails = new List<string>();
        if (!string.IsNullOrWhiteSpace(proyecto.EmailCoordSsoma)) ssomaEmails.Add(proyecto.EmailCoordSsoma.Trim());
        if (!string.IsNullOrWhiteSpace(proyecto.CoordAdmin?.EmailCorporativo)) ssomaEmails.Add(proyecto.CoordAdmin.EmailCorporativo.Trim());

        return new AtsResponsablesDto
        {
            ProyectoNombre = proyecto.ProjectDescription,
            ResidenteWorkerId = residente?.Id,
            ResidenteNombre = residente?.Person?.FullName,
            ResidenteEmail = residente?.EmailCorporativo,
            SsomaEmails = ssomaEmails.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
        };
    }

    public async Task<bool> EsProduccionDeProyecto(int workerId, int proyectoId)
    {
        using var ctx = _factory.CreateDbContext();

        var vinculacionVigente = await ctx.Set<Abril_Backend.Infrastructure.Models.WorkerVinculacion>()
            .Include(v => v.PuestoCatalogo)
            .Where(v => v.WorkerId == workerId && v.FechaFin == null)
            .OrderByDescending(v => v.Id)
            .FirstOrDefaultAsync();

        if (vinculacionVigente?.ProyectoId != proyectoId || vinculacionVigente.PuestoCatalogo == null) return false;

        var nombre = vinculacionVigente.PuestoCatalogo.Nombre;
        return nombre.Contains("Ingeniero de Producción", StringComparison.OrdinalIgnoreCase)
            || nombre.Contains("Arquitecto de Producción", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> EsPrevencionistaAbril(int workerId)
    {
        using var ctx = _factory.CreateDbContext();

        var worker = await ctx.Worker.Include(w => w.PuestoCatalogo).FirstOrDefaultAsync(w => w.Id == workerId);
        if (worker?.ContrataCasa != "Casa" || worker.PuestoCatalogo == null) return false;

        return worker.PuestoCatalogo.Nombre.Contains("Prevencionista", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string?> GetEmailDeUsuario(int userId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.User.Where(u => u.UserId == userId).Select(u => u.Email).FirstOrDefaultAsync();
    }

    public async Task<string?> GetEmailCorporativoWorker(int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.Worker.Where(w => w.Id == workerId).Select(w => w.EmailCorporativo).FirstOrDefaultAsync();
    }

    public async Task<(string Nombre, string? Dni)> GetNombreYDni(int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        var worker = await ctx.Worker.Include(w => w.Person).FirstOrDefaultAsync(w => w.Id == workerId)
            ?? throw new AbrilException("Trabajador no encontrado.", 404);
        return (worker.Person?.FullName ?? $"Worker {workerId}", worker.Person?.DocumentIdentityCode);
    }

    public async Task<(string Nombre, string? Cargo)> GetNombreYCargo(int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        var worker = await ctx.Worker.Include(w => w.Person).Include(w => w.PuestoCatalogo).FirstOrDefaultAsync(w => w.Id == workerId)
            ?? throw new AbrilException("Trabajador no encontrado.", 404);

        return (worker.Person?.FullName ?? $"Worker {workerId}", worker.PuestoCatalogo?.Nombre);
    }

    public async Task FirmarVisto(int atsId, string rol, int workerId, string nombre, string? cargo, string firmaUrl, string firmaHash, DateTime horaServidor)
    {
        using var ctx = _factory.CreateDbContext();
        var ats = await ctx.SsAts.FirstOrDefaultAsync(a => a.Id == atsId) ?? throw new AbrilException("ATS no encontrado.", 404);

        if (ats.Estado != "Firmado")
            throw new AbrilException("El ejecutante debe firmar el ATS antes de que se agreguen las demás firmas.", 409);

        if (rol == "Autoriza")
        {
            if (ats.AutorizaFirmaUrl != null)
                throw new AbrilException("Este ATS ya tiene la firma de Autoriza (Residente/Ing. Producción).", 409);
            ats.AutorizaWorkerId = workerId;
            ats.AutorizaNombre = nombre;
            ats.AutorizaCargo = cargo;
            ats.AutorizaFirmaUrl = firmaUrl;
            ats.AutorizaFirmaHash = firmaHash;
            ats.AutorizaHoraServidor = horaServidor;
        }
        else
        {
            if (ats.SsomaFirmaUrl != null)
                throw new AbrilException("Este ATS ya tiene el Visto Bueno de SSOMA.", 409);
            ats.SsomaWorkerId = workerId;
            ats.SsomaNombre = nombre;
            ats.SsomaCargo = cargo;
            ats.SsomaFirmaUrl = firmaUrl;
            ats.SsomaFirmaHash = firmaHash;
            ats.SsomaHoraServidor = horaServidor;
        }

        ats.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        var hashAnterior = await GetUltimoHashAuditLog(atsId);
        var hash = ComputeHash(hashAnterior, $"Firmado{rol}", atsId, workerId, firmaHash);
        ctx.SsAtsAuditLog.Add(new SsAtsAuditLog { AtsId = atsId, Evento = $"Firmado{rol}", UserId = null, Detalle = $"firma_hash={firmaHash}", HashAnterior = hashAnterior, Hash = hash });
        await ctx.SaveChangesAsync();
    }

    public async Task<string?> GetUltimoHashAuditLog(int atsId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsAtsAuditLog.Where(l => l.AtsId == atsId).OrderByDescending(l => l.Id).Select(l => l.Hash).FirstOrDefaultAsync();
    }

    public async Task AgregarAuditLog(int atsId, string evento, int? userId, string? ipOrigen, string? detalle, string hashAnterior, string hash)
    {
        using var ctx = _factory.CreateDbContext();
        ctx.SsAtsAuditLog.Add(new SsAtsAuditLog { AtsId = atsId, Evento = evento, UserId = userId, IpOrigen = ipOrigen, Detalle = detalle, HashAnterior = hashAnterior, Hash = hash });
        await ctx.SaveChangesAsync();
    }

    public async Task<bool> ExisteSelfieHash(string selfieHash)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsAts.AnyAsync(a => a.SelfieHash == selfieHash);
    }

    /// <summary>Listado paginado — a propósito NO trae Pasos/Epps/Herramientas/RiesgosDetalle
    /// (eran includes pesados repetidos en cada una de las 20 filas de la página, aun cuando la
    /// fila no está expandida): eso se pide aparte por ATS individual (GetPorId) recién cuando el
    /// usuario hace clic en "Ver detalle" o abre el modal de firma. Con cientos de ATS/día y ~100
    /// usuarios en simultáneo, ese detalle de más era el grueso de la lentitud del listado.</summary>
    public async Task<AtsListResponseDto> Listar(AtsFiltroDto filtro)
    {
        using var ctx = _factory.CreateDbContext();

        var query = ctx.SsAts
            .Include(a => a.Worker).ThenInclude(w => w!.Person)
            .Include(a => a.Proyecto)
            .Include(a => a.Puesto)
            .AsQueryable();

        if (filtro.ProyectoId.HasValue) query = query.Where(a => a.ProyectoId == filtro.ProyectoId);
        if (filtro.WorkerId.HasValue) query = query.Where(a => a.WorkerId == filtro.WorkerId);
        if (filtro.FechaDesde.HasValue) query = query.Where(a => a.Fecha >= filtro.FechaDesde);
        if (filtro.FechaHasta.HasValue) query = query.Where(a => a.Fecha <= filtro.FechaHasta);
        if (!string.IsNullOrWhiteSpace(filtro.Estado)) query = query.Where(a => a.Estado == filtro.Estado);

        query = query.OrderByDescending(a => a.Fecha).ThenByDescending(a => a.Id);

        var total = await query.CountAsync();
        var page = Math.Max(1, filtro.Page);
        var data = await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();

        return new AtsListResponseDto
        {
            Data = data.Select(ToDtoResumen).ToList(),
            Page = page,
            PageSize = PageSize,
            TotalRecords = total,
            TotalPages = (int)Math.Ceiling(total / (double)PageSize),
        };
    }

    /// <summary>Misma forma que ToDto pero sin Pasos/Epps/Herramientas/Riesgos (listas vacías) —
    /// ver comentario en Listar. No requiere que esas colecciones de navegación vengan cargadas.</summary>
    private static AtsResponseDto ToDtoResumen(SsAts ats) => new()
    {
        Id = ats.Id,
        WorkerId = ats.WorkerId,
        WorkerNombre = ats.Worker?.Person?.FullName,
        ProyectoId = ats.ProyectoId,
        ProyectoNombre = ats.Proyecto?.ProjectDescription,
        PuestoId = ats.PuestoId,
        PuestoNombre = ats.Puesto?.Nombre,
        PlantillaId = ats.PlantillaId,
        PlantillaNombre = ats.Plantilla?.Nombre,
        Actividad = ats.Actividad,
        TorreNombre = ats.TorreNombre,
        Pisos = ats.Pisos,
        Lugar = ats.Lugar,
        Fecha = ats.Fecha,
        HoraServidorFirma = ats.HoraServidorFirma,
        Lat = ats.Lat,
        Lng = ats.Lng,
        PrecisionMetros = ats.PrecisionMetros,
        SelfieUrl = ats.SelfieUrl,
        FirmaUrl = ats.FirmaUrl,
        Estado = ats.Estado,
        AtsAnteriorId = ats.AtsAnteriorId,
        PdfHash = ats.PdfHash,
        AutorizaNombre = ats.AutorizaNombre,
        AutorizaCargo = ats.AutorizaCargo,
        AutorizaFirmaUrl = ats.AutorizaFirmaUrl,
        AutorizaHoraServidor = ats.AutorizaHoraServidor,
        SsomaNombre = ats.SsomaNombre,
        SsomaCargo = ats.SsomaCargo,
        SsomaFirmaUrl = ats.SsomaFirmaUrl,
        SsomaHoraServidor = ats.SsomaHoraServidor,
        Pasos = [],
        Epps = [],
        Herramientas = [],
        Riesgos = [],
    };

    // ── Administración de plantillas ────────────────────────────────────

    public async Task<int> CrearPlantilla(AtsPlantillaGuardarRequestDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        var plantilla = new SsAtsPlantilla { Nombre = dto.Nombre, PuestoId = dto.PuestoId, Activo = true, CreatedAt = DateTime.UtcNow };
        AsignarPlantillaDetalle(plantilla, dto);
        ctx.SsAtsPlantilla.Add(plantilla);
        await ctx.SaveChangesAsync();
        return plantilla.Id;
    }

    public async Task EditarPlantilla(int id, AtsPlantillaGuardarRequestDto dto)
    {
        using var ctx = _factory.CreateDbContext();
        var plantilla = await ctx.SsAtsPlantilla
            .Include(p => p.Peligros).Include(p => p.Epps).Include(p => p.Herramientas)
            .FirstOrDefaultAsync(p => p.Id == id) ?? throw new AbrilException("Plantilla no encontrada.", 404);

        plantilla.Nombre = dto.Nombre;
        plantilla.PuestoId = dto.PuestoId;
        ctx.SsAtsPlantillaPeligro.RemoveRange(plantilla.Peligros);
        ctx.SsAtsPlantillaEpp.RemoveRange(plantilla.Epps);
        ctx.SsAtsPlantillaHerramienta.RemoveRange(plantilla.Herramientas);
        plantilla.Peligros.Clear();
        plantilla.Epps.Clear();
        plantilla.Herramientas.Clear();
        AsignarPlantillaDetalle(plantilla, dto);

        await ctx.SaveChangesAsync();
    }

    private static void AsignarPlantillaDetalle(SsAtsPlantilla plantilla, AtsPlantillaGuardarRequestDto dto)
    {
        foreach (var pid in dto.PeligroIds) plantilla.Peligros.Add(new SsAtsPlantillaPeligro { PeligroId = pid });
        foreach (var eid in dto.EppIds) plantilla.Epps.Add(new SsAtsPlantillaEpp { EppId = eid });
        foreach (var hid in dto.HerramientaIds) plantilla.Herramientas.Add(new SsAtsPlantillaHerramienta { HerramientaId = hid });
    }

    public async Task DesactivarPlantilla(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var plantilla = await ctx.SsAtsPlantilla.FirstOrDefaultAsync(p => p.Id == id) ?? throw new AbrilException("Plantilla no encontrada.", 404);
        plantilla.Activo = false;
        await ctx.SaveChangesAsync();
    }

    // ── Actividades/pasos por plantilla ──────────────────────────────────

    public async Task<List<AtsPlantillaActividadDto>> GetActividadesDePlantilla(int plantillaId)
    {
        using var ctx = _factory.CreateDbContext();
        var actividades = await ctx.SsAtsPlantillaActividad
            .Where(a => a.PlantillaId == plantillaId && a.Activo)
            .Include(a => a.Pasos.Where(p => p.Activo))
            .Include(a => a.Peligros)
            .OrderBy(a => a.Orden)
            .ToListAsync();

        return actividades.Select(a => new AtsPlantillaActividadDto
        {
            Id = a.Id,
            PlantillaId = a.PlantillaId,
            Texto = a.Texto,
            Orden = a.Orden,
            Pasos = a.Pasos.OrderBy(p => p.Orden)
                .Select(p => new AtsPlantillaPasoDto { Id = p.Id, Texto = p.Texto, Orden = p.Orden }).ToList(),
            PeligroIds = a.Peligros.Select(p => p.PeligroId).ToList(),
        }).ToList();
    }

    public async Task<int> CrearActividad(int plantillaId, string texto)
    {
        using var ctx = _factory.CreateDbContext();
        var maxOrden = await ctx.SsAtsPlantillaActividad
            .Where(a => a.PlantillaId == plantillaId)
            .Select(a => (short?)a.Orden)
            .MaxAsync() ?? 0;
        var actividad = new SsAtsPlantillaActividad { PlantillaId = plantillaId, Texto = texto, Orden = (short)(maxOrden + 1), Activo = true };
        ctx.SsAtsPlantillaActividad.Add(actividad);
        await ctx.SaveChangesAsync();
        return actividad.Id;
    }

    public async Task EditarActividad(int actividadId, string texto)
    {
        using var ctx = _factory.CreateDbContext();
        var actividad = await ctx.SsAtsPlantillaActividad.FirstOrDefaultAsync(a => a.Id == actividadId) ?? throw new AbrilException("Actividad no encontrada.", 404);
        actividad.Texto = texto;
        await ctx.SaveChangesAsync();
    }

    public async Task EliminarActividad(int actividadId)
    {
        using var ctx = _factory.CreateDbContext();
        var actividad = await ctx.SsAtsPlantillaActividad.FirstOrDefaultAsync(a => a.Id == actividadId) ?? throw new AbrilException("Actividad no encontrada.", 404);
        actividad.Activo = false;
        await ctx.SaveChangesAsync();
    }

    public async Task<int> CrearPaso(int actividadId, string texto)
    {
        using var ctx = _factory.CreateDbContext();
        var maxOrden = await ctx.SsAtsPlantillaPaso
            .Where(p => p.ActividadId == actividadId)
            .Select(p => (short?)p.Orden)
            .MaxAsync() ?? 0;
        var paso = new SsAtsPlantillaPaso { ActividadId = actividadId, Texto = texto, Orden = (short)(maxOrden + 1), Activo = true };
        ctx.SsAtsPlantillaPaso.Add(paso);
        await ctx.SaveChangesAsync();
        return paso.Id;
    }

    public async Task EditarPaso(int pasoId, string texto)
    {
        using var ctx = _factory.CreateDbContext();
        var paso = await ctx.SsAtsPlantillaPaso.FirstOrDefaultAsync(p => p.Id == pasoId) ?? throw new AbrilException("Paso no encontrado.", 404);
        paso.Texto = texto;
        await ctx.SaveChangesAsync();
    }

    public async Task EliminarPaso(int pasoId)
    {
        using var ctx = _factory.CreateDbContext();
        var paso = await ctx.SsAtsPlantillaPaso.FirstOrDefaultAsync(p => p.Id == pasoId) ?? throw new AbrilException("Paso no encontrado.", 404);
        paso.Activo = false;
        await ctx.SaveChangesAsync();
    }

    public async Task SetActividadPeligros(int actividadId, List<int> peligroIds)
    {
        using var ctx = _factory.CreateDbContext();
        var existentes = await ctx.SsAtsPlantillaActividadPeligro.Where(x => x.ActividadId == actividadId).ToListAsync();
        ctx.SsAtsPlantillaActividadPeligro.RemoveRange(existentes);
        foreach (var pid in peligroIds.Distinct())
            ctx.SsAtsPlantillaActividadPeligro.Add(new SsAtsPlantillaActividadPeligro { ActividadId = actividadId, PeligroId = pid });
        await ctx.SaveChangesAsync();
    }

    // ── Controles sugeridos por riesgo ───────────────────────────────────

    public async Task<List<AtsRiesgoConControlesDto>> GetRiesgosConControles()
    {
        using var ctx = _factory.CreateDbContext();
        var riesgos = await ctx.SsAtsRiesgo
            .Where(r => r.Activo)
            .Include(r => r.Peligro)
            .OrderBy(r => r.Peligro!.Orden).ThenBy(r => r.Orden)
            .ToListAsync();
        var riesgoIds = riesgos.Select(r => r.Id).ToList();
        var controles = await ctx.SsAtsRiesgoControl
            .Where(c => riesgoIds.Contains(c.RiesgoId) && c.Activo)
            .OrderBy(c => c.Orden)
            .ToListAsync();
        var controlesPorRiesgo = controles.GroupBy(c => c.RiesgoId).ToDictionary(g => g.Key, g => g.ToList());

        return riesgos.Select(r => new AtsRiesgoConControlesDto
        {
            RiesgoId = r.Id,
            RiesgoNombre = r.Nombre,
            PeligroId = r.PeligroId,
            PeligroNombre = r.Peligro?.Nombre ?? string.Empty,
            Controles = controlesPorRiesgo.GetValueOrDefault(r.Id, [])
                .Select(c => new AtsRiesgoControlDto { Id = c.Id, Texto = c.Texto, Orden = c.Orden, Tipo = c.Tipo }).ToList(),
        }).ToList();
    }

    public async Task<int> CrearControl(int riesgoId, string texto, string tipo)
    {
        using var ctx = _factory.CreateDbContext();
        var maxOrden = await ctx.SsAtsRiesgoControl
            .Where(c => c.RiesgoId == riesgoId)
            .Select(c => (short?)c.Orden)
            .MaxAsync() ?? 0;
        var control = new SsAtsRiesgoControl { RiesgoId = riesgoId, Texto = texto, Tipo = tipo, Orden = (short)(maxOrden + 1), Activo = true };
        ctx.SsAtsRiesgoControl.Add(control);
        await ctx.SaveChangesAsync();
        return control.Id;
    }

    public async Task EditarControl(int controlId, string texto, string tipo)
    {
        using var ctx = _factory.CreateDbContext();
        var control = await ctx.SsAtsRiesgoControl.FirstOrDefaultAsync(c => c.Id == controlId) ?? throw new AbrilException("Control no encontrado.", 404);
        control.Texto = texto;
        control.Tipo = tipo;
        await ctx.SaveChangesAsync();
    }

    public async Task EliminarControl(int controlId)
    {
        using var ctx = _factory.CreateDbContext();
        var control = await ctx.SsAtsRiesgoControl.FirstOrDefaultAsync(c => c.Id == controlId) ?? throw new AbrilException("Control no encontrado.", 404);
        control.Activo = false;
        await ctx.SaveChangesAsync();
    }

    /// <summary>Hash que encadena la fila anterior de <see cref="SsAtsAuditLog"/> con el evento
    /// actual — ver el mismo mecanismo documentado en AtsModels.cs.</summary>
    private static string ComputeHash(string? hashAnterior, string evento, int atsId, int workerId, string? extra = null)
    {
        var payload = $"{hashAnterior}|{evento}|{atsId}|{workerId}|{DateTime.UtcNow:O}|{extra}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    /// <summary>Hash estable del contenido firmado (para el QR de verificación pública) — a
    /// diferencia de <see cref="ComputeHash"/>, NO incluye la hora actual ni se recalcula
    /// nunca: se fija una sola vez al firmar, a partir de datos que no cambian sin importar
    /// cuántas veces se re-exporte el PDF.</summary>
    private static string ComputeHashVerificacion(int atsId, int workerId, string firmaHash, string selfieHash, DateTime horaServidorFirma)
    {
        var payload = $"{atsId}|{workerId}|{firmaHash}|{selfieHash}|{horaServidorFirma:O}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    // ── ATS Grupal ──────────────────────────────────────────────────────────

    public async Task<SsAtsGrupo> CrearGrupo(int creadoPorWorkerId, AtsGuardarRequestDto dto)
    {
        using var ctx = _factory.CreateDbContext();

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var grupo = new SsAtsGrupo
        {
            CreadoPorWorkerId = creadoPorWorkerId,
            ProyectoId = dto.ProyectoId,
            PlantillaId = dto.PlantillaId,
            Actividad = dto.Actividad,
            TorreNombre = dto.TorreNombre,
            Pisos = dto.Pisos,
            Lugar = dto.Lugar,
            Fecha = hoy,
            QrToken = Guid.NewGuid(),
            // Vence a medianoche del mismo día — un ATS grupal es para la jornada de hoy, no queda
            // reutilizable después (igual criterio que el ATS individual, que es por día).
            QrExpiraEn = hoy.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc),
            Estado = "Activo",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        if (dto.Pasos.Count > 0)
        {
            var pasoIds = dto.Pasos.Where(p => p.PasoId.HasValue).Select(p => p.PasoId!.Value).ToList();
            var pasosCatalogo = await ctx.SsAtsPaso.Include(p => p.Categoria)
                .Where(p => pasoIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

            short orden = 0;
            foreach (var p in dto.Pasos)
            {
                if (p.PasoId.HasValue)
                {
                    if (!pasosCatalogo.TryGetValue(p.PasoId.Value, out var cat)) continue;
                    grupo.Pasos.Add(new SsAtsGrupoPasoSeleccionado
                    {
                        PasoId = p.PasoId,
                        CategoriaNombre = cat.Categoria?.Nombre ?? string.Empty,
                        Texto = cat.Texto,
                        Aplica = p.Aplica,
                        Orden = orden++,
                    });
                }
                else if (!string.IsNullOrWhiteSpace(p.Texto))
                {
                    grupo.Pasos.Add(new SsAtsGrupoPasoSeleccionado
                    {
                        PasoId = null,
                        CategoriaNombre = p.CategoriaNombre ?? string.Empty,
                        Texto = p.Texto.Trim(),
                        Aplica = p.Aplica,
                        Orden = orden++,
                    });
                }
            }
        }

        if (dto.EppIds.Count > 0)
        {
            var epps = await ctx.SsAtsEpp.Where(e => dto.EppIds.Contains(e.Id)).ToListAsync();
            foreach (var e in epps)
                grupo.Epps.Add(new SsAtsGrupoEppSeleccionado { EppId = e.Id, Nombre = e.Nombre });
        }

        if (dto.HerramientaIds.Count > 0)
        {
            var herramientas = await ctx.SsAtsHerramienta.Where(h => dto.HerramientaIds.Contains(h.Id)).ToListAsync();
            foreach (var h in herramientas)
                grupo.Herramientas.Add(new SsAtsGrupoHerramientaSeleccionada { HerramientaId = h.Id, Nombre = h.Nombre });
        }

        foreach (var nombre in dto.HerramientasPersonalizadas)
        {
            if (string.IsNullOrWhiteSpace(nombre)) continue;
            grupo.Herramientas.Add(new SsAtsGrupoHerramientaSeleccionada { HerramientaId = null, Nombre = nombre.Trim() });
        }

        if (dto.Riesgos.Count > 0)
        {
            var riesgoIds = dto.Riesgos.Select(r => r.RiesgoId).ToList();
            var riesgosCatalogo = await ctx.SsAtsRiesgo.Include(r => r.Peligro)
                .Where(r => riesgoIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id);

            short orden = 0;
            foreach (var r in dto.Riesgos)
            {
                if (!riesgosCatalogo.TryGetValue(r.RiesgoId, out var cat)) continue;
                grupo.RiesgosDetalle.Add(new SsAtsGrupoRiesgoDetalle
                {
                    PeligroId = r.PeligroId,
                    RiesgoId = r.RiesgoId,
                    PeligroNombre = cat.Peligro?.Nombre ?? string.Empty,
                    RiesgoNombre = cat.Nombre,
                    RiesgoBase = r.RiesgoBase,
                    Controles = r.Controles,
                    RiesgoResidual = r.RiesgoResidual,
                    Orden = orden++,
                });
            }
        }

        ctx.SsAtsGrupo.Add(grupo);
        await ctx.SaveChangesAsync();
        return grupo;
    }

    public async Task<SsAtsGrupo?> GetGrupoPorToken(Guid token)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsAtsGrupo
            .Include(g => g.Proyecto)
            .Include(g => g.Pasos)
            .Include(g => g.Epps)
            .Include(g => g.Herramientas)
            .Include(g => g.RiesgosDetalle)
            .FirstOrDefaultAsync(g => g.QrToken == token);
    }

    public async Task<SsAtsGrupo?> GetGrupoEntidad(int id)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsAtsGrupo
            .Include(g => g.Pasos)
            .Include(g => g.Epps)
            .Include(g => g.Herramientas)
            .Include(g => g.RiesgosDetalle)
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<AtsGrupoEstadoDto?> GetEstadoGrupo(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var grupo = await ctx.SsAtsGrupo.Include(g => g.Proyecto).FirstOrDefaultAsync(g => g.Id == id);
        if (grupo is null) return null;

        var adheridos = await ctx.SsAts
            .Where(a => a.AtsGrupoId == id)
            .Include(a => a.Worker).ThenInclude(w => w!.Person)
            .OrderBy(a => a.Worker!.Person!.FullName)
            .ToListAsync();

        return new AtsGrupoEstadoDto
        {
            Id = grupo.Id,
            Actividad = grupo.Actividad,
            ProyectoNombre = grupo.Proyecto?.ProjectDescription,
            TorreNombre = grupo.TorreNombre,
            Pisos = grupo.Pisos,
            Fecha = grupo.Fecha,
            Estado = grupo.Estado,
            QrToken = grupo.QrToken.ToString(),
            QrExpiraEn = grupo.QrExpiraEn,
            TotalAdhesiones = adheridos.Count,
            TrabajadoresAdheridos = adheridos.Select(a => a.Worker?.Person?.FullName ?? "—").ToList(),
        };
    }

    public async Task<bool> EsAutorDeGrupo(int atsGrupoId, int workerId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsAtsGrupo.AnyAsync(g => g.Id == atsGrupoId && g.CreadoPorWorkerId == workerId);
    }

    public async Task CerrarGrupo(int atsGrupoId)
    {
        using var ctx = _factory.CreateDbContext();
        var grupo = await ctx.SsAtsGrupo.FirstOrDefaultAsync(g => g.Id == atsGrupoId) ?? throw new AbrilException("ATS grupal no encontrado.", 404);
        grupo.Estado = "Cerrado";
        grupo.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    /// <summary>Acotado al proyecto del grupo y a trabajadores Activos que ya tienen la
    /// autorización de firma digital — no tiene sentido ofrecerle la opción a alguien que de
    /// todas formas el backend va a rechazar al intentar firmar (ExigirAutorizacionPermiso).</summary>
    public async Task<List<AtsGrupoWorkerOpcionDto>> GetWorkersParaAdhesion(int proyectoId)
    {
        using var ctx = _factory.CreateDbContext();

        var workerIdsDelProyecto = await ctx.WorkerVinculacion
            .Where(v => v.ProyectoId == proyectoId && v.FechaFin == null)
            .Select(v => v.WorkerId)
            .ToListAsync();

        var workers = await ctx.Worker
            .Where(w => workerIdsDelProyecto.Contains(w.Id)
                && w.WorkersEstadoId == Abril_Backend.Shared.Constants.WorkersEstadoIds.Activo)
            .Include(w => w.Person)
            .ToListAsync();

        var workerIds = workers.Select(w => w.Id).ToList();
        var autorizados = await ctx.SsAtsAutorizacionPermiso
            .Where(a => workerIds.Contains(a.WorkerId) && a.ArchivoUrl != null)
            .Select(a => a.WorkerId)
            .ToHashSetAsync();

        return workers
            .Where(w => autorizados.Contains(w.Id))
            .Select(w => new AtsGrupoWorkerOpcionDto
            {
                WorkerId = w.Id,
                Nombre = w.Person?.FullName ?? string.Empty,
                DniUltimos4 = w.Person?.DocumentIdentityCode is { Length: >= 4 } dni ? dni[^4..] : null,
            })
            .OrderBy(w => w.Nombre)
            .ToList();
    }

    public async Task<bool> DniCoincide(int workerId, string ultimosDigitos)
    {
        if (string.IsNullOrWhiteSpace(ultimosDigitos)) return false;
        using var ctx = _factory.CreateDbContext();
        var worker = await ctx.Worker.Include(w => w.Person).FirstOrDefaultAsync(w => w.Id == workerId);
        var dni = worker?.Person?.DocumentIdentityCode;
        if (string.IsNullOrEmpty(dni) || dni.Length < ultimosDigitos.Length) return false;
        return dni[^ultimosDigitos.Length..] == ultimosDigitos.Trim();
    }

    public async Task<int> CrearDesdeGrupo(int workerId, SsAtsGrupo grupo)
    {
        using var ctx = _factory.CreateDbContext();
        var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.Id == workerId);

        var ats = new SsAts
        {
            WorkerId = workerId,
            ProyectoId = grupo.ProyectoId,
            PuestoId = worker?.PuestoId,
            PlantillaId = grupo.PlantillaId,
            Actividad = grupo.Actividad,
            TorreNombre = grupo.TorreNombre,
            Pisos = grupo.Pisos,
            Lugar = grupo.Lugar,
            Fecha = grupo.Fecha,
            Estado = "Borrador",
            AtsGrupoId = grupo.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        short ordenPaso = 0;
        foreach (var p in grupo.Pasos)
        {
            ats.Pasos.Add(new SsAtsPasoSeleccionado
            {
                PasoId = p.PasoId,
                CategoriaNombre = p.CategoriaNombre,
                Texto = p.Texto,
                Aplica = p.Aplica,
                Orden = ordenPaso++,
            });
        }
        foreach (var e in grupo.Epps)
            ats.Epps.Add(new SsAtsEppSeleccionado { EppId = e.EppId, Nombre = e.Nombre });
        foreach (var h in grupo.Herramientas)
            ats.Herramientas.Add(new SsAtsHerramientaSeleccionada { HerramientaId = h.HerramientaId, Nombre = h.Nombre });

        short ordenRiesgo = 0;
        foreach (var r in grupo.RiesgosDetalle)
        {
            ats.RiesgosDetalle.Add(new SsAtsRiesgoDetalle
            {
                PeligroId = r.PeligroId,
                RiesgoId = r.RiesgoId,
                PeligroNombre = r.PeligroNombre,
                RiesgoNombre = r.RiesgoNombre,
                RiesgoBase = r.RiesgoBase,
                Controles = r.Controles,
                RiesgoResidual = r.RiesgoResidual,
                Orden = ordenRiesgo++,
            });
        }

        ctx.SsAts.Add(ats);
        await ctx.SaveChangesAsync();

        var hash = ComputeHash(null, "CreadoDesdeGrupo", ats.Id, workerId, $"ats_grupo_id={grupo.Id}");
        ctx.SsAtsAuditLog.Add(new SsAtsAuditLog
        {
            AtsId = ats.Id,
            Evento = "CreadoDesdeGrupo",
            Detalle = $"ats_grupo_id={grupo.Id}",
            HashAnterior = null,
            Hash = hash,
        });
        await ctx.SaveChangesAsync();

        return ats.Id;
    }
}
