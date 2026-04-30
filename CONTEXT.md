# CONTEXT.md — Abril Backend
> Pega este archivo en la raíz del proyecto. Claude Code lo leerá al inicio de cada sesión.
> Última actualización: 2026-04-30 (migración entregables, reglas ss_item_trabajador, roles SSOMA/Admin, SharePoint fix, visor documentos)

---

## 1. Descripción del proyecto

Backend API REST para **Abril Grupo Inmobiliario** (Lima, Perú).
Gestiona proyectos inmobiliarios, contratistas, costos, y el módulo SSOMA de Salud Ocupacional (EMOs).

- **Framework:** ASP.NET Core (.NET 10)
- **Base de datos:** PostgreSQL en **Aiven** (producción) / SQL Server (desarrollo local opcional)
- **Auth:** JWT interno (`"Bearer"`) + Azure AD (`"AzureAd"`). Ambos esquemas coexisten.
- **Puerto dev:** 5236 (http) / 7298 (https)
- **Swagger:** solo en Development en `/swagger`
- **Email:** PowerAutomate (configurado en appsettings)
- **CronSecret:** leído via `IConfiguration["CronSecret"]` — NO usar `Environment.GetEnvironmentVariable`

---

## 2. Comandos esenciales

```bash
dotnet build Abril-Backend.csproj
dotnet run --project Abril-Backend.csproj
# NO existe dotnet test
```

---

## 3. Configuración

`Program.cs` carga en este orden:
```
appsettings.json
→ appsettings.{Environment}.json
→ appsettings.Local.json      ← gitignored, contiene secrets reales
→ variables de entorno
```

| Key | Valores |
|-----|---------|
| `Database:DatabaseProvider` | `"PostgreSQL"` / `"SqlServer"` |
| `Email:EmailProvider` | `"SendGrid"` / `"PowerAutomate"` / SMTP |
| `Storage:StorageProvider` | `"Azure"` / local `wwwroot/uploads` |
| `AzureAd:TenantId/ClientId/AbrilBackendSecret` | Credenciales Graph API |
| `SharePoint:SiteId` | ID del sitio SharePoint de habilitación |

**PostgreSQL** usa `UseSnakeCaseNamingConvention()`. Colisiones con palabras reservadas de PG → override en `ConfigurePostgreSQL` en `Shared/Data/AppContext.cs`.

---

## 4. Arquitectura: dos patrones coexistentes

### 4a. Layered tradicional (carpetas raíz)

```
Controllers/                  → [ApiController], ruta "api/v1/[controller]"
Application/Interfaces/       → I*Service
Application/Services/         → *Service
Application/DTOs/             → agrupados por dominio
Application/Exceptions/       → AbrilException (con HTTP StatusCode)
Infrastructure/Interfaces/    → I*Repository
Infrastructure/Repositories/  → EF Core con IDbContextFactory
Infrastructure/Models/        → entidades EF
Shared/Data/AppContext.cs     → AppDbContext
Shared/Services/              → Email, Excel, Jwt, Reniec, Storage, Sunat
```

### 4b. Vertical slice (Features/)

```
Features/<Modulo>Module/
  <Modulo>Module.cs                     → AddXxxModule(IServiceCollection)
  <Feature>Feature/
    Application/{Interfaces,Services,Dtos}
    Infrastructure/{Interfaces,Repositories,Models}
    Presentation/*Controller.cs
```

**Módulos existentes:**
- `ContractorsModule` — ContractorRegistration, ContractorManagement
- `CostsModule` — Adjudicaciones
- `MicrosoftAuthModule` — MicrosoftLogin, MicrosoftProfile
- `HabilitacionModule` ← **módulo principal activo**
- `SsomaModule` ← módulo SSOMA en `Features/SsomaModule/SaludOcupacionalFeature/`

**Regla:** Solo el `AddXxxModule()` se registra en `Program.cs`. Todo lo interno va dentro del módulo.
**DbSets** se declaran en el `AppDbContext` compartido, no en contextos propios.

---

## 5. Convenciones obligatorias

### Repositorios — siempre IDbContextFactory
```csharp
public class MiRepo : IMiRepo
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    public MiRepo(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task DoWork()
    {
        using var ctx = _factory.CreateDbContext();
        // ...
    }
}
```

### Controladores — try/catch estándar
```csharp
try { ... return Ok(result); }
catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
catch (Exception) { return StatusCode(500, new { message = "Error interno del servidor." }); }
```

### Auth para cronjobs
```csharp
var authHeader = Request.Headers["Authorization"].FirstOrDefault();
if (authHeader != $"Bearer {_configuration["CronSecret"]}") return Unauthorized();
```

### Mensajes de error → siempre en español.

---

## 6. Vocabulario de entidades — ⚠️ crítico

