# CONTEXT.md — Abril Backend
> Pega este archivo en la raíz del proyecto. Claude Code lo leerá al inicio de cada sesión.
> Última actualización: 2026-05-04 (InduccionController completo + `GetTrabajadoresPorProgramar`; SCTR mejoras: `SctrTrabajadorEstadoDto` con `SctrId`/`SctrHabId`/`ObraOficina`, empresaId opcional, filtros multi-valor comma-separated, upload retorna URL real; FK `empresa_id` migrada a `contributor` en `ss_sctr_vidaley` y `ss_induccion`; `equipo_electrico` en `ss_induccion`; `EsAbril` mapeado en `Contributor`; auto-aprobación ítems Casa al aprobar inducción)

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
| `Project` | `project` | **Entidad ÚNICA** para nombres de proyecto. Propiedades: `ProjectId`, `ProjectDescription`, etc. Ubicada en `Shared/Models/Project.cs`. DbSet `ctx.Project`. FK destino de `worker_vinculaciones.proyecto_id`, `ss_equipo.proyecto_id`, `ss_sctr_vidaley.proyecto_id`, `ss_hab_empresa.proyecto_id`. |
| `User` | `app_user` | Override en ConfigurePostgreSQL |
| `Worker` | `workers` | Personal SSOMA |
| `WorkerEmo` | `worker_emos` | Registros EMO |
| `CatSubarea` | `cat_subarea` | Catálogo de subáreas con jefatura. Tabla creada manualmente en DB; sin migración EF (DbSet declarado en AppDbContext). |
| `SsItemTrabajador` | `ss_item_trabajador` | Catálogo de entregables con reglas |
| `SsHabTrabajador` | `ss_hab_trabajador` | Entregables por trabajador |
| `SsHabDocumentoVersion` | `ss_hab_documento_version` | Historial versiones — campos nuevos 2026-04-30: `proyecto_id`, `empresa_id`, `estado_anterior`, `aprobado_por_user_id`, `motivo_rechazo` |
| `WorkerEvento` | `worker_eventos` | Log ciclo de vida worker — creada manualmente en BD, sin migración EF |
| `WorkerProyecto` | `ss_hab_worker_proyecto` | **Asignación multi-proyecto de un worker Casa**. Permite que un trabajador esté activo simultáneamente en N proyectos (a diferencia de `worker_vinculaciones` que es 1-activa-a-la-vez). Campos: `WorkerId`, `ProyectoId`, `EmpresaId?`, `FechaInicio`, `FechaFin?` (null = activa), `InduccionCompletada`, `FechaInduccion?`, `CreatedAt`, `UpdatedAt?`. Migración `20260504022041_AddWorkerProyectoTable`. **Solo workers Casa**: `AgregarProyectoAsync` valida `ContrataCasa == "Casa"` (400 si no). Unique partial index `(worker_id, proyecto_id) WHERE fecha_fin IS NULL` impide doble asignación activa al mismo proyecto. FKs: `worker_id → workers(id) CASCADE`, `proyecto_id → project(project_id) RESTRICT`, `empresa_id → contributor(id) SET NULL`. |
| `Contributor` | `contributor` | **Entidad unificada de empresas** (reemplaza `companies` eliminada). Incluye `es_abril` (bool, mapeado como `public bool EsAbril { get; set; }` en `Features/CostsModule/Shared/Models/Contributor.cs`) e `id_sharepoint` (int?, temporal para migración). `EmpresaId` en `worker_vinculaciones`, `ss_empresa_proyecto`, `ss_hab_empresa`, `ss_sctr_vidaley` y `ss_induccion` apunta a `contributor.id`. |
| `SsInduccion` | `ss_induccion` | Programación y aprobación de inducciones. Campos: `WorkerId`, `ProyectoId`, `EmpresaId` (→ `contributor`), `FechaProgramada`, `TrabajoAltura`, `EquipoElectrico` (bool, default false — creado manualmente en BD), `Estado` (`"PROGRAMADA"` / `"REALIZADA"`), `ProgramadoPor`, `CreatedAt`, `UpdatedAt`. |

