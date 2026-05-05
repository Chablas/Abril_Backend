# CONTEXT.md — Abril Backend
> Última actualización: 2026-05-05 (equipos habilitación — FK→contributor, ObsContratista, versiones; GET /equipos filtra por JWT para CONTRATISTA)

---

## 1. Stack

| Capa | Tecnología |
|------|-----------|
| Framework | ASP.NET Core (.NET 10) |
| ORM | EF Core + `UseSnakeCaseNamingConvention()` (PG) |
| BD principal | **PostgreSQL en Aiven** (cloud) |
| BD alternativa | SQL Server (dev local, selector `Database:DatabaseProvider`) |
| Auth | JWT Bearer interno (`Jwt:Key`) + Azure AD (Microsoft Entra) — ambos coexisten, política default acepta los dos |
| Email | PowerAutomate / SendGrid / SMTP (selector `Email:EmailProvider`) |
| Storage | Azure Blob / local `wwwroot/uploads` (selector `Storage:StorageProvider`) |
| Queries complejas | **Dapper** + `NpgsqlConnection` directa (solo en `BandejaRepository`) |
| Fechas UTC | `HabilitacionDateHelper` — `AsUtc()` y `ResolverVigencia()` |
| Puerto dev | 5236 http / 7298 https |
| Swagger | Solo en Development en `/swagger` |

```bash
dotnet build Abril-Backend.csproj
dotnet run --project Abril-Backend.csproj
# NO existe dotnet test
```

Config: `appsettings.json` → `appsettings.{Env}.json` → `appsettings.Local.json` (gitignored, secrets) → env vars.

---

## 2. Arquitectura

### 2a. Layered tradicional (carpetas raíz)

```
Controllers/                  → [ApiController], ruta "api/v1/[controller]"
Application/Interfaces/       → I*Service
Application/Services/         → *Service
Application/DTOs/             → agrupados por dominio
Application/Exceptions/       → AbrilException (con HTTP StatusCode)
Infrastructure/Interfaces/    → I*Repository
Infrastructure/Repositories/  → EF Core con IDbContextFactory
Infrastructure/Models/        → entidades EF
Shared/Data/AppContext.cs     → AppDbContext (namespace Abril_Backend.Infrastructure.Data)
Shared/Services/              → Email, Excel, Jwt, Reniec, Storage, Sunat
Shared/Models/                → Project, AuditoriaCambio
```

### 2b. Vertical slice — Features/

```
Features/<Modulo>Module/
  <Modulo>Module.cs                     → static AddXxxModule(IServiceCollection) — el ÚNICO punto que registra en Program.cs
  <Feature>Feature/
    Application/{Interfaces,Services,Dtos}
    Infrastructure/{Interfaces,Repositories,Models}
    Presentation/*Controller.cs
```

**Módulos activos:**
| Módulo | Estado |
|--------|--------|
| `HabilitacionModule` | Principal activo — ver sección 5 |
| `SsomaModule` | SaludOcupacionalFeature (EMO, SSOMA) |
| `ContractorsModule` | ContractorRegistration, ContractorManagement |
| `CostsModule` | Adjudicaciones, WorkItem, StaffProjectEmail |
| `MicrosoftAuthModule` | Login/Profile Microsoft |

**ArquitecturaComercial** vive en capa tradicional, no en Features.

---

## 3. Convenciones obligatorias

### Repositorios — IDbContextFactory siempre
```csharp
private readonly IDbContextFactory<AppDbContext> _factory;
// ...
using var ctx = _factory.CreateDbContext(); // contexto corto por llamada
```

### Controllers — try/catch estándar
```csharp
try { ... return Ok(result); }
catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
catch (Exception ex) { _logger.LogError(ex, "..."); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
```

### Auth cronjobs
```csharp
var authHeader = Request.Headers["Authorization"].FirstOrDefault();
if (authHeader != $"Bearer {_configuration["CronSecret"]}") return Unauthorized();
// NO usar Environment.GetEnvironmentVariable — usar IConfiguration
```

### Mensajes de error → siempre en español.

### DbSets → siempre en `Shared/Data/AppContext.cs`. Colisiones PG → override en `ConfigurePostgreSQL`.

---

## 4. Vocabulario de entidades — CRÍTICO

