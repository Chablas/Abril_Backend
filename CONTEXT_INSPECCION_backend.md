# CONTEXT_INSPECCION_backend.md — Módulo Inspecciones
# Backend: .NET 10 Feature Slice dentro de SsomaModule
# Última actualización: 2026-06-15

---

## TAREA DE ESTA SESIÓN
Implementar el backend completo del módulo Inspecciones dentro de `SsomaModule`.
Ejecutar SQL en pgAdmin primero, luego crear archivos .NET.
NO crear migraciones EF.

---

## DATOS CONFIRMADOS (verificados contra BD real)

- `Worker.Person.FullName` → nav property confirmada
- `Worker.Person.DocumentIdentityCode` → DNI confirmado
- `Worker.Contributor.ContributorNombreComercial` → confirmado
- `Project.ProjectDescription` → confirmado
- `Project.EmailCoordSsoma` → confirmado (Fase 2 notificaciones)
- SharePoint SiteId: `d9e26806-d535-4353-9610-195978e20390`
- SharePoint LibraryId InspeccionesAbril2026: `e745ab2c-2b39-437e-9139-90e47cdcb43b`
- Patrón SharePoint: `ISharePointHabService.SubirArchivoEnRutaAsync(Stream, fileName, libraryContexto, carpetaPath)`
- libraryContexto "inspeccion-fotos" e "inspeccion-firmas" → agregar en SharePointHabService.cs igual que "opt-firmas"
- Tabla `app_user` confirmada (existe en BD)
- Tablas ssoma_ existentes: solo ssoma_rac*, ssoma_paso* — inspecciones va desde cero

---

## UBICACIÓN EN EL PROYECTO

```
Features/SsomaModule/
  InspeccionFeature/
    Application/
      Interfaces/IInspeccionService.cs
      Interfaces/IInspeccionRepository.cs
      Services/IInspeccionSharePointService.cs
      Services/InspeccionSharePointService.cs
      Services/InspeccionService.cs
      Dtos/InspeccionDtos.cs
    Infrastructure/
      Repositories/InspeccionRepository.cs
      Models/InspeccionModels.cs
    Presentation/
      InspeccionController.cs
```

Registrar en `SsomaModule.cs` → `AddSsomaModule(IServiceCollection services)`.

---

## PATRÓN SHAREPOINT — IGUAL QUE OPT/RAC

```csharp
// IInspeccionSharePointService.cs
namespace Abril_Backend.Features.Ssoma.Inspeccion.Services;
public interface IInspeccionSharePointService
{
    Task<string> SubirFotoHallazgoAsync(Stream stream, string filename, int inspeccionId, int hallazgoId);
    Task<string> SubirFirmaInspectorAsync(Stream stream, string filename, int inspeccionId);
    Task<string> SubirFirmaRepresentanteAsync(Stream stream, string filename, int inspeccionId);
}

// InspeccionSharePointService.cs
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
namespace Abril_Backend.Features.Ssoma.Inspeccion.Services;
public class InspeccionSharePointService : IInspeccionSharePointService
{
    private readonly ISharePointHabService _sp;
    public InspeccionSharePointService(ISharePointHabService sp) => _sp = sp;

    public Task<string> SubirFotoHallazgoAsync(Stream stream, string filename, int inspeccionId, int hallazgoId)
        => _sp.SubirArchivoEnRutaAsync(stream, filename, "inspeccion-fotos", $"Inspecciones/{inspeccionId}/hallazgos/{hallazgoId}");

    public Task<string> SubirFirmaInspectorAsync(Stream stream, string filename, int inspeccionId)
        => _sp.SubirArchivoEnRutaAsync(stream, filename, "inspeccion-firmas", $"Inspecciones/{inspeccionId}/firmas");

    public Task<string> SubirFirmaRepresentanteAsync(Stream stream, string filename, int inspeccionId)
        => _sp.SubirArchivoEnRutaAsync(stream, filename, "inspeccion-firmas", $"Inspecciones/{inspeccionId}/firmas");
}
```

**IMPORTANTE:** Agregar en `SharePointHabService.cs` las entradas:
- `"inspeccion-fotos"` → `"e745ab2c-2b39-437e-9139-90e47cdcb43b"`
- `"inspeccion-firmas"` → `"e745ab2c-2b39-437e-9139-90e47cdcb43b"`

Igual que se agregó `"opt-firmas"` en la sesión anterior.

---

## SQL — EJECUTAR EN PGADMIN (en orden estricto)

