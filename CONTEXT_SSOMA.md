# CONTEXT_SSOMA.md — SSOMA Intelligence Platform

> Última actualización: 2026-06-11 — PasoHistoricoAnioDto creado; GetHistoricoProyectoAsync devuelve ese DTO con filtro de ciclo y lógica de vencidas por fin de mes. GetSpiAsync también filtra por ciclo [cicloStart,cicloEnd] y usa misma lógica de vencidas. 20 endpoints activos.
> Pegar este archivo al inicio de cada chat nuevo cuando se trabaje en este módulo.

---

## 1. Visión general

**Nombre del módulo**: SSOMA Intelligence Platform
**Objetivo**: Migrar todas las PowerApps de SSOMA a la plataforma Abril (Angular 21 + .NET 10 + PostgreSQL). No es una migración literal — es una reescritura mejorada con estándares internacionales (ISO 45001, Ley 29783, OSHA, Ley 28611).
**Referencia de mercado**: Intelex, Cority, SafetyCulture — customizado para construcción inmobiliaria peruana.
**Backend**: Feature slice `SsomaGestionModule` dentro del `SsomaModule` existente.
**Ruta base API**: `api/v1/ssoma-paso`, `api/v1/ssoma-presupuesto`, etc. (una ruta base por feature).
**PowerApps siguen funcionando** como respaldo mientras se migra módulo por módulo.

---

## 2. Los 17 módulos — prioridad y estado

### Fase 1 — Core operativo (P1)
| # | Nombre | Estado |
|---|---|---|
| 1 | Gestión de Inspecciones | Por iniciar |
| 2 | Incidentes & No Conformidades | Por iniciar |
| 3 | RAC — Actos & Condiciones Subestándar | Por iniciar |
| 4 | OPT — Observación Planeada de Tarea | Por iniciar |
| 5 | ATS — Análisis de Trabajo Seguro | Por iniciar |

### Fase 2 — Gestión y control (P2)
| # | Nombre | Estado |
|---|---|---|
| 6 | Charlas & Capacitaciones | Por iniciar |
| 7 | **PASO — Programa Anual de Seguridad** | **CONSTRUIDO ✓** |
| 8 | Amonestaciones & Sistema de Puntaje | Por iniciar |
| 9 | Cierre de Dossier | Por iniciar |
| 10 | Penalidades SSOMA | Por iniciar |
| 11 | Gestión de Residuos Sólidos | Por iniciar |

### Fase 3 — Estratégico & compliance (P3)
| # | Nombre | Estado |
|---|---|---|
| 12 | Dashboard de Indicadores | Por iniciar |
| 13 | Cursos Críticos | Por iniciar |
| 14 | Matriz de Cumplimiento Legal | Por iniciar |
| 15 | Guías & Checklists | Por iniciar |
| 16 | Control de Presupuesto SSOMA | Diseñado — ver sección 4 |
| 17 | IPER — Mapa de Riesgos (nuevo) | Por iniciar |

---

## 3. PASO — Programa Anual de Seguridad, Salud Ocupacional y Medio Ambiente

### 3.1 Descripción
Gestión del programa anual SSOMA con tres ámbitos: **Seguridad**, **Salud Ocupacional** y **Medio Ambiente**. Modelo corporativo + por proyecto: existe una plantilla corporativa que se instancia por obra. Gantt anual, control de avance con SPI, alertas automáticas y evidencias en SharePoint.

### 3.2 Decisiones de arquitectura confirmadas
- **Modelo**: Corporativo + por proyecto. Plantilla corporativa → se clona/instancia por obra.
- **Evidencias**: SharePoint (mismo patrón que EMOs del módulo Clínica).
- **Ámbitos**: Seguridad | Salud Ocupacional | Medio Ambiente (columna `ambito` en categorías).
- **Gantt**: `dhtmlx-gantt` (ya instalado en frontend).
- **Alertas**: cronjob diario igual que `revisar-vigencias` de EMOs.
- **Generación automática**: el cronjob del día 1 de cada mes genera las instancias de ejecución según frecuencia de cada actividad.