| Entidad C# | Tabla PG | PK | Notas |
|------------|----------|----|-------|
| `Project` | `project` | `project_id` | Entidad legacy ÚNICA para proyectos. Props: `ProjectId`, `ProjectDescription`. `Shared/Models/Project.cs`. **Siempre `ctx.Project` con `ProjectId`**. |
| `Contributor` | `contributor` | `contributor_id` | Entidad unificada de empresas. Reemplazó `companies` (eliminada). Incluye `EsAbril` (bool) e `IdSharepoint` (int?, temporal). En `Features/CostsModule/Shared/Models/Contributor.cs`. |
| `Worker` | `workers` | `id` | Personal con columnas explícitas `[Column("...")]`. No snake_case automático. |
| `WorkerVinculacion` | `worker_vinculaciones` | `id` | 1 activa por worker (`fecha_fin IS NULL`). Para empresa y proyecto actual del worker. |
| `WorkerProyecto` | `ss_hab_worker_proyecto` | `id` | Multi-proyecto **solo Casa**. N activos en paralelo. Unique partial index `(worker_id, proyecto_id) WHERE fecha_fin IS NULL`. |
| `SsInduccion` | `ss_induccion` | `id` | `empresa_id` → `contributor.contributor_id` (no `ss_empresa_contratista`). |
| `SsHabTrabajador` | `ss_hab_trabajador` | `id` | Entregables por worker. |
| `SsHabEmpresa` | `ss_hab_empresa` | `id` | `proyecto_id` → `project.project_id`. `empresa_id` → `contributor.contributor_id`. |
| `SsEquipo` | `ss_equipo` | `id` | `proyecto_id` → `project.project_id`. `propietario_empresa_id` → `contributor.contributor_id` (nav property `Contributor? PropietarioEmpresa`). |
| `SsHabEquipo` | `ss_hab_equipo` | `id` | Entregables por equipo. Tiene `ObsContratista` (agregada directamente en BD). `archivo_url` es `text` (fue `varchar(1000)` — alterada manualmente). |
| `SsItemTrabajador` | `ss_item_trabajador` | `id` | Catálogo de entregables con reglas. |
| `WorkerEvento` | `worker_eventos` | `id` | Creada manualmente en BD (sin migración EF). |
| `CatSubarea` | `cat_subarea` | `id` | Creada manualmente en BD (sin migración EF). |
| `User` | `app_user` | — | Override en `ConfigurePostgreSQL` (`User` es palabra reservada PG). |

> **⚠️ `projects` (plural) NO EXISTE** — fue eliminada vía migración `SwitchProyectoFkToProjectLegacy`. Todo `proyecto_id` de cualquier tabla apunta a `project.project_id` legacy. Resolver siempre con `ctx.Project.Where(p => p.ProjectId == id)`.

---

## 5. HabilitacionModule — detalle completo

**Ubicación:** `Features/HabilitacionModule/`

**DI adicional:** BCrypt.Net-Next, FluentValidation, Dapper. `ISharePointHabService` registrado como **Singleton** (cachea token OAuth2 y driveId).

### 5a. Catálogo ss_item_trabajador

Items clave por ID:

| id | nombre | aplica_a | requiere_vigencia | notas |
|----|--------|----------|--------------------|-------|
| 1 | DNI | TODOS | true | |
| 4 | Certificado de Aptitud (EMO) | TODOS | true | EMO Contratista en ss_hab_trabajador; Casa en worker_emos |
| 5 | Registro de Entrega de EPP | CASA | false | sentinel 2040 |
| 6 | Entrega RISST | CASA | false | sentinel 2040 |
| 8 | Entrega de Recomendaciones SST | CASA | false | sentinel 2040 |
| 10 | Difusion de PTS | CASA | false | sentinel 2040 |
| 11 | SCTR | TODOS | true | excluido de bandeja (NOT IN) |
| 12 | Induccion Obra | TODOS | false | sentinel 2040; reset al cambiar proyecto |
| 13 | Vida ley | TODOS | true | excluido de bandeja (NOT IN) |
| 25 | Lectura de EMO | CASA | true | incluido en itemsEmoIds → excluido cálculo bloqueo Casa |

