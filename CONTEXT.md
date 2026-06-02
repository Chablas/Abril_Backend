# CONTEXT.md — Abril Backend

> Última actualización: 2026-06-02 — Pull master: MejoraContinuaModule refactorizado (LessonsLearned + LessonsDashboard migrados desde UnidadDeProyectosModule, LessonReminderFeature nueva), LessonController raíz eliminado, DTOs Lesson movidos a Features, IEmailGroupResolver añadido a Shared/Services/Graph.

---

## 1. Stack

| Capa              | Tecnología                                                                                                     |
| ----------------- | -------------------------------------------------------------------------------------------------------------- |
| Framework         | ASP.NET Core (.NET 10)                                                                                         |
| ORM               | EF Core + `UseSnakeCaseNamingConvention()` (PG)                                                                |
| BD principal      | **PostgreSQL en Aiven** (cloud)                                                                                |
| BD alternativa    | SQL Server (dev local, selector `Database:DatabaseProvider`)                                                   |
| Auth              | JWT Bearer interno (`Jwt:Key`) + Azure AD (Microsoft Entra) — ambos coexisten, política default acepta los dos |
| Email             | PowerAutomate / SendGrid / SMTP (selector `Email:EmailProvider`)                                               |
| Storage           | Azure Blob / local `wwwroot/uploads` (selector `Storage:StorageProvider`)                                      |
| Queries complejas | **Dapper** + conexión directa (`NpgsqlConnection` en `BandejaRepository`; `ctx.Database.GetDbConnection()` en `EvEvaluacionResidenteRepository`) |
| Fechas UTC        | `HabilitacionDateHelper` — `AsUtc()` y `ResolverVigencia()`                                                    |
| Puerto dev        | 5236 http / 7298 https                                                                                         |
| Swagger           | Solo en Development en `/swagger`                                                                              |

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
| `MejoraContinuaModule` | `AddMejoraContinuaModule` | LessonsLearned, LessonsDashboard, LessonReminder, AreasYSubareas, PsssTemplate, Relations |
| `UnidadDeProyectosModule` | `AddUnidadDeProyectosModule` | ProjectsDashboard (LessonsLearnedDashboard migrado a MejoraContinua) |
| `EvaluacionesModule` | `AddEvaluacionesModule` | Evaluaciones de residentes — periodos, plantilla, evaluaciones, dashboard [nuevo 2026-05-31] |

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

| Entidad C#                  | Tabla PG                       | PK                    | Notas                                                                                                                                                                                                                                                                                                                                           |
| --------------------------- | ------------------------------ | --------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Project`                   | `project`                      | `project_id`          | Entidad legacy ÚNICA para proyectos. Props: `ProjectId`, `ProjectDescription`. `Shared/Models/Project.cs`. **Siempre `ctx.Project` con `ProjectId`**.                                                                                                                                                                                           |
| `Contributor`               | `contributor`                  | `contributor_id`      | Entidad unificada de empresas. Reemplazó `companies` (eliminada) y `ss_empresa_contratista` (eliminada 2026-05-23). Incluye `EsAbril` (bool), `IdSharepoint` (int?, temporal), `ContributorNombreComercial` (varchar 255), `SpPasswordTemp` (varchar 255, usado para migración masiva). En `Features/CostsModule/Shared/Models/Contributor.cs`. |
| `Worker`                    | `workers`                      | `id`                  | Personal con columnas explícitas `[Column("...")]`. No snake_case automático. Tiene `PersonId int?` (FK→`person`) y `ContributorId int?` (FK→`contributor`) con nav properties `Person?` y `Contributor?` (agregadas 2026-05-11). `EmpresaId` NO existe en el modelo — siempre leer de `WorkerVinculacion`.                                     |
| `WorkerVinculacion`         | `worker_vinculaciones`         | `id`                  | 1 activa por worker (`fecha_fin IS NULL`). Para empresa y proyecto actual del worker.                                                                                                                                                                                                                                                           |
| `WorkerProyecto`            | `ss_hab_worker_proyecto`       | `id`                  | Multi-proyecto **solo Casa**. N activos en paralelo. Unique partial index `(worker_id, proyecto_id) WHERE fecha_fin IS NULL`.                                                                                                                                                                                                                   |
| `SsInduccion`               | `ss_induccion`                 | `id`                  | `empresa_id` → `contributor.contributor_id` (no `ss_empresa_contratista`). Columnas manuales: `ingreso_confirmado` (bool NOT NULL DEFAULT false), `fecha_ingreso` (timestamptz).                                                                                                                                                                |
| `SsTareo`                   | `ss_tareo`                     | `id`                  | Tabla manual (sin migración EF). `proyecto_id` → `project.project_id`. `fecha` (DateOnly). `observaciones` (text?). `creado_por` (int?, FK→app_user). Unique implícito en (`proyecto_id`, `fecha`).                                                                                                                                             |
| `SsTareoPartida`            | `ss_tareo_partida`             | `id`                  | Catálogo fijo de 17 partidas Casa. Columnas: `nombre`, `orden` (int), `activo` (bool). Tabla manual (sin migración EF).                                                                                                                                                                                                                         |
| `SsTareoDetalleCasa`        | `ss_tareo_detalle_casa`        | `id`                  | Detalle de tareo para personal Casa. `tareo_id` → `ss_tareo.id`, `partida_id` → `ss_tareo_partida.id`, `cantidad_personas` (int). Tabla manual.                                                                                                                                                                                                 |
| `SsTareoDetalleContratista` | `ss_tareo_detalle_contratista` | `id`                  | Detalle de tareo para personal contratista. `tareo_id` → `ss_tareo.id`, `empresa_id` → `contributor.contributor_id`, `cantidad_personas` (int). Tabla manual.                                                                                                                                                                                   |
| `SsHabTrabajador`           | `ss_hab_trabajador`            | `id`                  | Entregables por worker.                                                                                                                                                                                                                                                                                                                         |
| `SsHabEmpresa`              | `ss_hab_empresa`               | `id`                  | `proyecto_id` → `project.project_id`. `empresa_id` → `contributor.contributor_id`.                                                                                                                                                                                                                                                              |
| `SsEquipo`                  | `ss_equipo`                    | `id`                  | `proyecto_id` → `project.project_id`. `propietario_empresa_id` → `contributor.contributor_id` (nav property `Contributor? PropietarioEmpresa`).                                                                                                                                                                                                 |
| `SsHabEquipo`               | `ss_hab_equipo`                | `id`                  | Entregables por equipo. Tiene `ObsContratista` (agregada directamente en BD). `archivo_url` es `text` (fue `varchar(1000)` — alterada manualmente).                                                                                                                                                                                             |
| `SsItemTrabajador`          | `ss_item_trabajador`           | `id`                  | Catálogo de entregables con reglas.                                                                                                                                                                                                                                                                                                             |
| `WorkerEvento`              | `worker_eventos`               | `id`                  | Creada manualmente en BD (sin migración EF).                                                                                                                                                                                                                                                                                                    |
| `CatSubarea`                | `cat_subarea`                  | `id`                  | Creada manualmente en BD (sin migración EF).                                                                                                                                                                                                                                                                                                    |
| `SsTrabajadorRestringido`   | `ss_trabajador_restringido`    | `id`                  | Blacklist de trabajadores. `Dni varchar(15)`, `WorkerId int?`, `Activo bool`. UNIQUE(dni). SQL en `Database/migrations/ss_trabajador_restringido.sql`.                                                                                                                                                                                          |
| `CatCategoria`              | `cat_categoria`                | `id`                  | Catálogo de categorías de workers. `Nombre`, `Orden`, `Activo`. DbSet registrado — crear tabla manualmente en BD.                                                                                                                                                                                                                               |
| `CatOcupacion`              | `cat_ocupacion`                | `id`                  | Catálogo de ocupaciones de workers. `Nombre`, `Orden`, `Activo`. DbSet registrado — crear tabla manualmente en BD.                                                                                                                                                                                                                              |
| `User`                      | `app_user`                     | —                     | Override en `ConfigurePostgreSQL` (`User` es palabra reservada PG).                                                                                                                                                                                                                                                                             |
| `ContractorEmail`           | `contractor_email`             | `contractor_email_id` | Email por contratista. Tiene `UserId int?` (FK→`app_user`) para vincular con cuenta del sistema. La FK `fk_contractor_email_user_user_id` se agrega con la migración `MigrateResetTokenToUserId`.                                                                                                                                               |
| `SsResetToken`              | `ss_reset_token`               | —                     | Token de reset/activación. `UserId int?` (FK→`app_user`). **`EmpresaId` eliminado** (migración `RemoveSsEmpresaContratista`).                                                                                                                                                                                                                   |

> **⚠️ `projects` (plural) NO EXISTE** — fue eliminada vía migración `SwitchProyectoFkToProjectLegacy`. Todo `proyecto_id` de cualquier tabla apunta a `project.project_id` legacy. Resolver siempre con `ctx.Project.Where(p => p.ProjectId == id)`.

---

## 5. HabilitacionModule — detalle completo

**Ubicación:** `Features/HabilitacionModule/`

**DI adicional:** BCrypt.Net-Next, FluentValidation, Dapper. `ISharePointHabService` registrado como **Singleton** (cachea token OAuth2 y driveId).

### 5a. Catálogo ss_item_trabajador

Items clave por ID:

| id  | nombre                         | aplica_a | requiere_vigencia | notas                                                     |
| --- | ------------------------------ | -------- | ----------------- | --------------------------------------------------------- |
| 1   | DNI                            | TODOS    | true              |                                                           |
| 4   | Certificado de Aptitud (EMO)   | TODOS    | true              | EMO Contratista en ss_hab_trabajador; Casa en worker_emos |
| 5   | Registro de Entrega de EPP     | CASA     | false             | sentinel 2040                                             |
| 6   | Entrega RISST                  | CASA     | false             | sentinel 2040                                             |
| 8   | Entrega de Recomendaciones SST | CASA     | false             | sentinel 2040                                             |
| 10  | Difusion de PTS                | CASA     | false             | sentinel 2040                                             |
| 11  | SCTR                           | TODOS    | true              | excluido de bandeja (NOT IN)                              |
| 12  | Induccion Obra                 | TODOS    | false             | sentinel 2040; reset al cambiar proyecto                  |
| 13  | Vida ley                       | TODOS    | true              | excluido de bandeja (NOT IN)                              |
| 25  | Lectura de EMO                 | CASA     | true              | incluido en itemsEmoIds → excluido cálculo bloqueo Casa   |

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
"ContractorCredentialsUrl": "https://abril-frontend-m21l.onrender.com/auth/contractor-credentials"
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

### 7v. SctrVidaLeyRepository — optimizaciones (2026-05-29)

`GetTrabajadoresPorEmpresaAsync`:
- Filtros `estadoSctr`/`estadoVidaLey` movidos a BD mediante LEFT JOIN EF (`GroupJoin + DefaultIfEmpty`) con COALESCE equivalente (`habX != null ? habX.Estado : "Falta"`). Ya no se filtran en memoria.
- N+1 de vinculación/empresa/proyecto eliminado: una sola query bulk por cada entidad, resueltas con diccionarios en memoria.
- `empresaId == null` omite el filtro de empresa (devuelve todos los workers vinculados activos); el filtro de estado reduce el resultado.

`AprobarAsync`: tras calcular `nuevoEstado`, si no quedan workers con `Estado == "Enviado"` en la póliza, se fuerza `nuevoEstado = "Aprobado"` (evita dejar la póliza en "Parcial" cuando todos ya fueron procesados).

### 7w. CatalogosRepository — ListEmpresas sin filtro EsAbril (2026-05-29)

`ListEmpresas` en SsomaModule ahora filtra solo por `e.State` (eliminado `&& e.EsAbril`). Devuelve tanto empresas Abril como contratistas.

### 7s. SsProgramacionEmo — campo Notificado (2026-05-29)

Propiedad `Notificado bool` agregada al modelo con `[Column("notificado")]`. **Pendiente migración EF** (`dotnet ef migrations add AddNotificadoProgramacionEmo`) para crear la columna en BD.

### 7t. ProgramacionEmoRepository — ApproverResolver inyectado (2026-05-29)

`IApproverResolver` inyectado en el constructor. En `EnviarNotificacionAceptacionAsync`, el bloque Oficina Central ya no consulta `CatJefatura` por string match — usa `_approverResolver.ResolveApproverEmailAsync(worker)` que sigue la cascada `Jefe → Sub Gerente → Gerente` por `Area`/`Subarea`/`Categoria` en `workers`.

### 7u. EmoAutoProgramacionService — excluye Completado (2026-05-29)

`programacionesExistentes` ahora filtra `p.Estado != "Completado"` además de `!= "Cancelado"` y `!= "Rechazado por Clínica"`. Antes, un worker con EMO completado quedaba bloqueado de recibir nueva programación automática.

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

| role_id | descripción                                                   |
| ------- | ------------------------------------------------------------- |
| 1       | ADMINISTRADOR DEL SISTEMA                                     |
| 2       | ADMINISTRADOR DE UDP                                          |
| 3       | USUARIO DE UDP                                                |
| 4       | ADMINISTRADOR DE RESIDENTES                                   |
| 5       | RESIDENTE                                                     |
| 6       | USUARIO DE COSTOS Y PRESUPUESTOS                              |
| 7       | ADMINISTRADOR DE COSTOS Y PRESUPUESTOS                        |
| 8       | USUARIO DE ARQUITECTURA COMERCIAL                             |
| 9       | ADMINISTRADOR SSOMA                                           |
| 10      | ADMINISTRADOR ADMINISTRACION                                  |
| —       | GESTOR DE ARQUITECTURA COMERCIAL _(pendiente insertar en BD)_ |

Roles aprobadores habilitación: `["ADMINISTRADOR SSOMA", "ADMINISTRADOR DE UDP", "ADMINISTRADOR ADMINISTRACION"]`

---

## 9. ArquitecturaComercial — detalle

**Ubicación:** capa tradicional — `Controllers/ArquitecturaComercialController.cs`, `Application/Services/ArquitecturaComercialService.cs`, `Infrastructure/Repositories/ArquitecturaComercialRepository.cs`.

### 9a. Tablas propias (prefijo `ac_`)

| Tabla                      | Entidad                | Rol                                                         |
| -------------------------- | ---------------------- | ----------------------------------------------------------- |
| `ac_actividades`           | `AcActividad`          | Actividad asignada a un proyecto                            |
| `ac_etapas`                | `AcEtapa`              | Catálogo de etapas                                          |
| `ac_actividades_plantilla` | `AcActividadPlantilla` | Plantilla para inicializar actividades de un proyecto nuevo |
| `ac_categorias`            | `AcCategoria`          | Catálogo de categorías                                      |
| `ac_especialidades`        | `AcEspecialidad`       | Catálogo de especialidades                                  |

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

| DTO                    | Uso                                                                                                                                                                                                                                    |
| ---------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `AcActividadCreateDTO` | POST actividades — Nombre, Tipo, ProjectId, EtapaId?, UserId?, UserId2?, CategoriaId?, EspecialidadId?, InicioProgramado?, FinProgramado?, Observaciones?                                                                              |
| `AcActividadUpdateDTO` | PUT actividades/{id} — mismo shape sin ProjectId, más InicioEfectivo/FinEfectivo, UserId2?, CategoriaId?, EspecialidadId?                                                                                                              |
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

`VerificarNoActivoEnOtraEmpresaAsync` (privado en `WorkerSearchRepository` y `HabTrabajadorRepository`): consulta `worker_vinculaciones WHERE fecha_fin IS NULL`, lanza 400 si `EmpresaId != empresaIdNueva`. Mensaje: _"El trabajador ya se encuentra activo en otra empresa. Debe ser retirado antes de poder registrarlo en una nueva empresa."_

`ValidarExclusividadEmpresaAsync` (privado en `HabTrabajadorRepository`): mismo check pero lanza 409 y escribe registro en `ss_hab_bloqueo_log`. Usado solo en `CambiarObraAsync`.

**Pendiente en código:**

- Quitar `Console.WriteLine` de debug en `ControlAccesoRepository.GetConsultaAsync` (líneas ~51-54)
- Quitar `Console.WriteLine` de debug en `ControlAccesoRepository.GetInduccionesHoyAsync` (3 líneas DEBUG agregadas temporalmente)
- Quitar `[AllowAnonymous]` de `GET /inducciones-hoy` en `ControlAccesoController` cuando se confirme fix de fechas

---

## 11. Módulos nuevos 2026-05 — resumen de arquitectura

### 11a. AuthModule (`Features/AuthModule/`)

Consolida toda la autenticación. Reemplaza y amplía el anterior `MicrosoftAuthModule`.

| Feature                      | Responsabilidad                                               |
| ---------------------------- | ------------------------------------------------------------- |
| MicrosoftLoginFeature        | Login con Microsoft Entra, emite JWT interno                  |
| MicrosoftProfileFeature      | Perfil Microsoft Graph (HttpClient)                           |
| ContractorCredentialsFeature | Credenciales JWT para contratistas (tabla `contractor_users`) |
| RoleFeature                  | CRUD roles + asignación de funcionalidades a roles            |
| UserFeature                  | Gestión de usuarios del sistema                               |

Migración: `20260505173114_AddContractorUserCredentials` (tabla `contractor_users`).

---

### 11b. ConfigurationModule (`Features/ConfigurationModule/`)

`ProjectFeature` — CRUD completo de proyectos AC (`Proyecto` en español). Controlador: `ProjectController`.

---

### 11c. GestionAdministrativaModule (`Features/GestionAdministrativaModule/`)

Prefijo de entidades: `Ga*` (`GaLugar`, `GaMotivoSalida`, `GaHoraOpcion`, `GaSolicitudSalida`).

| Feature                 | Responsabilidad                    |
| ----------------------- | ---------------------------------- |
| SolicitudSalidasFeature | Solicitudes de salida del personal |
| GestionSalidasFeature   | Aprobación y gestión de salidas    |
| LugaresFeature          | Catálogo de lugares                |
| MotivosSalidaFeature    | Catálogo de motivos de salida      |

---

### 11d. MejoraContinuaModule (`Features/MejoraContinuaModule/`)

| Feature               | Responsabilidad                                                   |
| --------------------- | ----------------------------------------------------------------- |
| LessonsLearnedFeature | Lecciones aprendidas — CRUD, filtros paginados, exportación Excel |
| AreasYSubareasFeature | CRUD áreas, subáreas y scopes PSSS                                |
| PsssTemplateFeature   | Plantillas PSSS (relación área/subárea → partidas)                |
| RelationsFeature      | Relaciones área/subárea para lecciones (2026-05-14)               |

Modelos compartidos: `Partida`, `PsssScope`, `PsssTemplate`, `PsssTemplateDetail`, `SubArea` en `MejoraContinuaModule/Shared/Models/`.

---

### 11e. UnidadDeProyectosModule (`Features/UnidadDeProyectosModule/`)

`LessonsLearnedDashboard` — dashboard consolidado de lecciones entre proyectos.

`ProjectsDashboard` — dashboard ejecutivo de proyectos ArquitecturaComercial.

#### ProjectsDashboard — endpoints

```
GET  /api/v1/projects-dashboard/filters
     → ProjectsDashboardFiltersResponseDto { Projects, Estados, ResponsablesArqCom }

