# CONTEXT.md — Abril Backend
> Última actualización: 2026-05-25 — SCTR: auto-aprobación Abril, SctrTrabajadorEstadoDto enriquecido, GetTrabajadoresPorEmpresa por vinculación

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
| Módulo | Registro DI | Contenido |
|--------|-------------|-----------|
| `HabilitacionModule` | `AddHabilitacionModule` | Principal activo — ver sección 5 |
| `SsomaModule` | `AddSsomaModule` | EMO, programación, alertas automáticas, clínica, reportes SUNAFIL |
| `AuthModule` | `AddAuthModule` | MicrosoftLogin, MicrosoftProfile, ContractorCredentials, RoleFeature, UserFeature |
| `ContractorsModule` | `AddContractorsModule` | ContractorRegistration, ContractorManagement |
| `CostsModule` | `AddCostsModule` | Adjudicaciones (contrato completo), WorkItem, StaffProjectEmail, ProjectLink |
| `ConfigurationModule` | `AddConfigurationModule` | ProjectFeature (CRUD proyectos AC) |
| `GestionAdministrativaModule` | `AddGestionAdministrativaModule` | SolicitudSalidas, GestionSalidas, Lugares, MotivosSalida |
| `MejoraContinuaModule` | `AddMejoraContinuaModule` | LessonsLearned, AreasYSubareas, PsssTemplate, Relations |
| `UnidadDeProyectosModule` | `AddUnidadDeProyectosModule` | LessonsLearnedDashboard |

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
| `Contributor` | `contributor` | `contributor_id` | Entidad unificada de empresas. Reemplazó `companies` (eliminada) y `ss_empresa_contratista` (eliminada 2026-05-23). Incluye `EsAbril` (bool), `IdSharepoint` (int?, temporal), `ContributorNombreComercial` (varchar 255), `SpPasswordTemp` (varchar 255, usado para migración masiva). En `Features/CostsModule/Shared/Models/Contributor.cs`. |
| `Worker` | `workers` | `id` | Personal con columnas explícitas `[Column("...")]`. No snake_case automático. Tiene `PersonId int?` (FK→`person`) y `ContributorId int?` (FK→`contributor`) con nav properties `Person?` y `Contributor?` (agregadas 2026-05-11). `EmpresaId` NO existe en el modelo — siempre leer de `WorkerVinculacion`. |
| `WorkerVinculacion` | `worker_vinculaciones` | `id` | 1 activa por worker (`fecha_fin IS NULL`). Para empresa y proyecto actual del worker. |
| `WorkerProyecto` | `ss_hab_worker_proyecto` | `id` | Multi-proyecto **solo Casa**. N activos en paralelo. Unique partial index `(worker_id, proyecto_id) WHERE fecha_fin IS NULL`. |
| `SsInduccion` | `ss_induccion` | `id` | `empresa_id` → `contributor.contributor_id` (no `ss_empresa_contratista`). Columnas manuales: `ingreso_confirmado` (bool NOT NULL DEFAULT false), `fecha_ingreso` (timestamptz). |
| `SsTareo` | `ss_tareo` | `id` | Tabla manual (sin migración EF). `proyecto_id` → `project.project_id`. `fecha` (DateOnly). `observaciones` (text?). `creado_por` (int?, FK→app_user). Unique implícito en (`proyecto_id`, `fecha`). |
| `SsTareoPartida` | `ss_tareo_partida` | `id` | Catálogo fijo de 17 partidas Casa. Columnas: `nombre`, `orden` (int), `activo` (bool). Tabla manual (sin migración EF). |
| `SsTareoDetalleCasa` | `ss_tareo_detalle_casa` | `id` | Detalle de tareo para personal Casa. `tareo_id` → `ss_tareo.id`, `partida_id` → `ss_tareo_partida.id`, `cantidad_personas` (int). Tabla manual. |
| `SsTareoDetalleContratista` | `ss_tareo_detalle_contratista` | `id` | Detalle de tareo para personal contratista. `tareo_id` → `ss_tareo.id`, `empresa_id` → `contributor.contributor_id`, `cantidad_personas` (int). Tabla manual. |
| `SsHabTrabajador` | `ss_hab_trabajador` | `id` | Entregables por worker. |
| `SsHabEmpresa` | `ss_hab_empresa` | `id` | `proyecto_id` → `project.project_id`. `empresa_id` → `contributor.contributor_id`. |
| `SsEquipo` | `ss_equipo` | `id` | `proyecto_id` → `project.project_id`. `propietario_empresa_id` → `contributor.contributor_id` (nav property `Contributor? PropietarioEmpresa`). |
| `SsHabEquipo` | `ss_hab_equipo` | `id` | Entregables por equipo. Tiene `ObsContratista` (agregada directamente en BD). `archivo_url` es `text` (fue `varchar(1000)` — alterada manualmente). |
| `SsItemTrabajador` | `ss_item_trabajador` | `id` | Catálogo de entregables con reglas. |
| `WorkerEvento` | `worker_eventos` | `id` | Creada manualmente en BD (sin migración EF). |
| `CatSubarea` | `cat_subarea` | `id` | Creada manualmente en BD (sin migración EF). |
| `SsTrabajadorRestringido` | `ss_trabajador_restringido` | `id` | Blacklist de trabajadores. `Dni varchar(15)`, `WorkerId int?`, `Activo bool`. UNIQUE(dni). SQL en `Database/migrations/ss_trabajador_restringido.sql`. |
| `CatCategoria` | `cat_categoria` | `id` | Catálogo de categorías de workers. `Nombre`, `Orden`, `Activo`. DbSet registrado — crear tabla manualmente en BD. |
| `CatOcupacion` | `cat_ocupacion` | `id` | Catálogo de ocupaciones de workers. `Nombre`, `Orden`, `Activo`. DbSet registrado — crear tabla manualmente en BD. |
| `User` | `app_user` | — | Override en `ConfigurePostgreSQL` (`User` es palabra reservada PG). |
| `ContractorEmail` | `contractor_email` | `contractor_email_id` | Email por contratista. Tiene `UserId int?` (FK→`app_user`) para vincular con cuenta del sistema. La FK `fk_contractor_email_user_user_id` se agrega con la migración `MigrateResetTokenToUserId`. |
| `SsResetToken` | `ss_reset_token` | — | Token de reset/activación. `UserId int?` (FK→`app_user`). **`EmpresaId` eliminado** (migración `RemoveSsEmpresaContratista`). |

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
- Empresa via `LEFT JOIN contributor ec ON ec.contributor_id = wv.empresa_id`
- Proyecto nombre/id via `LEFT JOIN project p ON p.project_id = wv.proyecto_id` + `p.project_description`

**EMPRESA** (`ss_hab_empresa WHERE estado='Enviado'`):
- `CAST(he.vigencia AS timestamp)`
- `JOIN project p ON p.project_id = he.proyecto_id` + `p.project_description`
- Empresa via `JOIN contributor ec ON ec.contributor_id = he.empresa_id` + `ec.contributor_name`

**EQUIPO** (`ss_hab_equipo WHERE estado='Enviado'`):
- `CAST(heq.vigencia AS timestamp)`
- `JOIN project p ON p.project_id = eq.proyecto_id` + `p.project_description`
- Empresa via `LEFT JOIN contributor ec ON ec.contributor_id = eq.propietario_empresa_id` + `ec.contributor_name`

**INDUCCION** (`ss_induccion WHERE estado='PROGRAMADA'`):
- `vigencia = NULL` (la vigencia real la asigna AprobarInduccionAsync al aprobar)
- `JOIN contributor c ON c.contributor_id = i.empresa_id` + `c.contributor_name`
- `JOIN project p ON p.project_id = i.proyecto_id` + `p.project_description`
- Entidad nombre: `COALESCE(per.full_name, '')` via `LEFT JOIN person per`

> **2026-05-23**: Los 4 segmentos del UNION ALL usan `contributor` uniformemente — asimetría eliminada al borrar `ss_empresa_contratista`.

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
POST   /api/v1/habilitacion/auth/validar-migracion   body: { ruc, spPassword } → { nombreComercial, razonSocial }  [AllowAnonymous]
POST   /api/v1/habilitacion/auth/activar-migracion   body: { ruc, spPassword, email, password } → crea app_user + contractor_user + limpia sp_password_temp  [AllowAnonymous]

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
GET  /api/v1/habilitacion/catalogos/subareas     (público, ?area= opcional)
GET  /api/v1/habilitacion/catalogos/categorias   (público)
GET  /api/v1/habilitacion/catalogos/ocupaciones  (público)
GET  /api/v1/habilitacion/proyectos              (lista activos desde Project legacy)

# Trabajadores restringidos
GET    /api/v1/habilitacion/restringidos?soloActivos=&dni=   (cualquier usuario autenticado)
POST   /api/v1/habilitacion/restringidos         body: { dni?, apellidoNombre?, motivo, proyectoOrigen?, restringidoPor?, fechaRestriccion? }  [solo ADMINISTRADOR SSOMA / ADMINISTRADOR ADMINISTRACION]
DELETE /api/v1/habilitacion/restringidos/{id}    desactiva (soft delete) [solo ADMINISTRADOR SSOMA / ADMINISTRADOR ADMINISTRACION]

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
PATCH  /api/v1/habilitacion/bandeja/bulk-aprobar      body: { ids: int[], tipo: "TRABAJADOR"|"EMPRESA"|"EQUIPO"|"INDUCCION" }
                                                       respuesta: { procesados: int, noEncontrados: int[] }
                                                       — itera los unitarios existentes; INDUCCION usa AprobarBatchAsync

# Inducciones
POST   /api/v1/habilitacion/inducciones               body: InduccionCreateDto { WorkerIds[], ProyectoId, EmpresaId?, FechaProgramada, TrabajoAltura, EquipoElectrico }
GET    /api/v1/habilitacion/inducciones?proyectoId=&empresaId=&estado=&fechaDesde=&fechaHasta=
       → CONTRATISTA: ignora ?empresaId, fuerza empresaId del JWT claim (igual que EquiposController)
       → Retorna InduccionListDto[] con IngresoConfirmado y FechaIngreso (para badges frontend)
GET    /api/v1/habilitacion/inducciones/trabajadores-por-programar?proyectoId=&empresaId=&search=
GET    /api/v1/inducciones/trabajadores-por-programar?proyectoId=&empresaId=&search=   ← alias (misma action, ruta alternativa)
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

# Control de Acceso
GET   /api/v1/habilitacion/control-acceso/consulta?search=&proyectoId=
      → busca workers; DNI exacto si search=8 dígitos, LIKE por nombre si no; filtra por proyectoId si viene
      → esOficinaCentral (proyectoId==36): solo evalúa SCTR (ItemId=11, Aprobado, Vigencia>now)
      → resto: evalúa todos los entregables; incluye lista completa Entregables[]
GET   /api/v1/habilitacion/control-acceso/no-autorizados?proyectoId=
      → workers del proyecto con algún entregable en {Falta, Rechazado, Vencido}
GET   /api/v1/habilitacion/control-acceso/oficina-central?proyectoId=
      → workers con ObraOficina ∈ {"Oficina Central","Staff"} con SCTR vigente
GET   /api/v1/habilitacion/control-acceso/inducciones-hoy                             [AllowAnonymous temporal]
      → ss_induccion WHERE estado='PROGRAMADA' AND fecha_programada ∈ [hoyLima, límite)
      → límite = mañanaLima si hora Lima < 12; pasadoLima si hora Lima ≥ 12 (look-ahead)
      → sin filtro por proyectoId — devuelve todas las inducciones del día
      → incluye IngresoConfirmado, FechaIngreso
POST  /api/v1/habilitacion/control-acceso/inducciones/{id}/confirmar-ingreso
      → marca ingreso_confirmado=true, fecha_ingreso=DateTime.UtcNow en ss_induccion
GET   /api/v1/habilitacion/control-acceso/tareo/partidas
      → ss_tareo_partida WHERE activo=true ORDER BY orden. DTO: { id, nombre }
GET   /api/v1/habilitacion/control-acceso/tareo/empresas?proyectoId={id}
      → empresas contratistas (EsAbril=false) con workers vinculados al proyecto (FechaFin IS NULL). DTO: { empresaId, empresaNombre }
GET   /api/v1/habilitacion/control-acceso/tareo?proyectoId=&fecha=YYYY-MM-DD
      → retorna cabecera + detallesCasa[] (con partidaNombre) + detallesContratista[] (con empresaNombre)
POST  /api/v1/habilitacion/control-acceso/tareo       body: TareoCreateDto { ProyectoId, Fecha, Observaciones, DetallesCasa[], DetallesContratista[] }
      → crea cabecera e inserta detalles. 409 si ya existe (ProyectoId, Fecha)
PUT   /api/v1/habilitacion/control-acceso/tareo/{id}  body: TareoCreateDto
      → actualiza cabecera, borra detalles anteriores e inserta los nuevos

# Archivos
POST  /api/v1/habilitacion/archivos/subir   → { path, url }  — guardar `path` (ruta relativa), NO `url` (URL firmada que expira)
      ⚠️ En el frontend, SIEMPRE usar res.path al guardar el resultado del upload (empresa.ts, sctr-subir.ts, registro-empresa.ts corregidos 2026-05-19)
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

### 7d. contributor reemplazó companies y ss_empresa_contratista
- `worker_vinculaciones.empresa_id` → `contributor.contributor_id`
- `ss_hab_empresa.empresa_id` → `contributor.contributor_id`
- `ss_induccion.empresa_id` → `contributor.contributor_id`
- `ss_sctr_vidaley.empresa_id` → `contributor.contributor_id`
- `ss_empresa_proyecto.empresa_id` → `contributor.contributor_id`
- `ss_hab_bloqueo_log.empresa_solicitante_id / empresa_propietaria_id` → `contributor.contributor_id`
- `ss_eval_supervisor.empresa_id` → `contributor.contributor_id`
- **`contributor` PK = `contributor_id`** (no `id`)
- Tablas `companies` y `ss_empresa_contratista` **eliminadas**. No usar ni referenciar.

### 7e. ss_hab_worker_proyecto — contratistas validados por ss_empresa_proyecto

**IDs en juego (post-migración 2026-05-23 — todos uniformes):**
| Tabla | `EmpresaId` FK apunta a |
|---|---|
| `worker_vinculaciones` | `contributor.contributor_id` |
| `ss_empresa_proyecto` | `contributor.contributor_id` (migrado de ss_empresa_contratista) |
| `ss_hab_worker_proyecto` | `contributor.contributor_id` |

No hay traducción vía IdLegacy — la comparación es directa:

```csharp
// AgregarProyectoAsync — lógica actual (post-migración 2026-05-23)
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

### 7k. SharePointHabService — Singleton con cache por biblioteca
El token OAuth2 se cachea en la instancia. El driveId ya no es un string único — es un `ConcurrentDictionary<string, string>` keyed por `libraryId` (o `"default"` para el drive predeterminado). Registrar como `AddSingleton`. La resolución de libraryId usa el contexto/path: "trabajadores" → `TrabajadoresLibraryId`, "empresas" → `EmpresaLibraryId`, "equipos" → `EquiposLibraryId`, cualquier otro → `null` (fallback drive predeterminado).

---

## 8. Sesión 2026-05-18 (segunda parte) — flujo auth contratistas

### Homologación → auto-envío de credenciales

`ContractorManagementService.Approve()` ahora incluye la lógica de `SendCredentials`: genera token de activación, lo guarda en `contractor.activation_token` y envía el email inmediatamente. Si el contratista no tiene emails registrados, la aprobación igual completa sin error.

### ContractorCredentialsRepository.Create() — tolera app_user existente

Antes lanzaba `AbrilException("Ya existe un usuario con este correo electrónico.", 400)`.  
Ahora: si el `app_user` ya existe, reutiliza el usuario y actualiza la contraseña. Si no existe, crea el registro. En ambos casos verifica con `AnyAsync` antes de insertar `ContractorUser` y `UserRole` para evitar duplicados.

### ContratistaAuthService — allowedFeatures desde BD (por roles del usuario)

`GenerarTokenDto` recibe `List<string> allowedFeatures` como parámetro (antes era array hardcodeado).  
Helper privado — **actualizado 2026-05-24** para cargar features de los roles asignados al usuario concreto en lugar del rol global `CONTRATISTA`:
```csharp
private static Task<List<string>> GetContratistasFeatureKeysAsync(AppDbContext ctx, int userId)
    => ctx.Database.SqlQuery<string>($"""
        SELECT DISTINCT f.feature_key
        FROM feature f
        JOIN role_feature rf ON rf.feature_id = f.feature_id
        JOIN user_role ur ON ur.role_id = rf.role_id
        WHERE ur.user_id = {userId}
          AND ur.active = true
          AND ur.state = true
        """).ToListAsync();