### ⚠️ Pitfall histórico: `Project` (legacy) vs `Projects` (eliminada)

**Resuelto el 2026-04-30** vía migración `20260430053121_SwitchProyectoFkToProjectLegacy` + eliminación de `Infrastructure/Models/Projects.cs`. La entidad `Projects` (plural, con `Id`/`Nombre`) **ya no existe en código**. Toda referencia a proyectos por ID se resuelve contra `ctx.Project` (legacy, con `ProjectId`/`ProjectDescription`).

```csharp
// ✅ ÚNICO patrón válido para resolver cualquier proyecto_id
var proyectos = await ctx.Project
    .Where(p => proyectoIds.Contains(p.ProjectId))
    .Select(p => new { p.ProjectId, p.ProjectDescription })
    .ToListAsync();
```

**Histórico del bug** (commits `8a3e317` → `4e49442` → `d4db179`): originalmente había dos entidades C# que apuntaban a dos tablas PG con IDs solapados; los joins compilaban y "funcionaban" silenciosamente contra la tabla equivocada. Caso emblemático: GODENZI (worker con `proyecto_id=10`) devolvía "Gran Manzano" en vez de "Oficina Central". Tras revertir el código y migrar el schema, todas las FKs apuntan a `project.project_id` consistentemente.

**Si en el futuro EF intenta regenerar `Projects`**: probablemente porque alguien restauró la navegación `Projects? Proyecto` en `WorkerVinculacion`. Verificar que esa navegación esté removida o apunte explícitamente a `Project` legacy.

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
| 25 | Lectura de EMO | CASA | | | | true | SSOMA |

> **Item 25 — Lectura de EMO**: excluido del set virtual `emoItems` en `GetEntregablesWorkerAsync` (se trata como documento real, no como virtual de worker_emos). Sí está incluido en `itemsEmoIds` (filtro por nombre "EMO") → excluido del cálculo de bloqueo para workers CASA.

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
GET         /api/v1/habilitacion/empresas/{empresaId}/entregables?proyectoId=&mes=&anio=
PUT         /api/v1/habilitacion/empresas/{empresaId}/entregables/{id}
GET         /api/v1/habilitacion/empresas/{empresaId}/proyectos-disponibles
POST        /api/v1/habilitacion/empresas/{empresaId}/activar-proyecto      ← body { proyectoId }
DELETE      /api/v1/habilitacion/empresas/{empresaId}/desactivar-proyecto   ← body { proyectoId }, soft delete (activo=false, fecha_fin=hoy)
GET         /api/v1/habilitacion/catalogos/items-trabajador|items-empresa|items-equipo|criterios
GET         /api/v1/habilitacion/catalogos/areas              (público)
GET         /api/v1/habilitacion/catalogos/subareas?area=     (público)
GET         /api/v1/habilitacion/proyectos                    (lista activos desde Project legacy)
GET         /api/v1/habilitacion/trabajadores?search=&empresaId=&proyectoId=&estadoHabilitacion=&contratistaCasa=&soloRetirados=false
GET         /api/v1/habilitacion/trabajadores/{id}            (detalle completo WorkerDetalleDto)
PUT         /api/v1/habilitacion/trabajadores/{id}            (PATCH semantics — solo asigna campos non-null)
POST        /api/v1/habilitacion/trabajadores/{id}/inicializar
GET/PUT     /api/v1/habilitacion/trabajadores/{id}/entregables
GET         /api/v1/habilitacion/trabajadores/entregables/{id}/versiones
PATCH       /api/v1/habilitacion/trabajadores/{id}/baja                ← body { fechaRetiro?: DateOnly }
PATCH       /api/v1/habilitacion/trabajadores/baja-masiva              ← body { ids: int[], fechaRetiro?: DateOnly }
PATCH       /api/v1/habilitacion/trabajadores/{id}/cambiar-obra        ← body WorkerCambiarObraDto (resetea entregables + correos + eventos + sincroniza ss_hab_worker_proyecto)
PATCH       /api/v1/habilitacion/trabajadores/{id}/reingreso           ← body { nuevoProyectoId?, nuevaEmpresaId?, fechaReingreso? } (sincroniza ss_hab_worker_proyecto)
GET         /api/v1/habilitacion/trabajadores/{id}/eventos             ← [AllowAnonymous] temporal
POST        /api/v1/habilitacion/trabajadores/{id}/proyectos                       ← AgregarProyectoDto, multi-proyecto, [AllowAnonymous] temporal
GET         /api/v1/habilitacion/trabajadores/{id}/proyectos                       ← lista activos+históricos, [AllowAnonymous] temporal
DELETE      /api/v1/habilitacion/trabajadores/{id}/proyectos/{proyectoId}          ← retira (FechaFin = hoy), [AllowAnonymous] temporal
PATCH       /api/v1/habilitacion/trabajadores/{id}/proyectos/{proyectoId}/induccion ← marca InduccionCompletada=true, [AllowAnonymous] temporal
GET/PATCH   /api/v1/habilitacion/bandeja
GET/PATCH   /api/v1/habilitacion/bandeja/cursor
GET/POST    /api/v1/habilitacion/sctr-vidaley
PATCH       /api/v1/habilitacion/sctr-vidaley/{id}/aprobar
GET         /api/v1/habilitacion/sctr-vidaley/trabajadores-por-empresa?empresaId=&estadoSctr=&estadoVidaLey=
            ← empresaId opcional (sin él → todas las empresas); estadoSctr/estadoVidaLey aceptan valores separados por coma (ej: "Falta,Vencido"); retorna SctrTrabajadorEstadoDto con SctrId, SctrHabId, ObraOficina, EmpresaNombre