| Nombre C# | Tabla PG | Notas |
|-----------|----------|-------|
| `Projects` | `projects` | **Entidad NUEVA**. Propiedades: `Id`, `Nombre`, `Activo`, `Estado`. Es la tabla a la que apuntan TODAS las FKs nuevas (`worker_vinculaciones.proyecto_id`, `ss_equipo.proyecto_id`, `ss_sctr_vidaley.proyecto_id`, `ss_hab_empresa.proyecto_id`). Ubicada en `Infrastructure/Models/Projects.cs`. **NO tiene `DbSet` declarado** — acceder vía `ctx.Set<Projects>()`. |
| `Project` | `project` | **Entidad LEGACY** (singular). Propiedades: `ProjectId`, `ProjectDescription`, etc. Ubicada en `Shared/Models/Project.cs`. DbSet `ctx.Project`. ⚠️ **NO usar para resolver `proyecto_id` proveniente de tablas nuevas** — los IDs caen en rangos similares y los joins "funcionan" pero retornan nombres incorrectos. |
| `User` | `app_user` | Override en ConfigurePostgreSQL |
| `Worker` | `workers` | Personal SSOMA |
| `WorkerEmo` | `worker_emos` | Registros EMO |
| `SsItemTrabajador` | `ss_item_trabajador` | Catálogo de entregables con reglas |
| `SsHabTrabajador` | `ss_hab_trabajador` | Entregables por trabajador |
| `SsHabDocumentoVersion` | `ss_hab_documento_version` | Historial versiones |

### ⚠️ Pitfall crítico: `Project` (legacy) vs `Projects` (nuevo)

Existen **dos entidades C# distintas** que apuntan a **dos tablas PG distintas**:

```csharp
// ❌ INCORRECTO al resolver nombre de proyecto desde una FK nueva
var proyectos = await ctx.Project
    .Where(p => proyectoIds.Contains(p.ProjectId))
    .Select(p => new { p.ProjectId, p.ProjectDescription })
    .ToListAsync();

// ✅ CORRECTO — usar la entidad nueva
var proyectos = await ctx.Set<Projects>()
    .Where(p => proyectoIds.Contains(p.Id))
    .Select(p => new { p.Id, p.Nombre })
    .ToListAsync();
```

**Síntoma del bug**: el endpoint devuelve un nombre de proyecto incorrecto (ej: worker GODENZI con `proyecto_id=10` mostraba "Gran Manzano" del legacy `project` en vez de "Oficina Central" del nuevo `projects`).

**Regla**: si la FK que tienes en mano viene de una tabla nueva (`worker_vinculaciones`, `ss_*`, `ac_*`), siempre resuélvela contra `ctx.Set<Projects>()`. Solo usa `ctx.Project` cuando trabajas con entidades del dominio legacy (`Lesson`, `MilestoneSchedule`, `UserProject`, `ResidentReport*`, `ProjectSubContractor`, `StaffProjectEmail`).

---

## 7. Roles del sistema

| role_id | role_description |
|---------|-----------------|
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

**Roles aprobadores habilitación** (array `RolesAprobadores` en controllers):
```csharp
["ADMINISTRADOR SSOMA", "ADMINISTRADOR DE UDP", "ADMINISTRADOR ADMINISTRACION"]
```
Actualizado en: `HabTrabajadorController`, `HabEmpresaController`, `EquipoController`, `SctrVidaLeyController`.

---

## 8. Módulo Habilitación SSOMA

**Ubicación:** `Features/HabilitacionModule/`

**Stack técnico agregado:**
- BCrypt.Net-Next 4.1.0 — hash de passwords contratistas
- FluentValidation.AspNetCore 11.3.1 — validaciones automáticas
- Dapper 2.1.72 — queries optimizadas para bandeja y reportes

### SharePoint — subida de archivos

`ISharePointHabService` / `SharePointHabService` — registrado como **Singleton** (importante para caché de token y driveId).

Flujo:
1. Token OAuth2 con `client_credentials` desde `AzureAd:{TenantId,ClientId,AbrilBackendSecret}` — cacheado con margen 2 min
2. `driveId` del sitio resuelto via Graph API — cacheado permanente
3. PUT a `graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/root:/{path}:/content`
4. Path guardado: `habilitacion/{contexto}/{yyyyMMdd}_{filename_sanitizado}`

**Fix crítico**: `SubirArchivoAsync` strip el prefijo `habilitacion/` del contexto si ya viene incluido (evita duplicado `habilitacion/habilitacion/`).

**Endpoint GET URL temporal**: `GET /api/v1/habilitacion/archivos/url?path={path}` → retorna `{ url }` con `@microsoft.graph.downloadUrl` (~1h vigencia, no requiere auth).

### Catálogo ss_item_trabajador — reglas completas