```
Llamado desde `LoginAsync` y `ActivarCuentaAsync` pasando `user.UserId`. Los features devueltos van en el **body del response** (`ContratistaTokenDto.AllowedFeatures`), no en el JWT. Para agregar/quitar features a un contratista concreto: modificar sus filas en `user_role` + `role_feature` en BD.

### ContratistaAuthService — claim empresaId usa ContributorId

```csharp
// ANTES
new Claim("empresaId", contractor.ContractorId.ToString())
// AHORA
new Claim("empresaId", contractor.ContributorId.ToString())
```

### ContratistaAuthService — ILogger y debug BCrypt

Inyectado `ILogger<ContratistaAuthService>`. Log temporal en `LoginAsync` tras BCrypt.Verify para diagnóstico. **Eliminar antes de merge a master.**

### FrontendSettings — ContractorCredentialsUrl

Añadida en `appsettings.Production.json` y `appsettings.Local.json`:
```json
"ContractorCredentialsUrl": "https://abril-frontend.onrender.com/auth/contractor-credentials"
```
`appsettings.Local.json` tiene temporalmente `http://localhost:4200/auth/contractor-credentials` — **revertir antes de merge a master**.  
La propiedad existía en `FrontendSettings.cs` pero faltaba en los archivos de config.

---

### 7p. ~~IdLegacy~~ — OBSOLETO (2026-05-23)

`ss_empresa_contratista` eliminada. No hay IdLegacy. `ContractorManagementRepository.Approve()` ya no crea filas en esa tabla.

### 7q. EmpresaContratistaRepository.GetProyectosAsync — lookup directo sobre contributor

`GetProyectosAsync(empresaId)`: `empresaId` es siempre `contributor.contributor_id`. La consulta es directa sobre `ss_empresa_proyecto.empresa_id` (que ahora también apunta a `contributor_id`). No hay doble lookup ni fallback vía IdLegacy.

### 7r. EmpresaContratistaController.Create — validación RUC en contributor

Al crear una empresa contratista, el endpoint verifica que el RUC no exista ya en `contributor`. Si existe → 400. La creación genera `Contributor` + `Contractor` (StateId=2 Aprobado) + filas `ContractorEmail`. No hay `IdLegacy` ni referencias a `ss_empresa_contratista`.

### 7l. Tablas y columnas creadas manualmente (sin migración EF efectiva)
- `worker_eventos` — `DbSet` con `HasColumnType("jsonb")` para `Datos`
- `cat_subarea` — `DbSet` declarado pero sin migración
- `equipo_electrico` en `ss_induccion` — columna manual, migración vacía `AddInduccionEquipoElectrico`
- `obs_contratista` en `ss_hab_equipo` — columna manual, NO tiene migración EF
- `ingreso_confirmado` (bool NOT NULL DEFAULT false) en `ss_induccion` — columna manual; mapeada en `InduccionListDto.IngresoConfirmado` (2026-05-19)
- `fecha_ingreso` (timestamptz) en `ss_induccion` — columna manual; mapeada en `InduccionListDto.FechaIngreso` (2026-05-19)
- `ss_tareo` — tabla completa creada manualmente; `DbSet<SsTareo>` registrado en AppDbContext
- `ss_hab_equipo.archivo_url` fue `varchar(1000)` en BD — alterada con `ALTER TABLE ss_hab_equipo ALTER COLUMN archivo_url TYPE text;`; modelo EF lleva `[Column(TypeName = "text")]`
Antes de `dotnet ef migrations add`, revisar el archivo generado y limpiar operaciones ya aplicadas en BD.

### 7m. BandejaRepository usa NpgsqlConnection directa
`BandejaRepository` abre conexión PG directa (no EF) para el UNION ALL. La connection string viene de `_configuration["Database:PostgreSQL"]`. Solo funciona en modo PostgreSQL.

### 7n. ProjectService acoplamiento con ISunatService
Mitigado: factory null-safe en Program.cs. Solo `/company-lookup/{ruc}` usa Sunat en runtime.

### 7o. DocumentoHelper — validación DNI / CE
`Shared/Helpers/DocumentoHelper.cs` centraliza la validación de documentos de identidad.
- **DNI**: `^\d{8}$` — exactamente 8 dígitos numéricos
- **CE**: `^[A-Za-z0-9]{6,12}$` — 6-12 caracteres alfanuméricos sin espacios
- `WorkerCreateDto.TipoDocumento` (string?) — solo transporte para validación, **no persiste en BD**
- Si `TipoDocumento` es null, acepta cualquier formato válido (DNI o CE)
- Todas las comparaciones de documentos en DB usan `.ToUpper()` en ambos lados (case-insensitive para CE con letras)
- El campo `workers.dni` es `text` sin límite — ya soporta CE. `ss_trabajador_restringido.dni` es `varchar(15)` — también suficiente.

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
| — | GESTOR DE ARQUITECTURA COMERCIAL *(pendiente insertar en BD)* |

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

`id`, `project_id` (FK→project), `user_id` (FK→workers, nullable), `user_id2` (FK→workers, nullable — responsable 2), `nombre`, `tipo`, `etapa_id` (FK→ac_etapas, nullable), `categoria_id` (FK→ac_categorias, nullable — creada manualmente en BD), `especialidad_id` (FK→ac_especialidades, nullable — creada manualmente en BD), `prioridad`, `estado`, `activo` (bool), `orden` (int?), `spi` (numeric 5,2), `inicio_programado` (DateOnly?), `fin_programado` (DateOnly?), `inicio_efectivo` (DateOnly?), `fin_efectivo` (DateOnly?), `observaciones`.

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
| `AcActividadCreateDTO` | POST actividades — Nombre, Tipo, ProjectId, EtapaId?, UserId?, UserId2?, CategoriaId?, EspecialidadId?, InicioProgramado?, FinProgramado?, Observaciones? |
| `AcActividadUpdateDTO` | PUT actividades/{id} — mismo shape sin ProjectId, más InicioEfectivo/FinEfectivo, UserId2?, CategoriaId?, EspecialidadId? |
| `ActividadListItemDTO` | Retorno de GET/POST/PUT — incluye estado calculado, retraso, EtapaNombre, ResponsableNombre, ResponsableNombre2, UserId2, **PartidaDeControl** (=campo `tipo` en BD), CategoriaId, CategoriaNombre, EspecialidadId, EspecialidadNombre |

---

## 10. Control de Acceso — notas de implementación

**Repositorio:** `ControlAccesoRepository` — inyecta `IDbContextFactory<AppDbContext>` + `IConfiguration`.

**OficinaCentral:** `appsettings.json → "OficinaCentral": { "ProjectId": 36 }`. Cuando `proyectoId == 36`, `BuildDtosAsync` evalúa **solo** SCTR (ItemId=11). El resto de proyectos evalúa todos los entregables.

**BuildDtosAsync (batch helper privado):**
1. Carga `WorkerVinculacion` activas (`FechaFin == null`) → empresa y proyecto por worker
2. Carga `Contributor` por `EmpresaId` → `EmpresaNombre`, `EmpresaActiva`
3. Carga `Project` por `ProyectoId` → `ProyectoNombre`
4. Carga catálogo `SsItemTrabajador` completo
5. Carga todos los `SsHabTrabajador` de los workers en lote
6. **Para workers Casa** (`ContrataCasa == "Casa"`): pre-carga `WorkerEmo` activos (`Activo=true`) desde `worker_emos`, toma el más reciente por `FechaEmo DESC, Id DESC`. Sintetiza entregable id=4 ("Certificado de Aptitud (EMO)"): `Aptitud=="Apto"` → Estado="Aprobado"; cualquier otro caso o sin EMO → Estado="Falta". Vigencia = `FechaVencimiento`.
7. Por worker: `DocumentosFaltantes`, `DocumentosPorVencer`, `Entregables[]` completo

**Regla de vigencia (aplicada a ss_hab_trabajador y al EMO sintetizado):**
- `vigencia > hoy + 7 días` → vigente (no aparece en faltantes ni porVencer)
- `hoy < vigencia ≤ hoy + 7 días` → `DocumentosPorVencer`
- `vigencia ≤ hoy` → `DocumentosFaltantes`, `hasPendientes = true`

**ControlAccesoWorkerDto:**
- `EstadoHabilitacion`: `"Habilitado"` | `"No Autorizado"`
- `Entregables`: lista de `EntregableResumenDto { Nombre, Estado, Vigencia }` — solo en endpoint no-OficinaCentral

**InduccionHoyDto:** filtra `ss_induccion WHERE estado='PROGRAMADA'` con rango fecha Lima (UTC-5). Sin filtro por proyecto.

**GetTrabajadoresPorProgramarAsync — filtro `search`:**
- Si `search` tiene 8 dígitos → `WHERE dni = search` (exacto)
- Si no → `WHERE LOWER(apellido_nombre) LIKE '%search%'` (aplicado en la query SQL, no en memoria)

**SsTareo:** `(proyecto_id, fecha)` se considera clave de negocio — `CreateTareoAsync` tira 409 si ya existe el par.

**Tareo con detalles:**
- `TareoCreateDto` incluye `DetallesCasa[]` (`PartidaId`, `CantidadPersonas`) y `DetallesContratista[]` (`EmpresaId`, `CantidadPersonas`). Ambas listas default a `[]` — retrocompatible.
- `TareoDto` respuesta incluye `DetallesCasa[]` (con `PartidaNombre`) y `DetallesContratista[]` (con `EmpresaNombre`).
- `UpdateTareoAsync`: borra todos los detalles anteriores (RemoveRange) e inserta los nuevos en el mismo SaveChanges.
- Helper privado `LoadDetallesAsync(ctx, tareoId)`: hace JOIN `ss_tareo_detalle_casa → ss_tareo_partida` y `ss_tareo_detalle_contratista → contributor` para resolver nombres.
- Helper privado `InsertDetalles(ctx, tareoId, dto)`: añade los nuevos registros al contexto sin llamar SaveChanges (lo llama el método público).

**Pendiente en BD (crear manualmente):**
```sql
ALTER TABLE ss_induccion ADD COLUMN IF NOT EXISTS ingreso_confirmado boolean NOT NULL DEFAULT false;
ALTER TABLE ss_induccion ADD COLUMN IF NOT EXISTS fecha_ingreso timestamptz;
CREATE TABLE IF NOT EXISTS ss_tareo (
    id serial PRIMARY KEY,
    proyecto_id int NOT NULL REFERENCES project(project_id),
    fecha date NOT NULL,
    observaciones text,
    creado_por int,
    created_at timestamptz,
    updated_at timestamptz
);
```

**Validaciones de acceso al registrar / reingresar trabajadores (5 puntos de control):**

1. **`WorkersController.Create` (POST /workers)** — antes de crear: `EstaRestringidoPorDniAsync` bloquea con 400 si el DNI está en la blacklist activa.
2. **`HabTrabajadorRepository.ReingresoAsync`** — tras cargar el worker: `EstaRestringidoPorDniAsync` (400) → `VerificarNoActivoEnOtraEmpresaAsync` (400 si tiene vinculación activa en empresa distinta).
3. **`HabTrabajadorRepository.CambiarObraAsync`** — valida `EstaRestringidoPorDniAsync` y `ValidarExclusividadEmpresaAsync` (409 + log en `ss_hab_bloqueo_log`).
4. **`HabTrabajadorRepository.AgregarProyectoAsync`** — valida `EstaRestringidoPorDniAsync`.
5. **`InduccionRepository.CreateAsync`** — itera `WorkerIds[]`, para cada uno valida `EstaRestringidoPorDniAsync`; si está restringido lanza 400 con el nombre del trabajador.

`VerificarNoActivoEnOtraEmpresaAsync` (privado en `WorkerSearchRepository` y `HabTrabajadorRepository`): consulta `worker_vinculaciones WHERE fecha_fin IS NULL`, lanza 400 si `EmpresaId != empresaIdNueva`. Mensaje: *"El trabajador ya se encuentra activo en otra empresa. Debe ser retirado antes de poder registrarlo en una nueva empresa."*

`ValidarExclusividadEmpresaAsync` (privado en `HabTrabajadorRepository`): mismo check pero lanza 409 y escribe registro en `ss_hab_bloqueo_log`. Usado solo en `CambiarObraAsync`.

**Pendiente en código:**
- Quitar `Console.WriteLine` de debug en `ControlAccesoRepository.GetConsultaAsync` (líneas ~51-54)
- Quitar `Console.WriteLine` de debug en `ControlAccesoRepository.GetInduccionesHoyAsync` (3 líneas DEBUG agregadas temporalmente)
- Quitar `[AllowAnonymous]` de `GET /inducciones-hoy` en `ControlAccesoController` cuando se confirme fix de fechas

---

## 11. Módulos nuevos 2026-05 — resumen de arquitectura

### 11a. AuthModule (`Features/AuthModule/`)

Consolida toda la autenticación. Reemplaza y amplía el anterior `MicrosoftAuthModule`.

| Feature | Responsabilidad |
|---------|-----------------|
| MicrosoftLoginFeature | Login con Microsoft Entra, emite JWT interno |
| MicrosoftProfileFeature | Perfil Microsoft Graph (HttpClient) |
| ContractorCredentialsFeature | Credenciales JWT para contratistas (tabla `contractor_users`) |
| RoleFeature | CRUD roles + asignación de funcionalidades a roles |
| UserFeature | Gestión de usuarios del sistema |

Migración: `20260505173114_AddContractorUserCredentials` (tabla `contractor_users`).

---

### 11b. ConfigurationModule (`Features/ConfigurationModule/`)

`ProjectFeature` — CRUD completo de proyectos AC (`Proyecto` en español). Controlador: `ProjectController`.

---

### 11c. GestionAdministrativaModule (`Features/GestionAdministrativaModule/`)

Prefijo de entidades: `Ga*` (`GaLugar`, `GaMotivoSalida`, `GaHoraOpcion`, `GaSolicitudSalida`).

| Feature | Responsabilidad |
|---------|-----------------|
| SolicitudSalidasFeature | Solicitudes de salida del personal |
| GestionSalidasFeature | Aprobación y gestión de salidas |
| LugaresFeature | Catálogo de lugares |
| MotivosSalidaFeature | Catálogo de motivos de salida |

---

### 11d. MejoraContinuaModule (`Features/MejoraContinuaModule/`)

| Feature | Responsabilidad |
|---------|-----------------|
| LessonsLearnedFeature | Lecciones aprendidas — CRUD, filtros paginados, exportación Excel |
| AreasYSubareasFeature | CRUD áreas, subáreas y scopes PSSS |
| PsssTemplateFeature | Plantillas PSSS (relación área/subárea → partidas) |
| RelationsFeature | Relaciones área/subárea para lecciones (2026-05-14) |

Modelos compartidos: `Partida`, `PsssScope`, `PsssTemplate`, `PsssTemplateDetail`, `SubArea` en `MejoraContinuaModule/Shared/Models/`.

---

### 11e. UnidadDeProyectosModule (`Features/UnidadDeProyectosModule/`)

`LessonsLearnedDashboard` — dashboard consolidado de lecciones entre proyectos.

---

### 11f. CostsModule — nuevas sub-features de Configuration

`Features/CostsModule/Features/Configuration/`:
- `ProjectLinkFeature` — vínculos entre proyectos
- `StaffProjectEmailFeature` — emails de staff por proyecto
- `WorkItemCategoryFeature` — categorías de partidas
- `WorkItemFeature` — catálogo de partidas

**Adjudicaciones extendidas (2026-05-07 al 2026-05-15):**
- Generación de contrato Word con cláusulas (`WordTemplateHelper` + `WorkItemCategoryClause`)
- Generación de pagaré
- Instructivo en paso de documentos
- Notificación de correo en paso 5 (antes paso 6)
- Filtro por proyectos en listado de adjudicaciones
- Validación: 400 si ya existe un documento abierto al generar nuevo

---

### 11g. SsomaModule — nuevos controladores y servicios (2026-05-06 al 2026-05-18)

| Controlador | Ruta | Descripción |
|-------------|------|-------------|
| `ClinicaUsuariosController` | `/catalogos/clinicas/{id}/usuarios` | CRUD usuarios por clínica — ver sección 12 |
| `EmoAlertaController` | `/alertas/procesar|auto-programar|resumen-diario` | Triggers manuales de cron jobs |
| `ReporteController` | `/reportes/sunafil-mensual` | Excel SUNAFIL mensual (ClosedXML) |

Nuevos servicios registrados en `SsomaModule`:
- `IEmoAlertaService` — evalúa vencimientos EMO
- `IEmoAutoProgramacionService` — motor de auto-programación (cron mañana)
- `IEmoResumenDiarioService` — resumen diario a clínicas (cron 4:30 pm Lima)

