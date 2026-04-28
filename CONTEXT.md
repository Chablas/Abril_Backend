# CONTEXT.md — Abril Backend
> Pega este archivo en la raíz del proyecto. Claude Code lo leerá al inicio de cada sesión.
> Última actualización: 2026-04-28 (fixes interceptor/jsonb/Swagger, filtro CONTRATISTA, CRUD usuario)

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
- `SsomaModule` ← **módulo SSOMA activo** en `Features/SsomaModule/SaludOcupacionalFeature/`

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
| `Projects` | `projects` | **Entidad unificada** — fusiona los antiguos `Project` (legacy) y `Proyecto` (SSOMA). 32 columnas mapeadas con `[Column("...")]`. Propiedades clave: `Id`, `Nombre`, `Activo`, `Estado`, `ResponsableArqCom`, `ResponsableArqComId`, `Email{Residente,Responsable,Rrhh,CoordSsoma,CoordAdmin}`, `CreatedAt`, `UpdatedAt`. Navigation: `Incidences` (a `ResidentReportIncidence`). |
| `User` | `app_user` | Override en ConfigurePostgreSQL |
| `Company` | `companies` | Dominio inglés legacy |
| `Empresa` | `companies` | **Misma tabla** — alias español en SSOMA |
| `Worker` | `workers` | Personal SSOMA |
| `WorkerEmo` | `worker_emos` | Registros EMO |

**DTOs preservan nombres legacy**: `ProjectDTO`/`ProjectSimpleDTO`/`ProjectCreateDTO`/`ProjectEditDTO`/`ProjectEmailsUpdateDto` siguen usando `ProjectId`/`ProjectDescription`/`Active`/`CreatedDateTime`/`UpdatedDateTime`. La traducción entidad↔DTO ocurre dentro de los repos (p. ej. `ProjectId = entity.Id`, `ProjectDescription = entity.Nombre ?? string.Empty`, `Active = entity.Activo`, `CreatedDateTime = entity.CreatedAt ?? DateTime.MinValue`).

**Auditoría perdida**: la tabla `projects` no tiene `created_user_id`/`updated_user_id`. Los métodos `Create`/`Update`/`DeleteSoftAsync`/`UpdateEmails` aún reciben `userId` por compat de firma, pero ese valor ya no se persiste sobre `Projects`.

---

## 7. Módulo SSOMA — Salud Ocupacional

**Ubicación:** `Features/SsomaModule/SaludOcupacionalFeature/`

### Estado de la BD (Aiven) — datos reales cargados

| Tabla | Registros | Estado |
|-------|-----------|--------|
| `workers` | 2,466 | ✅ Migrados, únicos por DNI |
| `worker_emos` | 813 | ✅ Con fecha_vencimiento calculada |
| `worker_vinculaciones` | 2,466 | ✅ Una por worker activo |
| `companies` | 23 | ✅ Razones sociales Abril |
| `projects` | 27 | ✅ Con columnas email SSOMA |
| `ss_emo_tipos` | 5 | ✅ Precargados |
| `ss_clinicas` | 0 | ⏳ Pendiente cargar |
| `ss_medicos_ocupacionales` | 0 | ⏳ Pendiente cargar |

### Índice único workers
```sql
CREATE UNIQUE INDEX uq_workers_dni_activo ON workers(dni) WHERE estado = 'ACTIVO';
-- Solo puede haber un worker ACTIVO por DNI
-- Retirar: UPDATE workers SET estado='RETIRADO', fecha_retiro=NOW() WHERE id=X
```

### Reglas de negocio workers
- `estado`: solo `'ACTIVO'` o `'RETIRADO'`
- `obra_oficina = 'Oficina Central'` → vigencia EMO **2 años**
- Resto → vigencia EMO **1 año**
- DNI siempre 8 dígitos, rellenar con ceros a la izquierda

### Reglas de negocio EMO
- `worker_emos.estado`: `Vigente`, `Convalidado`, `Vencido`, `Reemplazado`
- Al crear EMO nuevo: marcar anterior `activo=false, estado='Reemplazado'`
- `fecha_vencimiento_calculada = fecha_emo + vigencia_meses`
- En queries siempre: `COALESCE(fecha_vencimiento_calculada, fecha_vencimiento)`

### Columnas especiales en `projects`
```
email_residente, email_responsable, email_rrhh, email_coord_ssoma, email_coord_admin
```

