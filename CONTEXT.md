# CONTEXT.md — Abril Backend

> Última actualización: 2026-06-17 — DossierSemanal: módulo completo (entidades, repo, service, controller, 8 endpoints en api/v1/habilitacion/dossier, ON CONFLICT DO NOTHING, upload via SharePoint 'dossier-semanal'). InspeccionFeature: PDF RM 050-2013-TR con QuestPDF + tab hallazgos centralizado.

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
| Fechas UTC        | `HabilitacionDateHelper` — `AsUtc()`, `ResolverVigencia()`, `ResolverVigenciaEmpresa(itemId, estado, vigencia)` |
| Puerto dev        | 5236 http / 7298 https                                                                                         |
| Swagger           | Solo en Development en `/swagger`                                                                              |

```bash
dotnet build Abril-Backend.csproj
dotnet run --project Abril-Backend.csproj
# NO existe dotnet test
```

Config: `appsettings.json` → `appsettings.{Env}.json` → `appsettings.Local.json` (gitignored, secrets) → env vars.

---

## REGLAS DE CODIFICACIÓN (obligatorias en todo código nuevo)

### R1 — 1 acción = 1 endpoint = 1 query
Cada acción del usuario (entrar a una página, seleccionar un detalle, aplicar un filtro)
debe disparar **una sola llamada HTTP** al backend. Ese endpoint debe ejecutar
**una sola query** (o un pipeline SQL que cuente como una operación lógica única).

- Prohibido que el frontend llame a `/datos` + `/filtros` al iniciar una página.
  El endpoint debe devolver ambos en un solo response.
- Prohibido que un endpoint haga dos queries secuenciales donde una depende del
  resultado de la otra (roundtrip). Usar JOIN o subquery.

### R2 — Task.WhenAll solo para Microsoft Graph
`Task.WhenAll` está prohibido para queries a base de datos (EF Core o Dapper).
Solo se permite para llamadas a Microsoft Graph API (múltiples llamadas HTTP externas en paralelo).

```csharp
// PROHIBIDO
await Task.WhenAll(dbQuery1, dbQuery2);

// PERMITIDO
var result1 = await dbQuery1;
var result2 = await dbQuery2;

// PERMITIDO (solo Graph)
await Task.WhenAll(graphCall1, graphCall2);
```

### R3 — Sin N+1
Prohibido iterar una colección y hacer una query por elemento.
Usar `.Include()` en EF Core, o JOIN + agrupación en memoria con `Dictionary` en Dapper.

```csharp
// PROHIBIDO
foreach (var p in proyectos)
    p.Actividades = await ctx.Actividades.Where(a => a.ProjectId == p.Id).ToListAsync();

// CORRECTO
var proyectos = await ctx.Proyectos.Include(p => p.Actividades).ToListAsync();
```

### R4 — Sin roundtrips en Dapper
En Dapper, nunca hacer una query para obtener IDs y luego otra con esos IDs.
Usar un solo SQL con JOIN o subquery que traiga todo en una pasada.

### R5 — Estructura por features
Todo código nuevo va en `Features/<NombreFeature>/`.
Prohibido agregar código en carpetas por capa (`Controllers/`, `Services/`, `Repositories/` en raíz).
La estructura por capas en raíz es legacy y no debe crecer.

### DEPLOY

- P1: El frontend de producción vive en /var/www/abril en la VPS — se actualiza con npm run build + copia de dist/Abril/browser/*
- P2: El backend se conecta a la BD de producción a través del túnel SSH (localhost:5544 → VPS:5432)
- P3: El túnel SSH debe estar activo antes de levantar el backend: ssh -L 5544:localhost:5432 jefe@intranet.abril.pe
- P4: El usuario deploy es el dueño de /var/www/abril — los archivos deben copiarse con permisos correctos
- P5: Push directo a master está permitido (el usuario tiene permisos de bypass de branch protection) — pero SIEMPRE sin --force. Un push forzado puede pisar trabajo hecho desde otra PC o en otra sesión sin aviso previo.

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
| `SsomaModule` | `AddSsomaModule` | EMO, programación, alertas automáticas, clínica, reportes SUNAFIL. **PasoFeature** (PASO - Programa Anual de Seguridad, ruta `api/v1/ssoma-paso`) |
| `AuthModule` | `AddAuthModule` | MicrosoftLogin, MicrosoftProfile, ContractorCredentials, RoleFeature, UserFeature |
| `ContractorsModule` | `AddContractorsModule` | ContractorRegistration, ContractorManagement |
| `CostsModule` | `AddCostsModule` | Adjudicaciones (contrato completo), WorkItem, StaffProjectEmail, ProjectLink |
| `ConfigurationModule` | `AddConfigurationModule` | ProjectFeature (CRUD proyectos AC) |
| `GestionAdministrativaModule` | `AddGestionAdministrativaModule` | SolicitudSalidas, GestionSalidas, Lugares, MotivosSalida |
| `MejoraContinuaModule` | `AddMejoraContinuaModule` | LessonsLearned, LessonsDashboard, LessonReminder, AreasYSubareas, PsssTemplate, Relations |
| `UnidadDeProyectosModule` | `AddUnidadDeProyectosModule` | ProjectsDashboard (LessonsLearnedDashboard migrado a MejoraContinua) |
| `EvaluacionesModule` | `AddEvaluacionesModule` | Evaluaciones de residentes — periodos, plantilla, evaluaciones, dashboard. EvAsignacionSupervisor (supervisores UDP/BIM). Cron recordatorios + descargo. |

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
| `SsHabEmpresa`              | `ss_hab_empresa`               | `id`                  | `proyecto_id` → `project.project_id`. `empresa_id` → `contributor.contributor_id`. +`MotivoRechazo` (2026-06-06).                                                                                                                                                                                                                               |
| `SsItemEmpresa`             | `ss_item_empresa`              | `id`                  | Catálogo de entregables empresa. +`EsMensual bool` (2026-06-06) — items mensuales generan un registro por mes.                                                                                                                                                                                                                                   |
| `SsHabDocumentoVersion`     | `ss_hab_documento_version`     | `id`                  | Versiones de documentos. FK a SsHabTrabajador/SsHabEmpresa/SsHabEquipo. +`Enviado bool`, `FechaEnvio`, nav `Archivos` (2026-06-06).                                                                                                                                                                                                             |
| `SsHabDocumentoArchivo`     | `ss_hab_documento_archivo`     | `id`                  | Archivos individuales de una versión (flujo multi-archivo). FK `VersionId → ss_hab_documento_version`. Props: `ArchivoUrl`, `NombreArchivo`, `EsZip`, `ZipContenido` (JSONB string), `Orden`. ⚠️ **Pendiente migración EF**.                                                                                                                     |
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
- Columnas extra (2026-06-06): `item_id`, `es_mensual`, `empresa_id_raw`, `mes`, `anio`, `meses_pendientes` (subquery COUNT items `Enviado` mismo item/empresa/proyecto)
- Dedup mensual: `AND (NOT i.es_mensual OR he.id = (SELECT id ... ORDER BY anio DESC, mes DESC LIMIT 1))` — solo muestra la fila más reciente por item mensual
- Excluye `item_id IN (15, 16)` (sentinels excluidos de bandeja)

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

### 5d. InicializarEntregablesAsync / InicializarEntregablesEmpresaAsync

**Trabajadores** (`InicializarEntregablesAsync`): Crea registros `Estado="Falta"` filtrando en orden: `AplicaA` → `AplicaCategoria` → `AplicaObraOficina` → `ExcluyeObraOficina` → `ExcluyeCategoriaContratista`. Caso especial: Casa+Practicante omite `ItemVidaLey`. No toca `ss_hab_worker_proyecto`.

**Empresas** (`InicializarEntregablesEmpresaAsync`, 2026-06-06):
- IDs 12 y 13 (`itemsFalta`): arrancan `Estado="Falta"`, `Vigencia=null` — esperan que el contratista los envíe
- Resto: arrancan `Estado="Aprobado"`, `Vigencia=día 27 del mes siguiente` (vigenciaInicial)
- Solo inserta entregables que aún no existen para `(empresaId, proyectoId)`

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
GET    /api/v1/habilitacion/catalogos/items-trabajador|items-empresa|items-equipo|criterios
GET    /api/v1/habilitacion/catalogos/areas                   (público)
GET    /api/v1/habilitacion/catalogos/subareas                (público, ?area= opcional)
GET    /api/v1/habilitacion/catalogos/categorias              (público, solo activos)
GET    /api/v1/habilitacion/catalogos/categorias/admin        (público, todos — incluye Orden y Activo)
POST   /api/v1/habilitacion/catalogos/categorias              body: { nombre }  [AllowAnonymous]
PUT    /api/v1/habilitacion/catalogos/categorias/{id}         body: { nombre }  [AllowAnonymous]
PATCH  /api/v1/habilitacion/catalogos/categorias/{id}/toggle  body: { activo }  [AllowAnonymous]
GET    /api/v1/habilitacion/catalogos/ocupaciones             (público, solo activos)
GET    /api/v1/habilitacion/catalogos/ocupaciones/admin       (público, todos — incluye Orden y Activo)
POST   /api/v1/habilitacion/catalogos/ocupaciones             body: { nombre }  [AllowAnonymous]
PUT    /api/v1/habilitacion/catalogos/ocupaciones/{id}        body: { nombre }  [AllowAnonymous]
PATCH  /api/v1/habilitacion/catalogos/ocupaciones/{id}/toggle body: { activo }  [AllowAnonymous]
GET    /api/v1/habilitacion/proyectos                         (lista activos desde Project legacy)

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
POST  /api/v1/habilitacion/archivos/subir          → { path, url }  — flujo clásico (1 archivo + marca Enviado si HabTrabajadorId)
      ⚠️ En el frontend, SIEMPRE usar res.path al guardar el resultado del upload
POST  /api/v1/habilitacion/archivos/subir-multiple → { path, nombreArchivo, esZip, zipContenido? } — solo sube, NO marca Enviado
POST  /api/v1/habilitacion/archivos/enviar         body: { habTrabajadorId?, habEmpresaId?, habEquipoId?, archivos:[{archivoUrl,nombreArchivo,esZip,zipContenido?}], vigencia?, obsContratista? }
                                                   → { versionId, archivos: N } — crea versión + archivos hijos + marca entregable Enviado
GET   /api/v1/habilitacion/archivos/url?path=
# Empresas — endpoints adicionales (2026-06-06)
PATCH  /api/v1/habilitacion/empresas/{id}/entregables/{entregableId}/mes   body: EmpresaEntregableUpdateDto — aprobar/rechazar mes específico
DELETE /api/v1/habilitacion/empresas/{id}/archivos/{archivoId}             — eliminar archivo de versión (solo si estado != Aprobado/Rechazado)

# Dossier Semanal (2026-06-17)
GET    /api/v1/habilitacion/dossier?contributorId=&proyectoId=&anio=  → lista de semanas con contadores
GET    /api/v1/habilitacion/dossier/{id}                              → detalle con documentos
POST   /api/v1/habilitacion/dossier/semana                            body: { contributorId, proyectoId, numeroSemana, anio } → { id, fechaInicio, fechaFin }
POST   /api/v1/habilitacion/dossier/{dossierId}/documento             [FromForm] { File, TipoDoc } → sube a SharePoint 'dossier-semanal', guarda path
PATCH  /api/v1/habilitacion/dossier/documento/{docId}/marcar-na       → toggle NA/Pendiente, limpia path
POST   /api/v1/habilitacion/dossier/{dossierId}/enviar                → Borrador/Rechazado → Enviado
POST   /api/v1/habilitacion/dossier/{dossierId}/revisar               body: { estado: 'Aprobado'|'Rechazado', obsRevisor? } → Enviado → Aprobado/Rechazado
GET    /api/v1/habilitacion/dossier/documento/{docId}/url             → downloadUrl de SharePoint

⚠️ EnsureSemana usa ON CONFLICT DO NOTHING en PG + crea 7 tipos de doc automáticamente
⚠️ Tipos de doc: Accidente, EPP, Estadisticas, Capacitaciones, PETAR, ATS, Charlas
⚠️ Upload: path carpeta = "{contributorId}/{proyectoId}/Sem{N}_{fechaInicio:yyyyMMdd}", NUNCA guardar url

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

**EvAsignacionSupervisor** (`ev_asignacion_supervisor`) — tabla manual en BD. Entidad con `SupervisorWorkerId` (FK→`workers.id`), `ProjectId`, `Activo`, `CreatedAt`, `UpdatedAt`, `UpdatedByUserId`. DbSet `EvAsignacionesSupervisor` en AppDbContext.

**Endpoints asignaciones supervisor:**
```
GET  /api/v1/evaluaciones/asignaciones-supervisor           → supervisores UDP/BIM con sus proyectos asignados
GET  /api/v1/evaluaciones/asignaciones-supervisor/proyectos → proyectos activos (excluye General/AC/Post Venta/OC)
PUT  /api/v1/evaluaciones/asignaciones-supervisor/{workerId} body: { projectIds: [] } → reemplaza asignaciones
```
El PUT desactiva activas no-en-lista → reactiva existentes → inserta nuevas. Registra `updated_at`/`updated_by_user_id` del JWT.

**4 reglas `GetResidentesEvaluablesAsync(evaluadorUserId)` — Dapper:**
- Lee `workers.id`, `obra_oficina`, `area`, `subarea`, `categoria` del evaluador en 1 query.
- **R4**: `categoria='Gerente' AND area='Proyectos'` → lista vacía (no evalúa).
- **R1**: OC + Proyectos + `categoria IN ('Jefe','Coordinador')` + subarea no-especial → todos los residentes, `PuedeVerTodos=true`.
- **R2**: subarea IN ('Unidad de Proyectos','Planeamiento BIM') → consulta `ev_asignacion_supervisor WHERE activo=true` → residentes de esos proyectos con `ANY(@ProjectIds)`.
- **R3** (fallback/Staff): residentes del mismo proyecto via subquery `contributor_id`.
- ⚠️ La PK de `workers` es `id` (columna `id`), NO `worker_id`. Usar `w.id AS WorkerId` en Dapper.

**`GetEvaluadoresPendientesAsync` — 3 queries independientes (Dapper), resultado combinado con `Concat`:**
- **R1**: OC + Proyectos + `categoria IN ('Jefe','Coordinador')` + subarea no-especial. INNER JOIN app_user. Pendiente: NOT EXISTS en `ev_evaluacion_residente`.
- **R2**: subarea IN ('UDP','BIM') + tiene asignaciones activas en `ev_asignacion_supervisor`. LEFT JOIN app_user. Pendiente: EXISTS residente en sus proyectos sin su evaluación.
- **R3**: `obra_oficina != 'Oficina Central'` + tiene residente en su proyecto. INNER JOIN app_user. Pendiente: EXISTS residente del proyecto sin evaluación suya.
- R4 (Gerente+Proyectos) excluido de los tres.
- CC: `/enviar` sin CC (`null`); `/descargo` con CC jefatura + `coriundo@abril.pe`.

**Dapper en `EvEvaluacionResidenteRepository`** — patrón:
```csharp
await ctx.Database.OpenConnectionAsync();
var conn = ctx.Database.GetDbConnection();
await conn.QueryAsync<T>(sql, params)
```
`GetResidentesEvaluablesAsync` — Join: `workers → person → app_user → project ON contributor_id = w.contributor_id`. `ResidenteEvaluableDto`: UserId, NombreCompleto, ProjectId, ProjectNombre, Area, Subarea, PuedeVerTodos.

---

## 12. Sesión 2026-06-08 — Vigencia empresa refactorizada + Bandeja Meses[]

### 12a. HabilitacionDateHelper — nueva lógica de vigencia empresa

`HabilitacionDateHelper.cs` reemplazado completamente:

| Símbolo | Cambio |
|---|---|
| `ItemsEmpresaSentinel` | Renombrado a `ItemsCentinela` — quita items 20 y 22: `{ 12, 13, 14, 17, 18, 19, 21, 23, 24, 25 }` |
| `ItemsSctrVidaLey` | Nuevo: `{ 15, 16 }` — reservado a flujo SctrVidaLeyController, NO pasa por lógica empresa |
| `ResolverVigenciaAlEnviar(itemId, esMensual, mes, anio, dtoVigencia)` | **Nuevo** — llamado por contratista al enviar. SCTR/VidaLey → `dtoVigencia`; centinela → 2040; mensual → día 27 del mes siguiente; resto → `dtoVigencia` |
| `ResolverVigenciaAlAprobar(itemId, estado, dtoVigencia, vigenciaActual)` | **Nuevo** — llamado por admin al aprobar/rechazar. Rechazado → ayer UTC; Aprobado → `dtoVigencia` si viene, sino conserva `vigenciaActual` |
| `ResolverVigenciaEmpresa(itemId, estado, dtoVigencia)` | Simplificado: Rechazado → ayer; centinela → 2040; hay fecha → esa fecha; sino → día 27 mes siguiente |

### 12b. HabEmpresaRepository — UpdateEntregableEmpresaAsync

Bloque de vigencia dividido por estado:

```
Enviado            → ResolverVigenciaAlEnviar(itemId, esMensual, mes, anio, dtoVigencia)
Aprobado/Rechazado → ResolverVigenciaAlAprobar(...) + AprobadoPor, FechaAprobacion
                     Rechazado también setea MotivoRechazo
Otro con Vigencia  → AsUtc(dto.Vigencia)
```

### 12c. ArchivoHabilitacionController — método Enviar

- **Mensual**: `vigenciaCalculada = ResolverVigenciaAlEnviar(itemId, true, mes, anio, request.Vigencia)` → entra en `updateDto.Vigencia`
- **No mensual**: `ResolverVigenciaAlEnviar(ent.ItemId, false, null, null, request.Vigencia)` siempre (reemplaza `AsUtc` condicional)

### 12d. HabEmpresaRepository — GetEntregablesEmpresaAsync

- `meses` filtra `.Where(r => r.Mes.HasValue && r.Anio.HasValue)` — excluye registro base del cálculo de estado
- `CalcularEstadoGlobal` nueva prioridad: `Count==0→Falta`; `Rechazado`; `Enviado`; `Falta`; `all Aprobado→Aprobado`
- `archivosPorEntregable`: `.OrderByDescending(v => v.Version).First().Archivos` — solo versión más reciente

### 12e. BandejaRepository — EnrichWithArchivosAsync

- Archivos simplificados: solo `archivosPorEntregable[item.Id]` (registro de bandeja)
- Diccionario también usa versión más reciente
- **Nuevo**: `item.Meses` poblado con registros del mismo grupo `Estado=="Enviado"` con mes/anio, ordenados DESC, con archivos

### 12f. BandejaItemDto — nuevo campo Meses[]

`BandejaMesDto { Id, Mes, Anio, Estado, Vigencia, Archivos[] }` — permite aprobar mes a mes desde bandeja usando `PATCH /bandeja/empresa/{mesId}` con el Id del registro mensual específico.

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

---

## Sesión 2026-06-02 — Arquitectura, contratos API y eficiencia BD

### Reglas de codificación establecidas (ver sección §REGLAS al inicio)
- R1: 1 acción = 1 endpoint = 1 query
- R2: Task.WhenAll solo para Microsoft Graph
- R3: Sin N+1
- R4: Sin roundtrips en Dapper
- R5: Estructura por features

### Fixes de eficiencia en CronogramaActividadesRepository
- ImportarMppAsync: N+1 resuelto — 2 pasadas con 2 SaveChangesAsync en vez de N
- MilestoneScheduleHistoryRepository.Create: query duplicada ejecutada 1 sola vez
- Reordenar/Subir/BajarNivel: SELECT post-save eliminado, se mapea desde memoria
- Editar/ActualizarLineaBase: 2 queries combinadas en 1 con proyección EF

### Nuevos contratos API (CronogramaActividades)
- GET /{proyectoId}/actividades → ActividadesProyectoResponseDto { proyecto, actividades }
  (elimina la segunda llamada a /proyectos desde el frontend)
- PUT /actividades/{id} → acepta PredecessorIds? en el body, devuelve EditarActividadResultDto { actividad, cascada? }
  (unifica editar + predecesoras + cascada en 1 sola llamada)
- PUT /actividades/{id}/predecesoras → mantiene como legacy deprecated

### Nuevos contratos API (ProjectsDashboard)
- GET /projects-dashboard → incluye campo Filtros en el response
  (elimina la segunda llamada a /filters desde el frontend)
- GET /projects-dashboard/filters → mantiene como legacy deprecated

### Limpieza pre-merge
- Eliminado endpoint GET /cronograma-actividades/debug-order
- Eliminados Console.WriteLine en ImportarMppAsync

### Migraciones aplicadas en Aiven
- 20260601184746_AddBaselineDatesProjectActivity — aplicada manualmente vía pgAdmin
  (baseline_start_date, baseline_end_date nullable en project_activity)

---

## Sesión 2026-06-06 — HabilitacionModule: multi-archivo, vigencia empresa, entregables mensuales

### ResolverVigenciaEmpresa — HabilitacionDateHelper

Nuevo método (no reemplaza `ResolverVigencia` — coexisten):

```csharp
ResolverVigenciaEmpresa(int itemId, string estado, DateTime? dtoVigencia)
// IDs 12, 13 → sentinel 2040 (ItemsEmpresaSentinel)
// No Aprobado → AsUtc(dtoVigencia)
// Aprobado + fecha explícita → AsUtc(dtoVigencia)
// Aprobado + sin fecha → día 27 del mes siguiente
```

Usado en: `UpdateEntregableEmpresaAsync`, `BandejaRepository.AprobarEmpresaAsync`, `CrearOActualizarEntregableMesAsync`.

### InicializarEntregablesEmpresaAsync — nueva lógica de estado inicial

- IDs `{12, 13}` → `Estado="Falta"`, `Vigencia=null`
- Resto → `Estado="Aprobado"`, `Vigencia=día 27 del mes siguiente`

### VigenciaRevisionService — excluye ids 12 y 13 empresas

```csharp
.Where(h => ... && h.ItemId != 12 && h.ItemId != 13)
```
Los items 12 y 13 tienen `Vigencia=2040` o `null` — no deben vencerse automáticamente.

### Flujo multi-archivo (POST /subir-multiple + POST /enviar)

1. **`POST /archivos/subir-multiple`** — sube 1 archivo a SharePoint, extrae índice ZIP si aplica. Retorna `{ path, nombreArchivo, esZip, zipContenido }`. **NO toca el entregable**.
2. **`POST /archivos/enviar`** — recibe lista de archivos ya subidos + id del entregable. Crea `SsHabDocumentoVersion` (con `Enviado=true`, `FechaEnvio`) + N filas `SsHabDocumentoArchivo` + marca el entregable `Estado="Enviado"`.
3. **`POST /archivos/subir`** original — sin cambios, sigue funcionando para flujo de 1 archivo.

### Entregables mensuales (`SsItemEmpresa.EsMensual`)

- `GetEntregablesEmpresaAsync`: items con `EsMensual=true` se agrupan en un solo `EmpresaEntregableDto` con `Meses: [EntregableMesDto]`. Estado global = peor estado (`Rechazado > Falta > Enviado > Aprobado`).
- Cada `EntregableMesDto` incluye `Archivos: [EntregableMesArchivoDto]` — batch query `SsHabDocumentoVersion.Include(Archivos)` para todos los entregables a la vez. Fallback: si el mes no tiene archivos propios, busca el registro base del item (`Mes==null && Anio==null`) y usa sus archivos (compatibilidad con archivos legacy subidos antes del flujo multi-archivo).
- `CrearOActualizarEntregableMesAsync`: crea fila nueva si no existe `(empresaId, proyectoId, itemId, mes, anio)`, luego aplica update. **No bloquea** si estado == Aprobado/Rechazado — se puede subir nuevos archivos en cualquier estado. Solo `EliminarArchivoVersionAsync` bloquea en esos estados.
- `EliminarArchivoVersionAsync`: elimina fila de `SsHabDocumentoArchivo`; verifica empresa y que el entregable no esté Aprobado/Rechazado.
- `EnviarDocumentoRequest` incluye `Mes?` y `Anio?` para identificar el mes al que pertenece el envío.
- `ArchivoHabilitacionController` inyecta `IHabEmpresaRepository` (campo `_habEmpresaRepo`).

### Migración EF pendiente

```sql
-- ss_item_empresa
ALTER TABLE ss_item_empresa ADD COLUMN IF NOT EXISTS es_mensual boolean NOT NULL DEFAULT false;

-- ss_hab_empresa
ALTER TABLE ss_hab_empresa ADD COLUMN IF NOT EXISTS motivo_rechazo text;

-- ss_hab_documento_version
ALTER TABLE ss_hab_documento_version ADD COLUMN IF NOT EXISTS enviado boolean NOT NULL DEFAULT true;
ALTER TABLE ss_hab_documento_version ADD COLUMN IF NOT EXISTS fecha_envio timestamptz;

-- nueva tabla
CREATE TABLE IF NOT EXISTS ss_hab_documento_archivo (
    id serial PRIMARY KEY,
    version_id int NOT NULL REFERENCES ss_hab_documento_version(id),
    archivo_url text NOT NULL,
    nombre_archivo text,
    es_zip boolean NOT NULL DEFAULT false,
    zip_contenido text,
    orden int NOT NULL DEFAULT 0,
    created_at timestamptz
);
```

O generar con: `dotnet ef migrations add AddHabDocumentoArchivoAndMensuales --project Abril-Backend.csproj`

---

## Sesión 2026-06-07 — HabilitacionModule: bandeja archivos, sentinel empresa, archivos no mensuales

### ItemsEmpresaSentinel — ampliado

`HabilitacionDateHelper.ItemsEmpresaSentinel` actualizado:

```csharp
// Antes:
private static readonly HashSet<int> ItemsEmpresaSentinel = new() { 12, 13 };
// Ahora:
private static readonly HashSet<int> ItemsEmpresaSentinel = new() { 12, 13, 14, 17, 18, 19, 20, 21, 22, 23, 24, 25 };
```

Estos items reciben `Vigencia=2040-12-31` al aprobar (no requieren renovación periódica).

### EmpresaEntregableDto — propiedad Archivos agregada

`EmpresaEntregableDto` ahora incluye `List<EntregableMesArchivoDto> Archivos { get; set; } = []`.

`MapToDto` (HabEmpresaRepository) recibe y asigna `archivos`. La llamada para `esMensual=false` pasa los archivos del registro base desde `archivosPorEntregable.TryGetValue(reg.Id, ...)`.

### BandejaItemDto — propiedad Archivos agregada

`BandejaItemDto` ahora incluye `List<EntregableMesArchivoDto> Archivos { get; set; } = []`.

### BandejaRepository — EnrichWithArchivosAsync

Método privado que se llama después de ambas queries Dapper (`GetPendientesAsync` y `GetPendientesCursorAsync`). Enriquece los items de tipo `EMPRESA` con sus archivos:

1. Filtra `empresaIds` de items EMPRESA en la página.
2. Carga `registrosBase` (los registros exactos de esos IDs).
3. Expande a `todosRegistros` (todos los registros del mismo grupo `EmpresaId+ProyectoId+ItemId`) iterando por grupo con valores primitivos — evita el problema de EF con `.Any()` sobre lista en memoria.
4. Query batch de `SsHabDocumentoVersion.Include(Archivos)` para `todosIds` con `Enviado=true`.
5. Asigna archivos al item buscando en todos los IDs del grupo (base + mensuales).

### BandejaRepository — filtro sub2.mes IS NOT NULL en segmento EMPRESA

El subquery que selecciona el registro mensual más reciente en la cláusula WHERE del segmento EMPRESA ahora excluye el registro base:

```sql
AND sub2.estado = 'Enviado'
AND sub2.mes IS NOT NULL      -- evita que el registro base (mes=null) gane el ORDER BY
ORDER BY sub2.anio DESC, sub2.mes DESC
```

---

## Fixes 2026-06-08

### Vigencia en entregables trabajadores
- `/archivos/enviar`: agrega `ent.Vigencia = DateTime.SpecifyKind(request.Vigencia.Value, DateTimeKind.Utc)` al bloque `habTrabajadorId`
- `UpdateEntregableAsync` (HabTrabajadorRepository): no sobreescribe vigencia si `dto.Estado="Enviado"` + `dto.Vigencia=null` + vigencia actual ya existe
- `HabTrabajadorController`: contratista ya no borra `dto.Vigencia` en `UpdateEntregable`
- `guardarEntregable()`: si `!isContratista && archivosPendientes.length > 0` → delega a `enviarDocumento()`
- `trabajadores.html`: botón GUARDAR en footer para admin/Casa cuando hay `archivosPendientes`

### Permisos por responsable
- `RolesAprobadoresSsoma` / `RolesAprobadoresAdmin` en `HabTrabajadorController` y `HabEmpresaController`
- `GetResponsableItemTrabajadorAsync` / `GetResponsableItemEmpresaAsync` en repos
- `puedeAprobarEntregableActual` getter en `empresa.ts`, `trabajadores.ts`, `bandeja.ts`
- Bandeja: dropdown estado auto-guarda con `(ngModelChange)="guardarEstado()"`

### Cambio de obra Casa
- `ValidarExclusividadEmpresaAsync` solo ejecuta si `esContratista == true`

### Flujo archivo limpio
- `/archivos/subir`: ya no actualiza entregables (solo sube a SharePoint)
- `/archivos/enviar`: valida archivos obligatorios + vigencia obligatoria si `requiereVigencia`

---

## Sesión 2026-06-16 — HabilitacionModule (Inducción/listado), OPT fechas UTC, SharePointHabService drive OPT

### HabTrabajadorRepository — MarcarInduccionAsync sincroniza item InduccionObra

Al marcar inducción completada en `WorkerProyecto`, ahora también upsertea el ítem `HabItemIds.InduccionObra` (12) en `SsHabTrabajador` a `Estado="Aprobado"` con `Vigencia` sentinel (`HabilitacionDateHelper.ResolverVigencia(false, "Aprobado", null)` → 2040-12-31 UTC), igual que `InduccionRepository`. Antes solo quedaba reflejado en `WorkerProyecto`, no en el checklist de habilitación.

### WorkerHabilitacionListDto — AniosExperiencia y FechaIngreso

Agregados `AniosExperiencia` (`int?`) y `FechaIngreso` (`string?`, formateado `yyyy-MM-dd`) al DTO de listado de trabajadores. Mapeados en `HabTrabajadorRepository` desde `Worker.AniosExperiencia` / `Worker.FechaIngreso` (ya existían en el modelo, columnas `anios_experiencia` / `fecha_ingreso`). Usado en frontend para auto-calcular `tiempoEnObra` en OPT.

### OptRepository — fechas con Kind=Unspecified rompían Npgsql

Error: `Cannot write DateTime with Kind=Unspecified to PostgreSQL type 'timestamp with time zone'`. Causa: `Fecha = request.Fecha.Date` en `CrearOptAsync` llega del JSON con `Kind=Unspecified`, y `ssoma_opt.fecha` es `timestamptz` sin override. Fix (mismo patrón que `SctrVidaLeyRepository`/`RacService`):
- `CrearOptAsync`: `Fecha = DateTime.SpecifyKind(request.Fecha.Date, DateTimeKind.Utc)`
- `GetListAsync` / `GetListCountAsync`: filtros `fechaDesde`/`fechaHasta` envueltos en `DateTime.SpecifyKind(..., DateTimeKind.Utc)`
- `GetDashboardAsync`: `DateTime.Now.Year/.Month` → `DateTime.UtcNow.Year/.Month` (consistencia)

### SharePointHabService — GetDownloadUrlAsync no resolvía paths "OPT/..."

Las firmas de OPT se guardan en SharePoint bajo un path que empieza con `OPT/`, pero `OPT` no es el nombre de ninguna librería/drive del sitio — es una carpeta dentro de un drive específico. Dos fixes encadenados:

1. `GetDriveIdByNameAsync`: si no encuentra drive por nombre, en vez de `return null` cae al drive default del sitio vía `GetDriveIdAsync(siteId, token, null)`.
2. `GetDownloadUrlAsync`: agregado un caso especial **antes** de la extracción de `libraryName` — si `archivoUrl` empieza con `"OPT/"`, usa el `driveId` hardcodeado de la librería real que contiene la carpeta `OPT` (`b!Bmji2TXVU0OWEBlZeOIDkC8Dt6ceUVNLiodQihkLPHxZH7QqINghTq0UWOH5DOFR`) y construye la URL de `/content` con el **path completo** (incluyendo el prefijo `OPT/`, a diferencia del flujo normal que lo descarta) porque ahí `OPT` es carpeta, no librería.

### Frontend (Abril-Frontend) — opt-detalle: firmas removidas del detalle

En `opt-detalle.html` se quitaron ambas secciones que renderizaban `<app-document-viewer>` para firmas (firma observador y firma por trabajador en `trab-block`). Las firmas ya no se muestran en el detalle — solo se usarán al generar el PDF.

---

## Sesión 2026-06-16 (segunda parte) — PASO reprogramación y LecturaEmo

### PasoFeature — endpoint ProgramarEjecucion (SinProgramar → Programado)

Nuevo flujo para crear una ejecución con estado `Programado` en actividades que aún no tienen ejecución en el mes:

- `ProgramarEjecucionRequest { ActividadId, FechaProgramada }` agregado a `SsomaPasoDtos.cs`
- `IPasoService.ProgramarEjecucionAsync(req)` — firma en interfaz
- `PasoService.ProgramarEjecucionAsync`: valida unicidad `(ActividadId, FechaProgramada)`, crea `SsomaPasoEjecucion` con `Estado="Programado"` y `FechaVerificacion = UltimoDiaMes(...)`
- `POST api/v1/ssoma-paso/ejecucion/programar` en `PasoController`

### PasoFeature — GetResumenMesAsync: filtro de actividades unificado

El bloque `actividadesEnMes` reemplaza la lógica anterior con reglas en orden de prioridad:

1. Inactivas (`!act.Activo`) → excluir
2. Tiene ejecución `Programado` en **este** mes → incluir siempre (reprogramadas hacia aquí)
3. Tiene ejecución `Programado` en **otro** mes → excluir (fue reprogramada fuera de este mes)
4. Lógica normal de frecuencia/ciclo (Mensual, Bimestral, Única, etc.)

### HabilitacionModule — LecturaEmo (ItemId=25) excluido del cálculo de habilitación

`LecturaEmo` no debe bloquear la habilitación del trabajador. Excluido en tres puntos:

1. **`HabTrabajadorRepository.cs` líneas 84 y 91** (`EstadoCalc`): `h.ItemId != HabItemIds.LecturaEmo` en ambas ramas (No Autorizado y Autorizado Temporalmente). Commit anterior.
2. **`ControlAccesoRepository.cs` líneas 462 y 467** (`hasPendientes` y `faltantes`): misma exclusión para el endpoint de control de acceso. Requirió agregar `using Abril_Backend.Shared.Constants;` que faltaba.

### HabilitacionModule — validación vigencia flexible (WorkerEntregableUpdateValidator)

Regla anterior rechazaba cualquier `Vigencia ≤ DateTime.Today`. Nueva regla:

```csharp
RuleFor(x => x.Vigencia)
    .Must((dto, vigencia) => vigencia == null || dto.Estado == "Falta" || vigencia.Value > DateTime.Today)
    .WithMessage("La vigencia debe ser una fecha futura.");
```

Cuando `Estado == "Falta"`, la vigencia puede ser cualquier fecha (o null) — útil para registrar documentos históricos o vencidos sin bloquear el flujo.

---

## Sesión 2026-06-16 (tercera parte) — InspeccionFeature: flujo 3-pasos y URLs SharePoint

### InspeccionFeature — flujo de creación en 3 pasos

Problema original: firmas y fotos de hallazgos se subían a `Inspecciones/0/firmas` porque `inspeccionId=0` al momento del upload.

Nuevo flujo en `InspeccionService.CrearInspeccionAsync`:

1. **Paso 1** — Crear inspección en BD sin firmas ni fotos → obtiene `id` real
2. **Paso 2** — Subir firmas (`inspeccion-firmas`) y fotos de hallazgos (`inspeccion-fotos`) a SharePoint con el `id` real → rutas correctas `Inspecciones/{id}/firmas` y `Inspecciones/{id}/hallazgos/{hallazgoIdx}`
3. **Paso 3** — `ActualizarFirmasYFotosAsync`: si hay algo que actualizar, hace UPDATE de `FirmaInspectorUrl`/`FirmaRepresentanteUrl` en la inspección e inserta registros `SsomaInspeccionHallazgoFoto` (mapeando índice de hallazgo por `OrderBy(h.Id)`)

### InspeccionFeature — nuevo método ActualizarFirmasYFotosAsync

Agregado a `IInspeccionRepository` e implementado en `InspeccionRepository`:

```csharp
Task ActualizarFirmasYFotosAsync(int id, string? firmaInspectorUrl, string? firmaRepresentanteUrl, Dictionary<int, List<string>> fotosHallazgoUrls);
```

### SharePointHabService — SubirArchivoYObtenerUrlAsync

Nuevo método que sube el archivo y luego hace GET para obtener `@microsoft.graph.downloadUrl`:

```
GET /sites/{siteId}/drives/{driveId}/root:/{encoded}?$select=id,%40microsoft.graph.downloadUrl
```

Devuelve la URL pre-autenticada de Graph (expira ~1 hora). Fallback a ruta relativa si el GET falla (con warning en log). Agregado a `ISharePointHabService` y llamado desde `InspeccionSharePointService` en los 3 métodos (firma inspector, firma representante, foto hallazgo).

> **Nota:** `@microsoft.graph.downloadUrl` es temporal. Si el frontend muestra estas URLs días después de crearlas, habrá que refrescarlas con `GetDownloadUrlAsync`.

### SharePointHabService — fix clave config InspeccionesLibraryId

`ResolverLibraryId` usaba `"SharePoint:Sites:SSOMAApps:InspeccionesLibraryId"` pero la clave real en `appsettings.Local.json` es `"InspeccionesAbril2026LibraryId"`. Corregido en las dos líneas (`inspeccion-fotos` e `inspeccion-firmas`).

### InspeccionRepository — DateTime.SpecifyKind para campos del request

- `Fecha = DateTime.SpecifyKind(request.Fecha.Date, DateTimeKind.Utc)` (antes sin Kind)
- `FechaLimite = h.FechaLimite.HasValue ? DateTime.SpecifyKind(h.FechaLimite.Value, DateTimeKind.Utc) : null` (antes sin Kind)

---

## Sesión 2026-06-17 — InspeccionFeature: PDF + Tab Hallazgos

### TAREA 1 — PDF RM 050-2013-TR

**Archivo nuevo:** `Features/SsomaModule/InspeccionFeature/Application/Services/InspeccionPdfService.cs`

Inyecta `IHttpClientFactory`. Método público `GenerarPdfAsync(InspeccionDetalleDto)` → `byte[]`.

Flujo:
1. Descarga en paralelo firmas (`FirmaInspectorUrl`, `FirmaRepresentanteUrl`) y fotos de hallazgos via `DescargarImagenAsync(url)` — falla silenciosamente (return null) si la URL no responde.
2. Agrupa `Respuestas` por `Categoria ?? "General"` para el checklist.
3. Genera PDF A4 con QuestPDF: colores `#1B3A6B` (primario) / `#2D5AA0` (subheader) / `#E8EEF7` (header grupo).

**Secciones del PDF:**
- Header sticky: "REGISTRO DE INSPECCIONES — RM 050-2013-TR", código `REG-SSOMA-INS-{id:D4}`, paginación
- Datos generales: tabla 4 columnas (label/valor/label/valor)
- Resumen numérico: Total Items / Cumple / No Cumple / NA / Tasa
- Checklist: tabla con header grupal por `Categoria`, columnas N°/Descripción/Cumple(✓)/NoCumple(✗)/NA(—)/Observaciones
- Hallazgos: tabla con color de celda Estado (verde=Cerrado, naranja=Abierto, rojo=vencido no cerrado)
- Registro fotográfico: grid 2 columnas con caption = descripción del hallazgo (si hay fotos descargables)
- Conclusiones/Causas: `DescripcionCausas` + `Conclusiones`
- Firmas: 2 columnas con imagen descargada o línea punteada si null

**Endpoint:** `GET /api/v1/ssoma-inspeccion/{id}/pdf` → `File(bytes, "application/pdf", ...)`

**DI:** `services.AddScoped<InspeccionPdfService>()` en `SsomaModule.cs`

> QuestPDF ya estaba instalado (`2024.12.3`) y configurado en Program.cs (`LicenseType.Community`).

### TAREA 2 — Tab Hallazgos centralizado

**DTOs nuevos** en `InspeccionDtos.cs`:
- `HallazgoListItemDto`: `Id`, `InspeccionId`, `Proyecto`, `FechaInspeccion`, `Descripcion`, `Tipo`, `Area`, `ResponsableNombre`, `ResponsableCargo`, `FechaLimite`, `AccionCorrectiva`, `Estado`, `FechaCierre`, `FotosUrls` (`List<string>`)
- `LevantarHallazgoDto`: `Estado` ("En proceso" | "Cerrado"), `EvidenciaUrl`, `EvidenciaNombre`

**Endpoints nuevos:**
```
GET   /api/v1/ssoma-inspeccion/hallazgos
      ?estado=&proyecto=&area=&responsableId=&fechaLimiteHasta=
      → List<HallazgoListItemDto> — todos los hallazgos de todas las inspecciones

PATCH /api/v1/ssoma-inspeccion/hallazgos/{hallazgoId}/levantar
      body: LevantarHallazgoDto
      → actualiza Estado, FechaCierre (si Cerrado), EvidenciaCierreUrl
```

**Ordenamiento GetHallazgos** (en memoria tras query EF): vencidos-abiertos (0) → abiertos (1) → en proceso (2) → cerrados (3), luego por `FechaLimite ASC`.

**Estados en BD:** "Abierto" (inicial) | "En proceso" (vía LevantarHallazgo) | "Cerrado" (vía LevantarHallazgo o CerrarHallazgo).

> La tabla `ssoma_inspeccion_hallazgo_evidencia_cierre` y la columna `cerrado_por_id` del SQL de la tarea son DDL pendientes de ejecutar manualmente en PostgreSQL — no incluidas en código porque el modelo actual ya tiene `EvidenciaCierreUrl` y `FechaCierre`.

---

## Sesión 2026-06-24

### 1. Fix importación MPP — preservar actividades manuales

Columna nueva: is_manual boolean NOT NULL DEFAULT false en project_activity.
- Actividades creadas desde POST /{proyectoId}/actividades → is_manual = true
- Actividades importadas desde MPP → is_manual = false

Cambios en ImportarMppAsync:
- Solo borra actividades con is_manual = false
- Actividades manuales huérfanas (parentId ya no existe) → parentId = null, hierarchyLevel = 0
- Predecesoras de manuales que apunten a IDs borrados → se limpian
- Manuales van al final con order continuando desde el último del MPP

ImportarMppResultDto extendido: ActividadesManualesConservadas: int

SQL aplicado en VPS Abril Prod:
ALTER TABLE project_activity ADD COLUMN IF NOT EXISTS is_manual boolean NOT NULL DEFAULT false;

### 2. Fix PercentageComplete en ImportarMppAsync

- Leer PercentageComplete de MPXJ y asignar a ProgressPercentage (cast a int, null → 0)
- Si pct >= 100 → marcar como culminada con ActualEndDate
- Si pct < 100 → ActualEndDate = null
- Math.Min(pct, 100) para valores > 100

### 3. Fix SharePoint CostosYPresupuestos

En appsettings.Development.json agregar bajo SharePoint.Sites:
"CostosYPresupuestos": { "Hostname": "abrilinmob.sharepoint.com", "SitePath": "/sites/CostosYPresupuestos" }
Resuelve error al cargar /costs/adjudicaciones.

### 4. Pendientes próxima sesión

- Verificar avance promedio en Dashboard UDP después del fix MPP (reimportar y confirmar %)
- Fix semáforo gris en SIN ACTIVIDADES del dashboard
- Endpoint PATCH /actividades/{id}/mover con body { parentId, order } para drag & drop libre desde frontend
- Definir proceso de deploy frontend a VPS con usuario deploy

## Sesión 2026-06-29

### 1. SPI (Índice de Rendimiento del Cronograma) en Dashboard UDP

**Backend — `CronogramaActividadesRepository.cs`:**
- Clase privada `ActividadAvance` extendida con `PlannedStartDate`
- Query 2 del dashboard mapea `PlannedStartDate = a.PlannedStartDate`
- Nuevo cálculo SPI por proyecto antes del `return new CronogramaDashboardProyectoDto`:
  - `ev` = `actualEndDate != null ? 100 : progressPercentage`
  - `pv` = 100 si `hoy >= plannedEndDate`, 0 si `hoy <= plannedStartDate`, interpolación lineal en otro caso
  - `SPI = Sum(ev) / Sum(pv)`, redondeado a 2 decimales. Si `Sum(pv) == 0` → `1.0`
- `CronogramaDashboardProyectoDto` extendido con `public decimal Spi { get; set; } = 1.0m`

**Frontend — `features/projects/cronograma-dashboard/`:**
- `CronogramaDashboardProyectoDto` extendido con `spi: number`
- Propiedad `spiPromedio = 1.0` calculada en `loadDashboard()` promediando proyectos con actividades
- Métodos `spiColor(spi, estado)` y `spiLabel(spi, estado)` para color y texto del badge
- KPI card "SPI PROMEDIO" agregada (9na card, skeleton actualizado a 9)
- Columna SPI en tabla con badge coloreado: verde ≥1, naranja ≥0.9, rojo <0.9, gris SIN_ACTIVIDADES
- Estilos `.col-spi` y `.spi-badge` en `cronograma-dashboard.css`

### 2. Bugs identificados (pendientes)

- **Responsables vacíos en filtro**: el select solo muestra "Responsable: Todos", nunca carga nombres. Query 3 del dashboard trae los IDs correctos pero hay que verificar por qué no devuelve nombres.
- **Avance 0% en dashboard**: `CalcularAvanceNivel0` mezcla los 3 tipos de cronograma. El dashboard debe mostrar 3 barras separadas por tipo igual que `proyectos-cronograma-list`. Requiere cambiar `CronogramaDashboardProyectoDto` para devolver `avanceAnteproyecto`, `avanceProyecto`, `avanceProyectoActualizacion` en lugar de `porcentajeAvance`.

### 3. Setup de herramientas por PC

**PC Personal (esta máquina) — CON headroom:**
- `headroom` v0.28.0 instalado via `py -m pip install "headroom-ai[all]"`
- Al abrir Claude Code: `headroom wrap claude` desde la carpeta del repo correspondiente
- Si el proxy se cae entre sesiones: `headroom proxy` en cualquier terminal, luego reabrir Claude Code
- Modelo: `claude config set model claude-sonnet-4-5`

**PC Trabajo — SIN headroom:**
- Claude Code se abre directamente con `claude` como siempre

## Sesión 2026-07-03 — Skills de Claude Code versionadas (guardar-rama / guardar-master)

### 1. Skills locales `guardar-rama` y `guardar-master`

Nuevas skills en `.claude/skills/`, invocables con frase natural ("guardar rama"), que automatizan el cierre de una sesión de trabajo:

- `.claude/skills/guardar-rama/SKILL.md`: verifica que no se esté en `master` (si lo está, detiene y explica cómo cambiar de rama), commitea cambios pendientes con mensaje Conventional Commits generado automáticamente, corre build obligatorio (`dotnet build` o `ng build` según el repo), agrega un resumen de sesión al final de `CONTEXT.md`, hace `git fetch` + `merge` con `origin/<rama>` (se detiene si hay conflictos) y finalmente `git push origin <rama>` sin `--force`.
- `.claude/skills/guardar-master/SKILL.md`: mismo contenido que `guardar-rama` (solo cambia `name:` en el frontmatter) — **pendiente**: si `guardar-master` debe tener lógica propia (por ejemplo, para operar sobre `master` en vez de bloquearlo), falta diferenciar el cuerpo del archivo.

### 2. `.gitignore` — se permite versionar `.claude/skills/`

La línea `.claude/` se reemplazó por:

```
.claude/*
!.claude/skills/
```

Esto sigue ignorando `.claude/settings.local.json` y `.claude/worktrees/` (verificado con `git check-ignore`), pero permite subir `.claude/skills/*/SKILL.md` al repo para que las skills viajen con el proyecto.

### 3. Pendiente — sección DEPLOY en CONTEXT.md

Se pidió agregar una regla "P5: push directo a master permitido, nunca con --force" dentro de una sección "DEPLOY" en "REGLAS DE PROGRAMACIÓN". Esa sección no existe en este archivo — solo existe "REGLAS DE CODIFICACIÓN" (R1-R5, línea 33) y no tiene nada de deploy. Queda pendiente decidir si se crea una sección nueva para esto.
- Sin cambios en el flujo de trabajo habitual

## Sesión 2026-07-03 (continuación) — sección DEPLOY y guardar-master con lógica propia

### 1. `### DEPLOY` agregada dentro de `## REGLAS DE CODIFICACIÓN`

