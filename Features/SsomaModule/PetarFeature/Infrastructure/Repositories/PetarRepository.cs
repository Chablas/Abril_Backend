using System.Security.Cryptography;
using System.Text;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.PetarFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.PetarFeature.Infrastructure.Repositories;

public class PetarRepository : IPetarRepository
{
    private const int PageSize = 20;
    private readonly IDbContextFactory<AppDbContext> _factory;

    public PetarRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<PetarTipoDto>> GetTiposConItems()
    {
        using var ctx = _factory.CreateDbContext();
        var tipos = await ctx.SsPetarTipo
            .Where(t => t.Activo)
            .OrderBy(t => t.Orden)
            .Include(t => t.Items.Where(i => i.Activo))
            .ToListAsync();

        return tipos.Select(t => new PetarTipoDto
        {
            Id = t.Id,
            Nombre = t.Nombre,
            Codigo = t.Codigo,
            Items = t.Items.OrderBy(i => i.Orden).Select(i => new PetarItemDto { Id = i.Id, Texto = i.Texto }).ToList(),
        }).ToList();
    }

    public async Task<(int ProyectoId, string? Lugar, string Actividad, string Estado)> GetDatosAts(int atsId)
    {
        using var ctx = _factory.CreateDbContext();
        var ats = await ctx.SsAts.FirstOrDefaultAsync(a => a.Id == atsId)
            ?? throw new AbrilException("ATS de origen no encontrado.", 404);
        return (ats.ProyectoId, ats.Lugar, ats.Actividad, ats.Estado);
    }