Nuevos modelos:
- `SsClinicaResetToken` — tokens de activación/reset de cuenta clínica
- `SsSeguimientoMedico` — seguimiento médico post-EMO
- `SsEmoRestriccion` — restricciones médicas por EMO
- `SsClinicaEmail` — emails por clínica (`ss_clinica_emails`)

---

### 11h. HabilitacionModule — controladores nuevos (2026-05-04 al 2026-05-18)

| Controlador | Ruta | Descripción |
|-------------|------|-------------|
| `InduccionController` | `/inducciones` | Programar, listar, aprobar inducciones |
| `ControlAccesoController` | `/control-acceso` | Consulta habilitación en tiempo real, tareo, inducciones del día |
| `TrabajadorRestringidoController` | `/restringidos` | Blacklist trabajadores (roles: ADMINISTRADOR SSOMA / ADMINISTRADOR ADMINISTRACION) |
| `EmpresaContratistaController` | `/empresas` | CRUD empresas contratistas |
| `CatalogosHabilitacionController` | `/catalogos` | Catálogos del módulo (items, áreas, subareas, categorías, ocupaciones) |
| `RegistrosModeloController` | `/registros-modelo` | Registros modelo (público) |

---

## 12. ClinicaUsuariosModule — detalle

**Tablas creadas manualmente en pgAdmin (sin migración EF):**

| Tabla | Columnas clave |
|-------|----------------|
| `ss_clinica_usuarios` | `clinica_usuario_id`, `clinica_id`, `nombre`, `email`, `password_hash`, `activo`, `creado_por int`, `modificado_por int`, `desactivado_por int` |
| `ss_clinica_tokens` | `token_id`, `clinica_usuario_id`, `token`, `tipo`, `expiracion`, `usado_en`, `ip_solicitud` |
| `ss_clinica_auditoria` | `auditoria_id`, `clinica_usuario_id`, `clinica_id`, `accion`, `ip_origen`, `detalle_adicional jsonb` |

**⚠️ creado_por / modificado_por / desactivado_por son `int?` en modelo, servicio, interfaz, DTO y controller — NO string.**

**Archivos:**
- `Infrastructure/Models/SsClinicaUsuario.cs` — PK no convencional, requiere `HasKey` en `OnModelCreating`
- `Infrastructure/Models/SsClinicaToken.cs`
- `Infrastructure/Models/SsClinicaAuditoria.cs`
- `Application/Dtos/ClinicaUsuarios/ClinicaUsuarioDtos.cs`
- `Application/Interfaces/IClinicaUsuarioService.cs` → `Application/Services/ClinicaUsuarioService.cs`
- `Presentation/ClinicaUsuariosController.cs` — ruta: `api/v1/ssoma/salud-ocupacional/catalogos/clinicas/{clinicaId}/usuarios`
- `Shared/ClinicaClaimsHelper.cs` — extrae `clinicaId` / `clinicaUsuarioId`; `ValidarAcceso()` restringe scope CLINICA

**Estado actual:**
- `[AllowAnonymous]` a nivel de clase en `ClinicaUsuariosController` — **temporal para desarrollo**
- Validación scope: solo aplica si NO tiene rol ADMINISTRADOR SSOMA ni SSOMA

**Pendiente:** quitar `[AllowAnonymous]` y reemplazar por auth correcta.

---

## 13. Trabajo pendiente

### Alta prioridad
- Quitar `[AllowAnonymous]` de los 4 endpoints `/trabajadores/{id}/proyectos*`, `GET /eventos` y endpoints SSOMA
- Quitar prefijo `[PRUEBA - NO TOMAR EN CUENTA]` de subjects de correos antes de prod (en `CambiarObraAsync`, `ReingresoAsync`, correos Vida Ley)
- Crear primer usuario admin en `app_user`
- Deploy a producción
- 42 empresas SharePoint con IDs 1656+ pendientes de migrar a `contributor`
- Eliminar `id_sharepoint` de `contributor` cuando migración SharePoint esté completa

- Crear tablas/columnas manuales en BD (ver sección 10 — `ss_tareo`, columnas `ss_induccion`, `ss_tareo_partida`, `ss_tareo_detalle_casa`, `ss_tareo_detalle_contratista`)
- Crear tablas manuales en BD: `cat_categoria` y `cat_ocupacion` (DbSets registrados, endpoints listos — faltan las tablas físicas)
- Ejecutar `Database/migrations/ss_trabajador_restringido.sql` en Aiven si no se hizo ya
- Quitar `Console.WriteLine` de debug en `ControlAccesoRepository.GetConsultaAsync`

### Media prioridad
- Empresas contratistas: 1.591 vinculaciones sin empresa
- `tipo_emo_id`: 813 EMOs migrados tienen NULL
- Eliminar `id_trabajador` de `workers` tras confirmar migración completa
- Multi-proyecto FASE 4: `BandejaRepository`, listados, EMO, SCTR y Vida Ley aún razonan sobre `worker_vinculaciones` (1-activa). Evaluar si pivotar a `ss_hab_worker_proyecto` para workers Casa en N proyectos
- `InicializarEntregablesAsync` no crea fila inicial en `ss_hab_worker_proyecto` — considerar parámetro `proyectoInicialId?` en `POST /workers`
- Separar `ISunatLookupService` de `ProjectService` para eliminar el acoplamiento de DI

- `CONTRATISTA` multi-proyecto: `WorkerProyecto` soporta múltiples proyectos activos, pero `ControlAccesoRepository.BuildDtosAsync` toma solo la última `WorkerVinculacion` (1 empresa/proyecto mostrado). Evaluar si pivotar a `WorkerProyecto` para filtrado más fino.

### Baja prioridad
- 8 EMOs sin match de DNI — insertar manualmente
- 24 vinculaciones sin proyecto
- `ReminderController` aún usa `Environment.GetEnvironmentVariable` para CronSecret — migrar a `IConfiguration`
- FluentValidation 11.3.1 usa API deprecated — migrar cuando bumpeemos v12
- Refactor `Sunat:Token` headers en Program.cs dentro del `if` null-safe

---

## Sesión 2026-05-06 — Módulo Vigilancia Médica Ocupacional

### Nuevas columnas en BD (aplicadas en PgAdmin — inicio de sesión):
- ss_programacion_emos: origen varchar NOT NULL DEFAULT 'Manual', check_in_hora time, motivo_rechazo varchar, fecha_notificacion timestamptz
- ss_clinicas: password_hash text NOT NULL DEFAULT 'PENDIENTE_RESET'
- ss_alertas_emo: worker_id y emo_id pasaron a nullable
- ss_clinica_reset_token: tabla nueva creada
- role: nuevo registro role_id=14 'CLINICA'

### Nuevas tablas pendientes de crear en BD (PgAdmin):
```sql
CREATE TABLE cat_jefatura (
    id     serial PRIMARY KEY,
    nombre varchar NOT NULL,
    email  varchar,
    activo boolean NOT NULL DEFAULT true
);

CREATE TABLE ss_clinica_emails (
    id         serial PRIMARY KEY,
    clinica_id int NOT NULL REFERENCES ss_clinicas(id),
    nombre     varchar,
    email      varchar NOT NULL,
    activo     boolean NOT NULL DEFAULT true
);
```

### Nuevos archivos creados:
- Shared/Constants/HabItemIds.cs — constantes CertAptitud=4, LecturaEmo=25, InduccionObra=12, Sctr=11, VidaLey=13
- Features/SsomaModule/.../Presentation/ClinicaAuthController.cs — POST /auth/login, POST /auth/solicitar-activacion, POST /auth/activar
- Features/SsomaModule/.../Application/Services/EmoAutoProgramacionService.cs
- Features/SsomaModule/.../Application/Services/EmoResumenDiarioService.cs
- Features/SsomaModule/.../Presentation/ReporteController.cs
- Features/SsomaModule/.../Infrastructure/Models/SsClinicaResetToken.cs
- Infrastructure/Models/CatJefatura.cs — tabla cat_jefatura (nombre, email?, activo)
- Features/SsomaModule/.../Infrastructure/Models/SsClinicaEmail.cs — tabla ss_clinica_emails (clinica_id, nombre?, email, activo)
- Features/SsomaModule/.../Application/Dtos/Catalogos/ClinicaEmailDto.cs — ClinicaEmailDto + ClinicaEmailCreateDto

### Cambios en archivos existentes:
- EmoAlertaService.cs — 3 bugs corregidos: FechaVencimientoCalculada??FechaVencimiento, ventana 4 días hábiles por tipo worker, vigencia desde TipoEmo.VigenciaMeses
- SsProgramacionEmo.cs — 4 propiedades nuevas: Origen, CheckInHora, MotivoRechazo, FechaNotificacion
- SsClinica.cs — propiedad PasswordHash agregada
- ProgramacionEmoService.cs — estados nuevos: "Aceptado por Clínica", "Rechazado por Clínica", "En Atención", "Completado"
- ProgramacionEmoController.cs — endpoint PATCH /{id}/clinica-accion
- ProgramacionFilterDto.cs — campo ClinicaId? agregado
- ProgramacionListDto.cs — campos Origen, CheckInHora, MotivoRechazo, FechaNotificacion agregados
- EmoRepository.cs — método SincronizarEntregableEmoAsync() para reflejar aptitud en ss_hab_trabajador para contratistas
- HabTrabajadorRepository.cs — BajaAsync y BajaMasivaAsync crean EMO retiro automático. Prefijo [PRUEBA - NO TOMAR EN CUENTA] eliminado de 10 subjects
- ControlAccesoRepository.cs — 5 Console.WriteLine debug eliminados
- ReminderController.cs — migrado de Environment.GetEnvironmentVariable a IConfiguration ✓
- SsomaModule.cs — registros nuevos: IEmoAutoProgramacionService, IEmoResumenDiarioService
- AppDbContext — DbSet<SsClinicaResetToken>, DbSet<CatJefatura>, DbSet<SsClinicaEmail> agregados
- EmoAutoProgramacionService.cs — fechaProg = Max(fechaDesdeVencimiento, fechaMinima) donde fechaMinima = hoy + 2 días hábiles (antes: fallback solo si ya pasó)
- ProgramacionEmoRepository.cs — IConfiguration inyectado; EnviarNotificacionCreacionAsync reescrito: To=ss_clinica_emails (fallback ss_clinicas.email), CC diferenciado por tipo worker, EmoResumen:Destinatarios siempre en CC, contratistas no reciben email; BuildDestinatariosCreacion eliminado; label "Empresa" → "Proyecto" en body
- ICatalogosRepository + ICatalogosService + CatalogosRepository + CatalogosService + CatalogosController — 3 métodos/endpoints para gestión de ss_clinica_emails
- appsettings.Local.json + appsettings.Production.json — sección EmailsArea agregada

### Cron (confirmado): patrón externo igual que ReminderController
Los endpoints /alertas/auto-programar y /alertas/resumen-diario siguen el mismo patrón que GET /api/v1/reminder:
autenticación por header `Authorization: Bearer {CronSecret}`, sin BackgroundService ni IHostedService.
Configurar el cron externo (Azure Logic App / GitHub Actions / EasyCron) para llamar a esas URLs.
- /alertas/auto-programar → correr cada mañana (ej. 7:00 am hora Lima)
- /alertas/resumen-diario → correr a las 4:30 pm hora Lima

### Lógica de tipos de worker para notificaciones EMO (ProgramacionEmoRepository):
- Obrero: contrata_casa='Casa' AND obra_oficina='Ninguno' → To=clínica, CC=EmailResidente+EmailResponsable+MedicinaOcupacional
- Staff: contrata_casa='Casa' AND obra_oficina='Staff' → To=clínica, CC=EmailCorporativo+EmailResidente+EmailResponsable+MedicinaOcupacional
- Oficina Central: obra_oficina='Oficina Central' → To=clínica, CC=EmailCorporativo+GTH+MedicinaOcupacional+cat_jefatura.email
- Contratista: sin email

### Endpoints nuevos bajo /api/v1/ssoma/salud-ocupacional/:
- PATCH /programaciones/{id}/clinica-accion
- GET /alertas/auto-programar (CronSecret)
- GET /alertas/resumen-diario (CronSecret)
- POST /auth/login
- POST /auth/solicitar-activacion
- POST /auth/activar
- GET /reportes/sunafil-mensual?mes=&anio=
- GET /catalogos/clinicas/{id}/emails
- POST /catalogos/clinicas/{id}/emails    body: { email, nombre? }
- DELETE /catalogos/clinicas/{id}/emails/{emailId}

### Pendiente de configurar en appsettings.Production.json:
- "EmoResumen:Destinatarios": "correo1@abril.pe,correo2@abril.pe"
- "App:FrontendUrl": "https://..."
- (ya agregado) "EmailsArea": { "MedicinaOcupacional": "medicinaocupacionalnm@abril.pe", "GTH": "gthnm@abril.pe" }

---

## Sesión 2026-05-18 (segunda parte) — ContractorsModule y ContractorEmail.UserId

### ContractorEmail — nuevo campo UserId

`Features/CostsModule/Shared/Models/ContractorEmail.cs`:
- `public int? UserId { get; set; }` — FK→`app_user.user_id`
- `public User? User { get; set; }` — nav property

Al registrar un contratista nuevo (`ContractorRegistrationRepository.Create`), el sistema busca en `app_user` por el email del contacto y asigna el `UserId` si existe. Si el usuario ya tenía cuenta antes del campo (registros huérfanos), `ContratistaAuthService.ActivarCuentaAsync` repara el `UserId` antes de procesar la activación.

### SsResetToken — EmpresaId nullable, UserId añadido

`Features/HabilitacionModule/Infrastructure/Models/SsResetToken.cs`:
- `EmpresaId int?` — era NOT NULL, ahora nullable
- `public int? UserId { get; set; }` — nuevo FK→`app_user`
- `public User? User { get; set; }` — nav property

### EmpresaContratistaController.Create — [AllowAnonymous] + validación RUC

`Features/HabilitacionModule/Presentation/EmpresaContratistaController.cs`:
- `POST /habilitacion/empresas` tiene `[AllowAnonymous]` — ruta pública de auto-registro
- Antes de crear, valida que el RUC no exista en `ss_empresa_contratista` (400) ni en `contributor` (400)
- Dos métodos nuevos en `IEmpresaContratistaRepository`/`EmpresaContratistaRepository`: `ExisteRucEnEmpresaContratistaAsync` y `ExisteRucEnContributorAsync`

### ContractorRegistrationService — SharePoint lazy

`Features/ContractorsModule/.../Application/Services/ContractorRegistrationService.cs`:
- El bloque SharePoint (fetch de `SharePoint:ContractorListId` y uploads) ahora está dentro de un `if (dto.LogoFile is not null || ...)`. Si no se suben archivos, no requiere configuración SharePoint. Antes fallaba siempre si el key no estaba en config.

### Logging en ContractorRegistrationController y ContractorRegistrationRepository

Ambos ahora inyectan `ILogger<T>` y tienen `_logger.LogError(ex, ...)` en los bloques catch, lo que permite ver el error real en consola del servidor.

### Migraciones nuevas (rama feature/arquitectura-comercial)

| Migration ID | Descripción |
|---|---|
| `20260518193906_AddWorkerMissingColumns` | ~26 columnas worker, tablas nuevas, FKs — Up() reescrito como SQL idempotente |
| `20260518220129_MigrateResetTokenToUserId` | `user_id` en `ss_reset_token` y `contractor_email`; FKs; `empresa_id` nullable en reset_token |
| `20260518223250_AddContractorEmailUserId` | Migración vacía — columna ya añadida por la anterior vía SQL |

La migración `20260505173114_AddContractorUserCredentials` también fue reescrita como SQL idempotente (la DB estaba por delante de EF).

### NuGet — vulnerabilidades corregidas

`Abril-Backend.csproj`:
- Eliminado `Microsoft.AspNetCore.Mvc` 2.3.9 (NU1510 — incluido en framework net10.0)
- Sobrescrito `SixLabors.ImageSharp` → 3.1.12 (7 CVEs de `PdfSharpCore` 1.3.67; el código solo hace merge/lectura de PDF sin imágenes, seguro)
- Sobrescrito `Microsoft.Kiota.Abstractions` → 1.22.2 (GHSA-7j59-v9qr-6fq9; compatible con Microsoft.Graph 5.x)

---

## Sesión 2026-05-19 — flujo completo creación trabajador contratista

### WorkersController.Create — AgregarProyectoAsync + InicializarEntregablesAsync

