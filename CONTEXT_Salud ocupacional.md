# CONTEXTO COMPLETO - MÓDULO SALUD OCUPACIONAL

## 📋 ESTADO ACTUAL - 100% COMPLETO

| Módulo           | Backend | Frontend | Feature Key                                | Estado   |
| ---------------- | ------- | -------- | ------------------------------------------ | -------- |
| Tópico Médico    | ✅      | ✅       | `ssoma.salud-ocupacional.topico`           | COMPLETO |
| Accidentes       | ✅      | ✅       | `ssoma.salud-ocupacional.accidentes`       | COMPLETO |
| Descansos        | ✅      | ✅       | `ssoma.salud-ocupacional.descansos`        | COMPLETO |
| Asistenta Social | ✅      | ✅       | `ssoma.salud-ocupacional.asistente-social` | COMPLETO |
| Mi Salud         | ✅      | ✅       | `ssoma.salud-ocupacional.mi-salud`         | COMPLETO |

---

## 📂 ESTRUCTURA COMPLETA

### Backend

Features/SsomaModule/
├── MiSaludFeature/ ← MÓDULO INDEPENDIENTE
│ ├── Application/Dtos/MiSaludDto.cs
│ ├── Application/Interfaces/IMiSaludService.cs
│ ├── Application/Services/MiSaludService.cs
│ ├── Infrastructure/Interfaces/IMiSaludRepository.cs
│ ├── Infrastructure/Repositories/MiSaludRepository.cs
│ └── Presentation/MiSaludController.cs
│
└── SaludOcupacionalFeature/
├── Application/
│ ├── Dtos/
│ │ ├── Topico/
│ │ │ ├── TopicoAtencionDto.cs
│ │ │ ├── CrearTopicoAtencionDto.cs
│ │ │ ├── ActualizarTopicoAtencionDto.cs
│ │ │ └── TopicoFiltrosDto.cs
│ │ ├── Accidente/
│ │ │ ├── AccidenteTrabajoDto.cs
│ │ │ ├── AccidenteSeguimientoDto.cs
│ │ │ ├── AccidenteFiltrosDto.cs
│ │ │ ├── CrearAccidenteTrabajoDto.cs
│ │ │ ├── ActualizarAccidenteTrabajoDto.cs
│ │ │ ├── CambiarEstadoAccidenteDto.cs
│ │ │ └── CrearAccidenteSeguimientoDto.cs
│ │ ├── Descanso/
│ │ │ ├── DescansoMedicoDto.cs
│ │ │ ├── DescansoFiltrosDto.cs
│ │ │ ├── CrearDescansoMedicoDto.cs
│ │ │ ├── ActualizarDescansoMedicoDto.cs
│ │ │ ├── AprobarDescansoDto.cs
│ │ │ └── RechazarDescansoDto.cs
│ │ └── CasoSocial/
│ │ ├── CasoSocialDto.cs
│ │ └── SeguimientoDto.cs
│ ├── Interfaces/
│ │ ├── ITopicoService.cs
│ │ ├── IAccidenteTrabajoService.cs
│ │ ├── IDescansoMedicoService.cs
│ │ └── ICasoSocialService.cs
│ └── Services/
│ ├── TopicoService.cs
│ ├── AccidenteTrabajoService.cs
│ ├── DescansoMedicoService.cs
│ └── CasoSocialService.cs
├── Infrastructure/
│ ├── Models/
│ │ ├── TopicoAtencion.cs
│ │ ├── TopicoTipoAtencion.cs
│ │ ├── SsAccidenteTrabajo.cs
│ │ ├── SsAccidenteSeguimiento.cs
│ │ ├── SsDescansoMedico.cs
│ │ ├── SsCasoSocial.cs
│ │ └── SsCasoSocialSeguimiento.cs
│ ├── Interfaces/
│ │ ├── ITopicoRepository.cs
│ │ ├── IAccidenteTrabajoRepository.cs
│ │ ├── IDescansoMedicoRepository.cs
│ │ └── ICasoSocialRepository.cs
│ └── Repositories/
│ ├── TopicoRepository.cs
│ ├── AccidenteTrabajoRepository.cs
│ ├── DescansoMedicoRepository.cs
│ └── CasoSocialRepository.cs
└── Presentation/
├── TopicoController.cs
├── AccidenteTrabajoController.cs
├── DescansoMedicoController.cs
└── CasoSocialController.cs

text

### Frontend