```sql
-- 1. Tipos de inspección
CREATE TABLE ssoma_inspeccion_tipo (
    id SERIAL PRIMARY KEY,
    nombre VARCHAR(150) NOT NULL,
    ambito VARCHAR(20) NOT NULL DEFAULT 'Seguridad', -- Seguridad | Salud | Ambiente
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT NOW()
);

-- 2. Items del checklist por tipo
CREATE TABLE ssoma_inspeccion_checklist_item (
    id SERIAL PRIMARY KEY,
    tipo_id INT NOT NULL REFERENCES ssoma_inspeccion_tipo(id),
    pregunta VARCHAR(500) NOT NULL,
    categoria VARCHAR(150),  -- subtítulo agrupador dentro del checklist
    orden INT NOT NULL DEFAULT 0,
    activo BOOLEAN NOT NULL DEFAULT TRUE
);

-- 3. Cabecera de inspección
CREATE TABLE ssoma_inspeccion (
    id SERIAL PRIMARY KEY,
    proyecto_id INT NOT NULL REFERENCES project(project_id),
    tipo_id INT NOT NULL REFERENCES ssoma_inspeccion_tipo(id),
    empresa_id INT REFERENCES contributor(contributor_id),
    es_planificada BOOLEAN NOT NULL DEFAULT TRUE,
    fecha DATE NOT NULL,
    hora_inicio TIME,
    hora_fin TIME,
    area VARCHAR(200),
    responsable_area VARCHAR(200),          -- nombre libre (del PowerApp)
    -- Inspector — nombre libre (PENDIENTE: migrar a app_user logueado)
    inspector_nombre VARCHAR(200),
    inspector_cargo VARCHAR(200),
    inspector_empresa VARCHAR(200),
    firma_inspector_url VARCHAR(1000),
    -- Representante empleador (RM 050)
    representante_nombre VARCHAR(200),
    representante_cargo VARCHAR(200),
    firma_representante_url VARCHAR(1000),
    -- Textos generales
    descripcion_causas TEXT,               -- "Descripción causas resultados desfavorables"
    conclusiones TEXT,                     -- "Conclusiones y recomendaciones"
    -- Score checklist calculado al cerrar
    total_items INT NOT NULL DEFAULT 0,
    total_cumple INT NOT NULL DEFAULT 0,
    total_no_cumple INT NOT NULL DEFAULT 0,
    total_na INT NOT NULL DEFAULT 0,
    tasa_cumplimiento DECIMAL(5,2),        -- (cumple / (cumple+no_cumple)) * 100
    estado VARCHAR(20) NOT NULL DEFAULT 'Borrador', -- Borrador | En Proceso | Cerrada
    created_at TIMESTAMP DEFAULT NOW(),
    created_by INT
);

-- 4. Respuestas al checklist
CREATE TABLE ssoma_inspeccion_respuesta (
    id SERIAL PRIMARY KEY,
    inspeccion_id INT NOT NULL REFERENCES ssoma_inspeccion(id) ON DELETE CASCADE,
    item_id INT NOT NULL REFERENCES ssoma_inspeccion_checklist_item(id),
    resultado VARCHAR(20) NOT NULL DEFAULT 'NA', -- Cumple | NoCumple | NA
    observacion TEXT
);

-- 5. Hallazgos
CREATE TABLE ssoma_inspeccion_hallazgo (
    id SERIAL PRIMARY KEY,
    inspeccion_id INT NOT NULL REFERENCES ssoma_inspeccion(id) ON DELETE CASCADE,
    descripcion TEXT NOT NULL,
    tipo VARCHAR(20) NOT NULL DEFAULT 'Menor', -- Critico | Mayor | Menor
    area VARCHAR(200),
    responsable_nombre VARCHAR(200),           -- nombre libre
    responsable_cargo VARCHAR(200),
    fecha_limite DATE,
    estado VARCHAR(20) NOT NULL DEFAULT 'Abierto', -- Abierto | EnProceso | Cerrado
    accion_correctiva TEXT,
    evidencia_cierre_url VARCHAR(1000),
    fecha_cierre DATE,
    latitud DECIMAL(10,7),
    longitud DECIMAL(10,7),
    created_at TIMESTAMP DEFAULT NOW()
);

-- 6. Fotos de hallazgos (N fotos por hallazgo)
CREATE TABLE ssoma_inspeccion_hallazgo_foto (
    id SERIAL PRIMARY KEY,
    hallazgo_id INT NOT NULL REFERENCES ssoma_inspeccion_hallazgo(id) ON DELETE CASCADE,
    url VARCHAR(1000) NOT NULL,
    descripcion VARCHAR(300),
    orden INT NOT NULL DEFAULT 0,
    created_at TIMESTAMP DEFAULT NOW()
);

-- Índices
CREATE INDEX idx_ssoma_insp_proyecto ON ssoma_inspeccion(proyecto_id);
CREATE INDEX idx_ssoma_insp_tipo ON ssoma_inspeccion(tipo_id);
CREATE INDEX idx_ssoma_insp_fecha ON ssoma_inspeccion(fecha);
CREATE INDEX idx_ssoma_insp_estado ON ssoma_inspeccion(estado);
CREATE INDEX idx_ssoma_insp_hallazgo ON ssoma_inspeccion_hallazgo(inspeccion_id);
CREATE INDEX idx_ssoma_insp_respuesta ON ssoma_inspeccion_respuesta(inspeccion_id);

-- ─────────────────────────────────────────────────────────────
-- DATOS INICIALES — Tipos de inspección
-- ─────────────────────────────────────────────────────────────
INSERT INTO ssoma_inspeccion_tipo (nombre, ambito) VALUES
-- Del CSV de Abril (23 tipos)
('Extintores', 'Seguridad'),
('Accesos, señalización y delimitación de áreas', 'Seguridad'),
('Andamios', 'Seguridad'),
('Orden y limpieza', 'Seguridad'),
('Herramientas eléctricas', 'Seguridad'),
('Instalaciones y tableros eléctricos', 'Seguridad'),
('Herramientas manuales', 'Seguridad'),
('Escaleras', 'Seguridad'),
('Equipo de Protección Personal', 'Seguridad'),
('Equipo de protección contra caída', 'Seguridad'),
('Aparejos de izaje', 'Seguridad'),
('Botiquines y estación de emergencia', 'Salud'),
('Almacén de combustibles', 'Ambiente'),
('Almacén productos químicos', 'Ambiente'),
('Almacenes y talleres', 'Seguridad'),
('Comedores', 'Salud'),
('Oficinas', 'Seguridad'),
('Vestuarios', 'Salud'),
('Kit antiderrame', 'Ambiente'),
('Acopio de residuos sólidos', 'Ambiente'),
('Polvo y ruido', 'Salud'),
('Protecciones colectivas', 'Seguridad'),
('Inspección Integral de Obra', 'Seguridad'),
-- Nuevos RM 050 / DS 005 / G.050 (9 tipos)
('Trabajos en altura', 'Seguridad'),
('Espacios confinados', 'Seguridad'),
('Izaje de cargas - operador', 'Seguridad'),
('Vehículos y maquinaria pesada', 'Seguridad'),
('Trabajos en caliente', 'Seguridad'),
('Excavaciones y zanjas', 'Seguridad'),
('Instalaciones sanitarias', 'Salud'),
('SSHH y bienestar', 'Salud'),
('Primeros auxilios y AED', 'Salud');
```

**NOTA:** Los 421 items del checklist se cargan por separado vía script SQL (ver sección DATOS CHECKLIST al final).

---

## MODELOS EF CORE