### 3.3 Flujo principal
```
Crear plantilla corporativa
  → Definir categorías + actividades por ámbito (S / SO / MA)
  → Aprobar plantilla
  → Instanciar por proyecto (clonar con fechas ajustadas al año/proyecto)
  → Aprobar PASO del proyecto
  → Ejecutar actividades mes a mes (registrar ejecución + evidencia SharePoint)
  → Control mensual (SPI, alertas vencidos)
  → Cierre anual + reporte
```

### 3.4 Entidades BD

```sql
-- Categorías por ámbito (catálogo)
CREATE TABLE ssoma_paso_categoria (
    id SERIAL PRIMARY KEY,
    nombre VARCHAR(100) NOT NULL,
    ambito VARCHAR(20) NOT NULL, -- Seguridad | Salud | Ambiente
    icono VARCHAR(50),
    activo BOOLEAN NOT NULL DEFAULT TRUE
);

INSERT INTO ssoma_paso_categoria (nombre, ambito, icono) VALUES
-- Seguridad
('Inspecciones de seguridad',    'Seguridad',  'ti-clipboard-check'),
('Simulacros de emergencia',     'Seguridad',  'ti-alarm'),
('Auditorías internas SSOMA',    'Seguridad',  'ti-search'),
('Reuniones de seguridad',       'Seguridad',  'ti-users'),
('Señalización y EPP',           'Seguridad',  'ti-shield'),
('Permisos de trabajo',          'Seguridad',  'ti-file-check'),
-- Salud Ocupacional
('Exámenes médicos ocupacionales','Salud',     'ti-stethoscope'),
('Monitoreo de agentes físicos', 'Salud',      'ti-wave-sine'),
('Monitoreo de agentes químicos','Salud',      'ti-flask'),
('Monitoreo de agentes biológicos','Salud',    'ti-virus'),
('Campañas de salud',            'Salud',      'ti-heart-rate-monitor'),
('Ergonomía',                    'Salud',      'ti-armchair'),
-- Medio Ambiente
('Gestión de residuos sólidos',  'Ambiente',   'ti-recycle'),
('Monitoreo de calidad del aire','Ambiente',   'ti-wind'),
('Monitoreo de ruido ambiental', 'Ambiente',   'ti-ear'),
('Control de efluentes',         'Ambiente',   'ti-droplet'),
('Gestión de sustancias peligrosas','Ambiente','ti-flask-2'),
('Reporte ambiental OEFA',       'Ambiente',   'ti-file-certificate');

-- Programa (plantilla corporativa o instancia por proyecto)
CREATE TABLE ssoma_paso (
    id SERIAL PRIMARY KEY,
    proyecto_id INT REFERENCES project(project_id), -- NULL = plantilla corporativa
    plantilla_id INT REFERENCES ssoma_paso(id),      -- NULL = es plantilla, sino = instancia
    nombre VARCHAR(200) NOT NULL,
    anio INT NOT NULL,
    es_plantilla BOOLEAN NOT NULL DEFAULT FALSE,
    estado VARCHAR(20) NOT NULL DEFAULT 'Borrador', -- Borrador | Aprobado | Activo | Cerrado
    aprobado_por INT REFERENCES app_user(id),
    aprobado_en TIMESTAMP,
    created_by INT REFERENCES app_user(id),
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP
);

-- Actividades del programa
CREATE TABLE ssoma_paso_actividad (
    id SERIAL PRIMARY KEY,
    paso_id INT NOT NULL REFERENCES ssoma_paso(id) ON DELETE CASCADE,
    categoria_id INT NOT NULL REFERENCES ssoma_paso_categoria(id),
    nombre VARCHAR(300) NOT NULL,
    descripcion TEXT,
    alcance TEXT,
    frecuencia VARCHAR(20) NOT NULL, -- Mensual | Bimestral | Trimestral | Semestral | Anual | Unica
    responsable_id INT REFERENCES app_user(id),
    responsable_texto VARCHAR(200),
    mes_inicio INT NOT NULL DEFAULT 1,  -- 1-12 (mes del CICLO, no calendario)
    mes_fin INT NOT NULL DEFAULT 12,    -- 1-12 (mes del CICLO)
    cantidad_planificada INT NOT NULL DEFAULT 1,
    horas DECIMAL(8,2),
    recursos TEXT,
    indicador VARCHAR(300) NOT NULL DEFAULT 'N° Actividades Ejecutadas/N°Programadas*100',
    meta VARCHAR(100) NOT NULL DEFAULT '100%',
    orden INT,
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    -- Soft delete auditado (agregar con ALTER TABLE si tabla ya existe)
    deleted_at TIMESTAMP,
    deleted_by INT,
    motivo_eliminacion TEXT
);

-- Auditoría de cambios PASO
CREATE TABLE ssoma_paso_auditoria (
    id SERIAL PRIMARY KEY,
    tipo VARCHAR(50) NOT NULL,          -- ELIMINACION | REPROGRAMACION
    entidad VARCHAR(50) NOT NULL,       -- ACTIVIDAD | EJECUCION
    entidad_id INT NOT NULL,
    paso_id INT NOT NULL,
    descripcion TEXT,
    motivo TEXT,
    valor_anterior JSONB,
    valor_nuevo JSONB,
    usuario_id INT,
    created_at TIMESTAMP DEFAULT NOW()
);

-- Ejecuciones (instancias mensuales generadas por cronjob + registradas manualmente)
CREATE TABLE ssoma_paso_ejecucion (
    id SERIAL PRIMARY KEY,
    actividad_id INT NOT NULL REFERENCES ssoma_paso_actividad(id) ON DELETE CASCADE,
    fecha_programada DATE NOT NULL,
    fecha_ejecutada DATE,
    estado VARCHAR(20) NOT NULL DEFAULT 'Programado', -- Programado | Ejecutado | Vencido | Cancelado
    observaciones TEXT,
    participantes_count INT,
    evidencia_nombre VARCHAR(300),   -- nombre del archivo en SharePoint
    evidencia_url VARCHAR(1000),     -- URL SharePoint
    evidencia_sp_id VARCHAR(200),    -- ID del item en SharePoint
    registrado_por INT REFERENCES app_user(id),
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP
);
```