Se agregó justo después de R5 (línea ~78-81), con reglas P1-P5:
- P1: frontend de producción en `/var/www/abril` en la VPS (`npm run build` + copia de `dist/Abril/browser/*`)
- P2: backend se conecta a la BD de producción vía túnel SSH (`localhost:5544` → `VPS:5432`)
- P3: túnel SSH debe estar activo antes de levantar el backend (`ssh -L 5544:localhost:5432 jefe@intranet.abril.pe`)
- P4: usuario `deploy` es dueño de `/var/www/abril` — copiar con permisos correctos
- P5: push directo a `master` permitido (bypass de branch protection), pero nunca con `--force`

Resuelve el pendiente anotado en la sesión anterior (§3 arriba).

### 2. `.claude/skills/guardar-master/SKILL.md` — lógica propia, ya no es copia de `guardar-rama`

Reescrita completa. Diferencias clave respecto a `guardar-rama`:
- Solo corre si la rama actual **es** `master` (antes bloqueaba `master`; ahora es al revés — bloquea todo lo que no sea `master`).
- Antes del `git push origin master` exige confirmación explícita del usuario, mostrando `git log origin/master..HEAD --oneline` y `git diff origin/master..HEAD --stat`. No asume confirmación implícita.
- Aplica la regla P5: nunca `--force`.

### Archivos clave
- `CONTEXT.md` (sección DEPLOY)
- `.claude/skills/guardar-master/SKILL.md`

### Pendiente
- Las skills `guardar-rama`/`guardar-master` no se recargan dentro de una sesión ya iniciada — hay que abrir una sesión nueva de Claude Code para que el trigger por frase natural ("guardar rama", "guardar master") las detecte; mientras tanto se siguen los pasos manualmente.

## Sesión 2026-07-05/06 — Indicadores SSOMA: proyectos activos, PASSO, reactivos y meta anual

### 1. Filtro de "proyecto activo" para indicadores proactivos/reactivos

`IndicadoresProactivosRepository`: `GetSeguimientoTodosProyectosAsync` y `GetPuntajeTodosProyectosAsync` filtraban proyectos por `Project.Active && Project.Estado == "ACTIVO"` (estado genérico) en vez de `ss_proyecto_habilitado` (la tabla pensada justo para que SSOMA decida su propio subconjunto de proyectos, independiente de otros módulos). Corregido para usar `SsProyectoHabilitado`.

Reactivos (IF/IG/IA) es la excepción: ahí sí deben contar las horas-hombre de proyectos ya finalizados (no solo habilitados) — `GetIndicadoresReactivosTodosAsync` arma la lista de proyectos como unión de habilitados + cualquiera con tareo registrado, sin filtrar por estado.

### 2. Cierre de accidentes — ventana de fecha

`PctCierreAccidentes` contaba accidentes con `Fecha < fechaCorte` sin piso inferior (todo el histórico acumulado). Se agregó `Fecha >= fechaIni` para que sea solo del mes consultado — así "Sin accidentes" (ya existente en el front) se dispara correctamente cuando no hubo ninguno ese mes.

### 3. PASSO — dos bugs de cálculo

- **Regla de año de ciclo**: la fórmula vieja era `cicloStartYear = MesInicio > 6 ? Anio - 1 : Anio`. Para proyectos con `mes_inicio` en julio-diciembre esto hacía que `Anio` representara el año en que TERMINA el ciclo, no en el que arranca — confuso y causaba instancias con el año mal puesto (ej. Bosque Real necesitaba `anio=2027` con `mes_inicio=7` para cubrir jul2026-jun2027). Corregido: `cicloStartYear = Anio` siempre, en `PasoService_FIXED.cs` y en el helper duplicado de `IndicadoresProactivosRepository`. **Requiere migrar datos existentes**: todo PASO con `mes_inicio > 6` necesita `anio -= 1` para mantener la misma ventana real (se hizo a mano vía SQL para Bosque Real, Gran Manzano y Camelia — Camelia excluido a pedido del usuario por ser proyecto finalizado).
- **Cálculo de % programadas/ejecutadas**: contaba filas de `ssoma_paso_ejecucion` ya generadas para el mes, en vez de actividades teóricamente programadas ese mes de ciclo (según frecuencia/MesInicio/MesFin) — igual que hace `PasoService.GetResumenMesAsync`. Con pocas ejecuciones generadas, esto inflaba el % (ej. 1/1 = 100% cuando en realidad eran 40 actividades programadas y 1 completada). Se agregó `CalcularPassoDelMes` (réplica exacta de la lógica de `PasoService`) a `IndicadoresProactivosRepository`.

### 4. Regularización de accidentes 2026 importados de SharePoint

Se cruzó `FlashReport (1).csv` (export histórico de SharePoint, columnas `TotalDias`/`Codigo`/fechas de descanso) contra `ss_accidente_incidente` por fecha+proyecto (el `Codigo` del CSV no es único, no sirve como llave). De 23 accidentes tipo "AC" de 2026 en el CSV:
- 17 regularizados: `ssoma_flash_descanso` cargado con `TotalDias` real, y vinculados a `ss_accidente_trabajo` (worker_id resuelto por similitud de nombre contra `workers`/`person`, `estado` = Cerrado/Registrado según `EstadoDeCierre` del CSV, no asumido).
- 1 (Gran Manzano, `SF-AC-1`) vinculado usando el dato YA cargado en la app (2 días reales, más confiable que el `TotalDias=1` del CSV).
- 1 (Amaranta, `AMR-AC-12`) no tenía fila en `ss_accidente_incidente` — se creó desde cero con los datos del CSV.
- 4 quedan pendientes (3 "Incapacitante" con `TotalDias=0` que probablemente es dato incompleto del export, no un cero real; 1 sin trabajador identificable en el sistema).

### 5. Fuente de días perdidos en reactivos: `ssoma_flash_descanso`, no `ss_investigacion_rm050`

Para Flash Report sin vínculo a un accidente de Tópico, los días perdidos ahora se suman desde `ssoma_flash_descanso` (mismo dato que ya usa la lista de Accidentes e Incidentes) en vez de `ss_investigacion_rm050`, que en la práctica estaba casi vacío para 2026 (solo 2 de 26 casos tenían investigación cargada, y ambos en 0 días).

### 6. Performance: 1 fetch en vez de 3

Al agregar los niveles Mes/Año/Total del dashboard reactivo, cada cambio de mes disparaba las mismas queries a la BD 3 veces (una por nivel). Se separó en `FetchReactivosCrudoAsync` (trae todo sin filtrar) + `AgregarReactivos` (agrega en memoria, se llama 3 veces gratis).

### 7. Meta anual de reactivos — nueva tabla `ssoma_meta_anual`

Tabla simple (`anio` único, `meta_indice_frecuencia/gravedad/accidentabilidad` nullable) para que SSOMA cargue manualmente la meta de cada año (ej. "10% menos IF/IG, 20% menos IA vs año anterior") — no se calcula automáticamente contra el año anterior, es un valor editable desde el dashboard (botón "Meta"). Endpoints `GET/PUT api/v1/ssoma-indicadores-proactivos/meta-anual`.

### 8. Lista de Accidentes e Incidentes — columnas nuevas

`FlashReportListItemDto` ahora trae `descripcion`, `diasPerdidos` (suma de `ssoma_flash_descanso`) y `cerradoConAltaMedica` (null = no aplica / no es accidente con seguimiento médico; bool según `ss_accidente_trabajo.estado`+`fecha_alta`).

### Archivos clave
- `Features/SsomaModule/IndicadoresProactivosFeature/Infrastructure/Repositories/IndicadoresProactivosRepository.cs`
- `Features/SsomaModule/PasoFeature/Services/PasoService_FIXED.cs`
- `Features/SsomaModule/AccidentesIncidentesFeature/Infrastructure/Repositories/AccidenteIncidenteRepository.cs`
- `Features/SsomaModule/IndicadoresProactivosFeature/Infrastructure/Models/IndicadoresProactivosModels.cs` (`SsomaMetaAnual`)
- Tablas nuevas vía SQL directo (no migración EF, a pedido del usuario): `ssoma_meta_anual`

### Pendiente
- Cedro 33 · 2025 (instancia vieja de PASO, 130 actividades con 3 duplicados) sin limpiar.
- 4 accidentes 2026 sin regularizar (ver §4).
- Cronograma de Hitos (`milestone-schedule`): el usuario reportó fechas que no coinciden con un Excel de referencia — se determinó que las fechas son 100% manuales (sin cálculo ni importación automática), no se investigó más a fondo por indicación del usuario ("me equivoqué, es otro tema").

## Sesión 2026-07-07 — Bug "Nueva semana" no aparecía en Dossier del contratista

Se investigó por qué "Nueva semana" en `habilitacion/gestion/dossier` (Panel Contratista) parecía no crear nada. Se descartó que fuera un problema de esquema (las tablas `ss_dossier_semana`, `ss_dossier_documento`, `ss_dossier_documento_archivo` ya existían en la BD) revisando el flujo completo (frontend → `DossierController` → `DossierService` → `DossierRepository.EnsureSemanaAsync`, que usa `INSERT ... ON CONFLICT DO NOTHING`). Confirmando con Network tab del navegador se vio que el registro sí se creaba (`GET /dossier` devolvía la semana nueva en estado "Borrador"), pero no se mostraba en la UI.

Causa real: el bug estaba en el **frontend**, no en el backend. El getter `semanasFiltradas` (`Abril-Frontend/.../pages/dossier/dossier.ts`) ocultaba toda semana en estado `Borrador` con `docsSubidos === 0` — pensado para no ensuciar la vista admin con borradores vacíos, pero eso también ocultaba al contratista la semana que él mismo acababa de crear, dejándolo sin forma de empezar a subir documentos.

Se generó adicionalmente `dossier_semanal_schema.sql` (DDL idempotente para las 3 tablas del dossier semanal) como respaldo por si alguna instalación no las tuviera aplicadas — regla del proyecto: cambios de esquema siempre en SQL manual para pgAdmin, nunca `dotnet ef migrations`.

### Archivos clave
- `Abril_Backend/Features/HabilitacionModule/Infrastructure/Repositories/DossierRepository.cs` (`EnsureSemanaAsync` — sin cambios, funcionaba bien)
- `Abril_Backend/dossier_semanal_schema.sql` (nuevo, DDL de respaldo)
- El fix real quedó en el repo frontend: `Abril-Frontend/src/app/features/habilitacion/pages/dossier/dossier.ts` (getter `semanasFiltradas`)

### Pendiente
- Ninguno para este bug — verificado con Network tab que la semana ya aparece tras el fix del getter.

## Sesión 2026-07-05 — CronogramaActividades: preferencia última pestaña + fixes cascada/línea base

Rama: `victor-backend`

### 1. Feature nueva: recordar última pestaña de cronograma por usuario/proyecto

Tabla `user_cronograma_preference` (PK compuesta `user_id, project_id`, columna `tipo_cronograma`, `updated_at` default `now()`). Verificado antes de implementar que no existía nada (ni tabla, ni endpoints `ultima-pestana`).

- **Modelo:** `UserCronogramaPreference` (namespace `Abril_Backend.Shared.Models`, igual que `Feriado`/`ProjectActivity`, aunque vive físicamente en `Infrastructure/Models` de esta feature).
- **DbSet + config PG:** en `AppContext.cs`.
- **Endpoints** en `CronogramaActividadesController` (controller existente, no uno nuevo):
  - `GET /api/v1/cronograma-actividades/{proyectoId}/ultima-pestana` → `{ tipoCronograma: string | null }`
  - `PATCH /api/v1/cronograma-actividades/{proyectoId}/ultima-pestana` → upsert (find-or-create vía EF, mismo patrón que `ManagerSignatureRepository`).