```csharp
// InspeccionModels.cs
namespace Abril_Backend.Features.Ssoma.Inspeccion.Infrastructure.Models;

public class SsomaInspeccionTipo
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Ambito { get; set; } = "Seguridad";
    public bool Activo { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SsomaInspeccionChecklistItem> Items { get; set; } = [];
}

public class SsomaInspeccionChecklistItem
{
    public int Id { get; set; }
    public int TipoId { get; set; }
    public string Pregunta { get; set; } = string.Empty;
    public string? Categoria { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;

    public SsomaInspeccionTipo? Tipo { get; set; }
}

public class SsomaInspeccion
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public int TipoId { get; set; }
    public int? EmpresaId { get; set; }
    public bool EsPlanificada { get; set; } = true;
    public DateTime Fecha { get; set; }
    public TimeOnly? HoraInicio { get; set; }
    public TimeOnly? HoraFin { get; set; }
    public string? Area { get; set; }
    public string? ResponsableArea { get; set; }
    // Inspector nombre libre — PENDIENTE: migrar a app_user logueado
    public string? InspectorNombre { get; set; }
    public string? InspectorCargo { get; set; }
    public string? InspectorEmpresa { get; set; }
    public string? FirmaInspectorUrl { get; set; }
    // Representante empleador (RM 050)
    public string? RepresentanteNombre { get; set; }
    public string? RepresentanteCargo { get; set; }
    public string? FirmaRepresentanteUrl { get; set; }
    // Textos
    public string? DescripcionCausas { get; set; }
    public string? Conclusiones { get; set; }
    // Score
    public int TotalItems { get; set; }
    public int TotalCumple { get; set; }
    public int TotalNoCumple { get; set; }
    public int TotalNa { get; set; }
    public decimal? TasaCumplimiento { get; set; }
    public string Estado { get; set; } = "Borrador";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedBy { get; set; }

    public Project? Proyecto { get; set; }
    public SsomaInspeccionTipo? Tipo { get; set; }
    public Contributor? Empresa { get; set; }
    public ICollection<SsomaInspeccionRespuesta> Respuestas { get; set; } = [];
    public ICollection<SsomaInspeccionHallazgo> Hallazgos { get; set; } = [];
}

public class SsomaInspeccionRespuesta
{
    public int Id { get; set; }
    public int InspeccionId { get; set; }
    public int ItemId { get; set; }
    public string Resultado { get; set; } = "NA"; // Cumple | NoCumple | NA
    public string? Observacion { get; set; }

    public SsomaInspeccion? Inspeccion { get; set; }
    public SsomaInspeccionChecklistItem? Item { get; set; }
}

public class SsomaInspeccionHallazgo
{
    public int Id { get; set; }
    public int InspeccionId { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string Tipo { get; set; } = "Menor"; // Critico | Mayor | Menor
    public string? Area { get; set; }
    public string? ResponsableNombre { get; set; }   // nombre libre
    public string? ResponsableCargo { get; set; }
    public DateTime? FechaLimite { get; set; }
    public string Estado { get; set; } = "Abierto"; // Abierto | EnProceso | Cerrado
    public string? AccionCorrectiva { get; set; }
    public string? EvidenciaCierreUrl { get; set; }
    public DateTime? FechaCierre { get; set; }
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SsomaInspeccion? Inspeccion { get; set; }
    public ICollection<SsomaInspeccionHallazgoFoto> Fotos { get; set; } = [];
}

public class SsomaInspeccionHallazgoFoto
{
    public int Id { get; set; }
    public int HallazgoId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SsomaInspeccionHallazgo? Hallazgo { get; set; }
}
```

**Agregar DbSets en `Shared/Data/AppContext.cs`:**
```csharp
// Using necesario:
using Abril_Backend.Features.Ssoma.Inspeccion.Infrastructure.Models;

// DbSets — usar patrón => Set<>() igual que otros módulos recientes:
public DbSet<SsomaInspeccionTipo> SsomaInspeccionTipo => Set<SsomaInspeccionTipo>();
public DbSet<SsomaInspeccionChecklistItem> SsomaInspeccionChecklistItem => Set<SsomaInspeccionChecklistItem>();
public DbSet<SsomaInspeccion> SsomaInspeccion => Set<SsomaInspeccion>();
public DbSet<SsomaInspeccionRespuesta> SsomaInspeccionRespuesta => Set<SsomaInspeccionRespuesta>();
public DbSet<SsomaInspeccionHallazgo> SsomaInspeccionHallazgo => Set<SsomaInspeccionHallazgo>();
public DbSet<SsomaInspeccionHallazgoFoto> SsomaInspeccionHallazgoFoto => Set<SsomaInspeccionHallazgoFoto>();
```

**ConfigurePostgreSQL — ToTable explícito si hay colisión:**
```csharp
modelBuilder.Entity<SsomaInspeccionTipo>().ToTable("ssoma_inspeccion_tipo");
modelBuilder.Entity<SsomaInspeccionChecklistItem>().ToTable("ssoma_inspeccion_checklist_item");
modelBuilder.Entity<SsomaInspeccion>().ToTable("ssoma_inspeccion");
modelBuilder.Entity<SsomaInspeccionRespuesta>().ToTable("ssoma_inspeccion_respuesta");
modelBuilder.Entity<SsomaInspeccionHallazgo>().ToTable("ssoma_inspeccion_hallazgo");
modelBuilder.Entity<SsomaInspeccionHallazgoFoto>().ToTable("ssoma_inspeccion_hallazgo_foto");
```

---

## DTOs