### 3.12 Implementación backend — estado actual (2026-06-05)

**Ubicación:**
```
Features/SsomaModule/PasoFeature/
  Entities/SsomaPasoEntities.cs         — 5 entidades EF (+ SsomaPasoAuditoria)
  Dtos/SsomaPasoDtos.cs                 — todos los DTOs y requests
  Services/IPasoService.cs              — interfaz 20 métodos
  Services/PasoService_FIXED.cs         — implementación completa (reemplazó PasoService.cs — commit c0a41fc)
  PasoController.cs                     — 20 endpoints
```

**Namespaces:** `Abril_Backend.Features.Ssoma.Paso.*`

**Registro DI:** `SsomaModule.cs` → `services.AddScoped<IPasoService, PasoService_FIXED>()`

**AppDbContext:** DbSets `SsomaPasoCategorias`, `SsomaPasos`, `SsomaPasoActividades`, `SsomaPasoEjecuciones`, `SsomaPasoAuditorias`. Tabla `ssoma_paso_auditoria` mapeada en `ConfigurePostgreSQL` con `HasColumnName` explícito + jsonb para `valor_anterior`/`valor_nuevo`.

**Helpers implementados en PasoService (private static):**
- `CalcularSpi(plan, ejec)` → `plan==0 ? 1m : Round(ejec/plan, 2)`
- `SpiColor(spi)` → `>=0.95 "verde" | >=0.80 "amarillo" | "rojo"`
- `EsMesPlanificado(frec, mes, mesInicio)` → diff=mes-mesInicio; Mensual/Bimestral/Trimestral/Semestral/Anual/Unica
- `AjustarMes(mesOrig, mesInicioPlantilla, mesInicioInstancia)` → offset circular mod 12