GET  /api/v1/projects-dashboard?proyectoId=&estado=&responsableArqComId=&fechaDesde=&fechaHasta=
     → ProjectsDashboardResponseDto:
       - KPIs: TotalProyectos, AlDia, ConRetraso, SinActividades, PorcentajeAvancePromedio
       - Proyectos[]: ProjectId, ProjectDescription, Estado, ResponsableArqCom,
                      TotalActividades, Culminadas, EnProceso, Vencidas, PorcentajeAvance,
                      EstaConRetraso, DiasRetraso, Semaforo, EtapaNombre
       - DistribucionPorEstado[]: { Estado, CantidadProyectos }
       - RankingResponsables[]: { ResponsableId, ResponsableNombre, TotalProyectos,
                                  ActividadesCompletadas, ActividadesVencidas,
                                  TotalActividades, Score }  — ordenado Score DESC
       - HeatmapCarga[]: { ResponsableId, ResponsableNombre, Semana ("yyyy-Www"),
                           CantidadActividades }

GET  /api/v1/projects-dashboard/{proyectoId}
     → ProyectoDetailDashboardDto:
       - Kpis: TotalActividades, Culminadas, EnProceso, Vencidas, AvancePct, DiasRetraso, Semaforo
       - ActividadesVencidas[]: { Id, Nombre, Tipo, ResponsableNombre, FinProgramado, DiasRetraso }
       - Gantt[]: { Id, Nombre, InicioProgramado, FinProgramado, FinEfectivo, Estado, ResponsableNombre }