`requiere_vigencia = false` → `HabilitacionDateHelper.ResolverVigencia(false, "Aprobado", null)` retorna sentinel **`2040-12-31 UTC`**.

### 5b. BandejaRepository — SelectBase UNION ALL

Query Dapper con `NpgsqlConnection` directa. Cuatro segmentos:

**TRABAJADOR** (`ss_hab_trabajador WHERE estado='Enviado'`):
- Excluye `item_id IN (11, 13)` — SCTR y Vida Ley
- Excluye `item_id IN (4, 25) AND w.contrata_casa = 'Casa'` — EMO items para Casa
- `CAST(ht.vigencia AS timestamp)` para columna vigencia
- Proyecto via `LEFT JOIN LATERAL (worker_vinculaciones ORDER BY created_at DESC, id DESC LIMIT 1)`
- Empresa via `ss_empresa_contratista`
- Proyecto nombre/id via `LEFT JOIN project p ON p.project_id = wv.proyecto_id` + `p.project_description`

**EMPRESA** (`ss_hab_empresa WHERE estado='Enviado'`):
- `CAST(he.vigencia AS timestamp)`
- `JOIN project p ON p.project_id = he.proyecto_id` + `p.project_description`
- Empresa via `ss_empresa_contratista`

**EQUIPO** (`ss_hab_equipo WHERE estado='Enviado'`):
- `CAST(heq.vigencia AS timestamp)`
- `JOIN project p ON p.project_id = eq.proyecto_id` + `p.project_description`
- Empresa via `ss_empresa_contratista`

**INDUCCION** (`ss_induccion WHERE estado='PROGRAMADA'`):
- `vigencia = NULL` (la vigencia real la asigna AprobarInduccionAsync al aprobar)
- `JOIN contributor c ON c.contributor_id = i.empresa_id` + `c.contributor_name`
- `JOIN project p ON p.project_id = i.proyecto_id` + `p.project_description`
- Entidad nombre: `w.apellido_nombre` (Worker no tiene apellido_paterno/materno separados)

> **⚠️ En todo UNION ALL**: las tres tablas hab usan `ss_empresa_contratista`; solo INDUCCION usa `contributor`. Esta asimetría existe en el SQL de Dapper — `ss_hab_empresa.empresa_id` y `ss_hab_equipo → ss_equipo.propietario_empresa_id` se joinean con `ss_empresa_contratista` en la query cruda. Sin embargo, el **modelo EF** de `SsEquipo.PropietarioEmpresa` fue cambiado a `Contributor` (2026-05-05) — el SQL de bandeja aún usa `ss_empresa_contratista` directamente y funciona porque los IDs son compartidos.

### 5c. EstadoCalc (badge habilitación worker)

```csharp
itemsEmoIds = ss_item_trabajador WHERE nombre CONTAINS "EMO"  // ids 4 y 25

EstadoCalc =
  (ss_hab_trabajador.Any(Estado IN {Falta,Rechazado,Vencido}
       AND NOT (Casa AND itemsEmoIds))
   OR (Casa AND NOT worker_emos.Any(Activo AND Estado IN {Vigente,Convalidado})))
  ? "No Autorizado"
  : ss_hab_trabajador.Any(Estado == "En Plazo"
      AND NOT (Casa AND itemsEmoIds))
  ? "Autorizado Temporalmente"
  : "Habilitado"
```

### 5d. InicializarEntregablesAsync

Crea registros `Estado="Falta"` filtrando en orden: `AplicaA` → `AplicaCategoria` → `AplicaObraOficina` → `ExcluyeObraOficina` → `ExcluyeCategoriaContratista`. Caso especial: Casa+Practicante omite `ItemVidaLey`. No toca `ss_hab_worker_proyecto`.

### 5e. AprobarInduccionAsync (privado en InduccionRepository)

Al aprobar una inducción:
1. `ss_induccion.estado` → `"REALIZADA"`
2. Sentinel `2040-12-31 UTC` via `HabilitacionDateHelper.ResolverVigencia(false, "Aprobado", null)` asignado a **todos** los ítems que se aprueban
3. Siempre aprueba `ItemInduccionObra` (id=12) en `ss_hab_trabajador`
4. Si `contributor.es_abril = true`: también aprueba ids 5, 6, 8, 10
5. Busca `WorkerProyecto` donde `WorkerId + ProyectoId` **sin filtro `FechaFin`** → marca `InduccionCompletada=true`, `FechaInduccion=hoy`
6. `SaveChangesAsync` lo llama el método público (`AprobarAsync` / `AprobarBatchAsync`)