`Features/SsomaModule/.../Presentation/WorkersController.cs`:
- Agregado `using Abril_Backend.Features.Habilitacion.Application.Dtos.Trabajadores;`
- Agregado alias `using WorkerUpdateDto = Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Workers.WorkerUpdateDto;` — resuelve ambigüedad con el `WorkerUpdateDto` del namespace Habilitacion que se importó con el using anterior
- Después de `_service.Create(dto)`, si `dto.ProyectoId.HasValue`, llama `_habRepo.AgregarProyectoAsync(id, new AgregarProyectoDto { ProyectoId, EmpresaId, FechaInicio = dto.FechaIngreso })`
- `InicializarEntregablesAsync(id)` se llama siempre (ya existía, ahora va después del bloque AgregarProyecto)

Flujo completo para un contratista:
1. `_service.Create(dto)` → crea `Worker` + `Person` (lookup-or-create) + `WorkerVinculacion` con `EmpresaId = ContributorId`
2. `AgregarProyectoAsync` → crea fila en `ss_hab_worker_proyecto` + envía email coord SSOMA
3. `InicializarEntregablesAsync` → genera checklist en `ss_hab_trabajador`

### WorkerSearchRepository.Create — Person lookup-or-create (fix error 23505)

`Features/SsomaModule/.../Infrastructure/Repositories/WorkerSearchRepository.cs`:
- Antes de crear `Person`, busca si ya existe un registro en `ctx.Person` con el mismo `document_identity_code`
- Si existe: reutiliza el objeto tracked (EF usa FK, no INSERT duplicado)
- Si no existe: crea `Person` nuevo y llama `SaveChangesAsync` antes de crear `Worker`
- Evita el error `23505 — duplicate key value violates unique constraint "person_document_identity_code_key"`

### HabTrabajadorRepository.AgregarProyectoAsync — fix IdLegacy (bug crítico)

`Features/HabilitacionModule/Infrastructure/Repositories/HabTrabajadorRepository.cs` línea ~1364:

Bug: `WorkerVinculacion.EmpresaId` almacena `ContributorId`, pero `SsEmpresaProyecto.EmpresaId` almacena `ss_empresa_contratista.id` (SsId). La comparación directa siempre fallaba → `tieneEntregables = false` → excepción 400 siempre para contratistas.

Post-migración (2026-05-23): `ss_empresa_contratista` eliminada, `ss_empresa_proyecto.empresa_id` apunta directamente a `contributor_id`. La comparación es ahora directa:
```csharp
.AnyAsync(ep => ep.EmpresaId == empresaId.Value && ep.ProyectoId == dto.ProyectoId)
```

### HabTrabajadorController.GetWorkers — parámetro soloVerificacion

`Features/HabilitacionModule/Presentation/HabTrabajadorController.cs`:
- Nuevo `[FromQuery] bool soloVerificacion = false`
- Cuando `soloVerificacion = true`, el filtro `empresaId = empresaIdJwt` del contratista NO se aplica
- Permite al frontend verificar si un DNI ya existe en cualquier empresa antes de registrar un nuevo trabajador
- El frontend lo llama con `soloVerificacion: true` solo al verificar duplicados en `verificarExistenciaEnBd()`

### SubidoPorEmpresaId — simplificado post-migración (2026-05-23)

`SsHabDocumentoVersion.SubidoPorEmpresaId` ahora usa directamente `empresaId` (= `contributor.contributor_id`). El lookup de conversión via `SsEmpresaContratista.IdLegacy` fue eliminado en los tres repositorios:

| Archivo | Método |
|---|---|
| `HabTrabajadorRepository.cs` | `UpdateEntregableAsync` |
| `HabEmpresaRepository.cs` | `UpdateEntregableEmpresaAsync` |
| `EquipoRepository.cs` | `UpdateEntregableEquipoAsync` |

Patrón actual:
```csharp
int? ssEmpresaId = empresaId;  // ContributorId directo
// ...
SubidoPorEmpresaId = ssEmpresaId,
```

### SharePointHabService — arquitectura de storage

`SubirArchivoAsync` devuelve siempre el **path relativo** (`habilitacion/contexto/YYYYMMDD_archivo.pdf`).  
`GetDownloadUrlAsync` genera la URL absoluta firmada de SharePoint bajo demanda (Graph API redirect).  
`GetDownloadUrlAsync` tiene fallback: si recibe una URL absoluta (`https://...`) la devuelve tal cual con log `"URL absoluta detectada"` — indica que `archivo_url` en BD contiene una URL expirada en lugar del path relativo.

Endpoints de visualización (`ArchivoHabilitacionController`):
- `GET /archivos/url?path=` → `{ url }` para abrir en nueva pestaña
- `GET /archivos/ver?url=` → `302 Redirect` directo
- `GET /archivos/descargar?url=` → `302 Redirect` con `Content-Disposition: attachment`

---

## Sesión 2026-05-19 (segunda parte) — inducciones contratista, fix badges, fix res.path

### InduccionListDto — IngresoConfirmado + FechaIngreso

`Features/HabilitacionModule/Application/Dtos/Inducciones/InduccionListDto.cs`:
- Añadidos `bool IngresoConfirmado` y `DateTime? FechaIngreso`
- `InduccionRepository.GetAsync()` los mapea directamente desde `SsInduccion` (columnas manuales en BD)
- Estos campos alimentan el badge de estado en el frontend contratista: `REALIZADA`→verde, `ingresoConfirmado=true`→amarillo, `false`→rojo

### InduccionController.GetList — scope empresaId para CONTRATISTA

`Features/HabilitacionModule/Presentation/InduccionController.cs`:
```csharp
if (User.FindFirst("tipo")?.Value == "CONTRATISTA")
{
    if (!int.TryParse(User.FindFirst("empresaId")?.Value, out var empresaJwt))
        return StatusCode(403, new { message = "Token de contratista inválido." });
    empresaId = empresaJwt;
}
```
Mismo patrón que `EquiposController` y `HabTrabajadorController`. El `empresaId` del JWT es `ContributorId`. El filtro en `InduccionRepository.GetAsync()` es `WHERE empresa_id = empresaId` — `ss_induccion.empresa_id` apunta a `contributor.contributor_id` directamente (no a `ss_empresa_contratista.id`).

### Notas para el frontend (registradas en su CONTEXT.md)
- `programar-induccion` en `trabajadores/components/` y en `inducciones/components/`: ambos corregidos para CONTRATISTA — cargan proyectos vía `EmpresaContratistaService.getProyectos()` en vez de todos los proyectos del sistema.
- `empresa.ts`, `sctr-subir.ts`, `registro-empresa.ts`: corregidos para usar `res.path` en vez de `res.url` al guardar resultado del upload. `trabajadores.ts` y `equipos.ts` ya estaban correctos.

## Sesión 2026-05-19 (tercera parte) — bugs contratista retirados/reingreso/proyectos afiliados

### Bug fix EmpresaContratistaRepository: resolución IdLegacy dos pasos para proyectos afiliados

`GetProyectosAsync` usaba `ssId != 0 ? ssId : empresaId` como fallback directo, lo que pasaba el `ContributorId` como si fuera un `ss_empresa_contratista.id` cuando `IdLegacy` era null. Corregido con resolución de dos pasos:
1. Buscar `SsEmpresaContratista` por `IdLegacy == empresaId`
2. Si no encuentra, resolver vía RUC: `Contributor.ContributorRuc` → `SsEmpresaContratista` por RUC

Solo si ambos fallan se usa `empresaId` directamente (fallback admin). Esto garantiza que los contratistas vean sus proyectos afiliados correctamente.

### Bug fix HabTrabajadorRepository: LatestVincActiva vs LatestVincCualquiera según soloRetirados

`GetWorkersHabilitacionAsync` usaba una sola subquery `LatestVinc` con `FechaFin == null`. Al agregar ese filtro para corregir el 403 de trabajadores activos, los retirados (cuya vinculación tiene `FechaFin` seteada al momento del retiro) dejaban de aparecer en la vista de retirados de la empresa contratista.

Solución: dos subqueries paralelas en la proyección EF:
- `LatestVincActiva` — `WHERE fecha_fin IS NULL`, ordenado por `CreatedAt DESC, Id DESC`
- `LatestVincCualquiera` — sin filtro de `FechaFin`, misma ordenación

El filtro de `empresaId` y `proyectoId` usa `LatestVincActiva` cuando `soloRetirados=false` y `LatestVincCualquiera` cuando `soloRetirados=true`. El mapeo al DTO final también usa la subquery correcta según el flag.

### Bug fix ReingresoAsync: siempre crea vinculación nueva al reingresar

`ReingresoAsync` solo creaba nueva `WorkerVinculacion` dentro de `if (esCambioProyecto || esCambioEmpresa)`. Para contratistas, `esCambioEmpresa` es siempre `false` (`!esContratista = false`) y si el reingreso era al mismo proyecto, `esCambioProyecto` también era `false` — resultado: vinculación anterior cerrada, ninguna nueva creada, trabajador quedaba sin vinculación activa.

Corregido eliminando el guard `if (esCambioProyecto || esCambioEmpresa)`. El reingreso siempre cierra la vinculación anterior (si existe) y crea una nueva con `FechaInicio = fechaReingreso` y `FechaFin = null`.

### Dato corrupto worker 2473 corregido manualmente en BD

`worker_vinculaciones` id=7672 (worker_id=2473, empresa_id=408, proyecto_id=8) tenía `fecha_fin = fecha_inicio = 2026-05-19`. Investigación exhaustiva del código descartó bug: no hay triggers de negocio en `worker_vinculaciones` (solo `RI_ConstraintTrigger` de FK), ningún método C# establece `FechaFin` al crear una vinculación. Dato corrupto por acción manual puntual. Corregido directamente en BD: `fecha_fin = NULL`.

---

## Sesión 2026-05-19 (cuarta parte) — bugs contratista Equipos y SCTR

### EquipoRepository: HasPendientes corregido cuando no hay entregables

`GetPagedAsync` calculaba `HasPendientes = Any(entregable NOT IN {Aprobado, NoAplica})`. Sin entregables registrados, `Any(...)` retorna `false` → badge "Habilitado" incorrecto para equipos sin documentación.

Fix:
```csharp
HasPendientes = !ctx.SsHabEquipo.Any(h => h.EquipoId == e.Id)
             || ctx.SsHabEquipo.Any(h => h.EquipoId == e.Id
                    && h.Estado != "No Aplica" && h.Estado != "Aprobado")
```
Sin entregables → `HasPendientes = true` → badge "No Autorizado". Commit `c225e14`.

### SctrVidaLeyController.GetPaged — scope empresaId para CONTRATISTA

Mismo patrón que `EquiposController` e `InduccionController`:
```csharp
if (User.FindFirst("tipo")?.Value == "CONTRATISTA")
{
    if (!int.TryParse(User.FindFirst("empresaId")?.Value, out var contraId))
        return StatusCode(403, new { message = "Token de contratista inválido." });
    empresaId = contraId;
}
```
Sin este bloque, CONTRATISTA veía todas las pólizas del sistema. Commit `4a8363d`.

### SctrVidaLeyRepository.GetTrabajadoresPorEmpresaAsync — quitado check Contains("ABRIL")

**Bug**: cuando `empresaId` es ContributorId de contratista, el `contributor` SÍ se encuentra en la tabla (es un ContributorId válido), pero el nombre no contiene "ABRIL" → caía al `else` que hacía `WHERE ss_empresa_contratista.id == empresaId` (tratando ContributorId como SsId) → 0 workers.

**Fix**: si `contributor != null`, ya es ContributorId directo (independientemente del nombre). El lookup vía `IdLegacy` solo corre cuando `contributor == null`.

```csharp
// ANTES:
if (contributor != null
    && contributor.ContributorName != null
    && contributor.ContributorName.ToUpper().Contains("ABRIL"))

// DESPUÉS:
if (contributor != null)  // si está en la tabla contributor, ya es ContributorId válido
```
Commit `23f2b7f`.

### ReingresoAsync — recuperar empresa/proyecto de última vinculación cerrada

**Bug**: cuando el trabajador fue correctamente retirado (vinculación cerrada con `FechaFin`), `vinculActual == null` → `currentEmpresaId = null`, `currentProyectoId = null` → nueva vinculación creada con nulls → trabajador no aparece en listados filtrados por empresa/proyecto.

**Fix**: cuando `vinculActual == null`, recuperar la última vinculación cerrada:
```csharp
if (vinculActual == null)
{
    var vinculAnterior = await ctx.WorkerVinculacion
        .Where(v => v.WorkerId == workerId)
        .OrderByDescending(v => v.CreatedAt)
        .ThenByDescending(v => v.Id)
        .FirstOrDefaultAsync();
    currentProyectoId = vinculAnterior?.ProyectoId;
    currentEmpresaId  = vinculAnterior?.EmpresaId;
}
```
Commit `23f2b7f`.

### Resumen acumulado de todos los fixes CONTRATISTA de la sesión 2026-05-19

| Fix | Archivo | Commit |
|-----|---------|--------|
| `EmpresaContratistaRepository.GetProyectosAsync`: resolución IdLegacy en dos pasos (por `IdLegacy`, luego por RUC) | `EmpresaContratistaRepository.cs` | tercera parte |
| `GetWorkersHabilitacionAsync`: dos subqueries `LatestVincActiva` (FechaFin IS NULL) y `LatestVincCualquiera` (sin filtro) según `soloRetirados` | `HabTrabajadorRepository.cs` | tercera parte |
| `ReingresoAsync`: eliminado guard `if (esCambioProyecto \|\| esCambioEmpresa)` — siempre crea vinculación nueva al reingresar | `HabTrabajadorRepository.cs` | tercera parte |
| `ReingresoAsync`: recupera empresa/proyecto de última vinculación cerrada cuando `vinculActual == null` | `HabTrabajadorRepository.cs` | `23f2b7f` |
| `GetTrabajadoresPorEmpresaAsync`: quitado check `Contains("ABRIL")` | `SctrVidaLeyRepository.cs` | `23f2b7f` |
| `SctrVidaLeyController.GetPaged`: inyecta `empresaId` del JWT para CONTRATISTA | `SctrVidaLeyController.cs` | `4a8363d` |
| `GetPagedAsync`: `HasPendientes = true` cuando no hay entregables | `EquipoRepository.cs` | `c225e14` |
| `worker_vinculaciones` id=7672 `fecha_fin` → NULL (dato corrupto) | pgAdmin manual | — |

---

## Sesión 2026-05-19 (tarde) — feature/arquitectura-comercial

### SctrVidaLeyController — inyección empresaId JWT para CONTRATISTA

`GET /habilitacion/sctr-vidaley` (GetPaged): si el rol del JWT es `CONTRATISTA`, se extrae `empresaId` del claim y se sobreescribe el parámetro de query — el contratista solo ve sus propias pólizas.

### SctrVidaLeyRepository — fix Contains("ABRIL") → ContributorId directo

`GetTrabajadoresPorEmpresaAsync`: el check `contributor.ContributorName.ToUpper().Contains("ABRIL")` fue reemplazado por comprobar simplemente que `contributor != null` (si el registro existe en la tabla contributor, ya es un ContributorId válido para Abril). Elimina falsos negativos si el nombre cambia.

### SctrVidaLeyRepository — WorkerVinculacion como fuente primaria

`GetTrabajadoresPorEmpresaAsync`: `WorkerVinculacion` es siempre la fuente primaria; `WorkerProyecto` se añade como unión suplementaria solo cuando `proyectoId.HasValue`. Antes, `WorkerProyecto` era primario y `WorkerVinculacion` era fallback → devolvía 1 de 3 workers en ciertos proyectos.

### SctrWorkerDto — nuevo campo SctrHabId

`SctrWorkerDto.cs`: añadido `public int? SctrHabId { get; set; }`.  
`BuildDtosAsync`: captura `hab?.Id` del `SsHabTrabajador` correspondiente al worker y al itemTipo — permite al frontend mostrar el historial de versiones de documentos por worker en el tab Pólizas.

### ArchivoHabilitacionController — [AllowAnonymous] en Descargar

`GET /habilitacion/archivos/descargar`: añadido `[AllowAnonymous]`. El botón de descarga usa `window.open()` que no envía el JWT → sin este atributo retornaba 401 silencioso.

### HabTrabajadorRepository — ReingresoAsync safety check vinculación

Después de `SaveChanges`, si el trabajador no tiene ninguna vinculación abierta (no `FechaFin IS NULL`), se crea automáticamente una nueva con la empresa/proyecto de la última vinculación cerrada. Previene que el trabajador quede sin vinculación activa y desaparezca de los listados filtrados.

### Endpoint GET /habilitacion/trabajadores/reparar-vinculaciones