```csharp
// InspeccionDtos.cs

// ── Catálogos ──────────────────────────────────────────────

public class InspeccionTipoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Ambito { get; set; } = string.Empty;
}

public class InspeccionChecklistItemDto
{
    public int Id { get; set; }
    public int TipoId { get; set; }
    public string Pregunta { get; set; } = string.Empty;
    public string? Categoria { get; set; }
    public int Orden { get; set; }
}

// ── Crear / Actualizar ─────────────────────────────────────

public class InspeccionRespuestaRequest
{
    public int ItemId { get; set; }
    public string Resultado { get; set; } = "NA"; // Cumple | NoCumple | NA
    public string? Observacion { get; set; }
}

public class InspeccionHallazgoRequest
{
    public string Descripcion { get; set; } = string.Empty;
    public string Tipo { get; set; } = "Menor";
    public string? Area { get; set; }
    public string? ResponsableNombre { get; set; }
    public string? ResponsableCargo { get; set; }
    public DateTime? FechaLimite { get; set; }
    public string? AccionCorrectiva { get; set; }
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
    public List<string> FotosBase64 { get; set; } = []; // canvas/cámara → base64
}

public class CrearInspeccionRequest
{
    public int ProyectoId { get; set; }
    public int TipoId { get; set; }
    public int? EmpresaId { get; set; }
    public bool EsPlanificada { get; set; } = true;
    public DateTime Fecha { get; set; }
    public string? HoraInicio { get; set; } // "HH:mm"
    public string? HoraFin { get; set; }    // "HH:mm"
    public string? Area { get; set; }
    public string? ResponsableArea { get; set; }
    public string? InspectorNombre { get; set; }
    public string? InspectorCargo { get; set; }
    public string? InspectorEmpresa { get; set; }
    public string? FirmaInspectorBase64 { get; set; }
    public string? RepresentanteNombre { get; set; }
    public string? RepresentanteCargo { get; set; }
    public string? FirmaRepresentanteBase64 { get; set; }
    public string? DescripcionCausas { get; set; }
    public string? Conclusiones { get; set; }
    public List<InspeccionRespuestaRequest> Respuestas { get; set; } = [];
    public List<InspeccionHallazgoRequest> Hallazgos { get; set; } = [];
}

public class CerrarHallazgoRequest
{
    public string AccionCorrectiva { get; set; } = string.Empty;
    public string? EvidenciaCierreBase64 { get; set; }
}

// ── Respuestas ─────────────────────────────────────────────

public class InspeccionHallazgoFotoDto
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
}

public class InspeccionHallazgoDto
{
    public int Id { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string? Area { get; set; }
    public string? ResponsableNombre { get; set; }
    public string? ResponsableCargo { get; set; }
    public DateTime? FechaLimite { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? AccionCorrectiva { get; set; }
    public string? EvidenciaCierreUrl { get; set; }
    public DateTime? FechaCierre { get; set; }
    public decimal? Latitud { get; set; }
    public decimal? Longitud { get; set; }
    public List<InspeccionHallazgoFotoDto> Fotos { get; set; } = [];
}

public class InspeccionRespuestaDto
{
    public int ItemId { get; set; }
    public string Pregunta { get; set; } = string.Empty;
    public string? Categoria { get; set; }
    public int Orden { get; set; }
    public string Resultado { get; set; } = string.Empty;
    public string? Observacion { get; set; }
}

public class InspeccionDetalleDto
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public string ProyectoNombre { get; set; } = string.Empty;
    public int TipoId { get; set; }
    public string TipoNombre { get; set; } = string.Empty;
    public string TipoAmbito { get; set; } = string.Empty;
    public int? EmpresaId { get; set; }
    public string? EmpresaNombre { get; set; }
    public bool EsPlanificada { get; set; }
    public DateTime Fecha { get; set; }
    public string? HoraInicio { get; set; }
    public string? HoraFin { get; set; }
    public string? Area { get; set; }
    public string? ResponsableArea { get; set; }
    public string? InspectorNombre { get; set; }
    public string? InspectorCargo { get; set; }
    public string? InspectorEmpresa { get; set; }
    public string? FirmaInspectorUrl { get; set; }
    public string? RepresentanteNombre { get; set; }
    public string? RepresentanteCargo { get; set; }
    public string? FirmaRepresentanteUrl { get; set; }
    public string? DescripcionCausas { get; set; }
    public string? Conclusiones { get; set; }
    public int TotalItems { get; set; }
    public int TotalCumple { get; set; }
    public int TotalNoCumple { get; set; }
    public int TotalNa { get; set; }
    public decimal? TasaCumplimiento { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<InspeccionRespuestaDto> Respuestas { get; set; } = [];
    public List<InspeccionHallazgoDto> Hallazgos { get; set; } = [];
}

public class InspeccionListItemDto
{
    public int Id { get; set; }
    public string ProyectoNombre { get; set; } = string.Empty;
    public string TipoNombre { get; set; } = string.Empty;
    public string TipoAmbito { get; set; } = string.Empty;
    public string? EmpresaNombre { get; set; }
    public bool EsPlanificada { get; set; }
    public DateTime Fecha { get; set; }
    public string? Area { get; set; }
    public string? InspectorNombre { get; set; }
    public int TotalHallazgos { get; set; }
    public int HallazgosCriticos { get; set; }
    public int HallazgosAbiertos { get; set; }
    public decimal? TasaCumplimiento { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// ── Dashboard ──────────────────────────────────────────────

public class InspeccionDashboardDto
{
    public int TotalInspecciones { get; set; }
    public int TotalEsteMes { get; set; }
    public int HallazgosAbiertos { get; set; }
    public int HallazgosCriticosAbiertos { get; set; }
    public decimal? TasaCumplimientoPromedio { get; set; }
    public decimal? TasaCumplimientoEsteMes { get; set; }
    public List<InspeccionTendenciaMensualDto> TendenciaMensual { get; set; } = [];
    public List<InspeccionPorTipoDto> PorTipo { get; set; } = [];
    public List<InspeccionHallazgoPorAreaDto> HallazgosPorArea { get; set; } = [];
    public List<InspeccionHallazgoRecurrenteDto> HallazgosRecurrentes { get; set; } = [];
}

public class InspeccionTendenciaMensualDto
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public string MesNombre { get; set; } = string.Empty;
    public int Total { get; set; }
    public decimal? TasaPromedio { get; set; }
}

public class InspeccionPorTipoDto
{
    public string TipoNombre { get; set; } = string.Empty;
    public string Ambito { get; set; } = string.Empty;
    public int Total { get; set; }
    public decimal? TasaPromedio { get; set; }
}

public class InspeccionHallazgoPorAreaDto
{
    public string Area { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Criticos { get; set; }
    public int Abiertos { get; set; }
}

public class InspeccionHallazgoRecurrenteDto
{
    public string Descripcion { get; set; } = string.Empty;
    public int Ocurrencias { get; set; }
    public string UltimoTipo { get; set; } = string.Empty;
}
```

---

## INTERFACES

```csharp
// IInspeccionRepository.cs
public interface IInspeccionRepository
{
    Task<List<InspeccionTipoDto>> GetTiposAsync();
    Task<List<InspeccionChecklistItemDto>> GetChecklistItemsAsync(int tipoId);
    Task<List<InspeccionListItemDto>> GetListAsync(int? proyectoId, int? tipoId,
        string? estado, DateTime? fechaDesde, DateTime? fechaHasta, int page, int pageSize);
    Task<int> GetListCountAsync(int? proyectoId, int? tipoId,
        string? estado, DateTime? fechaDesde, DateTime? fechaHasta);
    Task<InspeccionDetalleDto?> GetDetalleAsync(int id);
    Task<int> CrearInspeccionAsync(CrearInspeccionRequest request,
        string? firmaInspectorUrl, string? firmaRepresentanteUrl,
        Dictionary<int, List<string>> fotosHallazgoUrls);
    Task CerrarHallazgoAsync(int hallazgoId, CerrarHallazgoRequest request, string? evidenciaUrl);
    Task<InspeccionDashboardDto> GetDashboardAsync(int? proyectoId, int? anio);
}

// IInspeccionService.cs
public interface IInspeccionService
{
    Task<object> GetCatalogosAsync();
    Task<List<InspeccionChecklistItemDto>> GetChecklistAsync(int tipoId);
    Task<object> GetListAsync(int? proyectoId, int? tipoId,
        string? estado, DateTime? fechaDesde, DateTime? fechaHasta, int page, int pageSize);
    Task<InspeccionDetalleDto> GetDetalleAsync(int id);
    Task<int> CrearInspeccionAsync(CrearInspeccionRequest request);
    Task CerrarHallazgoAsync(int hallazgoId, CerrarHallazgoRequest request);
    Task<InspeccionDashboardDto> GetDashboardAsync(int? proyectoId, int? anio);
}
```