```sql
-- Columnas de control:
aplica_a              → 'TODOS' | 'CASA' | 'CONTRATISTA'
aplica_categoria      → CSV positivo (ej: 'Supervisor,Prevencionista')
aplica_obra_oficina   → CSV positivo (ej: 'Oficina Central,Staff')
excluye_obra_oficina  → CSV exclusión (ej: 'Oficina Central,Staff')
excluye_categoria_contratista → CSV exclusión solo para CONTRATISTA
requiere_vigencia     → bool — si false, al aprobar vigencia = 2040-12-31
responsable           → 'SSOMA' | 'ADMINISTRACION'
```

**Items actuales y sus reglas:**

| id | nombre | aplica_a | aplica_categoria | excluye_obra_oficina | excluye_categoria_contratista | requiere_vigencia | responsable |
|----|--------|----------|-----------------|---------------------|------------------------------|-------------------|-------------|
| 1 | DNI | TODOS | | | | true | SSOMA |
| 2 | Certijoven | CONTRATISTA | | | | false | ADMINISTRACION |
| 3 | CarnetRetcc | TODOS | | Oficina Central,Staff | Supervisor,Prevencionista | true | SSOMA |
| 4 | Certificado de Aptitud (EMO) | TODOS | | | | true | SSOMA |
| 5 | Registro de Entrega de EPP | CASA | | | | false | SSOMA |
| 6 | Entrega RISST | CASA | | | | false | SSOMA |
| 7 | T-Registro | TODOS | | | | false | ADMINISTRACION |
| 8 | Entrega de Recomendaciones SST | CASA | | | | false | SSOMA |
| 10 | Difusion de Procedimiento de Trabajo Seguro | CASA | | | | false | SSOMA |
| 11 | SCTR | TODOS | | | | true | SSOMA |
| 12 | Induccion Obra | TODOS | | | | false | SSOMA |
| 13 | Vida ley | TODOS | | | | true | SSOMA |
| 14 | Declaracion Jurada de Domicilio | CASA | | | | false | SSOMA |
| 15 | Antecedentes Penales | CASA | | | | false | SSOMA |
| 16 | Antecedentes Policiales | CASA | | | | false | SSOMA |
| 17 | Certificado de estudios de derecho habientes | CASA | | | | false | SSOMA |
| 18 | Entrevista con Residente o Produccion | TODOS | Operador,Rigger,Vigia | | | false | SSOMA |
| 19 | Entrevista con el Jefe Corporativo SSOMA | CONTRATISTA | Supervisor,Prevencionista | | | false | SSOMA |
| 20 | Entrevista con el área de Calidad | CONTRATISTA | Supervisor,Capataz | | | false | SSOMA |
| 21 | Habilitación en el Colegio de Ingenieros | CASA | Residente | | | true | SSOMA |
| 22 | Curriculum Ultimos 2 años | CASA | | | | false | SSOMA (aplica_obra_oficina: Oficina Central,Staff) |

**Nota EMO**: Para workers Casa, el EMO NO vive en `ss_hab_trabajador` — se gestiona desde `worker_emos` via módulo SSOMA. Para Contratistas sí vive en `ss_hab_trabajador`.

### Lógica InicializarEntregablesAsync

Al crear un worker nuevo, se llama automáticamente desde `POST /api/v1/workers`. Aplica filtros en este orden:
1. `AplicaA == "TODOS" || AplicaA == workerType`
2. `CsvContiene(AplicaCategoria, worker.Categoria)`
3. `CsvContiene(AplicaObraOficina, worker.ObraOficina)`
4. `!CsvExcluye(ExcluyeObraOficina, worker.ObraOficina)`
5. `!esContratista || !CsvExcluye(ExcluyeCategoriaContratista, worker.Categoria)`

Registros creados con `Estado = "Falta"`, `Vigencia = null`.

### Lógica EstadoCalc (badge Habilitado/Autorizado Temporalmente/No Autorizado)

```csharp
EstadoCalc =
  (SsHabTrabajador.Any(h => WorkerId == w.Id &&
       (Estado == "Falta" || Estado == "Rechazado" || Estado == "Vencido") &&
       !(w.ContrataCasa == "Casa" && itemsEmoIds.Contains(h.ItemId)))
   || (w.ContrataCasa == "Casa" && !WorkerEmo.Any(e => e.WorkerId == w.Id &&
       e.Activo && (e.Estado == "Vigente" || e.Estado == "Convalidado"))))
  ? "No Autorizado"
  : SsHabTrabajador.Any(h => WorkerId == w.Id && Estado == "En Plazo"
      && !(w.ContrataCasa == "Casa" && itemsEmoIds.Contains(h.ItemId)))
  ? "Autorizado Temporalmente"
  : "Habilitado"
```

### Estados de entregables

`Aprobado` | `Rechazado` | `Falta` | `No Aplica` | `En Plazo` | `Vencido` | `Enviado`