    public async Task<int> Crear(int workerId, PetarGuardarRequestDto dto)
    {
        using var ctx = _factory.CreateDbContext();

        var ats = await ctx.SsAts.FirstOrDefaultAsync(a => a.Id == dto.AtsId)
            ?? throw new AbrilException("ATS de origen no encontrado.", 404);
        if (ats.Estado != "Firmado")
            throw new AbrilException("El ATS de origen debe estar firmado antes de generar un PETAR.", 409);

        var petar = new SsPetar
        {
            AtsId = dto.AtsId,
            TipoId = dto.TipoId,
            WorkerId = workerId,
            ProyectoId = ats.ProyectoId,
            DescripcionTrabajo = dto.DescripcionTrabajo,
            Lugar = dto.Lugar,
            Fecha = DateOnly.FromDateTime(DateTime.Today),
            HoraInicio = ParseHora(dto.HoraInicio),
            HoraFin = ParseHora(dto.HoraFin),
            Estado = "Borrador",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await LlenarRespuestas(ctx, petar, dto);
        petar.IzajeGrua = MapIzajeGrua(dto.IzajeGrua);

        ctx.SsPetar.Add(petar);
        await ctx.SaveChangesAsync();

        var hash = ComputeHash(null, "Creado", petar.Id, workerId);
        ctx.SsPetarAuditLog.Add(new SsPetarAuditLog { PetarId = petar.Id, Evento = "Creado", HashAnterior = null, Hash = hash });
        await ctx.SaveChangesAsync();

        return petar.Id;
    }

    public async Task Editar(int id, int workerId, PetarGuardarRequestDto dto)
    {
        using var ctx = _factory.CreateDbContext();

        var petar = await ctx.SsPetar.Include(p => p.Respuestas).Include(p => p.IzajeGrua).FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new AbrilException("PETAR no encontrado.", 404);
        if (petar.WorkerId != workerId)
            throw new AbrilException("Este PETAR no te pertenece.", 403);
        if (petar.Estado != "Borrador")
            throw new AbrilException("Un PETAR firmado no se puede editar.", 409);

        petar.TipoId = dto.TipoId;
        petar.DescripcionTrabajo = dto.DescripcionTrabajo;
        petar.Lugar = dto.Lugar;
        petar.HoraInicio = ParseHora(dto.HoraInicio);
        petar.HoraFin = ParseHora(dto.HoraFin);
        petar.UpdatedAt = DateTime.UtcNow;

        ctx.SsPetarItemRespuesta.RemoveRange(petar.Respuestas);
        petar.Respuestas.Clear();
        await LlenarRespuestas(ctx, petar, dto);

        if (petar.IzajeGrua != null) ctx.Remove(petar.IzajeGrua);
        petar.IzajeGrua = MapIzajeGrua(dto.IzajeGrua);

        await ctx.SaveChangesAsync();

        var hashAnterior = await GetUltimoHashAuditLog(id);
        var hash = ComputeHash(hashAnterior, "Editado", id, workerId);
        ctx.SsPetarAuditLog.Add(new SsPetarAuditLog { PetarId = id, Evento = "Editado", HashAnterior = hashAnterior, Hash = hash });
        await ctx.SaveChangesAsync();
    }

    private static SsPetarIzajeGrua? MapIzajeGrua(PetarIzajeGruaDto? dto)
    {
        if (dto == null) return null;
        return new SsPetarIzajeGrua
        {
            TipoGrua = dto.TipoGrua,
            FabricanteOMarca = dto.FabricanteOMarca,
            ModeloOPlaca = dto.ModeloOPlaca,
            SerieOTarjetaCirculacion = dto.SerieOTarjetaCirculacion,
            LongitudPlumaBrazoM = dto.LongitudPlumaBrazoM,
            RadioMaximoGiroM = dto.RadioMaximoGiroM,
            DireccionGradoGiro = dto.DireccionGradoGiro,
            ElevacionM = dto.ElevacionM,
            AnguloPluma = dto.AnguloPluma,
            CapacidadCertificadaTon = dto.CapacidadCertificadaTon,
            PesoCargaTotalTon = dto.PesoCargaTotalTon,
            PorcentajeCapacidad = dto.PorcentajeCapacidad,
            TamanoEstrobo = dto.TamanoEstrobo,
            Observaciones = dto.Observaciones,
        };
    }

    private static PetarIzajeGruaDto? MapIzajeGruaDto(SsPetarIzajeGrua? m)
    {
        if (m == null) return null;
        return new PetarIzajeGruaDto
        {
            TipoGrua = m.TipoGrua,
            FabricanteOMarca = m.FabricanteOMarca,
            ModeloOPlaca = m.ModeloOPlaca,
            SerieOTarjetaCirculacion = m.SerieOTarjetaCirculacion,
            LongitudPlumaBrazoM = m.LongitudPlumaBrazoM,
            RadioMaximoGiroM = m.RadioMaximoGiroM,
            DireccionGradoGiro = m.DireccionGradoGiro,
            ElevacionM = m.ElevacionM,
            AnguloPluma = m.AnguloPluma,
            CapacidadCertificadaTon = m.CapacidadCertificadaTon,
            PesoCargaTotalTon = m.PesoCargaTotalTon,
            PorcentajeCapacidad = m.PorcentajeCapacidad,
            TamanoEstrobo = m.TamanoEstrobo,
            Observaciones = m.Observaciones,
        };
    }

    private static async Task LlenarRespuestas(AppDbContext ctx, SsPetar petar, PetarGuardarRequestDto dto)
    {
        if (dto.Respuestas.Count == 0) return;

        var itemIds = dto.Respuestas.Select(r => r.ItemId).ToList();
        var catalogo = await ctx.SsPetarItem.Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);

        short orden = 0;
        foreach (var r in dto.Respuestas)
        {
            if (!catalogo.TryGetValue(r.ItemId, out var item)) continue;
            petar.Respuestas.Add(new SsPetarItemRespuesta
            {
                ItemId = r.ItemId,
                Texto = item.Texto,
                Respuesta = r.Respuesta,
                Orden = orden++,
            });
        }
    }