**Lógica de ciclo (CRÍTICA):**
- Cada PASO tiene `Anio` y `MesInicio`. Las actividades tienen `MesInicio`/`MesFin` en meses del CICLO (1-12), NO en meses calendario.
- Mes 1 del ciclo = mes `MesInicio` del calendario. Ejemplo: PASO anio=2026, mes_inicio=12 → mes 1 ciclo = diciembre 2025.
- Cálculo: `cicloStartYear = MesInicio > 6 ? Anio - 1 : Anio`
- `mesCiclo = (anio*12 + mes - 1) - (cicloStartYear*12 + MesInicio - 1) + 1`
- Si fecha solicitada está fuera del ciclo de 12 meses → retorna resumen vacío.

**Cambios de modelo (2026-06-11):**
- `SsomaPaso.Anio` es `int?` — plantillas corporativas tienen `Anio = NULL` en BD.
- `PasoListItemDto.Anio` es `int?` para consistencia con la entidad.
- `PasoResumenMesDto` incluye `public PasoResumenMesAmbitoDto Ssoma { get; set; } = new();` (entre Ambiente y Actividades).
- `PasoSpiDto` ya tenía `SpiPorAmbitoDto Ssoma` — confirmado activo en servicio (`CalcAmbito("SSOMA")`).
- `PasoHistoricoAnioDto` agregado: `{ Anio, TotalProgramadas, TotalEjecutadas, TotalVencidas, SpiGeneral, SpiColor, PorcentajeAvance }` — nombres exactos que consume el frontend.

**Lógica de ciclo en GetSpiAsync y GetHistoricoProyectoAsync (2026-06-11):**
- Ambos métodos calculan `cicloStart`/`cicloEnd` y filtran ejecuciones al rango `[cicloStart, cicloEnd]` antes de cualquier conteo. Evita contar ejecuciones migradas fuera del ciclo (ej: Cedro 33, `anio=2026, mes_inicio=5` → `cicloStart=2026-05-01`).
- Lógica de vencidas: `Estado != "Ejecutado" && fin_de_mes(FechaProgramada) < hoy` — usa `DateTime.DaysInMonth` para el último día del mes, no el campo `Estado == "Vencido"` (que depende del cron).
- `GetHistoricoProyectoAsync` devuelve `List<PasoHistoricoAnioDto>` (ya no `List<PasoListItemDto>`). IPasoService actualizado en consecuencia.

**Soft delete actividades:**
- `ssoma_paso_actividad` tiene `deleted_at`, `deleted_by`, `motivo_eliminacion`.
- `DeleteActividadAsync(id, motivo, userId)` → setea `Activo=false` + `DeletedAt` + inserta en `ssoma_paso_auditoria`.
- Todos los queries filtran `a.Activo && a.DeletedAt == null`.

**Endpoints implementados (`api/v1/ssoma-paso`):**
```
GET    /categorias                           → catálogo activo, order ambito/nombre
GET    /dashboard?anio=                      → KPIs consolidados, SPI, por proyecto
GET    /alertas                              → Vencido OR (Programado con fecha <= hoy+7)
GET    /cron/procesar                        → [AllowAnonymous] + CronSecret. Marca vencidas + día 1 genera ejecuciones
GET    /                                     → lista paginada con filtros
GET    /{id}                                 → detalle con actividades + SPI
POST   /                                     → crear PASO/plantilla
PUT    /{id}                                 → editar (solo Borrador)
PATCH  /{id}/aprobar                         → plantilla→"Aprobado", instancia→"Activo"
POST   /{id}/instanciar                      → clona plantilla con AjustarMes para mes_inicio/mes_fin
GET    /proyecto/{proyectoId}/historico      → lista todos los PASOs del proyecto (no plantillas), ordenados anio DESC, mesInicio ASC
GET    /{id}/spi                             → SPI general + por ambito (Seguridad/Salud/Ambiente/SSOMA)
GET    /{id}/resumen-mes?anio=&mes=          → resumen mensual con lógica de ciclo + actividades por estado
POST   /actividad                            → nueva actividad
PUT    /actividad/{id}                       → editar actividad
DELETE /actividad/{id}                       → soft delete + motivo (body: EliminarActividadRequest) + auditoría
GET    /actividad/{id}/auditoria             → historial de cambios de la actividad
POST   /ejecucion                            → upsert por (actividad_id, fecha_programada). FechaVerificacion=último día mes
PATCH  /ejecucion/{id}/reprogramar           → 400 si ya Ejecutado + auditoría
PATCH  /ejecucion/{id}/evidencia             → nombre/url/sp_id
```
> ⚠️ `GET /{id}/gantt` fue eliminado (2026-06-11). Usar `/proyecto/{proyectoId}/historico` para listar PASOs del proyecto.