Endpoint de mantenimiento disponible solo para roles aprobadores. Detecta y repara trabajadores con vinculaciones en estado inconsistente (sin ninguna vinculación abierta). Usado para correcciones masivas de datos históricos sin intervención directa en base de datos.

---

## Sesión 2026-05-20 — fixes BandejaRepository y SctrVidaLeyRepository

### BandejaRepository — SelectBase: apellido_nombre → person.full_name

`Features/HabilitacionModule/Infrastructure/Repositories/BandejaRepository.cs`

Segmentos TRABAJADOR e INDUCCION del UNION ALL: `w.apellido_nombre` no existe en la tabla `workers` → causaba 500 en `GET /api/v1/habilitacion/bandeja`.

Fix:
- `w.apellido_nombre as entidad_nombre` → `COALESCE(per.full_name, '') as entidad_nombre`
- Añadido `LEFT JOIN person per ON per.person_id = w.person_id` en ambos segmentos (LEFT para no excluir workers sin person_id)
- Alias `per` usado para no colisionar con `p` (ya usado para `project`)

EMPRESA (`ec.razon_social`) y EQUIPO (`CONCAT(eq.tipo, ...)`) no necesitaban cambio.

> Nota en CONTEXT.md anterior línea 190: "Entidad nombre: `w.apellido_nombre`" → **obsoleto**. Ahora usa `COALESCE(per.full_name, '')` via `LEFT JOIN person per`.

### SctrVidaLeyRepository — fix lookup de item por tipo

`Features/HabilitacionModule/Infrastructure/Repositories/SctrVidaLeyRepository.cs`

**Problema**: `.FirstOrDefaultAsync(i => i.Nombre.Contains(dto.Tipo))` falla cuando `dto.Tipo == "VIDA_LEY"` porque ningún nombre en BD contiene exactamente esa cadena (la BD usa "Vida Ley" o similar).

**Fix aplicado en tres métodos** (`CreateAsync`, `UpdateAsync`, `AprobarAsync`):
```csharp
// ANTES:
var item = await ctx.SsItemTrabajador
    .Where(i => i.EsSctrVidaley)
    .FirstOrDefaultAsync(i => i.Nombre.Contains(dto.Tipo));

// DESPUÉS:
var itemNombreBuscar = dto.Tipo == "VIDA_LEY" ? "Vida" : "SCTR";
var item = await ctx.SsItemTrabajador
    .Where(i => i.EsSctrVidaley && i.Nombre.Contains(itemNombreBuscar))
    .FirstOrDefaultAsync();
```
`AprobarAsync` usa `entity.Tipo` en vez de `dto.Tipo` (misma lógica).

### SctrVidaLeyRepository — fix lookup itemVidaLey en GetTrabajadoresPorEmpresaAsync

Línea 516 — antes buscaba `"VIDA_LEY"` o `"VIDA LEY"` exactos; ahora:
```csharp
var itemVidaLey = sctrItems.FirstOrDefault(i => i.Nombre.ToUpper().Contains("VIDA"));
```
Más tolerante a variaciones de nombre en BD.

### SctrVidaLeyRepository — vigenciaHab siempre desde dto en CreateAsync

`CreateAsync`: `vigenciaHab` dejó de depender de `esAbril`:
```csharp
// ANTES: solo asignaba vigencia si esAbril=true
var vigenciaHab = esAbril && dto.Vigencia.HasValue ? ... : null;

// DESPUÉS: siempre toma dto.Vigencia
var vigenciaHab = dto.Vigencia.HasValue
    ? DateTime.SpecifyKind(dto.Vigencia.Value, DateTimeKind.Utc)
    : (DateTime?)null;
```
`estadoHab` (Aprobado/Enviado) sigue dependiendo de `esAbril`.

### SctrVidaLeyRepository — Vigencia en SsSctrVidaley al crear

`CreateAsync`: el objeto `SsSctrVidaley` ahora incluye `Vigencia` al construirse:
```csharp
Vigencia = dto.Vigencia.HasValue ? DateTime.SpecifyKind(dto.Vigencia.Value, DateTimeKind.Utc) : null,
```
Antes la vigencia solo se asignaba en `AprobarAsync`.

### SctrVidaLeyRepository — SctrId filtrado por tipo en GetTrabajadoresPorEmpresaAsync

La subquery que calcula el `SctrId` activo por worker ahora filtra por tipo:
```csharp
&& (tipo == null || s.Tipo == tipo)
```
Antes devolvía el MAX(id) entre SCTR y VIDA_LEY mezclados, lo que podía retornar el id de la póliza del tipo incorrecto.

### Logs temporales de debug añadidos

- `AprobarAsync`: log al inicio con `polizaId`, `tipo` y `workerIdsAprobados`
- `GetTrabajadoresPorEmpresaAsync`: log antes de aplicar filtro `estadoVidaLey` con el valor recibido y el `EstadoVidaLey` de cada worker

**Eliminar antes de merge a master.**

### HabTrabajadorRepository — EstadoCalc incluye "Enviado" como No Autorizado

`Features/HabilitacionModule/Infrastructure/Repositories/HabTrabajadorRepository.cs` línea 84:

```csharp
// ANTES:
(h.Estado == "Falta" || h.Estado == "Rechazado" || h.Estado == "Vencido")

// DESPUÉS:
(h.Estado == "Falta" || h.Estado == "Rechazado" || h.Estado == "Vencido" || h.Estado == "Enviado")
```

Workers con entregables en estado `"Enviado"` (pendiente de aprobación) ahora se marcan "No Autorizado" en vez de "Habilitado". Commit `53732bb`.

### HabTrabajadorRepository — UpdateEntregableAsync resetea InduccionCompletada al rechazar ítem 12

`Features/HabilitacionModule/Infrastructure/Repositories/HabTrabajadorRepository.cs`

Cuando `ItemId == HabItemIds.InduccionObra (12)` y el nuevo estado es `"Falta"`, resetea en el mismo `SaveChangesAsync` todas las filas activas (`FechaFin IS NULL`) de `WorkerProyecto` del worker:

```csharp
if (entregable.ItemId == HabItemIds.InduccionObra
    && string.Equals(dto.Estado, "Falta", StringComparison.OrdinalIgnoreCase))
{
    var wpRows = await ctx.WorkerProyecto
        .Where(wp => wp.WorkerId == entregable.WorkerId && wp.FechaFin == null)
        .ToListAsync();
    foreach (var wp in wpRows)
    {
        wp.InduccionCompletada = false;
        wp.FechaInduccion = null;
    }
}
```

Garantiza que si se rechaza/revierte la inducción, el worker vuelva a la cola de programación de inducciones. Commit `0403639`.

---

## Sesión 2026-05-20 (segunda parte) — ArquitecturaComercial: SPI y snapshot semanal

### ac_actividades — columna indice renombrada a orden, nueva columna spi

Cambios aplicados directamente en BD (sin migración EF):
- `indice` renombrada a `orden` (`numeric` → sin cambio de tipo, sigue siendo `int?`)
- Nueva columna `spi numeric(5,2)` — Schedule Performance Index

### AcActividad — modelo EF actualizado

`Infrastructure/Models/AcActividad.cs`:
- `[Column("indice")] Indice` → `[Column("orden")] Orden`
- Nueva propiedad `[Column("spi")] public decimal? Spi { get; set; }`

### ActividadListItemDTO y GanttActividadDTO — Indice → Orden, Spi añadido

- `ActividadListItemDTO`: `Indice` → `Orden`, nueva propiedad `Spi (decimal?)`
- `GanttActividadDTO`: `Indice` → `Orden`

Todos los mapeos del repositorio actualizados en consecuencia (listado paginado, `CreateActividad`, `UpdateActividad`, `GetActividadItemById`, `GetGantt`, `GenerarActividades`).

### ArquitecturaComercialRepository — CalcularSpi() y CalcularPorcentajeAvance()

Dos nuevos helpers `private static` en `ArquitecturaComercialRepository.cs`:

**`CalcularSpi(AcActividad a)`** — lógica:
- `InicioProgramado IS NULL` → 0
- `FinEfectivo IS NOT NULL` → Round(diasPlanificados / diasReales, 2) donde `diasPlanificados = FinProgramado - InicioProgramado` y `diasReales = FinEfectivo - (InicioEfectivo ?? InicioProgramado)`
- `InicioEfectivo IS NOT NULL` → Round((hoy - InicioEfectivo) / diasPlanificados, 2)
- Else → 0
- Denominadores 0 → 0 (guard explícito)

**`CalcularPorcentajeAvance(AcActividad a, DateOnly today)`** — lógica:
- Sin `InicioProgramado` → 0
- Con `FinEfectivo` → 100
- Con `InicioEfectivo` y `FinProgramado` → `Min(99, Max(0, (hoy - InicioEfectivo) / (FinProgramado - InicioEfectivo) * 100))`
- Else → 0

`CalcularSpi` se llama al final de `UpdateActividad` y `PatchActividad` antes de `SaveChangesAsync` → persiste el SPI calculado en la columna `spi` de `ac_actividades`.

### AcAvanceSemanal — nuevo modelo, DbSet y snapshot semanal

**Tabla existente en BD:** `ac_avance_semanal (id, actividad_id, semana date, porcentaje_avance numeric(5,2), spi numeric(5,2), created_at)`

**Archivos nuevos:**
- `Infrastructure/Models/AcAvanceSemanal.cs` — modelo mapeado a la tabla
- `Application/DTOs/ArquitecturaComercial/AvanceSemanalSnapshotResultDTO.cs` — `{ Total, Semana, Message }`

**Cambios en archivos existentes:**
- `Shared/Data/AppContext.cs` — `DbSet<AcAvanceSemanal>` añadido
- `Infrastructure/Interfaces/IArquitecturaComercialRepository.cs` — firma `SnapshotAvanceSemanal()`
- `Application/Interfaces/IArquitecturaComercialService.cs` — idem
- `Infrastructure/Repositories/ArquitecturaComercialRepository.cs` — método público `SnapshotAvanceSemanal()` + helper `CalcularPorcentajeAvance`
- `Application/Services/ArquitecturaComercialService.cs` — delegación al repositorio
- `Controllers/ArquitecturaComercialController.cs` — inyección de `IConfiguration`; endpoint `POST /api/v1/arquitectura-comercial/avance-semanal/snapshot` con guard CronSecret

**Lógica del endpoint snapshot:**
- Autenticación: `Authorization: Bearer {CronSecret}` (mismo patrón que `/reminder` y `/alertas/*`)
- Semana = lunes de la semana actual: `today.AddDays(-(int)today.DayOfWeek + 1)`
- Trae todas las `AcActividad` con `Activo = true`
- Por cada actividad calcula `Spi` y `PorcentajeAvance`
- Upsert vía EF: carga filas existentes de la semana en diccionario → actualiza si existe, inserta si no
- Responde `{ total, semana, message }`

**Endpoint:**
```
POST /api/v1/arquitectura-comercial/avance-semanal/snapshot   [AllowAnonymous + CronSecret]
```

---

## Sesión 2026-05-21 — ArquitecturaComercial: UserId2, control de acceso por rol

### ac_actividades — nueva columna user_id2

Columna añadida directamente en BD (sin migración EF): `user_id2 int` (FK→workers, nullable) — segundo responsable de la actividad.

### AcActividad — modelo EF

`Infrastructure/Models/AcActividad.cs`:
- Nueva propiedad `[Column("user_id2")] public int? UserId2 { get; set; }`

### DTOs actualizados

- `AcActividadCreateDTO` — nueva propiedad `UserId2?`
- `AcActividadUpdateDTO` — nueva propiedad `UserId2?`
- `ActividadListItemDTO` — nuevas propiedades `UserId2?` y `ResponsableNombre2?`

### ArquitecturaComercialRepository — join w2 en todas las queries

Los cuatro métodos que construyen `ActividadListItemDTO` (`GetActividades`, `GetActividadItemById`, `CreateActividad`, `UpdateActividad`) reciben el join:
```csharp
from w2 in ctx.Worker.Where(x => x.Id == a.UserId2).DefaultIfEmpty()
// select:
ResponsableNombre2 = w2 != null ? (w2.Person != null ? w2.Person.FullName : null) : null,
```
El DTO de respuesta incluye `UserId2 = act.UserId2` y `ResponsableNombre2`.

`PatchActividad` agrega el case `"userid2"` al switch de campos patcheables.

`CreateActividad` y `UpdateActividad` persisten `UserId2 = dto.UserId2`.

### GetActividades — filtro por rol

`GetActividades` recibe dos nuevos parámetros en toda la cadena (interface → service → repository → controller):

| Parámetro | Tipo | Uso |
|-----------|------|-----|
| `userId` | `int?` | Id del usuario autenticado (de `ClaimTypes.NameIdentifier`) |
| `esUsuarioAc` | `bool` | Si `true`, filtra actividades donde `user_id == userId OR user_id2 == userId` |

Filtro en repositorio (se aplica solo cuando `esUsuarioAc && userId > 0`):
```csharp
baseQuery = baseQuery.Where(x => x.Actividad.UserId == userId || x.Actividad.UserId2 == userId);
```

### ArquitecturaComercialController — control de acceso en GetActividades

Guard de rol antes del try, con prioridad GESTOR sobre USUARIO. Usa el mismo patrón `OrdinalIgnoreCase` del resto del proyecto (`SctrVidaLeyController`, `HabTrabajadorController`, etc.):
```csharp
var rolesUsuario = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
var esGestor = rolesUsuario.Contains("GESTOR DE ARQUITECTURA COMERCIAL", StringComparer.OrdinalIgnoreCase);
bool esUsuarioAc;
if (esGestor)
    esUsuarioAc = false;   // ve todas las actividades
else if (rolesUsuario.Contains("USUARIO DE ARQUITECTURA COMERCIAL", StringComparer.OrdinalIgnoreCase))
    esUsuarioAc = true;    // ve solo las suyas (user_id o user_id2)
else
    return Forbid();       // 403 para cualquier otro rol
```

`ILogger<ArquitecturaComercialController>` inyectado en constructor (disponible para logs futuros).

### Nuevo rol pendiente en BD

```sql
INSERT INTO roles (role_description, active, state)
VALUES ('GESTOR DE ARQUITECTURA COMERCIAL', true, 'ACTIVO');
-- Luego asignar a usuarios en user_roles y features en role_feature
```

### Frontend — nuevo-entregable y nuevo-hito: nombre personalizado

Cambios en dos componentes AC del frontend (`nuevo-entregable.ts/html` y `nuevo-hito.ts/html`):

**TypeScript:**
- Dos nuevas propiedades: `nombrePersonalizado = false` y `nombreLibre = ''`
- `ngOnChanges`: las resetea a `false` / `''` al abrir el modal
- `canSubmit`: si `nombrePersonalizado` ON → solo exige `nombreLibre.trim()` no vacío; si OFF → lógica original
- `submit()`: nombre = `nombreLibre.trim()` si ON, o `nombreCalculado` si OFF

**HTML:**
- Campo "Nombre generado" (readonly) se muestra solo con `*ngIf="!nombrePersonalizado"`
- Input de texto libre aparece con `*ngIf="nombrePersonalizado"`
- Checkbox `[(ngModel)]="nombrePersonalizado"` con label "Nombre personalizado" debajo de ambos inputs

---

## Sesión 2026-05-21 (segunda parte) — AC: dashboard-v2, alertas y lógica de fechas

### Nuevos DTOs

| Archivo | Contenido |
|---------|-----------|
| `Application/DTOs/ArquitecturaComercial/DashboardFiltroDTO.cs` | `CategoriaId?`, `ProyectoId?`, `UserId?`, `Semana?`, `Mes?`, `Anio?` |
| `Application/DTOs/ArquitecturaComercial/ActividadAlertaDTO.cs` | `Id`, `Nombre`, `Proyecto`, `Responsable1/2`, `EmailResp1/2`, `FechaInicio/Fin`, `Estado`, `Spi`, `Tipo`, `Categoria`, `DiasRestantes` |
| `Application/DTOs/ArquitecturaComercial/EnviarAlertaRequestDTO.cs` | `List<int> ActividadIds`, `string TipoAlerta` |
| `Application/DTOs/ArquitecturaComercial/TareasPorArquitectoDTO.cs` | `TareasPorArquitectoDTO`, `AvanceSemanalDTO`, `EficienciaSpiDTO`, `CategoriaItemDTO` |

`ArqComercialDashboardDTO` ampliado: nuevos campos `TareasPorArquitectoDTO[]`, `AvanceSemanalDTO[]`, `EficienciaSpiDTO[]`, `CategoriaItemDTO[]`. `HitoCriticoDTO` ahora incluye `Id`.

### Nuevos endpoints (ArquitecturaComercialController)