    public async Task<SsPetar?> GetEntidad(int id)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsPetar.FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<PetarResponseDto?> GetPorId(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var petar = await ctx.SsPetar
            .Include(p => p.Worker).ThenInclude(w => w!.Person)
            .Include(p => p.Proyecto)
            .Include(p => p.Tipo)
            .Include(p => p.Respuestas)
            .Include(p => p.IzajeGrua)
            .FirstOrDefaultAsync(p => p.Id == id);

        return petar == null ? null : ToDto(petar);
    }

    private static PetarResponseDto ToDto(SsPetar p) => new()
    {
        Id = p.Id,
        AtsId = p.AtsId,
        TipoId = p.TipoId,
        TipoNombre = p.Tipo?.Nombre,
        TipoCodigo = p.Tipo?.Codigo,
        WorkerId = p.WorkerId,
        WorkerNombre = p.Worker?.Person?.FullName,
        ProyectoId = p.ProyectoId,
        ProyectoNombre = p.Proyecto?.ProjectDescription,
        DescripcionTrabajo = p.DescripcionTrabajo,
        Lugar = p.Lugar,
        Fecha = p.Fecha,
        HoraInicio = p.HoraInicio?.ToString("HH:mm"),
        HoraFin = p.HoraFin?.ToString("HH:mm"),
        HoraServidorFirma = p.HoraServidorFirma,
        Lat = p.Lat,
        Lng = p.Lng,
        PrecisionMetros = p.PrecisionMetros,
        SelfieUrl = p.SelfieUrl,
        FirmaUrl = p.FirmaUrl,
        SupervisorNombre = p.SupervisorNombre,
        SupervisorCargo = p.SupervisorCargo,
        SupervisorFirmaUrl = p.SupervisorFirmaUrl,
        SupervisorHoraServidor = p.SupervisorHoraServidor,
        SsomaNombre = p.SsomaNombre,
        SsomaCargo = p.SsomaCargo,
        SsomaFirmaUrl = p.SsomaFirmaUrl,
        SsomaHoraServidor = p.SsomaHoraServidor,
        Estado = p.Estado,
        CierreHoraServidor = p.CierreHoraServidor,
        CierreObservaciones = p.CierreObservaciones,
        CierreFirmaUrl = p.CierreFirmaUrl,
        PdfHash = p.PdfHash,
        Respuestas = p.Respuestas.OrderBy(r => r.Orden).Select(r => new PetarItemRespuestaResponseDto
        {
            ItemId = r.ItemId,
            Texto = r.Texto,
            Respuesta = r.Respuesta,
        }).ToList(),
        IzajeGrua = MapIzajeGruaDto(p.IzajeGrua),
    };

    public async Task<PetarListResponseDto> Listar(PetarFiltroDto filtro)
    {
        using var ctx = _factory.CreateDbContext();

        var query = ctx.SsPetar
            .Include(p => p.Worker).ThenInclude(w => w!.Person)
            .Include(p => p.Proyecto)
            .Include(p => p.Tipo)
            .Include(p => p.Respuestas)
            .AsQueryable();

        if (filtro.ProyectoId.HasValue) query = query.Where(p => p.ProyectoId == filtro.ProyectoId);
        if (filtro.WorkerId.HasValue) query = query.Where(p => p.WorkerId == filtro.WorkerId);
        if (filtro.AtsId.HasValue) query = query.Where(p => p.AtsId == filtro.AtsId);
        if (filtro.FechaDesde.HasValue) query = query.Where(p => p.Fecha >= filtro.FechaDesde);
        if (filtro.FechaHasta.HasValue) query = query.Where(p => p.Fecha <= filtro.FechaHasta);
        if (!string.IsNullOrWhiteSpace(filtro.Estado)) query = query.Where(p => p.Estado == filtro.Estado);

        query = query.OrderByDescending(p => p.Fecha).ThenByDescending(p => p.Id);

        var total = await query.CountAsync();
        var page = Math.Max(1, filtro.Page);
        var data = await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();

        return new PetarListResponseDto
        {
            Data = data.Select(ToDto).ToList(),
            Page = page,
            PageSize = PageSize,
            TotalRecords = total,
            TotalPages = (int)Math.Ceiling(total / (double)PageSize),
        };
    }