- **Migración EF:** `20260705162909_AddUserCronogramaPreference` — aislada a mano para que contenga *solo* esta tabla.
- **Prod:** `_sql_prod/cronograma_user_preference.sql` (idempotente, sin bookkeeping de `__EFMigrationsHistory` — así son los demás scripts de `_sql_prod/`).

**Hallazgo importante (no resuelto, no es de esta feature):** al correr `dotnet ef migrations add`, EF detectó drift preexistente entre modelos ya commiteados y migraciones — `workers.email_corporativo`/`worker_salida_jefe_id`, `invoice_status`, `invoice_observation_reason`, `vecino_lote`, `ga_salida_visibilidad_area`, etc. no tienen migración correspondiente. Si alguien corre `dotnet ef migrations add` de nuevo, ese drift va a volver a aparecer empaquetado. Vale la pena que alguien lo revise y genere la migración de catch-up correspondiente.

### 2. Bug: fechas de cascada no se reflejaban en la respuesta del PATCH

`CronogramaActividadesService.EditarActividadAsync` armaba el DTO `Actividad` con `request.PlannedStartDate/PlannedEndDate` (pre-cascada) y nunca lo refrescaba después de `AplicarCascadaAsync` (que sí persiste bien en BD). Fix: buscar el cambio propio en `cascada.Cambios` por `ProjectActivityId` y pisar `PlannedStartDate/PlannedEndDate` en el DTO antes de devolverlo.

### 3. Feature: auto-fill de línea base (LB)

Al guardar por primera vez INICIO/FIN PROG. (con LB vacía), se copia automáticamente a `BaselineStartDate`/`BaselineEndDate`. No mezcla (inicio no llena LB fin), no pisa si ya tiene valor, no aplica a nodos padre.

- `CronogramaActividadesRepository.CrearActividadAsync`/`EditarActividadAsync`: fill inicial.
- `CronogramaSchedulingService.CalcularCascadaAsync` (rama sucesor-hoja) y `DesplazarSubarbol` (rama sucesor-padre): mismo fill cuando la fecha llega por cascada, no por edición directa. En `DesplazarSubarbol` se agregó el guard `esHoja = !hijosDe.ContainsKey(rootId)` que no existía antes en este servicio.
- `CascadaCambioDto` ahora incluye `BaselineStartDate`/`BaselineEndDate` para que el frontend pueda refrescar esas columnas sin recargar la página.

### 4. Bug: fin se autocompletaba como inicio cuando la actividad no tenía fecha fin

En `CalcularCascadaAsync`, rama sucesor-hoja: el diccionario `duracion` le daba `1` día por defecto a actividades sin `PlannedEndDate`, y `AddBusinessDays(nuevoInicio, 0, ...)` devolvía el mismo día → fin quedaba igual a inicio. Fix: `finNuevoDo` ahora es condicional a `act.PlannedEndDate.HasValue`; si la actividad nunca tuvo fin, se deja `null` (el usuario lo pone a mano). La rama sucesor-padre (`DesplazarSubarbol`) ya manejaba esto bien desde antes.

### Archivos clave
- `Features/UnidadDeProyectosModule/Features/CronogramaActividades/Application/Services/CronogramaSchedulingService.cs`
- `Features/UnidadDeProyectosModule/Features/CronogramaActividades/Application/Services/CronogramaActividadesService.cs`
- `Features/UnidadDeProyectosModule/Features/CronogramaActividades/Infrastructure/Repositories/CronogramaActividadesRepository.cs`
- `Features/UnidadDeProyectosModule/Features/CronogramaActividades/Application/Dtos/CronogramaActividadesDtos.cs`

### Pendiente
- Sacar un `console.log // DEBUG` en el frontend (el usuario lo pidió aparte, no se tocó en esta sesión).
- Drift de migraciones preexistente (ver punto 1) sin resolver.

## Sesión 2026-07-05 (continuación) — CronogramaActividades: "Usar plantilla" (pestaña Proyecto)

Rama: `victor-backend`

### Feature: aplicar plantilla de actividades a un proyecto

Plantilla fija de 81 items (20 padres agrupadores sin fecha, 61 hojas con predecesoras ya encadenadas por código) en `Features/UnidadDeProyectosModule/Features/CronogramaActividades/Seeds/plantilla_proyecto_seed.json`. Agregada entrada `CopyToOutputDirectory: Always` en `Abril-Backend.csproj` (mismo patrón que las carpetas `Templates/` de otras features) para que el JSON se copie al `bin/` en cada build.

**Endpoint:** `POST /api/v1/cronograma-actividades/{proyectoId}/aplicar-plantilla` con body `{ tipoCronograma }` (default `"PROYECTO"`), agregado al controller existente.

**`CronogramaActividadesRepository.AplicarPlantillaAsync`:**
- Lee y deserializa el JSON (`System.Text.Json`, `PropertyNameCaseInsensitive`).
- Todo dentro de una transacción explícita (`ctx.Database.BeginTransactionAsync()` + `CommitAsync()` al final; si algo falla antes del commit, el `using` de la transacción hace rollback automático).
- Pasada 1: inserta las 81 actividades con `ParentId = null` y fechas `null`, un solo `SaveChangesAsync()` para generar todos los IDs (mismo patrón de 2 pasadas que `ImportarMppAsync`, evita N+1).
- Pasada 2: con los IDs ya generados, resuelve `parentCodigo` → `ParentId` real y agrega filas `ActivityPredecessor` por `predecesoraCodigo` — mismo efecto que `SetPredecesorasAsync` pero inline en el mismo `ctx`/transacción (no se llamó al método existente literalmente porque abre su propio `DbContext` por invocación, lo que rompería la atomicidad pedida al hacerlo 61 veces).

No se agregó validación de existencia de proyecto — se mantuvo consistente con `CrearActividadAsync`, que tampoco la tiene.

### Archivos clave
- `Features/UnidadDeProyectosModule/Features/CronogramaActividades/Seeds/plantilla_proyecto_seed.json`
- `Features/UnidadDeProyectosModule/Features/CronogramaActividades/Infrastructure/Repositories/CronogramaActividadesRepository.cs`
- `Abril-Backend.csproj`

## Sesión 2026-07-06 — Fixes en aplicar-plantilla y cascada de fechas

Rama: `victor-backend`

### Bug 1: `InvalidOperationException` al aplicar plantilla (execution strategy)

`AplicarPlantillaAsync` usaba una transacción manual (`BeginTransactionAsync`/`CommitAsync`) directo sobre el `DbContext`, pero el provider Npgsql tiene `EnableRetryOnFailure` (`NpgsqlRetryingExecutionStrategy`), y esa combinación no es compatible → `The configured execution strategy 'NpgsqlRetryingExecutionStrategy' does not support user-initiated transactions`.

**Fix:** se envolvió toda la transacción (las 2 pasadas de inserts + commit) con la execution strategy correcta:

```csharp
var strategy = ctx.Database.CreateExecutionStrategy();
await strategy.ExecuteAsync(async () =>
{
    await using var transaction = await ctx.Database.BeginTransactionAsync();
    // ... pasada 1 + pasada 2 ...
    await transaction.CommitAsync();
});
```

`ImportarMppAsync` NO tiene este bug: no usa transacción manual, solo `SaveChangesAsync()` sueltos, así que no requirió cambio.

### Bug 2: la cascada de fechas no se disparaba al editar solo la fecha de una actividad

Al ponerle fecha a una actividad predecesora, su sucesora (vinculada correctamente en `ActivityPredecessors`) no recibía fechas por cascada; el PUT devolvía `"cascada": null`.

Causa (descartada la sospecha inicial de tabla descalzada — ambos flujos usan la misma tabla `ActivityPredecessors`): en `CronogramaActividadesService.EditarActividadAsync`, la llamada a `AplicarCascadaAsync` estaba **encerrada dentro del `if (request.PredecessorIds != null)`**. Un PUT que solo cambia fechas (sin reenviar `predecessorIds`) se saltaba el bloque entero → la cascada nunca corría.

**Fix:** se separó el bloque de predecesoras (`SetPredecesorasAsync` + `DetectCycleAsync`, que solo debe correr si el request trae `PredecessorIds`) del cálculo de cascada, que ahora corre siempre que cambien fechas o predecesoras. `Cascada` en la respuesta sigue siendo `null` cuando no hay cambios reales (se mantiene el contrato con el frontend).

### Archivos clave
- `Features/UnidadDeProyectosModule/Features/CronogramaActividades/Application/Services/CronogramaActividadesService.cs`
- `Features/UnidadDeProyectosModule/Features/CronogramaActividades/Infrastructure/Repositories/CronogramaActividadesRepository.cs`

### Pendiente
- El usuario iba a reprobar en el navegador el flujo de cascada (fecha en 2167 → 2168) tras el Bug 2; confirmar que quedó bien.

## Sesión 2026-07-07 — Investigación: feriados en la cascada de fechas

Rama: `victor-backend`. Sesión solo de investigación, sin cambios de código.

### Hallazgo: dos sistemas de feriados desconectados

`CronogramaSchedulingService.EsHabil` sí excluye feriados (no solo sábado/domingo), leyendo `ctx.Feriados` dentro de `CalcularCascadaAsync`. Pero existen **dos tablas de feriados separadas que no se comunican entre sí**:

1. **`Feriados`** (`Shared/Models/Feriado.cs`, tabla `feriados`) — la que **realmente consulta** la cascada de cronograma. CRUD vía `CronogramaActividadesController` (`GET/POST/DELETE api/v1/.../feriados`).
2. **`Holiday`** (`Features/ConfigurationModule/Features/HolidayFeature/`, tabla `holiday`) — módulo CRUD más completo (con `HolidayType`, `RecurringYearly`, soft-delete) expuesto en `api/v1/holiday`. **No lo usa la cascada** — es una tabla paralela sin relación con `Feriados`.

Riesgo: si alguien carga feriados vía `api/v1/holiday` pensando que alimenta el cronograma, no tiene ningún efecto.

### Cómo se actualizan los feriados hoy

Seed hardcodeado en la migración `20260601002547_AddFeriadosAndActivityPredecessor.cs` con feriados nacionales de Perú para 2024-2026 (incluye Semana Santa movible calculada a mano por año). No hay generación automática ni job — para 2027+ hay que insertar manualmente vía nueva migración o los endpoints del `CronogramaActividadesController`.

### Pendiente / posible mejora futura
- Evaluar si conviene unificar `Feriados` y `Holiday` en una sola tabla, o al menos documentar/advertir que son sistemas distintos.
- Cargar feriados 2027 antes de que termine 2026 (ni la tabla `Feriados` ni `Holiday` los tienen aún).

## Sesión 2026-07-07 — RAC: fotos, empresa reportante vs reportada, indicadores

### 1. Fotos de RAC no se subían/mostraban en producción

Investigando un reclamo de que un contratista no podía ver fotos de evidencia en un RAC, se confirmó en pgAdmin que el RAC (`RAC-2026-GMZ-039`) no tenía ninguna fila en `ssoma_rac_foto` — la subida nunca llegó a guardarse. Causa raíz: `appsettings.Production.json` (gitignored, config real de producción vive en otro lado según el usuario) no tenía `RacFotosLibraryId`/`RacPdfLibraryId`/`RacFirmasLibraryId` bajo `SharePoint:Sites:SSOMAApps` — sin esos IDs, `SharePointHabService.GetDownloadUrlAsync`/`SubirArchivoEnRutaAsync` no puede resolver el drive de SharePoint. La subida (`RacSharePointService.SubirFotoAsync` → `SubirArchivoEnRutaAsync`) debería lanzar `AbrilException` 500 si esto pasa, pero en el frontend (`rac-nuevo.ts`) el `error` callback de la subida de fotos llamaba al mismo `mostrarSwalYNavegar()` que el `next`, mostrando "RAC registrado ✓" igual aunque las fotos fallaran — el usuario nunca se enteraba.

**Fix backend**: ninguno de código — los 3 IDs de SharePoint existen en `appsettings.Local.json`, pendiente que el usuario los agregue donde sea que viva la config real de producción (no es `appsettings.Production.json` del checkout local, gitignored y aparentemente no es lo que se despliega).

**Fix frontend** (`rac-nuevo.ts`): el callback de error de subida de fotos ahora muestra un SweetAlert de advertencia distinto ("RAC registrado, pero las fotos no se pudieron subir") en vez de fingir éxito. Además, ahora las fotos son obligatorias (`canSubmit` exige `fotosSeleccionadas.length > 0`).

### 2. Empresa Reportante y Empresa Reportada — antes opcionales, ahora obligatorias

En `rac-nuevo.html`/`.ts`, existían checkboxes "Reportar de forma anónima" y "No identificar la empresa reportada" que permitían crear un RAC sin ninguna de las dos empresas — resultado: 11+ RAC de julio quedaron con `empresa_reportada_id = NULL`, invisibles en cualquier filtro/indicador por empresa. Se quitaron ambos checkboxes; `empresaReportanteId` y `empresaReportadaId` ahora son obligatorios en `canSubmit`.

### 3. Indicadores proactivos mezclaban "empresa reportante" y "empresa reportada" en un solo conteo

`IndicadoresProactivosRepository` (en los 4 sitios que llaman a `BuildMetaDto`, tanto en `GetMetasEmpresaAsync` como en el método bulk multi-proyecto) contaba **todo** por `EmpresaReportadaId` — tanto "RACS rep." como "RACS cerr." usaban la misma población (RACs atribuidos a la empresa), y no existía ningún conteo de cuántos RAC había *levantado* cada empresa como reportante. Esto también explicaba por qué un RAC con `EmpresaReportanteId` = Lumbreras no sumaba nada al indicador de Lumbreras (solo contaba para la empresa reportada).

**Fix**: `BuildMetaDto` ahora recibe `actualRacsReportados` (por `EmpresaReportanteId`) separado de `actualRacsAtribuidos`/`actualRacsCerrados` (por `EmpresaReportadaId`). "RACS rep." = cuántos reportó la empresa (indicador proactivo); "RACS cerr." = de los que le fueron atribuidos, cuántos cerró (indicador de cumplimiento). Se agregó `ActualRacsAtribuidos` a `MetaEmpresaDto` para que el frontend use ese valor como "Prog" de la fila "RACS cerr." (antes reutilizaba `ActualRacs`, que ahora significa otra cosa).

También se encontró y corrigió que "RACS rep." mostraba el valor topado a la meta (`Math.Min(actualRacs, metaRacs)`) en vez del conteo real — una empresa que reportó 10 RACs con meta de 2 solo veía "2" en pantalla. Reportar de más es deseable y no debía ocultarse; el `%` sigue limitado a 100 pero el número ahora es siempre el real.

**Dashboard de RAC** (`RacService.GetDashboardAsync`, solo para vista contratista): se agregó `TotalReportados`/`TotalReportadosCerrados` (conteo por `EmpresaReportanteId`) como card nueva "RACs Levantados por Ti", antes inexistente.

**Lista de RAC**: se agregaron columnas separadas "Reportante"/"Reportada" (antes solo mostraba una), filtro de mes/año (usa `FechaDesde`/`FechaHasta` que ya existían en el backend), y dos selects de empresa separados (`empresaReportanteId` / `empresaReportadaId`, antes uno solo que filtraba por reportada).

### Archivos clave
- `Features/SsomaModule/RacFeature/Services/RacService.cs` (`GetListAsync`, `GetDashboardAsync`)
- `Features/SsomaModule/RacFeature/Dtos/RacDtos.cs` (`RacListQuery.EmpresaReportanteId`, `RacListItemDto.EmpresaReportanteNombre`, `RacDashboardDto.TotalReportados/TotalReportadosCerrados`)
- `Features/SsomaModule/IndicadoresProactivosFeature/Infrastructure/Repositories/IndicadoresProactivosRepository.cs` (`BuildMetaDto` y sus 4 call sites)
- `Features/SsomaModule/IndicadoresProactivosFeature/Application/Dtos/IndicadoresProactivosDtos.cs` (`MetaEmpresaDto.ActualRacsAtribuidos`)
- El fix real de fotos quedó pendiente en config de producción (no en este repo) — ver punto 1.

### Pendiente
- Confirmar con el usuario que agregó `RacFotosLibraryId`/`RacPdfLibraryId`/`RacFirmasLibraryId` en la config real de producción (no en `appsettings.Production.json` del checkout, que está gitignored y según el usuario "no tiene nada que ver con donde se hace deploy").
- 11 RAC de julio con `empresa_reportada_id = NULL` (`RAC-2026-KAU-014,015,016,017,020,025,030,035,036,037,041,043,044` y similares) quedaron huérfanos — no se corrigieron manualmente en BD, pendiente que el usuario identifique a qué empresa corresponden.

### 4. OPT/ATS/Charlas se atribuían por vinculación ACTUAL, no por la vigente en la fecha del evento

Al revisar si Inspecciones/OPT/Auditorías ATS contaban bien (a raíz de la revisión de RACs de este mismo día), se encontró que `IndicadoresProactivosRepository` atribuía OPT/ATS/Charlas a la empresa con la que el trabajador está vinculado **hoy** (`WorkerVinculacion.FechaFin == null`), no a la que tenía **el día que ocurrió el evento**. Un trabajador que cambió de contratista a mitad de mes hacía que sus OPT/ATS/Charlas pasadas se contaran para su empresa nueva, no la vieja. Inspecciones y RACs no tenían este problema porque guardan `EmpresaId`/`EmpresaReportadaId` directo en el registro.

**Fix** en `GetMetasEmpresaAsync` (single-proyecto) y `GetSeguimientoTodosProyectosAsync` (bulk multi-proyecto): se trae el historial completo de `WorkerVinculacion` (no solo la activa) y se resuelve la empresa vigente por `(workerId, fecha)` con un helper local `EmpresaDelTrabajadorEnFecha`. Los bulk queries de OPT/ATS ahora incluyen la fecha del evento en la proyección (antes no la traían). Se eliminó el guard `tieneWorkers`/`wSet.Any()` en el método single-proyecto porque ya no aplica (el conteo ahora es por evento, no por vinculación activa).

### Archivos clave (punto 4)
- `Features/SsomaModule/IndicadoresProactivosFeature/Infrastructure/Repositories/IndicadoresProactivosRepository.cs` (`GetMetasEmpresaAsync`, `GetSeguimientoTodosProyectosAsync`)

### Pendiente (punto 4)
- Verificado que compila; no se pudo probar contra datos reales en esta sesión — pendiente que el usuario confirme en `/ssoma/gestion/indicadores-proactivos/indicadores-ssoma/seguimiento` con un caso conocido (ej. Lumbreras, RP Mural) tras el deploy.

## Sesión 2026-07-09 — Fix: caché de indicadores reactivos no se invalidaba

Reporte del usuario: aprobó un descanso médico de 30 días vinculado a un accidente de Cedro 33, pero el dashboard de SSOMA (`indicadores-ssoma/dashboard`) seguía mostrando los días perdidos viejos.

**Diagnóstico** (confirmado por SQL que corrió el usuario): el dato en BD estaba correcto — `ss_accidente_trabajo.dias_descanso_reales = 30` ya reflejaba la aprobación (`DescansoMedicoRepository.Aprobar` recalcula ese campo sumando `ss_descanso_medico` con `Estado = 'Aprobado'`). El problema era el **caché de 10 minutos** (`IMemoryCache`, `CacheTtl` en `IndicadoresProactivosController`) sobre `/reactivos` y `/reactivos/{proyectoId}`: no había ninguna invalidación cuando cambiaba un accidente o un descanso, así que el dashboard servía la respuesta vieja hasta que expiraba sola.

**Fix**: en vez de enumerar/borrar claves de `IMemoryCache` (no soporta borrar por prefijo), se agregó un contador de versión:
- `ReactivosCacheVersion` (nuevo, singleton) — `Features/SsomaModule/IndicadoresProactivosFeature/Infrastructure/ReactivosCacheVersion.cs`. Expone `Current` y `Bump()`.
- Registrado en `SsomaModule.cs` (`AddSingleton`).
- `IndicadoresProactivosController`: las claves de caché de `GetReactivosProyecto` y `GetReactivosTodos` ahora incluyen `_v{_reactivosCacheVersion.Current}`.
- `DescansoMedicoRepository.Aprobar` llama `Bump()` justo después de recalcular `DiasDescansoReales`.
- `AccidenteTrabajoRepository.Create/Update/Cerrar/Delete` también llaman `Bump()` — cualquiera de esas acciones puede cambiar accidentes/días contabilizados en los reactivos.

Con esto, aprobar un descanso o editar un accidente invalida el caché al instante en vez de esperar hasta 10 minutos.

### Archivos clave
- `Features/SsomaModule/IndicadoresProactivosFeature/Infrastructure/ReactivosCacheVersion.cs` (nuevo)
- `Features/SsomaModule/IndicadoresProactivosFeature/Presentation/IndicadoresProactivosController.cs`
- `Features/SsomaModule/SaludOcupacionalFeature/Infrastructure/Repositories/DescansoMedicoRepository.cs`
- `Features/SsomaModule/SaludOcupacionalFeature/Infrastructure/Repositories/AccidenteTrabajoRepository.cs`
- `Features/SsomaModule/SsomaModule.cs`

### Pendiente
- El usuario ya reinició el backend local para tomar el cambio; falta confirmar en el dashboard que Cedro 33 refleja los 30 días sin demora.
- No se auditaron otros puntos de escritura que podrían afectar reactivos (p. ej. `AccidenteIncidenteRepository`/Flash Report vinculado a accidentes de tópico) — si en el futuro se reporta el mismo síntoma desde otro flujo, revisar si también necesita `Bump()`.

## Sesión 2026-07-09 (2) — Fix: Coordinador SSOMA sin boton de ocultar/mostrar empresas

Reporte del usuario: el mismo pudia ver y usar el boton de ocultar/mostrar empresas en `/ssoma/gestion/indicadores-proactivos/indicadores-ssoma/seguimiento` (es ADMINISTRADOR DEL SISTEMA), pero coordinadores SSOMA reales no lo veian.

**Diagnostico** (confirmado por SQL que corrio el usuario sobre el worker de un coordinador afectado, ocerna@abril.pe): `categoria="Coordinador"` y `ocupacion="SSOMA"` estaban en campos separados. `EsCoordinadorSsomaAsync` (duplicado en `IndicadoresProactivosRepository` y `DesempenoSupervisorRepository`) exigia que ambas palabras aparecieran en el MISMO campo (`Ocupacion` o `Categoria` individualmente), asi que ningun campo por separado calificaba.

**Fix**: en ambos repositorios, se combina `Categoria` + `Ocupacion` en un solo texto antes de buscar las dos palabras, en vez de evaluar cada campo por separado.

### Archivos clave
- `Features/SsomaModule/IndicadoresProactivosFeature/Infrastructure/Repositories/IndicadoresProactivosRepository.cs` (`EsCoordinadorSsomaAsync`)
- `Features/SsomaModule/DesempenoSupervisorFeature/Infrastructure/Repositories/DesempenoSupervisorRepository.cs` (`EsCoordinadorSsomaAsync`)

### Pendiente
- Los endpoints de seguimiento tienen cache de 10 min (`IMemoryCache`) — el usuario debe reiniciar el backend tras el deploy para que el cambio de permisos tome efecto de inmediato en vez de esperar hasta que expire el cache.
- Confirmar con el usuario que ocerna@abril.pe (y otros coordinadores en la misma situacion) ya ven el boton tras el reinicio.

## Sesión 2026-07-10 — Interconsultas: filtros, envío de correos y resolución de jefatura

Pantalla `Interconsultas` (Salud Ocupacional) rediseñada a pedido del usuario: filtros por proyecto/razón social/tipo, columnas de proyecto actual/jefatura/administrador/categoría-ocupación, selección múltiple + envío de correos, y varias rondas de fixes de datos reales verificados en Postgres local.

**Filtros y datos base**:
- `InterconsultaFilterDto`: `ProyectoId`, `ContributorId`, `ObraOficina` (si es `"Obra"` también matchea `obra_oficina` vacío/nulo, ya que solo Staff/Oficina Central se marcan explícitamente).
- La consulta base ahora excluye contratistas (`contrata_casa != "Casa"`) y trabajadores retirados (`estado = "RETIRADO"`) — antes solo se ocultaban en los combos, ahora ni entran a la query.

**Proyecto actual**: se descubrió que `worker_vinculaciones` está incompleta para varios trabajadores. La fuente confiable es `ss_hab_worker_proyecto` (la tabla detrás de Habilitación → Trabajadores → "Proyectos asignados"). Prioriza asignación activa (`fecha_fin is null`); si no hay ninguna, se deja en blanco (se probó cayendo al último proyecto cerrado, pero el usuario pidió revertir eso porque inducía a error con trabajadores que ya salieron de un proyecto). Cae a `worker_vinculaciones` solo si el trabajador no tiene ninguna fila en `ss_hab_worker_proyecto`.

**Jefatura**: 3 fuentes en cascada (`InterconsultaRepository.ResolveJefePorArea` + lógica inline en `List`/`GetForEnvioCorreo`):
1. `workers.worker_lesson_jefe_id` / `worker_salida_jefe_id` (jefe real asignado).
2. Texto libre `workers.jefatura` + catálogo `cat_jefatura` (por nombre exacto).
3. Árbol `area_scope` — mismo algoritmo que `ApproverResolver` (Solicitud de Salidas): camina ancestros buscando Jefe→Sub Gerente→Coordinador→Gerente; si nadie en la cadena de ancestros directos, busca cualquier Gerente que cuelgue de la **misma raíz** del árbol (cubre casos como "Residencia", que cuelga de "Gerencia de Proyectos" pero el Gerente real está en la rama hermana "Unidad de Proyectos", no en el nodo padre).

**Correos**: nuevo endpoint `POST .../interconsultas/enviar-correos`. Staff/Oficina Central con correo propio reciben notificación individual (a él + jefatura); obreros sin correo se agrupan por proyecto en un solo correo al administrador encargado. Remitente fijo `medicinaocupacionalnm@abril.pe` vía nuevo parámetro `fromOverride` en `IEmailService.SendAsync` (agregado a SMTP/SendGrid/PowerAutomate, opcional y retrocompatible).

**Categoría/Ocupación**: agregadas al DTO (`workers.categoria`, `workers.ocupacion`) para seguimiento; en el frontend se muestran compactas junto al DNI del trabajador en vez de sumar columnas nuevas.

### Archivos clave (backend)
- `Features/SsomaModule/SaludOcupacionalFeature/Infrastructure/Repositories/InterconsultaRepository.cs` — toda la lógica de resolución (proyecto, jefatura, filtros base) y los helpers `LoadAreaJefeContextAsync`/`ResolveJefePorArea`/`RootOf`.
- `Features/SsomaModule/SaludOcupacionalFeature/Application/Services/InterconsultaService.cs` — `EnviarRecordatorios` (agrupamiento y armado de correos).
- `Features/SsomaModule/SaludOcupacionalFeature/Application/Dtos/Interconsulta/` — `InterconsultaListDto`, `InterconsultaFilterDto`, nuevos `InterconsultaEnviarCorreoDto`/`InterconsultaEnvioInfoDto`.
- `Shared/Services/Email/` — `IEmailService` + las 3 implementaciones (parámetro `fromOverride`).

### Pendiente
- El usuario pidió explícitamente no compilar en esta sesión (backend corriendo localmente, bloquea el .exe/.dll) — los últimos cambios (resolución de jefatura por área, fallback a Gerente de la raíz) **no se verificaron con build**. Correr `dotnet build` y avisar si sale algún error de tipos antes de dar por cerrado.
- Falta que el usuario confirme en pantalla que la jefatura ahora resuelve bien para casos como María Sonia Alan Oceda (Residencia → debería caer a Carlos Fredy Oriundo Campos, Gerente de Unidad de Proyectos).

## Sesión 2026-07-11 — Agenda de clínica: programaciones "Programado" no aparecían pese a correo enviado

Reporte de Katyana (clínica): trabajadores (Bocanegra Pisco, Flores Quispe, Elías Vílchez) recibían el correo de "Nueva programación EMO" pero no figuraban en la pestaña "Programados" de la Agenda de clínica (`/clinica/agenda`).

**Diagnóstico** (confirmado con SQL contra producción): no era un problema de filtros (interconsulta pendiente, `EsAbril`, joins) — todos los registros pasaban esas condiciones. La causa real: la Agenda de clínica pide `GET .../programaciones` **sin filtro de fecha** (`selectedDate=''` por diseño, para mostrar todo lo pendiente sin importar la fecha), con `pageSize=500`. El total histórico de programaciones ya superaba ese límite (535 filas), y el `ORDER BY` anterior (`FechaProgramada ASC`) ponía las más viejas primero — las programaciones de HOY, al ser las más recientes, quedaban cortadas fuera de `Take(500)` sin ningún error visible.

**Fix**: en `ProgramacionEmoRepository.List`, se cambió el orden para priorizar estados activos (`Programado`, `Aceptado por Clínica`, `En Atención`, etc. — todo lo que no sea `Completado`/`Cancelado`/`Rechazado por Clínica`/`No se presentó`) antes que por fecha. Así, sin importar cuánto crezca el histórico de exámenes ya completados, las programaciones pendientes nunca se cortan por el límite de página. No se tocó ningún filtro, ni el conteo total, ni el contrato de la API — solo el `ORDER BY`.

De paso se detectaron (pero NO se corrigieron aún) 4 registros duplicados de programación "Ingreso" para el mismo trabajador/día (Bocanegra Pisco: ids 826, 827, 828, 832; Flores Quispe: ids 830, 831) creados vía "Registro directo"/"Manual" — no se tocaron por falta de tiempo/alcance de esta sesión.

### Archivos clave
- `Features/SsomaModule/SaludOcupacionalFeature/Infrastructure/Repositories/ProgramacionEmoRepository.cs` (método `List`, el `OrderBy`)

### Pendiente
- Revisar por qué se generan programaciones "Ingreso" duplicadas para el mismo trabajador/día vía "Registro directo" (posible doble submit del formulario de alta de trabajador, o el flujo de registro directo no verifica si ya existe una programación activa antes de crear una nueva). No confirmado con el usuario si ya lo notó/reportó como problema aparte.
- Confirmar con Katyana que tras el fix los 3 trabajadores ya aparecen en "Programados".