### 5f. CambiarObraAsync — lógica de reset

Al cambiar de proyecto:
1. Consulta `WorkerProyecto.AnyAsync(WorkerId + NuevoProyectoId + InduccionCompletada=true)` — sin filtro `FechaFin`
2. Si ya indujo en el nuevo proyecto → **NO** resetea ítem 12, **NO** envía email a coord SSOMA
3. Si no indujo → resetea `ItemInduccionObra` a `"Falta"` + envía email
4. `esCambioEmpresa` (solo Casa): resetea SCTR/VidaLey/CertAptitud independientemente del punto 1
5. Sincroniza `ss_hab_worker_proyecto` solo si `!esContratista`

### 5g. GetTrabajadoresPorProgramarAsync

Fuente: **`ctx.WorkerProyecto`** (no `WorkerVinculacion`):
1. Filtra `ProyectoId == proyectoId && !InduccionCompletada` — **sin filtro `FechaFin`**
2. Si `empresaId.HasValue` → intersecta con `WorkerVinculacion WHERE EmpresaId == empresaId`
3. Empresa de cada worker: última `WorkerVinculacion` `ORDER BY CreatedAt DESC, Id DESC`
4. `yaIndujeroSet` (workers con `InduccionCompletada=true` para el proyecto) se computa pero no filtra la lista — alimenta campo `YaIndujo` en `InduccionTrabajadorDto` (siempre `false` porque el paso 1 ya excluye)

### 5h. WorkerProyecto (ss_hab_worker_proyecto) — reglas

- **`AgregarProyectoAsync` admite contratistas** (2026-05-05): ya no bloquea con 400. Si `ContrataCasa != "Casa"`, valida que exista una fila en `ss_empresa_proyecto` para (`EmpresaId de WorkerVinculacion activa`, `dto.ProyectoId`) — 400 si no hay entregables registrados. Si es Casa, pasa directo.
- Email en `AgregarProyectoAsync`: prefijo `[PRUEBA - NO TOMAR EN CUENTA]` solo para Casa; contratistas envían email sin prefijo (igual que `CambiarObraAsync`).
- `Worker` **no tiene** `EmpresaId` — obtenerla de `WorkerVinculacion WHERE fecha_fin IS NULL`.
- Unique partial index `(worker_id, proyecto_id) WHERE fecha_fin IS NULL`
- `CambiarObraAsync` / `ReingresoAsync`: sincronización de `WorkerProyecto` gateada con `!esContratista`
- `BajaAsync` / `BajaMasivaAsync`: cierran TODAS las filas activas
- Reactivar fila previa **preserva** `InduccionCompletada`, `FechaInduccion` y `EmpresaId` históricos

---

## 6. Endpoints — HabilitacionModule