```

#### ProjectsDashboard — reglas de negocio

- **Semáforo**: calculado como `MAX(hoy - FinProgramado)` sobre actividades donde `FinProgramado < hoy AND FinEfectivo == null`. Verde = 0 días, Amarillo = 1-7 días, Rojo = > 7 días.
- **EtapaNombre**: etapa de la actividad activa con mayor `Id` que tenga `EtapaId` (más recientemente creada con etapa asignada).
- **Score ranking**: `MAX(0, completadas/total*100 - vencidas*5)`. Basado en `AcActividad.UserId` (responsable por actividad). `TotalProyectos` = proyectos distintos donde tiene actividades.
- **Heatmap**: agrupa por `AcActividad.UserId` y semana ISO de `FinProgramado`. Si no se pasa `fechaDesde/fechaHasta`, usa próximas 12 semanas desde hoy.
- **Filtro de proyectos**: `project.state = true` (sin filtro por `tiene_arquitectura_comercial` — flag no activo en BD al 2026-05-25).
- **Ranking y heatmap aplican los mismos filtros** que el dashboard principal (proyectoId, estado, responsableArqComId) ejecutando queries paralelas con `IDbContextFactory`.

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

| Controlador                 | Ruta                                | Descripción                                |
| --------------------------- | ----------------------------------- | ------------------------------------------ | --------------- | ------------------------------ |
| `ClinicaUsuariosController` | `/catalogos/clinicas/{id}/usuarios` | CRUD usuarios por clínica — ver sección 12 |
| `EmoAlertaController`       | `/alertas/procesar                  | auto-programar                             | resumen-diario` | Triggers manuales de cron jobs |
| `ReporteController`         | `/reportes/sunafil-mensual`         | Excel SUNAFIL mensual (ClosedXML)          |

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

### 11i. EvaluacionesModule (`Features/EvaluacionesModule/`) — nuevo 2026-05-31

Namespace: `Abril_Backend.Features.Evaluaciones.*`. Estructura `Application/Infrastructure/Presentation` sin sub-features. Interfaces de repo en `Application/Interfaces/` (no en `Infrastructure/Interfaces/`).

**Modelos** (`Infrastructure/Models/`):
| Entidad | Tabla | Notas |
|---|---|---|
| `EvPeriodo` | `ev_periodo` | Mes, Año, FechaApertura, FechaCierre, Activo |
| `EvPlantilla` | `ev_plantilla` | AreaNombre, Criterio, Orden, Activo, Version |
| `EvEvaluacionResidente` | `ev_evaluacion_residente` | `EvaluadorUserId int?`, `EvaluadorPersonId int?`, `EvaluadoUserId int`, FK→EvPeriodo, FK→Project |
| `EvEvaluacionResidenteDetalle` | `ev_evaluacion_residente_detalle` | FK→EvEvaluacionResidente, FK→EvPlantilla, Puntaje, EsNa |
| `EvNoAplica` | `ev_no_aplica` | FK→EvPeriodo |
| `EvRecordatorioLog` | `ev_recordatorio_log` | Sin navegaciones |

**DbSets en AppDbContext:** EvPeriodos, EvPlantillas, EvEvaluacionesResidente, EvEvaluacionesResidenteDetalle, EvNoAplica, EvRecordatorioLogs

**Endpoints:**
```
GET/POST         /api/v1/evaluaciones/periodos
PUT              /api/v1/evaluaciones/periodos/{id}/activar|desactivar
GET              /api/v1/evaluaciones/plantilla
GET              /api/v1/evaluaciones/plantilla/areas
GET              /api/v1/evaluaciones/plantilla/{area}
POST             /api/v1/evaluaciones/plantilla
PUT              /api/v1/evaluaciones/plantilla/{id}
POST             /api/v1/evaluaciones/residentes           ← crea evaluación (valida periodo activo, duplicados)
GET              /api/v1/evaluaciones/residentes/periodo/{periodoId}
GET              /api/v1/evaluaciones/residentes/mis-evaluaciones
GET              /api/v1/evaluaciones/residentes/mi-perfil
GET              /api/v1/evaluaciones/residentes/mi-subarea          ← Dapper → { subarea }
GET              /api/v1/evaluaciones/residentes/residentes-evaluables  ← Dapper, 2 pasos
GET              /api/v1/evaluaciones/residentes/{id}
GET              /api/v1/evaluaciones/dashboard/gerencia
GET              /api/v1/evaluaciones/dashboard/residentes
GET              /api/v1/evaluaciones/dashboard/areas
GET              /api/v1/evaluaciones/dashboard/tendencia   ← sin parámetro año (todos los períodos)
GET              /api/v1/evaluaciones/dashboard/pendientes
GET              /api/v1/evaluaciones/recordatorios/enviar    ← CronSecret, envía recordatorios del periodo activo
GET              /api/v1/evaluaciones/recordatorios/descargo  ← CronSecret, envía descargos tras cierre de periodo
```

**Lógica clave:**
- Nota = `promedio(puntajes donde EsNa=false) × 4` (escala 1-5 → 20)
- `EvaluacionesEsperadas = residentes.Count * 8`
- `GetResidentesResumenAsync` agrupa por `EvaluadoUserId`; ProjectId/Nombre = `g.First()`. Periodo anterior buscado por `(Anio, Mes)` calendario real (no por `Id`). Campo `Evaluaciones` poblado con evaluador, criterios y comentarios — usa `.Include(Detalles)` en el query inicial (evita N+1) + diccionario `evaluadores` separado del de `persons` (evaluados).
- `GetTendenciaAsync()` sin filtro año

**Cron de recordatorios (`EvRecordatorioService`):**
- `GET /recordatorios/enviar` — autenticado con `CronSecret` (sin `[Authorize]`). Día de apertura = PRIMER_AVISO a todos; días siguientes = RECORDATORIO_DIA_{n} solo a pendientes. CC al jefe mapeado desde `cat_jefatura` por subarea.
- `GET /recordatorios/descargo` — se dispara el día después del cierre (periodo cerrado ayer). Envía descargo a quien no evaluó nada, con CC al gerente de proyectos (`coriundo@abril.pe`) y jefe directo.
- `EvRecordatorioRepository.GetEvaluadoresPendientesAsync` — Dapper. Filtra `workers WHERE area='Proyectos' AND subarea NOT IN ('Residencia','Almacenero','Proyectos')`. Une con `cat_jefatura` por CASE de subarea. `soloSinEvaluar=true` agrega NOT EXISTS sobre `ev_evaluacion_residente`.
- `YaEnvioRecordatorioHoyAsync` — antiduplicado por `(periodoId, userId, tipo)` en ventana UTC del día.
- `EvRecordatorioLog` ahora es usado activamente (ya no solo tabla pasiva).
- `AddEvaluacionesModule` registra `IEvRecordatorioRepository` + `IEvRecordatorioService`.

**Dapper en `EvEvaluacionResidenteRepository`** — patrón:
```csharp
await ctx.Database.OpenConnectionAsync();
var conn = ctx.Database.GetDbConnection();
await conn.QueryAsync<T>(sql, params)
```
`GetResidentesEvaluablesAsync(evaluadorUserId)`:
- Paso 1: lee `obra_oficina` del evaluador (`workers → person WHERE user_id = @id LIMIT 1`)
- Paso 2: si `obra_oficina = 'Oficina Central'` → todos los residentes activos; si no → filtra por `project_id` del evaluador
- Join: `workers → person → app_user → project ON contributor_id = w.contributor_id`
- `ResidenteEvaluableDto`: UserId, NombreCompleto, ProjectId, ProjectNombre, Area, Subarea, PuedeVerTodos

---

### 11h. HabilitacionModule — controladores nuevos (2026-05-04 al 2026-05-18)

| Controlador                       | Ruta                | Descripción                                                                        |
| --------------------------------- | ------------------- | ---------------------------------------------------------------------------------- |
| `InduccionController`             | `/inducciones`      | Programar, listar, aprobar inducciones                                             |
| `ControlAccesoController`         | `/control-acceso`   | Consulta habilitación en tiempo real, tareo, inducciones del día                   |
| `TrabajadorRestringidoController` | `/restringidos`     | Blacklist trabajadores (roles: ADMINISTRADOR SSOMA / ADMINISTRADOR ADMINISTRACION) |
| `EmpresaContratistaController`    | `/empresas`         | CRUD empresas contratistas                                                         |
| `CatalogosHabilitacionController` | `/catalogos`        | Catálogos del módulo (items, áreas, subareas, categorías, ocupaciones)             |
| `RegistrosModeloController`       | `/registros-modelo` | Registros modelo (público)                                                         |

---

## 12. ClinicaUsuariosModule — detalle

**Tablas creadas manualmente en pgAdmin (sin migración EF):**

| Tabla                  | Columnas clave                                                                                                                                  |
| ---------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| `ss_clinica_usuarios`  | `clinica_usuario_id`, `clinica_id`, `nombre`, `email`, `password_hash`, `activo`, `creado_por int`, `modificado_por int`, `desactivado_por int` |
| `ss_clinica_tokens`    | `token_id`, `clinica_usuario_id`, `token`, `tipo`, `expiracion`, `usado_en`, `ip_solicitud`                                                     |
| `ss_clinica_auditoria` | `auditoria_id`, `clinica_usuario_id`, `clinica_id`, `accion`, `ip_origen`, `detalle_adicional jsonb`                                            |

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
- GET  /programaciones/habilitacion?estado=&proyectoId=&fecha=&soloNoNotificados=  ← nuevo 2026-05-29
- PATCH /programaciones/{id}/notificado  body: { notificado: bool }  ← nuevo 2026-05-29
- GET /alertas/auto-programar (CronSecret)
- GET /alertas/resumen-diario (CronSecret)
- POST /auth/login
- POST /auth/solicitar-activacion
- POST /auth/activar
- GET /reportes/sunafil-mensual?mes=&anio=
- GET /catalogos/clinicas/{id}/emails
- POST /catalogos/clinicas/{id}/emails body: { email, nombre? }
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

| Migration ID                               | Descripción                                                                                   |
| ------------------------------------------ | --------------------------------------------------------------------------------------------- |
| `20260518193906_AddWorkerMissingColumns`   | ~26 columnas worker, tablas nuevas, FKs — Up() reescrito como SQL idempotente                 |
| `20260518220129_MigrateResetTokenToUserId` | `user_id` en `ss_reset_token` y `contractor_email`; FKs; `empresa_id` nullable en reset_token |
| `20260518223250_AddContractorEmailUserId`  | Migración vacía — columna ya añadida por la anterior vía SQL                                  |

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

| Archivo                      | Método                         |
| ---------------------------- | ------------------------------ |
| `HabTrabajadorRepository.cs` | `UpdateEntregableAsync`        |
| `HabEmpresaRepository.cs`    | `UpdateEntregableEmpresaAsync` |
| `EquipoRepository.cs`        | `UpdateEntregableEquipoAsync`  |

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

| Fix                                                                                                                                             | Archivo                           | Commit        |
| ----------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------- | ------------- |
| `EmpresaContratistaRepository.GetProyectosAsync`: resolución IdLegacy en dos pasos (por `IdLegacy`, luego por RUC)                              | `EmpresaContratistaRepository.cs` | tercera parte |
| `GetWorkersHabilitacionAsync`: dos subqueries `LatestVincActiva` (FechaFin IS NULL) y `LatestVincCualquiera` (sin filtro) según `soloRetirados` | `HabTrabajadorRepository.cs`      | tercera parte |
| `ReingresoAsync`: eliminado guard `if (esCambioProyecto \|\| esCambioEmpresa)` — siempre crea vinculación nueva al reingresar                   | `HabTrabajadorRepository.cs`      | tercera parte |
| `ReingresoAsync`: recupera empresa/proyecto de última vinculación cerrada cuando `vinculActual == null`                                         | `HabTrabajadorRepository.cs`      | `23f2b7f`     |
| `GetTrabajadoresPorEmpresaAsync`: quitado check `Contains("ABRIL")`                                                                             | `SctrVidaLeyRepository.cs`        | `23f2b7f`     |
| `SctrVidaLeyController.GetPaged`: inyecta `empresaId` del JWT para CONTRATISTA                                                                  | `SctrVidaLeyController.cs`        | `4a8363d`     |
| `GetPagedAsync`: `HasPendientes = true` cuando no hay entregables                                                                               | `EquipoRepository.cs`             | `c225e14`     |
| `worker_vinculaciones` id=7672 `fecha_fin` → NULL (dato corrupto)                                                                               | pgAdmin manual                    | —             |

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

| Parámetro     | Tipo   | Uso                                                                           |
| ------------- | ------ | ----------------------------------------------------------------------------- |
| `userId`      | `int?` | Id del usuario autenticado (de `ClaimTypes.NameIdentifier`)                   |
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

| Archivo                                                            | Contenido                                                                                                                              |
| ------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------- |
| `Application/DTOs/ArquitecturaComercial/DashboardFiltroDTO.cs`     | `CategoriaId?`, `ProyectoId?`, `UserId?`, `Semana?`, `Mes?`, `Anio?`                                                                   |
| `Application/DTOs/ArquitecturaComercial/ActividadAlertaDTO.cs`     | `Id`, `Nombre`, `Proyecto`, `Responsable1/2`, `EmailResp1/2`, `FechaInicio/Fin`, `Estado`, `Spi`, `Tipo`, `Categoria`, `DiasRestantes` |
| `Application/DTOs/ArquitecturaComercial/EnviarAlertaRequestDTO.cs` | `List<int> ActividadIds`, `string TipoAlerta`                                                                                          |
| `Application/DTOs/ArquitecturaComercial/TareasPorArquitectoDTO.cs` | `TareasPorArquitectoDTO`, `AvanceSemanalDTO`, `EficienciaSpiDTO`, `CategoriaItemDTO`                                                   |

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

| Concepto             | Lógica                                                                     |
| -------------------- | -------------------------------------------------------------------------- |
| Culminada            | `FinEfectivo != null`                                                      |
| En proceso           | `InicioEfectivo != null && FinEfectivo == null`                            |
| Vencida              | `FinEfectivo == null && FinProgramado < today`                             |
| Pendiente            | `InicioEfectivo == null && InicioProgramado > today`                       |
| Vence esta semana    | `FinEfectivo == null && FinProgramado ∈ [semLunes, semDomingo]`            |
| Arranca esta semana  | `InicioEfectivo == null && InicioProgramado ∈ [semLunes, semDomingo]`      |
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

ss*clinica*\*, catálogos SSOMA, Phase/Stage/Layer, AcPlantillas, ac_categorias,
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

| Migration                                      | Descripción                                                                                                                                   |
| ---------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| `20260522182631_AddContributorMigracionFields` | `sp_password_temp` + `contributor_nombre_comercial` en contributor; tablas `ac_avance_semanal`, `costos_presupuestos_email`; columnas GA + AC |
| `20260523002524_RemoveSsEmpresaContratista`    | Drop `ss_empresa_contratista` (CASCADE); migra empresa_id vía id_legacy; elimina empresa_id de ss_reset_token; agrega FKs a contributor       |

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

| contexto                          | LibraryId usado         | Biblioteca SharePoint          |
| --------------------------------- | ----------------------- | ------------------------------ |
| `"habilitacion/trabajadores/..."` | `TrabajadoresLibraryId` | Biblioteca Trabajadores        |
| `"habilitacion/empresas/..."`     | `EmpresaLibraryId`      | Biblioteca Empresas            |
| `"habilitacion/equipos/..."`      | `EquiposLibraryId`      | Biblioteca Equipos             |
| cualquier otro                    | `null`                  | Drive predeterminado del sitio |

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

| Archivo                                                          | Descripción                                              |
| ---------------------------------------------------------------- | -------------------------------------------------------- |
| `Infrastructure/Models/SsContratistaRol.cs`                      | Entidad `[Table("ss_contratista_rol")]`                  |
| `Infrastructure/Models/SsContratistaUsuario.cs`                  | Entidad con `RolId` (interno) + `SystemRoleId` (FK→role) |
| `Infrastructure/Models/SsContratistaUsuarioProyecto.cs`          | Relación usuario↔proyecto                                |
| `Application/Dtos/ContratistaUsuarios/ContratistaUsuarioDtos.cs` | `ContratistaUsuarioListDto`, `CreateDto`, `UpdateDto`    |
| `Infrastructure/Interfaces/IContratistaUsuarioRepository.cs`     | Interfaz repositorio                                     |
| `Application/Interfaces/IContratistaUsuarioService.cs`           | Interfaz servicio                                        |
| `Infrastructure/Repositories/ContratistaUsuarioRepository.cs`    | Implementación repositorio                               |
| `Application/Services/ContratistaUsuarioService.cs`              | Implementación servicio                                  |
| `Presentation/ContratistaUsuarioController.cs`                   | Controller `api/v1/contratista-usuarios`                 |

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

| role_id                       | descripción            |
| ----------------------------- | ---------------------- |
| 11                            | CONTRATISTA            |
| 49                            | SERVICIO DE VIGILANCIA |
| (ver sección 8 para ids 1–10) | —                      |

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

---

## Sesión 2026-05-26 (continuación) — EMO: InterconsultaInline, FechaLectura, bloque create expandido

### WorkerEmo — nueva propiedad FechaLectura

`Infrastructure/Models/WorkerEmo.cs`:

```csharp
[Column("fecha_lectura")]
public DateOnly? FechaLectura { get; set; }
```

Insertada junto a `FechaVencimiento`. **Pendiente migración EF** (`dotnet ef migrations add AddFechaLecturaWorkerEmo`) para crear la columna `fecha_lectura` en BD.

### EmoCreateDto — dos nuevos campos

`Features/SsomaModule/SaludOcupacionalFeature/Application/Dtos/Emo/EmoCreateDto.cs`:

```csharp
public DateOnly? FechaLectura { get; set; }
public InterconsultaInlineDto? InterconsultaInline { get; set; }
```

### EmoInterconsultaInlineDto — archivo nuevo

`Features/SsomaModule/SaludOcupacionalFeature/Application/Dtos/Emo/EmoInterconsultaInlineDto.cs`:

```csharp
public class InterconsultaInlineDto
{
    public string Especialidad { get; set; } = string.Empty;
    public string? CentroAtencion { get; set; }
    public string? Diagnostico { get; set; }
    public string? Cie10 { get; set; }
    public int? MedicoDerivaId { get; set; }
    public bool RequiereSeguimiento { get; set; }
}
```

### EmoRepository.Create — bloque interconsulta expandido

**Antes:** solo disparaba cuando `Aptitud == "Observado" && RequiereInterconsulta`.

**Ahora:** dispara también cuando `InterconsultaInline != null` o `Aptitud == "No Apto"`. Usa los campos de `InterconsultaInline` con fallback a los valores originales:

```csharp
if (dto.InterconsultaInline != null ||
    (dto.Aptitud == "Observado" && dto.RequiereInterconsulta) ||
    dto.Aptitud == "No Apto")
{
    var ic = dto.InterconsultaInline;
    ctx.SsInterconsulta.Add(new SsInterconsulta
    {
        Especialidad = ic?.Especialidad ?? "Por definir",
        MedicoDerivaId = ic?.MedicoDerivaId ?? dto.MedicoId,
        CentroAtencion = ic?.CentroAtencion,
        Diagnostico = ic?.Diagnostico,
        Cie10 = ic?.Cie10,
        RequiereSeguimiento = ic?.RequiereSeguimiento ?? false,
        // resto igual ...
    });
}
```

`CentroAtencion`, `Diagnostico`, `Cie10` ya existen en `SsInterconsulta` — no requieren migración.

### EmoRepository.Create — FechaLectura en object initializer

```csharp
NumeroInforme = dto.NumeroInforme,
FechaLectura = dto.FechaLectura,   // ← nuevo
UrlResultado = dto.UrlResultado,
```

Depende de que se ejecute la migración de `WorkerEmo.FechaLectura` antes de correr en producción.

---

## Sesión 2026-05-26 (tarde) — ProgramacionEmoRepository: filtro EsAbril y refactor notificaciones

### ProgramacionEmoRepository.List — filtro EsAbril

`Features/SsomaModule/SaludOcupacionalFeature/Infrastructure/Repositories/ProgramacionEmoRepository.cs`

El query ya tenía JOIN con `Contributor` (`em`). Se agrega filtro fijo antes de los filtros opcionales:

```csharp
q = q.Where(x => x.em != null && x.em.EsAbril);
```

Mismo patrón que `EmoRepository.ListPorTrabajador`, `DashboardRepository` y `CatalogosRepository`.

### EnviarNotificacionCreacionAsync — simplificado (solo clínica)

Reemplazado por versión reducida:

- Guarda si no hay `ClinicaId`. No distingue tipo de worker.
- `To` = `ss_clinica_emails` (fallback `ss_clinicas.email`). Sin CC.
- Subject: `[EMO Programado] {nombre} — {fecha}`.
- Body: tabla HTML compacta con trabajador, tipo, fecha, hora, proyecto, clínica.
- `BuildBodyCreacion` (método estático) eliminado — quedó huérfano.

### EnviarNotificacionAceptacionAsync — nuevo método

Se dispara cuando la clínica acepta (`Accion == "Aceptar"`). Notifica al equipo interno según tipo de worker:

| Tipo                               | To                                                                                       |
| ---------------------------------- | ---------------------------------------------------------------------------------------- |
| Obrero (Casa, ObraOficina=Ninguno) | EmailAdministrador + EmailResidente + EmailSsoma del proyecto + MedicinaOcupacional      |
| Staff (Casa, ObraOficina=Staff)    | EmailCorporativo + EmailResidente + EmailAdministrador + EmailSsoma del proyecto         |
| Oficina Central                    | EmailCorporativo + GTH + MedicinaOcupacional + cat_jefatura emails del `worker.Jefatura` |
| Contratista (!esCasa)              | sin notificación — return inmediato                                                      |

Subject: `[EMO Confirmado] {nombre} — {fecha}`.

### ClinicaAccion — carga worker + llama EnviarNotificacionAceptacionAsync

```csharp
var worker = await ctx.Worker.Include(w => w.Person)
    .FirstOrDefaultAsync(w => w.Id == ent.WorkerId)
    ?? throw new AbrilException("Trabajador no encontrado.", 404);