### Columnas especiales en `worker_emos`
```
interconsulta_resuelta (bool), fecha_levantamiento (date),
coincide_empresa (bool), fecha_lectura (date)
```

### Sistema de alertas (implementado)
- `GET /api/v1/ssoma/salud-ocupacional/alertas/procesar` — autenticado con CronSecret
- Busca EMOs que vencen en 4 días hábiles (excluye domingos)
- Idempotente via `ss_alertas_emo`
- **Pendiente:** flujo Grupo A (Staff/OC → correo individual a clínica) vs Grupo B (Obra → correo agrupado por proyecto)

### Repositorios — todos completos ✅
`CatalogosRepository`, `ConvalidacionRepository`, `DashboardRepository`,
`InterconsultaRepository`, `ProgramacionEmoRepository`, `WorkerSearchRepository`,
`EmoRepository`, `EmoAlertaService`,
`EmpresaContratistaRepository`, `HabTrabajadorRepository`,
`HabEmpresaRepository`, `SctrVidaLeyRepository`, `EquipoRepository`,
`BandejaRepository`, `ReglasTrabajadorRepository`, `CatalogosHabilitacionRepository`

### Endpoints completos

```
GET/POST        /api/v1/ssoma/salud-ocupacional/emos
GET/PUT         /api/v1/ssoma/salud-ocupacional/emos/{id}
PATCH           /api/v1/ssoma/salud-ocupacional/emos/{id}/estado
GET             /api/v1/ssoma/salud-ocupacional/emos/por-trabajador
GET             /api/v1/ssoma/salud-ocupacional/workers/{id}/historial-emo
GET/POST        /api/v1/ssoma/salud-ocupacional/convalidaciones
PUT             /api/v1/ssoma/salud-ocupacional/convalidaciones/{id}
GET/POST        /api/v1/ssoma/salud-ocupacional/programaciones
PUT             /api/v1/ssoma/salud-ocupacional/programaciones/{id}
PATCH           /api/v1/ssoma/salud-ocupacional/programaciones/{id}/estado
GET/POST        /api/v1/ssoma/salud-ocupacional/interconsultas
PUT             /api/v1/ssoma/salud-ocupacional/interconsultas/{id}
PATCH           /api/v1/ssoma/salud-ocupacional/interconsultas/{id}/resultado
GET             /api/v1/ssoma/salud-ocupacional/dashboard
GET/POST/PUT    /api/v1/ssoma/salud-ocupacional/catalogos/clinicas
GET/POST/PUT    /api/v1/ssoma/salud-ocupacional/catalogos/medicos
GET/POST/PUT    /api/v1/ssoma/salud-ocupacional/catalogos/emo-tipos
GET/POST/PUT    /api/v1/ssoma/salud-ocupacional/catalogos/examen-tipos
GET/POST/PUT    /api/v1/ssoma/salud-ocupacional/catalogos/restriccion-tipos
GET             /api/v1/ssoma/salud-ocupacional/catalogos/empresas
GET             /api/v1/ssoma/salud-ocupacional/workers/search
GET             /api/v1/ssoma/salud-ocupacional/alertas/procesar
PATCH           /api/v1/project/{id}/emails
```

---

## 8. DbSets en AppDbContext — no duplicar

```csharp
DbSet<Projects>               Projects           // tabla: projects (entidad unificada)
DbSet<Worker>                 Worker
DbSet<WorkerEmo>              WorkerEmo
DbSet<WorkerEmoConvalidacion> WorkerEmoConvalidacion
DbSet<WorkerVinculacion>      WorkerVinculacion
DbSet<Empresa>                Empresa            // tabla: companies
DbSet<SsClinica>              SsClinica
DbSet<SsMedicoOcupacional>    SsMedicoOcupacional
DbSet<SsEmoTipo>              SsEmoTipo
DbSet<SsExamenTipo>           SsExamenTipo
DbSet<SsRestriccionTipo>      SsRestriccionTipo
DbSet<SsEmoExamenDetalle>     SsEmoExamenDetalle
DbSet<SsEmoRestriccion>       SsEmoRestriccion
DbSet<SsInterconsulta>        SsInterconsulta
DbSet<SsProgramacionEmo>      SsProgramacionEmo
DbSet<SsSeguimientoMedico>    SsSeguimientoMedico
DbSet<SsAlertaEmo>            SsAlertaEmo
```