```
# Auth contratistas
POST   /api/v1/habilitacion/auth/login
POST   /api/v1/habilitacion/auth/activar|solicitar-reset|reset-password
PATCH  /api/v1/habilitacion/auth/cambiar-password
GET    /api/v1/habilitacion/auth/empresas

# Empresas contratistas
GET/POST/PUT  /api/v1/habilitacion/empresas
POST          /api/v1/habilitacion/empresas/{id}/reenviar-activacion
GET           /api/v1/habilitacion/empresas/{id}/entregables?proyectoId=&mes=&anio=
PUT           /api/v1/habilitacion/empresas/{id}/entregables/{entregableId}
GET           /api/v1/habilitacion/empresas/{id}/proyectos-disponibles
POST          /api/v1/habilitacion/empresas/{id}/activar-proyecto
DELETE        /api/v1/habilitacion/empresas/{id}/desactivar-proyecto

# Catálogos
GET  /api/v1/habilitacion/catalogos/items-trabajador|items-empresa|items-equipo|criterios
GET  /api/v1/habilitacion/catalogos/areas        (público)
GET  /api/v1/habilitacion/catalogos/subareas     (público)
GET  /api/v1/habilitacion/proyectos              (lista activos desde Project legacy)

# Trabajadores
GET    /api/v1/habilitacion/trabajadores?search=&empresaId=&proyectoId=&estadoHabilitacion=&contratistaCasa=&soloRetirados=
GET    /api/v1/habilitacion/trabajadores/{id}
PUT    /api/v1/habilitacion/trabajadores/{id}
POST   /api/v1/habilitacion/trabajadores/{id}/inicializar
GET    /api/v1/habilitacion/trabajadores/{id}/entregables
PUT    /api/v1/habilitacion/trabajadores/{id}/entregables/{entregableId}
GET    /api/v1/habilitacion/trabajadores/entregables/{id}/versiones
PATCH  /api/v1/habilitacion/trabajadores/{id}/baja
PATCH  /api/v1/habilitacion/trabajadores/baja-masiva
PATCH  /api/v1/habilitacion/trabajadores/{id}/cambiar-obra
PATCH  /api/v1/habilitacion/trabajadores/{id}/reingreso
GET    /api/v1/habilitacion/trabajadores/{id}/eventos          [AllowAnonymous temporal]
POST   /api/v1/habilitacion/trabajadores/{id}/proyectos        [AllowAnonymous temporal]
GET    /api/v1/habilitacion/trabajadores/{id}/proyectos        [AllowAnonymous temporal]
DELETE /api/v1/habilitacion/trabajadores/{id}/proyectos/{pId}  [AllowAnonymous temporal]
PATCH  /api/v1/habilitacion/trabajadores/{id}/proyectos/{pId}/induccion  [AllowAnonymous temporal]

# Bandeja de aprobaciones
GET    /api/v1/habilitacion/bandeja?tipo=&proyectoId=&empresaId=&responsable=&page=&pageSize=
GET    /api/v1/habilitacion/bandeja/cursor?tipo=&proyectoId=&empresaId=&responsable=&cursor=&pageSize=
PATCH  /api/v1/habilitacion/bandeja/trabajador/{id}   body: { estado, obsAbril, vigencia }
PATCH  /api/v1/habilitacion/bandeja/empresa/{id}      body: { estado, obsAbril, vigencia }
PATCH  /api/v1/habilitacion/bandeja/equipo/{id}       body: { estado, obsAbril, vigencia }
PATCH  /api/v1/habilitacion/bandeja/induccion/{id}    sin body — llama AprobarAsync

# Inducciones
POST   /api/v1/habilitacion/inducciones               body: InduccionCreateDto { WorkerIds[], ProyectoId, EmpresaId?, FechaProgramada, TrabajoAltura, EquipoElectrico }
GET    /api/v1/habilitacion/inducciones?proyectoId=&empresaId=&estado=&fechaDesde=&fechaHasta=
GET    /api/v1/habilitacion/inducciones/trabajadores-por-programar?proyectoId=&empresaId=
PATCH  /api/v1/habilitacion/inducciones/{id}/aprobar
PATCH  /api/v1/habilitacion/inducciones/aprobar-batch  body: { ids: int[] }

# SCTR / Vida Ley
GET/POST  /api/v1/habilitacion/sctr-vidaley
PATCH     /api/v1/habilitacion/sctr-vidaley/{id}/aprobar
GET       /api/v1/habilitacion/sctr-vidaley/trabajadores-por-empresa?empresaId=&estadoSctr=&estadoVidaLey=
          estadoSctr/estadoVidaLey aceptan valores comma-separated (ej: "Falta,Vencido")

# Equipos
GET    /api/v1/habilitacion/equipos?proyectoId=&empresaId=&search=&activo=&page=&pageSize=
       → CONTRATISTA: ignora ?empresaId, fuerza empresaId del JWT claim
GET    /api/v1/habilitacion/equipos/{id}/entregables
GET    /api/v1/habilitacion/equipos/entregables/{id}/versiones     ← historial ss_hab_documento_version por hab_equipo_id
POST   /api/v1/habilitacion/equipos
PUT    /api/v1/habilitacion/equipos/{id}
PUT    /api/v1/habilitacion/equipos/entregables/{id}               body: { estado, vigencia, archivoUrl, obsAbril, obsContratista }

# Archivos
POST  /api/v1/habilitacion/archivos/subir   → { path, url }  — guardar `path` (ruta relativa), NO `url` (larga SharePoint)
GET   /api/v1/habilitacion/archivos/url?path=

# Otros
GET/POST/PUT/DELETE  /api/v1/habilitacion/reglas
GET                  /api/v1/habilitacion/auditoria
GET                  /api/v1/habilitacion/registros-modelo  (público)
```