---

## REPOSITORIO

```csharp
// InspeccionRepository.cs
public class InspeccionRepository : IInspeccionRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public InspeccionRepository(IDbContextFactory<AppDbContext> factory)
        => _factory = factory;

    public async Task<List<InspeccionTipoDto>> GetTiposAsync()
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsomaInspeccionTipo
            .Where(t => t.Activo)
            .OrderBy(t => t.Ambito).ThenBy(t => t.Nombre)
            .Select(t => new InspeccionTipoDto
            {
                Id = t.Id,
                Nombre = t.Nombre,
                Ambito = t.Ambito
            })
            .ToListAsync();
    }

    public async Task<List<InspeccionChecklistItemDto>> GetChecklistItemsAsync(int tipoId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsomaInspeccionChecklistItem
            .Where(i => i.TipoId == tipoId && i.Activo)
            .OrderBy(i => i.Orden)
            .Select(i => new InspeccionChecklistItemDto
            {
                Id = i.Id,
                TipoId = i.TipoId,
                Pregunta = i.Pregunta,
                Categoria = i.Categoria,
                Orden = i.Orden
            })
            .ToListAsync();
    }

    public async Task<int> CrearInspeccionAsync(CrearInspeccionRequest request,
        string? firmaInspectorUrl, string? firmaRepresentanteUrl,
        Dictionary<int, List<string>> fotosHallazgoUrls)
    {
        using var ctx = _factory.CreateDbContext();

        // Calcular score desde respuestas
        var totalCumple = request.Respuestas.Count(r => r.Resultado == "Cumple");
        var totalNoCumple = request.Respuestas.Count(r => r.Resultado == "NoCumple");
        var totalNa = request.Respuestas.Count(r => r.Resultado == "NA");
        var evaluados = totalCumple + totalNoCumple;
        decimal? tasa = evaluados > 0
            ? Math.Round((decimal)totalCumple / evaluados * 100, 2)
            : null;

        // Parsear horas
        TimeOnly? horaInicio = null, horaFin = null;
        if (!string.IsNullOrEmpty(request.HoraInicio) && TimeOnly.TryParse(request.HoraInicio, out var hi))
            horaInicio = hi;
        if (!string.IsNullOrEmpty(request.HoraFin) && TimeOnly.TryParse(request.HoraFin, out var hf))
            horaFin = hf;

        var inspeccion = new SsomaInspeccion
        {
            ProyectoId = request.ProyectoId,
            TipoId = request.TipoId,
            EmpresaId = request.EmpresaId,
            EsPlanificada = request.EsPlanificada,
            Fecha = request.Fecha.Date,
            HoraInicio = horaInicio,
            HoraFin = horaFin,
            Area = request.Area,
            ResponsableArea = request.ResponsableArea,
            InspectorNombre = request.InspectorNombre,
            InspectorCargo = request.InspectorCargo,
            InspectorEmpresa = request.InspectorEmpresa,
            FirmaInspectorUrl = firmaInspectorUrl,
            RepresentanteNombre = request.RepresentanteNombre,
            RepresentanteCargo = request.RepresentanteCargo,
            FirmaRepresentanteUrl = firmaRepresentanteUrl,
            DescripcionCausas = request.DescripcionCausas,
            Conclusiones = request.Conclusiones,
            TotalItems = request.Respuestas.Count,
            TotalCumple = totalCumple,
            TotalNoCumple = totalNoCumple,
            TotalNa = totalNa,
            TasaCumplimiento = tasa,
            Estado = request.Hallazgos.Any() ? "En Proceso" : "Cerrada",
            CreatedAt = DateTime.UtcNow
        };

        ctx.SsomaInspeccion.Add(inspeccion);
        await ctx.SaveChangesAsync();

        // Respuestas checklist
        foreach (var r in request.Respuestas)
        {
            ctx.SsomaInspeccionRespuesta.Add(new SsomaInspeccionRespuesta
            {
                InspeccionId = inspeccion.Id,
                ItemId = r.ItemId,
                Resultado = r.Resultado,
                Observacion = r.Observacion
            });
        }

        // Hallazgos + fotos
        for (int i = 0; i < request.Hallazgos.Count; i++)
        {
            var h = request.Hallazgos[i];
            var hallazgo = new SsomaInspeccionHallazgo
            {
                InspeccionId = inspeccion.Id,
                Descripcion = h.Descripcion,
                Tipo = h.Tipo,
                Area = h.Area,
                ResponsableNombre = h.ResponsableNombre,
                ResponsableCargo = h.ResponsableCargo,
                FechaLimite = h.FechaLimite,
                AccionCorrectiva = h.AccionCorrectiva,
                Latitud = h.Latitud,
                Longitud = h.Longitud,
                Estado = "Abierto",
                CreatedAt = DateTime.UtcNow
            };
            ctx.SsomaInspeccionHallazgo.Add(hallazgo);
            await ctx.SaveChangesAsync(); // necesario para obtener hallazgo.Id

            // Fotos del hallazgo (URLs ya subidas a SharePoint)
            if (fotosHallazgoUrls.TryGetValue(i, out var urls))
            {
                for (int j = 0; j < urls.Count; j++)
                {
                    ctx.SsomaInspeccionHallazgoFoto.Add(new SsomaInspeccionHallazgoFoto
                    {
                        HallazgoId = hallazgo.Id,
                        Url = urls[j],
                        Orden = j,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        await ctx.SaveChangesAsync();
        return inspeccion.Id;
    }

    public async Task CerrarHallazgoAsync(int hallazgoId, CerrarHallazgoRequest request, string? evidenciaUrl)
    {
        using var ctx = _factory.CreateDbContext();
        var hallazgo = await ctx.SsomaInspeccionHallazgo.FindAsync(hallazgoId)
            ?? throw new AbrilException(404, "Hallazgo no encontrado.");
        hallazgo.Estado = "Cerrado";
        hallazgo.AccionCorrectiva = request.AccionCorrectiva;
        hallazgo.EvidenciaCierreUrl = evidenciaUrl;
        hallazgo.FechaCierre = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    public async Task<InspeccionDetalleDto?> GetDetalleAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var insp = await ctx.SsomaInspeccion
            .Include(i => i.Proyecto)
            .Include(i => i.Tipo)
            .Include(i => i.Empresa)
            .Include(i => i.Respuestas).ThenInclude(r => r.Item)
            .Include(i => i.Hallazgos).ThenInclude(h => h.Fotos)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (insp == null) return null;

        return new InspeccionDetalleDto
        {
            Id = insp.Id,
            ProyectoId = insp.ProyectoId,
            ProyectoNombre = insp.Proyecto?.ProjectDescription ?? "",
            TipoId = insp.TipoId,
            TipoNombre = insp.Tipo?.Nombre ?? "",
            TipoAmbito = insp.Tipo?.Ambito ?? "",
            EmpresaId = insp.EmpresaId,
            EmpresaNombre = insp.Empresa?.ContributorNombreComercial,
            EsPlanificada = insp.EsPlanificada,
            Fecha = insp.Fecha,
            HoraInicio = insp.HoraInicio?.ToString("HH:mm"),
            HoraFin = insp.HoraFin?.ToString("HH:mm"),
            Area = insp.Area,
            ResponsableArea = insp.ResponsableArea,
            InspectorNombre = insp.InspectorNombre,
            InspectorCargo = insp.InspectorCargo,
            InspectorEmpresa = insp.InspectorEmpresa,
            FirmaInspectorUrl = insp.FirmaInspectorUrl,
            RepresentanteNombre = insp.RepresentanteNombre,
            RepresentanteCargo = insp.RepresentanteCargo,
            FirmaRepresentanteUrl = insp.FirmaRepresentanteUrl,
            DescripcionCausas = insp.DescripcionCausas,
            Conclusiones = insp.Conclusiones,
            TotalItems = insp.TotalItems,
            TotalCumple = insp.TotalCumple,
            TotalNoCumple = insp.TotalNoCumple,
            TotalNa = insp.TotalNa,
            TasaCumplimiento = insp.TasaCumplimiento,
            Estado = insp.Estado,
            CreatedAt = insp.CreatedAt,
            Respuestas = insp.Respuestas
                .OrderBy(r => r.Item?.Orden)
                .Select(r => new InspeccionRespuestaDto
                {
                    ItemId = r.ItemId,
                    Pregunta = r.Item?.Pregunta ?? "",
                    Categoria = r.Item?.Categoria,
                    Orden = r.Item?.Orden ?? 0,
                    Resultado = r.Resultado,
                    Observacion = r.Observacion
                }).ToList(),
            Hallazgos = insp.Hallazgos
                .OrderByDescending(h => h.Tipo) // Critico primero
                .Select(h => new InspeccionHallazgoDto
                {
                    Id = h.Id,
                    Descripcion = h.Descripcion,
                    Tipo = h.Tipo,
                    Area = h.Area,
                    ResponsableNombre = h.ResponsableNombre,
                    ResponsableCargo = h.ResponsableCargo,
                    FechaLimite = h.FechaLimite,
                    Estado = h.Estado,
                    AccionCorrectiva = h.AccionCorrectiva,
                    EvidenciaCierreUrl = h.EvidenciaCierreUrl,
                    FechaCierre = h.FechaCierre,
                    Latitud = h.Latitud,
                    Longitud = h.Longitud,
                    Fotos = h.Fotos.OrderBy(f => f.Orden)
                        .Select(f => new InspeccionHallazgoFotoDto
                        {
                            Id = f.Id,
                            Url = f.Url,
                            Descripcion = f.Descripcion,
                            Orden = f.Orden
                        }).ToList()
                }).ToList()
        };
    }

    public async Task<List<InspeccionListItemDto>> GetListAsync(int? proyectoId, int? tipoId,
        string? estado, DateTime? fechaDesde, DateTime? fechaHasta, int page, int pageSize)
    {
        using var ctx = _factory.CreateDbContext();
        var q = ctx.SsomaInspeccion
            .Include(i => i.Proyecto)
            .Include(i => i.Tipo)
            .Include(i => i.Empresa)
            .Include(i => i.Hallazgos)
            .AsQueryable();

        if (proyectoId.HasValue) q = q.Where(i => i.ProyectoId == proyectoId.Value);
        if (tipoId.HasValue) q = q.Where(i => i.TipoId == tipoId.Value);
        if (!string.IsNullOrEmpty(estado)) q = q.Where(i => i.Estado == estado);
        if (fechaDesde.HasValue) q = q.Where(i => i.Fecha >= fechaDesde.Value.Date);
        if (fechaHasta.HasValue) q = q.Where(i => i.Fecha <= fechaHasta.Value.Date);

        return await q
            .OrderByDescending(i => i.Fecha)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InspeccionListItemDto
            {
                Id = i.Id,
                ProyectoNombre = i.Proyecto != null ? i.Proyecto.ProjectDescription : "",
                TipoNombre = i.Tipo != null ? i.Tipo.Nombre : "",
                TipoAmbito = i.Tipo != null ? i.Tipo.Ambito : "",
                EmpresaNombre = i.Empresa != null ? i.Empresa.ContributorNombreComercial : null,
                EsPlanificada = i.EsPlanificada,
                Fecha = i.Fecha,
                Area = i.Area,
                InspectorNombre = i.InspectorNombre,
                TotalHallazgos = i.Hallazgos.Count,
                HallazgosCriticos = i.Hallazgos.Count(h => h.Tipo == "Critico"),
                HallazgosAbiertos = i.Hallazgos.Count(h => h.Estado == "Abierto"),
                TasaCumplimiento = i.TasaCumplimiento,
                Estado = i.Estado,
                CreatedAt = i.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<int> GetListCountAsync(int? proyectoId, int? tipoId,
        string? estado, DateTime? fechaDesde, DateTime? fechaHasta)
    {
        using var ctx = _factory.CreateDbContext();
        var q = ctx.SsomaInspeccion.AsQueryable();
        if (proyectoId.HasValue) q = q.Where(i => i.ProyectoId == proyectoId.Value);
        if (tipoId.HasValue) q = q.Where(i => i.TipoId == tipoId.Value);
        if (!string.IsNullOrEmpty(estado)) q = q.Where(i => i.Estado == estado);
        if (fechaDesde.HasValue) q = q.Where(i => i.Fecha >= fechaDesde.Value.Date);
        if (fechaHasta.HasValue) q = q.Where(i => i.Fecha <= fechaHasta.Value.Date);
        return await q.CountAsync();
    }

    public async Task<InspeccionDashboardDto> GetDashboardAsync(int? proyectoId, int? anio)
    {
        using var ctx = _factory.CreateDbContext();
        var anioFiltro = anio ?? DateTime.Now.Year;
        var mesActual = DateTime.Now.Month;

        var q = ctx.SsomaInspeccion.Include(i => i.Tipo).Include(i => i.Hallazgos).AsQueryable();
        if (proyectoId.HasValue) q = q.Where(i => i.ProyectoId == proyectoId.Value);

        var all = await q.ToListAsync();
        var delAnio = all.Where(i => i.Fecha.Year == anioFiltro).ToList();
        var delMes = delAnio.Where(i => i.Fecha.Month == mesActual).ToList();
        var todosHallazgos = all.SelectMany(i => i.Hallazgos).ToList();

        // Tendencia 12 meses
        var tendencia = Enumerable.Range(1, 12).Select(m =>
        {
            var items = delAnio.Where(i => i.Fecha.Month == m).ToList();
            return new InspeccionTendenciaMensualDto
            {
                Anio = anioFiltro,
                Mes = m,
                MesNombre = new DateTime(anioFiltro, m, 1).ToString("MMM",
                    new System.Globalization.CultureInfo("es-PE")),
                Total = items.Count,
                TasaPromedio = items.Any(i => i.TasaCumplimiento.HasValue)
                    ? Math.Round(items.Where(i => i.TasaCumplimiento.HasValue)
                        .Average(i => i.TasaCumplimiento!.Value), 1)
                    : null
            };
        }).ToList();

        // Por tipo
        var porTipo = delAnio
            .GroupBy(i => new { i.TipoId, Nombre = i.Tipo?.Nombre ?? "", Ambito = i.Tipo?.Ambito ?? "" })
            .Select(g => new InspeccionPorTipoDto
            {
                TipoNombre = g.Key.Nombre,
                Ambito = g.Key.Ambito,
                Total = g.Count(),
                TasaPromedio = g.Any(i => i.TasaCumplimiento.HasValue)
                    ? Math.Round(g.Where(i => i.TasaCumplimiento.HasValue)
                        .Average(i => i.TasaCumplimiento!.Value), 1)
                    : null
            })
            .OrderByDescending(t => t.Total)
            .Take(10)
            .ToList();

        // Hallazgos por área
        var hallazgosPorArea = todosHallazgos
            .Where(h => !string.IsNullOrEmpty(h.Area))
            .GroupBy(h => h.Area!)
            .Select(g => new InspeccionHallazgoPorAreaDto
            {
                Area = g.Key,
                Total = g.Count(),
                Criticos = g.Count(h => h.Tipo == "Critico"),
                Abiertos = g.Count(h => h.Estado == "Abierto")
            })
            .OrderByDescending(a => a.Criticos)
            .Take(10)
            .ToList();

        // Hallazgos recurrentes (mismas palabras clave en descripción — top 5)
        var recurrentes = todosHallazgos
            .GroupBy(h => h.Descripcion.ToLower().Trim())
            .Where(g => g.Count() > 1)
            .Select(g => new InspeccionHallazgoRecurrenteDto
            {
                Descripcion = g.First().Descripcion,
                Ocurrencias = g.Count(),
                UltimoTipo = g.OrderByDescending(h => h.CreatedAt).First().Tipo
            })
            .OrderByDescending(r => r.Ocurrencias)
            .Take(5)
            .ToList();

        return new InspeccionDashboardDto
        {
            TotalInspecciones = all.Count,
            TotalEsteMes = delMes.Count,
            HallazgosAbiertos = todosHallazgos.Count(h => h.Estado == "Abierto"),
            HallazgosCriticosAbiertos = todosHallazgos.Count(h => h.Tipo == "Critico" && h.Estado == "Abierto"),
            TasaCumplimientoPromedio = all.Any(i => i.TasaCumplimiento.HasValue)
                ? Math.Round(all.Where(i => i.TasaCumplimiento.HasValue)
                    .Average(i => i.TasaCumplimiento!.Value), 1)
                : null,
            TasaCumplimientoEsteMes = delMes.Any(i => i.TasaCumplimiento.HasValue)
                ? Math.Round(delMes.Where(i => i.TasaCumplimiento.HasValue)
                    .Average(i => i.TasaCumplimiento!.Value), 1)
                : null,
            TendenciaMensual = tendencia,
            PorTipo = porTipo,
            HallazgosPorArea = hallazgosPorArea,
            HallazgosRecurrentes = recurrentes
        };
    }
}
```

