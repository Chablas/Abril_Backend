# CONTEXT_OPT_backend.md — Módulo OPT (Observación Planeada de Tarea)
# Backend: .NET 10 Feature Slice dentro de SsomaModule
# Última actualización: 2026-06-14

---

## TAREA DE ESTA SESIÓN
Implementar el backend completo del módulo OPT dentro de `SsomaModule`.
Las tablas ya existen en BD (creadas vía pgAdmin — ver SQL abajo).
Solo generar código .NET, NO migraciones EF.

---

## UBICACIÓN EN EL PROYECTO

```
Features/SsomaModule/
  OptFeature/
    Application/
      Interfaces/IOptService.cs
      Interfaces/IOptRepository.cs
      Services/OptService.cs
      Services/IOptSharePointService.cs     <- igual que RacFeature
      Services/OptSharePointService.cs      <- igual que RacFeature
      Dtos/OptDtos.cs
    Infrastructure/
      Repositories/OptRepository.cs
      Models/OptModels.cs
    Presentation/
      OptController.cs
```

Registrar en `SsomaModule.cs` → `AddSsomaModule(IServiceCollection services)`.

---

## PATRÓN SHAREPOINT — IGUAL QUE RAC

Cada feature tiene su propio service que delega en `ISharePointHabService`.
NO usar `IGraphSharePointService` ni `ISharePointService` genérico — usar `ISharePointHabService`.
Firma confirmada: `SubirArchivoEnRutaAsync(Stream, string fileName, string libraryContexto, string carpetaPath)`

```csharp
// IOptSharePointService.cs
namespace Abril_Backend.Features.Ssoma.Opt.Services;
public interface IOptSharePointService
{
    Task<string> SubirFirmaObservadorAsync(Stream stream, string filename, int optId);
    Task<string> SubirFirmaTrabajadorAsync(Stream stream, string filename, int optId, int trabajadorId);
}

// OptSharePointService.cs
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
namespace Abril_Backend.Features.Ssoma.Opt.Services;
public class OptSharePointService : IOptSharePointService
{
    private readonly ISharePointHabService _sp;
    public OptSharePointService(ISharePointHabService sp) => _sp = sp;

    public Task<string> SubirFirmaObservadorAsync(Stream stream, string filename, int optId)
        => _sp.SubirArchivoEnRutaAsync(stream, filename, "opt-firmas", $"OPT/{optId}/firmas");

    public Task<string> SubirFirmaTrabajadorAsync(Stream stream, string filename, int optId, int trabajadorId)
        => _sp.SubirArchivoEnRutaAsync(stream, filename, "opt-firmas", $"OPT/{optId}/firmas");
}
```

**IMPORTANTE — `libraryContexto`**: El string "opt-firmas" debe estar mapeado en `SharePointHabService.cs`.
Antes de compilar, verificar en ese archivo cómo mapea los contextos (switch/dictionary).
Si no existe "opt-firmas", agregar la entrada con LibraryId `dff4c4a5-cb52-4f26-b299-913e4d7da663`.
Ver cómo RAC agregó "rac-firmas" y replicar el mismo patrón para "opt-firmas".

---

## SQL — EJECUTAR EN PGADMIN (en orden)