---

## 7. Pitfalls críticos

### 7a. JOIN project — NUNCA projects
```sql
-- ✅ CORRECTO (tabla real en BD)
JOIN project p ON p.project_id = t.proyecto_id
SELECT p.project_description, p.project_id

-- ❌ INCORRECTO (tabla eliminada)
JOIN projects p ON p.id = t.proyecto_id
SELECT p.nombre
```
`projects` (plural) fue eliminada vía migración `SwitchProyectoFkToProjectLegacy`. Solo existe `project` (singular, PK `project_id`).

### 7b. CAST timestamp obligatorio en Dapper
Dapper mapea `timestamp` de PG a `DateTime?` en C#. Sin el cast explícito, columnas `date` o `DateOnly` no mapean correctamente:
```sql
CAST(ht.vigencia AS timestamp) as vigencia
CAST(i.fecha_programada AS timestamp) as vigencia
```
Aplica a todos los segmentos del UNION ALL en `BandejaRepository.SelectBase`.

### 7c. worker_vinculaciones — ORDER BY estable
`fecha_inicio` no es único. Para obtener la vinculación activa más reciente sin duplicar filas:
```sql
LEFT JOIN LATERAL (
    SELECT empresa_id, proyecto_id
    FROM worker_vinculaciones
    WHERE worker_id = w.id AND fecha_fin IS NULL
    ORDER BY created_at DESC, id DESC
    LIMIT 1
) wv ON TRUE
```
En EF: `.OrderByDescending(v => v.CreatedAt).ThenByDescending(v => v.Id).FirstOrDefault()`.

### 7d. contributor reemplazó companies
- `worker_vinculaciones.empresa_id` → `contributor.contributor_id`
- `ss_hab_empresa.empresa_id` → `contributor.contributor_id` (via `ss_empresa_contratista` para joins en bandeja)
- `ss_induccion.empresa_id` → `contributor.contributor_id` directamente
- `ss_sctr_vidaley.empresa_id` → `contributor.contributor_id`
- **`contributor` PK = `contributor_id`** (no `id`)
- Tabla `companies` eliminada. No usar ni referenciar.

### 7e. ss_hab_worker_proyecto — contratistas validados por ss_empresa_proyecto
```csharp
// AgregarProyectoAsync — lógica actual
if (esContratista)
{
    var empresaId = await ctx.WorkerVinculacion
        .Where(v => v.WorkerId == workerId && v.FechaFin == null)
        .Select(v => v.EmpresaId).FirstOrDefaultAsync();
    var tieneEntregables = empresaId.HasValue &&
        await ctx.SsEmpresaProyecto
            .AnyAsync(ep => ep.EmpresaId == empresaId.Value && ep.ProyectoId == dto.ProyectoId);
    if (!tieneEntregables)
        throw new AbrilException("La empresa no tiene entregables registrados en este proyecto.", 400);
}
// CambiarObraAsync / SincronizarWorkerProyectoCambioAsync: solo Casa
if (!esContratista) await SincronizarWorkerProyectoCambioAsync(...);
```
`Worker` no tiene `EmpresaId` — siempre leer de `WorkerVinculacion` activa.

### 7f. Sentinel 2040 para requiere_vigencia=false
```csharp
// Siempre via helper — NO construir la fecha inline
var sentinel = HabilitacionDateHelper.ResolverVigencia(false, "Aprobado", null);
// Retorna: DateTime.SpecifyKind(new DateOnly(2040, 12, 31).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
```
El helper devuelve **2040** (no 2030). Aplica a items 12, 5, 6, 8, 10 al aprobar inducción.

### 7g. FechaFin sin filtro en inducciones
Tanto `AprobarInduccionAsync` como `GetTrabajadoresPorProgramarAsync` consultan `WorkerProyecto` **sin** `wp.FechaFin == null`. Un worker retirado del proyecto tras inducción no debe perder el estado `InduccionCompletada`.