POST        /api/v1/habilitacion/inducciones          ← body InduccionCreateDto (WorkerIds[], ProyectoId, EmpresaId?, FechaProgramada, TrabajoAltura, EquipoElectrico)
GET         /api/v1/habilitacion/inducciones?proyectoId=&empresaId=&estado=&fechaDesde=&fechaHasta=
GET         /api/v1/habilitacion/inducciones/trabajadores-por-programar?proyectoId=X[&empresaId=Y]
            ← retorna workers activos sin InduccionCompletada=true en ss_hab_worker_proyecto para ese proyecto; InduccionTrabajadorDto
PATCH       /api/v1/habilitacion/inducciones/{id}/aprobar
PATCH       /api/v1/habilitacion/inducciones/aprobar-batch   ← body { ids: int[] }
GET/POST/PUT/DELETE /api/v1/habilitacion/reglas
GET         /api/v1/habilitacion/auditoria
POST        /api/v1/habilitacion/archivos/subir       ← retorna { path, url } donde url es la URL real de descarga de SharePoint
GET         /api/v1/habilitacion/archivos/url?path=   ← URLs absolutas legacy pasan directamente; relativas via Graph /content + 302
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

### Estado actual del módulo (2026-05-04)

- **Sprint trabajador multi-proyecto (FASE 1+2+3)** — `ss_hab_worker_proyecto` permite N proyectos activos en paralelo por worker Casa. Convive con `worker_vinculaciones` (que sigue siendo 1-activa-a-la-vez); ambas se sincronizan vía `SincronizarWorkerProyectoCambioAsync`. 4 endpoints nuevos (`POST/GET/DELETE/PATCH /proyectos`) + integración con flujos existentes.
- **Sincronización en flujos existentes**:
  - `CambiarObraAsync` y `ReingresoAsync` (gate `esCambioProyecto && !esContratista`): cierran fila activa del proyecto anterior; si existe fila previa cerrada del proyecto destino, **la reactivan preservando `InduccionCompletada` y `FechaInduccion`**; si no existe, crean nueva con `InduccionCompletada=false`. No tocan `EmpresaId` al reactivar.
  - `BajaAsync` y `BajaMasivaAsync`: cierran TODAS las filas activas del worker (`FechaFin = fechaRetiro`).
  - `InicializarEntregablesAsync` no toca `ss_hab_worker_proyecto` — la asignación inicial debe crearse vía `POST /proyectos` o `CambiarObraAsync`.