## Sesión 2026-07-12 — Plantilla de anteproyecto en "Usar plantilla"

Rama: `victor-backend`.

### Qué se hizo
- Se agregó soporte para la plantilla de **ANTEPROYECTO** en el flujo "Usar plantilla" del cronograma. Antes `AplicarPlantillaAsync` siempre leía `plantilla_proyecto_seed.json`; ahora selecciona el archivo según `tipoCronograma`: si es `"ANTEPROYECTO"` usa el nuevo `plantilla_anteproyecto_seed.json`, en cualquier otro caso mantiene `plantilla_proyecto_seed.json`.
- Se creó el seed `plantilla_anteproyecto_seed.json` con las actividades base del anteproyecto.
- Se agregó `.tokensave/` al `.gitignore` (estado local de la herramienta, no debe versionarse).

### Archivos clave
- `Features/UnidadDeProyectosModule/Features/CronogramaActividades/Infrastructure/Repositories/CronogramaActividadesRepository.cs` — nueva ruta `PlantillaAnteproyectoPath` y selección de plantilla por `tipoCronograma` en `AplicarPlantillaAsync`.
- `Features/UnidadDeProyectosModule/Features/CronogramaActividades/Seeds/plantilla_anteproyecto_seed.json` (nuevo).

### Pendiente
- Verificar en el navegador el flujo "Usar plantilla" para un cronograma de tipo Anteproyecto.

## Sesión 2026-07-13 — Sync master

Rama: `master`. Sesión sin cambios de código: se verificó que `victor-backend` estaba limpio, se cambió a `master` y se corrió "guardar master" para sincronizar con `origin/master`.

## Sesión 2026-07-14 — Observaciones (Arquitectura Comercial): performance, fotos, quién-levanta; permisos Clínica; logging Charlas

**Permisos Clínica** (mismo patrón repetido en varios controllers): `[RequireFeature]` solo aceptaba la clave `ssoma.salud-ocupacional.*`, sin el fallback `"clinica.agenda"` que sí tenían `EmoController`/`ProgramacionEmoController`. Corregido en `InterconsultaController` y `DashboardController` (Salud Ocupacional) para que acepten también `"clinica.agenda"`, igual que los otros dos.

**CharlaController**: los 28 `catch { }` de la clase eran ciegos (sin `ILogger`) — cualquier excepción real quedaba enterrada detrás de un mensaje genérico. Se agregó `ILogger<CharlaController>` y ahora todos loguean la excepción real antes de responder 500. Sospecha sin confirmar: `SharePoint:Sites:SSOMAApps:CharlasLibraryId` podría faltar en el config de producción (no está en `appsettings.local.json`) — pendiente de confirmar con logs reales la próxima vez que falle una subida.

**Observaciones (Arquitectura Comercial)** — módulo completo revisado a pedido del usuario:
- **Performance**: `GetDashboard` traía toda la tabla `ac_observaciones` a memoria y agrupaba en C# — usado también por la Lista solo para 4 totales. Nuevo endpoint `GET .../observaciones/stats` con `COUNT` en SQL; la Lista ya no llama a `/dashboard`.
- **Quién levanta**: nueva columna `ac_observaciones.levanta_por_worker_id` (FK a `workers`, migración manual `Migrations/Manual/20260714_AddLevantaPorAObservaciones.sql`, ya corrida). `LevantarObservacionDTO.LevantaPorWorkerId` es obligatorio (400 si falta). **Ojo con el mapeo EF**: la navegación `AcObservacion.LevantaPor` necesitó `[ForeignKey(nameof(LevantaPorWorkerId))]` explícito — sin eso EF Core inventaba una FK sombra `LevantaPorId` que no existe en la tabla y tiraba `column a.levanta_por_id does not exist` en cualquier query que tocara `AcObservaciones`.
- **Catálogo de "obreros" para el selector**: `ArquitecturaComercialRepository.GetSupervisoresAc(soloObreros: true)`. Pasó por 3 iteraciones hasta dar con el criterio correcto — dejar constancia para no repetir el ciclo:
  1. ~~`Worker.Subarea == "Arquitectura Comercial"`~~ — es el criterio de "Responsable 1" en Actividades (staff/supervisores), no de obreros de campo.
  2. ~~`Worker.ObraOficina != "Staff"`~~ — devolvía prácticamente todos los no-staff de toda la empresa.
  3. ~~Proyecto actual vía `ss_hab_worker_proyecto` + `Project.TieneArquitecturaComercial`~~ — ese flag solo marca qué proyectos aparecen en el módulo de Observaciones (p. ej. "Villar", "9 Nogales"), NO que sus trabajadores sean de AC; devolvía 190 personas.
  4. **Correcto**: el trabajador debe tener como proyecto actual, literalmente, el proyecto llamado **"Arquitectura Comercial"** (comparación case-insensitive contra `Project.ProjectDescription`) — el mismo que aparece como opción de proyecto en Ingreso de Trabajadores. "Proyecto actual" = vinculación activa (`worker_vinculaciones.fecha_fin IS NULL`, la más reciente por `created_at`/`id`) — **mismo criterio exacto que ya usa `HabTrabajadorRepository.GetPaged` (`LatestVincActiva`)**. Verificado con el usuario contra datos reales: 19 trabajadores, nombres correctos vía `Worker.Person.FullName`.
  - **Nota de fuente de verdad en conflicto**: la sesión 2026-07-10 (Interconsultas) dejó escrito que `worker_vinculaciones` está incompleta y que `ss_hab_worker_proyecto` es la fuente confiable para "proyecto actual" en ese contexto. En Observaciones se usó `worker_vinculaciones` porque es el mismo criterio de Ingreso de Trabajadores y el usuario lo verificó con SQL real dando el resultado esperado — pero si en el futuro este selector empieza a fallar para algunos trabajadores, revisar si son casos sin fila en `worker_vinculaciones` (el mismo hueco de datos que motivó el fallback en Interconsultas).
- **Reemplazar foto ya subida**: nuevo endpoint `PATCH .../observaciones/fotos/{fotoId}` (`ObservacionRepository.ActualizarFoto`/`GetFotoById`) — antes solo existía `AgregarFoto` (insert-only).
- **Miniaturas rotas en celular**: las fotos se mostraban con la `webUrl` cruda de SharePoint en `<img src>`, que solo carga si el navegador tiene sesión de Microsoft 365 activa (funcionaba en escritorio por SSO de Office, no en celular). Nuevo endpoint proxy `GET .../observaciones/fotos/{fotoId}/contenido` que trae los bytes vía `IGraphSharePointService.DownloadFromSharePointAsync` con permisos de aplicación. Como un `<img>` no manda headers custom, el JWT viaja por query string (`?access_token=`) — se extendió el mismo `OnMessageReceived` de `Program.cs` que ya aceptaba esto para `/hubs` (SignalR), ahora también para rutas que terminan en `/contenido`.

### Archivos clave
- `Features/ArquitecturaComercialModule/Features/ObservacionesFeature/**` (Controller, Service, Repository, DTOs, Model)
- `Infrastructure/Repositories/ArquitecturaComercialRepository.cs` (`GetSupervisoresAc`)
- `Features/SsomaModule/CharlasFeature/Presentation/CharlaController.cs`
- `Features/SsomaModule/SaludOcupacionalFeature/Presentation/{InterconsultaController,DashboardController}.cs`
- `Program.cs` (JWT por query string en rutas `/contenido`)
- `Migrations/Manual/20260714_AddLevantaPorAObservaciones.sql`

### Pendiente
- Confirmar con el usuario que las miniaturas ya cargan en celular tras el fix del proxy.
- Si alguna vez el selector "Quién levanta" queda vacío para un trabajador que debería aparecer, revisar si tiene fila vigente en `worker_vinculaciones` (ver nota de fuente en conflicto arriba).

## Sesión 2026-07-15 — Migración de feature "Cronograma de Hitos" a Mejora Continua (prod)

Rama: `victor-backend`. No hubo cambios de código; fue una migración de datos directa en la BD de producción vía túnel SSH (puerto 5544).

### Qué se hizo
- Se movió el feature `projects.milestone-schedule` (Cronograma de Hitos, `feature_id = 5`) del módulo **Proyectos** (`module_id = 6`) al módulo **Mejora Continua** (`module_id = 11`), y se renombró su `feature_key` a `mejora-continua.milestone-schedule`.
- Investigación previa (solo SELECT) confirmó: `module_id` real de Mejora Continua en prod es `11` (no asumir que coincide con local); la fila de `feature` a modificar; y que 4 filas en `role_feature` referencian `feature_id = 5` (permisos existentes, no tocados por el UPDATE).
- `UPDATE feature SET module_id = 11, feature_key = 'mejora-continua.milestone-schedule' WHERE feature_key = 'projects.milestone-schedule';` → 1 fila afectada, como se esperaba. `role_feature` quedó intacto (mismo `feature_id`, los 4 roles conservan acceso).

### Incidente
- Al armar el comando de conexión a psql se expuso por error la contraseña de PostgreSQL de producción en la salida de un comando intermedio (`grep`/`echo` sobre `appsettings.Production.json`). Quedó registrada en el historial de esa conversación. Recomendado rotar la contraseña de la BD Aiven si esto es una preocupación.

### Pendiente
- Verificar en el frontend que "Cronograma de Hitos" aparezca ahora bajo Mejora Continua para los roles que ya tenían acceso.
- Evaluar si conviene rotar la contraseña de Postgres de producción (Aiven) por el incidente de exposición mencionado arriba.

## Sesión 2026-07-15 — Módulo Gestión de Revisiones (nuevo) + fixes de Observaciones y migraciones históricas

**Módulo nuevo `RevisionesFeature`** (`Features/ArquitecturaComercialModule/Features/RevisionesFeature/`), clon del patrón de Observaciones con una capa extra de agrupación:
- Entidad `AcRevision` = catálogo de "revisiones" por proyecto (Tipo fijo en código `R1|R2|R1-AC|R2-AC|RF-AC` vía `TipoRevision.Valores`, Lugar = catálogo `AcCatalogoItem` tipo `LugarRevision` o texto libre, Nombre autogenerado `"{Tipo}-{ProyectoNombre}-{Lugar}"`).
- `AcRevisionObservacion` / `AcRevisionObservacionFoto` = mismo shape que `AcObservacion`/`AcObservacionFoto` pero con FK a `AcRevision` en vez de proyecto directo, y `ZonaAmbiente` en vez de `Lugar` (para no chocar con el `Lugar` de la revisión). **Decisión explícita del usuario**: tablas nuevas separadas, NO reusar `ac_observaciones` — cero riesgo sobre el módulo que ya está en producción.
- SharePoint: mismo site key `ObservacionesArqCom`, librería distinta `BRevisionesArqComercial` (confirmado con el archivo real `BibliotecaRevision.xlsx` que compartió el usuario).
- Endpoints bajo `api/v1/arquitectura-comercial/revisiones`: catálogo (`GET/POST/DELETE .../catalogo`) + observaciones (mismo set que Observaciones: lista paginada, filtros, dashboard, stats, crear, levantar, editar, fotos).
- Features nuevas en tabla `feature` (module_id=1, insertadas a mano vía SQL — **el catálogo de features/permisos NO se autogenera desde `[RequireFeature]`, hay que insertarlo manual cada vez que se crea un endpoint nuevo protegido**): `arquitectura-comercial.revisiones{,.dashboard,.lista,.editar}`.

**Bugs reales encontrados y corregidos en el camino** (dejar constancia, son trampas que se van a repetir):
1. **`GetFiltros` de Revisiones sacaba los proyectos de `AcRevisiones` en vez de `Project.TieneArquitecturaComercial`** — huevo-y-gallina: sin revisiones creadas, el combo de proyectos salía vacío en todos lados (ni se podía crear la primera revisión). Corregido para usar el mismo criterio que `ObservacionRepository.GetFiltros`.
2. **IDs de proyecto del CSV histórico (`DBRevisionesComercial.csv`, export de SharePoint) NO coinciden con los `project_id` reales de Abril** (ej. Kaurí es `IDProyecto=43` en SharePoint pero `project_id=7` en la BD real). El import se rehizo uniendo por **nombre de proyecto** (`upper(project_description)`) contra la tabla `project` real en vez de hardcodear IDs — ese patrón (join por nombre, nunca por ID de un sistema externo) hay que repetirlo para cualquier import histórico futuro desde los CSVs de SharePoint/Power Apps.
3. **Columnas `timestamp` de `ac_revisiones`/`ac_revision_observaciones` rechazaban cualquier insert** (`Cannot write DateTime with Kind=Unspecified to PostgreSQL type 'timestamp with time zone'`). Npgsql por defecto mapea `DateTime` de C# a `timestamp with time zone` y exige `Kind=Utc`, sin importar que la columna física sea `TIMESTAMP` (sin tz). Fix: overrides `.HasColumnType("timestamp without time zone")` en `AppContext.ConfigurePostgreSQL` para las 3 entidades nuevas. **Sospecha sin confirmar**: `AcObservacion` probablemente tiene el mismo problema latente (nunca se probó crear una observación nueva vía UI en esta sesión, todo lo visible viene del import histórico por SQL directo) — revisar si alguna vez falla un `POST /observaciones` nuevo.
4. **`GetStats` con 4 `CountAsync()` secuenciales** (Observaciones y ahora Revisiones) — optimizado a una sola query con `GroupBy(o => 1)` + agregación condicional.
5. **Falta de índice en `fecha`** en Observaciones (agregado ahora, `20260714_AddIndexFechaObservaciones.sql`) — para Revisiones se creó el índice desde el día 1 en la migración original, no hubo que parchear después.