### 7h. DateTime UTC obligatorio para columnas timestamptz
```csharp
// ❌ Npgsql rechaza Kind=Unspecified
entity.Fecha = dto.Fecha;

// ✅ siempre AsUtc
entity.Fecha = HabilitacionDateHelper.AsUtc(dto.Fecha);
```
JSON sin `Z` deserializa como `Kind=Unspecified` → Npgsql tira `"Cannot write DateTime with Kind=Unspecified to PostgreSQL type 'timestamp with time zone'"`.

### 7i. Patch semantics en entregables
Al aprobar/rechazar, solo asignar campos `when not null`:
```csharp
if (dto.ArchivoUrl is not null) entity.ArchivoUrl = dto.ArchivoUrl;
```
Pisar con null borra el documento ya subido.

### 7j. LatestVinc no filtra por fecha_fin
`GetWorkersHabilitacionAsync` usa `LatestVinc` = última vinculación sin importar si está cerrada. Permite ver empresa/proyecto de workers retirados.

### 7k. SharePointHabService — Singleton
El token OAuth2 y el `driveId` del sitio se cachean en la instancia. Registrar como `AddSingleton`.

### 7l. Tablas creadas manualmente (sin migración EF efectiva)
- `worker_eventos` — `DbSet` con `HasColumnType("jsonb")` para `Datos`
- `cat_subarea` — `DbSet` declarado pero sin migración
- `equipo_electrico` en `ss_induccion` — columna manual, migración vacía `AddInduccionEquipoElectrico`
Antes de `dotnet ef migrations add`, revisar el archivo generado y limpiar operaciones ya aplicadas en BD.

### 7m. BandejaRepository usa NpgsqlConnection directa
`BandejaRepository` abre conexión PG directa (no EF) para el UNION ALL. La connection string viene de `_configuration["Database:PostgreSQL"]`. Solo funciona en modo PostgreSQL.

### 7n. ProjectService acoplamiento con ISunatService
Mitigado: factory null-safe en Program.cs. Solo `/company-lookup/{ruc}` usa Sunat en runtime.

---

## 8. Roles del sistema

| role_id | descripción |
|---------|-------------|
| 1 | ADMINISTRADOR DEL SISTEMA |
| 2 | ADMINISTRADOR DE UDP |
| 3 | USUARIO DE UDP |
| 4 | ADMINISTRADOR DE RESIDENTES |
| 5 | RESIDENTE |
| 6 | USUARIO DE COSTOS Y PRESUPUESTOS |
| 7 | ADMINISTRADOR DE COSTOS Y PRESUPUESTOS |
| 8 | USUARIO DE ARQUITECTURA COMERCIAL |
| 9 | ADMINISTRADOR SSOMA |
| 10 | ADMINISTRADOR ADMINISTRACION |

Roles aprobadores habilitación: `["ADMINISTRADOR SSOMA", "ADMINISTRADOR DE UDP", "ADMINISTRADOR ADMINISTRACION"]`

---

## 9. ArquitecturaComercial — detalle

**Ubicación:** capa tradicional — `Controllers/ArquitecturaComercialController.cs`, `Application/Services/ArquitecturaComercialService.cs`, `Infrastructure/Repositories/ArquitecturaComercialRepository.cs`.

### 9a. Tablas propias (prefijo `ac_`)

| Tabla | Entidad | Rol |
|-------|---------|-----|
| `ac_actividades` | `AcActividad` | Actividad asignada a un proyecto |
| `ac_etapas` | `AcEtapa` | Catálogo de etapas |
| `ac_actividades_plantilla` | `AcActividadPlantilla` | Plantilla para inicializar actividades de un proyecto nuevo |
| `ac_categorias` | `AcCategoria` | Catálogo de categorías |
| `ac_especialidades` | `AcEspecialidad` | Catálogo de especialidades |

Tablas compartidas: `project` (= "Proyecto" en AC, PK `project_id`) y `workers` (encargados).

### 9b. AcActividad — campos

`id`, `project_id` (FK→project), `user_id` (FK→workers, nullable), `nombre`, `tipo`, `etapa_id` (FK→ac_etapas, nullable), `prioridad`, `estado`, `activo` (bool), `indice` (int?), `inicio_programado` (DateOnly?), `fin_programado` (DateOnly?), `inicio_efectivo` (DateOnly?), `fin_efectivo` (DateOnly?), `observaciones`.