```
GET  /api/v1/arquitectura-comercial/dashboard-v2     [DashboardFiltroDTO desde query]
     → GESTOR: ve todo; USUARIO AC: UserId se fuerza desde JWT; otro rol: 403

GET  /api/v1/arquitectura-comercial/alertas/{tipoAlerta}   [DashboardFiltroDTO desde query]
     → tipos: VENCIDA | VENCE_SEMANA | ARRANQUE | HITO_PROXIMO
     → devuelve List<ActividadAlertaDTO>

POST /api/v1/arquitectura-comercial/alertas/enviar    body: EnviarAlertaRequestDTO
     → EnviarAlertasActividades: envía email a gestores AC y encargados de las actividades indicadas
```

### ArquitecturaComercialService — nuevas inyecciones

`IDbContextFactory<AppDbContext>` e `IEmailService` inyectados en constructor.

`EnviarAlertasActividades` consulta emails de gestores vía JOIN manual:
```csharp
ctx.User.Join(ctx.UserRole, ...).Join(ctx.Role, ...)
    .Where(x => x.RoleDescription.ToUpper() == "GESTOR DE ARQUITECTURA COMERCIAL")
    .Select(x => x.Email)
```

### Lógica de estado basada en fechas (no en campo `estado`)

Todos los cálculos de KPIs y alertas en `GetDashboardDataFiltrado` y `GetActividadesPorAlerta` usan `FinEfectivo`/`InicioEfectivo`:

| Concepto | Lógica |
|----------|--------|
| Culminada | `FinEfectivo != null` |
| En proceso | `InicioEfectivo != null && FinEfectivo == null` |
| Vencida | `FinEfectivo == null && FinProgramado < today` |
| Pendiente | `InicioEfectivo == null && InicioProgramado > today` |
| Vence esta semana | `FinEfectivo == null && FinProgramado ∈ [semLunes, semDomingo]` |
| Arranca esta semana | `InicioEfectivo == null && InicioProgramado ∈ [semLunes, semDomingo]` |
| Hito próximo 14 días | `Tipo=="HITO" && FinEfectivo == null && FinProgramado ∈ [today, today+14]` |

El campo `estado` en BD ya no se usa para calcular KPIs ni alertas.

### Fallback ResponsableArqComId en GetDashboardDataFiltrado y GetActividadesPorAlerta

`AcActividad.UserId` es `NULL` en Hitos y Entregables que no tienen responsable directo. El responsable real de esas actividades es `project.responsable_arq_com_id` (FK→workers).

En ambos métodos se carga un mapa de fallback:
```csharp
var proyectoResponsableMap = proyectos
    .Where(p => p.ResponsableArqComId != null)
    .ToDictionary(p => p.ProjectId, p => p.ResponsableArqComId!.Value);

var resp1Id = a.UserId ??
    (proyectoResponsableMap.TryGetValue(a.ProjectId, out var rid) ? rid : (int?)null);
```

Este `resp1Id` se usa en lugar de `a.UserId` directo para:
- Calcular `workerIds` (qué workers cargar)
- Filtrar tareas por arquitecto en `tareasPorArquitectoDetalle`
- Contar `Completadas` en `supervisores`
- Campos `Responsable1` y `EmailResp1` en `ActividadAlertaDTO`

`UserId2` (`ResponsableNombre2`) no tiene fallback — es siempre directo desde `AcActividad.UserId2`.

---

## §MIGRACIÓN MASIVA 2026-05-22

### Estado (2026-05-23) — FASE 1 COMPLETADA
Datos de 74 empresas + 2,339 trabajadores importados. ss_empresa_contratista eliminada. Backend listo para flujo de activación.
Pendiente: EMOs, Equipos, SCTR → scripts Python → segunda vuelta migración.

### Mapeo IDProyecto SharePoint → project_id BD (confirmado)
SP=1→32, SP=2→1, SP=3→3, SP=4→2, SP=22→4, SP=36→41, SP=37→40,
SP=40→5, SP=42→36, SP=43→7, SP=44→13, SP=46→6, SP=47→11,
SP=48→8, SP=62→12, SP=64→14, SP=66→10, SP=68→9, SP=76→15,
SP=78→17, SP=79→16, SP=89→39, SP=90→40, SP=91→41

### Columnas nuevas en contributor (YA EJECUTADO)
ALTER TABLE contributor ADD COLUMN contributor_nombre_comercial VARCHAR(255), ADD COLUMN sp_password_temp VARCHAR(255);

### Archivos Excel listos ✅

#### 1. Lista_contratistas_limpia.xlsx — 74 empresas → contributor + contractor_email
- contributor_name←RazonSocial, contributor_nombre_comercial←NombreComercial
- contributor_ruc←RUC, sp_password_temp←Password, id_sharepoint←IDListaCont
- 4 emails → contractor_email (Gerente, Administrador, Residente, SSOMA)
- es_abril=false, active=true siempre

#### 2. entregables_empresa_estandarizados.xlsx — 8,300 filas → ss_hab_empresa
- Cols: NombreComercial, project_id_BD, item_id, estado, vigencia
- 352 combinaciones empresa+proyecto × 25 items c/u
- NombreComercial = llave de cruce con contributor post-import

#### 3. trabajadores_limpios.xlsx — 2,339 trabajadores (914 Casa + 1,425 Contratistas) → workers + worker_vinculaciones + ss_hab_worker_proyecto
- Cols: id_trabajador, dni, nombre_completo, email_personal, fecha_ingreso,
  fecha_nacimiento, categoria, ocupacion, area, subarea, obra_oficina,
  contrata_casa, condicion_medica, notas, puntos_infraccion, celular,
  sctr, project_id_BD, empresa_nombre, proyectos_habilitado
- empresa_nombre: Casa→contributor_id BD directo (int) | Contratista→NombreComercial
- proyectos_habilitado: lista project_id_BD separados por coma → ss_hab_worker_proyecto (AMBOS tipos)
- 0 DNI duplicados, 0 IDProyecto no mapeado ✅

#### 4. entregables_trabajadores_limpios.xlsx — 26,223 filas → ss_hab_trabajador
- Cols: id_trabajador, item_id, estado, vigencia
- id_trabajador = llave de cruce con workers post-import
- Lógica: ss_item_trabajador.aplica_a + aplica_categoria + aplica_obra_oficina +
  excluye_obra_oficina + excluye_categoria_contratista
- NOTA: ss_item_trabajador_regla NO se usa — lógica hardcodeada en ss_item_trabajador
- Casa: 15-17 items/trab | Contratistas: 8-9 items/trab

### Pendiente procesar
5. EMOs → worker_emos
6. Equipos → ss_equipo + ss_hab_equipo
7. SCTR trabajadores → ss_sctr_vidaley_worker

### Tablas hijas de workers a borrar (orden FK)
ss_hab_trabajador, worker_vinculaciones, ss_hab_worker_proyecto, ss_induccion,
worker_emos, ss_programacion_emos, ss_sctr_vidaley_worker, ss_alertas_emo,
ss_eval_supervisor, ss_hab_bloqueo_log, ss_interconsultas, ss_seguimientos_medicos,
ss_trabajador_restringido (178 — PRESERVAR), worker_eventos, ga_solicitud_salida

### Tablas hijas de contributor(externos) a borrar
ss_hab_empresa, ss_empresa_proyecto, ss_equipo, ss_tareo_detalle_contratista,
ss_sctr_vidaley, worker_emos(empresa_origen), worker_emo_convalidaciones, ss_hab_documento_version

### Tablas NO tocar
ss_clinica_*, catálogos SSOMA, Phase/Stage/Layer, AcPlantillas, ac_categorias,
ac_especialidades, ac_etapas, role, feature, role_feature, project, app_user,
ss_trabajador_restringido (blacklist — PRESERVAR)

### Flujo activación empresa (IMPLEMENTADO 2026-05-23)
- `POST /api/v1/habilitacion/auth/validar-migracion` `{ ruc, spPassword }` → valida `contributor.sp_password_temp`; retorna `{ nombreComercial, razonSocial }`
- `POST /api/v1/habilitacion/auth/activar-migracion` `{ ruc, spPassword, email, password }` → crea/reutiliza `app_user`, crea `contractor_user` + `user_role` (roleId=11), limpia `sp_password_temp`; frontend redirige a login normal

### Multi-usuario por empresa (segunda fase — PENDIENTE)
ss_contratista_usuario, ss_contratista_usuario_proyecto, ss_contratista_auditoria
Roles: OWNER | ADMIN | GESTOR con scope ALL | BY_PROJECT

---

## §2026-05-23 — Eliminación ss_empresa_contratista

### Resumen
`ss_empresa_contratista` era una tabla legacy SSOMA que duplicaba datos de `contributor`. Se eliminó en su totalidad. Todas las FKs migradas a `contributor.contributor_id`.

### Migraciones EF aplicadas
| Migration | Descripción |
|---|---|
| `20260522182631_AddContributorMigracionFields` | `sp_password_temp` + `contributor_nombre_comercial` en contributor; tablas `ac_avance_semanal`, `costos_presupuestos_email`; columnas GA + AC |
| `20260523002524_RemoveSsEmpresaContratista` | Drop `ss_empresa_contratista` (CASCADE); migra empresa_id vía id_legacy; elimina empresa_id de ss_reset_token; agrega FKs a contributor |

Ambas migraciones reescritas como SQL idempotente porque la BD estaba por delante de EF (cambios manuales previos).

### Arquitectura resultante (empresas contratistas)
```
contributor (es_abril=false)     ← empresa contratista canónica
  └── contractor                 ← registro homologación (state_id=2 APROBADO)
        └── contractor_email     ← emails (sin user_id hasta activación)
  └── ss_empresa_proyecto        ← proyectos donde opera (empresa_id → contributor_id)
  └── ss_hab_empresa             ← entregables de habilitación (empresa_id → contributor_id)
  └── ss_equipo                  ← equipos (propietario_empresa_id → contributor_id)
  └── ss_induccion               ← inducciones (empresa_id → contributor_id)
  └── ss_sctr_vidaley            ← SCTR/Vida Ley (empresa_id → contributor_id)
  └── ss_hab_bloqueo_log         ← bloqueos (empresa_sol/prop_id → contributor_id)
  └── ss_eval_supervisor         ← evaluaciones supervisor (empresa_id → contributor_id)
```

### Archivos backend modificados (2026-05-23)
- `SsEmpresaContratista.cs` → ELIMINADO
- `AppDbContext.cs` → eliminado DbSet<SsEmpresaContratista>
- `SsHabEmpresa.cs`, `SsInduccion.cs`, `SsSctrVidaley.cs`, `SsEmpresaProyecto.cs`, `SsHabBloqueoLog.cs`, `SsEvalSupervisor.cs` → nav property `Empresa` → `Contributor`
- `EmpresaContratistaRepository.cs` → reescrito sobre Contributor + Contractor + ContractorEmail
- `IEmpresaContratistaRepository.cs` → retorna DTOs directamente (sin SsEmpresaContratista)
- `HabEmpresaRepository.cs` → eliminado `ResolveSsEmpresaId`, usa `empresaId` directo
- `HabTrabajadorRepository.cs` → `ssEmpresaId = empresaId` directo; `ep.EmpresaId == empresaId.Value` (sin IdLegacy)
- `EquipoRepository.cs` → GetPagedAsync usa `Contributor`; UpdateEntregableAsync usa `empresaId` directo
- `SctrVidaLeyRepository.cs` → eliminado dual-path, `contributorId = empresaId` directo
- `ContractorManagementRepository.Approve()` → eliminado bloque creación `ss_empresa_contratista`
- `ContratistaAuthService.cs` → GetEmpresasParaLoginAsync, SolicitarActivacionAsync, ActivarCuentaAsync, ValidarMigracionAsync, ActivarMigracionAsync reescritos sobre Contributor
- `BandejaRepository.cs` → SQL raw: `ss_empresa_contratista` → `contributor`, `razon_social` → `contributor_name`
- `CatalogosRepository.cs` → `TipoActividad = e.ContributorEconomicActivityDescription ?? ""`
- `AuditoriaInterceptor.cs` → eliminada entrada `"ss_empresa_contratista"` de TablasAuditar

---

## §MIGRACIÓN MASIVA — GUÍA COMPLETA (2026-05-23)

### Orden de borrado (FASE 0)

```sql
DELETE FROM ss_hab_documento_version;    -- PRIMERA — tiene FK hacia ss_hab_trabajador
DELETE FROM ss_hab_trabajador;
DELETE FROM ss_hab_worker_proyecto;
DELETE FROM worker_vinculaciones;
DELETE FROM ss_induccion;
DELETE FROM worker_emos;
DELETE FROM ss_programacion_emos;
DELETE FROM ss_sctr_vidaley_worker;
DELETE FROM ss_alertas_emo;
DELETE FROM ss_eval_supervisor;
DELETE FROM ss_hab_bloqueo_log;
DELETE FROM ss_interconsultas;
DELETE FROM ss_seguimientos_medicos;
DELETE FROM worker_eventos;
DELETE FROM ga_solicitud_salida;
DELETE FROM workers;                     -- borrar DESPUÉS de todas las hijas
DELETE FROM ss_hab_empresa;
DELETE FROM person WHERE user_id IS NULL; -- solo persons sin usuario del sistema
```

**NUNCA borrar:** `ss_trabajador_restringido` (blacklist 178 registros)

### Orden de inserción

1. `ss_hab_empresa` — `ON CONFLICT (empresa_id, proyecto_id, item_id, mes, anio) DO UPDATE`
2. `person` — **SIN email** (tabla tiene UNIQUE en email), `ON CONFLICT (document_identity_code) DO NOTHING`
3. `workers` — incluir `person_id` (FK→person); capturar IDs con `RETURNING id` via `mogrify`
4. `worker_vinculaciones` — solo si tiene `project_id` Y `fecha_ingreso`
5. `ss_hab_worker_proyecto` — `ON CONFLICT DO NOTHING`
6. `ss_hab_trabajador` — `ON CONFLICT (worker_id, item_id) DO UPDATE`

### Lecciones aprendidas

- `person` tiene UNIQUE constraint en **`document_identity_code`** Y en **`email`** → **NUNCA insertar email** al migrar (evita conflictos con cuentas del sistema existentes)
- `person_id` tiene secuencia **`public.person_person_id_seq`** → **NUNCA asignar manualmente**; dejar que la secuencia lo genere
- Usar **`mogrify`** de un solo golpe para todos los inserts (no `execute_values` — no retorna IDs correctamente en este entorno)
- **Recuperar `person_id` por DNI** después del insert: `SELECT person_id FROM person WHERE document_identity_code = %s`
- `ss_hab_documento_version` tiene FK hacia `ss_hab_trabajador` → debe borrarse **primero** (olvidada en la primera corrida)
- `contributor_id` para personal Casa: resolver en runtime por RUC contra `contributor WHERE es_abril = true`
- `IDTrabajador` SP viene como string `"2.010"` (punto = separador de miles) → limpiar con `replace('.', '')` antes de usar como clave

### Conteos de verificación post-migración

```sql
SELECT COUNT(*) FROM ss_hab_empresa;         -- 8202
SELECT COUNT(*) FROM workers;                -- 2336
SELECT COUNT(*) FROM worker_vinculaciones;   -- 2318
SELECT COUNT(*) FROM ss_hab_worker_proyecto; -- 4273
SELECT COUNT(*) FROM ss_hab_trabajador;      -- 26216
SELECT COUNT(*) FROM person WHERE user_id IS NULL; -- ~2336 nuevos
```

### Para correr el script

```bash
cd C:\Users\conta\Abril_Backend\Migracionfinal
python migracion_masiva.py
```

Dependencias: `python -m pip install psycopg2-binary openpyxl pandas`

---

## Sesión 2026-05-24 — fixes flujo contratista entregables

### ContratistaAuthService.ActivarMigracionAsync — setear UserId en contractor_email

Al activar cuenta vía `POST /habilitacion/auth/activar-migracion`, ahora se setea `ContractorEmail.UserId = user.UserId` en **todas** las filas de `contractor_email` del mismo `contractor_id`, antes del `SaveChangesAsync` final. Sin esto, `LoginAsync` (que busca `contractor_email WHERE user_id = user.UserId`) no encontraba la empresa y retornaba 403.

### HabTrabajadorController/Repo + HabEmpresaController/Repo — contratista: solo obsContratista y archivo

**Patrón aplicado a ambos endpoints** (`PUT /habilitacion/trabajadores/{id}/entregables/{id}` y `PUT /habilitacion/empresas/{empresaId}/entregables/{id}`):

