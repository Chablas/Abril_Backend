# CONTEXT.md — Abril Backend
> Última actualización: 2026-05-21 — AC: UserId2/ResponsableNombre2, control de acceso por rol (GESTOR/USUARIO AC), ILogger en GetActividades

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
| `Contributor` | `contributor` | `contributor_id` | Entidad unificada de empresas. Reemplazó `companies` (eliminada). Incluye `EsAbril` (bool) e `IdSharepoint` (int?, temporal). En `Features/CostsModule/Shared/Models/Contributor.cs`. |
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
| `SsResetToken` | `ss_reset_token` | — | Token de reset/activación. `EmpresaId` es nullable. Tiene `UserId int?` (FK→`app_user`) para reset de cuentas de usuario directo. |

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

### 7d. contributor reemplazó companies
- `worker_vinculaciones.empresa_id` → `contributor.contributor_id`
- `ss_hab_empresa.empresa_id` → `contributor.contributor_id` (via `ss_empresa_contratista` para joins en bandeja)
- `ss_induccion.empresa_id` → `contributor.contributor_id` directamente
- `ss_sctr_vidaley.empresa_id` → `contributor.contributor_id`
- **`contributor` PK = `contributor_id`** (no `id`)
- Tabla `companies` eliminada. No usar ni referenciar.

### 7e. ss_hab_worker_proyecto — contratistas validados por ss_empresa_proyecto

**IDs en juego (crítico — bug corregido 2026-05-19):**
| Tabla | `EmpresaId` FK apunta a |
|---|---|
| `worker_vinculaciones` | `contributor.contributor_id` (ContributorId) |
| `ss_empresa_proyecto` | `ss_empresa_contratista.id` (SsId) |
| `ss_hab_worker_proyecto` | `contributor.contributor_id` (ContributorId) |

La traducción se hace vía `SsEmpresaContratista.IdLegacy == ContributorId`.

```csharp
// AgregarProyectoAsync — lógica actual (fix 2026-05-19)
if (esContratista)
{
    var empresaId = await ctx.WorkerVinculacion
        .Where(v => v.WorkerId == workerId && v.FechaFin == null)
        .Select(v => v.EmpresaId).FirstOrDefaultAsync();
    // empresaId = ContributorId; SsEmpresaProyecto.EmpresaId = ss_empresa_contratista.id
    // → traducir vía navigation property IdLegacy (no comparar directamente)
    var tieneEntregables = empresaId.HasValue &&
        await ctx.SsEmpresaProyecto
            .AnyAsync(ep => ep.Empresa != null && ep.Empresa.IdLegacy == empresaId.Value
                         && ep.ProyectoId == dto.ProyectoId);
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

---

## 8. Sesión 2026-05-18 (segunda parte) — flujo auth contratistas

### Homologación → auto-envío de credenciales

`ContractorManagementService.Approve()` ahora incluye la lógica de `SendCredentials`: genera token de activación, lo guarda en `contractor.activation_token` y envía el email inmediatamente. Si el contratista no tiene emails registrados, la aprobación igual completa sin error.

### ContractorCredentialsRepository.Create() — tolera app_user existente

Antes lanzaba `AbrilException("Ya existe un usuario con este correo electrónico.", 400)`.  
Ahora: si el `app_user` ya existe, reutiliza el usuario y actualiza la contraseña. Si no existe, crea el registro. En ambos casos verifica con `AnyAsync` antes de insertar `ContractorUser` y `UserRole` para evitar duplicados.

### ContratistaAuthService — allowedFeatures desde BD

`GenerarTokenDto` recibe `List<string> allowedFeatures` como parámetro (antes era array hardcodeado).  
Nuevo helper privado:
```csharp
private static Task<List<string>> GetContratistasFeatureKeysAsync(AppDbContext ctx)
    => ctx.Database.SqlQuery<string>($"""
        SELECT f.feature_key
        FROM feature f
        JOIN role_feature rf ON rf.feature_id = f.feature_id
        JOIN role r ON r.role_id = rf.role_id
        WHERE r.role_description = 'CONTRATISTA'
        """).ToListAsync();