Estado calculado dinámicamente al devolver el DTO (`ComputeEstado`): `VACIO` → `PENDIENTE` → `EN_PROCESO` → `VENCIDO` → `CULMINADO`. El campo `estado` en BD almacena el estado pero el DTO siempre lo recalcula.

### 9c. Endpoints AC

```
GET    /api/v1/arquitectura-comercial/actividades           → lista paginada + filtros
POST   /api/v1/arquitectura-comercial/actividades           → crea AcActividad (Estado="VACIO", Activo=true, Indice=max+1) → 201 + ActividadListItemDTO
PUT    /api/v1/arquitectura-comercial/actividades/{id}      → sobreescribe 9 campos editables → 200 + ActividadListItemDTO
DELETE /api/v1/arquitectura-comercial/actividades/{id}      → hard delete → 204
PATCH  /api/v1/arquitectura-comercial/actividades/{id}      → patch parcial (campos opcionales por nombre)
POST   /api/v1/arquitectura-comercial/actividades/reasignar-encargado
POST   /api/v1/arquitectura-comercial/actividades/generar

GET    /api/v1/arquitectura-comercial/proyectos-con-actividades
GET    /api/v1/arquitectura-comercial/supervisores-ac
GET    /api/v1/arquitectura-comercial/filtros
GET    /api/v1/arquitectura-comercial/gantt
GET    /api/v1/arquitectura-comercial/plantilla
POST   /api/v1/arquitectura-comercial/plantilla
PATCH  /api/v1/arquitectura-comercial/plantilla/{id}
GET    /api/v1/arquitectura-comercial/categorias
GET    /api/v1/arquitectura-comercial/especialidades
GET    /api/v1/arquitectura-comercial/etapas
```

### 9d. DTOs clave

| DTO | Uso |
|-----|-----|
| `AcActividadCreateDTO` | POST actividades — Nombre, Tipo, ProjectId, EtapaId?, UserId?, InicioProgramado?, FinProgramado?, Observaciones? |
| `AcActividadUpdateDTO` | PUT actividades/{id} — mismo shape sin ProjectId ni Indice, más InicioEfectivo/FinEfectivo |
| `ActividadListItemDTO` | Retorno de GET/POST/PUT — incluye estado calculado, retraso, EtapaNombre, ResponsableNombre |

---

## 10. Trabajo pendiente

### Alta prioridad
- Quitar `[AllowAnonymous]` de los 4 endpoints `/trabajadores/{id}/proyectos*`, `GET /eventos` y endpoints SSOMA
- Quitar prefijo `[PRUEBA - NO TOMAR EN CUENTA]` de subjects de correos antes de prod (en `CambiarObraAsync`, `ReingresoAsync`, correos Vida Ley)
- Crear primer usuario admin en `app_user`
- Deploy a producción
- 42 empresas SharePoint con IDs 1656+ pendientes de migrar a `contributor`
- Eliminar `id_sharepoint` de `contributor` cuando migración SharePoint esté completa

### Media prioridad
- Empresas contratistas: 1.591 vinculaciones sin empresa
- `tipo_emo_id`: 813 EMOs migrados tienen NULL
- Eliminar `id_trabajador` de `workers` tras confirmar migración completa
- Multi-proyecto FASE 4: `BandejaRepository`, listados, EMO, SCTR y Vida Ley aún razonan sobre `worker_vinculaciones` (1-activa). Evaluar si pivotar a `ss_hab_worker_proyecto` para workers Casa en N proyectos
- `InicializarEntregablesAsync` no crea fila inicial en `ss_hab_worker_proyecto` — considerar parámetro `proyectoInicialId?` en `POST /workers`
- Separar `ISunatLookupService` de `ProjectService` para eliminar el acoplamiento de DI

### Baja prioridad
- 8 EMOs sin match de DNI — insertar manualmente
- 24 vinculaciones sin proyecto
- `ReminderController` aún usa `Environment.GetEnvironmentVariable` para CronSecret — migrar a `IConfiguration`
- FluentValidation 11.3.1 usa API deprecated — migrar cuando bumpeemos v12
- Refactor `Sunat:Token` headers en Program.cs dentro del `if` null-safe