    public async Task Firmar(
        SsPetar petar, string selfieUrl, string selfieHash, string firmaUrl, string firmaHash,
        DateTime horaServidor, PetarFirmarRequestDto body, string? ipOrigen, string? userAgent)
    {
        using var ctx = _factory.CreateDbContext();
        var entidad = await ctx.SsPetar.FirstOrDefaultAsync(p => p.Id == petar.Id)
            ?? throw new AbrilException("PETAR no encontrado.", 404);
        if (entidad.Estado != "Borrador")
            throw new AbrilException("Este PETAR ya fue firmado.", 409);

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
        entidad.UpdatedAt = DateTime.UtcNow;
        // El estado pasa a "Firmado" recién cuando Supervisor Y SSOMA también firmaron — ver
        // FirmarVisto, que es quien de verdad cierra el estado a "Firmado" (autorización a iniciar).

        await ctx.SaveChangesAsync();

        var hashAnterior = await GetUltimoHashAuditLog(petar.Id);
        var hash = ComputeHash(hashAnterior, "FirmadoEjecutante", petar.Id, entidad.WorkerId, firmaHash);
        ctx.SsPetarAuditLog.Add(new SsPetarAuditLog { PetarId = petar.Id, Evento = "FirmadoEjecutante", IpOrigen = ipOrigen, Detalle = $"firma_hash={firmaHash}", HashAnterior = hashAnterior, Hash = hash });
        await ctx.SaveChangesAsync();
    }

    public async Task FirmarVisto(int petarId, string rol, int workerId, string nombre, string? cargo, string firmaUrl, string firmaHash, DateTime horaServidor)
    {
        using var ctx = _factory.CreateDbContext();
        var petar = await ctx.SsPetar.FirstOrDefaultAsync(p => p.Id == petarId) ?? throw new AbrilException("PETAR no encontrado.", 404);

        if (petar.FirmaUrl == null)
            throw new AbrilException("El ejecutante debe firmar el PETAR antes de que se agreguen las demás firmas.", 409);
        if (petar.Estado != "Borrador")
            throw new AbrilException("Este PETAR ya está firmado/cerrado.", 409);

        if (rol == "Supervisor")
        {
            if (petar.SupervisorFirmaUrl != null)
                throw new AbrilException("Este PETAR ya tiene la firma del Supervisor.", 409);
            petar.SupervisorWorkerId = workerId;
            petar.SupervisorNombre = nombre;
            petar.SupervisorCargo = cargo;
            petar.SupervisorFirmaUrl = firmaUrl;
            petar.SupervisorFirmaHash = firmaHash;
            petar.SupervisorHoraServidor = horaServidor;
        }
        else
        {
            if (petar.SsomaFirmaUrl != null)
                throw new AbrilException("Este PETAR ya tiene el Visto Bueno de SSOMA.", 409);
            petar.SsomaWorkerId = workerId;
            petar.SsomaNombre = nombre;
            petar.SsomaCargo = cargo;
            petar.SsomaFirmaUrl = firmaUrl;
            petar.SsomaFirmaHash = firmaHash;
            petar.SsomaHoraServidor = horaServidor;
        }

        // Las 3 firmas completas = autorizado a iniciar el trabajo de alto riesgo.
        if (petar.SupervisorFirmaUrl != null && petar.SsomaFirmaUrl != null)
            petar.Estado = "Firmado";

        petar.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        var hashAnterior = await GetUltimoHashAuditLog(petarId);
        var hash = ComputeHash(hashAnterior, $"Firmado{rol}", petarId, workerId, firmaHash);
        ctx.SsPetarAuditLog.Add(new SsPetarAuditLog { PetarId = petarId, Evento = $"Firmado{rol}", Detalle = $"firma_hash={firmaHash}", HashAnterior = hashAnterior, Hash = hash });
        await ctx.SaveChangesAsync();
    }