case "Aceptar":
    ent.Estado = "Aceptado por Clínica";
    ent.MotivoRechazo = null;
    await EnviarNotificacionAceptacionAsync(ctx, ent, worker);
    break;
```

### Fix: campos incorrectos en EnviarNotificacionAceptacionAsync

`p.EmailAdministrador` y `p.EmailSsoma` no existen en `Shared/Models/Project.cs`. Corregidos:

- `p.EmailAdministrador` → `p.EmailCoordAdmin`
- `p.EmailSsoma` → `p.EmailCoordSsoma`

Campos correctos de `Project.cs`: `EmailResidente` (31), `EmailResponsable` (32), `EmailRrhh` (33), `EmailCoordSsoma` (34), `EmailCoordAdmin` (35).

### Contributor.EmailAdministrador — nueva propiedad

`Features/CostsModule/Shared/Models/Contributor.cs`:

```csharp
[Column("email_administrador")]
public string? EmailAdministrador { get; set; }
```

### Migraciones aplicadas — 2026-05-26

| Migration                                         | Descripción                                                                                                                                    | Aplicada |
| ------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------- | -------- |
| `20260526162047_AddEmailAdministradorContributor` | SQL idempotente: ADD COLUMN email*administrador en contributor + fecha_lectura en worker_emos + tablas ga*_ + ss*contratista*_ (IF NOT EXISTS) | ✅       |
| `20260526162657_SyncSnapshot`                     | Migración vacía — sincroniza snapshot EF sin cambios en BD                                                                                     | ✅       |

Ambas aplicadas con `dotnet ef database update --project Abril-Backend.csproj`.

---

## Sesión 2026-05-26 (continuación 2) — Notificaciones EMO, WorkerHabilitacionListDto, validaciones

### EnviarNotificacionAceptacionAsync — adminEmail desde contributor del worker

Carga `contributor.email_administrador` por `worker.ContributorId` y lo agrega a `toRaw` en los tres bloques (esObrero, esStaff, esOficinaCentral):

```csharp
var adminEmail = worker.ContributorId.HasValue
    ? await ctx.Contributor.AsNoTracking()
        .Where(c => c.ContributorId == worker.ContributorId.Value)
        .Select(c => c.EmailAdministrador)
        .FirstOrDefaultAsync()
    : null;
// ...
toRaw.Add(adminEmail); // en cada bloque
```

### Prefijo [PRUEBAS - NO RESPONDER] en subjects EMO

Ambos métodos de notificación actualizados:

- `EnviarNotificacionCreacionAsync`: `"[PRUEBAS - NO RESPONDER] [EMO Programado] {nombre} — {fecha}"`
- `EnviarNotificacionAceptacionAsync`: `"[PRUEBAS - NO RESPONDER] [EMO Confirmado] {nombre} — {fecha}"`

**Quitar antes de producción.**

### WorkerHabilitacionListDto — TieneEmo y DiasRestantesEmo

`Features/HabilitacionModule/Application/Dtos/Trabajadores/WorkerHabilitacionListDto.cs`:

```csharp
public bool TieneEmo { get; set; }
public int? DiasRestantesEmo { get; set; }
```

`HabTrabajadorRepository.GetWorkersHabilitacionAsync` — batch post-query (mismo patrón que `empresaMap`/`proyectoMap`):

```csharp
var emoMap = await ctx.WorkerEmo
    .Where(e => workerIds.Contains(e.WorkerId) && e.Activo
             && (e.Estado == "Vigente" || e.Estado == "Convalidado"))
    .GroupBy(e => e.WorkerId)
    .Select(g => new { WorkerId = g.Key,
        FechaVencimiento = g.OrderByDescending(e => e.FechaVencimiento)
                            .Select(e => e.FechaVencimiento).FirstOrDefault() })
    .ToDictionaryAsync(x => x.WorkerId, x => x.FechaVencimiento);

// En el mapper:
TieneEmo = emoMap.ContainsKey(r.Worker.Id),
DiasRestantesEmo = emoVenc.HasValue ? (int?)(emoVenc.Value.DayNumber - today.DayNumber) : null
```

`FechaVencimiento` en `WorkerEmo` es `DateOnly?` — días calculados con `DayNumber` (sin conversión de zona horaria).

---

## Sesión 2026-05-26 (continuación 3) — EMO: EsAbril, TipoEmoId nullable, upload documentos, notificaciones

### EmoCreateDto.TipoEmoId → int?

`Features/SsomaModule/.../Application/Dtos/Emo/EmoCreateDto.cs`: `int TipoEmoId` → `int? TipoEmoId`. Evita que el frontend envíe `0` cuando no hay tipo seleccionado (antes deserializaba como 0 y rompía silenciosamente).

`EmoService.ValidarComun`: firma actualizada a `int? tipoEmoId`, validación cambiada a `!tipoEmoId.HasValue || tipoEmoId.Value <= 0`.

### EmoAutoProgramacionService — filtro EsAbril + usar IProgramacionEmoRepository

**Filtro EsAbril** en query de candidatos — nuevo join y condición:

```csharp
join contrib in ctx.Contributor on v.EmpresaId equals contrib.ContributorId
where ... && contrib.EsAbril
```

**Refactor bloque inserción** — reemplaza inserción directa `ctx.SsProgramacionEmo.Add` + `SaveChangesAsync` por:

```csharp
await _progRepo.Create(new ProgramacionCreateDto
{
    WorkerId        = c.Emo.WorkerId,
    EmpresaId       = c.Vinculacion.EmpresaId,
    TipoEmoId       = tipoEmoId,
    FechaProgramada = fechaProg,
    Origen          = "Automatico",
    Motivo          = "Programación automática por vencimiento de EMO",
}, userId: null);
```

Así el cron reutiliza el mismo flujo que una programación manual, incluyendo el envío de correo a la clínica.

Constructor actualizado — `IProgramacionEmoRepository progRepo` inyectado. `IProgramacionEmoRepository` ya estaba registrado en `SsomaModule.cs`.

### ProgramacionEmoRepository.Create — validación FechaProgramada

```csharp
if (dto.FechaProgramada == default)
    throw new AbrilException("La fecha es obligatoria.", 400);
```

Insertado después de cargar el worker. Evita guardar `0001-01-01` cuando el cliente omite el campo.

### ClinicaAccion — actualizar HoraProgramada al aceptar

En `case "Aceptar"`: si la clínica envía `CheckInHora`, se actualiza `HoraProgramada` antes de llamar a `EnviarNotificacionAceptacionAsync` (así el email refleja la hora real):

```csharp
if (dto.CheckInHora.HasValue) ent.HoraProgramada = dto.CheckInHora.Value;
```

`horaStr` en ambos métodos de notificación ya usaba `prog.HoraProgramada` — sin cambio adicional.

### Upload documentos EMO a SharePoint

`POST api/v1/ssoma/salud-ocupacional/emos/{emoId}/documentos` — `[FromForm] IFormFile file, [FromForm] string tipo` (`Aptitud` | `EMO`).

- `EmoController` inyecta `ISharePointHabService` + `IDbContextFactory<AppDbContext>`
- Construye `{DNI}_{tipo}_{yyyyMMdd}.pdf`, contexto `emo-aptitud` o `emo-completo`
- Guarda path en `WorkerEmo.UrlAptitud` o `WorkerEmo.UrlEmoCompleto`

`SharePointHabService.ResolverLibraryId`: nuevos casos `emo-aptitud` → `AptitudesLibraryId`, `emo-completo` → `EMOSLibraryId` (ambos bajo `SharePoint:Sites:SSOMAApps`).

`WorkerEmo`: `UrlAptitud` y `UrlEmoCompleto` (text, nullable). Migración `AddUrlDocumentosWorkerEmo` aplicada.

### Contributor.EmailAdministrador

`Features/CostsModule/Shared/Models/Contributor.cs`: `[Column("email_administrador")] public string? EmailAdministrador { get; set; }`. Migración `AddEmailAdministradorContributor` aplicada.

---

## Sesión 2026-05-26 (continuación 4) — ProgramacionEmo: correo resumen, notificación aceptación, validaciones

### EmoAutoProgramacionService — correo resumen en lugar de por-worker

Reemplaza inserción vía `_progRepo.Create` (que enviaba un correo por cada programación) por inserción directa `ctx.SsProgramacionEmo.Add` + `SaveChangesAsync`. Al final del loop llama a `EnviarResumenClinicaAsync` — un único correo HTML a `ClinicaId=1` con tabla de todos los trabajadores programados.

`IProgramacionEmoRepository` eliminado del constructor; en su lugar `IEmailService` inyectado. `ClinicaId = 1` hardcoded en la entidad.

### ProgramacionEmoRepository — EnviarNotificacionCreacionAsync: CC eliminado

Eliminados `medOcupacional`, `gth`, `emoResumenRaw`, `ccSiempre`, `ccRaw`, y el bloque `var cc`. El método ahora solo envía a `to` (clínica), sin CC. Catch mejorado a `LogError` con `Provider`, `To`, y `Error`.

`var toRaw` movido fuera del `try` para accesibilidad en el `catch`.

### ProgramacionEmoRepository — ClinicaAccion case "Aceptar"

`case "Aceptar"` hace su propio `SaveChangesAsync` + `return` antes de llegar al `SaveChangesAsync` compartido del final. Flujo:

```csharp
case "Aceptar":
    ent.Estado = "Aceptado por Clínica";
    ent.MotivoRechazo = null;
    if (dto.CheckInHora.HasValue) ent.HoraProgramada = dto.CheckInHora.Value;
    ent.UpdatedAt = DateTimeOffset.UtcNow;
    await ctx.SaveChangesAsync();
    await EnviarNotificacionAceptacionAsync(ctx, ent, worker);
    return;
```

### ProgramacionEmoRepository — EnviarNotificacionAceptacionAsync (nuevo método)

Notifica equipo interno cuando la clínica acepta. Routing por tipo de worker:

| Tipo           | To                                                                                                 |
| -------------- | -------------------------------------------------------------------------------------------------- |
| Obrero         | EmailCoordAdmin + EmailResidente + EmailCoordSsoma del proyecto + MedicinaOcupacional + adminEmail |
| Staff          | EmailCorporativo + EmailResidente + EmailCoordAdmin + EmailCoordSsoma + adminEmail                 |
| OficinaCentral | EmailCorporativo + GTH + MedicinaOcupacional + adminEmail + CatJefatura emails                     |
| Contratista    | return inmediato                                                                                   |

`adminEmail` cargado desde `contributor.email_administrador` vía `worker.ContributorId`.

Subject: `"[PRUEBAS - NO RESPONDER] [EMO Confirmado] {nombre} — {fecha}"`.

### ProgramacionListDto — TipoEmoId agregado

`ProgramacionListDto.cs`: nueva propiedad `public int? TipoEmoId { get; set; }`.
`ProgramacionEmoRepository.List` Select: `TipoEmoId = x.p.TipoEmoId` agregado.

### EmoController — endpoint SubirDocumento

`POST api/v1/ssoma/salud-ocupacional/emos/{emoId}/documentos` — ya documentado en continuación 3. `ISharePointHabService` y `IDbContextFactory` inyectados en el constructor.

`WorkerEmo.UrlAptitud` y `UrlEmoCompleto` agregados en `Infrastructure/Models/WorkerEmo.cs`.

### SharePointHabService.ResolverLibraryId — casos EMO

```csharp
if (c.Contains("emo-aptitud"))  return _configuration["SharePoint:Sites:SSOMAApps:AptitudesLibraryId"];
if (c.Contains("emo-completo")) return _configuration["SharePoint:Sites:SSOMAApps:EMOSLibraryId"];
```

### Vinculación Habilitación ↔ WorkerEmo

| Tipo worker | ItemId            | Mecanismo                                                                                             |
| ----------- | ----------------- | ----------------------------------------------------------------------------------------------------- |
| Contratista | `CertAptitud = 4` | Automático: `EmoRepository.SincronizarEntregableEmoAsync` escribe en `ss_hab_trabajador` al crear EMO |
| Casa        | `LecturaEmo = 25` | En tiempo real: no hay fila en `ss_hab_trabajador`, estado calculado desde `WorkerEmo` activo         |

Casa: `EstadoCalc = "No Autorizado"` si no hay `WorkerEmo` con `Activo && (Estado == "Vigente" || "Convalidado")`.
`SincronizarEntregableEmoAsync` solo se llama en `EmoRepository.Create`, **no en Update**.

### Migración pendiente

`WorkerEmo.UrlAptitud` + `UrlEmoCompleto` → migración `AddUrlDocumentosWorkerEmo` pendiente de crear y aplicar (columnas no existen aún en BD).

`Contributor.EmailAdministrador` → migración `AddEmailAdministradorContributor` pendiente de crear y aplicar (columna `email_administrador` no existe aún en BD).

---

## Sesión 2026-05-26 (continuación 5) — ClinicaAuth: investigación flujo activación, App:FrontendUrl

### ClinicaAuthController — flujo completo

**Ruta base:** `api/v1/ssoma/salud-ocupacional/auth` — `[AllowAnonymous]` a nivel de clase.

| Endpoint                          | Body                  | Comportamiento                                                                                                                                                                                      |
| --------------------------------- | --------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `POST /auth/login`                | `{ email, password }` | BCrypt.Verify contra `ss_clinica_usuarios.password_hash`; emite JWT con claims `clinicaUsuarioId`, `clinicaId`, role `"CLINICA"`, expiry 8h                                                         |
| `POST /auth/solicitar-activacion` | `{ email }`           | **Email debe existir ya** en `ss_clinica_usuarios` (busca el usuario activo); genera token de activación en `ss_clinica_tokens`; envía email con link `{App:FrontendUrl}/clinica/activar?token=...` |
| `POST /auth/activar`              | `{ token, password }` | Valida token en `ss_clinica_tokens`; hace hash de la nueva contraseña; activa el usuario                                                                                                            |

`solicitar-activacion` no crea usuarios nuevos — requiere que el admin Abril haya creado el usuario previamente vía `ClinicaUsuariosController`.

### ClinicaUsuariosController — POST responde 409 en email duplicado

`POST /catalogos/clinicas/{clinicaId}/usuarios` — `ClinicaUsuarioService.CreateUsuarioAsync` lanza `AbrilException("Ya existe un usuario con ese correo.", 409)` si el email ya existe en `ss_clinica_usuarios`. El controller captura la excepción y retorna `StatusCode(409, { message })`.

### App:FrontendUrl — añadido a appsettings.Local.json

`ClinicaAuthController.SolicitarActivacion` lee `_configuration["App:FrontendUrl"]` para construir el link de activación. Faltaba en `appsettings.Local.json`; añadido:

```json
"App": {
  "FrontendUrl": "https://abril-frontend-m21l.onrender.com"
}
```

(gitignored — no commitear)

### WorkerSearchRepository — campos de Person en Create y Update

**Create** (línea ~114): solo asigna `FullName = dto.ApellidoNombre`. `FirstNames` y `FirstLastName` quedan `null` en BD.

**Update** (línea ~180): igual — solo actualiza `person.FullName = dto.ApellidoNombre`. `FirstNames` y `FirstLastName` nunca se tocan.

`full_name` se asigna directo desde `dto.ApellidoNombre` sin concatenar `first_names + first_last_name`.

---

## Sesión 2026-05-26 — ProjectsDashboard: migración BD, feature en BD, renombrado ArqCom

### 1. Migración `AddFechaRealFinToMilestoneSchedule`

La entidad `MilestoneSchedule` (`Infrastructure/Models/MilestoneSchedule.cs`) ya tenía la propiedad `DateOnly? FechaRealFin` pero el snapshot EF no la conocía (no existía migración). La columna sí existía en Aiven (creada manualmente en sesión anterior).

Pasos ejecutados:

1. `dotnet ef migrations add AddFechaRealFinToMilestoneSchedule` — generó migración con `AddColumn<DateOnly>`.
2. Primer `dotnet ef database update` falló con `42701: ya existe la columna «fecha_real_fin»`.
3. El `Up()` fue reemplazado con `migrationBuilder.Sql("ALTER TABLE milestone_schedule ADD COLUMN IF NOT EXISTS fecha_real_fin date;")` para hacerlo idempotente.
4. Segundo `dotnet ef database update` → `Done.` — migración registrada en `__EFMigrationsHistory`.

Archivo: `Migrations/20260526130525_AddFechaRealFinToMilestoneSchedule.cs`

> **Patrón a seguir:** cuando la BD está por delante de EF (columna ya aplicada manualmente), modificar el `Up()` generado para usar SQL idempotente (`IF NOT EXISTS`, `CREATE TABLE IF NOT EXISTS`, etc.) antes de aplicar.

---

### 2. ProjectsDashboard migrado a `milestone_schedule`

El dashboard ejecutivo de proyectos (`Features/UnidadDeProyectosModule/Features/ProjectsDashboard/`) usa `milestone_schedule` como fuente de actividades en lugar de `AcActividad`. Este cambio venía del commit `e658b5e` de la sesión anterior.

**Fuente de datos:**

- `MilestoneSchedule` (tabla `milestone_schedule`) — actividades del cronograma de proyecto.
- `MilestoneScheduleHistory` — historial de cronogramas; se toma el de mayor `MilestoneScheduleHistoryId` activo por proyecto.
- `FechaRealFin` (DateOnly?) — fecha real de término; `null` = no culminada. Usada para calcular completadas, vencidas, semáforo y Gantt.

**Campos calculados en runtime (no almacenados):**

- `Semaforo`: MAX(hoy - PlannedEndDate) sobre actividades vencidas. Verde=0d, Amarillo=1-7d, Rojo=>7d.
- `Estado` por actividad: `CULMINADO` (FechaRealFin != null) → `VENCIDO` (PlannedEndDate < hoy) → `EN_PROCESO` (PlannedStartDate <= hoy) → `PENDIENTE`.
- `Score` ranking: `MAX(0, completadas/total*100 - vencidas*5)`.

---

### 3. Feature `projects.projects-dashboard` registrada en BD

Ejecutado directamente contra Aiven (PostgreSQL):

```sql
-- Tabla real: "feature" (singular), columna "feature_key"
INSERT INTO feature (feature_key, module_id)
VALUES ('projects.projects-dashboard', 6)       -- módulo "Proyectos"
ON CONFLICT DO NOTHING;
-- → feature_id = 93