```sql
-- 1. PETs corporativos
CREATE TABLE ssoma_pet (
    id SERIAL PRIMARY KEY,
    nombre VARCHAR(300) NOT NULL,
    codigo VARCHAR(50),
    sharepoint_url VARCHAR(1000),       -- URL PDF en biblioteca PetsAbril
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT NOW()
);

-- 2. Criterios de verificación de entrenamiento (catálogo configurable)
CREATE TABLE ssoma_opt_criterio_verificacion (
    id SERIAL PRIMARY KEY,
    pregunta VARCHAR(500) NOT NULL,
    orden INT NOT NULL DEFAULT 0,
    activo BOOLEAN NOT NULL DEFAULT TRUE
);

-- Datos iniciales — 4 preguntas del PowerApp
INSERT INTO ssoma_opt_criterio_verificacion (pregunta, orden) VALUES
('¿El observado conoce y tiene la habilidad necesaria para realizar la tarea?', 1),
('¿El observado es consciente de realizar el procedimiento/PETS?', 2),
('¿El observado realizó correctamente el paso a paso? Si es SÍ, felicitar al trabajador.', 3),
('¿El observado requiere reinducción en los procedimientos?', 4);

-- 3. Cabecera OPT
CREATE TABLE ssoma_opt (
    id SERIAL PRIMARY KEY,
    proyecto_id INT NOT NULL REFERENCES project(project_id),
    pet_id INT REFERENCES ssoma_pet(id),
    fecha DATE NOT NULL,
    tipo_observacion VARCHAR(20) NOT NULL,  -- Inicial | Seguimiento
    cuenta_con_pet BOOLEAN NOT NULL DEFAULT FALSE,
    area VARCHAR(200),
    se_informa_trabajador BOOLEAN NOT NULL DEFAULT TRUE,
    -- Observador (PENDIENTE: migrar a app_user del logueado)
    observador_nombre VARCHAR(200),
    observador_cargo VARCHAR(200),
    firma_observador_url VARCHAR(1000),     -- SharePoint PetsAbril/firmas/
    -- Retroalimentación
    se_felicito BOOLEAN NOT NULL DEFAULT FALSE,
    se_recibieron_comentarios BOOLEAN NOT NULL DEFAULT FALSE,
    se_retroalimento BOOLEAN NOT NULL DEFAULT FALSE,
    se_obtuvo_compromiso BOOLEAN NOT NULL DEFAULT FALSE,
    -- Acción requerida (PENDIENTE Fase 2: generar actividad PASO + email coordinadores)
    accion_requerida VARCHAR(50),           -- ElaborarPETS | ModificarPETS | Entrenamiento | MantenerPETS | Ninguna
    accion_observacion TEXT,
    -- Score BBS calculado al guardar
    total_pasos INT NOT NULL DEFAULT 0,
    total_seguros INT NOT NULL DEFAULT 0,
    total_inseguros INT NOT NULL DEFAULT 0,
    score_pct DECIMAL(5,2),                -- (seguros / (seguros+inseguros)) * 100, NULL si no hay pasos evaluados
    estado VARCHAR(20) NOT NULL DEFAULT 'Completado',
    created_at TIMESTAMP DEFAULT NOW(),
    created_by INT
);

-- 4. Trabajadores observados (1 OPT → N trabajadores)
CREATE TABLE ssoma_opt_trabajador (
    id SERIAL PRIMARY KEY,
    opt_id INT NOT NULL REFERENCES ssoma_opt(id) ON DELETE CASCADE,
    trabajador_id INT NOT NULL REFERENCES workers(id),
    tipo_trabajador VARCHAR(50),            -- Normal | Sobresaliente | Nuevo | QueToma Riesgos | QueTiende Accidentarse | ProblemasHabilidad | Experimentado | PocoEficiente
    tiempo_en_obra VARCHAR(100),
    anios_experiencia VARCHAR(50),
    firma_trabajador_url VARCHAR(1000)      -- SharePoint PetsAbril/firmas/
);

-- 5. Verificación de entrenamiento por OPT (respuestas a criterios del catálogo)
CREATE TABLE ssoma_opt_verificacion (
    id SERIAL PRIMARY KEY,
    opt_id INT NOT NULL REFERENCES ssoma_opt(id) ON DELETE CASCADE,
    criterio_id INT NOT NULL REFERENCES ssoma_opt_criterio_verificacion(id),
    resultado BOOLEAN NOT NULL DEFAULT FALSE  -- TRUE=Sí, FALSE=No
);

-- 6. Pasos observados con scoring BBS
CREATE TABLE ssoma_opt_paso (
    id SERIAL PRIMARY KEY,
    opt_id INT NOT NULL REFERENCES ssoma_opt(id) ON DELETE CASCADE,
    numero_display VARCHAR(20) NOT NULL,    -- "1", "1.1", "1.2", "2", "2.1" — calculado en frontend
    descripcion TEXT NOT NULL,
    nivel INT NOT NULL DEFAULT 1,           -- 1=raíz, 2=sub-paso, 3=sub-sub-paso
    resultado VARCHAR(20),                  -- Seguro | Inseguro | N/A
    desviacion_observada TEXT,
    orden INT NOT NULL DEFAULT 0
);

-- Índices
CREATE INDEX idx_ssoma_opt_proyecto ON ssoma_opt(proyecto_id);
CREATE INDEX idx_ssoma_opt_fecha ON ssoma_opt(fecha);
CREATE INDEX idx_ssoma_opt_trabajador ON ssoma_opt_trabajador(trabajador_id);
CREATE INDEX idx_ssoma_opt_paso_opt ON ssoma_opt_paso(opt_id);
```

---

## MODELOS EF CORE

```csharp
// OptModels.cs
// IMPORTANTE: Workers usa [Column("...")] explícito — no snake_case automático
// Usar navigation properties con nombres exactos de la entidad Worker existente

public class SsomaOpt
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public int? PetId { get; set; }
    public DateTime Fecha { get; set; }
    public string TipoObservacion { get; set; } = string.Empty;
    public bool CuentaConPet { get; set; }
    public string? Area { get; set; }
    public bool SeInformaTrabajador { get; set; }
    public string? ObservadorNombre { get; set; }
    public string? ObservadorCargo { get; set; }
    public string? FirmaObservadorUrl { get; set; }
    public bool SeFelicito { get; set; }
    public bool SeRecibieronComentarios { get; set; }
    public bool SeRetroalimento { get; set; }
    public bool SeObtuvoCCompromiso { get; set; }
    public string? AccionRequerida { get; set; }
    public string? AccionObservacion { get; set; }
    public int TotalPasos { get; set; }
    public int TotalSeguros { get; set; }
    public int TotalInseguros { get; set; }
    public decimal? ScorePct { get; set; }
    public string Estado { get; set; } = "Completado";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedBy { get; set; }

    public Project? Proyecto { get; set; }
    public SsomaPet? Pet { get; set; }
    public ICollection<SsomaOptTrabajador> Trabajadores { get; set; } = [];
    public ICollection<SsomaOptVerificacion> Verificaciones { get; set; } = [];
    public ICollection<SsomaOptPaso> Pasos { get; set; } = [];
}

public class SsomaOptTrabajador
{
    public int Id { get; set; }
    public int OptId { get; set; }
    public int TrabajadorId { get; set; }
    public string? TipoTrabajador { get; set; }
    public string? TiempoEnObra { get; set; }
    public string? AniosExperiencia { get; set; }
    public string? FirmaTrabajadorUrl { get; set; }

    public SsomaOpt? Opt { get; set; }
    public Worker? Trabajador { get; set; }
    // Navegación: Worker.Person.FullName → nombre completo
    // Navegación: Worker.Person.DocumentIdentityCode → DNI
    // Navegación: Worker.Contributor.ContributorNombreComercial → empresa
}

public class SsomaPet
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Codigo { get; set; }
    public string? SharepointUrl { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SsomaOptCriterioVerificacion
{
    public int Id { get; set; }
    public string Pregunta { get; set; } = string.Empty;
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}

public class SsomaOptVerificacion
{
    public int Id { get; set; }
    public int OptId { get; set; }
    public int CriterioId { get; set; }
    public bool Resultado { get; set; }

    public SsomaOpt? Opt { get; set; }
    public SsomaOptCriterioVerificacion? Criterio { get; set; }
}

public class SsomaOptPaso
{
    public int Id { get; set; }
    public int OptId { get; set; }
    public string NumeroDisplay { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public int Nivel { get; set; } = 1;
    public string? Resultado { get; set; }   // Seguro | Inseguro | N/A
    public string? DesviacionObservada { get; set; }
    public int Orden { get; set; }

    public SsomaOpt? Opt { get; set; }
}
```