src/app/features/ssoma/salud-ocupacional/
├── topico/
│ ├── topico.component.ts
│ ├── topico.component.html
│ ├── topico.component.css
│ ├── topico-modal.component.ts
│ ├── topico-modal.component.html
│ ├── topico-modal.component.css
│ ├── topico.service.ts
│ └── topico.dtos.ts
├── accidentes/
│ ├── accidentes.component.ts
│ ├── accidentes.component.html
│ ├── accidentes.component.css
│ ├── accidentes-modal.component.ts
│ ├── accidentes-modal.component.html
│ ├── accidentes-modal.component.css
│ ├── accidentes-seguimiento-modal.component.ts
│ ├── accidentes-seguimiento-modal.component.html
│ ├── accidentes-seguimiento-modal.component.css
│ ├── accidentes.service.ts
│ └── accidentes.dtos.ts
├── descansos/
│ ├── descansos.component.ts
│ ├── descansos.component.html
│ ├── descansos.component.css
│ ├── descansos-modal.component.ts
│ ├── descansos-modal.component.html
│ ├── descansos-modal.component.css
│ ├── descansos.service.ts
│ └── descansos.dtos.ts
├── asistenta-social/
│ ├── asistente-social.component.ts
│ ├── asistente-social.component.html
│ └── asistente-social.component.css
├── mi-salud/
│ ├── mi-salud.component.ts
│ ├── mi-salud.component.html
│ ├── mi-salud.component.css
│ ├── mi-salud-modal.component.ts
│ ├── mi-salud-modal.component.html
│ ├── mi-salud-modal.component.css
│ ├── mi-salud.service.ts
│ └── mi-salud.dtos.ts
├── services/
│ ├── caso-social.service.ts
│ ├── catalogos-salud.service.ts
│ ├── convalidacion.service.ts
│ ├── dashboard-salud.service.ts
│ ├── emo.service.ts
│ ├── http-base.ts
│ ├── interconsulta.service.ts
│ ├── programacion.service.ts
│ ├── reporte.service.ts
│ ├── worker-search.service.ts
│ └── worker.service.ts
├── dtos/
│ ├── caso-social.dtos.ts
│ ├── catalogos.model.ts
│ ├── convalidacion.model.ts
│ ├── dashboard-salud.model.ts
│ ├── emo.model.ts
│ ├── interconsulta.model.ts
│ ├── programacion-habilitacion.dto.ts
│ ├── programacion.model.ts
│ └── worker-search.model.ts
├── shared/
│ ├── ssoma-page-header/
│ └── worker-search-input/
├── salud-ocupacional.routes.ts
└── topico/topico.component.ts (exporta SSOMA_TABS)

text

---

## 🗄️ TABLAS EN BASE DE DATOS

### Tablas principales (con PKs correctas)

| Tabla         | PK               | Estado       |
| ------------- | ---------------- | ------------ |
| `workers`     | `id`             | ✅ Existente |
| `project`     | `project_id`     | ✅ Existente |
| `contributor` | `contributor_id` | ✅ Existente |
| `app_user`    | `user_id`        | ✅ Existente |
| `module`      | `module_id`      | ✅ Existente |
| `feature`     | `feature_id`     | ✅ Existente |

### Tablas de Salud Ocupacional

| Tabla                        | Estado    |
| ---------------------------- | --------- |
| `ss_topico_tipo_atencion`    | ✅ Creada |
| `ss_topico_atencion`         | ✅ Creada |
| `ss_accidente_trabajo`       | ✅ Creada |
| `ss_accidente_seguimiento`   | ✅ Creada |
| `ss_descanso_medico`         | ✅ Creada |
| `ss_caso_social`             | ✅ Creada |
| `ss_caso_social_seguimiento` | ✅ Creada |

### Columnas críticas

**ss_topico_atencion:**
id, worker_id, fecha, hora, tipo_atencion_id, motivo, diagnostico,
diagnostico_cie10, tratamiento, medicamentos, presion_arterial,
temperatura, frecuencia_cardiaca, saturacion_oxigeno, peso,
derivado_clinica, clinica_derivacion, genera_descanso, descanso_dias,
genera_accidente, accidente_id, proyecto_id, empresa_id, observaciones,
registrado_por_id, created_at, updated_at, state

text

**ss_accidente_trabajo:**
id, worker_id, fecha_accidente, hora_accidente, proyecto_id, empresa_id,
lugar_accidente, tipo_accidente, mecanismo, parte_cuerpo_afectada,
descripcion, descripcion_lesion, requiere_hospitalizacion,
hospital_nombre, atencion_topico_id, dias_descanso_estimados,
dias_descanso_reales, estado, fecha_alta, restricciones_reintegro,
notificado_sunafil, fecha_notificacion_sunafil, numero_notificacion_sunafil,
paso_id, url_informe, registrado_por_id, cerrado_por_id, fecha_cierre,
created_at, updated_at, state

text

**ss_descanso_medico:**
id, worker_id, tipo, fecha_inicio, fecha_fin, dias, motivo, diagnostico,
diagnostico_cie10, medico_certifica, establecimiento, url_certificado,
url_documento, estado, motivo_rechazo, aprobado_por_id, fecha_aprobacion,
accidente_id, topico_origen_id, proyecto_id, empresa_id, notificado_gth,
notificado_jefe, reportado_por_trabajador, observaciones,
registrado_por_id, created_at, updated_at, state

text

**ss_caso_social:**
id, worker_id, tipo_caso, descripcion, estado, prioridad,
fecha_apertura, fecha_cierre, descanso_id, accidente_id,
asignado_a_id, creado_por_id, created_at, updated_at, state