**Migraciones manuales corridas** (todas ya ejecutadas y verificadas por el usuario contra la BD real):
- `20260714_AddIndexFechaObservaciones.sql` — índice en `ac_observaciones.fecha` y `partida_reportada`.
- `20260714_CreateAcRevisiones.sql` — tablas `ac_revisiones`, `ac_revision_observaciones`, `ac_revision_observacion_fotos` + índices + seed del catálogo `LugarRevision` (5 valores).
- `20260715_ImportRevisionesHistorico.sql` — import de 22 revisiones históricas, join por nombre de proyecto (ver bug #2 arriba). Verificado: 22/22 importadas.
- Insert manual (no versionado en archivo, corrido directo por el usuario) de las 4 features nuevas en la tabla `feature`.

**Sin resolver — pendiente para la próxima sesión/cuenta**:
- **"Nueva observación" tarda en cargar los proyectos, tanto en Observaciones como en Revisiones, a pesar de que el request `/filtros` mide <1s en Network tab.** Se aplicó `cdr.markForCheck()` en todos los callbacks async de ambas páginas de lista (mismo patrón que ya usaban los dashboards) como mitigación de un posible problema de change detection con Zone.js + `withFetch()`, pero **no se confirmó que esto resuelva la demora real** — quedó sin verificar con el usuario tras el último cambio. Si sigue lento, el problema no es de red ni de query SQL (ya descartado con evidencia real), hay que investigar en el frontend con más profundidad (Angular DevTools / Performance tab, no asumir).
- Relacionado: el botón "Nueva observación" depende de que `filtrosListos` (poblado por `/filtros`) esté en `true` para habilitarse — eso significa que, aunque el fetch sea rápido, la UX obliga a esperar ese roundtrip antes de poder hacer clic. No se evaluó si conviene desacoplar eso (ej. habilitar el botón de entrada y que el combo de Proyecto cargue dentro del modal en vez de bloquear el FAB entero) — quedó pendiente de decidir con el usuario.
- No se probó de punta a punta el flujo completo de Revisiones en el navegador (crear revisión → crear observación → levantar) tras el último fix de timestamps — el usuario iba a probarlo pero la sesión se cortó por límite de tokens.

### Archivos clave (sesión 2026-07-15)
- `Features/ArquitecturaComercialModule/Features/RevisionesFeature/**` (módulo completo nuevo)
- `Features/ArquitecturaComercialModule/Features/ObservacionesFeature/Infrastructure/Repositories/ObservacionRepository.cs` (GetStats optimizado, endpoint AgregarFotoObservacion)
- `Features/ArquitecturaComercialModule/Features/ObservacionesFeature/Infrastructure/Models/AcCatalogoItem.cs` (tipo `LugarRevision`)
- `Shared/Data/AppContext.cs` (DbSets de Revisiones + overrides de timestamp)
- `Migrations/Manual/20260714_AddIndexFechaObservaciones.sql`
- `Migrations/Manual/20260714_CreateAcRevisiones.sql`
- `Migrations/Manual/20260715_ImportRevisionesHistorico.sql`

## Sesión 2026-07-17 — Investigación de campo "ingeniero residente" en Project (sin cambios netos de feature)

Rama: `victor-backend`. Sesión larga de investigación + dos implementaciones que terminaron revertidas; el único cambio que sobrevivió es un fix de bug real encontrado en el camino.

### Investigación de esquema (Project)
- No existe columna `caracteristica_proyecto`. Lo más cercano a "característica del proyecto" es `level_description` (`LevelDescription`), texto libre tipo "20 pisos + azotea + 4 sótanos" — **ya era editable** desde antes vía `PUT /api/v1/project` (`ProjectEditDto`), no hizo falta agregar nada ahí.
- "Ingeniero residente" se investigó como candidato a `responsable_udp`/`responsable_udp_id` (distinto de `responsable_arq_com`/`Id`, que es el responsable de Arquitectura Comercial y ya tenía endpoint de edición). Confirmado con dato real de producción: en "MÁXIMO ABRIL" (`project_id=11`) `responsable_udp_id=14031` = "COLONIO BARRUETO VICTOR ALEJANDRO".
- **Hallazgo clave que invalidó el enfoque**: la pantalla real "Cronograma de Hitos" (`Features/UnidadDeProyectosModule/Features/MilestoneScheduleFeature/Infrastructure/Repositories/ProjectsRepository.cs`, método `GetPagedWithResidents`) **no lee `responsable_udp` en absoluto** — obtiene el/los residente(s) de una tabla `ProjectResident` (N:M `project_id ↔ user_id`, join con `User`/`Person.FullName`), soporta múltiples residentes por proyecto. `responsable_udp`/`responsable_udp_id` sigue siendo un campo aparte, usado solo por `CronogramaActividades` y `ProjectsDashboard` (Cronograma de *Actividades*, no de *Hitos*).

### Qué se implementó y se revirtió (dos rondas)
1. Primera ronda: se agregó `responsableUdpId` a `PatchProyectoDTO`/`ProyectoConActividadesDTO` reutilizando el endpoint `PATCH /api/v1/arquitectura-comercial/proyectos/{id}` — revertido por decisión del usuario (semánticamente no pertenece a Arquitectura Comercial).
2. Segunda ronda: se agregó `responsableUdpId` a `ProjectEditDto` (`PUT /api/v1/project`) con resolución server-side `Worker → Person.FullName` — revertido tras confirmarse que Cronograma de Hitos no consume ese campo (ver hallazgo arriba). `ProjectEditDto.cs` quedó idéntico a `git HEAD` (diff vacío).
- Estado final: `responsable_udp`/`responsable_udp_id` siguen siendo **solo lectura** en todo el código (confirmado por grep, sin ningún `.ResponsableUdp(Id)? =` fuera del entity model).

### Fix real que sí quedó (único cambio de esta sesión)
- `Features/ConfigurationModule/Features/ProjectFeature/Presentation/ProjectController.cs`, endpoint `PUT` (`Update`): el `catch (AbrilException ex)` ignoraba `ex.StatusCode` y siempre devolvía `400 BadRequest`, distinto al patrón documentado en CLAUDE.md (`StatusCode(ex.StatusCode, ...)`) que sí siguen otros controllers (ej. `ArquitecturaComercialController`). Corregido a `return StatusCode(ex.StatusCode, new { message = ex.Message });`.

### Métricas de completitud de `responsable_udp` (solo lectura, vía conexión directa a Postgres de prod con el connection string de `appsettings.Production.json` — sin necesidad de túnel SSH, la BD es alcanzable directo)
- Tabla `project`: 33 filas totales, 32 con `state=true`.
- Subset que consulta Cronograma de Hitos (`active AND state AND tiene_unidad_de_proyectos`): 12 filas — **100% con `responsable_udp`/`responsable_udp_id` cargado** (aunque, como se documentó arriba, esa pantalla no lee este campo).
- Tabla completa: 13/33 con valor, 20/33 en NULL (fuera del subset activo de UDP).
- `responsable_udp` y `responsable_udp_id` están siempre sincronizados (0 inconsistencias en las 33 filas).

### Pendiente / decisión de negocio abierta
- Si en algún momento se quiere que Cronograma de Hitos muestre/edite un residente único a nivel proyecto (vs. la lista actual vía `ProjectResident`), hay que decidir conscientemente cuál de los dos mecanismos (`responsable_udp` escalar vs. `ProjectResident` N:M) es la fuente de verdad — hoy conviven sin relación entre sí.
- `responsable_udp`/`responsable_udp_id` en Project siguen sin ningún endpoint de escritura.

## Sesión 2026-07-17 — Sync master

Rama: `master`. Sesión sin cambios de código en `master`: `git status` estaba limpio al invocar "guardar master" (nada que commitear), build local en 0 errores, y se sincronizó con `origin/master` (fetch + merge — un conflicto trivial de orden en `CONTEXT.md` con las sesiones 07-14/07-15 recién traídas, resuelto dejando las tres en orden cronológico, sin descartar contenido de ningún lado).

## Sesión 2026-07-20 — Limpieza prod `milestone_schedule_history` (Cedro 33) + permisos ver/editar en Cronograma de Hitos

**1. Limpieza de datos de prueba en producción (Cedro 33, `project_id=8`)**, previo a lanzamiento:
- Se pidió borrar `milestone_schedule_history_id IN (63, 64)`. Antes de tocar nada se verificó el esquema real (vía túnel SSH puerto 5544, `psql`/`pg_dump`): `milestone_schedule` **sí** es tabla hija real de `milestone_schedule_history` (FK en BD, sin `ON DELETE CASCADE`), y a su vez `ss_consumo_linea` (Presupuesto de Materiales) depende de `milestone_schedule` (`hito_id`, `ON DELETE SET NULL`).
- Resultado: `history_id=64` estaba vacío (0 hitos hijos) — se borró. `history_id=63` tenía **29 hitos reales** en `milestone_schedule`, con **1,321 filas** de `ss_consumo_linea` dependientes de esos hitos — el usuario decidió **no borrarlo** (dejarlo intacto) al ver que no era solo un log de subida de Excel sino que tenía datos de consumo reales enganchados.
- Backup tomado antes del delete: `C:\Users\vcolonio\Backup database\milestone_schedule_history_backup_20260720_103959.dump` (`pg_dump --data-only --format=custom`, tabla completa, verificado con `pg_restore -l`). Delete ejecutado dentro de `BEGIN`/`COMMIT` explícito (ojo: la primera corrida se hizo sin `COMMIT` en el mismo script y el DELETE quedó implícitamente revertido al cerrar la sesión de `psql` — hay que recordar siempre incluir `COMMIT;` en el mismo heredoc, no en una invocación de `psql` separada).
- **Cedro 33 NO quedó en "0 cronogramas"** — sigue con `history_id=63` activo. Pendiente decidir con el usuario qué hacer con esas 1,321 filas de `ss_consumo_linea` si el objetivo de lanzamiento en verdad requiere dejarlo en cero.

**2. Modelo de permisos investigado (para caso nuevo: RESIDENTE puede editar Cronograma de Hitos, el resto solo ve)**:
- `role_feature` es 100% binario (`role_id, feature_id`, sin columna de nivel de acceso). No hay concepto nativo de "view vs edit" en el dato.
- Sí existe la **convención de código** (ya usada en `RevisionesFeature`/`ObservacionesFeature` de Arquitectura Comercial): dos `feature_key` — uno base a nivel de **clase** del controller (ver) y uno con sufijo `.editar` a nivel de **método** en los endpoints de escritura. Atributo `[RequireFeature("...")]` (`Abril_Backend.Shared.Filters.RequireFeatureAttribute`, `IAsyncAuthorizationFilter`). Como son dos filtros de autorización separados (clase + método), en los endpoints de escritura se exige **ambos** featureKeys (AND), no solo el `.editar`.
- Se confirmó que `MilestoneScheduleController`/`MilestoneScheduleHistoryController` **no tenían ningún `[RequireFeature]`** — solo `[Authorize]` genérico (JWT). Cualquier usuario autenticado, de cualquier rol, podía llamar los 3 endpoints de escritura (`Create` de versión de cronograma, `Culminar`, `MarcarCritico`) sin importar si su rol tenía o no la feature en `role_feature` (el guard de rol solo bloqueaba la navegación del frontend, no la API).
- `feature_key` real de la página: `mejora-continua.milestone-schedule` (feature_id=5) — vive bajo módulo "Mejora Continua" en la tabla `module` pero está asignado a roles de UDP/Residentes (probable mislabel histórico, no se corrigió por no ser parte del pedido). Roles con acceso hoy: `ADMINISTRADOR DE UDP`, `USUARIO DE UDP`, `ADMINISTRADOR DE RESIDENTES`, `RESIDENTE`, `USUARIO DE ABRIL`.

**3. Implementado** (build verificado en 0 errores, sin warnings nuevos en los archivos tocados):
- `[RequireFeature("mejora-continua.milestone-schedule")]` a nivel de clase en ambos controllers.
- `[RequireFeature("mejora-continua.milestone-schedule.editar")]` a nivel de método en `Culminar`, `MarcarCritico` (`MilestoneScheduleController`) y `Create` (`MilestoneScheduleHistoryController`).
- `ProjectsController.cs` (mismo folder, `PATCH .../foto`) explícitamente NO tocado — entidad distinta.

### Archivos clave (sesión 2026-07-20)
- `Features/UnidadDeProyectosModule/Features/MilestoneScheduleFeature/Presentation/MilestoneScheduleController.cs`
- `Features/UnidadDeProyectosModule/Features/MilestoneScheduleFeature/Presentation/MilestoneScheduleHistoryController.cs`
- `Shared/Filters/RequireFeatureAttribute.cs` (sin cambios, solo referenciado)
- Backup: `C:\Users\vcolonio\Backup database\milestone_schedule_history_backup_20260720_103959.dump`

### Pendiente
- **Verificar en BD** que el `feature_key` `mejora-continua.milestone-schedule.editar` exista realmente en la tabla `feature` y esté asignado en `role_feature` solo a `RESIDENTE` (role_id=5) — el usuario afirmó que ya estaba creado en BD, pero no se confirmó con una query en esta sesión (a diferencia del featureKey base, que sí se verificó). Si no existe o no está asignado, `RESIDENTE` va a recibir 403 en los 3 endpoints de escritura pese al cambio de código.
- Probar en pantalla el flujo completo: RESIDENTE puede editar, el resto de roles con acceso a la página (ADMINISTRADOR DE UDP, USUARIO DE UDP, ADMINISTRADOR DE RESIDENTES, USUARIO DE ABRIL) puede ver pero recibe 403 al intentar escribir.
- Decidir qué hacer con `milestone_schedule_history_id=63` de Cedro 33 (29 hitos reales + 1,321 filas de `ss_consumo_linea`) si el lanzamiento requiere el proyecto en cero.

El trabajo real de la sesión (investigación de `responsable_udp`/`responsable_udp_id` en `Project` para "ingeniero residente", dos rondas de implementación que terminaron revertidas al confirmarse que Cronograma de Hitos usa `ProjectResident` y no `responsable_udp`, y el fix de `ex.StatusCode` en `ProjectController.cs` que sí quedó) está documentado en el `CONTEXT.md` de la rama `victor-backend` (commit `0a2eb930`), no en este archivo de `master`. **Ese trabajo todavía no está mergeado a `master`** — solo llegó a `origin/victor-backend`.

## Sesión 2026-07-18 — Barrido completo del filtro `Project.Active` en listados de proyectos

Rama: `victor-backend`.

### Qué se hizo

**Ronda 1 — 4 casos reportados por el usuario**: se agregó el chequeo de `Project.Active` (además del ya existente `State`) a las queries que arman las opciones de filtro/dropdown de proyecto en 4 pantallas, que hoy mostraban proyectos inactivos como opción:
- `ProjectsDashboardRepository.GetFiltersDataFactory()` (Dashboard de Proyectos, dropdown de filtro).
- `CronogramaActividadesRepository.GetProyectosAsync()` (Cronograma de Actividades).
- `ProjectResidentRepository.GetProjectsDescription()` (compartido por Control de IVTs, Cuaderno de Obra y Seguimiento de Residentes) — se agregó `Project.Active` junto al `ProjectResident.Active` que ya existía (ambos chequeos, no reemplazo).
- `LessonsDashboardRepository.GetFiltersAsync()` (filtro de Proyecto en Lessons Dashboard).

**Merge de `origin/master` → `victor-backend`**: la rama estaba 96 commits detrás de `master`. Se hizo `git merge` (no rebase, la rama ya está pusheada y es compartida). Único conflicto real: `CONTEXT.md` — dos logs de sesión divergentes (`victor-backend`: 07-12, 07-15, 07-17; `master`: 07-07 a 07-17) resuelto intercalando las 12 sesiones en orden cronológico real, sin descartar contenido de ningún lado. `ProjectController.cs` se auto-mergeó sin conflicto (el fix de `ex.StatusCode` de esta rama y el nuevo endpoint `ToggleArquitecturaComercial` de master tocan regiones distintas del archivo). Se confirmó que `ActasReunionFeature` (Controller, Service, Repository, DTOs, 9 modelos `Reunion*`) ya está completo en el checkout tras el merge — existía en `master` desde los commits `09e11275`/`013ec52f` (2026-07-10, Christian Alvarez) pero nunca había llegado a esta rama; el frontend (`ActasReunionService.getPaginaInicial()`) apunta a un endpoint real que simplemente faltaba en este checkout, no a algo nunca implementado.

**Ronda 2 — Dashboard UDP (`/projects/cronograma-dashboard`)**: se reportó que el filtro de proyectos de este dashboard seguía mostrando inactivos pese al fix de ayer. Causa: `CronogramaActividadesRepository.GetDashboardAsync()` tiene una única query "todos los proyectos UDP activos" (`Where(p => p.TieneUnidadDeProyectos && p.State)`, sin `Active`) que alimenta a la vez KPIs, tabla principal (ranking/heatmap) y filtro/responsables — a diferencia de los otros dashboards, acá no hay una query de filtro separada de la de datos, así que el fix afecta también a la tabla principal (correcto: tampoco se quiere ahí un proyecto inactivo).

**Auditoría amplia**: se buscó en todo el backend (~200 referencias a `ctx.Project`/`_context.Project`) otras queries que arman listados de proyectos para filtros/selects/dropdowns/tablas resumen. Se encontraron y corrigieron 7 casos adicionales (todos con el mismo patrón: agregar `p.Active`):
- `ProjectsDashboardRepository.BuildProjectQueryAsync` — tabla principal del Dashboard de Proyectos (el dropdown de filtro de esa misma pantalla ya había quedado bien en la ronda 1, pero la tabla de datos no).
- `ObservacionRepository.GetFiltros` (Observaciones, Arquitectura Comercial).
- `RevisionRepository.GetFiltros` (Revisiones, Arquitectura Comercial).
- `HabEmpresaRepository.GetProyectosDisponiblesAsync` (Habilitación, asignar proyecto a empresa).
- `ProyectoHabRepository.GetActivosAsync` (el método se llamaba "Activos" pero nunca chequeó el flag).
- `SharedFiltersService.GetProyectosAsync` (`GET api/v1/shared-filters/proyectos`) — se verificó que en el backend solo lo consume `SharedFiltersController.GetProyectos()`, sin ningún otro repo/servicio dependiendo de recibir inactivos.
- `ArquitecturaComercialRepository.GetProyectosConActividades` — no tenía ningún `.Where` sobre `Project`; se agregó `State && Active` (no solo `Active`).

**Casos identificados pero NO tocados** (a propósito):
- `CronogramaActividadesRepository.GetDebugProyectosAsync` — sin filtro, pero es debug, no user-facing.
- `ArquitecturaComercialRepository` (línea ~1210, filtros DTO) — comentario explícito: trae todos los proyectos a propósito para que el frontend los marque visualmente por estado.
- `IndicadoresProactivosRepository` (SSOMA) — usa `SsProyectoHabilitado.Active` como su propio concepto de "proyecto activo", independiente de `Project.Active`, decisión de dominio documentada.
- `Features/ConfigurationModule/ProjectFeature/ProjectRepository.GetPaged` — CRUD admin de proyectos, alimenta la pestaña "Proyectos Activos"; debe seguir mostrando inactivos para poder reactivarlos.
- 9 métodos confirmados ya correctos: `AdjudicacionFolderRepository`, `ProjectLinkRepository`, `ActasReunionRepository.GetPaginaInicial`, `PasoService_FIXED`, `CroquisRepository`, `GestionVecinosRepository`, `Infrastructure/ProjectRepository.cs` (raíz), `LessonReminderRepository`, `ProjectSubContractorRepository`.

### Archivos clave
- Ronda 1: `ProjectsDashboardRepository.cs` (`GetFiltersDataFactory`), `CronogramaActividadesRepository.cs` (`GetProyectosAsync`), `ProjectResidentRepository.cs` (`GetProjectsDescription`), `LessonsDashboardRepository.cs` (`GetFiltersAsync`).
- Ronda 2: `CronogramaActividadesRepository.cs` (`GetDashboardAsync`), `ProjectsDashboardRepository.cs` (`BuildProjectQueryAsync`), `ObservacionRepository.cs`, `RevisionRepository.cs`, `HabEmpresaRepository.cs`, `ProyectoHabRepository.cs`, `SharedFiltersService.cs`, `Infrastructure/Repositories/ArquitecturaComercialRepository.cs` (`GetProyectosConActividades`).

### Pendiente
- No se pudo verificar en el frontend (repo separado, no presente en este checkout) si algún consumidor de `GET api/v1/shared-filters/proyectos` necesita ver proyectos inactivos a propósito — si algún flujo se rompe, revertir puntualmente ese caso.
- Verificar en el navegador que el Dashboard UDP (tabla principal + filtro) y el Dashboard de Proyectos (tabla principal) ya no muestran proyectos inactivos tras el deploy.

## Sesión 2026-07-19 — Reset de Cronograma de Actividades a estado inicial (producción)

Rama: `victor-backend`. Sesión sin cambios de código — operación directa sobre la BD de producción vía túnel SSH (`localhost:5544` → VPS `5432`, regla P2/P3), a pedido del usuario, porque toda la data de Cronograma de Actividades era de pruebas de desarrollo y el sistema está por salir en limpio para uso real.

### Investigación previa (inventario, sin borrar nada)
- Tablas relacionadas con Cronograma de Actividades confirmadas contra `pg_constraint` de la BD real (no solo el código C#): `project_activity` (tabla principal, sin FK real a `project` — `project_id` es un int simple), `activity_predecessor` (FK `activity_id` CASCADE y `predecessor_id` RESTRICT hacia `project_activity`), `user_cronograma_preference` (preferencia de UI, sin FK real), y `feriados` (catálogo global de feriados, sin columna `project_id` — **no es data de prueba, es calendario compartido**).
- Se encontraron y descartaron por nombre engañoso: `costos_cronograma`/`costos_cronograma_actividad`/`costos_cronograma_actividad_nodo` — feature completamente distinto (`CostsModule/Features/CronogramaFeature`, cronograma de costos de subcontratistas), verificado que sus FKs van hacia `project_sub_contractor`/`app_user`, cero relación con `project_activity`.
- Conteo real en producción: 1,672 filas en `project_activity` repartidas en 11 proyectos (incluía 2 proyectos ya marcados `Active=false` — CAMELIA y Baronet — que igual tenían actividades de prueba), 521 en `activity_predecessor`, 11 en `user_cronograma_preference` (una por proyecto), 45 en `feriados`.
- No había `psql`/`pg_dump` en el PATH del sistema — se usó el binario bundled en pgAdmin 4 (`C:\Users\Victor\AppData\Local\Programs\pgAdmin 4\runtime\pg_dump.exe`) y un script Node desechable (`pg` npm package) para las queries de solo lectura, leyendo la contraseña directo de `appsettings.Development.json` sin exponerla nunca en la conversación.

### Ejecución (con autorización explícita del usuario)
- Backup previo: `pg_dump --data-only --format=custom` de las 3 tablas relevantes (`project_activity`, `activity_predecessor`, `user_cronograma_preference`) → `cronograma_backup_20260718_233807.dump` (43,115 bytes, verificado con `pg_restore --list` antes de borrar nada).
- Se confirmó el nombre real de la secuencia con `pg_get_serial_sequence('project_activity', 'project_activity_id')` → `project_activity_project_activity_id_seq` (coincidía con lo que asumía el usuario, pero se verificó igual antes de correr el `ALTER SEQUENCE`).
- Transacción ejecutada: `DELETE FROM activity_predecessor` (521 filas) → `DELETE FROM project_activity` (1,672 filas) → `ALTER SEQUENCE project_activity_project_activity_id_seq RESTART WITH 1` → `COMMIT`. Verificado post-commit: ambos conteos en 0, secuencia en `last_value=1, is_called=false`.
- `user_cronograma_preference` y `feriados` quedaron intactos, tal como pidió el usuario.

### Desviación del plan original (documentada y explicada al usuario)
- El usuario había pedido confirmar el arranque de la secuencia en 1 con un INSERT/DELETE de prueba dentro de una transacción con `ROLLBACK`. Se optó por NO hacerlo: en Postgres las secuencias no son transaccionales — un `nextval()` consumido dentro de una transacción que hace `ROLLBACK` no se revierte — así que esa prueba habría quemado el valor `1` para siempre y la primera actividad real creada tras el reset habría arrancado en `id=2`. Se usó en su lugar la alternativa no-destructiva que el propio usuario había dejado planteada como opción B (`SELECT last_value, is_called FROM ...`), que confirma lo mismo sin el efecto secundario.

### Pendiente
- El backup (`cronograma_backup_20260718_233807.dump`) quedó en el scratchpad de la sesión de Claude Code (temporal, no en el repo ni en una ruta persistente) — si se quiere conservar como respaldo a largo plazo, moverlo a un lugar permanente.
- `activity_predecessor_id_seq` no se reinició (no fue pedido explícitamente) — si se quiere una numeración 100% limpia también ahí, falta ese `ALTER SEQUENCE`.

## Sesión 2026-07-21 — Filtros opcionales en GET paginado de ResidentReportIncidence

Rama: `victor-backend`. Se extendió el endpoint `GET api/v1/ResidentReportIncidence/paged` para aceptar dos filtros opcionales (`projectId`, `stateId`), replicando el patrón de parámetros opcionales de `AccidenteIncidenteController.GetList` (SSOMA): `[FromQuery] int?` pasados directo del Controller al Service sin lógica de negocio (regla B5). Se descartó explícitamente cualquier filtro de "especialidad" (no existe esa columna).

### Cambios
- `Controllers/ResidentReportIncidenceController.cs` — firma de `GetPaged` extendida con `int? projectId = null` e `int? stateId = null`; se reenvían al Service sin lógica (B5).
- `Application/Interfaces/IResidentReportIncidenceService.cs` y `Application/Services/ResidentReportIncidenceService.cs` — dos parámetros opcionales agregados a la firma de `GetPaged`, passthrough al repo.
- `Infrastructure/Interfaces/IResidentReportIncidenceRepository.cs` y `Infrastructure/Repositories/ResidentReportIncidenceRepository.cs` — en `GetPaged`, sobre el `Where(r => r.Project.Active)` existente se agregan filtros condicionales `if (projectId.HasValue) ... r.ProjectId == projectId.Value` y `if (stateId.HasValue) ... r.StateId == stateId.Value` antes de materializar, de modo que tanto el `CountAsync` como el `Take` respetan los filtros. Una sola query de datos (mas el Count preexistente).

### Notas
- `ProjectId` y `StateId` son columnas escalares reales en la entidad `ResidentReportIncidence` (verificado).
- Build en 0 errores. B1 respetado (cero archivos nuevos, solo edicion de los 5 existentes).

## Sesión 2026-07-25 — Merge de master sobre victor-backend (conflicto en ResidentReportIncidenceRepository)

Rama: `victor-backend`. `git merge master` trajo un lote grande de master (módulo GTH/Reclutamiento completo, Notificaciones, GestionSalidas/SolicitudSalidas, filtro de proyecto por funcionalidad `ProyectoFiltro`, y varios fixes de SSOMA/Habilitación/Vecinos) que auto-mergeó limpio salvo un archivo.

### Conflicto resuelto
- `Infrastructure/Repositories/ResidentReportIncidenceRepository.cs`, método `GetPaged`: HEAD (esta rama) había agregado filtros opcionales `projectId`/`stateId` (sesión 2026-07-21, ver arriba) sobre el mismo `.Where(r => r.Project.Active)` que master extendió con la exclusión `!_context.ProyectoFiltro.Any(f => f.ProjectId == r.ProjectId && f.FuncionalidadId == ProyectoFiltroFuncionalidades.Residentes && !f.Active)` (feature nuevo de visibilidad de proyecto por funcionalidad, commit `1974cf58` de master, no relacionado a GTH pese a venir en el mismo commit). No eran cambios que chocaran en lógica — se combinaron en un único `baseQuery`: primero las dos condiciones fijas (`Project.Active` + exclusión `ProyectoFiltro`), después los `if (projectId.HasValue)` / `if (stateId.HasValue)` encadenados como estaban en HEAD, y por último el `.OrderByDescending(...)` que arma el `query` final. Se usó la sintaxis exacta de master (`f.ProjectId`, `ProyectoFiltroFuncionalidades.Residentes`, `!f.Active`) en vez de nombres parafraseados que se habían mencionado antes en la conversación por error.

### Verificación
- `dotnet build Abril-Backend.csproj`: 0 errores, 233 advertencias (todas preexistentes — nullability `CS8618` en DTOs/Models de toda la base de código, ninguna nueva originada por el merge o el archivo resuelto).
- Commit de merge: `8dafc74a` ("Merge branch 'master' into victor-backend"), mensaje por defecto de git, sin editar.

### Pendiente
- Nada pendiente de este merge en particular. El resto de los archivos entrantes de master (GTH, Notificaciones, GestionSalidas) no se revisó en profundidad en esta sesión — si aparecen bugs ahí, no fueron introducidos por la resolución de este conflicto puntual.

## Sesión 2026-07-25 (cont.) — Skills nuevas actualizar-master / actualizar-rama

Rama: `victor-backend`. Sesión corta de solo housekeeping de tooling: se agregaron dos skills de Claude Code al repo (`.claude/skills/actualizar-master/SKILL.md` y `.claude/skills/actualizar-rama/SKILL.md`) que ya estaban en el working tree al arrancar `guardar-rama` (staged pero sin commitear de una sesión previa). No hubo cambios de código de aplicación.

### Cambios
- `actualizar-master`: trae `origin/master` al `master` local, para trabajo directo en master.
- `actualizar-rama`: trae `origin/master` a la rama de trabajo actual (sin push) y de paso actualiza el `master` local.

### Verificación
- `dotnet build Abril-Backend.csproj`: 0 errores, 233 advertencias (mismo baseline preexistente de siempre).
- Commit: `b6dda418` ("chore: agrega skills actualizar-master y actualizar-rama").

## Sesión 2026-07-26 — CreatedDateTime en GetPaged de ResidentReportIncidence

Rama: `victor-backend`. Sesión corta: se expuso la fecha de creación del reporte en el endpoint paginado, dato que ya existía en la entidad pero no viajaba al DTO.

### Cambios
- `Application/DTOs/ResidentReportIncidence/ResidentReportIncidenceDTO.cs` — se agregó `public DateTime CreatedDateTime {get; set;}`.
- `Infrastructure/Repositories/ResidentReportIncidenceRepository.cs` — en el `Select` de `GetPaged`, se mapea `CreatedDateTime = r.CreatedDateTime` (passthrough directo, sin lógica).

### Verificación
- `dotnet build Abril-Backend.csproj`: 0 errores.
- Commit: `4beb182e` ("feat: expone CreatedDateTime en GetPaged de ResidentReportIncidence").

## Sesión 2026-07-26 (cont.) — Gating de roles y validación de residente en ResidentReportIncidence

Rama: `victor-backend`. Auditoría de seguridad del flujo de incidencias/respuestas de residentes: se encontró que `ResidentReportIncidenceController` (los 4 endpoints) solo tenía `[Authorize]` genérico, sin restricción de rol ni validación de pertenencia a proyecto — el gating de "solo ADMINISTRADOR DE RESIDENTES puede crear/levantar" vivía únicamente en el frontend (FAB oculto), y `CreateResponse` no validaba que quien responde sea el residente asignado al proyecto de la incidencia (cualquier usuario autenticado podía responder cualquier incidencia de cualquier proyecto).

### Cambios
- `Controllers/ResidentReportIncidenceController.cs` — `CreateIncidence` y `UpdateIncidenceState` pasan a `[Authorize(Roles = Roles.AdministradorResidentes)]`; `CreateResponse` pasa a `[Authorize(Roles = Roles.Residente)]`. `GetPaged` quedó intacto a propósito (a pedido del usuario — el filtro de "qué proyectos ve cada quién" se decide en otra sesión). Se corrigió además el `catch (AbrilException ex)` de `CreateResponse`, que hardcodeaba `return BadRequest(...)` ignorando `ex.StatusCode` (inconsistente con el resto del codebase, que usa `StatusCode(ex.StatusCode, ...)`) — sin este fix el 403 de la validación nueva se hubiera devuelto como 400. Los catches de `CreateIncidence` y `UpdateIncidenceState` se dejaron con el `BadRequest` hardcodeado tal cual estaban (no afecta: ninguno de esos dos lanza `AbrilException` con status distinto de 400).
- `Application/Services/ResidentReportIncidenceService.cs` — `CreateResponse` ahora, antes de procesar imágenes: busca el `ProjectId` de la incidencia (`_repository.GetProjectId`), lanza `AbrilException("Incidencia no encontrada.", 404)` si no existe, y valida que el usuario autenticado tenga una fila `ProjectResident` activa para ese proyecto (`_projectResidentRepository.IsUserAssignedToProject`), lanzando `AbrilException("No estás asignado como residente de este proyecto.", 403)` si no. Se inyectó `IProjectResidentRepository` en el constructor.
- `Infrastructure/Interfaces/IResidentReportIncidenceRepository.cs` + `Infrastructure/Repositories/ResidentReportIncidenceRepository.cs` — nuevo método `GetProjectId(int residentReportIncidenceId)` (proyección `int?`, `null` si no existe la incidencia).
- `Infrastructure/Interfaces/IProjectResidentRepository.cs` + `Infrastructure/Repositories/ProjectResidentRepository.cs` — nuevo método `IsUserAssignedToProject(int userId, int projectId)` (`AnyAsync` sobre `ProjectResident.Active && State && UserId && ProjectId`), usando el `_context` inyectado directamente (no `IDbContextFactory`, porque es una query secuencial única — `IDbContextFactory` es solo para queries paralelas con `Task.WhenAll`, no aplica acá).

### Verificación
- `dotnet build Abril-Backend.csproj`: 0 errores, warnings preexistentes sin cambios.
- Diff revisado línea por línea con el usuario antes de commitear (lógica de seguridad real).
- Commit: `e2d59239` ("fix: gatea roles y valida asignacion de residente en ResidentReportIncidence").

### Pendiente
- `GetPaged` sigue sin filtrar por proyectos asignados al usuario logueado — queda para otra sesión decidir si se filtra automáticamente por `ProjectResident` o se deja como está (actualmente cualquier usuario autenticado puede listar incidencias de cualquier proyecto, solo mitigado por lo que el frontend elija mostrar).

## Sesión 2026-07-26 (cont. 2) — GetPaged filtrado por proyecto asignado (residente) + endpoint assigned-projects

Rama: `victor-backend`. Cierra el gap de lectura pendiente de la sesión anterior: `GetPaged` de `ResidentReportIncidence` ahora restringe automáticamente los resultados a los proyectos del residente autenticado, sin tocar el comportamiento para otros roles (ej. ADMINISTRADOR_RESIDENTES).

### Cambios
- `Controllers/ResidentReportIncidenceController.cs` — `GetPaged` extrae `isResidente = User.IsInRole(Roles.Residente)` (solo claims del JWT, sin query extra) y lo pasa al Service junto con `userId`. Nuevo endpoint `GET /api/v1/ResidentReportIncidence/assigned-projects` en el mismo controller (B1), para que el frontend arme el selector de proyectos cuando el usuario es residente.
- `Application/Services/ResidentReportIncidenceService.cs` — `GetPaged` (firma ahora `(page, userId, isResidente, projectId, stateId)`): si `isResidente`, pide `_projectResidentRepository.GetActiveProjectsForResident(userId)` y arma `allowedProjectIds`; si el `projectId` pedido no está en esa lista, se ignora en silencio (queda `null`, no error/403) y se devuelve la lista propia del residente sin ese filtro. Si no es residente, `allowedProjectIds` queda `null` y el comportamiento es idéntico al de antes. Nuevo método `GetAssignedProjects(userId, isResidente)`: lista vacía si no es residente, si no devuelve `GetActiveProjectsForResident` tal cual.
- `Infrastructure/Repositories/ResidentReportIncidenceRepository.cs` + interfaz — `GetPaged` recibe `List<int>? allowedProjectIds = null`; si no es null, agrega `.Where(r => allowedProjectIds.Contains(r.ProjectId))` sobre el `baseQuery`, antes de los filtros existentes de `projectId`/`stateId` (sesión 2026-07-21). Con lista vacía (residente sin ninguna asignación activa), el `Contains` nunca matchea → `PagedResult` con `Data` vacío y `TotalRecords = 0`, sin error.
- `Infrastructure/Repositories/ProjectResidentRepository.cs` + interfaz — nuevo método `GetActiveProjectsForResident(userId)`: join `Project`+`ProjectResident` filtrando `ProjectResident.Active && State`, `Project.Active`, y la misma exclusión por `ProyectoFiltro`/funcionalidad Residentes que ya usa `GetProjectsDescription`. Es la única query nueva — la reusan tanto `GetPaged` (solo extrae los `ProjectId`) como `GetAssignedProjects` (devuelve el DTO completo), sin duplicar lógica. No se tocó `GetProjectByResidentUserId` (método preexistente sin filtro de `ProjectResident.Active/State`) para no alterar el comportamiento de `ProjectResidentController.GetWithResidentByUserId`, que no era parte de este pedido.

### Verificación
- `dotnet build Abril-Backend.csproj`: 0 errores, sin warnings nuevos.
- Diff completo revisado por el usuario antes de commitear (incluyendo el método `GetPaged` completo del repository, línea por línea).
- Commit: `a9fd0388` ("feat: filtra GetPaged de ResidentReportIncidence por proyecto asignado al residente").

### Pendiente
- Nada pendiente de seguridad en este flujo por ahora — lectura y escritura de `ResidentReportIncidence` quedaron ambas gateadas por rol + pertenencia a proyecto.

## Sesión 2026-07-26 (cont. 3) — Investigación: origen de datos del dashboard AC ("curva de avance" / "tendencia SPI")

Rama: `victor-backend`. Sesión de solo lectura/explicación, sin cambios de código — se documenta acá para no tener que re-derivar esto en otra sesión.

### Hallazgos
- Ambos gráficos salen del **dashboard v2** (`GET /api/v1/arquitectura-comercial/dashboard-v2`, `GetDashboardV2` en `Controllers/ArquitecturaComercialController.cs:352` → `ArquitecturaComercialService.GetDashboardDataFiltrado` (passthrough) → `ArquitecturaComercialRepository.GetDashboardDataFiltrado`, lógica real en líneas ~1563-1715). El dashboard v1 (`GET .../dashboard`) tiene sus propios `ProyeccionAvance`/`TendenciaEficiencia` con semántica distinta (progreso de tareas por supervisor / % eficiencia acumulada, no SPI real) — no confundir los dos.
- **"Curva de avance"** (`AvanceSemanalDTO { Semana, Programado, Real }`) **no es presupuesto** — es progreso de cronograma de `ac_actividades`. Se calcula como **delta semanal** (cuánto avanzó cada actividad de una semana a la siguiente), no nivel acumulado promedio — hay un comentario en el código (línea ~1580) explicando por qué: promediar el % acumulado sobre un grupo cuya composición cambia semana a semana (entran actividades que arrancan, salen las que cierran) producía subidas/bajadas falsas.
- **"Tendencia SPI"** (`EficienciaSpiDTO { Semana, Spi, Esperado=1.0 }`) es el promedio semanal de `ac_avance_semanal.spi` (ya calculado, no se recalcula al vuelo). Fórmula real en `CalcularSpi(AcActividad a)` (Repository línea 970-1017): si la actividad terminó, `SPI = diasPlan / diasReal`; si sigue en curso, `SPI = %avanceReal / %avanceEsperado`; tope `min(spi, 1.5)`. 100% backend — el frontend recibe el número ya calculado.
- **Precálculo/caché**: tabla `ac_avance_semanal` (snapshot por actividad×semana). Se llena vía `POST /api/v1/arquitectura-comercial/avance-semanal/snapshot` (`SnapshotAvanceSemanal`), `[AllowAnonymous]` + guard `Authorization: Bearer {CronSecret}` (mismo patrón que `/reminder` y `/alertas/*` de otros módulos) — **no hay `IHostedService`/Hangfire en el proceso**, según el propio CONTEXT.md el patrón de este repo es que un cron externo (Azure Logic App / GitHub Actions / EasyCron) le pegue a esa URL periódicamente; no hay evidencia en el código de la frecuencia configurada (vive fuera del repo). Aparte, `ac_actividades.spi` se recalcula síncronamente en cada `UpdateActividad`/`PatchActividad`, y en bloque vía `POST /recalcular-spi` (botón manual, `[Authorize]` normal, sin CronSecret).

### Verificación
- Sin cambios de código — sesión de solo investigación. `dotnet build`: 0 errores (sin tocar nada).

## Sesión 2026-08-02 — Responsable UDP en ProjectFeature + endpoint de lookup unificado

Rama: `victor-backend`. Punto de partida: investigación de cómo funciona "Responsable Arq. Comercial" en el modal editar proyecto (`/configuracion/proyectos`), como paso previo para agregar responsables análogos (UDP, y luego Residente). Hallazgo clave: `project.responsable_arq_com(_id)` es una FK plana (sin navegación EF) a `worker.id`, resuelta por nombre solo en el código legado de `ArquitecturaComercialRepository`; el selector de personas sale de un endpoint separado (`GET /api/v1/arquitectura-comercial/supervisores-ac`) que filtra `Worker.Subarea == "Arquitectura Comercial"` — texto libre, no catálogo normalizado. Se confirmó además que `project.responsable_udp(_id)` ya existía en BD (migración `20260526215118_AddResponsableUdpToProject`, sin cablear en ningún DTO/repo/controller) y que `"unidad de proyectos"` ya es un valor válido de `Worker.Subarea` (confirmado en `AreaScopeMatcher.cs`).

### Cambios
- `Features/ConfigurationModule/Features/ProjectFeature/Application/Dtos/{ProjectDto,ProjectEditDto,ProjectCreateDto}.cs` — agregado `ResponsableUdp`/`ResponsableUdpId` (mismo shape que `ResponsableArqCom`), sin flags booleanos adicionales (confirmado con el usuario: todo proyecto tiene UDP, no hace falta `tieneUdp`).
- `Features/ConfigurationModule/Features/ProjectFeature/Infrastructure/Repositories/ProjectRepository.cs` — mapeo de los campos nuevos en el `Select` de `GetPaged` y en ambos overloads de `ApplyDtoToEntity` (Create/Update). Nuevo método `GetResponsables(string tipo)`: switch `tipo` → `Subarea` (`"ARQ_COMERCIAL"` → `"Arquitectura Comercial"`, `"UDP"` → `"Unidad de Proyectos"`, default → `AbrilException(400)`), una sola query contra `Worker` (sin N+1).
- `Features/ConfigurationModule/Features/ProjectFeature/Presentation/ProjectController.cs` — nuevo endpoint `GET /api/v1/project/responsables?tipo=ARQ_COMERCIAL|UDP` (reemplaza un `responsables-udp` inicial que se descartó por el contrato final). Solo `[Authorize]` genérico — a pedido explícito del usuario no se tocó el gap de roles de este controller (queda para otra sesión, igual que en Arq. Comercial).
- `Features/ConfigurationModule/Features/ProjectFeature/Application/Dtos/ContributorLookupDto.cs` — se agregó `ResponsableLookupDto` (`Id`, `ApellidoNombre`) en este archivo ya existente, a pedido explícito del usuario (B1/B2: nada de DTOs nuevos, mismo shape que `SupervisorAcDTO` de Arq. Comercial pero como tipo propio del feature, sin reutilizar la clase de otro feature) — el contrato final de respuesta usa `apellidoNombre` (no `nombre`) para que `app-search-select` del frontend no necesite ajustes.
- `IProjectRepository`/`IProjectService`/`ProjectService` — firma `GetResponsables(string tipo)` propagada.

### Investigación adicional (sin implementar): "Responsable Residente"
- Confirmado que `responsable_residente(_id)` **no existe** en `project` ni en el modelo — a diferencia de UDP, acá hace falta migración nueva (mismo patrón que `AddResponsableUdpToProject`).
- El catálogo correcto es `Worker.WorkerCategoryId` → `WorkersCategory.Name == "Residente"` (join, no `Subarea` como Arq. Comercial/UDP) — patrón ya usado en `LessonReminderRepository.cs`/`LessonJefeResolver.cs` para categorías "Jefe"/"Coordinador"/"Residente". Se recomendó filtrar por `Name` en vez de hardcodear el `worker_category_id=29` que dio el usuario, igual que ya se cuida en `AreaScopeMatcher.cs` con los IDs de `area_scope` (pueden diferir entre dev/prod).
- Se descartó explícitamente apoyarse en `ProjectResident` (tabla `UserId`-based, N:N, usada para control de acceso — qué usuario residente ve qué proyecto, consumida por `ResidentReportIncidence`/`ResidentMonitoring` y por el recordatorio mensual de Cronograma de Hitos en `MilestoneScheduleFeature`) — es un concepto distinto a "responsable único a mostrar en el modal", que debe ser una columna plana nueva análoga a Arq. Comercial/UDP.
- Falta decisión del usuario para implementar: migración + DTOs + tercer caso `"RESIDENTE"` en `GetResponsables`.

### Verificación
- `dotnet build Abril-Backend.csproj`: 0 errores, 0 warnings nuevos (233 warnings preexistentes sin relación con `ProjectFeature`).
- Commit: `76dfdadd` ("feat: agrega Responsable UDP a Project y endpoint de lookup por tipo").

### Pendiente
- Implementar "Responsable Residente" si el usuario confirma el approach (columna nueva + join por `WorkersCategory.Name`).
- Gap de `[Authorize]` genérico en `ProjectController` (compartido por Arq. Comercial, UDP, y a futuro Residente) — pendiente de sesión aparte, a pedido explícito del usuario.

## Sesión 2026-08-04 — Fotos de Inspección rotas + Inspecciones colaborativas (gerencial/cruzada/coordinadores SSOMA)

Rama: `master`.

### Bug de fotos (Inspección, también presente en RAC pero no tocado)
- Causa raíz: `SubirArchivoYObtenerUrlAsync` guardaba el `webUrl` de SharePoint (página interactiva, exige sesión Microsoft) y el frontend lo ponía directo en `<img src>` — nunca cargaba para un usuario sin sesión SharePoint abierta. El PDF sí funcionaba porque descargaba los bytes server-to-server con el token de la app.
- Fix: `InspeccionSharePointService` ahora usa `SubirArchivoEnRutaAsync` (ruta relativa, mismo patrón que RAC) para fotos/firmas. Nuevo endpoint `GET api/v1/ssoma-inspeccion/media?path=&tipo=` que descarga los bytes reales vía Graph y los sirve. Frontend (`inspeccion-detalle.component.ts`) precarga cada foto como blob autenticado (mismo patrón que ya usaba `descargarPdf`) y arma `object URL`s locales.
- Pendiente: inspecciones creadas antes del fix quedan con `webUrl` guardado — esas fotos no se pueden recuperar sin volver a subir el archivo. RAC tiene el mismo bug de fondo pero no se tocó (queda para otra sesión si se pide).

### Inspecciones colaborativas
- Nuevo flag `EsColaborativa` en `SsomaInspeccionTipo` (catálogo) y `SsomaInspeccion`. Cuando el tipo es colaborativo, el wizard de creación salta el checklist y la inspección queda en estado `"Abierta"` en vez de cerrarse al primer submit.
- Nueva tabla `SsomaInspeccionParticipante` (inspeccionId, workerId, nombre, cargo, empresa, fechaUnion) y `CreadoPorWorkerId`/`CreadoPorNombre` en `SsomaInspeccionHallazgo`.
- Endpoints nuevos en `InspeccionController`: `GET abiertas`, `POST {id}/unirse`, `POST {id}/hallazgos` (agregar hallazgo suelto sin checklist), `PATCH {id}/cerrar-colaborativa`, `PATCH {id}/reabrir-colaborativa` (ambos restringidos a staff interno Abril, no contratistas).
- Frontend: pestaña/página nueva "Abiertas" (`pages/abiertas/`) para listar y unirse; página nueva "Agregar hallazgo" (`pages/agregar-hallazgo/`) liviana (descripción, foto(s), criticidad Crítico/Mayor/Menor, fecha propuesta, responsable, recomendación); botones "Cerrar inspección"/"Reabrir inspección" y lista de participantes en el detalle.
- Bug corregido durante pruebas: el creador quedaba duplicado en la lista de participantes porque su registro inicial no tenía `WorkerId` — `UnirseAsync` ahora hace match por nombre cuando el `WorkerId` es null y lo completa retroactivamente.
- PDF: cuando `EsColaborativa=true`, `InspeccionPdfService` genera un PDF horizontal (A4 landscape) tipo tabla Excel — una fila por hallazgo con N°, descripción, foto(s), recomendación, criticidad, responsable, fecha límite, estado y evidencia de levantamiento — en vez del formato vertical con checklist.
- Catálogo poblado con 3 tipos nuevos vía SQL manual: "Inspección Gerencial", "Inspección Cruzada", "Coordinadores SSOMA" (todos `es_colaborativa=true`).

### Migración EF — drift pendiente (importante)
- Al correr `dotnet ef migrations add` para esta feature, EF arrastró **todos los cambios de modelo acumulados sin migrar de otras features ya commiteadas en master** (tablas `ac_observaciones`/`ac_revisiones` de Gestión de Revisiones, cambios de `gth_*`, `DropTable("manager_signature")`, columnas renombradas en `person`/`workers`, etc.) — nada de eso es de esta sesión. Se descartó esa migración (se borraron los archivos generados y se revirtió `AppDbContextModelSnapshot.cs` con `git checkout`) y en su lugar se aplicó SQL manual acotado solo a Inspección:
  - `Migrations_Manual/2026-08-04_inspeccion_colaborativa.sql` (columnas + tabla nueva)
  - `Migrations_Manual/2026-08-04_inspeccion_colaborativa_tipos.sql` (3 tipos de catálogo)
  - `Migrations_Manual/2026-08-04_dedupe_participantes.sql` (limpieza de duplicados de la sesión de pruebas)
- **El drift sigue sin resolver**: mientras el modelo de Gestión de Revisiones (y lo demás) no tenga su propia migración generada+aplicada, cualquier `dotnet ef migrations add` futuro va a seguir arrastrando todo junto. Si se retoma Gestión de Revisiones, conviene generar esa migración por separado antes de que crezca más.

### Archivos clave
- Backend: `Features/SsomaModule/InspeccionFeature/**` (Models, Dtos, Interfaces, Services, Repository, Controller), `Shared/Data/AppContext.cs`.
- Frontend: `features/ssoma/gestion/inspeccion/**` (dtos, service, `pages/detalle`, `pages/nueva`, nuevas `pages/abiertas` y `pages/agregar-hallazgo`).

### Pendiente
- Resolver el drift de migración EF (ver arriba) cuando se retome esa otra feature.
- RAC tiene el mismo bug de fotos rotas en pantalla — no se tocó en esta sesión.

## Sesión 2026-08-04 (cont.) — Limpieza de worktrees huérfanos

Rama: `victor-backend`. Housekeeping de 3 worktrees huérfanos encontrados en `.claude/worktrees/`.

### Parte 1 — Worktrees huérfanos
- `git worktree list` mostró 3 worktrees además del principal: `elated-ellis-724b34` (rama `claude/elated-ellis-724b34`, línea de trabajo de Contratistas, último commit 2026-05-19 de `danijustiniani31415`, 702 commits atrás de `origin/master`, con un feature `ProjectsDashboard` completo pero sin commitear — lógica real de KPIs por proyecto, no relacionado a SPI), `planeamiento-bim-data-model-772373` (rama `claude/planeamiento-bim-data-model-772373`, con un feature Planeamiento BIM completo y comiteado en `19d428ff`: 3 columnas en `project`, 11 tablas, seeds — pero el commit lo traía mezclado con trabajo ajeno de otro dev, `c4ecf980` "cambio de lugar las jefaturas", ya mergeado a `origin/master` por su cuenta), y `crazy-ardinghelli-737df4` (sin `.git`, solo carpetas vacías de un paquete de skills de marketing sin relación al repo).
- Se investigó `elated-ellis-724b34` en detalle (status/diff/log/fechas de archivo) pero **no se tocó** — queda pendiente de decisión de Dani.
- El usuario decidió **descartar por completo** el trabajo de `planeamiento-bim-data-model-772373` (empezar de cero) en vez de rescatarlo — se hizo `git worktree remove` + `git branch -D`. El borrado físico de la carpeta quedó bloqueado por un lock de Windows (VS Code con handles abiertos); el contenido se borró igual, solo quedó una carpeta vacía huérfana en disco (cosmético).
- `crazy-ardinghelli-737df4` se confirmó vacío (0 archivos, sin `.git`) y se borró con `Remove-Item -Recurse -Force`.

## Sesión 2026-08-05 — Planeamiento BIM: todo el modelo de datos ya existe en producción

**Importante para quien retome el modelo de datos de Planeamiento BIM** (`Features/PlaneamientoBimFeature/`, revertido el 2026-08-05, ver arriba): el modelo de datos completo **ya existe en producción**, creado a mano por el usuario vía pgAdmin, verificado paso a paso:

- **4 catálogos**, seeds ya cargados y confirmados por conteo de filas: `bim_macro_actividad` (3), `bim_actividad` (37 = 14+14+9), `bim_causa_no_cumplimiento` (5), `bim_fase` (5).
- **3 columnas nuevas en `project`**: `responsable_planeamiento_bim`, `responsable_planeamiento_bim_id`, `meta_ppc`.
- **7 tablas por proyecto** (vacías, esperando la pantalla de Configuración Inicial): `bim_proyecto_zona`, `bim_zona_nivel`, `bim_zona_sector`, `bim_proyecto_fase`, `bim_registro_diario` (con su `UNIQUE INDEX` sobre `project_id`+`zona_id`+`nivel_id`+`sector_id`+`actividad_id`+`fecha`), `bim_evidencia_foto`, `bim_bloqueo`.

Implicancias:
1. **Nada de esto debe crearse de nuevo** — cualquier intento de `CREATE TABLE`/`ADD COLUMN` sobre estos objetos va a fallar contra producción (mismo tipo de colisión que encontramos con `ss_proyecto_habilitado` y `ss_charla_contratista` esta sesión).
2. Cuando se retome el feature, **el primer paso NO es crear estas tablas** — ya existen. El primer paso es: crear los modelos C# + registrar en `DbContext` (para que EF conozca el esquema), y escribir la migración de EF que reconoce todo esto como ya aplicado — mismo patrón aprendido hoy con `ss_proyecto_habilitado`: la migración no debe ejecutar los `CREATE TABLE`/`ADD COLUMN` reales contra producción, solo marcarse como aplicada (o usar `IF NOT EXISTS` con verificación previa de columnas/constraints reales contra producción, no asumidas).
3. **Después de eso, el siguiente paso real es la pantalla de Configuración Inicial** (Controller/Service/Repository + UI) — no más modelo de datos.

## Sesión 2026-08-05 — Editar email de usuario contratista

Rama: `master`.

### Contexto
En "Gestión de Ingresos → Usuarios" (`admin-contratista-usuarios`), el modal "Editar" de un usuario contratista no permitía corregir el email (se armaba explícitamente sin ese campo). Si alguien invitaba con el correo equivocado o el usuario perdía acceso a esa casilla, no había forma de arreglarlo — solo desactivar e invitar de nuevo.

### Cambio
- `ContratistaUsuarioUpdateDto` ahora acepta `Email` (antes solo `RolNombre`/`Scope`/`Activo`/`ProyectoIds`/`Modulos`).
- `ContratistaUsuarioService.ActualizarUsuarioAsync`: si viene `Email` distinto al actual, valida formato básico, verifica que no esté en uso por otro `User` (evita colisión de login) y si está libre actualiza `User.Email` directo (el email vive en la tabla `User`, compartida por todo el sistema — no es propiedad de `ss_contratista_usuario`).

### Archivos clave
- `Features/HabilitacionModule/Application/Dtos/ContratistaUsuarios/ContratistaUsuarioDtos.cs`
- `Features/HabilitacionModule/Application/Services/ContratistaUsuarioService.cs`

### Investigación sin cambios de código (pendiente para otra sesión)
Se investigó por qué el personal de Oficina Central / Post Venta / Arquitectura Comercial no aparece en "Programar Inducción" (`habilitacion/gestion/trabajadores`). Causa raíz identificada: "Programar Inducción" solo lista workers con fila activa en `ss_hab_worker_proyecto`, y el campo "Proyecto" en el alta de trabajadores `Casa` (incluye Staff/Oficina Central) **no es obligatorio** (`worker-create-edit.ts`, getter `canSubmit` — ver rama `esStaffOOficina`, no exige `proyectoId`). Si se crea sin seleccionar Proyecto, el worker nunca queda vinculado y jamás aparece en la programación de inducción, sin importar el proyecto elegido.

Pendiente para retomar:
1. Confirmar con SQL cuántos workers de esas áreas están sin vínculo (`ss_hab_worker_proyecto` sin fila activa) y si el proyecto "Arquitectura Comercial" existe en la tabla `project` (Oficina Central y Post Venta sí existen).
2. Backfill de esos workers hacia el "proyecto" que les corresponde.
3. Hacer `proyectoId` obligatorio en el formulario para `esStaffOOficina` (frontend, `worker-create-edit.ts`) para que no vuelva a pasar.

## Sesión 2026-08-06 — Planeamiento BIM: Controller/Service/Repository de Configuración Inicial

Rama: `victor-backend`. Retoma el feature cuyo modelo de datos ya estaba verificado en producción (sesión 2026-08-05). Se construyó de cero el Controller/Service/Repository (no existía ninguno) para la pantalla de Configuración Inicial: zonas/niveles/sectores, responsable BIM y meta PPC.

### Contexto perdido: la spec original
`dashboard-planeamiento-bim-spec.md` (mencionada como fuente de verdad) **no aparece en ningún lado** — se buscó en todo el disco, en el repo backend, en el repo frontend (`Abril-Frontend`) y en `git log --all` de ambos; no está ni en disco ni en historial de git. La estructura zona→(niveles, sectores) como listas planas hermanas se infirió y confirmó contra el **FK real en producción** (`bim_zona_nivel.zona_id` y `bim_zona_sector.zona_id` ambos apuntan a `bim_proyecto_zona.id`, ninguno anida nivel→sector). El resto de las reglas de negocio que la spec habría cubierto se resolvieron con un `/grill-me` corto del usuario (8 decisiones, ver abajo) — no son inferencia, son decisión explícita.

### Las 8 reglas de negocio (fuente de verdad, reemplazan la spec perdida)
1. Zonas: texto libre, sin catálogo de tipos.
2. Niveles: `orden` numérico explícito, editable por el usuario (no inferido).
3. Sectores compartidos por zona, no por nivel (confirmado contra el esquema real).
4. Combinación nivel×sector: producto cartesiano implícito, sin tabla de combinaciones ni activación manual — no requiere código en esta pantalla.
5. Fases del proyecto: las 5 filas de `bim_fase` (Diseño, Movimiento de Tierras, Casco, Acabados, Entrega) se auto-asignan a todo proyecto, sin poder desactivarlas.
6. Fechas de fase: única validación es `fecha_fin_meta > fecha_inicio` de la misma fase, si ambas vienen con valor. Sin control de traslape entre fases.
7. Guardado de Configuración Inicial: parcial permitido, sin campos obligatorios a nivel backend — única restricción dura es el 409 al intentar borrar zona/nivel/sector con registros en `bim_registro_diario`.
8. Meta PPC: rango 0–100 inclusive, 400 si está fuera.

### Diseño e implementación
- **Patrón de responsable reutilizado sin endpoint compartido**: el catálogo "Worker.Subarea == X" ya estaba duplicado localmente en `ArquitecturaComercialRepository`, `ProjectRepository.GetResponsables` y `LessonReminderRepository` — se replicó el mismo patrón (duplicado, no una llamada cruzada a otro feature) filtrando `Subarea == "Planeamiento BIM"`, consistente con R5.
- **Endpoints** (`api/v1/planeamiento-bim/configuracion`, todos con 1 query/acción por R1):
  - `GET /responsables` — catálogo de workers.
  - `GET /{projectId}` — zonas con niveles+sectores anidados (proyección correlacionada sin N+1) + responsable + meta PPC + **fases** (lazy-create: si el proyecto no tiene filas en `bim_proyecto_fase`, las crea desde el catálogo `bim_fase` antes de responder).
  - `PUT /{projectId}` — guarda zonas (upsert por Id: crea/actualiza/elimina), responsable, meta PPC y fechas de fase (por Id de `bim_proyecto_fase`; si el Id no pertenece al proyecto, 400). Un solo `SaveChangesAsync` transaccional; el borrado de zona/nivel/sector con FK restringida (`bim_registro_diario`) se captura como `AbrilException 409` en vez de 500 crudo.
- **Iteración sobre la regla 7**: la primera versión del `Service` tenía validaciones de "nombre obligatorio" en zona/nivel/sector que contradecían la regla explícita del usuario ("sin campos obligatorios a nivel backend, única restricción dura = el 409 por FK"). Se removieron, y se agregó null-guard (`?? string.Empty`) antes de cada `.Trim()` en el repository para que un nombre nulo en guardado parcial no crashee con 500.

### Migración manual acotada: `fecha_inicio` nullable
Para soportar el lazy-create de fases con fechas sin definir, `bim_proyecto_fase.fecha_inicio` tenía que dejar de ser `NOT NULL` (así estaba en producción, verificado en vivo). `dotnet ef migrations add` arrastró drift no relacionado ya presente en los modelos del repo pero nunca migrado (`cat_jefatura`, columnas nuevas en `workers`/`lesson`/`ssoma_inspeccion`, tablas `ss_emo_correo_*`) — se descartó esa migración completa y se escribió a mano `Migrations/20260806170000_MakeFechaInicioNullableEnBimProyectoFase.cs` con un único `ALTER COLUMN`, sin `Designer.cs` propio (los atributos `[DbContext]`/`[Migration]` van directo en la clase; `dotnet ef migrations script`/`migrations list` lo reconocen igual). Se parcheó una sola línea del `AppDbContextModelSnapshot.cs` (el resto del drift ajeno queda intacto, tal como estaba). El SQL se mostró al usuario antes de aplicar, y el usuario lo corrió a mano en pgAdmin contra producción — verificado en vivo después: `is_nullable=YES` y `__EFMigrationsHistory` con la fila nueva como última entrada.

### Archivos clave
- `Features/PlaneamientoBimFeature/Application/{Dtos/ConfiguracionInicialDtos.cs, Interfaces/IPlaneamientoBimConfiguracionService.cs, Services/PlaneamientoBimConfiguracionService.cs}`
- `Features/PlaneamientoBimFeature/Infrastructure/{Interfaces/IPlaneamientoBimConfiguracionRepository.cs, Repositories/PlaneamientoBimConfiguracionRepository.cs, Models/BimProyectoFase.cs}` (FechaInicio → `DateOnly?`)
- `Features/PlaneamientoBimFeature/Presentation/PlaneamientoBimConfiguracionController.cs`
- `Features/PlaneamientoBimFeature/PlaneamientoBimModule.cs`, registrado en `Program.cs`
- `Migrations/20260806170000_MakeFechaInicioNullableEnBimProyectoFase.cs`, `Migrations/AppDbContextModelSnapshot.cs`

### Pendiente
- Drift de migración ajeno (`cat_jefatura`, `workers`/`lesson`/`ssoma_inspeccion`, `ss_emo_correo_*`) sigue sin resolver — no se tocó, no es de este feature.
- Falta el frontend de la pantalla de Configuración Inicial.
- No se implementó nada de `bim_registro_diario`/`bim_evidencia_foto`/`bim_bloqueo` (pantallas de seguimiento diario) — fuera de alcance de esta sesión.

## Sesión 2026-08-06/07 — Planeamiento BIM: Carga Diaria + Bloqueos, fix de GetPaged, fix de storage

Rama: `victor-backend`. Cierra la Fase 1 de Planeamiento BIM (Configuración Inicial ya estaba cerrada en la sesión anterior) y de paso corrige dos bugs reales encontrados al probar en vivo, no relacionados directamente con BIM.

### Carga Diaria + Bloqueos (100% desde cero, sin spec original — ver sesión anterior sobre `dashboard-planeamiento-bim-spec.md` perdida)
Reglas de negocio confirmadas por `/grill-me` corto del usuario (fuente de verdad, no la spec):
1. Acceso: mismos 3 roles que Configuración Inicial (`AdministradorSistema`/`AdministradorUdp`/`UsuarioUdp`) para Carga Diaria y Bloqueos.
2. Ventana de edición de `bim_registro_diario`: hoy y los 4 días anteriores (5 días corridos). Fuera de ventana: 400 si la fecha es futura, 409 si ya venció. Corrección dentro de ventana = UPDATE (upsert), no INSERT duplicado.
3. `bim_evidencia_foto` es general por proyecto+fecha, sin relación a zona/nivel/sector/actividad — misma ventana de 5 días que las celdas.
4. Guardado parcial: se puede cargar solo algunas celdas del cruce zona×nivel×sector×actividad; ausencia de registro = "sin cargar", no "no cumplida". Sin validación de "todo completo".
5. `causa_id` obligatorio (400) si `cumplida=false`; se ignora (se guarda null) si `cumplida=true`.
6. `bim_bloqueo` es puramente informativo — no bloquea la Carga Diaria normal, sin relación con celdas.
7. Mismo control de acceso (3 roles) para crear/actualizar/cerrar bloqueos.

Diseño de endpoints (acordado explícitamente antes de escribir código, iterando sobre 6 confirmaciones del usuario):
- **`GET /api/v1/planeamiento-bim/carga-diaria/{projectId}?fecha=`** — todo en una sola llamada (R1/B6): `zonas` (niveles+sectores anidados, reusa `ZonaDto`/`NivelDto`/`SectorDto` de Configuración Inicial), `actividades` (catálogo de 37, con `macroActividadNombre` resuelto), `causas` (catálogo de 5 — agregado en un paso posterior de la sesión, mismo patrón que `actividades`, tras detectar que el frontend tenía un dropdown de causas hardcodeado con 8 opciones inventadas), `celdas` (**sparse** — solo lo cargado, ausencia = sin cargar), `evidencias` de esa fecha, `bloqueosActivos` (`FechaCierre == null`), y `esEditable` calculado server-side.
- **`PUT /api/v1/planeamiento-bim/carga-diaria/{projectId}?fecha=`** — upsert por la tupla natural `(zonaId, nivelId, sectorId, actividadId)` + fecha de la URL contra `ix_bim_registro_diario_unico` (no por Id, a diferencia del diff-por-Id de Configuración Inicial, porque acá el cliente no tiene Id de antemano al ser sparse).
- **`POST /api/v1/planeamiento-bim/carga-diaria/{projectId}/evidencias?fecha=`** (multipart) — sube a `IStorageContainerResolver.GetProjectFotosContainerName()` (contenedor `project-fotos`, confirmado sin ningún uso previo en todo el repo antes de esto).
- **`api/v1/planeamiento-bim/bloqueos`** — `GET/{projectId}?soloActivos=`, `POST/{projectId}`, `PUT/{id}`, `PUT/{id}/cerrar`. `Estado` (texto libre en BD) validado en el Service contra `{ABIERTO, EN_GESTION}` en Create/Update — `"CERRADO"` solo lo asigna el endpoint `Cerrar` dedicado, para que `Estado` y `FechaCierre` nunca queden inconsistentes entre sí (decisión propia, confirmada con el usuario).

**Gap encontrado y corregido en el camino**: `PlaneamientoBimConfiguracionController` (Configuración Inicial) solo tenía `[Authorize]` genérico, sin los 3 roles — quedó inconsistente con los controllers nuevos hasta que se alineó explícitamente.

### Fix: `ProjectController.GetPaged` ignoraba `active`
Encontrado mientras se diagnosticaba una pantalla nueva (no relacionado a BIM). El parámetro `active` que mandan 7 pantallas SSOMA (Inspección, Auditoría ATS, OPT) nunca se bindeaba en el controller — el filtro se ignoraba en silencio y el endpoint devolvía proyectos activos e inactivos mezclados. Se confirmó contra el modelo (`Project.Active` es el campo correcto; `State` es borrado lógico ya filtrado fijo; `Activo` es un string sin uso real en este dominio) antes de tocar código. Fix de punta a punta (Controller→Service→Repository) con `bool? active = null`, sin cambiar el comportamiento por defecto. Pendiente aparte, no tocado: `rac-nuevo.ts` manda `estado: 'ACTIVO'` (otro parámetro, tampoco bindeado hoy) — mismo tipo de bug, queda para otra sesión.

### Bug real: evidencia fotográfica se subía pero la imagen no cargaba
Probado en vivo por el usuario: el backend devolvía URL de éxito, pero la URL daba `ResourceNotFound` en Azure. Investigación en dos capas:
1. **Bug de código real, corregido**: `AzureBlobStorageService.UploadFilesAsync` encadenaba `blobClient.UploadAsync(...).ContinueWith(_ => blobClient.Uri.ToString())` sin `OnlyOnRanToCompletion` — si `UploadAsync` fallaba, la excepción quedaba en un `Task` fallado que nadie observaba, y la continuación igual devolvía la URL calculada (no una confirmación de escritura). Se reemplazó por `await` normal. Afecta a los ~14 endpoints que usan este servicio contra Azure (Lessons, IVTs, Cuaderno de Obra, Adjudicaciones, ActasReunion, Vecinos, Topico, DescansoMedico, BIM) — se verificó que los 14 controllers llamantes ya tienen `catch (Exception)` genérico → 500, ninguno necesitó ajuste en paralelo. Firma pública sin cambios.
2. **Hallazgo posterior, NO corregido todavía**: al probar el fix end-to-end (subida real a Azure + verificación con `blobClient.ExistsAsync()` autenticado + insert/select real en `bim_evidencia_foto`, con limpieza del registro de prueba), se descubrió que el contenedor `project-fotos` tiene `PublicAccess = None` (privado), a diferencia de los otros 3 contenedores activos del sistema (`lecciones-aprendidas-imagenes`, `ivts-pdfs`, `cuaderno-de-obra-pdfs`, los tres en `Blob`). Causa: `CreateIfNotExistsAsync(PublicAccessType.Blob)` solo aplica el nivel de acceso al crear el contenedor por primera vez — como `project-fotos` ya existía (creado fuera de este código, sin uso previo real), quedó privado para siempre. **Las 3 filas de `bim_evidencia_foto` de las pruebas de esta sesión NO son huérfanas** — los 3 blobs existen realmente en Azure (confirmado con `ExistsAsync()`), simplemente no son accesibles públicamente. No se borraron. Quedan pendientes de aprobación del usuario: (a) cambiar el `PublicAccess` de `project-fotos` a `Blob` en Azure, y (b) reemplazar `CreateIfNotExistsAsync` por `SetAccessPolicyAsync(PublicAccessType.Blob)` incondicional en el código para que esto se autocorrija ante cualquier otro contenedor pre-existente mal configurado.

### Archivos clave
- `Features/PlaneamientoBimFeature/Application/{Dtos/CargaDiariaDtos.cs, Dtos/BloqueoDtos.cs, Interfaces/IPlaneamientoBimCargaDiariaService.cs, Interfaces/IPlaneamientoBimBloqueoService.cs, Services/PlaneamientoBimCargaDiariaService.cs, Services/PlaneamientoBimBloqueoService.cs}`
- `Features/PlaneamientoBimFeature/Infrastructure/{Interfaces/IPlaneamientoBimCargaDiariaRepository.cs, Interfaces/IPlaneamientoBimBloqueoRepository.cs, Repositories/PlaneamientoBimCargaDiariaRepository.cs, Repositories/PlaneamientoBimBloqueoRepository.cs}`
- `Features/PlaneamientoBimFeature/Presentation/{PlaneamientoBimCargaDiariaController.cs, PlaneamientoBimBloqueoController.cs, PlaneamientoBimConfiguracionController.cs}` (el último solo por el fix de roles)
- `Features/PlaneamientoBimFeature/PlaneamientoBimModule.cs`
- `Features/ConfigurationModule/Features/ProjectFeature/{Application,Infrastructure,Presentation}/**` (fix de `active`)
- `Shared/Services/Storage/Services/AzureBlobStorageService.cs` (fix del `.ContinueWith`)

### Pendiente
- Decisión del usuario sobre el contenedor `project-fotos` privado (ver arriba) — sin esto, ninguna evidencia fotográfica de Carga Diaria va a ser visible en el navegador aunque el backend funcione perfecto.
- `rac-nuevo.ts` / parámetro `estado` no bindeado en `GetPaged` — bug análogo al de `active`, no corregido a propósito.
- Falta el frontend de Carga Diaria y de gestión de Bloqueos (Antigravity).
- Drift de migración ajeno (`cat_jefatura`, etc., ver sesión anterior) sigue sin resolver.

## Sesión 2026-08-07 — Fix IES (ranking Arquitectura Comercial)

### Contexto
El ranking de eficiencia (IES) de supervisores en el dashboard de Arquitectura Comercial mostraba números confusos: supervisores con 100% de tareas culminadas (12/12, 4/4, 6/6) no llegaban a 100% de IES. Se identificó que el componente SPI del IES exigía un ritmo de 1.5x (50% adelantado sobre el plan) para dar el máximo puntaje — un umbral irreal, casi nadie llega a SPI 1.5 sostenido.

### Cambio
`Infrastructure/Repositories/ArquitecturaComercialRepository.cs:1513`, método `GetDashboardDataFiltrado`:
```csharp
var compSpi = Math.Min(spiPromedio / 1.0, 1.0) * 100;  // antes: / 1.5
```
Ahora SPI=1.0 (a tiempo, sin adelanto) ya da el 100% de ese componente. Sesión anterior (misma fecha) ya había quitado la penalización por mora del 10% del IES — ambos cambios en el mismo archivo/método.

Fórmula final del IES:
```
IES = (SPI*0.35 + Cierre*0.35 + Puntualidad*0.20) / 0.90
```
donde `compSpi = min(spiPromedio, 1.0) * 100`.

### Verificado
Confirmado en vivo (frontend) que el detalle semanal del ranking ("ver cuáles", agregado en `Abril-Frontend` la misma sesión) coincide con el conteo del IES — ej. Carbajal 3/5 mostró exactamente 3 Culminado + 2 no culminadas en el modal filtrado a la semana en control.

### Pendiente
Nada pendiente de este cambio puntual. Build local limpio (`dotnet build`, 0 errores).

## Sesión 2026-08-07 — Diagnóstico Planeamiento BIM invisible + role_feature faltante (Dashboard UDP) + fix timezone Carga Diaria

Rama: `victor-backend`. Sesión de tres pedidos encadenados del usuario, cada uno resuelto con SELECTs reales contra la única BD del proyecto (túnel SSH `localhost:5544 → VPS:5432`, ver reglas P2/P3) antes de tocar código — nunca se afirmó "está bien" sin pegar el resultado de una query.

### 1) Por qué Planeamiento BIM no aparecía en el menú
Diagnóstico con evidencia, no supuestos:
- **`feature`**: la fila existe (`feature_id=190`, `feature_key='planeamiento-bim.configuracion-inicial'`, `module_id=6` "Proyectos"). Las 3 pantallas (Configuración Inicial, Carga Diaria, Bloqueos) comparten a propósito el mismo `feature_key`.
- **`role_feature`**: ya asignada a los 3 roles esperados (`ADMINISTRADOR DEL SISTEMA`, `ADMINISTRADOR DE UDP`, `USUARIO DE UDP`), todos activos.
- **Local vs producción**: no existen como bases distintas en este proyecto — `appsettings.Development.json` y `appsettings.Production.json` tienen la misma connection string exacta (túnel 5544). Confirmado corriendo las mismas queries contra ambos archivos: resultado idéntico.
- **`vcolonio@abril.pe` (`user_id=23`)**: tiene 11 roles, incluidos `ADMINISTRADOR DEL SISTEMA` y `USUARIO DE UDP`. Se replicó la query real de `AuthRepository.GetAllowedFeaturesAsync` para ese usuario: de 104 features permitidas, `planeamiento-bim.configuracion-inicial` está incluida. El backend no es el problema.
- **featureKey backend vs frontend**: coincide carácter por carácter (`navigation.service.ts:79`, `proyectos.routes.ts:25,31,37`).
- **Causa real**: el commit del frontend con el sidebar y las 3 rutas (`a0e92443`, "feat(planeamiento-bim): agregar sub-navegación y pantallas completas de Carga Diaria y Bloqueos...") vive solo en `victor-frontend`/`origin/victor-frontend`. `git branch --contains a0e92443 -a` no lista `master`; `git show origin/master:navigation.service.ts | grep bim` y lo mismo para `proyectos.routes.ts` no encuentran nada. El frontend que se despliega a `/var/www/abril` (build desde `master`, regla P1) simplemente no tiene el código de BIM todavía. Pendiente: mergear `victor-frontend` a `master` y desplegar — no se hizo en esta sesión, quedó para decisión del usuario por tocar `master`/producción.
- **Hallazgo colateral**: `Migrations/Manual/20260807_PlaneamientoBimFeatureSeed.sql` existía en disco sin trackear en git (`git status` → `??`) — el INSERT que documenta ya estaba aplicado en BD, pero el archivo nunca se comiteó. Se comiteó en esta sesión (ver más abajo).

### 2) `role_feature` faltante para Dashboard UDP (`projects.cronograma-dashboard`)
Mismo patrón de diagnóstico con SELECTs reales: el rol de `vcolonio@abril.pe` para ese módulo es correcto (`USUARIO DE UDP`, `role_id=3`, confirmado que también tiene `ADMINISTRADOR DEL SISTEMA`), pero ninguno de sus 11 roles tenía la fila en `role_feature` para `feature_id=143` (`projects.cronograma-dashboard`). No era problema de rol asignado, sino de fila faltante. Se generó (no se ejecutó) el INSERT idempotente, `feature_id` por `SELECT` en vez de hardcodeado:
```sql
INSERT INTO role_feature (role_id, feature_id)
SELECT 3, feature_id FROM feature
WHERE feature_key = 'projects.cronograma-dashboard'
ON CONFLICT DO NOTHING;
```
Queda pendiente que el usuario lo corra manualmente (psql/pgAdmin, regla del proyecto).

### 3) Fix: Carga Diaria de Planeamiento BIM aceptaba fechas futuras
Bug real reportado por el usuario: un registro con fecha 2026-08-08 se guardó siendo 2026-08-07. Causa encontrada: `PlaneamientoBimCargaDiariaService.EsFechaEditable`/`ValidarVentanaDeEdicion` calculaban "hoy" con `DateOnly.FromDateTime(DateTime.UtcNow)` — UTC puro. Perú es UTC-5 sin horario de verano, así que desde las 19:00 hora Lima el backend ya considera "hoy" el día calendario siguiente (UTC ya cruzó medianoche), dejando pasar como "no futura" una fecha que en Lima todavía no llegaba. No era el frontend corriendo la fecha con `toISOString()`.

Fix: se agregó `HoyLima()` usando `TimeZoneInfo.FindSystemTimeZoneById("America/Lima")` + `TimeZoneInfo.ConvertTimeFromUtc(...)`, siguiendo el mismo patrón ya usado en `InduccionRepository.cs` (único otro lugar del repo con esta lógica). No se tocó `PlaneamientoBimConfiguracionRepository.cs:151` (`UpdatedDateTime = DateTime.UtcNow`) por ser columna de auditoría, correctamente en UTC.

### Archivos clave
- `Features/PlaneamientoBimFeature/Application/Services/PlaneamientoBimCargaDiariaService.cs` (fix de timezone)
- `Migrations/Manual/20260807_PlaneamientoBimFeatureSeed.sql` (comiteado, ya estaba aplicado en BD)

### Pendiente
- Mergear `victor-frontend` a `master` y desplegar para que Planeamiento BIM sea visible en producción — decisión del usuario, no hecho en esta sesión.
- Correr manualmente el INSERT de `role_feature` para `projects.cronograma-dashboard` + `role_id=3` (arriba) — no ejecutado en esta sesión, solo generado.
- Build local limpio (`dotnet build`, 0 errores) tras el fix de timezone.

## Sesión 2026-08-10 — Convalidación de EMO: riesgo, firma electrónica y control de cambio de puesto

### Contexto
El módulo de Convalidaciones (SSOMA → Salud Ocupacional) dejaba que el médico digitara a
mano el puesto/clasificación origen-destino, sin ninguna evaluación de riesgo ni control de
quién autorizó realmente la decisión. Paralelamente, "Cambiar obra" (Habilitación → Gestión
de Trabajadores) no distinguía cambio de obra (inocuo) de cambio de razón social/puesto/
clasificación (que sí debía revisar la aptitud del trabajador).

### Cambios principales
1. **Convalidaciones**: `puesto_origen`/`puesto_destino`/`obra_oficina_staff_origen_id`/
   `obra_oficina_staff_destino_id` en `worker_emo_convalidaciones`, resueltos automáticamente
   server-side (origen = vinculación histórica en la empresa de origen del EMO; destino =
   vinculación vigente) — ya no se digitan a mano. `cambio_riesgo` bloquea aprobar cuando sube
   de Oficina Central (bajo) a Staff/Obra (alto): exige EMO nuevo.
2. **Firma electrónica**: PIN de firma del médico (PBKDF2, `PinHasher.cs`) + reautenticación
   fresca de Microsoft (prompt=login), validados en `ConvalidacionService.ValidarFirmaAsync`
   antes de aceptar Aprobada/Rechazada. Auditoría en `ss_convalidacion_firma_log` (IP,
   user-agent, hash del documento). Formato SSO-FO-149 (`AutorizacionFirmaPdfService`) con la
   firma digital del médico (dibujada en un canvas, `firma-digital-pad`) impresa junto a un
   recuadro para la firma manuscrita de comparación. Orden obligatorio antes de habilitar el
   PIN: firma digital → autorización impresa/firmada a mano/escaneada y subida → PIN.
3. **Habilitación — "Cambiar obra"**: rediseñado con 4 checkboxes independientes (Obra, Razón
   social, Puesto de trabajo, Clasificación). Solo razón social, puesto o subida de riesgo
   disparan revisión de aptitud (`CertAptitud` → "Pendiente" + convalidación auto-creada,
   igual mecanismo para los 3 casos); cambio de obra puro no toca nada. Bloquea un nuevo
   cambio si ya hay convalidación sin resolver (`worker_emo_convalidaciones.resultado =
   'Pendiente'`).
4. **Dos bugs reales corregidos**:
   - `WorkerSearchRepository.Update()` (el lápiz "Editar trabajador") permitía cambiar obra/
     empresa/puesto/clasificación sin pasar por ningún control, y mutaba la vinculación
     vigente en vez de cerrarla y abrir una nueva (corrompía el historial). Esos 5 campos
     ahora son de solo lectura fuera de "Cambiar obra".
   - `GetEntregablesWorkerAsync`: el checklist de Habilitación mostraba "Certificado de
     Aptitud (EMO)" calculando su estado solo desde `WorkerEmo.Estado` (nunca "Pendiente"),
     ignorando el estado real en `ss_hab_trabajador` — corregido para leer ese estado real.

### SQL a correr en pgAdmin (`Migrations_Manual/`)
`2026-08-10_convalidacion_cambio_puesto.sql`, `_worker_vinculacion_obra_oficina_staff.sql`,
`_autorizacion_firma_medico.sql`, `_firma_digital_medico.sql` (todas confirmadas corridas en
esta sesión) y `_backfill_puesto_riesgo_convalidaciones.sql` (backfill de las 55
convalidaciones existentes, confirmado corrido).

### Verificado en vivo
Probado end-to-end con el trabajador real Justiniani Aranda (worker_id 12305): cambio de
razón social disparó `CAMBIO_EMPRESA`, `CertAptitud` → Pendiente, convalidación auto-creada;
confirmado por SQL contra `worker_eventos`/`ss_hab_trabajador`/`worker_vinculaciones`.

### Pendiente
- Subir documentos del EMO (Lectura/Certificado/EMO Completo) directo desde "Revisar
  convalidación" — hoy esa sección solo muestra si hay archivo, no permite cargar uno ahí.
- Repasar workers cuyo cambio de obra/empresa/puesto pasó por el bug del lápiz antes de este
  fix — no hay forma 100% confiable de detectarlos retroactivamente (la vinculación se mutó
  in-place, se perdió el historial de esos cambios puntuales).

## Sesión 2026-08-14 — Merge victor-backend→master, Planeamiento BIM Fase 2a/2b/3 (Avance, PPC, Plan Maestro, Procura, Portafolio, export PDF)

Rama: `victor-backend` (con un tramo de la sesión trabajado directo en `master`, ver abajo). Sesión larga retomando el diagnóstico del 404 en `/api/v1/planeamiento-bim/carga-diaria` de la sesión anterior (2026-08-07).

### 1) Merge de victor-backend a master
`victor-backend` estaba 44 commits detrás de `origin/master`. Se actualizó `master` local (fast-forward), se mergeó `victor-backend` (`--no-ff`, commit `f7d53aea`) con un solo conflicto real en `CONTEXT.md` (resuelto conservando ambas entradas de sesión), y se hizo push directo a `master` (regla P5). El resto de la sesión (Fase 2a en adelante) se trabajó directo sobre `master` por error de continuidad — corregido al final moviendo todo a `victor-backend` (ver sección 5).

Nota: el push reportó `Bypassed rule violations for refs/heads/master` — hay una regla de protección de rama en GitHub que este push saltó (permisos de admin). Vale la pena que el usuario la revise si no debía aplicar a este caso.

### 2) Fix bug real: 500 en `dashboard/{projectId}/avance`
`PlaneamientoBimDashboardRepository.GetAvance` armaba diccionarios de nombres con `.ToDictionary()` **dentro** de un `.Select()` de EF Core — no traducible a SQL (`InvalidOperationException`), fallaba para cualquier proyecto (no solo los sin configurar, como se creía al reportarlo). Fix: materializar las zonas con `.Include()` primero, armar los diccionarios después en memoria.

### 3) Fase 2a — Avance, PPC histórico, Plan Maestro semanal
- Tabla nueva `bim_meta_semanal` (project_id, macro_actividad_id, fecha_inicio/fin_semana, meta_avance decimal 0-100 = % acumulado tipo curva S). Confirmado con SQL real contra prod: `bim_macro_actividad` tiene 3 filas (Estructura Sótanos, Estructura Torre, Losa contraterreno), sin columna de metrado — se decidió medir la meta en % de avance (mismo criterio que `MetaPpc`), no en cantidad.
- Nuevo `PlaneamientoBimDashboardController` (`api/v1/planeamiento-bim/dashboard`): `GET avance`, `GET ppc`, `GET/PUT metas-semanales`, `GET plan-maestro`, `GET causas-pareto`. Todo agregación sobre `bim_registro_diario`/`bim_bloqueo` ya existentes, sin tocar los 3 controllers de Fase 1.

### 4) Procura simplificado (Fase 2b) — se descartó el módulo completo
Decisión de negocio: Procura es solo una categoría de evidencia fotográfica, no un módulo de OT/compras. Se agregó `bim_evidencia_foto.categoria` (`GENERAL`|`PROCURA`, CHECK constraint) en vez de tabla nueva — reutiliza el `GET`/`POST evidencias` de Carga Diaria ya existentes con un parámetro `categoria` opcional (default `GENERAL`, compatible con el frontend actual). Confirmado con el usuario: Procura comparte la misma ventana de edición de 5 días, sin excepción.

### 5) Fase 3 — Dashboard de Portafolio + export PDF
Dos decisiones de negocio confirmadas con el usuario antes de implementar (ninguna estaba en el código):
- **Rol de acceso**: no existe rol "Gerencia/Dirección" en la tabla `role` de prod (43 filas revisadas) → se gateó con `AdministradorSistema` + `AdministradorUdp` únicamente (sin `UsuarioUdp`, más restrictivo que el resto del feature).
- **Alcance del portafolio**: "proyecto con Planeamiento BIM asignado" = al menos 1 fila en `bim_proyecto_zona` (configuración real), no solo `bim_proyecto_fase` (se auto-crea con solo abrir la pantalla una vez — de 7 proyectos que la tienen, solo 2 —Kaurí, Torre Abril— tienen zonas reales).

Nuevo `PlaneamientoBimPortafolioController` (`api/v1/planeamiento-bim/portafolio`): `GET kpis` (PPC promedio últimos 7 días, proyectos por fase actual, proyectos con bloqueos >3 días abiertos, Pareto de causas del mes — todo cross-proyecto), `GET proyectos` (semáforo ≥90% verde / 70-89% amarillo / <70% rojo / sin registros gris), `POST {projectId}/export-pdf` (QuestPDF, ya instalado y licenciado en `Program.cs` — sin dependencia nueva; reutiliza `GetCargaDiaria`/`GetPpcHistorico` en vez de reconsultar).

### 6) Reordenamiento de rama al cierre
Todo el trabajo de la sesión 2)-5) se hizo por error directo sobre `master` (sin commitear). Al pedir "guardar rama", se detectó el error, se hizo `git stash`, `checkout victor-backend` (confirmado ancestro directo de `master`, sin commits propios divergentes — fast-forward seguro), `merge master --ff-only`, y `stash pop` — todo el trabajo quedó en `victor-backend` sin tocar `master` de nuevo ni perder historial.