**Agregar DbSets en `Shared/Data/AppContext.cs`:**
```csharp
public DbSet<SsomaOpt> SsomaOpt { get; set; }
public DbSet<SsomaOptTrabajador> SsomaOptTrabajador { get; set; }
public DbSet<SsomaPet> SsomaPet { get; set; }
public DbSet<SsomaOptCriterioVerificacion> SsomaOptCriterioVerificacion { get; set; }
public DbSet<SsomaOptVerificacion> SsomaOptVerificacion { get; set; }
public DbSet<SsomaOptPaso> SsomaOptPaso { get; set; }
```

**Si hay colisión de nombres en PG, agregar en `ConfigurePostgreSQL`:**
```csharp
modelBuilder.Entity<SsomaPet>().ToTable("ssoma_pet");
modelBuilder.Entity<SsomaOptCriterioVerificacion>().ToTable("ssoma_opt_criterio_verificacion");
```

---

## DTOs

```csharp
// OptDtos.cs

// ── Catálogos ──────────────────────────────────────────────

public class OptPetDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Codigo { get; set; }
    public string? SharepointUrl { get; set; }
}

public class OptCriterioVerificacionDto
{
    public int Id { get; set; }
    public string Pregunta { get; set; } = string.Empty;
    public int Orden { get; set; }
}

// ── Crear OPT ──────────────────────────────────────────────

public class OptTrabajadorRequest
{
    public int TrabajadorId { get; set; }
    public string? TipoTrabajador { get; set; }
    public string? TiempoEnObra { get; set; }
    public string? AniosExperiencia { get; set; }
    public string? FirmaTrabajadorBase64 { get; set; }  // canvas → base64 → guardar en SP
}

public class OptVerificacionRequest
{
    public int CriterioId { get; set; }
    public bool Resultado { get; set; }
}

public class OptPasoRequest
{
    public string NumeroDisplay { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public int Nivel { get; set; } = 1;
    public string? Resultado { get; set; }
    public string? DesviacionObservada { get; set; }
    public int Orden { get; set; }
}

public class CrearOptRequest
{
    public int ProyectoId { get; set; }
    public int? PetId { get; set; }
    public DateTime Fecha { get; set; }
    public string TipoObservacion { get; set; } = string.Empty;
    public bool CuentaConPet { get; set; }
    public string? Area { get; set; }
    public bool SeInformaTrabajador { get; set; }
    public string? ObservadorNombre { get; set; }
    public string? ObservadorCargo { get; set; }
    public string? FirmaObservadorBase64 { get; set; }
    public bool SeFelicito { get; set; }
    public bool SeRecibieronComentarios { get; set; }
    public bool SeRetroalimento { get; set; }
    public bool SeObtuvoCCompromiso { get; set; }
    public string? AccionRequerida { get; set; }
    public string? AccionObservacion { get; set; }
    public List<OptTrabajadorRequest> Trabajadores { get; set; } = [];
    public List<OptVerificacionRequest> Verificaciones { get; set; } = [];
    public List<OptPasoRequest> Pasos { get; set; } = [];
}

// ── Respuestas ─────────────────────────────────────────────

public class OptTrabajadorDto
{
    public int Id { get; set; }
    public int TrabajadorId { get; set; }
    public string NombreTrabajador { get; set; } = string.Empty;
    public string? Dni { get; set; }
    public string? TipoTrabajador { get; set; }
    public string? TiempoEnObra { get; set; }
    public string? AniosExperiencia { get; set; }
    public string? FirmaTrabajadorUrl { get; set; }
}

public class OptVerificacionDto
{
    public int CriterioId { get; set; }
    public string Pregunta { get; set; } = string.Empty;
    public bool Resultado { get; set; }
}

public class OptPasoDto
{
    public int Id { get; set; }
    public string NumeroDisplay { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public int Nivel { get; set; }
    public string? Resultado { get; set; }
    public string? DesviacionObservada { get; set; }
    public int Orden { get; set; }
}

public class OptDetalleDto
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public string ProyectoNombre { get; set; } = string.Empty;
    public int? PetId { get; set; }
    public string? PetNombre { get; set; }
    public string? PetSharepointUrl { get; set; }
    public DateTime Fecha { get; set; }
    public string TipoObservacion { get; set; } = string.Empty;
    public bool CuentaConPet { get; set; }
    public string? Area { get; set; }
    public bool SeInformaTrabajador { get; set; }
    public string? ObservadorNombre { get; set; }
    public string? ObservadorCargo { get; set; }
    public string? FirmaObservadorUrl { get; set; }
    public bool SeFelicito { get; set; }
    public bool SeRecibieronComentarios { get; set; }
    public bool SeRetroalimento { get; set; }
    public bool SeObtuvoCCompromiso { get; set; }
    public string? AccionRequerida { get; set; }
    public string? AccionObservacion { get; set; }
    public int TotalPasos { get; set; }
    public int TotalSeguros { get; set; }
    public int TotalInseguros { get; set; }
    public decimal? ScorePct { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<OptTrabajadorDto> Trabajadores { get; set; } = [];
    public List<OptVerificacionDto> Verificaciones { get; set; } = [];
    public List<OptPasoDto> Pasos { get; set; } = [];
}

public class OptListItemDto
{
    public int Id { get; set; }
    public string ProyectoNombre { get; set; } = string.Empty;
    public string? PetNombre { get; set; }
    public DateTime Fecha { get; set; }
    public string TipoObservacion { get; set; } = string.Empty;
    public string? Area { get; set; }
    public string? ObservadorNombre { get; set; }
    // Primer trabajador como representante (puede haber N)
    public string TrabajadoresPrincipal { get; set; } = string.Empty;
    public int TotalTrabajadores { get; set; }
    public decimal? ScorePct { get; set; }
    public string? AccionRequerida { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// ── Dashboard ──────────────────────────────────────────────

public class OptDashboardDto
{
    public int TotalOpts { get; set; }
    public int TotalEsteMes { get; set; }
    public decimal? ScorePromedioGlobal { get; set; }
    public decimal? ScorePromedioEsteMes { get; set; }
    public int AccionesPendientes { get; set; }
    public List<OptScoreMensualDto> TendenciaMensual { get; set; } = [];
    public List<OptEmpresaRankingDto> RankingEmpresas { get; set; } = [];
    public List<OptTrabajadorRiesgoDto> TopTrabajadoresRiesgo { get; set; } = [];
    public List<OptAccionResumenDto> AccionesRequeridas { get; set; } = [];
}

public class OptScoreMensualDto
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public string MesNombre { get; set; } = string.Empty;
    public decimal? ScorePromedio { get; set; }
    public int TotalOpts { get; set; }
}

public class OptEmpresaRankingDto
{
    public int EmpresaId { get; set; }
    public string EmpresaNombre { get; set; } = string.Empty;
    public decimal? ScorePromedio { get; set; }
    public int TotalOpts { get; set; }
}

public class OptTrabajadorRiesgoDto
{
    public int TrabajadorId { get; set; }
    public string NombreTrabajador { get; set; } = string.Empty;
    public string? Empresa { get; set; }
    public decimal? ScorePromedio { get; set; }
    public int TotalOpts { get; set; }
    public int TotalInseguros { get; set; }
}

public class OptAccionResumenDto
{
    public string TipoAccion { get; set; } = string.Empty;
    public int Cantidad { get; set; }
}
```