**Tablas pendientes de crear/alterar manualmente en BD:**
```sql
-- Ver sección 3.4 para DDL completo
CREATE TABLE ssoma_paso_categoria (...);
CREATE TABLE ssoma_paso (...);
CREATE TABLE ssoma_paso_actividad (...);
ALTER TABLE ssoma_paso_actividad ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMP;
ALTER TABLE ssoma_paso_actividad ADD COLUMN IF NOT EXISTS deleted_by INT;
ALTER TABLE ssoma_paso_actividad ADD COLUMN IF NOT EXISTS motivo_eliminacion TEXT;
CREATE TABLE ssoma_paso_ejecucion (...);
CREATE TABLE ssoma_paso_auditoria (...);  -- ver DDL en sección 3.4
```
Nota: la migración EF NO se generó (restricción del blueprint). Crear/alterar tablas manualmente.

---

### 3.5 SharePoint — biblioteca de evidencias PASO
- **SiteId**: `d9e26806-d535-4353-9610-195978e20390` (SSOMA-Powerapps, mismo que EMOs)
- **Biblioteca**: crear `PasoEvidencias` — obtener ID con:
  `GET https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/_api/web/lists?$select=Title,Id&$filter=BaseTemplate eq 101`
- **Ruta de archivo**: `PASO/{anio}/{proyecto_id}/{actividad_id}/{fecha}_{nombre_archivo}`
- **Patrón de subida**: igual que `LecturaEmosLibraryId` en el módulo Clínica.

### 3.6 Lógica SPI
```
Planificadas_a_hoy  = ejecuciones con fecha_programada <= hoy
Ejecutadas_a_hoy    = ejecuciones con estado = 'Ejecutado' y fecha_ejecutada <= hoy
SPI                 = ejecutadas_a_hoy / planificadas_a_hoy
  < 0.80  → rojo
  0.80–0.94 → amarillo
  >= 0.95 → verde
% avance global = ejecutadas_total / total_programadas_anio * 100
```

### 3.7 Cronjob de alertas (diario)
```
GET /api/v1/ssoma-paso/cron/procesar
Authorization: Bearer {CronSecret}

Lógica:
1. Marcar como 'Vencido' toda ejecución con fecha_programada < hoy y estado = 'Programado'
2. Enviar email por cada nueva vencida → responsable + jefe SSOMA
3. Enviar recordatorio para ejecuciones con fecha_programada = hoy + 7 días → responsable
4. Día 1 de cada mes: generar ejecuciones del mes para actividades Mensual/Bimestral/etc.
```