---

## 9. Pitfalls conocidos

- **Una sola entidad para `projects`**: usar `Projects` (consolidada). Los archivos `Project.cs` y `Proyecto.cs` fueron eliminados; no recrearlos. Navigation properties en otras entidades (`ResidentReportIncidence.Project`, `ProjectSubContractor.Project`, `WorkerVinculacion.Proyecto`) son tipo `Projects` aunque conserven el nombre singular legacy.
- **Property renames legacy → consolidado**: `.ProjectId` → `.Id`, `.ProjectDescription` → `.Nombre`, `.Active` → `.Activo`, `.CreatedDateTime` → `.CreatedAt`, `.UpdatedDateTime` → `.UpdatedAt`. Las propiedades `State`, `LevelDescription`, `CreatedUserId`, `UpdatedUserId` ya no existen en la entidad.
- **DbSet renombrado**: `_context.Project` y `_context.Proyecto` ya no existen. Usar `_context.Projects`.
- `Empresa`/`Company` → misma tabla `companies`. NO crear tabla `empresas`.
- `User` → tabla `app_user` (override en ConfigurePostgreSQL).
- `CronSecret` → `IConfiguration["CronSecret"]`, NO `Environment.GetEnvironmentVariable`. (Nota: `ReminderController.cs` aún usa la forma vieja — pendiente migrar.)
- Controllers SSOMA tienen `[AllowAnonymous]` temporal — quitar antes de producción.
- Archivos `* - copia.cs` causan CS0101 — eliminar siempre.
- `IDbContextFactory` obligatorio en repos nuevos — el `ProjectRepository` legacy aún inyecta `AppDbContext` directo (no replicar ese patrón).
- `Abril_Backend.sln` está en `.gitignore` — no commitear.
- **`AuditoriaInterceptor` debe ser `Singleton`** — inyectar `IServiceScopeFactory` en el constructor; crear un scope puntual dentro de `SavingChangesAsync` para resolver `IHttpContextAccessor`. Registrarlo como `Scoped` produce "Cannot resolve scoped service from root provider" porque `DbContextFactory` resuelve desde el root provider.
- **`datos_anteriores`/`datos_nuevos` son `jsonb`** — la propiedad C# es `string?` pero la columna PG es `jsonb`. Sin `HasColumnType("jsonb")` en `ConfigurePostgreSQL`, Npgsql envía el parámetro como `text` y PG rechaza con "expression is of type text". El override va en `ConfigurePostgreSQL`, no en el modelo (evita romper SQL Server).
- **`ProjectController` tiene `[AllowAnonymous]` temporal a nivel de clase** — aplicado para pruebas; revertir antes de producción. `userId` está hardcodeado a `0` en Create/Edit/Delete.
- `SsEmoTipo.VigenciaMeses` es `int?` (nullable) — el tipo "Retiro" tiene `NULL` y el cálculo de `fecha_vencimiento_calculada` se omite cuando es null (`tipo.VigenciaMeses > 0` descarta null).
- Al retirar un worker (`PATCH /workers/{id}/retirar`) se cierran automáticamente todas sus `worker_vinculaciones` abiertas (`fecha_fin = today`) en la misma transacción.
- `ProjectController` tiene `[AllowAnonymous]` a nivel de clase y la validación `User.FindFirst(ClaimTypes.NameIdentifier)` está comentada en sus 6 acciones — `userId` se reemplazó por `0` en `Create`/`Edit`/`Delete` (no se persiste en la tabla `projects`). Es estado temporal de pruebas, revertir antes de producción.

---

## 10. Trabajo pendiente