---

## INTERFACES

```csharp
// IOptRepository.cs
public interface IOptRepository
{
    Task<List<OptListItemDto>> GetListAsync(int? proyectoId, int? petId, string? tipoObservacion,
        DateTime? fechaDesde, DateTime? fechaHasta, int? trabajadorId, int page, int pageSize);
    Task<int> GetListCountAsync(int? proyectoId, int? petId, string? tipoObservacion,
        DateTime? fechaDesde, DateTime? fechaHasta, int? trabajadorId);
    Task<OptDetalleDto?> GetDetalleAsync(int id);
    Task<int> CrearOptAsync(CrearOptRequest request, string? firmaObservadorUrl,
        Dictionary<int, string> firmasTrabajadorUrls);
    Task<OptDashboardDto> GetDashboardAsync(int? proyectoId, int? anio);
    Task<List<OptPetDto>> GetPetsAsync();
    Task<List<OptCriterioVerificacionDto>> GetCriteriosVerificacionAsync();
}

// IOptService.cs
public interface IOptService
{
    Task<object> GetListAsync(int? proyectoId, int? petId, string? tipoObservacion,
        DateTime? fechaDesde, DateTime? fechaHasta, int? trabajadorId, int page, int pageSize);
    Task<OptDetalleDto> GetDetalleAsync(int id);
    Task<int> CrearOptAsync(CrearOptRequest request);
    Task<OptDashboardDto> GetDashboardAsync(int? proyectoId, int? anio);
    Task<List<OptPetDto>> GetPetsAsync();
    Task<List<OptCriterioVerificacionDto>> GetCriteriosVerificacionAsync();
}
```

---

## REPOSITORIO