### 3.8 Endpoints backend

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/v1/ssoma-paso` | Lista paginada (filtros: proyecto_id, anio, estado, es_plantilla) |
| GET | `/api/v1/ssoma-paso/{id}` | Detalle con actividades y ejecuciones |
| POST | `/api/v1/ssoma-paso` | Crear programa / plantilla |
| PUT | `/api/v1/ssoma-paso/{id}` | Editar (solo Borrador) |
| PATCH | `/api/v1/ssoma-paso/{id}/aprobar` | Aprobar programa |
| POST | `/api/v1/ssoma-paso/{id}/instanciar` | Crear instancia por proyecto desde plantilla |
| GET | `/api/v1/ssoma-paso/{id}/gantt` | Datos Gantt con avance real vs planificado |
| GET | `/api/v1/ssoma-paso/{id}/spi` | SPI + KPIs en tiempo real |
| GET | `/api/v1/ssoma-paso/{id}/reporte` | Export Excel/PDF (?format=excel|pdf) |
| GET | `/api/v1/ssoma-paso/dashboard` | KPIs consolidados todos los proyectos |
| POST | `/api/v1/ssoma-paso-actividad` | Crear actividad en programa |
| PUT | `/api/v1/ssoma-paso-actividad/{id}` | Editar actividad |
| DELETE | `/api/v1/ssoma-paso-actividad/{id}` | Eliminar actividad |
| POST | `/api/v1/ssoma-paso-ejecucion` | Registrar ejecución |
| PATCH | `/api/v1/ssoma-paso-ejecucion/{id}/evidencia` | Subir evidencia a SharePoint |
| GET | `/api/v1/ssoma-paso/alertas` | Vencidas + próximas a vencer |
| GET | `/api/v1/ssoma-paso/cron/procesar` | Cronjob diario (auth: CronSecret) |
| GET | `/api/v1/ssoma-paso-categoria` | Catálogo de categorías por ámbito |

### 3.9 Estructura frontend Angular
```
features/ssoma/salud-ocupacional/paso/
  paso.routes.ts
  pages/
    dashboard/               ← KPIs consolidados, SPI por proyecto
    lista/                   ← listado programas con filtros
    detalle/                 ← árbol actividades por ámbito + gantt
    actividad-detalle/       ← ficha actividad + historial ejecuciones
  components/
    paso-gantt/              ← dhtmlx-gantt wrapper
    actividad-tree/          ← agrupado por ámbito (Seguridad/Salud/Ambiente)
    ejecucion-modal/         ← registrar ejecución + subir evidencia
    spi-badge/               ← componente reutilizable semáforo SPI
  services/
    paso.service.ts
    paso-actividad.service.ts
    paso-ejecucion.service.ts
  dtos/
    paso.dtos.ts
```

### 3.10 Pantallas del módulo
1. **Dashboard** — KPIs: SPI, avance %, vencidas, próximas. Filtro por proyecto. Barras por ámbito.
2. **Lista de programas** — plantillas corporativas + instancias por proyecto. Filtros año/proyecto/estado.
3. **Detalle del programa** — tabs: Seguridad / Salud Ocupacional / Medio Ambiente. Gantt + tabla actividades.
4. **Actividad detalle** — frecuencia, responsable, historial de ejecuciones mes a mes, evidencias.
5. **Modal registrar ejecución** — fecha real, participantes, observaciones, drag&drop evidencia → SharePoint.
6. **Instanciar por proyecto** — wizard: seleccionar plantilla → seleccionar proyecto → ajustar fechas → confirmar.
7. **Alertas** — lista vencidas y próximas a vencer con acción directa de registro.
8. **Reporte** — export mensual/anual Excel o PDF con % cumplimiento por ámbito.

### 3.11 Roles y permisos
| Rol | Permisos |
|---|---|
| Jefe SSOMA / Corporativo | Crear plantilla, aprobar, instanciar, exportar, ver todo |
| Asistente SSOMA | Registrar ejecuciones, subir evidencias |
| Residente / Jefe de obra | Ver Gantt y avance de su proyecto |
| Gerencia | Solo lectura, dashboard consolidado |

---

## 4. Control de Presupuesto SSOMA (diseñado, pendiente construir)

### Entidades
```sql
CREATE TABLE ssoma_presupuesto (
    id SERIAL PRIMARY KEY,
    proyecto_id INT NOT NULL REFERENCES project(project_id),
    nombre VARCHAR(200) NOT NULL,
    anio INT NOT NULL,
    moneda CHAR(3) NOT NULL DEFAULT 'PEN',
    tipo_cambio DECIMAL(10,4),
    total_aprobado DECIMAL(14,2),
    estado VARCHAR(20) NOT NULL DEFAULT 'Borrador',
    aprobado_por INT REFERENCES app_user(id),
    aprobado_en TIMESTAMP,
    created_at TIMESTAMP DEFAULT NOW(),
    created_by INT,
    updated_at TIMESTAMP
);