**Controller:** reemplazado el `return 403` por sobreescritura silenciosa del DTO cuando `tipo == "CONTRATISTA"`:
```csharp
if (esContratista)
{
    dto.Estado = "Enviado";
    dto.Vigencia = null;
}
```
El 403 bloqueaba requests legítimos del frontend que enviaban estado incorrecto o vacío.

**Repositorios:** `Estado` y `Vigencia` solo se actualizan si `!string.IsNullOrEmpty(dto.Estado)`. `HabEmpresaRepository` además convertido a patch-style en todos los campos opcionales (`ArchivoUrl`, `ObsAbril`, `ObsContratista`, `Mes`, `Anio`) con null-guard — evita pisar valores existentes si el payload no los envía.

### WorkerEntregableUpdateValidator — Estado opcional

`NotEmpty()` eliminado de la regla `Estado`. La validación de formato solo corre `When(!string.IsNullOrEmpty(x.Estado))`. FluentValidation ya no rechaza con 400 antes de entrar al controller cuando el contratista envía solo `obsContratista`.

### EquipoController/Repo — mismo patrón CONTRATISTA que trabajadores y empresas

`PUT /habilitacion/equipos/entregables/{id}`: mismo bloque de sobreescritura DTO para CONTRATISTA. `EquipoRepository.UpdateEntregableAsync`: Estado y Vigencia separados con sus propios guards.

---

## Sesión 2026-05-24 (segunda parte) — Vigencia patch-style y ResolverVigencia extendida

### Vigencia separada del guard de Estado en trabajadores y empresas

**Problema:** `Vigencia` estaba dentro del bloque `if (!string.IsNullOrEmpty(dto.Estado))`. Si un admin enviaba solo `vigencia` sin `estado`, la fecha no se persistía.

**Fix `HabTrabajadorRepository.UpdateEntregableAsync`:**
```csharp
if (!string.IsNullOrEmpty(dto.Estado))
    entregable.Estado = dto.Estado;
if (!string.IsNullOrEmpty(dto.Estado) || dto.Vigencia.HasValue)
    entregable.Vigencia = HabilitacionDateHelper.ResolverVigencia(
        entregable.Item?.RequiereVigencia ?? true, entregable.Estado, dto.Vigencia);
```
El guard de vigencia ahora dispara cuando **o** cambia el estado **o** viene vigencia explícita. El estado ya actualizado se pasa a `ResolverVigencia` — correcto para "Aprobado + no requiereVigencia → 2040".

**Fix `HabEmpresaRepository.UpdateEntregableEmpresaAsync`:**
```csharp
if (!string.IsNullOrEmpty(dto.Estado))
    entregable.Estado = dto.Estado;
if (dto.Vigencia.HasValue)
    entregable.Vigencia = HabilitacionDateHelper.AsUtc(dto.Vigencia);
```
Vigencia se actualiza independientemente del estado (empresa no tiene lógica de sentinel por estado).

### ResolverVigencia extendida a "Enviado" — `HabilitacionDateHelper.cs`

**Antes:** sentinel `2040-12-31 UTC` solo para `estado == "Aprobado"` + `requiereVigencia == false`.

**Ahora:** también para `estado == "Enviado"` + `requiereVigencia == false`:
```csharp
var esSintetico = !requiereVigencia
    && (string.Equals(estado, "Aprobado", StringComparison.OrdinalIgnoreCase)
        || string.Equals(estado, "Enviado", StringComparison.OrdinalIgnoreCase));
if (esSintetico)
    return DateTime.SpecifyKind(new DateOnly(2040, 12, 31).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
return AsUtc(dtoVigencia);
```

Cuando un CONTRATISTA sube un documento a un ítem que no requiere vigencia (e.g. `ss_item_equipo.requiere_vigencia = false`), el estado se fuerza a `"Enviado"` y la vigencia se asigna automáticamente como `2040-12-31`.

### HabEmpresaRepository y EquipoRepository — .Include(h => h.Item) + ResolverVigencia

Para que ambos repos puedan leer `RequiereVigencia`, se agregó `.Include(h => h.Item)` a la query del entregable en `UpdateEntregableEmpresaAsync` y `UpdateEntregableAsync` (equipo). Ambos ahora usan `ResolverVigencia` en lugar de `AsUtc`:

```csharp
var entregable = await ctx.SsHabEquipo  // o SsHabEmpresa
    .Include(h => h.Item)
    .FirstOrDefaultAsync(h => h.Id == id) ...

if (!string.IsNullOrEmpty(dto.Estado))
    entregable.Estado = dto.Estado;
if (!string.IsNullOrEmpty(dto.Estado) || dto.Vigencia.HasValue)
    entregable.Vigencia = HabilitacionDateHelper.ResolverVigencia(
        entregable.Item?.RequiereVigencia ?? true, entregable.Estado, dto.Vigencia);
```

`HabTrabajadorRepository` ya tenía `.Include(h => h.Item)` — solo hereda el fix del helper.

---

## Sesión 2026-05-24 (tercera parte) — SharePoint: migración a bibliotecas propias

### Diagnóstico previo

`ISharePointHabService.SubirArchivoAsync` subía todos los archivos (trabajadores, empresa, equipos) al **drive predeterminado** del sitio `Sites:Habilitacion`. No había `LibraryId` explícito — el DriveId se resolvía dinámicamente con `GET /sites/{siteId}/drive`.

Endpoints existentes para CONTRATISTA (inventariado):
- `GET /trabajadores` — sí filtra por JWT
- `GET /equipos` — sí filtra por JWT
- `GET /empresas/{id}/entregables` — sin guard (cualquier contratista puede consultar cualquier empresa)
- `GET /empresas/{id}/proyectos-disponibles` — sí tiene guard `EmpresaJwtCoinside`
- No existe dashboard/resumen para contratistas

### appsettings.json — nueva sección SharePoint (commit `5cf6a24`)

```json
"SharePoint": {
  "Sites": {
    "Habilitacion": {
      "SiteId": "abrilinmob.sharepoint.com,d9e26806-d535-4353-9610-195978e20390,a7b7032f-511e-4b53-8a87-508a190b3c7c",
      "TrabajadoresLibraryId": "8693cb8a-7d15-4c32-97d3-a0946aba77f5",
      "EmpresaLibraryId": "d0c56309-2b02-414b-b762-c8475bb09199",
      "EquiposLibraryId": "d12d18df-e912-474b-8964-7c3c10bea45d"
    }
  }
}
```

**No commitear** `appsettings.Local.json` ni `appsettings.Production.json` (gitignored).

### SharePointHabService.cs — tres cambios

1. **Cache:** `_cachedDriveId: string?` → `_driveIdCache: ConcurrentDictionary<string, string>` (clave = `libraryId` o `"default"`). Necesario porque ahora hay 3 drives distintos.

2. **`ResolverLibraryId(contexto)`** — nuevo método privado:
```csharp
private string? ResolverLibraryId(string contexto)
{
    var c = (contexto ?? string.Empty).ToLowerInvariant();
    if (c.Contains("trabajadores")) return _configuration["SharePoint:Sites:Habilitacion:TrabajadoresLibraryId"];
    if (c.Contains("empresas"))     return _configuration["SharePoint:Sites:Habilitacion:EmpresaLibraryId"];
    if (c.Contains("equipos"))      return _configuration["SharePoint:Sites:Habilitacion:EquiposLibraryId"];
    return null;  // fallback → drive predeterminado
}
```
El mismo método se llama en `SubirArchivoAsync` (con el `contexto` del request) y en `GetDownloadUrlAsync` (con el `archivoUrl`/path almacenado).

3. **`GetDriveIdAsync`** ahora acepta `string? libraryId = null`:
   - Con libraryId → `GET /v1.0/sites/{siteId}/lists/{libraryId}/drive`
   - Sin libraryId → `GET /v1.0/sites/{siteId}/drive` (drive predeterminado)

**Ruta resultante de upload por contexto:**

| contexto | LibraryId usado | Biblioteca SharePoint |
|---|---|---|
| `"habilitacion/trabajadores/..."` | `TrabajadoresLibraryId` | Biblioteca Trabajadores |
| `"habilitacion/empresas/..."` | `EmpresaLibraryId` | Biblioteca Empresas |
| `"habilitacion/equipos/..."` | `EquiposLibraryId` | Biblioteca Equipos |
| cualquier otro | `null` | Drive predeterminado del sitio |

### Bloque para appsettings.Local.json de Samuel

```json
"SharePoint": {
  "Sites": {
    "Habilitacion": {
      "SiteId": "abrilinmob.sharepoint.com,d9e26806-d535-4353-9610-195978e20390,a7b7032f-511e-4b53-8a87-508a190b3c7c",
      "TrabajadoresLibraryId": "8693cb8a-7d15-4c32-97d3-a0946aba77f5",
      "EmpresaLibraryId": "d0c56309-2b02-414b-b762-c8475bb09199",
      "EquiposLibraryId": "d12d18df-e912-474b-8964-7c3c10bea45d"
    }
  }
}
```

---

## Sesión 2026-05-24 — fixes auth contratistas + Sunat config

### GetContratistasFeatureKeysAsync — features por usuario (no por rol global)

Ver sección "ContratistaAuthService — allowedFeatures desde BD (por roles del usuario)" en sesión 2026-05-18 segunda parte — ya actualizada inline.

### Sunat — sección de config ausente en appsettings (bug pendiente)

`Program.cs` registra `ISunatService` leyendo `Sunat:BaseUrl` y `Sunat:Token`, pero **ningún appsettings tiene esa sección**. Resultado: `HttpClient.BaseAddress = null` → `GET /api/v1/contractorRegistration/ruc/{ruc}` devuelve 500 silencioso (el `catch` del controller no loguea la excepción).

Fix pendiente — agregar en `appsettings.Production.json` y `appsettings.Local.json`:
```json
"Sunat": {
  "BaseUrl": "https://api.decolecta.com",
  "Token": "<mismo token que Reniec:Token>"
}
```
El proveedor es el mismo que Reniec (`https://api.decolecta.com`). Confirmar si el token es el mismo.

### contractor_person_type — valores solo en BD

La tabla `contractor_person_type` clasifica el rol del contacto de una empresa (representante legal, técnico, etc.). Se crea en la migración `20260518193906` pero **no tiene seed data en el repo**. Los valores solo existen en la BD de producción. El endpoint `GET /api/v1/contractorRegistration/person-types` los expone.

### ActivarMigracionAsync — un solo app_user para todos los contractor_email

`ActivarMigracionAsync` crea/reutiliza **un único `app_user`** (el del `dto.Email`) y asigna ese `UserId` a **todos** los `contractor_email` del contractor sin filtro `Active`/`State`. El rol `RoleId = 11` (CONTRATISTA) está hardcodeado en el servicio.

---

## Sesión 2026-05-25 — módulo multi-usuario contratista

### Nuevas tablas (creadas manualmente en pgAdmin)

```sql
CREATE TABLE IF NOT EXISTS ss_contratista_rol (id SERIAL PRIMARY KEY, nombre VARCHAR(50) NOT NULL UNIQUE);
INSERT INTO ss_contratista_rol (nombre) VALUES ('OWNER'),('ADMIN'),('GESTOR') ON CONFLICT DO NOTHING;

CREATE TABLE IF NOT EXISTS ss_contratista_usuario (
  id SERIAL PRIMARY KEY,
  contractor_id INT NOT NULL REFERENCES contractor(contractor_id),
  user_id INT NOT NULL REFERENCES app_user(user_id),
  rol_id INT NOT NULL REFERENCES ss_contratista_rol(id),
  system_role_id INT REFERENCES role(id),   -- añadido en segunda iteración
  scope VARCHAR(20) NOT NULL DEFAULT 'TODOS',
  activo BOOL NOT NULL DEFAULT true,
  creado_en TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  creado_por INT REFERENCES app_user(user_id),
  UNIQUE(contractor_id, user_id)
);

CREATE TABLE IF NOT EXISTS ss_contratista_usuario_proyecto (
  id SERIAL PRIMARY KEY,
  contratista_usuario_id INT NOT NULL REFERENCES ss_contratista_usuario(id) ON DELETE CASCADE,
  proyecto_id INT NOT NULL REFERENCES project(project_id),
  UNIQUE(contratista_usuario_id, proyecto_id)
);

-- Añadido después:
ALTER TABLE ss_contratista_usuario ADD COLUMN IF NOT EXISTS system_role_id INT REFERENCES role(id);
UPDATE ss_contratista_usuario SET system_role_id = 11 WHERE rol_id = 1; -- OWNER → CONTRATISTA
```

### Archivos nuevos

| Archivo | Descripción |
|---|---|
| `Infrastructure/Models/SsContratistaRol.cs` | Entidad `[Table("ss_contratista_rol")]` |
| `Infrastructure/Models/SsContratistaUsuario.cs` | Entidad con `RolId` (interno) + `SystemRoleId` (FK→role) |
| `Infrastructure/Models/SsContratistaUsuarioProyecto.cs` | Relación usuario↔proyecto |
| `Application/Dtos/ContratistaUsuarios/ContratistaUsuarioDtos.cs` | `ContratistaUsuarioListDto`, `CreateDto`, `UpdateDto` |
| `Infrastructure/Interfaces/IContratistaUsuarioRepository.cs` | Interfaz repositorio |
| `Application/Interfaces/IContratistaUsuarioService.cs` | Interfaz servicio |
| `Infrastructure/Repositories/ContratistaUsuarioRepository.cs` | Implementación repositorio |
| `Application/Services/ContratistaUsuarioService.cs` | Implementación servicio |
| `Presentation/ContratistaUsuarioController.cs` | Controller `api/v1/contratista-usuarios` |

### Endpoints

```
GET    /api/v1/contratista-usuarios?contractorId={id}          → lista usuarios de la empresa
POST   /api/v1/contratista-usuarios?contractorId={id}          → invitar usuario
PUT    /api/v1/contratista-usuarios/{id}?contractorId={id}     → actualizar rol/scope/proyectos
DELETE /api/v1/contratista-usuarios/{id}?contractorId={id}     → desactivar (soft delete)
```

### Lógica de InvitarUsuarioAsync

1. Valida `SystemRoleId ∈ {11, 49}` y `RolNombre ∈ {ADMIN, GESTOR}`
2. Busca `app_user` por email — si no existe: crea uno nuevo con contraseña temporal aleatoria de 8 chars (BCrypt), `Active=true`, `EmailConfirmed=true`
3. Inserta `user_role` con `SystemRoleId` si no existe ya
4. Inserta `contractor_email` si no existe ya (`UserId + ContractorId`)
5. Crea `ss_contratista_usuario`
6. Si el `app_user` fue creado nuevo: envía email con asunto "Invitación a plataforma Abril - CASEVIP" con usuario + contraseña temporal

### Reglas de validación

- `RolNombre` válido para invitaciones/updates: solo `ADMIN` o `GESTOR`. El rol `OWNER` no puede asignarse ni desactivarse.
- `SystemRoleId` válido: `11` (CONTRATISTA) o `49` (SERVICIO DE VIGILANCIA)
- `scope = "POR_PROYECTO"` requiere `ProyectoIds` no vacío
- `NombreCompleto` en `GetUsuariosAsync`: `COALESCE(Person.FullName, User.Email)` — fallback al email cuando `Person` es null

### Roles del sistema — tabla completa conocida

| role_id | descripción |
|---------|-------------|
| 11 | CONTRATISTA |
| 49 | SERVICIO DE VIGILANCIA |
| (ver sección 8 para ids 1–10) | — |

### Contraseña temporal — generador

```csharp
private static string GenerarPasswordTemporal()
{
    const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
    return RandomNumberGenerator.GetString(chars, 8);
}
```
Usa `System.Security.Cryptography.RandomNumberGenerator` — sin `0`, `O`, `I`, `l` para evitar confusión visual.

---

## Sesión 2026-05-25 (continuación) — claim systemRoles + fixes inducciones-hoy

### ContratistaAuthService — claim "systemRoles" en JWT

`GenerarTokenDto` ahora recibe `List<int> systemRoleIds` y agrega:
```csharp
new Claim("systemRoles", string.Join(",", systemRoleIds))  // ej. "11,49"
```
Nuevo helper privado que carga los role_id del usuario:
```csharp
private static Task<List<int>> GetSystemRoleIdsAsync(AppDbContext ctx, int userId)
    => ctx.UserRole
        .Where(ur => ur.UserId == userId && ur.Active && ur.State)
        .Select(ur => ur.RoleId)
        .ToListAsync();
```
Llamado desde `LoginAsync` y `ActivarCuentaAsync` antes de invocar `GenerarTokenDto`.

### InduccionController — SERVICIO DE VIGILANCIA (role 49) ve todas las empresas