    public async Task Cerrar(int petarId, int workerId, string firmaUrl, string firmaHash, string? observaciones, DateTime horaServidor)
    {
        using var ctx = _factory.CreateDbContext();
        var petar = await ctx.SsPetar.FirstOrDefaultAsync(p => p.Id == petarId) ?? throw new AbrilException("PETAR no encontrado.", 404);

        if (petar.Estado != "Firmado")
            throw new AbrilException("Solo se puede cerrar un PETAR con las 3 firmas completas (trabajo autorizado).", 409);
        if (petar.WorkerId != workerId)
            throw new AbrilException("Solo el ejecutante puede cerrar este PETAR.", 403);

        petar.Estado = "Cerrado";
        petar.CierreHoraServidor = horaServidor;
        petar.CierreObservaciones = observaciones;
        petar.CierreFirmaUrl = firmaUrl;
        petar.CierreFirmaHash = firmaHash;
        petar.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        var hashAnterior = await GetUltimoHashAuditLog(petarId);
        var hash = ComputeHash(hashAnterior, "Cerrado", petarId, workerId, firmaHash);
        ctx.SsPetarAuditLog.Add(new SsPetarAuditLog { PetarId = petarId, Evento = "Cerrado", Detalle = observaciones, HashAnterior = hashAnterior, Hash = hash });
        await ctx.SaveChangesAsync();
    }

    public async Task GuardarPdf(int id, string pdfUrl, string pdfHash)
    {
        using var ctx = _factory.CreateDbContext();
        var petar = await ctx.SsPetar.FirstOrDefaultAsync(p => p.Id == id) ?? throw new AbrilException("PETAR no encontrado.", 404);
        petar.PdfUrl = pdfUrl;
        petar.PdfHash = pdfHash;
        await ctx.SaveChangesAsync();

        var hashAnterior = await GetUltimoHashAuditLog(id);
        var hash = ComputeHash(hashAnterior, "PdfExportado", id, petar.WorkerId, pdfHash);
        ctx.SsPetarAuditLog.Add(new SsPetarAuditLog { PetarId = id, Evento = "PdfExportado", Detalle = $"pdf_hash={pdfHash}", HashAnterior = hashAnterior, Hash = hash });
        await ctx.SaveChangesAsync();
    }

    public async Task<string?> GetUltimoHashAuditLog(int petarId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsPetarAuditLog.Where(l => l.PetarId == petarId).OrderByDescending(l => l.Id).Select(l => l.Hash).FirstOrDefaultAsync();
    }

    public async Task AgregarAuditLog(int petarId, string evento, int? userId, string? ipOrigen, string? detalle, string hashAnterior, string hash)
    {
        using var ctx = _factory.CreateDbContext();
        ctx.SsPetarAuditLog.Add(new SsPetarAuditLog { PetarId = petarId, Evento = evento, UserId = userId, IpOrigen = ipOrigen, Detalle = detalle, HashAnterior = hashAnterior, Hash = hash });
        await ctx.SaveChangesAsync();
    }

    public async Task<bool> ExisteSelfieHash(string selfieHash)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsPetar.AnyAsync(p => p.SelfieHash == selfieHash);
    }

    private static TimeOnly? ParseHora(string? hhmm)
        => TimeOnly.TryParse(hhmm, out var t) ? t : null;

    private static string ComputeHash(string? hashAnterior, string evento, int petarId, int workerId, string? extra = null)
    {
        var payload = $"{hashAnterior}|{evento}|{petarId}|{workerId}|{DateTime.UtcNow:O}|{extra}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}