```csharp
// OptRepository.cs
public class OptRepository : IOptRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly ISharePointService _sharePoint;
    private readonly IConfiguration _configuration;

    // SharePoint: sitio SSOMA-Powerapps, biblioteca PETSAbril2026
    // SiteId: d9e26806-d535-4353-9610-195978e20390
    // PetsAbrilLibraryId: dff4c4a5-cb52-4f26-b299-913e4d7da663
    // Ruta firmas: PETSAbril2026/Firmas/{optId}_observador.png
    // Ruta firmas trabajador: PETSAbril2026/Firmas/{optId}_trabajador_{trabajadorId}.png

    private const string LibraryId = "dff4c4a5-cb52-4f26-b299-913e4d7da663";

    public OptRepository(IDbContextFactory<AppDbContext> factory,
        ISharePointService sharePoint, IConfiguration configuration)
    {
        _factory = factory;
        _sharePoint = sharePoint;
        _configuration = configuration;
    }

    public async Task<List<OptPetDto>> GetPetsAsync()
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsomaPet
            .Where(p => p.Activo)
            .OrderBy(p => p.Nombre)
            .Select(p => new OptPetDto
            {
                Id = p.Id,
                Nombre = p.Nombre,
                Codigo = p.Codigo,
                SharepointUrl = p.SharepointUrl
            })
            .ToListAsync();
    }

    public async Task<List<OptCriterioVerificacionDto>> GetCriteriosVerificacionAsync()
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsomaOptCriterioVerificacion
            .Where(c => c.Activo)
            .OrderBy(c => c.Orden)
            .Select(c => new OptCriterioVerificacionDto
            {
                Id = c.Id,
                Pregunta = c.Pregunta,
                Orden = c.Orden
            })
            .ToListAsync();
    }

    public async Task<int> CrearOptAsync(CrearOptRequest request, string? firmaObservadorUrl,
        Dictionary<int, string> firmasTrabajadorUrls)
    {
        using var ctx = _factory.CreateDbContext();

        // Calcular score BBS desde los pasos
        var pasosEvaluados = request.Pasos
            .Where(p => p.Resultado == "Seguro" || p.Resultado == "Inseguro")
            .ToList();
        var totalSeguros = pasosEvaluados.Count(p => p.Resultado == "Seguro");
        var totalInseguros = pasosEvaluados.Count(p => p.Resultado == "Inseguro");
        var totalEvaluados = totalSeguros + totalInseguros;
        decimal? scorePct = totalEvaluados > 0
            ? Math.Round((decimal)totalSeguros / totalEvaluados * 100, 2)
            : null;

        var opt = new SsomaOpt
        {
            ProyectoId = request.ProyectoId,
            PetId = request.PetId,
            Fecha = request.Fecha.Date,
            TipoObservacion = request.TipoObservacion,
            CuentaConPet = request.CuentaConPet,
            Area = request.Area,
            SeInformaTrabajador = request.SeInformaTrabajador,
            ObservadorNombre = request.ObservadorNombre,
            ObservadorCargo = request.ObservadorCargo,
            FirmaObservadorUrl = firmaObservadorUrl,
            SeFelicito = request.SeFelicito,
            SeRecibieronComentarios = request.SeRecibieronComentarios,
            SeRetroalimento = request.SeRetroalimento,
            SeObtuvoCCompromiso = request.SeObtuvoCCompromiso,
            AccionRequerida = request.AccionRequerida,
            AccionObservacion = request.AccionObservacion,
            TotalPasos = request.Pasos.Count,
            TotalSeguros = totalSeguros,
            TotalInseguros = totalInseguros,
            ScorePct = scorePct,
            Estado = "Completado",
            CreatedAt = DateTime.UtcNow
        };

        ctx.SsomaOpt.Add(opt);
        await ctx.SaveChangesAsync();

        // Trabajadores
        foreach (var t in request.Trabajadores)
        {
            var ot = new SsomaOptTrabajador
            {
                OptId = opt.Id,
                TrabajadorId = t.TrabajadorId,
                TipoTrabajador = t.TipoTrabajador,
                TiempoEnObra = t.TiempoEnObra,
                AniosExperiencia = t.AniosExperiencia,
                FirmaTrabajadorUrl = firmasTrabajadorUrls.TryGetValue(t.TrabajadorId, out var url) ? url : null
            };
            ctx.SsomaOptTrabajador.Add(ot);
        }

        // Verificaciones
        foreach (var v in request.Verificaciones)
        {
            ctx.SsomaOptVerificacion.Add(new SsomaOptVerificacion
            {
                OptId = opt.Id,
                CriterioId = v.CriterioId,
                Resultado = v.Resultado
            });
        }

        // Pasos
        foreach (var p in request.Pasos)
        {
            ctx.SsomaOptPaso.Add(new SsomaOptPaso
            {
                OptId = opt.Id,
                NumeroDisplay = p.NumeroDisplay,
                Descripcion = p.Descripcion,
                Nivel = p.Nivel,
                Resultado = p.Resultado,
                DesviacionObservada = p.DesviacionObservada,
                Orden = p.Orden
            });
        }

        await ctx.SaveChangesAsync();
        return opt.Id;
    }

    public async Task<OptDetalleDto?> GetDetalleAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var opt = await ctx.SsomaOpt
            .Include(o => o.Proyecto)
            .Include(o => o.Pet)
            .Include(o => o.Trabajadores).ThenInclude(t => t.Trabajador).ThenInclude(w => w!.Person)   // Person para nombre + DNI
            .Include(o => o.Trabajadores).ThenInclude(t => t.Trabajador).ThenInclude(w => w!.Contributor) // Contributor para empresa
            .Include(o => o.Verificaciones).ThenInclude(v => v.Criterio)
            .Include(o => o.Pasos)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (opt == null) return null;

        return new OptDetalleDto
        {
            Id = opt.Id,
            ProyectoId = opt.ProyectoId,
            ProyectoNombre = opt.Proyecto?.ProjectDescription ?? "",
            PetId = opt.PetId,
            PetNombre = opt.Pet?.Nombre,
            PetSharepointUrl = opt.Pet?.SharepointUrl,
            Fecha = opt.Fecha,
            TipoObservacion = opt.TipoObservacion,
            CuentaConPet = opt.CuentaConPet,
            Area = opt.Area,
            SeInformaTrabajador = opt.SeInformaTrabajador,
            ObservadorNombre = opt.ObservadorNombre,
            ObservadorCargo = opt.ObservadorCargo,
            FirmaObservadorUrl = opt.FirmaObservadorUrl,
            SeFelicito = opt.SeFelicito,
            SeRecibieronComentarios = opt.SeRecibieronComentarios,
            SeRetroalimento = opt.SeRetroalimento,
            SeObtuvoCCompromiso = opt.SeObtuvoCCompromiso,
            AccionRequerida = opt.AccionRequerida,
            AccionObservacion = opt.AccionObservacion,
            TotalPasos = opt.TotalPasos,
            TotalSeguros = opt.TotalSeguros,
            TotalInseguros = opt.TotalInseguros,
            ScorePct = opt.ScorePct,
            Estado = opt.Estado,
            CreatedAt = opt.CreatedAt,
            Trabajadores = opt.Trabajadores.Select(t => new OptTrabajadorDto
            {
                Id = t.Id,
                TrabajadorId = t.TrabajadorId,
                NombreTrabajador = t.Trabajador?.Person?.FullName
                    ?? $"{t.Trabajador?.Person?.FirstName} {t.Trabajador?.Person?.FirstLastName}".Trim(),
                Dni = t.Trabajador?.Person?.DocumentIdentityCode,
                TipoTrabajador = t.TipoTrabajador,
                TiempoEnObra = t.TiempoEnObra,
                AniosExperiencia = t.AniosExperiencia,
                FirmaTrabajadorUrl = t.FirmaTrabajadorUrl
            }).ToList(),
            Verificaciones = opt.Verificaciones
                .OrderBy(v => v.Criterio?.Orden)
                .Select(v => new OptVerificacionDto
                {
                    CriterioId = v.CriterioId,
                    Pregunta = v.Criterio?.Pregunta ?? "",
                    Resultado = v.Resultado
                }).ToList(),
            Pasos = opt.Pasos
                .OrderBy(p => p.Orden)
                .Select(p => new OptPasoDto
                {
                    Id = p.Id,
                    NumeroDisplay = p.NumeroDisplay,
                    Descripcion = p.Descripcion,
                    Nivel = p.Nivel,
                    Resultado = p.Resultado,
                    DesviacionObservada = p.DesviacionObservada,
                    Orden = p.Orden
                }).ToList()
        };
    }

    public async Task<List<OptListItemDto>> GetListAsync(int? proyectoId, int? petId,
        string? tipoObservacion, DateTime? fechaDesde, DateTime? fechaHasta,
        int? trabajadorId, int page, int pageSize)
    {
        using var ctx = _factory.CreateDbContext();
        var q = ctx.SsomaOpt
            .Include(o => o.Proyecto)
            .Include(o => o.Pet)
            .Include(o => o.Trabajadores).ThenInclude(t => t.Trabajador)
            .AsQueryable();

        if (proyectoId.HasValue) q = q.Where(o => o.ProyectoId == proyectoId.Value);
        if (petId.HasValue) q = q.Where(o => o.PetId == petId.Value);
        if (!string.IsNullOrEmpty(tipoObservacion)) q = q.Where(o => o.TipoObservacion == tipoObservacion);
        if (fechaDesde.HasValue) q = q.Where(o => o.Fecha >= fechaDesde.Value.Date);
        if (fechaHasta.HasValue) q = q.Where(o => o.Fecha <= fechaHasta.Value.Date);
        if (trabajadorId.HasValue) q = q.Where(o => o.Trabajadores.Any(t => t.TrabajadorId == trabajadorId.Value));

        return await q
            .OrderByDescending(o => o.Fecha)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OptListItemDto
            {
                Id = o.Id,
                ProyectoNombre = o.Proyecto != null ? o.Proyecto.ProjectDescription : "",
                PetNombre = o.Pet != null ? o.Pet.Nombre : null,
                Fecha = o.Fecha,
                TipoObservacion = o.TipoObservacion,
                Area = o.Area,
                ObservadorNombre = o.ObservadorNombre,
                TrabajadoresPrincipal = o.Trabajadores
                    .OrderBy(t => t.Id)
                    .Select(t => t.Trabajador != null && t.Trabajador.Person != null
                        ? t.Trabajador.Person.FullName
                          ?? $"{t.Trabajador.Person.FirstName} {t.Trabajador.Person.FirstLastName}".Trim()
                        : "")
                    .FirstOrDefault() ?? "",
                TotalTrabajadores = o.Trabajadores.Count,
                ScorePct = o.ScorePct,
                AccionRequerida = o.AccionRequerida,
                Estado = o.Estado,
                CreatedAt = o.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<int> GetListCountAsync(int? proyectoId, int? petId,
        string? tipoObservacion, DateTime? fechaDesde, DateTime? fechaHasta, int? trabajadorId)
    {
        using var ctx = _factory.CreateDbContext();
        var q = ctx.SsomaOpt.AsQueryable();
        if (proyectoId.HasValue) q = q.Where(o => o.ProyectoId == proyectoId.Value);
        if (petId.HasValue) q = q.Where(o => o.PetId == petId.Value);
        if (!string.IsNullOrEmpty(tipoObservacion)) q = q.Where(o => o.TipoObservacion == tipoObservacion);
        if (fechaDesde.HasValue) q = q.Where(o => o.Fecha >= fechaDesde.Value.Date);
        if (fechaHasta.HasValue) q = q.Where(o => o.Fecha <= fechaHasta.Value.Date);
        if (trabajadorId.HasValue) q = q.Where(o => o.Trabajadores.Any(t => t.TrabajadorId == trabajadorId.Value));
        return await q.CountAsync();
    }

    public async Task<OptDashboardDto> GetDashboardAsync(int? proyectoId, int? anio)
    {
        using var ctx = _factory.CreateDbContext();
        var anioFiltro = anio ?? DateTime.Now.Year;
        var mesActual = DateTime.Now.Month;

        var q = ctx.SsomaOpt.AsQueryable();
        if (proyectoId.HasValue) q = q.Where(o => o.ProyectoId == proyectoId.Value);

        var all = await q.Include(o => o.Trabajadores).ThenInclude(t => t.Trabajador)
            .ThenInclude(w => w!.Contributor)
            .ToListAsync();

        var delAnio = all.Where(o => o.Fecha.Year == anioFiltro).ToList();
        var delMes = delAnio.Where(o => o.Fecha.Month == mesActual).ToList();

        // Tendencia mensual (12 meses del año)
        var tendencia = Enumerable.Range(1, 12).Select(m =>
        {
            var opts = delAnio.Where(o => o.Fecha.Month == m).ToList();
            return new OptScoreMensualDto
            {
                Anio = anioFiltro,
                Mes = m,
                MesNombre = new DateTime(anioFiltro, m, 1).ToString("MMM", new System.Globalization.CultureInfo("es-PE")),
                ScorePromedio = opts.Any(o => o.ScorePct.HasValue)
                    ? Math.Round(opts.Where(o => o.ScorePct.HasValue).Average(o => o.ScorePct!.Value), 1)
                    : null,
                TotalOpts = opts.Count
            };
        }).ToList();

        // Ranking empresas
        var empresas = delAnio
            .SelectMany(o => o.Trabajadores.Select(t => new
            {
                o.ScorePct,
                EmpresaId = t.Trabajador?.ContributorId ?? 0,
                EmpresaNombre = t.Trabajador?.Contributor?.ContributorNombreComercial ?? "Sin empresa"
            }))
            .Where(x => x.EmpresaId > 0)
            .GroupBy(x => new { x.EmpresaId, x.EmpresaNombre })
            .Select(g => new OptEmpresaRankingDto
            {
                EmpresaId = g.Key.EmpresaId,
                EmpresaNombre = g.Key.EmpresaNombre,
                ScorePromedio = g.Any(x => x.ScorePct.HasValue)
                    ? Math.Round(g.Where(x => x.ScorePct.HasValue).Average(x => x.ScorePct!.Value), 1)
                    : null,
                TotalOpts = g.Select(x => x.ScorePct).Distinct().Count()
            })
            .OrderBy(e => e.ScorePromedio)
            .Take(10)
            .ToList();

        // Top trabajadores en riesgo (menor score)
        var trabajadoresRiesgo = delAnio
            .SelectMany(o => o.Trabajadores.Select(t => new
            {
                t.TrabajadorId,
                Nombre = t.Trabajador?.Person?.FullName
                    ?? $"{t.Trabajador?.Person?.FirstName} {t.Trabajador?.Person?.FirstLastName}".Trim(),
                Empresa = t.Trabajador?.Contributor?.ContributorNombreComercial,
                o.ScorePct,
                o.TotalInseguros
            }))
            .GroupBy(x => new { x.TrabajadorId, x.Nombre, x.Empresa })
            .Select(g => new OptTrabajadorRiesgoDto
            {
                TrabajadorId = g.Key.TrabajadorId,
                NombreTrabajador = g.Key.Nombre,
                Empresa = g.Key.Empresa,
                ScorePromedio = g.Any(x => x.ScorePct.HasValue)
                    ? Math.Round(g.Where(x => x.ScorePct.HasValue).Average(x => x.ScorePct!.Value), 1)
                    : null,
                TotalOpts = g.Count(),
                TotalInseguros = g.Sum(x => x.TotalInseguros)
            })
            .Where(t => t.ScorePromedio.HasValue && t.ScorePromedio < 80)
            .OrderBy(t => t.ScorePromedio)
            .Take(10)
            .ToList();

        // Acciones requeridas
        var acciones = all
            .Where(o => !string.IsNullOrEmpty(o.AccionRequerida) && o.AccionRequerida != "Ninguna")
            .GroupBy(o => o.AccionRequerida!)
            .Select(g => new OptAccionResumenDto
            {
                TipoAccion = g.Key,
                Cantidad = g.Count()
            })
            .ToList();

        return new OptDashboardDto
        {
            TotalOpts = all.Count,
            TotalEsteMes = delMes.Count,
            ScorePromedioGlobal = all.Any(o => o.ScorePct.HasValue)
                ? Math.Round(all.Where(o => o.ScorePct.HasValue).Average(o => o.ScorePct!.Value), 1)
                : null,
            ScorePromedioEsteMes = delMes.Any(o => o.ScorePct.HasValue)
                ? Math.Round(delMes.Where(o => o.ScorePct.HasValue).Average(o => o.ScorePct!.Value), 1)
                : null,
            AccionesPendientes = acciones.Sum(a => a.Cantidad),
            TendenciaMensual = tendencia,
            RankingEmpresas = empresas,
            TopTrabajadoresRiesgo = trabajadoresRiesgo,
            AccionesRequeridas = acciones
        };
    }
}
```