Después de forzar `empresaId` desde el JWT para CONTRATISTA, se anula el filtro si el usuario tiene `role_id = 49`:
```csharp
var systemRoles = User.FindFirst("systemRoles")?.Value ?? "";
if (systemRoles.Split(',').Contains("49"))
    empresaId = null;
```
Resultado: un usuario con `role_id = 49` ve inducciones de todas las empresas del proyecto, no solo la suya.

⚠️ **Log temporal activo** en `InduccionController.GetList`:
```csharp
_logger.LogInformation("GetInducciones — empresaId={EmpresaId}, systemRoles={SystemRoles}", ...);
```
Quitar antes de merge a master.

### ControlAccesoRepository.GetInduccionesHoyAsync — dos fixes

1. **Estado corregido:** `"PROGRAMADA"` → `"Programado"` (valor real en BD).
2. **Look-ahead eliminado:** la lógica condicional `hora >= 12 ? +2 días : +1 día` fue reemplazada por `fechaLimite = hoyLima.AddDays(1)` siempre. El endpoint muestra únicamente las inducciones de la fecha actual Lima sin anticipar el día siguiente.

```csharp
// Antes
var ahoraLima = DateTime.UtcNow.AddHours(-5);
var hoyLima = ahoraLima.Date;
var mananaLima = hoyLima.AddDays(1);
var fechaLimite = ahoraLima.Hour >= 12 ? mananaLima.AddDays(1) : mananaLima;

// Ahora
var hoyLima = DateTime.UtcNow.AddHours(-5).Date;
var fechaLimite = hoyLima.AddDays(1);
```

---

## Sesión 2026-05-25 — Bandeja bulk-aprobar, exclusiones, SharePoint multi-sitio, fix duplicados workers

### Bandeja — PATCH /bulk-aprobar

Nuevo endpoint `PATCH /api/v1/habilitacion/bandeja/bulk-aprobar`.

Body: `{ ids: int[], tipo: "TRABAJADOR"|"EMPRESA"|"EQUIPO"|"INDUCCION" }`
Respuesta: `{ procesados: int, noEncontrados: int[] }`

Implementación: itera los métodos unitarios existentes (`AprobarTrabajadorAsync`, `AprobarEmpresaAsync`, `AprobarEquipoAsync`) sin lógica nueva. Para INDUCCION usa `AprobarBatchAsync` que ya existía en `IInduccionRepository`. Sin cambios en `BandejaRepository` ni interfaces. DTOs: `BandejaBulkAprobarDto`, `BandejaBulkResultDto` en `BandejaAprobarDto.cs`.

### Bandeja — exclusiones de ítems por segmento

- **TRABAJADOR**: `item_id NOT IN (11, 12, 13)` — agrega ítem 12 (Inducción Obra) a la exclusión preexistente de 11 y 13.
- **EMPRESA**: `AND he.item_id NOT IN (15, 16)` — nuevo filtro; antes no tenía ninguna exclusión.

### SharePointHabService — multi-sitio

Todos los archivos de habilitación (trabajadores, empresas, equipos, sctr) están en el sitio **SSOMAApps**, no en el sitio Habilitacion.

- `ResolverSiteId(contexto)` — nuevo método; retorna siempre `SharePoint:Sites:SSOMAApps:SiteId`.
- `ResolverLibraryId`: caso `"sctr"` → `SharePoint:Sites:SSOMAApps:SctrLibraryId`.
- `GetDownloadUrlAsync` y `SubirArchivoAsync` usan `ResolverSiteId` en lugar del siteId hardcodeado.

Config en `appsettings.Local.json` (gitignored):
```json
"SSOMAApps": {
  "SiteId": "abrilinmob.sharepoint.com,d9e26806-...,a7b7032f-...",
  "SctrLibraryId": "78ae8a4b-4d48-46f8-a3f9-0abf12277198"
}
```
El `SctrLibraryId` fue movido de `Habilitacion` a `SSOMAApps`.

### Fix duplicados en GET /habilitacion/trabajadores

**Causa:** `WorkerProyecto` declara `public Worker? Worker { get; set; }` con `[ForeignKey]`. EF Core infería automáticamente la relación inversa `Worker HasMany WorkerProyecto`, generando un JOIN implícito a `ss_hab_worker_proyecto` en el query de listado — produciendo N filas por worker con N proyectos.

**Fix en `AppDbContext.OnModelCreating`:**
```csharp
modelBuilder.Entity<WorkerProyecto>()
    .HasOne(wp => wp.Worker)
    .WithMany()
    .HasForeignKey(wp => wp.WorkerId);
```
`WithMany()` sin parámetro suprime la colección inversa, eliminando el JOIN implícito.

### Pendientes de código (debug logs temporales)

- `HabTrabajadorRepository.GetWorkersHabilitacionAsync`: `Console.WriteLine("[DEBUG SQL] " + baseQuery.ToQueryString())` — quitar tras diagnóstico.
- `SctrVidaLeyRepository.AprobarAsync`: log `[DEBUG AprobarAsync]` al inicio — quitar antes de merge.
- `SctrVidaLeyRepository.GetTrabajadoresPorEmpresaAsync`: varios `LogInformation("[GetTrabajadoresPorEmpresa]...")` y `LogInformation("[DEBUG] estadoVidaLey...")` — quitar antes de merge.

---

## Sesión 2026-05-25 — SctrVidaLeyRepository BuildDtosAsync y diagnóstico SharePoint SCTR

### SctrWorkerDto — nuevos campos

`Features/HabilitacionModule/Application/Dtos/SctrVidaley/SctrWorkerDto.cs`:
- `public string Estado { get; set; } = "Falta"` — estado textual del entregable SCTR/VidaLey del worker
- `public DateTime? FechaVencimiento { get; set; }` — vigencia desde `ss_hab_trabajador.vigencia`

### SctrVidaLeyRepository.BuildDtosAsync — refactor completo

**Problema 1 — itemTipo match:** `e.Tipo == "VIDA_LEY"` nunca matcheaba con el nombre BD "Vida Ley". Fix:
```csharp
var itemTipo = sctrItem.FirstOrDefault(i =>
    e.Tipo == "VIDA_LEY" ? i.Nombre.Contains("Vida") : i.Nombre.Contains("SCTR"));
```
Mismo patrón ya aplicado en `CreateAsync`, `UpdateAsync`, `AprobarAsync`.

**Problema 2 — hab fuera de scope:** `hab` estaba declarado dentro del `if (itemTipo is not null)` pero sus campos se usaban en el return fuera. Fix: elevar `estadoWorker` y `fechaVencimiento` antes del bloque:
```csharp
var aprobado = false;
var estadoWorker = "Falta";
int? sctrHabId = null;
DateTime? fechaVencimiento = null;
if (itemTipo is not null)
{
    var hab = habs.FirstOrDefault(h => h.WorkerId == w.WorkerId && h.ItemId == itemTipo.Id);
    if (hab is not null)
    {
        aprobado = hab.Estado == "Aprobado";
        estadoWorker = hab.Estado ?? "Falta";
        sctrHabId = hab.Id;
        fechaVencimiento = hab.Vigencia;
    }
}
return new SctrWorkerDto { ..., Estado = estadoWorker, FechaVencimiento = fechaVencimiento };
```

**Problema 3 — `static` impide acceder a `_sharePoint`:** `BuildDtosAsync` era `private static`. Removido `static`.

**Problema 4 — `Select` síncrono con `await` dentro:** Cambiado a `Select(async e => {...})` + `Task.WhenAll`:
```csharp
var tasks = entities.Select(async e => { ... });
return (await Task.WhenAll(tasks)).ToList();
```

**Resolución URLs:** `_sharePoint.GetDownloadUrlAsync` inyectado en el método; `ISharePointHabService` añadido al constructor. Por cada póliza, antes del return:
```csharp
if (!string.IsNullOrEmpty(e.ArchivoUrl))
{
    try { archivoUrl = await _sharePoint.GetDownloadUrlAsync(e.ArchivoUrl); }
    catch (Exception ex) { _logger.LogError(ex, "Error resolviendo URL: {Path}", e.ArchivoUrl); archivoUrl = null; }
}
```
Idem para `ArchivoUrl2`.

### Diagnóstico SharePoint SCTR — 404 en /content

Logs observados al abrir una póliza SCTR:
- OAuth2 token: 200 ✅
- Drive `SCTRVidaLey2026` resuelto (200): `b!Bmji2TXVU0OWEBlZeOIDkC8Dt6ceUVNLiodQihkLPHxLiq54SE34RqP5Cr8SJ3GY` ✅
- `/drives/{driveId}/root:/habilitacion/sctr/20260525_VIDA_LEY_...pdf:/content` → **NotFound (404)** ⚠️
- No se lanza excepción — `GetDownloadUrlAsync` retorna `null` internamente al recibir 404

**Root cause probable:** los archivos en `SCTRVidaLey2026` no están en el subdirectorio `habilitacion/sctr/` — posiblemente en raíz de la biblioteca o en otra ruta. Verificar en `abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/SCTRVidaLey2026`.

**Pendiente:** confirmar ruta real de archivos SCTR en SharePoint. Si están en raíz, la columna `archivo_url` de `ss_sctr_vidaley` debería guardar solo el nombre de archivo sin prefijo `habilitacion/sctr/`. O bien, agregar lógica de strip/normalización en `NormalizarPath` de `SharePointHabService`.

---

## Sesión 2026-05-25 (continuación 2) — SCTR: auto-aprobación Abril, enriquecimiento GetTrabajadoresPorEmpresa

### SctrVidaLeyRepository.BuildDtosAsync — Include Person + log diagnóstico

- Join de workers usa `ctx.Worker.Include(x => x.Person)` (explícito, aunque EF ya hacía LEFT JOIN por la proyección)
- `LogWarning` tras cargar `workersData`: emite hasta 3 avisos cuando un worker tiene `ApellidoNombre == null` (diagnóstico de datos huérfanos)

### SctrVidaLeyRepository.CreateAsync — auto-aprobación para empresa Abril

Cuando `esAbril == true && entity.ProyectoId.HasValue`, después del upsert de workers en `ss_hab_trabajador`:

1. **Upsert `ss_hab_empresa`**: item 15 si `Tipo == "SCTR"`, item 16 si `Tipo == "VIDA_LEY"`. Lookup por `(EmpresaId, ProyectoId, ItemId)`. Si no existe: crea con `Mes/Anio` de la póliza. Si existe: actualiza `Estado` y `Vigencia`.
2. **`entity.Estado = "Aprobado"`** — persiste en el mismo `SaveChangesAsync`.

Los workers ya se aprobaban vía `estadoHab = esAbril ? "Aprobado" : "Enviado"` — el nuevo bloque completa la aprobación a nivel empresa y póliza.

### SctrTrabajadorEstadoDto — tres campos nuevos

`Features/HabilitacionModule/Application/Dtos/SctrVidaley/SctrTrabajadorEstadoDto.cs`:
- `public string? EmpresaNombre { get; set; }` — nombre del contributor activo del worker
- `public string? ProyectoNombre { get; set; }` — descripción del proyecto activo del worker
- `public DateTime? FechaVencimiento { get; set; }` — vigencia de `ss_hab_trabajador` (item SCTR primero, VidaLey como fallback)

### SctrVidaLeyRepository.GetTrabajadoresPorEmpresaAsync — foreach async por vinculación

El `workers.Select(w => {...}).ToList()` sícrono reemplazado por `foreach` async. Por cada worker:
1. Query `WorkerVinculacion WHERE worker_id = w.Id AND fecha_fin IS NULL ORDER BY id DESC` → vinculación activa
2. Si tiene `EmpresaId`: query `Contributor.ContributorName` → `EmpresaNombre`
3. Si tiene `ProyectoId`: query `Project.ProjectDescription` → `ProyectoNombre`
4. `FechaVencimiento` desde `habs` en memoria (`??=` — SCTR primero, VidaLey como fallback)

Patrón `foreach` (en lugar de `Select + Task.WhenAll`) porque EF Core no permite queries concurrentes en el mismo `DbContext`. Los 3 queries por worker corren secuencialmente sobre el mismo `ctx`.

---

## Sesión 2026-05-26 — Fix cruce SCTR/VidaLey + enriquecimiento EMO dashboard

### SctrVidaLeyRepository — fix bug cruce SCTR ↔ Vida Ley

**Root cause:** el lookup de `SsItemTrabajador` usaba `i.Nombre.Contains(itemNombreBuscar)` como filtro EF Core (traducido a `LIKE` case-sensitive en PG) sin filtrar por `Activo`. `GetTrabajadoresPorEmpresaAsync` sí filtraba por `Activo` — divergencia que podía hacer que Create/Update escribiera a un ítem distinto del que leía Get.

**Fixes aplicados en CreateAsync, UpdateAsync, AprobarAsync:**
```csharp
// Antes (EF Core, sin Activo, case-sensitive):
var item = await ctx.SsItemTrabajador
    .Where(i => i.EsSctrVidaley && i.Nombre.Contains(itemNombreBuscar))
    .FirstOrDefaultAsync();

// Después (cliente, con Activo, OrdinalIgnoreCase):
var sctrItems = await ctx.SsItemTrabajador
    .Where(i => i.EsSctrVidaley && i.Activo)
    .ToListAsync();
var item = sctrItems.FirstOrDefault(i =>
    i.Nombre.Contains(itemNombreBuscar, StringComparison.OrdinalIgnoreCase));
```

**Fix en `GetTrabajadoresPorEmpresaAsync`:** `itemSctr` ya usaba `ToListAsync()` con `Activo`, pero `Contains("SCTR")` era case-sensitive. Uniformizado a `OrdinalIgnoreCase` junto con `itemVidaLey`.

**Logs debug eliminados:**
- `HabTrabajadorRepository`: `Console.WriteLine("[DEBUG SQL] " + baseQuery.ToQueryString())`
- `SctrVidaLeyRepository.AprobarAsync`: `LogInformation("[DEBUG AprobarAsync] ...")`
- `SctrVidaLeyRepository.GetTrabajadoresPorEmpresaAsync`: 10 líneas de `LogInformation("[GetTrabajadoresPorEmpresa]...")` y `LogInformation("[DEBUG] estadoVidaLey...")`

### EmoRepository.ListPorTrabajador — enriquecimiento y filtros

**Filtro EsAbril (empresa de vinculación):**
```csharp
q = q.Where(x => x.em != null && x.em.EsAbril);
```
`em` proviene del JOIN `Contributor on vv.EmpresaId` (vinculación activa), no de `EmpresaOrigenId`.

**Nuevos JOINs en el query principal:**
- `join eop in ctx.Contributor on ue.EmpresaOrigenId equals eop.ContributorId` → `EmpresaOrigenNombre` (empresa que emitió el EMO)
- `join proy in ctx.Project on (vv != null ? vv.ProyectoId : -1) equals proy.ProjectId` → `ProyectoNombre`. Guardado `vv != null ?` para evitar match incorrecto cuando `vv` es null de `DefaultIfEmpty`.

**Nuevos campos en `EmoPorTrabajadorDto`:** `EmpresaOrigenNombre`, `ProyectoNombre`, `ObraOficina` (directo de `x.w.ObraOficina`).

**Búsqueda case-insensitive:** `Contains(term)` → `EF.Functions.ILike(field, $"%{term}%")` para `FullName` y `DocumentIdentityCode`. Funciona nativamente en PG sin `ToUpper`.

### CatalogosRepository.ListEmpresas — filtro EsAbril hardcodeado

Endpoint `GET /ssoma/catalogos/empresas` (dropdown de empresas en vista EMOs):
```csharp
// Antes:
var q = ctx.Contributor.Where(e => e.State).AsQueryable();
// Después:
var q = ctx.Contributor.Where(e => e.State && e.EsAbril).AsQueryable();
```

### DashboardRepository.GetDashboard — workerIdsAbril para todos los conteos

Todos los conteos del dashboard ahora se restringen a workers con vinculación activa a una empresa `EsAbril = true`:

```csharp
var workerIdsAbril = await ctx.WorkerVinculacion
    .Where(v => v.FechaFin == null)
    .Join(ctx.Contributor.Where(c => c.EsAbril),
          v => v.EmpresaId, c => c.ContributorId, (v, c) => v.WorkerId)
    .Distinct()
    .ToListAsync();
```

Queries filtradas: `totalTrabajadores`, `totalAbril`, `totalContratistas`, `emosActivos` (propaga a `ultimosEmos`, `aptitud`, `emosVencidos`, `vencer`, `proximos`), `interconsultasPendientes` (`i.WorkerId`), `trabajadoresInhabilitados`.

`programacionesSemana` (`SsProgramacionEmo`) no filtrado — es agenda de clínica, sin relación directa a `WorkerId`.