### ✅ Completado recientemente
- **Workers CRUD** — `POST` / `PUT /{id}` / `PATCH /{id}/retirar` (cierra vinculaciones abiertas). Validación DNI 8 dígitos en controller, unicidad de DNI activo en repo (409).
- **Catálogos SSOMA funcionando** — clínicas, médicos, tipos EMO con CRUD completo. `vigencia_meses` ahora `int?`.
- **Emails de proyectos** — `PATCH /api/v1/project/{id}/emails` (parcial: solo actualiza los campos que vienen no-null).
- **Projects entity consolidada** — `Project.cs` y `Proyecto.cs` eliminados; única entidad `Projects` con `[Table("projects")]` y 32 columnas mapeadas explícitamente.
- **`[AllowAnonymous]` temporal en `ProjectController`** — aplicado a nivel de clase para pruebas; revertir antes de producción.
- **Flujo alertas A/B** — implementado en Sprints 1-4 del módulo Habilitación SSOMA.
- **Auth contratistas completo** — registro, activación por email, login por email+password, reset de contraseña, cambio de contraseña.
- **Endpoint reenviar activación para admins** — `POST /api/v1/habilitacion/empresas/{id}/reenviar-activacion`, gateado a `ADMINISTRADOR SSOMA` / `ADMINISTRADOR DE UDP`, invalida tokens previos.
- **57 empresas contratistas migradas desde PowerApps** — vía `Database/migrations/002_migracion_datos.sql`; quedan con `password_hash='PENDIENTE_RESET'` hasta primer login.
- **16 registros modelo insertados** — catálogo público disponible vía `GET /api/v1/habilitacion/registros-modelo` (`[AllowAnonymous]`).
- **Tabla `ss_reset_token`** — soporta tokens de activación (48h) y reset de contraseña (2h); `usado=true` invalida re-uso y reenvíos.
- **PUT /api/v1/user/{id} y PATCH /api/v1/user/{id}/toggle** — CRUD de usuario completo: edita nombre/email/rol (con reemplazo de `UserRole`); toggle activa/desactiva. `UpdatedUserId` se toma del JWT claim.
- **Fix `AuditoriaInterceptor`** — registrado como `Singleton`; accede a `IHttpContextAccessor` vía `IServiceScopeFactory` (scope puntual en cada `SavingChangesAsync`). Resuelve "Cannot resolve scoped service from root provider".
- **Fix jsonb** — `HasColumnType("jsonb")` para `DatosAnteriores`/`DatosNuevos` en `ConfigurePostgreSQL`. Resuelve "column datos_anteriores is of type jsonb but expression is of type text".
- **Perfil `Development` en `launchSettings.json`** — `ASPNETCORE_ENVIRONMENT=Development`, puerto 5236. Arranca Swagger en `/swagger`.
- **Fix Swagger `ArchivoHabilitacionController.Subir`** — `IFormFile` + `string` envueltos en `SubirArchivoRequest` DTO (`Features/HabilitacionModule/Application/Dtos/Archivos/`). Resuelve `SwaggerGeneratorException`.
- **Filtro CONTRATISTA en `GET /habilitacion/trabajadores`** — si el rol es `CONTRATISTA`, se ignora el query param `empresaId` y se fuerza desde el claim JWT `"empresaId"`. Patrón idéntico al que ya existía en `GET /{workerId}/entregables`.

### Alta prioridad
- **Auth real** — quitar `[AllowAnonymous]` de SSOMA y `ProjectController`, activar JWT
- **Usuario admin** — `app_user` vacía, sin usuario no hay login
- **Migración de datos PowerApps → `ss_empresa_contratista`** (IdLegacy mapping)
- **Correlación `WorkerVinculacion.EmpresaId` con `ss_empresa_contratista.Id`**
- **Crear primer usuario admin en `app_user`** — sin esto no hay login de Abril
- **Resetear passwords de empresas migradas** — las 57 empresas tienen `password_hash='PENDIENTE_RESET'`; lanzar reenvío masivo de activación o disparar `solicitar-reset` por correo a cada una.
- **Deploy a producción**

### Media prioridad
- **Empresas contratistas** — 1,591 vinculaciones sin empresa
- **tipo_emo_id** — los 813 EMOs migrados tienen NULL

### Baja prioridad
- 8 EMOs sin match de DNI — insertar manualmente
- 24 vinculaciones sin proyecto

---

## 11. Módulo Habilitación SSOMA

**Ubicación:** `Features/HabilitacionModule/`

**Stack técnico agregado:**
- BCrypt.Net-Next 4.1.0 — hash de passwords contratistas
- FluentValidation.AspNetCore 11.3.1 — validaciones automáticas
- Dapper 2.1.72 — queries optimizadas para bandeja y reportes

**Auth contratistas:**
- Endpoint: `POST /api/v1/habilitacion/auth/login`
- JWT con claims: `NameIdentifier=empresaId`, `Role=CONTRATISTA`, `empresaId`, `tipo`
- Expiración: 8 horas
- Password hasheado con BCrypt