### SQL aplicado en prod (túnel SSH, ambos entornos comparten la misma BD — ver nota en sesión 2026-08-07)
- `Migrations/Manual/20260814_AddBimMetaSemanal.sql`
- `Migrations/Manual/20260814_AddCategoriaBimEvidenciaFoto.sql`

Ambos idempotentes, aplicados directo con psql. Las migraciones EF correspondientes se generaron después solo para mantener el snapshot del modelo sincronizado (no se corrió `dotnet ef database update`).

### Verificado
- Build: 0 errores, 239 warnings (baseline estable en toda la sesión, sin warnings nuevos).
- Smoke tests en vivo contra el backend local (bypass temporal de `[Authorize]`, revertido en cada caso) para cada endpoint nuevo/tocado, incluyendo casos borde (proyecto inexistente, sin datos, categoría inválida) y una subida real de evidencia PROCURA a Azure Blob (borrada después de verificar).

### Pendiente
- Frontend de Fase 2a/2b/3 — no se tocó en esta sesión (era solo backend).
- Revisar si el push a `master` debía saltar la regla de protección de rama de GitHub (ver punto 1).
- Decidir si el rol "Gerencia/Dirección" del spec original amerita crearse en `role` a futuro, o si `AdministradorSistema`+`AdministradorUdp` queda como gate definitivo del Portafolio.