-- Asignada al rol USUARIO DE UDP (role_id = 3)
INSERT INTO role_feature (role_id, feature_id)
SELECT 3, feature_id FROM feature
WHERE feature_key = 'projects.projects-dashboard'
ON CONFLICT DO NOTHING;
```

Estado de tablas BD relevantes (confirmado en sesión):

- `module` (singular) — PK `module_id`, nombre en `module_name`. 11 módulos.
- `feature` (singular) — PK `feature_id`, clave en `feature_key`. 61 features al inicio + 1 nueva = 62.
- `role` (singular) — PK `role_id`, nombre en `role_description`.
- `role_feature` — PK compuesta `(role_id, feature_id)`.

---

### 4. Renombrado referencias "ArqCom" → nombres neutros (7 archivos)

Todas las referencias a `ArqCom` en la capa pública (DTOs, interfaces, servicios, controller) fueron renombradas para desacoplar el dashboard de proyectos del dominio de Arquitectura Comercial.

| Cambio                                                   | Antes                                       | Después                               |
| -------------------------------------------------------- | ------------------------------------------- | ------------------------------------- |
| Clase DTO                                                | `ResponsableArqComSimpleDto`                | `ResponsableSimpleDto`                |
| Propiedad respuesta filtros                              | `ResponsablesArqCom`                        | `Responsables`                        |
| Propiedad respuesta proyectos                            | `ResponsableArqCom`                         | `ResponsableNombre`                   |
| Query param HTTP                                         | `?responsableArqComId=`                     | `?responsableId=`                     |
| Parámetro de métodos (4 interfaces, 2 servicios, 1 repo) | `responsableArqComId`                       | `responsableId`                       |
| Clase privada `ProjectFlat` (repo interno)               | `ResponsableArqCom` / `ResponsableArqComId` | `ResponsableNombre` / `ResponsableId` |

**Archivos modificados:**

1. `Application/Dtos/ProjectsDashboardFiltersResponseDto.cs`
2. `Application/Dtos/ProjectsDashboardResponseDto.cs`
3. `Infrastructure/Interfaces/IProjectsDashboardRepository.cs`
4. `Application/Interfaces/IProjectsDashboardService.cs`
5. `Application/Services/ProjectsDashboardService.cs`
6. `Presentation/ProjectsDashboardController.cs`
7. `Infrastructure/Repositories/ProjectsDashboardRepository.cs`

> **Nota:** Las propiedades de la entidad `Project` (`Project.ResponsableArqComId`, `Project.ResponsableArqCom`) **no fueron renombradas** — son columnas en BD. El renombrado aplica solo a la capa de presentación y a la clase privada `ProjectFlat` del repositorio.

---

### 5. Endpoints ProjectsDashboard — estado actual

```
GET  /api/v1/projects-dashboard/filters
     → ProjectsDashboardFiltersResponseDto
       { Projects[], Estados[], Responsables[] }
          Responsables[]: { WorkerId, FullName }

GET  /api/v1/projects-dashboard
     ?proyectoId=&estado=&responsableId=&fechaDesde=&fechaHasta=
     → ProjectsDashboardResponseDto
       { TotalProyectos, AlDia, ConRetraso, SinActividades, PorcentajeAvancePromedio,
         Proyectos[]: { ProjectId, ProjectDescription, Estado, ResponsableNombre,
                        TotalActividades, Culminadas, EnProceso, Vencidas,
                        PorcentajeAvance, EstaConRetraso, DiasRetraso, Semaforo, EtapaNombre },
         DistribucionPorEstado[]: { Estado, CantidadProyectos },
         RankingResponsables[]: { ResponsableId, ResponsableNombre, TotalProyectos,
                                  ActividadesCompletadas, ActividadesVencidas,
                                  TotalActividades, Score },
         HeatmapCarga[]: { ResponsableId, ResponsableNombre, Semana, CantidadActividades } }

GET  /api/v1/projects-dashboard/{proyectoId}
     → ProyectoDetailDashboardDto
       { Kpis: { TotalActividades, Culminadas, EnProceso, Vencidas, AvancePct,
                 DiasRetraso, Semaforo },
         ActividadesVencidas[]: { Id, Nombre, Tipo, ResponsableNombre,
                                  FinProgramado, DiasRetraso },
         Gantt[]: { Id, Nombre, InicioProgramado, FinProgramado, FinEfectivo,
                    Estado, ResponsableNombre } }
```

Feature key en BD: `projects.projects-dashboard` (feature_id=93). Asignada a rol USUARIO DE UDP (role_id=3).

---

### 6. Pendientes frontend tras sesión

- Actualizar query param de `?responsableArqComId=` a `?responsableId=` en todas las llamadas al dashboard.
- Actualizar lectura de campo JSON `responsablesArqCom` → `responsables` en la respuesta de `/filters`.
- Actualizar lectura de campo JSON `responsableArqCom` → `responsableNombre` en la lista de proyectos.

### 7. Herramienta instalada

`dotnet-ef` v10.0.8 instalada como herramienta global (`dotnet tool install --global dotnet-ef`). Necesaria para `dotnet ef migrations *` y `dotnet ef database update`.

---

## Sesión 2026-05-26 (parte 2)

### 1. Modelo ProjectActivity (nueva tabla)

`Shared/Models/ProjectActivity.cs` — entidad nueva, completamente independiente de `milestone_schedule`.
`Shared/Data/AppContext.cs` — `DbSet<ProjectActivity> ProjectActivity` agregado. Override en `ConfigurePostgreSQL`:

- Tabla: `project_activity`
- `Order` → columna `project_activity_order` (evitar palabra reservada PostgreSQL)
- `ActivityDescription` IsRequired MaxLength(500), `ProgressPercentage` DefaultValue(0)

### 2. Campos agregados a Project

`Shared/Models/Project.cs`:

```
public bool TieneUnidadDeProyectos { get; set; }
public string? ResponsableUdp { get; set; }
public int? ResponsableUdpId { get; set; }
```

### 3. Migraciones aplicadas a Aiven

Patrón: `dotnet ef database update` siempre apunta a la BD local de Development. Para aplicar en Aiven hay que leer la cadena de `appsettings.Production.json` y ejecutar el SQL directamente con psql.

- `20260526203642_AddFechaRealFinAndTieneUnidadDeProyectos`: agrega `tiene_unidad_de_proyectos boolean NOT NULL DEFAULT false` a `project` y `fecha_real_fin date` nullable a `milestone_schedule`.
- `20260526215118_AddResponsableUdpToProject`: agrega `responsable_udp text` y `responsable_udp_id integer` nullable a `project`.
- `20260526223020_AddProjectActivityTable`: crea tabla `project_activity` con columna `project_activity_order` en lugar de `order`.

### 4. Proyectos UDP marcados en Aiven

13 proyectos con `tiene_unidad_de_proyectos = true` (project_ids: 6, 7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 18, 39). Responsables UDP asignados en `responsable_udp` / `responsable_udp_id` directamente en la tabla `project`.

### 5. Feature CronogramaActividades — reescritura completa

Reemplazó completamente el feature anterior (basado en `milestone_schedule` + `milestone_schedule_history`).
Ahora usa `project_activity` exclusivamente.

**Endpoints** (`api/v1/cronograma-actividades`):

- `GET /proyectos` — proyectos con `tiene_unidad_de_proyectos=true && state=true`
- `GET /{proyectoId}/actividades` — actividades activas del proyecto, orden por `Order`
- `POST /{proyectoId}/actividades` — crea actividad; Order = MAX(order)+1
- `PUT /actividades/{actividadId}` — edita actividad
- `PUT /actividades/{actividadId}/culminar` — toggle `ActualEndDate` (null↔hoy)
- `DELETE /actividades/{actividadId}` — soft-delete (`State=false, Active=false`)

**DTOs**: `ProyectoSimpleCronogramaDto`, `ActividadDto`, `CrearActividadRequest`, `EditarActividadRequest`, `CulminarActividadDto`.

### 6. Feature ProjectsDashboard — migración a project_activity

Repositorio reescrito para usar `ctx.ProjectActivity` en lugar de `milestone_schedule`. Todos los métodos filtran `p.TieneUnidadDeProyectos && p.State`.

**Cambios en KPIs**:

- `AvanceReal` = promedio de `ProgressPercentage` de actividades activas del proyecto
- `AvanceProgramado` = promedio de porcentaje de tiempo transcurrido por actividad (clamped 0–100)
- `Culminadas` = actividades con `ActualEndDate != null`
- `Vencidas` = `PlannedEndDate < today && ActualEndDate == null`
- `EstaConRetraso` = tiene vencidas O (AvanceReal < AvanceProgramado − 10)

**Responsables**: se leen de `ResponsableUdp`/`ResponsableUdpId` directo en `project` (sin JOIN a `worker`/`person`).

### 7. DTOs renombrados/reestructurados en ProjectsDashboard

| Antes                                                  | Después                                                    |
| ------------------------------------------------------ | ---------------------------------------------------------- |
| `PorcentajeAvancePromedio`                             | `AvancePromedio`                                           |
| `ProjectId` en ProyectoDetalleDto                      | `ProyectoId`                                               |
| `PorcentajeAvance`                                     | `AvanceProgramado` + `AvanceReal`                          |
| `ResponsableNombre`, `TotalProyectos`, etc. en ranking | `Nombre`, `Proyectos`, `Completadas`, `Vencidas`, `Score`  |
| `HeatmapCargaItemDto` (plana)                          | `HeatmapResponsableDto { Responsable, Semanas[] }` anidada |
| `ProyectoDetailKpisDto`                                | campos aplanados en `ProyectoDetailDashboardDto`           |
| `ActividadVencidaDto`                                  | `ActividadCriticaDto`                                      |
| `ActividadGanttDto`                                    | `GanttTareaDto` con `[JsonPropertyName]` para dhtmlx-gantt |

`ProyectoSimpleDto` y `ResponsableSimpleDto` son ahora locales al feature (no importan de `Application.DTOs`).

**GET /api/v1/projects-dashboard** — nueva forma del response:

```
{ TotalProyectos, AlDia, ConRetraso, SinActividades, AvancePromedio,
  Proyectos[]: { ProyectoId, ProjectDescription, Estado, ResponsableNombre,
                 TotalActividades, Culminadas, EnProceso, Vencidas,
                 AvanceProgramado, AvanceReal, EstaConRetraso, DiasRetraso, Semaforo, EtapaNombre },
  DistribucionPorEstado[]: { Estado, CantidadProyectos },
  RankingResponsables[]: { ResponsableId, Nombre, Proyectos, Completadas, Vencidas, Score },
  HeatmapCarga[]: { Responsable, Semanas[]: { Semana, Cantidad } } }