text

---

## ✅ FEATURE KEYS

```sql
-- Todas con module_id = 8 (SSOMA)
ssoma.salud-ocupacional.topico
ssoma.salud-ocupacional.accidentes
ssoma.salud-ocupacional.descansos
ssoma.salud-ocupacional.asistente-social
ssoma.salud-ocupacional.mi-salud
Roles asignados: role_id=12 y role_id=53

🏗️ ARQUITECTURA Y PATRONES
Backend
Framework: .NET 10

Arquitectura: Feature-based

Patrón: Controller → Service → Repository (con interfaces)

ORM: EF Core con IDbContextFactory<AppDbContext>

Soft delete: Campo state (false = eliminado)

Mapeo: Manual (NO AutoMapper - no está instalado)

Errores: AbrilException con status code

Frontend
Framework: Angular 21

Arquitectura: Standalone components

Patrón: OnPush + ChangeDetectorRef

Componentes shared: app-search-select, app-file-selector, app-file-preview, app-paginator, app-base-modal

Auth: roleGuard con featureKey, JWT

Tabs: SSOMA_TABS exportado desde topico.component.ts

Servicios compartidos
ISharePointHabService.SubirArchivoAsync(stream, fileName, contexto)

IEmailService.SendAsync(to, subject, body, isHtml, cc, bcc, attachments)

⚠️ ERRORES YA CORREGIDOS
Error	Causa	Solución
Unable to resolve service	Servicios no registrados	Registrar en SsomaModule.cs
relation does not exist	Tablas no creadas	Ejecutar SQL en pgAdmin
column X does not exist	Código vs BD no coinciden	Usar SQL exacto, NO inventar columnas
Tabs redirigen a inicio	Rutas absolutas	Usar rutas relativas (sin / al inicio)
No se veían los tabs	Tabs hardcodeados	Usar SSOMA_TABS en todos los componentes
roleGuard redirige	FeatureKey no registrada	Insertar featureKey + role_feature
⚡ REGLAS DE ORO PARA NUEVOS MÓDULOS
Backend
NO inventes columnas. Usa SOLO las que existen en la BD.

Registra servicios en SsomaModule.cs:

csharp
services.AddScoped<IRepository, Repository>();
services.AddScoped<IService, Service>();
Agrega DbSets en AppDbContext.cs.

Usa PKs correctas: project_id, user_id, contributor_id.

Soft delete: Siempre filtrar state = true.

Mapeo manual: NO AutoMapper.

Usa IDbContextFactory (NO DbContext directo).

Frontend
Rutas relativas en tabs (sin / al inicio):

typescript
{ label: 'Topico', route: 'topico' }  // ✅
Todos usan SSOMA_TABS (importado de topico.component.ts).

Agregar ruta en salud-ocupacional.routes.ts con featureKey.

Usar roleGuard en todas las rutas.

Componentes standalone con ChangeDetectionStrategy.OnPush.

🚫 LO QUE NUNCA DEBES HACER
❌ No	✅ Sí
Inventar columnas	Usar SOLO las que existen
Asumir que Claude registra servicios	Revisar SsomaModule.cs
Dejar que Claude compile	Compilar TÚ
Abrir navegador sin probar API	Probar con curl primero
Cambiar 10 cosas a la vez	Cambiar 1, probar, seguir
🚀 PENDIENTE POR IMPLEMENTAR
Tema	Descripción	Prioridad
Dashboard enriquecido	KPIs: atenciones tópico, descansos pendientes, accidentes activos, casos sociales	Media
Alertas y recordatorios	Vencimiento de descansos, próximas citas, recordatorios EMO	Media
Pruebas en producción	Validar que todo funcione en el entorno real	Alta
📋 TEMPLATE PARA NUEVOS MÓDULOS
text
Implementar [NOMBRE].

SQL EXACTO (YA EJECUTADO):
[pegar SQL]

REGLAS ESTRICTAS:
1. NO inventes columnas. Usa SOLO las del SQL.
2. Registra servicios en SsomaModule.cs.
3. Usa rutas relativas en Angular (sin / al inicio).
4. Sigue el patrón de EmoService/EmoRepository.
5. Soft delete con state = false.
6. Mapeo manual (NO AutoMapper).
7. Componentes Angular con OnPush.
8. Insertar featureKey para role_id=12 y 53.

DAME TODOS LOS ARCHIVOS DE UNA VEZ.
NO ME PREGUNTES NADA. SOLO GENERA TODO.
🔑 PALABRAS CLAVE
"NO inventes columnas"

"Usa mapeo manual, NO AutoMapper"

"Soft delete con state = false"

"Usa IDbContextFactory"

"Rutas relativas en Angular"

"Sigue patrón de EmoService"

"Registra en SsomaModule.cs"

"OnPush"

📊 RESUMEN
Métrica	Valor
Módulos completos	5
Tablas creadas	7
Feature Keys	5
Backend archivos	~50
Frontend archivos	~40
Estado	✅ 100% COMPLETO
```