---

## SERVICIO

```csharp
// OptService.cs
public class OptService : IOptService
{
    private readonly IOptRepository _repo;
    private readonly IOptSharePointService _sp;

    public OptService(IOptRepository repo, IOptSharePointService sp)
    {
        _repo = repo;
        _sp = sp;
    }

    public async Task<List<OptPetDto>> GetPetsAsync() => await _repo.GetPetsAsync();

    public async Task<List<OptCriterioVerificacionDto>> GetCriteriosVerificacionAsync()
        => await _repo.GetCriteriosVerificacionAsync();

    public async Task<OptDetalleDto> GetDetalleAsync(int id)
    {
        var result = await _repo.GetDetalleAsync(id);
        if (result == null) throw new AbrilException(404, "OPT no encontrada.");
        return result;
    }

    public async Task<object> GetListAsync(int? proyectoId, int? petId, string? tipoObservacion,
        DateTime? fechaDesde, DateTime? fechaHasta, int? trabajadorId, int page, int pageSize)
    {
        var items = await _repo.GetListAsync(proyectoId, petId, tipoObservacion,
            fechaDesde, fechaHasta, trabajadorId, page, pageSize);
        var total = await _repo.GetListCountAsync(proyectoId, petId, tipoObservacion,
            fechaDesde, fechaHasta, trabajadorId);
        return new { items, total, page, pageSize };
    }

    public async Task<OptDashboardDto> GetDashboardAsync(int? proyectoId, int? anio)
        => await _repo.GetDashboardAsync(proyectoId, anio);

    public async Task<int> CrearOptAsync(CrearOptRequest request)
    {
        string? firmaObservadorUrl = null;
        if (!string.IsNullOrEmpty(request.FirmaObservadorBase64))
        {
            var base64 = request.FirmaObservadorBase64.Contains(",")
                ? request.FirmaObservadorBase64.Split(',')[1]
                : request.FirmaObservadorBase64;
            var bytes = Convert.FromBase64String(base64);
            using var stream = new MemoryStream(bytes);
            firmaObservadorUrl = await _sp.SubirFirmaObservadorAsync(
                stream, $"obs_{DateTime.UtcNow:yyyyMMddHHmmss}.png", 0);
        }

        var firmasTrabajadorUrls = new Dictionary<int, string>();
        foreach (var t in request.Trabajadores.Where(t => !string.IsNullOrEmpty(t.FirmaTrabajadorBase64)))
        {
            var base64 = t.FirmaTrabajadorBase64!.Contains(",")
                ? t.FirmaTrabajadorBase64.Split(',')[1]
                : t.FirmaTrabajadorBase64;
            var bytes = Convert.FromBase64String(base64);
            using var stream = new MemoryStream(bytes);
            var url = await _sp.SubirFirmaTrabajadorAsync(
                stream, $"trab_{t.TrabajadorId}_{DateTime.UtcNow:yyyyMMddHHmmss}.png", 0, t.TrabajadorId);
            firmasTrabajadorUrls[t.TrabajadorId] = url;
        }

        return await _repo.CrearOptAsync(request, firmaObservadorUrl, firmasTrabajadorUrls);
    }
}
```