- **Reset al cambiar de proyecto reducido** — `CambiarObraAsync` y `ReingresoAsync` (rama `esCambioProyecto`) ahora **solo resetean `ItemInduccionObra`** a "Falta". Removidos del set: `ItemRisst`, `ItemRegistroEpp`, `ItemDifusionPts`, `ItemEntregaRecomendaciones`, `ItemTRegistro`. Consecuencia en correos: el body a `EmailCoordSsoma` ahora muestra solo `"• Inducción Obra"` (sin ternario por contratista/casa) y el correo dedicado a `EmailCoordAdmin` por T-Registro fue eliminado de ambos métodos. Bloque `esCambioEmpresa` (SCTR/Vida Ley/Cert. Aptitud) intacto.
- **Correos Vida Ley por cambio de cargo (Casa)** — `UpdateAsync` detecta transición `Categoria == "Practicante" → otro cargo`; si no existe ya, inserta entregable `ItemVidaLey` y notifica a `EmailAsistentaSocial` (`pquispe@abril.pe`).
- **Correos Vida Ley por cambio ObraOficina (Casa)** — `UpdateAsync` detecta transición ObraOficina → `"Staff"` (correo a `EmailCoordAdmin` del proyecto activo) o → `"Oficina Central"` (correo a `EmailAsistentaSocial`). Solo notificación, no crea entregable.
- **Practicante exclusión Vida Ley** — `InicializarEntregablesAsync` filtra `ItemVidaLey` cuando `workerType == "CASA" && Categoria == "Practicante"` (case-insensitive). Cuando deja de ser Practicante, `UpdateAsync` lo agrega.
- **Prefijo `[PRUEBA - NO TOMAR EN CUENTA]`** en subjects de correos del módulo HabilitacionModule cuando el worker es Casa (Abril). Variable `prefijoSubject = esContratista ? "" : "[PRUEBA - NO TOMAR EN CUENTA] "` en `CambiarObraAsync` y `ReingresoAsync`. Aplicado siempre en correos nuevos de Vida Ley (cargo/obra-oficina) y Nuevo Proyecto (multi-proyecto). Pendiente: quitar antes de prod real.
- **InduccionController (2026-05-04)** — módulo completo en `Features/HabilitacionModule/`. `POST /inducciones` programa batch de inducciones (omite workers que ya tienen estado `PROGRAMADA` en ese proyecto). `GET /inducciones` lista con filtros. `GET /inducciones/trabajadores-por-programar?proyectoId=X[&empresaId=Y]` retorna workers activos en `worker_vinculaciones` (FechaFin==null) que NO tienen `InduccionCompletada=true` en `ss_hab_worker_proyecto` para ese proyecto; workers con inducción en otro proyecto sí aparecen (necesitan homologación). `PATCH /{id}/aprobar` y `PATCH /aprobar-batch` marcan estado `REALIZADA`, actualizan `ss_hab_trabajador` (siempre `ItemInduccionObra=12`; si `es_abril=true` también `RegistroEpp=5`, `Risst=6`, `EntregaRecomendaciones=8`, `DifusionPts=10`) y marcan `InduccionCompletada=true` + `FechaInduccion` en `ss_hab_worker_proyecto`. Registrado en `HabilitacionModule.cs` como `IInduccionRepository`/`InduccionRepository`.
- **FK empresa_id migrada a contributor (2026-05-04)** — `ss_sctr_vidaley.empresa_id` y `ss_induccion.empresa_id` ahora referencian `contributor.contributor_id` directamente. Eliminada la resolución intermedia via `ss_empresa_contratista`. `empresaNombre` en `SctrVidaLeyRepository.BuildDtosAsync` se obtiene con `ctx.Contributor.ContributorName`.
- **`equipo_electrico` en `ss_induccion`** — columna bool añadida manualmente en BD (`ALTER TABLE ss_induccion ADD COLUMN equipo_electrico boolean NOT NULL DEFAULT false`). Migración vacía `20260504220000_AddInduccionEquipoElectrico`. Mapeada en `SsInduccion` como `public bool EquipoElectrico { get; set; }`.
- **`EsAbril` en `Contributor`** — propiedad mapeada directamente en `Features/CostsModule/Shared/Models/Contributor.cs` (`[Column("es_abril")] public bool EsAbril { get; set; }`). Antes solo existía como campo computado en CatalogosRepository. Usada en `InduccionRepository.AprobarInduccionAsync` y `SctrVidaLeyRepository.CreateAsync` para determinar auto-aprobación de ítems Casa.
- **SCTR mejoras (2026-05-04)** — `SctrTrabajadorEstadoDto` incluye `ObraOficina`, `SctrId` (póliza activa con estado `"Enviado"` o `"Parcial"` más reciente) y `SctrHabId` (id de `ss_hab_trabajador` para item SCTR). `GetTrabajadoresPorEmpresaAsync` acepta `empresaId` como `int?` (sin él retorna todos). Filtros `estadoSctr`/`estadoVidaLey` aceptan valores separados por coma (incluye decodificación de `%2C`). `POST /archivos/subir` retorna `{ path, url }` con URL real de SharePoint via `GetDownloadUrlAsync`.
- **Baja individual y masiva de trabajadores** — `BajaAsync` / `BajaMasivaAsync`: marcan worker con `Estado = "RETIRADO"` y `FechaRetiro`, cierran vinculación activa
- **Reingreso** — `ReingresoAsync`: reactiva worker, crea nueva vinculación; si cambia proyecto o empresa, resetea entregables según tipo (CONTRATISTA: siempre; CASA: solo si cambia proyecto), envía correos, inserta eventos
- **CambiarObraAsync** — misma lógica que reingreso: resets de entregables + correos + eventos; `NuevoProyectoId` es no-nullable, comparar directamente sin `.HasValue`
- **`worker_eventos`** — tabla creada manualmente en BD (sin migración EF). Eventos: `BAJA`, `REINGRESO`, `CAMBIO_OBRA`, `CAMBIO_EMPRESA`, `ENTREGABLE_RESETEADO`. Campos clave: `proyecto_anterior_id`, `proyecto_nuevo_id`, `empresa_anterior_id`, `empresa_nueva_id`, `datos` (jsonb), `usuario_id`
- **`ss_hab_documento_version` enriquecida** — 5 campos nuevos: `proyecto_id`, `empresa_id`, `estado_anterior`, `aprobado_por_user_id`, `motivo_rechazo`. Populados en `UpdateEntregableAsync` al subir archivo, aprobar o rechazar
- **`WorkerHabilitacionListDto`** incluye `ContrataCasa` y `ObraOficina`
- **`GetWorkersHabilitacionAsync`** acepta `soloRetirados`: `true` = solo `RETIRADO`; `false` (default) = excluye `RETIRADO`. `LatestVinc` no filtra por `fecha_fin` — devuelve la última vinculación aunque esté cerrada (para mostrar empresa/proyecto de retirados)
- **Item 25 "Lectura de EMO"** — CASA, `requiere_vigencia = true`. Excluido del virtual `emoItems`, incluido en `itemsEmoIds`
- **Migración `contributor`** — tabla `companies` eliminada de BD. `worker_vinculaciones.empresa_id`, `ss_empresa_proyecto.empresa_id`, `ss_hab_empresa.empresa_id` ahora apuntan a `contributor`. 281 empresas migradas (23 Abril + 258 contratistas), 428 relaciones `ss_empresa_proyecto`, 10.506 entregables `ss_hab_empresa`, 868 `worker_vinculaciones`. `contributor` tiene `es_abril` (bool) e `id_sharepoint` (int?, temporal)
- **HabEmpresa — activación en proyecto** — `POST /activar-proyecto` valida empresa + proyecto + no-duplicado → inserta `ss_empresa_proyecto` + llama `InicializarEntregablesEmpresaAsync`. `GET /proyectos-disponibles` retorna todos con flag `estaActiva`. `DELETE /desactivar-proyecto` soft-delete