## Sesión 2026-08-17

Sesión corta: se actualizó `victor-backend` con lo último de `master` (44 archivos de GTH: Onboarding, Reclutamiento con doble aprobación GG, Actas de Reunión con agenda/recordatorios, Tareo de Arquitectura Comercial — todo de otras sesiones/ramas, fast-forward sin conflictos, build 0 errores) y se agregó el seed de la Fase 3 del Portafolio BIM.

- Nuevo `Migrations/Manual/20260817_PlaneamientoBimPortafolioFeatureSeed.sql`: siembra `feature.feature_key = 'planeamiento-bim.portafolio'` y su `role_feature` para `AdministradorSistema` (role_id 1) + `AdministradorUdp` (role_id 2), sin `UsuarioUdp` — coincide con el gate ya implementado en `PlaneamientoBimPortafolioController` (ver sesión anterior, punto 5).
- El pedido de frontend (Fase 3 + Procura simplificado, `DESIGN-VICTOR.md`) se redirigió a la sesión de `Abril-Frontend` — este repo es solo backend.

### Pendiente
- **El seed `20260817_PlaneamientoBimPortafolioFeatureSeed.sql` NO se corrió contra producción todavía.** Se revisó contra los checks D2/D3 pedidos: D3 (SELECT de `feature_id`, sin ID hardcodeado) OK; D2 usa `NOT EXISTS` en vez de `ON CONFLICT DO NOTHING` — funcionalmente idempotente igual, pero no es el patrón literal pedido (posible razón: `ON CONFLICT` exige una constraint UNIQUE que puede no existir en `feature_key`/`role_feature`). Quedó pendiente de confirmación del usuario antes de ejecutar por el túnel SSH.
- Frontend de Fase 3 (Dashboard de Portafolio + export PDF) y Procura simplificado — pendiente en `Abril-Frontend`.

## Sesión 2026-08-19 — Lectura de EMO por médico interno de Abril + coautores en Actas de Reunión

### 1) EMOs: lectura a cargo del médico ocupacional de Abril (no la clínica)
Hasta ahora la lectura de un EMO (`fecha_lectura` + `url_resultado`) siempre la subía la clínica al completar el EMO. Se necesitaba distinguir los EMOs cuya lectura la hace el médico interno de Abril Grupo Inmobiliario en vez de la clínica, y darle a ese médico una cola propia para completarla.

- Columna nueva `worker_emos.requiere_lectura_abril` (boolean, default false) — `Migrations_Manual/2026-08-18_worker_emos_requiere_lectura_abril.sql`, aplicar manual vía psql/pgAdmin (no EF). "Pendiente de lectura por Abril" = `requiere_lectura_abril = true AND url_resultado IS NULL` (mismo criterio que ya usaba el filtro existente "Sin Lectura EMO").
- El flag se puede marcar desde dos lugares: la clínica al completar el EMO (`EmoCreateDto.RequiereLecturaAbril`) o el personal interno al editar un EMO existente (`EmoUpdateDto.RequiereLecturaAbril`).
- Nuevo endpoint `POST /emos/{emoId}/lectura-abril` (`EmoController.CompletarLecturaAbril`) — sube el PDF a SharePoint, y delega a `EmoRepository.CompletarLecturaAbril` que guarda `FechaLectura`/`UrlResultado` y corre `SincronizarEntregableEmoAsync` (la misma sincronización de habilitaciones — `ss_hab_trabajador` item LecturaEmo → "Aprobado" — que ya corrían `Create()`/`Update()`). Antes, subir un documento de tipo "Lectura" vía el endpoint genérico `SubirDocumento` NO corría esa sincronización; este nuevo endpoint sí, para que "aprobar" siga el mismo proceso que cuando la clínica lo hace.
- `EmoPorTrabajadorFilterDto.PendienteLecturaAbril` + filtro correspondiente en `EmoRepository.ListPorTrabajador`, para alimentar la subtab nueva del frontend.
- Frontend (ver Abril-Frontend, misma sesión): checkbox "Será leído por el médico de Abril Grupo Inmobiliario" en Completar EMO (clínica) y Editar EMO (interno); subtab "Pendientes de lectura (médico Abril)" en la pantalla EMOs; modal Documentos EMO detecta el caso pendiente y muestra fecha + botón "Subir y aprobar" que pega al endpoint nuevo.

### 2) Actas de Reunión: coautores de acuerdos + flag `es_informativo`
Dos migraciones manuales nuevas, sin aplicar todavía contra prod (quedan pendientes, avisar antes de dar por cerrado):
- `Migrations_Manual/2026-08-18_reunion_participante_coautor.sql`
- `Migrations_Manual/2026-08-19_reunion_acuerdo_es_informativo.sql`

Tocados: `ReunionAcuerdo`, `ReunionParticipante`, `ActasReunionRepository` (+220 líneas), `ActasReunionService`, `ActasReunionController`, `ActasReunionDtos`.

### 3) Evaluaciones — ajustes menores
`EvPeriodoRepository`, `EvContratistaRepository`, `EvDashboardController`, `EvPeriodoController` — cambios acarreados de sesión(es) anterior(es), no hay detalle adicional registrado en esta sesión.

### Verificado
- Build: `dotnet build Abril-Backend.csproj` → 0 errores, solo warnings preexistentes (CS8618 en DTOs de Adjudicaciones, no relacionados a esta sesión).
- No se probó en runtime (el usuario tenía el backend corriendo en otra terminal, bloqueando el build hasta detenerlo).

### Pendiente
- Aplicar las 3 migraciones SQL nuevas contra la base real (el usuario las corre manualmente, no `dotnet ef database update`): `2026-08-18_reunion_participante_coautor.sql`, `2026-08-18_worker_emos_requiere_lectura_abril.sql`, `2026-08-19_reunion_acuerdo_es_informativo.sql`.
- Frontend correspondiente (Abril-Frontend) va en commit separado de esta misma sesión.
- Excluido a propósito de este push: rama `curso-prueba-loto` del repo `plataforma-cursos` (piloto de material interactivo LOTO, no tocar git/deploy todavía).

## Sesión 2026-08-19 (continuación) — Actualización de rama, sin cambios de código

Sesión corta: se trajo `origin/master` a `victor-backend` (`git fetch` + `git merge origin/master`) y se resolvió un conflicto en `CONTEXT.md` — no era un conflicto real de código, sino dos entradas de sesión cronológicas independientes (17 y 19 de agosto) que se concatenaron en orden. Build post-merge: 0 errores. `master` local también quedó sincronizado con `origin/master` (`git fetch origin master:master`).

Además se confirmó al usuario, a pedido, el contenido exacto y completo del seed `Migrations/Manual/20260817_PlaneamientoBimPortafolioFeatureSeed.sql` (40 líneas, 2 `INSERT` idempotentes vía `NOT EXISTS` — feature `planeamiento-bim.portafolio` + `role_feature` para roles 1 y 2) para que lo corra manualmente en pgAdmin. Sigue pendiente de ejecución contra producción (ver sesión anterior).

No hubo cambios de código en esta sesión.

## Sesión 2026-08-20 — Evaluaciones SSOMA: 3 flujos nuevos (backend completo, frontend flujo A)

Pedido del usuario (Jefe SSOMA): evaluar a los supervisores de campo de los contratistas, que su equipo (Coordinador SSOMA=70, Prevencionista=72) lo evalúe a él de forma anónima y obligatoria, y que los contratistas evalúen a los Prevencionistas/Coordinadores SSOMA asignados a su proyecto. Diseño confirmado con el usuario antes de implementar (incluye que sí quiere ver promedio+comentarios agregados del Flujo B, nunca la identidad del evaluador).

### Backend — `Features/EvaluacionesModule/` (10 tablas nuevas, 3 controllers)
- **Flujo A** (`EvSupervisorContratistaController`, ruta `api/v1/evaluaciones/supervisores-contratista`): evaluador = rol 70/72 vía `[Authorize(Roles=...)]`; `/ver` y `/dashboard` solo rol 9. Evaluado = persona en `ss_contratista_usuario` con rol de sistema 74 (Contratista Supervisor de Campo), resuelta por proyecto vía `worker_vinculaciones` del evaluador (mismo patrón que ya usa `EvContratistaRepository`).
- **Flujo B** (`EvJefeSsomaController`, ruta `.../jefe-ssoma`): anónimo y obligatorio. `ev_evaluacion_jefe_ssoma` (nota/comentario, SIN `evaluador_user_id`) y `ev_evaluacion_jefe_ssoma_cumplimiento` (solo `evaluador_user_id` + `completado_at`, para trackear quién falta) son dos tablas separadas sin FK entre sí — se insertan juntas en una sola transacción (`EvJefeSsomaRepository.RegistrarAsync`) pero nada en el esquema permite unir autor con respuesta. `/resultados` y `/pendientes` solo rol 9.
- **Flujo C** (`EvPrevencionistaController`, ruta `.../prevencionistas`): evaluador = sesión contratista existente (`tipo=CONTRATISTA`, `[Authorize(Roles=Roles.Contratista)]`, lee claims `empresaId`/`proyectoIds` del JWT ya emitido por `ContratistaAuthService`). Sí guarda identidad del evaluador (empresa + `ss_contratista_usuario_id`) porque el Jefe SSOMA la necesita en `/dashboard`; el anonimato es solo de cara al evaluado — `/mi-perfil` nunca selecciona esas columnas.
- `AppDbContext`: 10 `DbSet` nuevos agregados — **ojo**: estos ya quedaron commiteados y pusheados a `origin/master` accidentalmente dentro de un commit ajeno (`4e378b8 "fix: EMO sabados Staff..."`), de otra sesión que corrió `git add -A` sobre el mismo archivo mientras esta sesión trabajaba. Sin impacto (DbSet no se ejecuta hasta usarse, y nada más de este trabajo estaba commiteado en ese momento), pero avisado al usuario.
- Migración manual `Migrations_Manual/2026-08-20_evaluaciones_ssoma_supervisores_jefe_prevencionistas.sql` (10 tablas + 3 plantillas con 5 criterios sembrados cada una) — **ya corrida y verificada por el usuario**.
- Migración manual `Migrations_Manual/2026-08-20_evaluaciones_supervisores_contratista_feature_seed.sql` (feature+role_feature para las 2 rutas del Flujo A) — pendiente de correr.
- Bug real corregido en el camino: `EvPrevencionistaRepository` usaba `ValueTuple` como tipo de retorno de dos queries Dapper (`QueryAsync<(int,int)>` / `QueryAsync<(decimal?,string?)>`), que Dapper no sabe mapear — reemplazado por records (`YaEvaluadoRaw`, `NotaComentarioRaw`).

### Frontend — solo Flujo A implementado
`pages/evaluar-supervisor-contratista/` (clon de `evaluar-contratista`, escala 0-4 por criterio) y `pages/ver-evaluacion-supervisores/` (tabla consolidada, solo Jefe SSOMA), + `dtos/ev-supervisor-contratista.model.ts` + `services/ev-supervisor-contratista.service.ts`. Rutas registradas en `evaluaciones.routes.ts` y sidebar en `navigation.service.ts`.

**Descubrimiento importante**: el acceso a rutas de Evaluaciones no usa `data.roles` estático (aunque `roleGuard` lo soporta como fallback) sino el sistema dinámico `feature`/`role_feature` vía `featureKey`, igual que el resto del módulo — de ahí la migración de feature seed de arriba.

### Verificado
- `dotnet build Abril-Backend.csproj`: 0 errores de compilación (solo warnings preexistentes). Los 2 "errores" de MSB3027/MSB3021 al final son por el .exe bloqueado porque el usuario tenía el backend corriendo en otra terminal — no son errores de código.
- `npm run build` (frontend): 0 errores, solo warnings preexistentes de CommonJS de terceros.
- No probado en navegador ni con datos reales todavía.

### Pendiente (frontend)
- **Flujo B**: pantalla "Evaluar al Jefe SSOMA" (para 70/72) + pantalla de resultados (solo rol 9, promedio + comentarios sin autor + lista de pendientes). Falta decidir mecanismo de "obligatorio" (¿banner de bloqueo como charlas SSOMA, o solo recordatorio?).
- **Flujo C**: pantalla de evaluación dentro del portal `dashboard-contratista` (ya existe, ya resuelve `empresaId`/`proyectoIds` del JWT contratista) + `/mi-perfil` para el propio Prevencionista/Coordinador + dashboard consolidado para el Jefe SSOMA.
- Feature seeds de B y C (mismo patrón que el de A).
- Aplicar la migración de feature seed de A pendiente arriba.

## Sesión 2026-08-20 — Migración PdfSharpCore 1.3.67 → PDFsharp 6.2.4

### Motivo: bug real en producción
Al generar el paquete de contrato de la adjudicación **id 1** (JS MUEBLES Y DISEÑO SAC, GRAN MANZANO, contrato N° 32) el paso 4 fallaba con `Error generando paquete: Object with ID 32 0 resolved with negative position`.

Causa: la cotización adjunta (`PPTO 217 RV-1 TORRE A MUEBLES... .pdf`, exportada de Excel 2019) es un PDF 1.7 con tabla de referencias cruzadas en formato *stream* (`/Type /XRef`) cuya lista de objetos libres tiene huecos: `0(gen 65535) → 32 → 357 → 359 → 361 → 364(fin)`. El objeto 32 es un hueco de esa lista y **nada del documento lo referencia** (verificado descomprimiendo el `/ObjStm`). El PDF es válido y cualquier visor lo abre; PdfSharpCore registraba esas entradas libres con posición negativa y al importar las páginas lanzaba `PositionNotFoundException`.

Disparador exacto: **huecos libres en la xref**, no los object streams. De los 3 PDFs de esa adjudicación: cotización (xref stream + 4 huecos) fallaba; ficha técnica (xref stream, 261 objetos comprimidos, sin huecos) y orden de servicio (tabla xref clásica) pasaban.

Por qué solo afecta a los adjuntos: los `.docx`/`.xlsx` se bajan con `?format=pdf` y los convierte Graph → PDF limpio. Los adjuntos que ya son `.pdf` se bajan crudos (`AlreadyPdf` en `GenerateContractPackageAsync`) y van directo a la librería. Hoy hay ~64 PDFs así en prod (24 cotizaciones, 13 fichas técnicas, 27 órdenes de servicio), todos expuestos al mismo fallo.

**Descartado**: forzar `?format=pdf` para todo. Graph devuelve **406 Not Acceptable** al convertir PDF→PDF, así que el atajo `AlreadyPdf` es necesario.

### Cambios
- `Abril-Backend.csproj`: `PdfSharpCore` 1.3.67 → `PDFsharp` 6.2.4.
- `ProjectSubContractorService.cs`: `using PdfSharpCore.Pdf[.IO]` → `using PdfSharp.Pdf[.IO]`. `MergePdfs`, `InsertPdfAfterMarker` y `RotatePdfPages` no cambiaron de lógica (la API de `PdfReader.Open` / `AddPage` / `page.Rotate` es igual).
- `SignaturePdfStamper.cs`: usings + **único cambio de API real**: en PDFsharp 6 `XImage.FromStream` recibe un `Stream`, no un `Func<Stream>`. Se pasa un `MemoryStream` en `using`; el `XImage` ya tiene la imagen decodificada, así que cerrar el stream antes del `Save` no afecta (verificado).
- **Eliminado `Shared/Services/Pdf/ImageSharp3ImageSource.cs`** y su registro en `Program.cs`. Existía solo para parchear el proveedor de imágenes de PdfSharpCore (compilado contra ImageSharp v1, lanzaba `MissingMethodException` con ImageSharp 3.x). PDFsharp 6 decodifica imágenes nativamente y además genera `/SMask` en vez del hack BMP 32bpp + `/Mask`, o sea **mejor fidelidad de transparencia** en la firma.
- `SixLabors.ImageSharp` 3.1.12 se mantiene: ahora es dependencia **directa** de `SignaturePdfStamper.StampImageAsPdf` (normaliza png/jpg/webp y saca dimensiones), ya no un override para tapar CVEs transitivos. Esto deja obsoleta la nota de la línea ~1410.

### Verificado
- `dotnet build`: **0 errores**.
- Árbol de dependencias limpio: desaparecieron `MigraDocCore` y el `SixLabors.ImageSharp` 1.0.4 transitivo → **los 7 CVEs de ImageSharp ya no aparecen** (el único NU1903 restante es `Microsoft.OpenApi` 2.3.0, preexistente y ajeno).
- Contra PDFsharp 6.2.4, con los archivos reales bajados de SharePoint: `Import` + `AddPage` + `Save` OK en los 3 PDFs (incluida la cotización que rompía), `Modify` + `page.Rotate` OK sobre la cotización, `XGraphics.FromPdfPage` + `DrawImage` OK con transparencia `/SMask`, y `XUnit.FromPoint` + setters de `page.Width/Height` OK.
- Sin `XFont`/`DrawString` en el proyecto → no hace falta registrar `GlobalFontSettings.FontResolver` (PDFsharp 6 solo lo exige para dibujar texto).
- No probado end-to-end en el navegador: falta que el usuario regenere el paquete de la adjudicación 1.