---

## SERVICIO

```csharp
// InspeccionService.cs
public class InspeccionService : IInspeccionService
{
    private readonly IInspeccionRepository _repo;
    private readonly IInspeccionSharePointService _sp;

    public InspeccionService(IInspeccionRepository repo, IInspeccionSharePointService sp)
    {
        _repo = repo;
        _sp = sp;
    }

    public async Task<object> GetCatalogosAsync()
    {
        var tipos = await _repo.GetTiposAsync();
        return new { tipos };
    }

    public async Task<List<InspeccionChecklistItemDto>> GetChecklistAsync(int tipoId)
        => await _repo.GetChecklistItemsAsync(tipoId);

    public async Task<object> GetListAsync(int? proyectoId, int? tipoId,
        string? estado, DateTime? fechaDesde, DateTime? fechaHasta, int page, int pageSize)
    {
        var items = await _repo.GetListAsync(proyectoId, tipoId, estado, fechaDesde, fechaHasta, page, pageSize);
        var total = await _repo.GetListCountAsync(proyectoId, tipoId, estado, fechaDesde, fechaHasta);
        return new { items, total, page, pageSize };
    }

    public async Task<InspeccionDetalleDto> GetDetalleAsync(int id)
    {
        var result = await _repo.GetDetalleAsync(id);
        if (result == null) throw new AbrilException(404, "Inspección no encontrada.");
        return result;
    }

    public async Task<InspeccionDashboardDto> GetDashboardAsync(int? proyectoId, int? anio)
        => await _repo.GetDashboardAsync(proyectoId, anio);

    public async Task<int> CrearInspeccionAsync(CrearInspeccionRequest request)
    {
        if (request.TipoId <= 0)
            throw new AbrilException(400, "El tipo de inspección es requerido.");

        // Firma inspector
        string? firmaInspectorUrl = null;
        if (!string.IsNullOrEmpty(request.FirmaInspectorBase64))
        {
            var bytes = Convert.FromBase64String(
                request.FirmaInspectorBase64.Contains(",")
                    ? request.FirmaInspectorBase64.Split(',')[1]
                    : request.FirmaInspectorBase64);
            using var stream = new MemoryStream(bytes);
            firmaInspectorUrl = await _sp.SubirFirmaInspectorAsync(
                stream, $"inspector_{DateTime.UtcNow:yyyyMMddHHmmss}.png", 0);
        }

        // Firma representante
        string? firmaRepresentanteUrl = null;
        if (!string.IsNullOrEmpty(request.FirmaRepresentanteBase64))
        {
            var bytes = Convert.FromBase64String(
                request.FirmaRepresentanteBase64.Contains(",")
                    ? request.FirmaRepresentanteBase64.Split(',')[1]
                    : request.FirmaRepresentanteBase64);
            using var stream = new MemoryStream(bytes);
            firmaRepresentanteUrl = await _sp.SubirFirmaRepresentanteAsync(
                stream, $"representante_{DateTime.UtcNow:yyyyMMddHHmmss}.png", 0);
        }

        // Fotos de hallazgos (index del hallazgo → lista de URLs)
        var fotosHallazgoUrls = new Dictionary<int, List<string>>();
        for (int i = 0; i < request.Hallazgos.Count; i++)
        {
            var urls = new List<string>();
            for (int j = 0; j < request.Hallazgos[i].FotosBase64.Count; j++)
            {
                var base64 = request.Hallazgos[i].FotosBase64[j];
                var data = base64.Contains(",") ? base64.Split(',')[1] : base64;
                var bytes = Convert.FromBase64String(data);
                using var stream = new MemoryStream(bytes);
                var url = await _sp.SubirFotoHallazgoAsync(
                    stream, $"foto_{i}_{j}_{DateTime.UtcNow:yyyyMMddHHmmss}.jpg", 0, i);
                urls.Add(url);
            }
            if (urls.Any()) fotosHallazgoUrls[i] = urls;
        }

        return await _repo.CrearInspeccionAsync(request, firmaInspectorUrl,
            firmaRepresentanteUrl, fotosHallazgoUrls);
    }

    public async Task CerrarHallazgoAsync(int hallazgoId, CerrarHallazgoRequest request)
    {
        string? evidenciaUrl = null;
        if (!string.IsNullOrEmpty(request.EvidenciaCierreBase64))
        {
            var data = request.EvidenciaCierreBase64.Contains(",")
                ? request.EvidenciaCierreBase64.Split(',')[1]
                : request.EvidenciaCierreBase64;
            var bytes = Convert.FromBase64String(data);
            using var stream = new MemoryStream(bytes);
            evidenciaUrl = await _sp.SubirFotoHallazgoAsync(
                stream, $"evidencia_{hallazgoId}_{DateTime.UtcNow:yyyyMMddHHmmss}.jpg",
                0, hallazgoId);
        }
        await _repo.CerrarHallazgoAsync(hallazgoId, request, evidenciaUrl);
    }
}
```