### Pitfalls conocidos

- `SharePointHabService` debe ser **Singleton** — el token y driveId se cachean en instancia
- **Todos los `proyecto_id` de tablas nuevas (`worker_vinculaciones`, `ss_equipo`, `ss_sctr_vidaley`, `ss_hab_empresa`) apuntan a `project.project_id` legacy**. Resolver siempre vía `ctx.Project` con `ProjectId`/`ProjectDescription`. La entidad `Projects` (plural) fue eliminada el 2026-04-30 vía migración `SwitchProyectoFkToProjectLegacy`. Ver sección 6.
- **Al consultar `worker_vinculaciones` activas, siempre `ORDER BY created_at DESC, id DESC`**. `fecha_inicio` no es único y EF/PG no garantiza orden estable sin tie-breaker → puede devolver vinculación incorrecta cuando un worker tiene varias filas con `fecha_fin IS NULL`. Para JOINs en SQL crudo, usar `LEFT JOIN LATERAL (... ORDER BY created_at DESC, id DESC LIMIT 1) ON TRUE` para evitar duplicar filas base.
- **`LatestVinc` en `GetWorkersHabilitacionAsync` NO filtra por `fecha_fin`** — devuelve la última vinculación independientemente de si está cerrada, para mostrar empresa/proyecto de workers retirados
- `worker_vinculaciones.empresa_id`, `ss_empresa_proyecto.empresa_id` y `ss_hab_empresa.empresa_id` apuntan a `contributor.id`. La tabla `companies` fue eliminada. **No usar** `SsEmpresaContratista` ni `companies` para resolver `empresa_id` en estas tablas — usar `contributor`
- **`worker_eventos` creada manualmente en BD** (igual que `cat_subarea`) — `DbSet` declarado en AppDbContext con `HasColumnType("jsonb")` para `Datos`. No generar migración EF para esta tabla
- **`contributor.id_sharepoint`** es columna temporal para migración SharePoint. 42 empresas con IDs 1656+ pendientes. Eliminar cuando la migración esté completa
- **`HabilitacionDateHelper.AsUtc(dto.Fecha)` obligatorio** para todo `DateTime` que venga de un DTO antes de asignarlo a una columna `timestamp with time zone`. JSON deserializa fechas sin `Z` como `Kind=Unspecified` y Npgsql las rechaza con `"Cannot write DateTime with Kind=Unspecified to PostgreSQL type 'timestamp with time zone', only UTC is supported"`. Ya aplicado en `HabTrabajadorRepository`, `EquipoRepository`, `HabEmpresaRepository`, `SctrVidaLeyRepository`, `BandejaRepository`. Para el sentinel "sin vencimiento" usar `DateTime.SpecifyKind(new DateOnly(2040,12,31).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)` (ya encapsulado en `HabilitacionDateHelper.ResolverVigencia`).
- **Patch semantics en `UpdateEntregableAsync`**: solo asignar `ArchivoUrl`/`ObsAbril`/`ObsContratista` cuando `dto.X is not null`. Si el frontend manda `{ estado: "Aprobado", vigencia: "..." }` sin esos campos, **NO** los pises con null — borrarías el documento subido. Mismo principio en `UpdateAsync` de `WorkerUpdateDto` (todos los campos `is not null` o `HasValue`).
- **`WorkerEntregableUpdateValidator.EstadosValidos`** debe incluir `"En Plazo"` y `"Vencido"` además de `Falta`/`Enviado`/`Aprobado`/`Rechazado`/`No Aplica`. El sistema usa los 7.
- `BandejaRepository.SelectBase` segmento TRABAJADOR usa `LEFT JOIN project p ON p.project_id = wv.proyecto_id` con `p.project_description AS proyecto_nombre`. Los segmentos UNION ALL de EMPRESA y EQUIPO aún apuntan a `projects` plural — pendientes de revertir cuando esas tablas tengan datos.
- **`ss_sctr_vidaley.empresa_id` y `ss_induccion.empresa_id` → `contributor`**: NO resolver via `ss_empresa_contratista`. El `empresaId` que llega en los DTOs ya es `contributor_id`. La tabla `ss_empresa_contratista` (si existe) ya no es la FK destino para estas columnas.
- **`SctrVidaLeyRepository.sctrIdPorWorker`**: filtra por `s.Estado == "Enviado" || s.Estado == "Parcial"` antes de tomar el `Max`. Sin este filtro, pólizas en estado `"Pendiente"` o rechazadas contaminarían el lookup de póliza activa.
- **`InduccionRepository.GetTrabajadoresPorProgramarAsync`**: la exclusión se basa en `WorkerProyecto.InduccionCompletada == true && FechaFin == null` para el `proyectoId` dado. Un worker retirado de ese proyecto (con `FechaFin != null`) **sí reaparece** si vuelve a ser vinculado — comportamiento correcto porque la inducción previa ya no está activa.
- **`equipo_electrico` creada manualmente en BD**: columna de `ss_induccion` sin migración EF efectiva. Si se genera una nueva migración, EF **no** detectará esta columna como pendiente (el snapshot ya la tiene vía la migración vacía). No regenerar la migración vacía.
- FluentValidation 11.3.1 usa API deprecated — migrar cuando bumpeemos v12
- `AuditoriaInterceptor` debe ser **Singleton** — inyectar `IServiceScopeFactory`
- `datos_anteriores`/`datos_nuevos` son `jsonb` → `HasColumnType("jsonb")` en `ConfigurePostgreSQL`
- `id_trabajador` en `workers` es campo temporal (vinculador PowerApps legacy) — pendiente eliminar
- **`ProjectService` exige `ISunatService` en su constructor**: cualquier endpoint del módulo Project (ej. `GET /paged`) instancia toda la cadena DI. Si `Sunat:BaseUrl` falta, el HttpClient revienta al inicializar y rompe endpoints sin relación con Sunat. Mitigado en Program.cs: la factory chequea `string.IsNullOrEmpty(baseUrl)` antes de setear `BaseAddress` — el backend arranca sin Sunat configurado y solo `/company-lookup/{ruc}` falla en runtime.
- **`ss_hab_worker_proyecto` solo aplica a Casa**: `AgregarProyectoAsync` valida `ContrataCasa == "Casa"` (400 si no). La sincronización en `CambiarObraAsync`/`ReingresoAsync` está gateada con `!esContratista` por la misma razón. Los contratistas siguen el modelo 1-vinculación-activa de `worker_vinculaciones` exclusivamente.
- **Reactivación de fila previa preserva `InduccionCompletada`, `FechaInduccion` y `EmpresaId` históricos**. Si el worker había completado la inducción de obra X, fue retirado y vuelve a X, **no** se le pide inducción nueva. Si esto cambia (p.ej. inducción válida solo 12 meses), agregar lógica de expiración en `SincronizarWorkerProyectoCambioAsync`. Tampoco se actualiza `EmpresaId` al reactivar — si el worker volvió a X bajo otra empresa, queda la histórica.
- **Anti-duplicado defensivo en sincronización**: el helper hace `AnyAsync(activa nueva)` antes de insertar para evitar pelear con el unique partial index `(worker_id, proyecto_id) WHERE fecha_fin IS NULL`. Si el chequeo dice que ya existe activa, hace early return sin tocar nada — útil para idempotencia pero también enmascara casos raros donde la activa apunta a empresa/fecha distinta.
- **Migración `AddWorkerProyectoTable` se editó a mano** para remover el ruido del scaffold EF — `dotnet ef migrations add` detectó como pendientes 5 `AddColumn` a `ss_hab_documento_version` y `CreateTable cat_subarea`/`worker_eventos` (deuda histórica del snapshot porque esos cambios se aplicaron en BD sin pasar por EF). El snapshot quedó actualizado, así que próximas migraciones no regenerarán ese ruido — pero **antes de correr `migrations add` siempre revisar el archivo generado** y limpiar operaciones que ya estén en BD.
- **Workers Casa con cargo "Practicante"**: `InicializarEntregablesAsync` filtra `ItemVidaLey` (id 13). La transición Practicante→otro cargo en `UpdateAsync` agrega el entregable e invoca correo a `EmailAsistentaSocial`. La transición inversa (otro→Practicante) **no elimina** el entregable existente — queda histórico en `ss_hab_trabajador`.
- **Correos de cambio ObraOficina**: solo se disparan a `"Staff"` (admin del proyecto activo) o `"Oficina Central"` (asistenta social). Otros valores (`"Obra X"`, etc.) no notifican. La resolución del proyecto activo usa el patrón estable de `GetEmpresaActivaWorkerAsync` (vinculación con `FechaFin == null` ordenada por `CreatedAt + Id DESC`).