```

**GET /api/v1/projects-dashboard/{proyectoId}** — nueva forma:

```
{ ProyectoId, ProyectoNombre, Estado, AvanceProgramado, AvanceReal, DiasRetraso, Semaforo,
  ActividadesVencidas[]: { Id, Nombre, ResponsableNombre, FinProgramado, DiasRetraso },
  ActividadesCriticas[]: { Id, Nombre, ResponsableNombre, FinProgramado, DiasRetraso },
  Gantt: { Tasks[]: { id, text, start_date, duration, progress }, Links[] } }
```

`GanttTareaDto.StartDate` formateado como `"dd-MM-yyyy 00:00"` para dhtmlx-gantt. `Duration = Math.Max(1, end-start)`.

### 8. Pendientes frontend tras esta sesión

- Actualizar lectura de `responsablesArqCom` → `responsables` (ya corregido en sesión anterior, verificar).
- Adaptar componentes de Ranking y Heatmap a la nueva estructura de DTOs.
- `ProyectoDetalleDto.ProjectId` → `ProyectoId` (renombrado).

---

## Sesión 2026-05-27 — Fixes HabilitacionModule y ClinicaAuth

### 1. Fix: filtro Estado en GetInduccionesHoyAsync

`Features/HabilitacionModule/Infrastructure/Repositories/ControlAccesoRepository.cs`

El método `GetInduccionesHoyAsync` filtraba `Estado == "Programado"` (capitalización mixta), pero `InduccionRepository.CreateAsync` guarda `Estado = "PROGRAMADA"` (mayúsculas). El filtro nunca matcheaba registros recién creados.

Corregido a `Estado == "PROGRAMADA"` para consistencia con el valor que escribe el Create.

### 2. Fix: token Base64 con espacios en activación de cuenta clínica

`Features/SsomaModule/SaludOcupacionalFeature/Presentation/ClinicaAuthController.cs` — método `Activar`

El token de activación se genera con `RandomNumberGenerator.GetBytes(32)` codificado en Base64. Los caracteres `+` del Base64 llegan como espacios cuando el frontend no hace `decodeURIComponent` antes de enviar el token al POST.

Agregado antes del `FirstOrDefaultAsync`:

```csharp
dto.Token = dto.Token?.Replace(" ", "+");
```

Los logs de diagnóstico temporales (`Console.WriteLine`) fueron removidos en sesión 2026-05-27 parte 3.

### 3. Comportamiento documentado: EmoController — sin control de rol

`Features/SsomaModule/SaludOcupacionalFeature/Presentation/EmoController.cs`

Ningún endpoint del módulo EMO tiene `[Authorize(Roles = "...")]` ni comprueba el claim `tipo` (CLINICA / CONTRATISTA / admin). Cualquier JWT válido puede crear, editar o cambiar estado de cualquier EMO.

### 4. Comportamiento documentado: SctrVidaLeyController — auto-aprobación para empresa Abril

En `SctrVidaLeyRepository.CreateAsync`, si la empresa es Abril (`Contributor.EsAbril == true`):

- `SsHabTrabajador.Estado` se setea a `"Aprobado"` (en lugar de `"Enviado"`).
- Se hace upsert en `SsHabEmpresa` con `ItemId` hardcodeado: 15 = SCTR, 16 = VIDA_LEY, `Estado = "Aprobado"`.
- La propia póliza (`SsSctrVidaley.Estado`) se eleva a `"Aprobado"`.

### 5. Comportamiento documentado: ListPorTrabajador filtra solo empresas Abril

`EmoRepository.ListPorTrabajador` aplica filtro `em.EsAbril == true` sobre la vinculación vigente del worker. Trabajadores vinculados solo a empresas contratistas **no aparecen** en este endpoint.

---

## Sesión 2026-05-27 (parte 2) — Auth contratista y clínica

### 1. SystemRoleId 14 permitido en InvitarUsuarioAsync

`Features/HabilitacionModule/Application/Services/ContratistaUsuarioService.cs`

La validación de `SystemRoleId` en `InvitarUsuarioAsync` aceptaba solo `11` (CONTRATISTA) y `49` (SERVICIO DE VIGILANCIA). Se amplió para aceptar también `14`. La inserción en `user_role` ya usaba `dto.SystemRoleId` directamente, por lo que no requirió cambios adicionales.

Valores válidos actuales: `11`, `14`, `49`.

### 2. Logs de diagnóstico temporales en ActivarCuentaAsync — REMOVIDOS (2026-05-27 parte 3)

`Features/HabilitacionModule/Application/Services/ContratistaAuthService.cs`

Se habían agregado `Console.WriteLine` de diagnóstico (`[ACTIVAR] Paso 1..4`, `[ACTIVAR ERROR]`, `[ACTIVAR STACK]`) con un `try/catch` envolvente. Todo fue removido. El método `ActivarCuentaAsync` ahora no tiene ese try/catch wrapper — la lógica original quedó intacta sin indentación extra.

### 3. Arquitectura auth contratista — resumen

- Usuarios contratistas son filas en la tabla global `user` (email + BCrypt password).
- Vínculo empresa↔usuario: `contractor_email` (fuente de verdad para login) + `ss_contratista_usuario` (rol interno).
- Token de activación/reset: `ss_reset_token` — GUID 32 hex chars, TTL 48h activación / 2h reset.
- JWT contratista claims: `NameIdentifier=userId`, `Role=CONTRATISTA`, `empresaId=contributorId`, `tipo=CONTRATISTA`, `systemRoles=ids_separados_por_coma`.
- `AllowedFeatures` va en el body de la respuesta (no en el JWT): query SQL `feature → role_feature → user_role` por `userId`.
- Roles internos (`ss_contratista_rol`): solo `ADMIN` y `GESTOR` — no existe `OWNER` en ningún endpoint.
- Config key del link de activación: `FrontendSettings:SetPasswordUrl` (distinto de `App:FrontendUrl` que usa clínica).

### 4. Arquitectura auth clínica — resumen

- Usuarios clínica son filas en `ss_clinica_usuarios` (independiente de `user`).
- Token de activación: `ss_clinica_tokens` — Base64 32 bytes, TTL 48h, tipo `"ACTIVACION"`.
- Fix activo: `dto.Token?.Replace(" ", "+")` antes del lookup (Base64 `+` llega como espacio).
- JWT clínica claims: `NameIdentifier=clinicaUsuarioId`, `Role=CLINICA`, `clinicaId`, `clinicaUsuarioId`, `email`, `tipo=CLINICA`. Expira en 8h.
- Config key del link: `App:FrontendUrl` + `/clinica/activar?token=...`.
- Control de acceso: `ClinicaClaimsHelper.ValidarAcceso` compara `clinicaId` del JWT con el de la ruta. `EmoController` no tiene ningún guard por tipo — cualquier JWT válido puede crear/editar EMOs.

---

## Sesión 2026-05-27 (parte 3) — ProgramacionEmo: Ocupacion, NuevaFecha en ClinicaAccion, limpieza de logs

### 1. ProgramacionListDto — campo Ocupacion agregado

`Features/SsomaModule/SaludOcupacionalFeature/Application/Dtos/Programacion/ProgramacionListDto.cs`:

```csharp
public string? Ocupacion { get; set; }
```

`Features/SsomaModule/SaludOcupacionalFeature/Infrastructure/Repositories/ProgramacionEmoRepository.cs` — método `List`, SELECT:

```csharp
Ocupacion = x.w.Ocupacion
```

Fuente: `Worker.Ocupacion` (columna `ocupacion`). No requiere migración — columna ya existe en BD.

### 2. ProgramacionClinicaAccionDto — campo NuevaFecha para Aceptar

`Features/SsomaModule/SaludOcupacionalFeature/Application/Dtos/Programacion/ProgramacionClinicaAccionDto.cs`:

```csharp
public DateOnly? NuevaFecha { get; set; }
```

`ProgramacionEmoRepository.ClinicaAccion`, case `"Aceptar"` — línea agregada:

```csharp
if (dto.NuevaFecha.HasValue) ent.FechaProgramada = dto.NuevaFecha.Value;
```

Si el frontend no envía `NuevaFecha` (null), el comportamiento es idéntico al anterior. Las otras acciones (Rechazar, CheckIn, Completar) no fueron tocadas.

### 3. Logs de diagnóstico removidos

**`ClinicaAuthController.Activar`** (`ClinicaAuthController.cs`):

- Removidas 2 líneas `Console.WriteLine` con `[DEBUG ACTIVAR]`.
- Conservada la línea `dto.Token?.Replace(" ", "+")` (es lógica de negocio, no diagnóstico).

**`ContratistaAuthService.ActivarCuentaAsync`** (`ContratistaAuthService.cs`):

- Removidas 4 líneas `Console.WriteLine` con `[ACTIVAR] Paso N`.
- Removidas 2 líneas `Console.WriteLine` con `[ACTIVAR ERROR]` y `[ACTIVAR STACK]`.
- Removido el `try/catch` envolvente que solo existía para capturarlos.
- La lógica interna quedó inalterada.

---

## Sesión 2026-05-28 — ProgramacionEmo: FechaVencimientoEmo, Categoria, TipoTrabajador; notificación rechazo; SharePoint SSOMAOcupacional; Interconsulta documentos + SsHabTrabajador

### 1. ProgramacionListDto — tres nuevos campos

`Features/SsomaModule/SaludOcupacionalFeature/Application/Dtos/Programacion/ProgramacionListDto.cs`:

```csharp
public string? Ocupacion { get; set; }          // ya existía
public DateOnly? FechaVencimientoEmo { get; set; }
public string? Categoria { get; set; }
public string? TipoTrabajador { get; set; }
```

### 2. ProgramacionEmoRepository.List — subquery correlacionada para FechaVencimientoEmo

El LEFT JOIN directo a `WorkerEmo` fue reemplazado por una subquery correlacionada en el SELECT para evitar duplicación de filas cuando un worker tiene múltiples EMOs activos con el mismo `TipoEmoId`.

JOIN eliminado del query principal (`from e in ctx.WorkerEmo...`). Tipo anónimo ahora es `{ p, w, em, t, c, m }` (sin `e`).

Campos agregados al SELECT:

```csharp
Ocupacion = x.w.Ocupacion,
Categoria = x.w.Categoria,
TipoTrabajador = x.w.ContrataCasa == "Casa" && x.w.ObraOficina == "Oficina Central"
    ? "Oficina Central"
    : x.w.ContrataCasa == "Casa" && x.w.ObraOficina == "Staff"
        ? "Staff Obra"
        : "Obrero",
FechaVencimientoEmo = ctx.WorkerEmo
    .Where(e => e.WorkerId == x.p.WorkerId
             && e.TipoEmoId == x.p.TipoEmoId
             && e.Activo)
    .OrderByDescending(e => e.FechaVencimiento)
    .Select(e => (DateOnly?)e.FechaVencimiento)
    .FirstOrDefault()
```

`Categoria` y `TipoTrabajador` provienen directamente de `Worker`. `TipoTrabajador` se deriva de `ContrataCasa + ObraOficina` (no es columna directa).

### 3. EmoAutoProgramacionService — ventana reducida a 6 días

`Features/SsomaModule/SaludOcupacionalFeature/Application/Services/EmoAutoProgramacionService.cs`:

```csharp
var ventanaFin = hoy.AddDays(6);  // antes: AddDays(30)
```

El cron de auto-programación ahora solo captura workers cuyo EMO vence en los próximos 6 días (no 30).

### 4. ProgramacionEmoRepository — notificación de rechazo

**Case "Rechazar"** en `ClinicaAccion` extendido:

```csharp
case "Rechazar":
    ent.Estado = "Rechazado por Clínica";
    ent.MotivoRechazo = dto.MotivoRechazo;
    ent.UpdatedAt = DateTimeOffset.UtcNow;
    await ctx.SaveChangesAsync();
    await EnviarNotificacionRechazoAsync(ctx, ent, worker, dto.MotivoRechazo);
    return;
```

Antes solo asignaba los campos y hacía `break` — el `SaveChangesAsync` compartido del final no llegaba a ejecutarse.

**Nuevo método `EnviarNotificacionRechazoAsync`** — mirrors `EnviarNotificacionAceptacionAsync`:

- Mismo routing por tipo worker (Obrero / Staff / OficinaCentral / Contratista → return inmediato)
- Subject: `"[PRUEBAS - NO RESPONDER] [EMO Rechazado] {nombre} — {fecha}"`
- Body HTML igual al de aceptación pero con fila extra en rojo: `"Motivo de rechazo: {motivo}"`

### 5. SharePointHabService — sitio SSOMAOcupacional

`Features/HabilitacionModule/Application/Services/SharePointHabService.cs`:

**`ResolverSiteId`** — nueva condición:

```csharp
if (c.Contains("interconsulta") || c.Contains("lectura-emo"))
    return _configuration["SharePoint:Sites:SSOMAOcupacional:SiteId"]!;
return _configuration["SharePoint:Sites:SSOMAApps:SiteId"]!;
```

**`ResolverLibraryId`** — dos nuevas entradas al final:

```csharp
if (c.Contains("interconsulta")) return _configuration["SharePoint:Sites:SSOMAOcupacional:EmoInterconsultasLibraryId"];
if (c.Contains("lectura-emo"))   return _configuration["SharePoint:Sites:SSOMAOcupacional:LecturaEmosLibraryId"];
```

**`appsettings.json`** — nueva sección añadida bajo `SharePoint:Sites`:

```json
"SSOMAOcupacional": {
  "SiteId": "",
  "EmoInterconsultasLibraryId": "",
  "LecturaEmosLibraryId": ""
}
```

Valores reales van en `appsettings.Local.json` (gitignored).

### 6. InterconsultaController — endpoint SubirDocumento

`Features/SsomaModule/SaludOcupacionalFeature/Presentation/InterconsultaController.cs`:

Inyecciones añadidas: `IDbContextFactory<AppDbContext> _factory`, `ISharePointHabService _sharePoint`.

Nuevo endpoint:

```
POST /api/v1/ssoma/salud-ocupacional/interconsultas/{id}/documentos
[Consumes("multipart/form-data")]  [FromForm] IFormFile file
```

- Valida que `file` no sea nulo ni vacío (400)
- Busca `SsInterconsulta` por id (404 si no existe)
- Sube a SharePoint con contexto `"interconsulta"` → biblioteca `EmoInterconsultasLibraryId`
- Guarda el path retornado en `interconsulta.UrlInforme`
- Retorna `{ url }`

### 7. InterconsultaRepository.Create — actualiza SsHabTrabajador item 25

Tras `ctx.SsInterconsulta.Add(ent)` y antes de `SaveChangesAsync`, actualiza el ítem "Lectura de EMO":

```csharp
var lecturaEmo = await ctx.SsHabTrabajador
    .FirstOrDefaultAsync(h => h.WorkerId == dto.WorkerId && h.ItemId == 25);