CREATE TABLE ssoma_partida (
    id SERIAL PRIMARY KEY,
    presupuesto_id INT NOT NULL REFERENCES ssoma_presupuesto(id),
    padre_id INT REFERENCES ssoma_partida(id),
    codigo VARCHAR(20),
    nombre VARCHAR(200) NOT NULL,
    tipo_id INT REFERENCES ssoma_partida_tipo(id),
    monto_presupuestado DECIMAL(14,2) NOT NULL DEFAULT 0,
    es_hoja BOOLEAN NOT NULL DEFAULT TRUE,
    orden INT
);

CREATE TABLE ssoma_partida_tipo (
    id SERIAL PRIMARY KEY,
    nombre VARCHAR(100) NOT NULL,
    codigo VARCHAR(20),
    categoria VARCHAR(50) -- EPP | Capacitacion | Medico | Señaletica | Otros
);

CREATE TABLE ssoma_consumo (
    id SERIAL PRIMARY KEY,
    partida_id INT NOT NULL REFERENCES ssoma_partida(id),
    proyecto_id INT NOT NULL REFERENCES project(project_id),
    tipo VARCHAR(20) NOT NULL,
    descripcion VARCHAR(500),
    monto DECIMAL(14,2) NOT NULL,
    fecha DATE NOT NULL,
    documento_ref VARCHAR(100),
    proveedor VARCHAR(200),
    registrado_por INT REFERENCES app_user(id),
    created_at TIMESTAMP DEFAULT NOW()
);
```

### Lógica EVM
```
AC  = SUM(ssoma_consumo.monto)
CPI = EV / AC   (< 1 = sobre costo)
EAC = BAC / CPI (proyección costo final)
VAC = BAC - EAC (negativo = sobre costo)
```

---

## 5. Decisiones de arquitectura globales

- Tablas nuevas: prefijo `ssoma_` (distingue de `ss_` habilitación y entidades clínica)
- Todos los módulos como feature slices dentro de `SsomaModule` (o `SsomaGestionModule` si crece)
- Roles nuevos se agregan a `app_role` en BD — no hardcodear role_ids
- Reportes PDF: `jsPDF + jsPDF-autotable` (ya instalado)
- Reportes Excel: `xlsx` (ya instalado)
- Email: `IEmailService` existente
- Evidencias: SharePoint (mismo patrón que EMOs — `SiteId` + library por módulo)
- Mobile-first para módulos de campo: RAC, OPT, Inspecciones, ATS
- Módulos PowerApps siguen activos como respaldo — migración incremental

---

## 6. Orden de construcción

1. **PASO** ✓ construido
2. RAC
3. Inspecciones
4. OPT + ATS
5. Incidentes & No Conformidades
6. Charlas & Capacitaciones
7. Amonestaciones
8. Control de Presupuesto SSOMA
9. PASO + resto Fase 2
10. Fase 3

---

## 7. Pendientes / decisiones abiertas

- [ ] Confirmar ID biblioteca SharePoint `PasoEvidencias` (crearla si no existe)
- [ ] Confirmar si `SsomaGestionModule` va dentro de `SsomaModule` o módulo independiente
- [ ] Definir roles nuevos necesarios para SSOMA Gestion (actualmente SALUD OCUPACIONAL = role_id 53)
- [ ] Confirmar si los módulos de campo (RAC, OPT) tienen vista móvil dedicada o responsive
- [ ] IPER: confirmar si adelantar por requerimiento SUNAFIL