---

## 9. Trabajo pendiente

### Alta prioridad
- **Auth real** — quitar `[AllowAnonymous]` de SSOMA, `ProjectController`, `GET /trabajadores/{id}/eventos` y los 4 endpoints `/trabajadores/{id}/proyectos*`
- **Quitar prefijo `[PRUEBA - NO TOMAR EN CUENTA]`** de subjects antes de prod real (en `HabTrabajadorRepository`: `prefijoSubject` en CambiarObra/Reingreso, hardcoded en correos de Vida Ley cargo/obra-oficina y Nuevo Proyecto)
- **Crear primer usuario admin** en `app_user`
- **Deploy a producción**
- **42 empresas SharePoint con IDs 1656+** pendientes de migrar a `contributor` (necesita Excel actualizado con los datos correctos)
- **Eliminar `id_sharepoint`** de `contributor` cuando la migración SharePoint esté completa

### Media prioridad
- **Empresas contratistas** — 1,591 vinculaciones sin empresa
- **tipo_emo_id** — los 813 EMOs migrados tienen NULL
- **Eliminar `id_trabajador`** de `workers` tras confirmar migración completa
- **`BandejaRepository` segmentos UNION ALL EMPRESA y EQUIPO** — siguen apuntando a `JOIN projects p ON p.id = …`. Cuando `ss_hab_empresa` y `ss_equipo` tengan datos con la nueva FK a `project`, revertir a `JOIN project p ON p.project_id = …` (mismo patrón que ya tiene el segmento TRABAJADOR).
- **`ProjectService` acoplamiento con `ISunatService`** — separar a `ISunatLookupService` para que solo `/company-lookup/{ruc}` lo necesite. Hoy mitigado con factory null-safe pero la dependencia sigue en el constructor.
- **Multi-proyecto FASE 4 (consumidores)**: `BandejaRepository`, listados, EMO, SCTR y Vida Ley aún razonan exclusivamente sobre `worker_vinculaciones` (1-activa). Decidir si y cómo pivotar lecturas a `ss_hab_worker_proyecto` para reflejar a workers Casa que están en N proyectos simultáneos.
- **`InicializarEntregablesAsync` no crea fila inicial en `ss_hab_worker_proyecto`** — al crear un worker nuevo, su asignación a un primer proyecto debe hacerse vía `POST /proyectos` o `CambiarObraAsync`. Considerar agregar parámetro `proyectoInicialId?` al endpoint `POST /workers` para encadenarlo.
- **`Project` no tiene campo `id_sharepoint`** (a diferencia de `Contributor`). Si se necesita correlacionar proyectos con su origen en SharePoint para futuras migraciones, agregar columna + override en `ConfigurePostgreSQL`.

### Baja prioridad
- **Refactor `Sunat:Token` y `Sunat` headers** en Program.cs — quedaron fuera del fix null-safe; restaurar dentro del `if` cuando se confirme que en producción siempre hay configuración.
- 8 EMOs sin match de DNI — insertar manualmente
- 24 vinculaciones sin proyecto
- `ReminderController.cs` aún usa `Environment.GetEnvironmentVariable` para CronSecret — migrar