if (lecturaEmo != null)
{
    lecturaEmo.Estado = "En revision";
    lecturaEmo.ObsAbril = $"Interconsulta pendiente — {dto.Especialidad}";
    lecturaEmo.UpdatedAt = DateTime.UtcNow;  // DateTime?, no DateTimeOffset
}
```

### 8. InterconsultaRepository.UpdateResultado — efectos colaterales al Completar

Cuando `dto.Estado == "Completado"`, antes del `SaveChangesAsync` final:

1. Actualiza `SsHabTrabajador` item 25 a `"Aprobado"`:

```csharp
lecturaEmo.Estado = "Aprobado";
lecturaEmo.ObsAbril = $"Interconsulta levantada — {dto.FechaAtencion}";
lecturaEmo.UpdatedAt = DateTime.UtcNow;
```

2. Busca la programación EMO activa más reciente del worker y la pone `"En Atención"`:

```csharp
var prog = await ctx.SsProgramacionEmo
    .Where(p => p.WorkerId == ent.WorkerId
             && p.Estado != "Completado"
             && p.Estado != "Cancelado"
             && p.Estado != "Rechazado por Clínica")
    .OrderByDescending(p => p.FechaProgramada)
    .FirstOrDefaultAsync();
if (prog != null)
{
    prog.Estado = "En Atención";
    prog.UpdatedAt = DateTimeOffset.UtcNow;
}
```

`SsProgramacionEmo` no tiene `EmoId` (solo `EmoResultadoId`, FK post-completado) — el vínculo se hace por `WorkerId`.

### Notas técnicas

- `SsHabTrabajador.UpdatedAt` es `DateTime?` → usar `DateTime.UtcNow` (no `DateTimeOffset.UtcNow`)
- `SsProgramacionEmo.UpdatedAt` es `DateTimeOffset?` → usar `DateTimeOffset.UtcNow`
- `ctx.SsHabTrabajador` usa `=> Set<SsHabTrabajador>()` (expression, no `DbSet` propiedad estándar) — sigue siendo accesible igual
  Backend:

InterconsultaCreateDto — ProgramacionId, Diagnostico, Cie10 agregados; EmoId nullable
SsInterconsulta modelo — ProgramacionId, EmoId nullable
InterconsultaRepository.Create — FechaDerivacion automática, EmoId = null, Estado = "Pendiente"
InterconsultaController.Create — [FromForm] multipart, sube documento opcional vía SharePoint
InterconsultaController.SubirDocumento — POST /{id}/documentos restaurado con [FromForm]
EmoRepository.Create — vincula interconsulta pendiente + sube documento + asigna EmoId; retorna EmoCreateResultDto
EmoCreateDto — DocumentoInterconsulta: IFormFile? con [JsonIgnore]
EmoCreateResultDto — DTO nuevo: EmoId + InterconsultaId?
IEmoRepository.Create, IEmoService.Create, EmoService.Create — retornan EmoCreateResultDto (antes int)
EmoController.Create — respuesta incluye { id, interconsultaId, message }
BD — emo_id nullable, programacion_id agregado en ss_interconsultas

Frontend:

ClinicaInterconsultaCreateDto — interface creada
InterconsultaClinicaService — createInterconsulta() con FormData
agenda.ts — confirmarInterconsulta() usa el nuevo servicio

❌ Pendiente
Migración BD:

ALTER TABLE ss_interconsultas ALTER COLUMN emo_id DROP NOT NULL  (ya está en el modelo, falta aplicar en BD)

Frontend:

completar-emo.ts — agregar documentoInterconsulta: File | null = null y handler onDocumentoInterconsulta()
completar-emo.html — agregar input file dentro de *ngIf="requiereInterconsulta"
EmoService.createEmo() — pasar documentoInterconsulta como campo FormData
Después de POST /emos exitoso, si response.interconsultaId != null → llamar POST /interconsultas/{id}/documentos con el archivo

---

## Sesión 2026-05-28 — CronogramaActividades fixes + GET /project/paged-with-residents

Rama: `feature/cronograma-actividades`

### 1. Fix: GET /cronograma-actividades/proyectos filtra por actividades existentes

`Features/UnidadDeProyectosModule/Features/CronogramaActividades/Infrastructure/Repositories/CronogramaActividadesRepository.cs`

`GetProyectosAsync` devolvía todos los proyectos con `TieneUnidadDeProyectos=true`. Corregido para devolver solo los que tienen al menos una fila activa en `project_activity`:

```csharp
.Where(p => p.State && p.TieneUnidadDeProyectos &&
            ctx.ProjectActivity.Any(a => a.ProjectId == p.ProjectId && a.State && a.Active))
```

### 2. Fix: PATCH /cronograma-actividades/actividades/{id}/culminar setea progressPercentage

`CronogramaActividadesRepository.CulminarActividadAsync` solo toggleaba `ActualEndDate`. Corregido:
- Al culminar: `ActualEndDate = hoy`, `ProgressPercentage = 100`
- Al revertir: `ActualEndDate = null`, `ProgressPercentage = 0`

`CulminarActividadDto` ampliado con campo `ProgressPercentage` para que el frontend actualice el estado sin re-fetch.

### 3. Endpoint debug temporal GET /cronograma-actividades/debug-proyectos

Agregado para diagnosticar qué proyectos existen en `project` con sus flags (`project_id`, `project_description`, `tiene_unidad_de_proyectos`, `state`). **Pendiente eliminar** tras confirmar proyectos UDP en producción.

### 4. Fix: GET /project/paged-with-residents devuelve todos los proyectos UDP

`Infrastructure/Repositories/ProjectRepository.cs` — `GetPagedWithResidents`

Tenía `ProjectResident.Any(...)` en el `Where`, que excluía proyectos sin residente asignado (solo salían 7 de 13). Corregido a `Active && State && TieneUnidadDeProyectos`. Proyectos sin residente retornan `residentFullNames: []`.

### 5. Feat: parámetro search en GET /project/paged-with-residents

Agregado `[FromQuery] string? search` que filtra por `project_description` (case-insensitive, `Contains`) antes de la paginación. El `TotalRecords` del response refleja el conteo filtrado.

```
GET /api/v1/project/paged-with-residents?page=1&search=kauri
```

### 6. Feat: pageSize dinámico en GET /project/paged-with-residents

`const int pageSize = 10` estaba hardcodeado en el repository ignorando lo que mandaba el frontend. Reemplazado por parámetro `[FromQuery] int pageSize = 10` que recorre toda la cadena controller → service → repository.

```
GET /api/v1/project/paged-with-residents?page=1&pageSize=12&search=kauri
```

### 7. Firma actual del endpoint

```
GET /api/v1/project/paged-with-residents?page={int=1}&pageSize={int=10}&search={string?}
```

Archivos modificados: `ProjectController.cs`, `IProjectService.cs`, `ProjectService.cs`, `IProjectRepository.cs`, `ProjectRepository.cs`.

---

## Sesión 2026-05-29 — Diagnóstico POST /milestoneScheduleHistory 400

Rama: `feature/cronograma-actividades`

### 1. Investigación de causas de 400 Bad Request en POST /api/v1/milestoneScheduleHistory

Sin modificar código — diagnóstico de lectura.

**Archivos revisados:**
- `Controllers/MilestoneScheduleHistoryController.cs`
- `Application/DTOs/MilestoneScheduleHistory/MilestoneScheduleHistoryCreateDTO.cs`
- `Application/DTOs/MilestoneSchedule/MilestoneScheduleCreateDTO.cs`
- `Infrastructure/Repositories/MilestoneScheduleHistoryRepository.cs`

**DTOs sin validación explícita:**

`MilestoneScheduleHistoryCreateDTO`: `ProjectId`, `List<MilestoneScheduleCreateDTO> MilestoneSchedules`, `bool ForceSave` — ningún `[Required]`.

`MilestoneScheduleCreateDTO`: `MilestoneId`, `Order`, `DateOnly PlannedStartDate` (non-nullable), `DateOnly? PlannedEndDate` — ningún `[Required]`.

**Causa 1 — model binding automático de ASP.NET Core:**

`PlannedStartDate` es `DateOnly` (no-nullable). Si el payload envía `null` o lo omite, el framework rechaza con 400 antes de ejecutar el action. Requiere formato `"YYYY-MM-DD"` en JSON.

**Causa 2 — `AbrilException` desde el repository (llega al controller → `return BadRequest`):**

| Línea | Condición | Mensaje |
|-------|-----------|---------|
| 80 | Mismo count, todos los campos iguales, `ForceSave=false` | `"El cronograma es igual a la última versión subida."` |
| 108 | `DetectChanges` no detecta cambios, `ForceSave=false` | `"El cronograma es igual a la última versión subida."` |
| 122 | `ForceSave=true` pero hay cambios | `"Para guardar sin cambios la última versión subida debe ser igual a la que se está editando actualmente."` |

**Diagnóstico:** el 400 más frecuente en primer envío es `PlannedStartDate` nulo o con formato incorrecto. Si el cronograma ya existe sin cambios, cae en las líneas 80/108.

---

## Sesión 2026-05-29 — CronogramaActividades: importar MPP + jerarquía padre/hijo

Rama: `feature/cronograma-actividades`

### 1. MPXJ.Net instalado

`dotnet add package MPXJ.Net` → versión **16.2.0**. Usa IKVM para compilar Java → .NET en build time (primera compilación lenta, luego cacheada). El namespace correcto es **`MPXJ.Net`** (no `net.sf.mpxj`). API completamente .NET: propiedades PascalCase, fechas como `DateTime?`, sin tipos Java en surface.

Nota de Docker: requiere `libfontconfig` (`RUN apt-get update && apt-get install -y libfontconfig`).

### 2. ProjectActivity — nuevas columnas

Entidad `Shared/Models/ProjectActivity.cs`:
- `ParentId` (int?) — FK self-referencing a la misma tabla, nullable
- `HierarchyLevel` (int) — nivel de jerarquía (0 = raíz); mapeado a `hierarchy_level`

Configuración en `AppDbContext.ConfigurePostgreSQL`:
```csharp
entity.HasOne<ProjectActivity>()
    .WithMany()
    .HasForeignKey(e => e.ParentId)
    .IsRequired(false)
    .OnDelete(DeleteBehavior.SetNull);
```

### 3. Migración EF

`Migrations/20260529194643_AddProjectActivityHierarchy.cs` — agrega `hierarchy_level` (int NOT NULL DEFAULT 0), `parent_id` (int nullable), FK `fk_project_activity_project_activity_parent_id` (ON DELETE SET NULL), índice `ix_project_activity_parent_id`.

**Aplicación en Aiven:** SQL idempotente vía `psql.exe` (nunca `dotnet ef database update` en prod):

```sql
ALTER TABLE project_activity ADD COLUMN IF NOT EXISTS hierarchy_level integer NOT NULL DEFAULT 0;
ALTER TABLE project_activity ADD COLUMN IF NOT EXISTS parent_id integer;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_project_activity_project_activity_parent_id') THEN
        ALTER TABLE project_activity ADD CONSTRAINT fk_project_activity_project_activity_parent_id
            FOREIGN KEY (parent_id) REFERENCES project_activity(project_activity_id) ON DELETE SET NULL;
    END IF;