---

## CONTROLLER

```csharp
// InspeccionController.cs
[ApiController]
[Route("api/v1/ssoma-inspeccion")]
[Authorize]
public class InspeccionController : ControllerBase
{
    private readonly IInspeccionService _service;
    private readonly ILogger<InspeccionController> _logger;

    public InspeccionController(IInspeccionService service, ILogger<InspeccionController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // GET api/v1/ssoma-inspeccion/catalogos
    [HttpGet("catalogos")]
    public async Task<IActionResult> GetCatalogos()
    {
        try { return Ok(await _service.GetCatalogosAsync()); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error catalogos inspeccion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    // GET api/v1/ssoma-inspeccion/checklist/{tipoId}
    [HttpGet("checklist/{tipoId:int}")]
    public async Task<IActionResult> GetChecklist(int tipoId)
    {
        try { return Ok(await _service.GetChecklistAsync(tipoId)); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error checklist inspeccion {TipoId}", tipoId); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    // GET api/v1/ssoma-inspeccion
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int? proyectoId, [FromQuery] int? tipoId,
        [FromQuery] string? estado,
        [FromQuery] DateTime? fechaDesde, [FromQuery] DateTime? fechaHasta,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try { return Ok(await _service.GetListAsync(proyectoId, tipoId, estado, fechaDesde, fechaHasta, page, pageSize)); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error lista inspecciones"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    // GET api/v1/ssoma-inspeccion/dashboard
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] int? proyectoId, [FromQuery] int? anio)
    {
        try { return Ok(await _service.GetDashboardAsync(proyectoId, anio)); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error dashboard inspecciones"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    // GET api/v1/ssoma-inspeccion/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetalle(int id)
    {
        try { return Ok(await _service.GetDetalleAsync(id)); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error detalle inspeccion {Id}", id); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    // POST api/v1/ssoma-inspeccion
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearInspeccionRequest request)
    {
        try
        {
            if (request.TipoId <= 0)
                return BadRequest(new { message = "El tipo de inspección es requerido." });
            if (request.ProyectoId <= 0)
                return BadRequest(new { message = "El proyecto es requerido." });
            var id = await _service.CrearInspeccionAsync(request);
            return Ok(new { id, message = "Inspección registrada correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error crear inspeccion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    // PATCH api/v1/ssoma-inspeccion-hallazgo/{id}/cerrar
    [HttpPatch("~/api/v1/ssoma-inspeccion-hallazgo/{id:int}/cerrar")]
    public async Task<IActionResult> CerrarHallazgo(int id, [FromBody] CerrarHallazgoRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.AccionCorrectiva))
                return BadRequest(new { message = "La acción correctiva es requerida." });
            await _service.CerrarHallazgoAsync(id, request);
            return Ok(new { message = "Hallazgo cerrado correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error cerrar hallazgo {Id}", id); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }
}
```