```
Llamado desde `LoginAsync` y `ActivarCuentaAsync`. Gestionar features del contratista directamente en `role_feature` BD.

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

### 7p. IdLegacy — propagación automática al aprobar homologación

`ContractorManagementRepository.Approve()` (2026-05-19): al aprobar un contratista, garantiza que `ss_empresa_contratista` tenga el `id_legacy` correcto:
1. Si no existe fila con el mismo RUC → la crea con `IdLegacy = contributor.ContributorId`, `Activo = false`, `PasswordHash = "PENDIENTE_RESET"`.
2. Si existe pero `IdLegacy == null` → lo asigna.

Esto evita que el flujo de habilitación falle por ausencia del vínculo IdLegacy cuando el contratista aún no ha completado el registro SSOMA.

### 7q. EmpresaContratistaRepository.GetProyectosAsync — doble lookup ContributorId/SsId

`GetProyectosAsync(empresaId)` acepta tanto `ContributorId` (contratistas vía JWT) como `ss_empresa_contratista.id` (admin). Lógica:
```csharp
var ssId = await ctx.SsEmpresaContratista
    .Where(e => e.IdLegacy == empresaId)
    .Select(e => e.Id)
    .FirstOrDefaultAsync();
var idEfectivo = ssId != 0 ? ssId : empresaId;  // ContributorId si no hay match
```
Fallback al valor directo si no hay fila con `id_legacy` coincidente (ej. admin pasando `ss_empresa_contratista.id`).

### 7r. EmpresaContratistaController.Create — auto-resolución IdLegacy por RUC

Al crear una empresa contratista, si `dto.IdLegacy` es null, el controller intenta resolverlo:
```csharp
var idLegacy = dto.IdLegacy ?? await _repo.GetContributorIdByRucAsync(dto.Ruc);
```
`GetContributorIdByRucAsync` busca `contributor WHERE contributor_ruc = ruc → contributor_id`. Devuelve `null` si no existe. Ya no retorna 400 cuando el RUC ya está en `contributor` — ahora vincula automáticamente vía `IdLegacy`.

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

Fix aplicado:
```csharp
// Antes (bug):
.AnyAsync(ep => ep.EmpresaId == empresaId.Value && ep.ProyectoId == dto.ProyectoId)

// Después (fix):
.AnyAsync(ep => ep.Empresa != null && ep.Empresa.IdLegacy == empresaId.Value
             && ep.ProyectoId == dto.ProyectoId)
```
`SsEmpresaContratista.IdLegacy` == `ContributorId` — es el puente entre los dos espacios de IDs.

### HabTrabajadorController.GetWorkers — parámetro soloVerificacion

`Features/HabilitacionModule/Presentation/HabTrabajadorController.cs`:
- Nuevo `[FromQuery] bool soloVerificacion = false`
- Cuando `soloVerificacion = true`, el filtro `empresaId = empresaIdJwt` del contratista NO se aplica
- Permite al frontend verificar si un DNI ya existe en cualquier empresa antes de registrar un nuevo trabajador
- El frontend lo llama con `soloVerificacion: true` solo al verificar duplicados en `verificarExistenciaEnBd()`

### SubidoPorEmpresaId — fix ContributorId → SsId en tres repositorios

`SsHabDocumentoVersion.SubidoPorEmpresaId` espera `ss_empresa_contratista.id` (SsId), pero el JWT `empresaId` claim es `ContributorId` desde 2026-05-18. Los tres repositorios que crean versiones de documento tenían el mismo bug:

| Archivo | Método |
|---|---|
| `HabTrabajadorRepository.cs` ~línea 290 | `UpdateEntregableAsync` |
| `HabEmpresaRepository.cs` ~línea 76 | `UpdateEntregableEmpresaAsync` |
| `EquipoRepository.cs` ~línea 261 | `UpdateEntregableEquipoAsync` |

Fix aplicado en los tres (mismo patrón):
```csharp
int? ssEmpresaId = null;
if (empresaId.HasValue)
    ssEmpresaId = await ctx.SsEmpresaContratista
        .Where(e => e.IdLegacy == empresaId.Value)
        .Select(e => (int?)e.Id)
        .FirstOrDefaultAsync();
// ... luego:
SubidoPorEmpresaId = ssEmpresaId,
```
Si `IdLegacy` no tiene match → `null` (sin error).

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

Guard de rol antes del try, con prioridad GESTOR sobre USUARIO:
```csharp
var esGestor = User.IsInRole("GESTOR DE ARQUITECTURA COMERCIAL");
if (esGestor)
    esUsuarioAc = false;   // ve todas las actividades
else if (User.IsInRole("USUARIO DE ARQUITECTURA COMERCIAL"))
    esUsuarioAc = true;    // ve solo las suyas (user_id o user_id2)
else
    return Forbid();       // 403 para cualquier otro rol
```

`ILogger<ArquitecturaComercialController>` inyectado en constructor. Logs temporales de debug:
```csharp
_logger.LogInformation("Roles del usuario: {roles}", ...);
_logger.LogInformation("esGestor: {esGestor}, esUsuarioAc: {esUsuarioAc}", ...);
```
**Eliminar los dos logs antes de merge a master.**

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