END $$;
CREATE INDEX IF NOT EXISTS ix_project_activity_parent_id ON project_activity(parent_id);
```

### 4. Endpoint POST /api/v1/cronograma-actividades/{proyectoId}/importar-mpp

**Controlador:** `CronogramaActividadesController` — `[RequestSizeLimit(52_428_800)]` (50 MB), parámetro `IFormFile archivo`.

**Lógica en `CronogramaActividadesRepository.ImportarMppAsync`:**

1. Guarda el IFormFile en temp path (`Path.GetTempPath()`), siempre limpiado en `finally`.
2. Lee con `new Mpxj.UniversalProjectReader().Read(tempPath)` → `ProjectFile`.
3. Calcula `offsetDias` = `proyecto.FechaInicio.DayNumber - mppStartDate.DayNumber` (si ambas están presentes). Aplica el offset a cada fecha de tarea.
4. Elimina **físicamente** (RemoveRange) todas las actividades existentes del proyecto antes de insertar.
5. Itera `projectFile.Tasks` en orden; salta tareas con `Null == true` o nombre vacío.
6. Resuelve `ParentId` en BD usando un diccionario `uniqueId (MPP) → ProjectActivityId (BD)` actualizado tras cada `SaveChangesAsync`.
7. Inserta una actividad por vez (para poder capturar el ID generado y resolver hijos).
8. Retorna `ImportarMppResultDto { ActividadesImportadas, ActividadesEliminadas }`.

**Alias namespace:** `using Mpxj = MPXJ.Net;` evita colisión de `MPXJ.Net.Task` con `System.Threading.Tasks.Task`.

### 5. Fixes en endpoints existentes

**`GetProyectosAsync`** — eliminado filtro `ctx.ProjectActivity.Any(...)` que excluía proyectos sin actividades. Ahora devuelve todos los proyectos con `State && TieneUnidadDeProyectos`, sin importar si tienen actividades cargadas.

**`GetActividadesAsync`** — `ActividadDto` y su mapeo LINQ ampliados con `HierarchyLevel` y `ParentId`.

### 6. Debugging temporal

`ImportarMpp` en el controlador tiene `Console.WriteLine($"[ImportarMpp ERROR] {ex}")` en el catch genérico para visibilidad de errores en consola. Quitar antes de merge a master.

---

## Sesión 2026-05-30 — CronogramaActividades: reordenamiento con order global único + cambio de jerarquía

Rama: `feature/cronograma-actividades`

### 1. Order global único (DFS) — decisión de diseño

`project_activity_order` ahora es **global y único por proyecto** (1, 2, 3, … sin repeticiones), no relativo por nivel de hermanos. El árbol se aplana en orden DFS: cada padre aparece antes que sus hijos, y los hermanos en su orden relativo.

**Problema detectado:** los reordenamientos parciales del frontend (solo hermanos) producían `order` duplicados entre niveles distintos (varias filas con `order=1`, `order=12`, etc.), dejando el `ORDER BY project_activity_order` indeterminado. Verificado con el proyecto 17 (CAPULÍ): 188 actividades con orders repetidos.

**Reparación de datos en Aiven** (CTE recursivo, no se versiona como migración):
```sql
WITH RECURSIVE dfs AS (
    SELECT project_activity_id, ARRAY[project_activity_order] AS sort_path
    FROM project_activity
    WHERE project_id = :pid AND state = true AND parent_id IS NULL
    UNION ALL
    SELECT pa.project_activity_id, dfs.sort_path || pa.project_activity_order
    FROM project_activity pa
    INNER JOIN dfs ON pa.parent_id = dfs.project_activity_id
    WHERE pa.project_id = :pid AND pa.state = true
),
ordered_activities AS (
    SELECT project_activity_id, ROW_NUMBER() OVER (ORDER BY sort_path)::int AS new_order
    FROM dfs
)
UPDATE project_activity pa
SET project_activity_order = oa.new_order
FROM ordered_activities oa
WHERE pa.project_activity_id = oa.project_activity_id;
```
Ejecutado sobre proyecto 17 → 188 filas, 0 duplicados. **Pendiente:** correr el mismo UPDATE en cualquier otro proyecto con datos previos a este fix.

> Nota operativa: `psql` no está en PATH en este entorno. Para consultas/UPDATEs ad-hoc contra Aiven se usó un mini console app .NET 10 temporal con `Npgsql` 10.0.0 (la DLL net10.0 no carga en PowerShell 5.1 vía `Add-Type`). El endpoint `debug-order` (abajo) cubre la inspección de solo-lectura sin salir de la app.

### 2. PATCH /api/v1/cronograma-actividades/{proyectoId}/actividades/reordenar

Ruta exacta consumida por el frontend (es `PATCH`, no `PUT`; va bajo `/actividades/`). El frontend envía **todas** las actividades del proyecto con su nuevo order global.

`ReordenarActividadesAsync(int proyectoId, List<ReordenarItem> items)`:
- Valida lista no vacía (400) y que todas las IDs pertenezcan al proyecto (400 si alguna falta).
- **NO** valida que compartan `parentId` — esa validación se eliminó al pasar a order global (antes existía y rompía el drag&drop entre niveles).
- Actualiza `project_activity_order` de cada item y retorna la lista completa del proyecto ordenada por `Order ASC`.
- Logs de debug en consola: items recibidos, por cada item `ID/parentId/orderAnterior→nuevoOrder`, y `"Reordenamiento completado"`.

`ReordenarItem { ProjectActivityId, Order }`.

### 3. Cambio de jerarquía — subir / bajar nivel

```
PATCH /api/v1/cronograma-actividades/{proyectoId}/actividades/{actividadId}/subir-nivel
PATCH /api/v1/cronograma-actividades/{proyectoId}/actividades/{actividadId}/bajar-nivel
```

Ambos cargan todas las actividades del proyecto en memoria (un query) y propagan el cambio de nivel a los descendientes con el helper recursivo `ActualizarHijosRecursivo(parentId, levelDelta, todas)`.

**`SubirNivelAsync`** (nivel n → n−1):
- 400 `"La actividad ya está en el nivel más alto."` si `HierarchyLevel == 0`.
- Nuevo `ParentId` = `parentId` del padre actual (el abuelo); `null` si el padre era raíz.
- `HierarchyLevel -= 1` en la actividad y `−1` en todos los descendientes.

**`BajarNivelAsync`** (nivel n → n+1):
- Busca el **hermano inmediatamente anterior**: mismo `ParentId`, mayor `Order` que sea menor al de la actividad (`OrderByDescending(Order).FirstOrDefault()`).
- 400 `"No hay un padre disponible para asignar esta actividad."` si no existe hermano anterior.
- Nuevo `ParentId` = ese hermano; `HierarchyLevel += 1` en la actividad y `+1` en descendientes.

Ambos retornan la lista completa del proyecto ordenada por `Order ASC`.

> También existe el `CambiarJerarquiaAsync` genérico (`PUT .../cambiar-jerarquia`, body `{ projectActivityId, nuevoHierarchyLevel, nuevoParentId }`) que aplica el delta de nivel y propaga a hijos. subir/bajar-nivel son los atajos que usa el frontend.

### 4. Crear actividad acepta jerarquía

`CrearActividadRequest` ampliado con `HierarchyLevel` (int, default 0) y `ParentId` (int?). Se persisten en `CrearActividadAsync` y se devuelven en el `ActividadDto`. También se corrigió el return de `EditarActividadAsync` (faltaban `HierarchyLevel` y `ParentId` en el DTO de respuesta).

### 5. GET proyectos → totalActividades

`ProyectoSimpleCronogramaDto` ahora incluye `TotalActividades`. `GetProyectosAsync` lo resuelve con subconsulta correlacionada (traducida a SQL, sin eval en cliente) usando el mismo filtro que `GetActividadesAsync` (`State && Active`), por lo que el conteo coincide con lo que el frontend ve al abrir el proyecto.

### 6. Endpoint debug de solo-lectura

```
GET /api/v1/cronograma-actividades/{proyectoId}/debug-order   [AllowAnonymous]
```
Retorna `[{ projectActivityId, description, order, parentId, hierarchyLevel }]` ordenado por `Order ASC`. Útil para verificar el estado real del árbol/order en BD sin token. **Temporal — quitar antes de merge a master** (junto con `debug-proyectos` y los `Console.WriteLine` de reordenar/importar).

---

## Sesión 2026-06-01 — RecalcularFechasPadres: DFS → BFS+reverso; verificación estructura de módulo

Rama: `feature/cronograma-actividades`

### 1. Bug en `RecalcularFechasPadresInternoAsync` — DFS con memoización reemplazado por BFS + reverso

**Síntoma:** tras importar un MPP, los nodos de nivel 3 (o cualquier nivel intermedio) que tienen hijos mostraban fechas inconsistentes con sus hijos — el recálculo bottom-up no propagaba correctamente a todos los niveles.

**Causa raíz del DFS:** el outer `foreach (var nodo in todas)` podía visitar un nodo intermedio (p.ej. nivel 3, que es padre de nivel 4) *antes* de que el DFS lo alcanzara por la rama de su propio subárbol. Al marcarlo `estado=2` con sus fechas **originales del MPP**, cuando su padre (nivel 2) luego llamaba `Procesar(nivel3)`, lo encontraba terminado y leía las fechas obsoletas en lugar de las actualizadas desde los hijos.

**Solución — BFS + iteración inversa** (archivo: `CronogramaSchedulingService.cs`, método `RecalcularFechasPadresInternoAsync`):

```
BFS desde raíces → bfsOrder = [raíz, nivel1, nivel2, nivel3, nivel4…hojas]
Iterar bfsOrder al revés → [hojas, nivel3, nivel2, nivel1, raíz]
```

Al procesar en este orden, cuando se llega a un nodo padre **todos sus descendientes ya tienen fechas actualizadas** (están en posiciones de mayor índice del array, por tanto se iteraron antes en el `for` invertido). Las referencias en `hijosDe` apuntan a los mismos objetos en memoria → lectura inmediata de valores actualizados.

**Ventajas adicionales:**
- Sin recursión → sin riesgo de `StackOverflowException` en jerarquías profundas
- Nodos huérfanos con ciclo en `ParentId` se agregan al final del BFS y se procesan primero en el reverso (caso defensivo)
- `esPadre` calculado exclusivamente por `hijosDe.TryGetValue(id, ...)` — sin ningún filtro por `HierarchyLevel`

**Los tres criterios verificados:**

| Criterio | Estado |
|---|---|
| Sin filtro por `HierarchyLevel` | ✓ Query solo filtra `ProjectId + State + Active`. `hijosDe` se construye por `ParentId` únicamente |
| Bottom-up garantizado | ✓ BFS invertido: hojas → padres directos → abuelos → nivel 0, sin excepción |
| `esPadre` correcto | ✓ `hijosDe.TryGetValue(id, ...)` ≡ "existe ≥1 actividad con `ParentId = id`" |

Build: 0 errores.

### 2. Verificación de estructura de módulo — ya estaba correctamente separada

Ante una solicitud de reorganizar `UnidadDeProyectosModule` en dos features independientes, se verificó que la separación **ya existía** desde sesiones anteriores:

```
Features\UnidadDeProyectosModule\
├── UnidadDeProyectosModule.cs          ← registro DI de los tres features
└── Features\
    ├── CronogramaActividades\           ← namespace ...CronogramaActividades.*
    ├── ProjectsDashboard\               ← namespace ...ProjectsDashboard.*
    └── LessonsLearnedDashboard\         ← namespace ...LessonsLearnedDashboard.*
```

Sin referencias cruzadas entre features, namespaces correctos en todos los archivos, y `UnidadDeProyectosModule.cs` registrando las tres features por separado. Build limpio.

---

## Sesión 2026-06-01 (cont.) — Fechas línea base + predecesoras para padres + RecalcularFechasPadres fix definitivo

Rama: `feature/cronograma-actividades`

### 1. Fechas línea base en `project_activity`

**BD (Aiven):** `ALTER TABLE project_activity ADD COLUMN IF NOT EXISTS baseline_start_date date, ADD COLUMN IF NOT EXISTS baseline_end_date date;` — aplicado y verificado.

**Migración EF:** `20260601184746_AddBaselineDatesProjectActivity.cs`.

**Modelo:** `ProjectActivity` + `BaselineStartDate DateOnly?` + `BaselineEndDate DateOnly?`.

**DTO:** `ActividadDto` + ambos campos. Nueva clase `ActualizarLineaBaseRequest { BaselineStartDate, BaselineEndDate }`.

**Endpoint:** `PATCH /api/v1/cronograma-actividades/actividades/{id}/linea-base`
- 404 si no existe o inactiva.
- 400 `"La línea base solo puede definirse en actividades hoja (sin sub-actividades)."` si `esPadre = true`.
- Permite sobrescribir si ya tenía fechas base (el frontend advierte al usuario).
- Devuelve `ActividadDto` completo.

Todos los mapeos de `ActividadDto` en el repositorio (7 lugares: `GetActividadesAsync`, `CrearActividadAsync`, `EditarActividadAsync`, y los 4 LINQ-to-SQL en reordenar/subir/bajar/cambiar-jerarquía) actualizados con `BaselineStartDate` y `BaselineEndDate`.

### 2. Predecesoras para nodos padre (cambio de regla)

**Antes:** solo hojas podían ser predecesoras o tener predecesoras.  
**Ahora:** cualquier nodo (padre o hoja) puede ser predecesor o tener predecesoras.

**`SetPredecesorasAsync`** — eliminadas dos validaciones:
- `"Una actividad con sub-actividades no puede tener predecesoras."` → removida.
- `"Una predecesora con sub-actividades no es válida; solo se permiten hojas."` → removida.
- Se conserva: existencia en mismo proyecto, auto-exclusión.

**`CalcularCascadaAsync`** — nueva bifurcación en el bucle Kahn:
- **Sucesor hoja:** comportamiento anterior sin cambios (reposicionar con `AddBusinessDays`, preservar duración hábil).
- **Sucesor padre:** `DesplazarSubarbol(id, deltaCalDias, ...)` — desplaza el nodo padre y TODOS sus descendientes por el mismo delta calendario, manteniendo duraciones y offsets internos.

**`DesplazarSubarbol`** (nuevo método estático privado):
- Calcula `delta = nuevoInicio.DayNumber - actual.PlannedStartDate.DayNumber` (días calendario).
- Aplica `+delta` a `PlannedStartDate` y `PlannedEndDate` del nodo y de cada descendiente recursivamente.
- Registra un `CascadaCambioDto` por cada nodo movido.
- Actualiza `finVigente[id]` para que los sucesores en el grafo de predecesoras vean el fin correcto.
- Si un descendiente tiene además predecesoras externas, la cascada lo reposicionará y `RecalcularFechasPadresInternoAsync` corregirá al padre.

### 3. RecalcularFechasPadresInternoAsync — fix definitivo

**Problema:** la implementación BFS+reverso anterior asumía que el orden inverso de descubrimiento BFS era siempre bottom-up. En árboles con ramas de profundidad desigual, la asignación de ParentId podía no estar perfectamente alineada con HierarchyLevel (especialmente tras importar MPPs con nodos omitidos o raíces virtuales), haciendo que el reverso procesara algunos padres antes que sus descendientes.

**Síntoma observado:** "fila 75 'Proyecto' muestra fin 22/05/2026 pero tiene hijos con fechas hasta 2028".

**Solución:** reemplazar BFS+reverso por `OrderByDescending(HierarchyLevel)`:
- El MPP importa `HierarchyLevel = tarea.OutlineLevel` directamente.
- Un padre siempre está en nivel L, sus hijos en nivel L+1.
- Procesando de mayor a menor nivel, cuando se llega a un padre en nivel L, **todos sus hijos directos (nivel > L) ya tienen fechas actualizadas en memoria**.
- No depende de la coherencia de `ParentId` para el ordenamiento (solo lo usa para detectar hijos vía `hijosDe`).

**Código final:**
```csharp
foreach (var nodo in todas.OrderByDescending(a => a.HierarchyLevel))
{
    if (!hijosDe.TryGetValue(nodo.ProjectActivityId, out var hijos) || hijos.Count == 0) continue;
    var inicios = hijos.Where(h => h.PlannedStartDate.HasValue).Select(h => h.PlannedStartDate!.Value).ToList();
    var fines   = hijos.Where(h => h.PlannedEndDate.HasValue).Select(h => h.PlannedEndDate!.Value).ToList();
    var nuevoInicio = inicios.Count > 0 ? inicios.Min() : (DateOnly?)null;
    var nuevoFin    = fines.Count   > 0 ? fines.Max()   : (DateOnly?)null;
    if (nodo.PlannedStartDate != nuevoInicio || nodo.PlannedEndDate != nuevoFin)
    { nodo.PlannedStartDate = nuevoInicio; nodo.PlannedEndDate = nuevoFin; nodo.UpdatedDateTime = DateTime.UtcNow; }
}
```

`RecalcularFechasPadresAsync` (y su versión interna) se llama en: `ImportarMppAsync`, `AplicarCascadaAsync`, `EditarActividadAsync`.