**Interceptor de auditoría:**
- `Shared/Interceptors/AuditoriaInterceptor.cs`
- Registrado como **`Singleton`** en `Program.cs` (no Scoped)
- Inyecta `IServiceScopeFactory`; crea scope puntual en cada `SavingChangesAsync` para leer `IHttpContextAccessor`
- Auto-setea `CreatedAt`/`UpdatedAt` en todas las entidades
- Audita tablas: `ss_hab_trabajador`, `ss_hab_empresa`, `ss_hab_equipo`,
  `ss_sctr_vidaley`, `ss_empresa_contratista`, `ss_equipo`, `ss_induccion`,
  `ss_eval_supervisor`
- Lee `userId`, `usuarioNombre`, `empresaId` e IP del `HttpContext`
- Columnas `datos_anteriores`/`datos_nuevos` son `jsonb` → override en `ConfigurePostgreSQL`

**Control de versiones:**
- Tabla: `ss_hab_documento_version`
- Se activa automáticamente cuando cambia `archivo_url` en un entregable
- Disponible para trabajador, empresa y equipo

**Endpoints completos:**

```
POST/GET    /api/v1/habilitacion/auth/login|empresas
POST        /api/v1/habilitacion/auth/activar
POST        /api/v1/habilitacion/auth/solicitar-reset
POST        /api/v1/habilitacion/auth/reset-password
PATCH       /api/v1/habilitacion/auth/cambiar-password   (rol CONTRATISTA)
GET/POST/PUT /api/v1/habilitacion/empresas
POST        /api/v1/habilitacion/empresas/{id}/reenviar-activacion  (admin)
GET         /api/v1/habilitacion/empresas/{id}/entregables
PUT         /api/v1/habilitacion/empresas/{id}/entregables/{itemId}
GET         /api/v1/habilitacion/catalogos/items-trabajador|items-empresa|items-equipo|criterios
GET         /api/v1/habilitacion/trabajadores
GET/PUT     /api/v1/habilitacion/trabajadores/{id}/entregables
GET         /api/v1/habilitacion/trabajadores/entregables/{id}/versiones
PATCH       /api/v1/habilitacion/trabajadores/{id}/cambiar-obra|reingreso
GET/PATCH   /api/v1/habilitacion/bandeja
GET/PATCH   /api/v1/habilitacion/bandeja/cursor
GET/POST    /api/v1/habilitacion/sctr-vidaley
PATCH       /api/v1/habilitacion/sctr-vidaley/{id}/aprobar
GET/POST/PUT/DELETE /api/v1/habilitacion/reglas
GET         /api/v1/habilitacion/auditoria
GET         /api/v1/habilitacion/archivos/ver|descargar
POST        /api/v1/habilitacion/archivos/subir
GET         /api/v1/habilitacion/registros-modelo  (público)
```

**Reglas de negocio críticas:**
- Worker solo puede pertenecer a UNA empresa activa a la vez
- Violación registrada en `ss_hab_bloqueo_log` y retorna 409
- Contratista solo ve workers de su empresa (validado por JWT claim `empresaId`)
- Contratista solo puede cambiar estado a `'Enviado'` (no puede aprobar)
- EMO es read-only — lee de `worker_emos`, no de `ss_hab_trabajador`
- SCTR/Vida Ley es masivo — un documento cubre múltiples workers
- Estado SCTR: `Aprobado`/`Rechazado`/`Parcial` según workers aprobados

**Pitfalls conocidos:**
- `WorkerVinculacion.EmpresaId` apunta a tabla legacy `companies`, NO a
  `ss_empresa_contratista.Id` — requiere correlación via `IdLegacy`
- FluentValidation 11.3.1 usa API deprecated — migrar cuando bumpeemos v12
- SharePoint: `/descargar` usa redirect 302, el browser ignora
  `Content-Disposition` en redirect (limitación inherente)
- `AuditoriaInterceptor` detecta `DateTimeOffset` vs `DateTime` por `ClrType`
  para evitar type mismatch en `SaveChanges`
- **Migración entregables** — 25,531 registros migrados desde PowerApps vía SQL a `ss_hab_trabajador`
- **Catálogo `ss_item_trabajador` extendido** — items 18-22 agregados (sprints posteriores a migración inicial)
- **`id_trabajador` en `workers`** — campo temporal usado como vinculador con el ID legacy de PowerApps; pendiente eliminar una vez consolidada la migración