---

## CONTROLLER

```csharp
// OptController.cs
[ApiController]
[Route("api/v1/ssoma-opt")]
[Authorize]
public class OptController : ControllerBase
{
    private readonly IOptService _service;
    private readonly ILogger<OptController> _logger;

    public OptController(IOptService service, ILogger<OptController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // GET api/v1/ssoma-opt/catalogos
    [HttpGet("catalogos")]
    public async Task<IActionResult> GetCatalogos()
    {
        try
        {
            var pets = await _service.GetPetsAsync();
            var criterios = await _service.GetCriteriosVerificacionAsync();
            return Ok(new { pets, criterios });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error al obtener catálogos OPT"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    // GET api/v1/ssoma-opt?proyectoId=1&page=1&pageSize=20
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int? proyectoId, [FromQuery] int? petId,
        [FromQuery] string? tipoObservacion,
        [FromQuery] DateTime? fechaDesde, [FromQuery] DateTime? fechaHasta,
        [FromQuery] int? trabajadorId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var result = await _service.GetListAsync(proyectoId, petId, tipoObservacion,
                fechaDesde, fechaHasta, trabajadorId, page, pageSize);
            return Ok(result);
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error al obtener lista OPT"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    // GET api/v1/ssoma-opt/dashboard?proyectoId=1&anio=2026
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] int? proyectoId, [FromQuery] int? anio)
    {
        try
        {
            var result = await _service.GetDashboardAsync(proyectoId, anio);
            return Ok(result);
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error al obtener dashboard OPT"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    // GET api/v1/ssoma-opt/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetalle(int id)
    {
        try
        {
            var result = await _service.GetDetalleAsync(id);
            return Ok(result);
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error al obtener OPT {Id}", id); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    // POST api/v1/ssoma-opt
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearOptRequest request)
    {
        try
        {
            if (request.Trabajadores.Count == 0)
                return BadRequest(new { message = "Debe incluir al menos un trabajador observado." });
            if (string.IsNullOrEmpty(request.TipoObservacion))
                return BadRequest(new { message = "El tipo de observación es requerido." });

            var id = await _service.CrearOptAsync(request);
            return Ok(new { id, message = "OPT registrada correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error al crear OPT"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }
}
```