---

## REGISTRO EN SsomaModule.cs

```csharp
services.AddScoped<IInspeccionSharePointService, InspeccionSharePointService>();
services.AddScoped<IInspeccionRepository, InspeccionRepository>();
services.AddScoped<IInspeccionService, InspeccionService>();
```

---

## PENDIENTES REGISTRADOS

1. **Inspector** — campo libre por ahora. Migrar a `app_user` del logueado en sprint siguiente.
2. **Checklist items CSV** — los 421 items se cargan con script SQL separado en la siguiente sesión.
3. **Reporte PDF RM 050** — Fase 2: endpoint `GET /{id}/reporte` con jsPDF + formato oficial.
4. **Acción requerida → PASO** — Fase 2 igual que OPT.

---

## VERIFICACIÓN ANTES DE COMPILAR

- [ ] SQL ejecutado en pgAdmin sin errores (6 tablas + índices + 32 tipos)
- [ ] DbSets agregados en `AppContext.cs` con using correcto
- [ ] `ToTable()` agregado en `ConfigurePostgreSQL`
- [ ] `"inspeccion-fotos"` y `"inspeccion-firmas"` agregados en `SharePointHabService.cs`
- [ ] `SsomaModule.cs` registra los 3 servicios
- [ ] Verificar que `Contributor` tiene nav property en `SsomaInspeccion` — FK `empresa_id`
- [ ] `TimeOnly` disponible en .NET 10 ✅ — no necesita paquete adicional