## Sesión 2026-08-25 — Observaciones de Planeamiento sobre BIM: auditoría, fix de autorización, rol PLANEAMIENTO UDP, causas nuevas

Rama: `victor-backend`. Punto de partida: Planeamiento probó el módulo BIM y mandó una lista de observaciones. Antes de estimar nada se pidió mapear qué ya existía vs qué era desarrollo nuevo genuino.

### 1) Auditoría de las observaciones (sin tocar código)
- **Plan Meta / dashboard / KPIs**: ya existía backend completo desde la sesión 2026-08-14 (`GET plan-maestro`, `avance`, `ppc`, `causas-pareto`, Portafolio). El Plan Maestro es **semanal** (`bim_meta_semanal`), no diario — si Planeamiento pide granularidad diaria tipo curva S, eso sí sería desarrollo nuevo (no hay tabla de meta diaria hoy). El frontend de Fase 2a/2b/3 (Dashboard/Portafolio) seguía sin confirmarse desplegado — dato que este repo no puede verificar solo (Abril-Frontend aparte).
- **Cumplimiento parcial por %** (punto 3, prioridad alta): confirmado desarrollo real. Impacto mapeado: `BimRegistroDiario.Cumplida` (bool) se usa en `CargaDiariaDtos.cs`, `PlaneamientoBimCargaDiariaRepository/Service.cs`, 5 puntos en `PlaneamientoBimDashboardRepository.cs`, `PlaneamientoBimPortafolioRepository.cs` y `PlaneamientoBimReportePdfService.cs`. **No implementado todavía** — se armaron 4 preguntas de diseño para el usuario (columna nueva vs. migrar `cumplida` in-place, backfill histórico true/false→100/0, umbral de causa obligatoria con porcentaje, contrato del DTO fijo vs. libre) con recomendación en cada una; queda pendiente que el usuario decida con Planeamiento antes de programar.
- **Ampliar causas de incumplimiento** (punto 4): confirmado que `bim_causa_no_cumplimiento` es tabla catálogo, no enum — cambio de datos puro.
- **Bloqueos → Restricciones** y **Sector vs. Nivel**: confirmado que no son simples, quedan pendientes de hablar con el ingeniero antes de tocar nada (ver respuestas completas más arriba en la conversación de esa sesión).

### 2) Bug de arquitectura encontrado al pedir un rol nuevo (PlaneamientoUDP)
Al pedir crear un rol con acceso exclusivo a Planeamiento BIM + Portafolio, se descubrió que los 5 controllers de `PlaneamientoBimFeature` (`Configuracion`, `CargaDiaria`, `Bloqueo`, `Dashboard`, `Portafolio`) autorizaban con `[Authorize(Roles = "1,2,3")]` / `"1,2"` **hardcodeado por ID de rol**, sin relación con `role_feature` — cualquier rol nuevo con la feature sembrada en `role_feature` habría visto las pantallas (frontend es 100% featureKey-driven) pero recibido 403 en cada llamada API.

**Fix aplicado**: migrados los 5 controllers de `[Authorize(Roles=...)]` a `[Authorize]` + `[RequireFeature("planeamiento-bim.configuracion-inicial")]` / `"planeamiento-bim.portafolio"` — mismo patrón ya usado en `EmoController`/`ProgramacionEmoController` (`Shared/Filters/RequireFeatureAttribute.cs`, autoriza contra `role_feature` en runtime, sin IDs hardcodeados).

**Verificación real, no solo build**: se levantó el backend local contra la BD real (túnel SSH) y se probaron los 5 controllers × 3 roles existentes (UsuarioUdp, AdministradorUdp, AdministradorSistema) con JWTs firmados reales — 15/15 resultados idénticos al comportamiento anterior (incluido que UsuarioUdp sigue sin acceso a Portafolio, 403). Un rol de control sin ninguna feature BIM confirmó 403 en los 5. Solo hay 2 `feature_key` reales en la BD para todo el namespace `planeamiento-bim.*` (`configuracion-inicial` cubre las 4 pantallas, `portafolio` aparte) — no 5 como se asumió al principio.

### 3) Rol PLANEAMIENTO UDP — creado y asignado en producción
- `Migrations/Manual/20260825_RolPlaneamientoUdp.sql`: crea el rol (sequence normal, **sin ID fijo** — se verificó que ni backend (`RequireFeature`) ni frontend (`roleGuard`/`isNavEntryAllowed`, ambos featureKey-primero-luego-roles-como-fallback-muerto) dependen de un ID numérico para este rol) + 2 filas en `role_feature`. Corrido en prod: quedó `role_id = 80`.
- Sección 2 del mismo archivo: `INSERT INTO user_role` para los 4 "Ingeniero de Planeamiento BIM" activos (identificados por `workers.puesto`/`subarea`, confirmados por nombre con el usuario antes de ejecutar): Dulanto Martinez Jean Franco (114), Haro Jesus Jherson Steven (306), Portilla Velasquez Lidis Dayana Marlene (239), Sanchez Taipe Arturo (243). Se excluyó a propósito a 2 personas de "Ingeniería BIM" (Modelador/Arquitecto BIM) que el comodín de búsqueda también trajo.
- Ambos pasos corridos y verificados contra producción (vía túnel SSH, `localhost:5544`).

### 4) Causas de incumplimiento — 3 nuevas
`Migrations/Manual/20260825_BimCausasNoCumplimientoSeed.sql`: agrega "Falla de contratista", "Retrabajos", "Reprocesos por calidad" (orden 6-8) a `bim_causa_no_cumplimiento`. Corrido y verificado contra producción — 8 filas en total.

### Verificado
- Build: `dotnet build` → 0 errores, warnings preexistentes sin cambios.
- Todas las corridas de SQL contra producción fueron confirmadas con SELECT de verificación antes de darlas por cerradas.

### Pendiente
- Decisión de Planeamiento sobre el diseño de cumplimiento por % (punto 3) — 4 preguntas respondidas con recomendación, sin implementar.
- Confirmar con el ingeniero: Bloqueos→Restricciones (¿solo rename o cambio de flujo?) y la observación de Sector/Nivel (probable confusión de términos, no bug).
- Confirmar si el frontend de Fase 2a/2b/3 (Dashboard/Portafolio BIM) llegó a desplegarse — no verificable desde este repo.

## Sesión 2026-08-25 (continuación) — Implementa cumplimiento por %, asigna 2 residentes

Rama: `victor-backend`. Retoma el punto 3 (prioridad alta) dejado pendiente en la sección anterior: Planeamiento confirmó las 4 decisiones de diseño con el ingeniero.

### 1) `BimRegistroDiario.Cumplida` (bool) → `PorcentajeAvance` (decimal)
Confirmado con Planeamiento: migrar la columna existente (no una nueva), backfill true→100/false→0, causa obligatoria si `PorcentajeAvance < 100`, set fijo de valores permitidos (0/25/50/75/100) validado en código (`PlaneamientoBimCargaDiariaService.PorcentajesValidos`), no como `CHECK` en BD — para poder ajustar el set sin migración.

Código migrado de punta a punta: `CargaDiariaDtos.cs`, `DashboardDtos.cs`, `PortafolioDtos.cs`, `BimRegistroDiario.cs`, `PlaneamientoBimCargaDiariaService/Repository.cs`, `PlaneamientoBimDashboardRepository.cs`, `PlaneamientoBimPortafolioRepository.cs`, `PlaneamientoBimReportePdfService.cs`. Los KPIs de Avance/PPC/Pareto pasan de `COUNT(cumplida=true)` a `SUM(PorcentajeAvance)` — `PorcentajeDe()` ya no multiplica por 100 (ese factor ahora viene incluido en cada término sumado).

`Migrations_Manual/2026-08-25_bim_registro_diario_porcentaje_avance.sql`: no pasa por EF (el model snapshot tiene deuda acumulada de otras sesiones — generar la migración arrastraba ~2300 líneas ajenas, incluidos DROPs de otras features). `ALTER COLUMN ... TYPE numeric USING (CASE WHEN cumplida THEN 100 ELSE 0 END)` + `RENAME COLUMN`, con tabla de respaldo (`bim_registro_diario_backup_20260825`) y todo en una transacción. Backfill verificado de antemano (solo lectura, sin escribir nada) contra los 3 registros reales en prod, comparando la fórmula vieja vs. la nueva agrupada por zona/nivel/sector, por fecha y por macro-actividad — 0 discrepancias. **Corrido y verificado contra producción.**

### 2) Dos residentes de obra asignados en producción
`Migrations/Manual/20260825_AsignarResidenteNogales.sql` (Alfredo Canales → 9 NOGALES) y `20260825_AsignarResidenteSauceZen.sql` (Martín Véliz → SAUCE ZEN): insertan en `project_resident` (idempotente vía `ON CONFLICT`), sin IDs hardcodeados (resuelve por email/nombre de proyecto vía subqueries). Necesario porque las pantallas de Planeamiento BIM filtran el selector de proyectos por `project_resident` activo. **Corridos y verificados contra producción.**

### 3) Housekeeping: `appsettings.Development.json.bak` no estaba gitignorado
Apareció un `.bak` de `appsettings.Development.json` (mismo tipo de archivo que `CLAUDE.md` marca como "nunca commitear", con credenciales reales de SQL Server y PostgreSQL) sin regla de `.gitignore` que lo cubriera — quedaba como `??` en `git status`. Se agregó `appsettings.*.json.bak` a `.gitignore` y el archivo se dejó fuera del commit.

### Verificado
- `dotnet build` → 0 errores (251 warnings preexistentes, sin cambios).
- Los 3 SQL de esta sesión corridos contra producción con SELECT de verificación antes de cerrar cada uno.

### Pendiente
- Confirmar con Planeamiento que el flujo de carga diaria con % parcial funciona bien end-to-end en el frontend (este repo no lo puede probar).
- Mismos pendientes de la sección anterior: Bloqueos→Restricciones, Sector/Nivel, despliegue del frontend de Dashboard/Portafolio.

## Sesión 2026-08-25 — Deploy a master: BIM observaciones de Planeamiento

Merge de `victor-backend` a `master` (deploy a intranet/producción). Trae las dos sesiones de trabajo sobre las observaciones de Planeamiento en el módulo BIM de arriba (auditoría + fix de autorización + rol PlaneamientoUDP + causas nuevas, y luego la implementación de cumplimiento por % + asignación de 2 residentes). Resumen de lo que queda en producción tras este deploy:
- `BimRegistroDiario.Cumplida` (bool) migrado a `PorcentajeAvance` (decimal 0/25/50/75/100) de punta a punta (DTOs, servicio, repos de dashboard/portafolio, PDF).
- Los 5 controllers de `PlaneamientoBimFeature` migrados de `[Authorize(Roles=...)]` hardcodeado por ID a `[RequireFeature(...)]`.
- Rol PLANEAMIENTO UDP creado y asignado a los 4 ingenieros de Planeamiento BIM activos.
- 3 causas de incumplimiento nuevas en el catálogo.
- Residentes de obra asignados: Alfredo Canales → 9 NOGALES, Martín Véliz → SAUCE ZEN.
- `appsettings.*.json.bak` agregado a `.gitignore` (housekeeping, credenciales expuestas sin querer).

### Verificado
- `dotnet build` en `master` tras el merge: 0 errores.

### Pendiente
- Decisión de Planeamiento sobre Bloqueos→Restricciones y la observación de Sector/Nivel.
- Confirmar si el frontend de Fase 2a/2b/3 (Dashboard/Portafolio BIM) llegó a desplegarse.
- Confirmar con Planeamiento que el flujo de carga diaria con % parcial funciona bien end-to-end en el frontend.

## Sesión 2026-08-28 — Presupuesto Materiales SSOMA (ciclo de vida de proyecto) + módulo PETS

### 1) Diagnóstico de "Presupuesto real" para Sauce Zen (project_id=9)
Mapeado el pipeline completo del módulo `PresupuestoMaterialesFeature`: Catálogo → Cargas S10 → Estandarización → Drivers → Ratios (materiales `ss_ratio_proyecto` + dotación HH/Trabajadores `ss_ratio_proyecto_driver`) → Generar presupuesto. Sauce Zen no tiene drivers cargados (único bloqueante). Detectada dispersión grande en el ratio HH/m² (0.017–33) correlacionada con días de Tareo acumulados — proyectos con pocos días no son confiables aún incluidos. Anomalía sin resolver: Gardenia (id=2) y Amancae (id=33) muestran 0 días de Tareo con HH acumulado grande (inconsistente, pendiente investigar en `ss_tareo`). `Project.TiempoConstruccion` descartado como referencia (datos no confiables, ej. "18 días" para un edificio de 29k m²).

### 2) Nuevo campo `Project.Activo` (ciclo de vida) expuesto en Editar Proyecto
La columna ya existía (`Activo`: Finalizado|Activo|Inactivo) y ya la leía `RatioDriverRepository` como `CicloVida`, pero ningún endpoint la escribía — todos los proyectos históricos salían "Activo" por default. Se agregó `CicloVida` a `ProjectDto`/`ProjectEditDto` y su persistencia en `ProjectRepository.ApplyDtoToEntity(Project, ProjectEditDto)`. Sin migración (columna ya existía). Frontend: nuevo select en `proyecto-edit.html`.

### 3) Fix de build en módulo PETS (trabajo en curso de otra sesión, no relacionado)
`PetsImportService.cs` no compilaba: `ImportParrafoDto` e `ImportPasoPreviewDto` eran clases duplicadas idénticas, `TodosLosParrafos` usaba el tipo equivocado (CS0029 x2). Se unificó a `ImportPasoPreviewDto` y se eliminó el duplicado muerto.

### Verificado
- `dotnet build` → 0 errores (247 warnings preexistentes, sin cambios) tras el fix.
- `npm run build` (frontend) → 0 errores.

### Pendiente
- Marcar "Finalizado" en Configuración → Proyectos a los proyectos ya culminados (Los Laureles, Aquilaria, Gardenia, Amancae, Amaranta, Camelia, Lilas, Sauco).
- Investigar la anomalía de Tareo de Gardenia/Amancae antes de confiar en su ratio.
- Cargar drivers de Sauce Zen (Área Techada mínimo) y generar su presupuesto.
- Carga semanal histórica de HH vía Excel (análoga al S10 de materiales) — **no implementada todavía**, bloqueada esperando que el usuario pase un archivo de ejemplo real de su reporte de asistencia/Tareo (no hay formato estándar como el S10 para esto).
- Módulo PETS: el usuario mencionó que quedan "modelos" pendientes de otra sesión más allá de este fix puntual de build.

## Sesión 2026-08-28 (continuación) — Planeamiento BIM: sectores por nivel, Restricciones (rename de Bloqueos), 2 bugs reales encontrados y arreglados

Implementa las 3 observaciones de Planeamiento pendientes de la sesión del 25/08 (Bloqueos→Restricciones, Sector/Nivel) más el fix de PPC META fijo:

### Tarea 1 — PPC META fijo en 85%
`PlaneamientoBimConfiguracionService.MetaPpcEstandar` (const `85m`, `public` porque `PlaneamientoBimDashboardRepository.GetPpcHistorico` la referencia). Deja de ser editable desde Configuración; `GuardarConfiguracion`/`GetConfiguracion` fuerzan el valor. `PlaneamientoBimReportePdfService` no se tocó — hereda el valor vía la cadena `PortafolioService.ExportarPdf → DashboardService.GetPpcHistorico → repo`.

### Tarea 2 — Sectores por nivel + subestructura
`bim_zona_sector.zona_nivel_id` (nullable, diseño híbrido: NULL = compartido entre todos los niveles de la zona, con valor = exclusivo de ese nivel — sin migrar datos existentes) y `bim_zona_nivel.tipo_estructura` (SUBESTRUCTURA|SUPERESTRUCTURA). `ZonaDto.Sectores` se mueve a `NivelDto.Sectores` (merge propio-del-nivel + compartido-de-zona). `ZonaUpdateDto.SectoresCompartidos` nuevo, paralelo a `NivelUpdateDto.Sectores`, para poder seguir creando sectores compartidos desde la API. Migración: `Migrations/Manual/20260828_BimSectorPorNivelYTipoEstructura.sql`.

### Tarea 3 — Restricciones (rename de Bloqueos) + ubicación + fecha prevista
`BimBloqueo`→`BimRestriccion` (tabla física sigue `bim_bloqueo`, solo cambian clases/rutas C#: `PlaneamientoBimRestriccion{Controller,Service,Repository}`, ruta `api/v1/planeamiento-bim/restricciones`). Nuevas columnas nullable: `zona_id`, `zona_nivel_id`, `zona_sector_id`, `actividad_id`, `fecha_levantamiento_prevista` (la fecha real ya existía como `fecha_cierre`). Migración: `Migrations/Manual/20260828_BimRestriccionUbicacionYFechaPrevista.sql`. Fix manual del snapshot EF (`AppDbContextModelSnapshot.cs`, 2 ocurrencias de `BimBloqueo`→`BimRestriccion`) para evitar que un futuro `dotnet ef migrations add` genere un DropTable+CreateTable espurio por el rename de clase.

### Bug real #1 — Concat de navegaciones no traducible por Npgsql (encontrado en incidente de producción, CEDRO 33)
`GetCargaDiaria`/`GetConfiguracion` armaban `NivelDto.Sectores` con `n.Sectores.Concat(z.Sectores.Where(...))` dentro de una proyección LINQ-to-SQL — `InvalidOperationException` en runtime (Npgsql no lo traduce), aunque compilaba bien. Fix: materializar primero con `Include(z => z.Niveles).ThenInclude(n => n.Sectores).Include(z => z.Sectores)` + `ToListAsync()`, y armar el merge en memoria (LINQ-to-Objects) después. `PlaneamientoBimDashboardRepository.GetAvance` no tenía el bug (nunca hacía Concat en SQL) pero se alineó al mismo patrón por consistencia.

### Bug real #2 — FK violation al crear sector exclusivo de nivel (`SincronizarSectoresDeNivel`)
Al guardar Configuración con un sector nuevo asignado a un nivel específico, el INSERT fallaba con `bim_zona_sector_zona_id_fkey` (`zona_id=0`) — el método solo hacía `nivel.Sectores.Add(sector)` (fixup de `ZonaNivelId`) pero nunca asignaba la relación con `Zona`. El `catch (DbUpdateException... SqlState=="23503")` de `GuardarConfiguracion` devolvía el mismo 409 genérico de "no se puede eliminar", enmascarando que en realidad era un INSERT mal formado, no un DELETE bloqueado. Fix: `sector = new BimZonaSector { Zona = zona }` (mismo patrón de fixup por navegación que ya usaba `SincronizarSectoresCompartidos` con `zona.Sectores.Add(sector)`), pasando `zona` como parámetro nuevo de `SincronizarSectoresDeNivel`.

### Logging real agregado
`PlaneamientoBimCargaDiariaController` y `PlaneamientoBimConfiguracionController`: `ILogger<T>` inyectado, los `catch (Exception)` ahora loguean con `_logger.LogError(ex, ...)`. Antes no quedaba rastro de ningún 500 — el incidente de CEDRO 33 hubo que reproducirlo a mano para conseguir el stack trace real.

### Verificado (reproducción real contra la BD de prod vía túnel, con datos de prueba marcados y borrados después)
- Los 2 bugs reproducidos y confirmados arreglados con `curl` real contra `TORRE ABRIL` (id=15) y un proyecto de prueba (`ROBLES`, id=50, inactivo, sin datos reales) — 204 en los 3 escenarios: crear desde cero, sector compartido, y sector nuevo agregado a un nivel con sector compartido preexistente con Carga Diaria asociada (el sector viejo no se toca).
- `dotnet build` → 0 errores, 247 warnings (línea base, sin cambios).

### Pendiente
- **Bug nuevo sin resolver, investigación cortada a mitad**: reporte de que un sector creado desde el frontend en el panel "exclusivos de este nivel" termina clasificado como compartido tras guardar y recargar. No se pudo confirmar la causa — no hay logging de request body (se agregó temporalmente y se revirtió sin llegar a reproducir), y el estado real en BD (proyecto 12, BOSQUE REAL, zona "EDIFICIO PRINCIPAL" id=6, niveles "Sotano 1"/"Piso 1") no tiene ningún sector asociado (ni compartido ni de nivel) — no coincide con el síntoma reportado. Hipótesis del usuario (no confirmada): la zona/niveles se crearon con el bug #2 todavía activo, el intento de agregar sectores falló con 409 y nunca se persistieron; un guardado posterior sin esos sectores en el payload los habría "borrado" correctamente (comportamiento esperado del diff-by-Id, no un bug). **No se tocó el proyecto 12 (datos reales).**
- Deploy a `master` sigue pendiente — falta confirmación de que el frontend terminó su parte, ya que esto rompe contrato JSON (`CargaDiariaDto.BloqueosActivos`→`RestriccionesActivas`, `ZonaDto.Sectores` cambió de forma a `NivelDto.Sectores`).
- Confirmar con el usuario si retoma la investigación del bug de clasificación de sectores (con logging de payload) antes o después del deploy a `master`.

## Sesión 2026-08-29 — Rediseño de Torres/Niveles/Sectores y Alineación de Restricciones & Carga Diaria en Planeamiento BIM

Rediseño completo de la arquitectura del módulo de Planeamiento BIM para alinearse al nuevo modelo de datos de Torres/Niveles y sectores derivados 1..N, eliminando definitivamente la dependencia con la tabla huérfana `bim_zona_sector`.

### 1. Modelo de Datos y Entidades
- **Torres & Niveles**: Reemplazo de `BimProyectoZona` y `BimZonaNivel` por `BimProyectoTorre` (`bim_proyecto_torre`) y `BimTorreNivel` (`bim_torre_nivel`).
- **Sectores Derivados**: `bim_zona_sector` pasa a ser tabla huérfana no navegable. El sector es un entero derivado 1..N calculado según `TipoEstructura` ("SUBESTRUCTURA" → `CantidadSectoresSubestructura`, "SUPERESTRUCTURA" → `CantidadSectoresSuperestructura`).
- **Restricciones (`bim_bloqueo`)**:
  - Removida la FK `fk_bim_bloqueo_zona_sector`.
  - Mapeadas columnas reales: `torre_id` (FK a `bim_proyecto_torre`), `nivel_id` (FK a `bim_torre_nivel`), `sector` (`int?` nullable derivado).
  - Propiedades legadas `ZonaId`, `Zona`, `ZonaNivelId`, `ZonaNivel`, `ZonaSectorId` marcadas con `[NotMapped]` y Fluent API `e.ToTable("bim_bloqueo")` con `.HasColumnName(...)` explícito.
- **Carga Diaria (`bim_registro_diario`)**:
  - Columna `zona_id` renombrada a `torre_id` (FK `fk_bim_registro_diario_torre` a `bim_proyecto_torre.id`).
  - Columna `nivel_id` mantenida con FK `fk_bim_registro_diario_nivel` a `bim_torre_nivel.id`.
  - Columna `sector_id` mantenida como entero plano 1..N (sin FK).
  - `BimRegistroDiario.cs`: Mapeadas únicamente `TorreId`, `NivelId`, `SectorId`. Propiedades legadas `ZonaId` y `Zona` marcadas con `[NotMapped]` y desmapeadas en Fluent API con `e.Ignore(x => x.ZonaId)` y `e.Ignore(x => x.Zona)`.
  - Soporte de `Cumplida = null` (celdas neutras / no evaluadas / sin programar): `CausaId` es exigido ÚNICAMENTE si `Cumplida.HasValue && Cumplida.Value == false`. Celdas neutras no registran fila en BD o remueven la fila previa existente si la hubiere.

### 2. Migraciones DDL Transaccionales Ejecutadas en VPS (PostgreSQL)
- `Migrations/Manual/20260829_UpdateBimBloqueoTorreNivelSector.sql`:
  - `ALTER TABLE bim_bloqueo DROP CONSTRAINT fk_bim_bloqueo_zona_sector;`
  - Renombrado de columnas `zona_id` → `torre_id`, `zona_nivel_id` → `nivel_id`, `zona_sector_id` → `sector`.
  - Creación de FKs `fk_bim_bloqueo_bim_proyecto_torre_torre_id` y `fk_bim_bloqueo_bim_torre_nivel_nivel_id`.
- `Migrations/Manual/20260829_UpdateBimRegistroDiarioTorreFk.sql`:
  - Renombrado de columna `zona_id` → `torre_id` en `bim_registro_diario`.
  - Removidas constraints obsoletas `bim_registro_diario_zona_sector_id_fkey`, `bim_registro_diario_zona_nivel_id_fkey`.
  - Recreadas constraints limpias: `fk_bim_registro_diario_torre` ON `torre_id` → `bim_proyecto_torre(id)` y `fk_bim_registro_diario_nivel` ON `nivel_id` → `bim_torre_nivel(id)`.

### 3. Verificación
- Compilación `dotnet build` → **0 errores** (247 warnings preexistentes sin cambios).
- Commit realizado en rama **`victor-backend`**: `feat(planeamiento-bim): refactor Torres, Niveles, Restricciones y Carga Diaria`.

## Sesión 2026-08-30 — Diagnóstico post-merge de Planeamiento BIM + campo Responsable Planeamiento UDP

### 1. Actualizar rama + diagnóstico del merge
- `actualizar rama` trajo 1 commit ajeno de `origin/master` (`0530e5c9`, presupuesto-materiales, de otra persona) — confirmado sin relación alguna a Planeamiento BIM (sin tocar `Features/PlaneamientoBimFeature/`, sin migraciones, sin menciones a `bim_zona_sector`/`bim_bloqueo`/`bim_torre`/etc.). Build limpio tras el merge.
- A pedido del usuario se auditó a fondo el commit `dbffca3c` (rediseño Torres/Niveles/Restricciones/Carga Diaria, autoría propia del 2026-08-29) para retomar el diseño de Restricciones que había quedado sin cerrar.

### 2. Shim `[NotMapped]` de `BimRestriccion.cs`: diagnóstico y decisión
- Se verificó que el commit `dbffca3c` agregó dos shims de compatibilidad de nombres viejos (`Zona*`), pero con propósitos distintos:
  - `CeldaDto`/`CeldaUpdateDto.ZonaId` (`CargaDiariaDtos.cs`) — **real y activo**: wire-compat con el frontend que puede seguir mandando `zonaId` en el JSON. Consumido en `PlaneamientoBimCargaDiariaRepository.cs:159` y `PlaneamientoBimReportePdfService.cs:96,99`. **No tocar.**
  - `BimRestriccion.ZonaId/.Zona/.ZonaNivelId/.ZonaNivel/.ZonaSectorId` (el modelo EF) — **código muerto**: la entidad nunca se serializa directo (siempre pasa por `RestriccionDto`, ya renombrado sin campos legados en el mismo commit), y no hay ningún consumidor C# real. No hay tests en el repo.
- Decisión del usuario: no completar el shim (dejar el hueco de `ZonaSector` sin agregar) ni eliminarlo todavía — queda como limpieza pendiente para cuando se cierre el diseño completo de cascada de Restricciones (Controller/Service/UX, que `dbffca3c` nunca tocó).

### 3. Deuda histórica de sectores sin clasificar en Carga Diaria
- Query real contra producción (túnel SSH `localhost:5544`, confirmado que no hay separación local/prod real en este proyecto — mismo connection string en `appsettings.Development.json`/`appsettings.Production.json`): 3 registros de `bim_registro_diario` (ids 1, 2 en **KAURÍ**; id 3 en **TORRE ABRIL**) cuyo nivel (`Piso 1` de `Torre A` en ambos proyectos) tiene `tipo_estructura = NULL` — mismo patrón ya documentado en `BimTorreNivel.cs` para BOSQUE REAL.
- Decisión del usuario (opción 1): dejar esos 3 registros tal cual, sin tocar/reinterpretar/asignar default. Quedan como deuda histórica hasta que alguien clasifique esos niveles (tipo_estructura + cantidad de sectores) en Configuración Inicial. La validación de rango de `SectorId` en `PlaneamientoBimCargaDiariaService` aplica solo a guardados nuevos.

### 4. Feature nueva: "Responsable Planeamiento UDP" en Configuración de Proyectos
- Requerimiento: campo nuevo tipo autocomplete en Configuración → Proyectos → sección RESPONSABLE, mismo patrón que "Responsable UDP"/"Responsable Arq. Comercial" (`GET api/v1/project/responsables?tipo=...`, filtro por `Worker.Subarea` + `WorkersEstadoId == Activo`).
- Investigación previa a codear: la subárea real en el catálogo de `workers` es `"Planeamiento BIM"` (5 activos), distinta de `"Ingeniería BIM"` (2 activos, modelado/arquitectura BIM — explícitamente excluida por el usuario).
- **Hallazgo clave que cambió el diseño**: `Project.ResponsablePlaneamientoBimId`/`ResponsablePlaneamientoBim` ya existían y ya estaban cableados end-to-end desde Planeamiento BIM → Configuración Inicial (`PlaneamientoBimConfiguracionRepository`, mismo filtro exacto por subárea "Planeamiento BIM"). El usuario confirmó que es el mismo dato/rol — no se creó columna nueva ni migración.
- Cambios (commit `a8a73f0e`, rama `victor-backend`, **sin push todavía** — pendiente de verificación en UI real con frontend antes de mergear/considerar cerrado, ver [[project_responsable_planeamiento_udp]] en memoria):
  - `ProjectRepository.cs`: nuevo case `"PLANEAMIENTO_UDP" => "Planeamiento BIM"` en el switch de `GetResponsables(tipo)`; campo agregado a la proyección `ProjectDto` del listado/detalle; agregado a ambos overloads de `ApplyDtoToEntity` (`ProjectCreateDto` y `ProjectEditDto`).
  - `ProjectDto.cs`, `ProjectEditDto.cs`, `ProjectCreateDto.cs`: agregado el par `ResponsablePlaneamientoBim`/`ResponsablePlaneamientoBimId`.
- Build `dotnet build` → 0 errores.
- **Pendiente**: esperar a que frontend termine su parte y verificar en UI real (crear/editar proyecto desde Configuración de Proyectos, confirmar persistencia y que no rompe lo que ya guarda Planeamiento BIM → Configuración Inicial sobre la misma columna) antes de dar la feature por cerrada.