- `Enviado` = contratista subió archivo, pendiente aprobación
- `requiere_vigencia = false` → al aprobar, vigencia = `2040-12-31` automático
- `requiere_vigencia = true` → vigencia obligatoria al aprobar

### Endpoints completos

```
POST/GET    /api/v1/habilitacion/auth/login|empresas
POST        /api/v1/habilitacion/auth/activar|solicitar-reset|reset-password
PATCH       /api/v1/habilitacion/auth/cambiar-password
GET/POST/PUT /api/v1/habilitacion/empresas
POST        /api/v1/habilitacion/empresas/{id}/reenviar-activacion
GET         /api/v1/habilitacion/empresas/{id}/entregables
PUT         /api/v1/habilitacion/empresas/{id}/entregables/{itemId}
GET         /api/v1/habilitacion/catalogos/items-trabajador|items-empresa|items-equipo|criterios
GET         /api/v1/habilitacion/trabajadores
POST        /api/v1/habilitacion/trabajadores/{id}/inicializar   ← auto-crea entregables
GET/PUT     /api/v1/habilitacion/trabajadores/{id}/entregables
GET         /api/v1/habilitacion/trabajadores/entregables/{id}/versiones
PATCH       /api/v1/habilitacion/trabajadores/{id}/cambiar-obra|reingreso
GET/PATCH   /api/v1/habilitacion/bandeja
GET/PATCH   /api/v1/habilitacion/bandeja/cursor
GET/POST    /api/v1/habilitacion/sctr-vidaley
PATCH       /api/v1/habilitacion/sctr-vidaley/{id}/aprobar
GET/POST/PUT/DELETE /api/v1/habilitacion/reglas
GET         /api/v1/habilitacion/auditoria
POST        /api/v1/habilitacion/archivos/subir
GET         /api/v1/habilitacion/archivos/url?path={path}   ← URL temporal SharePoint
GET         /api/v1/habilitacion/registros-modelo (público)
```

### Reglas de negocio críticas

- Worker solo puede pertenecer a UNA empresa activa a la vez
- Violación registrada en `ss_hab_bloqueo_log` → 409
- Contratista solo ve workers de su empresa (JWT claim `empresaId`)
- Contratista solo puede cambiar estado a `'Enviado'`
- EMO Casa → read-only, lee de `worker_emos`
- EMO Contratista → vive en `ss_hab_trabajador`
- SCTR/Vida Ley → masivo, un documento cubre múltiples workers
- Al aprobar con `requiere_vigencia = false` → forzar `Vigencia = 2040-12-31`

### Pitfalls conocidos

- `SharePointHabService` debe ser **Singleton** — el token y driveId se cachean en instancia
- **`worker_vinculaciones.proyecto_id` apunta a `projects.id` (entidad `Projects`), NO a `project.project_id` (entidad `Project` legacy)**. Resolver siempre vía `ctx.Set<Projects>()`. Ver sección 6 para detalles.
- **Al consultar `worker_vinculaciones` activas, siempre `ORDER BY created_at DESC, id DESC`**. `fecha_inicio` no es único y EF/PG no garantiza orden estable sin tie-breaker → puede devolver vinculación incorrecta cuando un worker tiene varias filas con `fecha_fin IS NULL`. Para JOINs en SQL crudo, usar `LEFT JOIN LATERAL (... ORDER BY created_at DESC, id DESC LIMIT 1) ON TRUE` para evitar duplicar filas base.
- `WorkerVinculacion.EmpresaId` apunta a tabla legacy `companies`, NO a `ss_empresa_contratista.Id`
- FluentValidation 11.3.1 usa API deprecated — migrar cuando bumpeemos v12
- `AuditoriaInterceptor` debe ser **Singleton** — inyectar `IServiceScopeFactory`
- `datos_anteriores`/`datos_nuevos` son `jsonb` → `HasColumnType("jsonb")` en `ConfigurePostgreSQL`
- `id_trabajador` en `workers` es campo temporal (vinculador PowerApps legacy) — pendiente eliminar

---

## 9. Trabajo pendiente

### Alta prioridad
- **Auth real** — quitar `[AllowAnonymous]` de SSOMA y `ProjectController`
- **Crear primer usuario admin** en `app_user`
- **Correlación `WorkerVinculacion.EmpresaId`** con `ss_empresa_contratista.Id` via `IdLegacy`
- **Deploy a producción**

### Media prioridad
- **Empresas contratistas** — 1,591 vinculaciones sin empresa
- **tipo_emo_id** — los 813 EMOs migrados tienen NULL
- **Eliminar `id_trabajador`** de `workers` tras confirmar migración completa

### Baja prioridad
- 8 EMOs sin match de DNI — insertar manualmente
- 24 vinculaciones sin proyecto
- `ReminderController.cs` aún usa `Environment.GetEnvironmentVariable` para CronSecret — migrar