---

## REGISTRO EN SsomaModule.cs

```csharp
// Agregar dentro de AddSsomaModule():
services.AddScoped<IOptSharePointService, OptSharePointService>();
services.AddScoped<IOptRepository, OptRepository>();
services.AddScoped<IOptService, OptService>();
```

**ANTES de compilar — verificar en `SharePointHabService.cs`:**
Buscar el switch/dictionary que mapea `libraryContexto` → LibraryId.
Agregar la entrada `"opt-firmas"` → `"dff4c4a5-cb52-4f26-b299-913e4d7da663"` si no existe.
Ver cómo RAC agregó `"rac-firmas"` y replicar exactamente.

---

## PENDIENTES REGISTRADOS

1. **`observador_id`** — actualmente campo libre (`observador_nombre` + `observador_cargo`). Migrar a `app_user` del usuario logueado en sprint siguiente.
2. **`PetsAbrilLibraryId`** — ✅ confirmado: `dff4c4a5-cb52-4f26-b299-913e4d7da663` (biblioteca `PETSAbril2026`).
3. **Acción requerida → Fase 2** — cuando `accion_requerida != null && != "Ninguna"`: generar actividad en PASO del proyecto del mes actual + enviar email a coordinadores SSOMA.
4. **PETs con pasos** — Fase 2: tabla `ssoma_pet_paso` con pasos por PET. Al seleccionar PET en nueva OPT → auto-cargar pasos.

---

## VERIFICACIÓN ANTES DE COMPILAR

- [ ] SQL ejecutado en pgAdmin sin errores
// DATOS CONFIRMADOS AL 100%:
// Project.ProjectDescription → columna "project_description" ✅
// Project.EmailCoordSsoma → columna "email_coord_ssoma" ✅ (usar en Fase 2 para notificaciones)
// Worker.Person.FullName → "full_name" ✅
// Worker.Person.DocumentIdentityCode → "document_identity_code" ✅
// Worker.Person.FirstName + FirstLastName → fallback nombre ✅
// Worker.Contributor.ContributorNombreComercial ✅
// Worker.Person nav property ✅ | Worker.Contributor nav property ✅
- [ ] DbSets agregados en `AppContext.cs`
- [ ] `SsomaModule.cs` registra `IOptRepository` → `OptRepository` y `IOptService` → `OptService`
- [ ] `Worker.Person.FullName` — nav property `Worker → Person` con `PersonId`. Nombre completo en `full_name`. Fallback: `first_name + first_last_name`
- [ ] `Worker.Person.DocumentIdentityCode` — DNI en `document_identity_code`
- [ ] `Worker.ContributorId` — FK directa en `workers.contributor_id`
- [ ] `Worker.Contributor.ContributorNombreComercial` — nav property `Worker → Contributor`, columna `contributor_nombre_comercial`
- [ ] Verificar que `Worker` tiene nav property `Person?` (agregada 2026-05-11 según CONTEXT backend)
- [ ] `PetsAbrilLibraryId` reemplazado antes de subir a producción
