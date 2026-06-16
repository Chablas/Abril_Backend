# CONTEXT_RAC.md — Módulo RAC v2

> Documento maestro de especificación. Claude Code debe leer este archivo antes de tocar cualquier archivo del módulo RAC.
> Stack: Angular 21 + .NET 10 + PostgreSQL (Aiven). SharePoint para archivos. Power Automate para email.

---

## 1. VISIÓN DEL MÓDULO

El módulo RAC (Registro de Actos & Condiciones Subestándar) es la app de campo principal del sistema Abril SSOMA. El **70% del uso es en celular** — todo debe diseñarse mobile-first. La web es secundaria pero también debe verse profesional.

Este módulo es la **base de referencia** para todos los módulos SSOMA que vienen. El diseño, patrones de componentes, y UX que se establezcan aquí serán replicados en los demás módulos. Debe quedar al 100%.

---

## 2. DESIGN SYSTEM — "Sage & Stone Premium"

### 2.1 Tokens de color (definir en variables CSS globales del módulo)

```css
/* Primarios */
--rac-primary: #1b3a2d; /* verde bosque profundo — acento principal */
--rac-primary-soft: #eaf2ee; /* verde muy suave — fondos de badges activos */
--rac-primary-hover: #152e23; /* hover sobre botones primarios */

/* Texto */
--rac-text-dark: #1a1a18; /* títulos y texto principal */
--rac-text-muted: #8a8880; /* labels, subtítulos, metadatos */
--rac-text-light: #a8a59e; /* placeholders, hints */

/* Fondos */
--rac-bg-page: #f5f3ef; /* fondo general de página — crema muy suave */
--rac-bg-card: #ffffff; /* cards y panels */
--rac-bg-hover: #f0ede8; /* hover sobre filas y elementos */

/* Bordes */
--rac-border: #e8e5df; /* borde estándar */
--rac-border-strong: #d4d0c8; /* borde con más peso */

/* Semánticos */
--rac-danger: #b83030; /* crítico, vencido, error */
--rac-danger-soft: #fdecea; /* fondo badge crítico */
--rac-warning: #d97706; /* alto, advertencia */
--rac-warning-soft: #fef5d0; /* fondo badge alto */
--rac-amber: #8a5a00; /* penalidades, en evaluación */
--rac-amber-soft: #fdf3dc; /* fondo badge penalidad */
--rac-info: #2563a8; /* en proceso, informativo */
--rac-info-soft: #eef4ff; /* fondo badge en proceso */
--rac-success: #1b3a2d; /* cerrado, completado */
--rac-success-soft: #eaf2ee; /* fondo badge cerrado */
```

### 2.2 Header — igual que PASO

Usar el **mismo componente de header que PASO** sin modificarlo. Solo cambiar el título y subtítulo.

Estructura:

```
[Badge SSOMA·2026]  Actos & Condiciones Subestándar     [24 activos] [7 críticos]  [+ Nuevo RAC]  [Avatar]
                    Seguridad · Salud Ocupacional · MA
──────────────────────────────────────────────────────────────────────────────────────────────────────────
[Dashboard]  [Observaciones]  [Penalidades]
```

- Header background: `#FFFFFF`
- Borde inferior: `0.5px solid #E8E5DF`
- Badge SSOMA: `background: #1B3A2D; color: #fff`
- Botón "+ Nuevo RAC": `background: #1B3A2D; color: #fff; border-radius: 9px`
- Tab activo: `color: #1B3A2D; border-bottom: 2px solid #1B3A2D`
- Tab inactivo: `color: #8A8880`

### 2.3 Cards

```css
background: #ffffff;
border: 0.5px solid #e8e5df;
border-radius: 12px;
/* Sin box-shadow — la elegancia viene del borde y el fondo de página */
```

Hover sobre filas: `background: #F0EDE8`

### 2.4 Badges de estado (pill-shaped, border-radius: 999px)

| Estado        | Fondo   | Texto   |
| ------------- | ------- | ------- |
| Abierto       | #F5F3EF | #5A5850 |
| En proceso    | #EEF4FF | #2563A8 |
| Cerrado       | #EAF2EE | #1B3A2D |
| Vencido       | #FDECEA | #922B21 |
| En evaluación | #FDF3DC | #8A5A00 |

### 2.5 Badges de severidad

| Severidad | Fondo   | Texto   | Dot color |
| --------- | ------- | ------- | --------- |
| Bajo      | #EAF2EE | #1B3A2D | #1B3A2D   |
| Medio     | #FEF5D0 | #7D6008 | #D97706   |
| Alto      | #FEF5D0 | #7D6008 | #D97706   |
| Crítico   | #FDECEA | #922B21 | #B83030   |

### 2.6 KPI Cards

```css
background: #ffffff;
border: 0.5px solid #e8e5df;
border-radius: 12px;
padding: 13px 15px;
```

- Label: `font-size: 9px; font-weight: 600; letter-spacing: 0.08em; text-transform: uppercase; color: #A8A59E`
- Número: `font-size: 26px; font-weight: 700; color: #1A1A18`
- Tendencia positiva: `color: #1B3A2D`
- Tendencia negativa: `color: #B83030`

### 2.7 Botones

```css
/* Primario */
.btn-primary {
  background: #1b3a2d;
  color: #fff;
  border: none;
  border-radius: 9px;
  font-size: 11px;
  font-weight: 600;
  padding: 7px 14px;
  letter-spacing: 0.02em;
}
.btn-primary:hover {
  background: #152e23;
}

/* Secundario */
.btn-secondary {
  background: #f5f3ef;
  color: #5a5850;
  border: 0.5px solid #e0ddd8;
  border-radius: 7px;
  font-size: 10px;
  font-weight: 600;
  padding: 4px 10px;
}

/* Ghost (outline) */
.btn-ghost {
  background: transparent;
  color: #1b3a2d;
  border: 0.5px solid #1b3a2d;
  border-radius: 9px;
}
```

### 2.8 Tipografía

- Font: Inter (ya cargada en el proyecto)
- Títulos de sección: `font-size: 11px; font-weight: 600; letter-spacing: 0.02em; color: #1A1A18`
- Labels: `font-size: 9px; font-weight: 600; letter-spacing: 0.08em; text-transform: uppercase; color: #A8A59E`
- Body: `font-size: 12px; color: #1A1A18`
- Muted: `font-size: 10-11px; color: #8A8880`

### 2.9 FAB "Nuevo RAC"

```css
position: fixed;
bottom: 24px;
right: 24px;
z-index: 100;
background: #1b3a2d;
color: #fff;
border: none;
border-radius: 999px;
padding: 12px 18px;
font-size: 12px;
font-weight: 600;
box-shadow: 0 4px 14px rgba(27, 58, 45, 0.25);
cursor: pointer;
```

- Mobile: solo `＋` (ícono)
- Desktop: `＋ Nuevo RAC`

---

## 3. ESTRUCTURA DE RUTAS

```
/ssoma/rac                    → RacListComponent (tabs: Dashboard | Observaciones | Penalidades)
/ssoma/rac/nuevo              → RacNuevoComponent (stepper 4 pasos)
/ssoma/rac/:id                → RacDetalleComponent
/ssoma/rac/:id/cerrar         → RacCerrarComponent
/ssoma/rac/:id/regularizar    → RacRegularizarComponent
```

---

## 4. ROLES Y PERMISOS

| Acción                                     | ABRIL (admin/ssoma) | CONTRATISTA         |
| ------------------------------------------ | ------------------- | ------------------- |
| Ver dashboard global (todos los proyectos) | ✅                  | ❌                  |
| Ver dashboard propio (solo su empresa)     | ✅                  | ✅                  |
| Crear nuevo RAC                            | ✅                  | ✅                  |
| Ver lista de observaciones                 | ✅ (todas)          | ✅ (solo las suyas) |
| Cerrar RAC ajeno                           | ✅                  | ❌                  |
| Cerrar RAC propio                          | ✅                  | ✅                  |
| Regularizar RAC                            | ✅                  | ✅                  |
| Ver penalidades                            | ✅ (todas)          | ✅ (solo las suyas) |
| Presentar descargo                         | ✅                  | ✅                  |
| Gestión RAC (menú lateral)                 | ✅                  | ✅                  |

**Implementación:**

- Frontend: `*ngIf` con `AuthService.hasRole()` o mecanismo existente del proyecto
- Backend: el endpoint de lista filtra por `empresaId` cuando rol es CONTRATISTA
- Backend: el endpoint de cerrar valida que si rol es CONTRATISTA, el RAC pertenece a su empresa → 403 si no

---

## 5. DASHBOARD

### 5.1 Vista ABRIL

**Fila 1 — KPIs (4 cols desktop, 2x2 mobile)**

```
[ RACs abiertos ]  [ Vencidos ]  [ Tasa de cierre % ]  [ Críticos activos ]
```

**Fila 2 — Gráficos (3 cols desktop, 1 col mobile)**

```
[ RACs por Severidad — donut ]  [ Tendencia mensual — line ]  [ Top categorías — bar horizontal ]
```

**Fila 3 — Tabla proyectos**
Proyecto | RACs abiertos | Vencidos | Tasa cierre | →

**Fila 4 — Ranking contratistas**
Empresa | RACs | Críticos | Pendientes | Tasa cierre

### 5.2 Vista CONTRATISTA

**Fila 1 — KPIs (2x2 mobile)**

```
[ Mis RACs abiertos ]  [ Por vencer 7d ]  [ Cerrados este mes ]  [ Penalidades activas ]
```

**Fila 2**

```
[ Mis RACs por estado — donut ]  [ Mis categorías frecuentes — bar horizontal ]
```

**Fila 3 — Mis pendientes de levantar**
Cards ordenadas por vencimiento ascendente con botón de acción directo.

---

## 6. LISTA DE OBSERVACIONES

### 6.1 Filtros

- Desktop: fila horizontal visible siempre
- Mobile: acordeón colapsable con botón "🔍 Filtrar"
- Campos: Estado | Severidad | Tipo (Acto/Condición) | Solo con penalidad | Proyecto (solo ABRIL) | Buscar

### 6.2 Tabla Desktop

Columnas: # | Código | Proyecto | Tipo | Categoría | Severidad | Estado | Fecha | Acciones

**Acciones inline en la misma fila — NO en modal separado:**

- `PDF` → genera y descarga PDF
- `Regularizar` → navega a /rac/:id/regularizar
- `Cerrar` → navega a /rac/:id/cerrar (visible: ABRIL siempre; CONTRATISTA solo si es su RAC)
- `Ver` → navega a /rac/:id

Indicador visual: punto de color a la izquierda según severidad.

### 6.3 Cards Mobile (< 768px)

```
┌─────────────────────────────────────────┐
│ ● RAC-2026-CDR-001         [Crítico]    │
│   Uso incorrecto de EPP · CDR · Acto    │
│   📅 13/06/2026      [Abierto]          │
│   ─────────────────────────────────     │
│   [PDF]  [Regularizar]  [Ver]           │
└─────────────────────────────────────────┘
```

Card styles:

```css
background: #ffffff;
border: 0.5px solid #e8e5df;
border-radius: 12px;
padding: 13px 14px;
margin-bottom: 8px;
border-left: 3px solid [color-severidad];
```

---

## 7. NUEVO RAC — STEPPER

- 4 pasos: Ubicación → Observado → Detalle → Revisión
- Desktop: stepper con número + texto
- Mobile: solo puntos numerados compactos sin texto
- Footer fijo con "Anterior" / "Siguiente"

**Paso 1:** Proyecto (select) | Tipo: Acto/Condición (toggle pills) | Severidad: Bajo/Medio/Alto/Crítico (toggle pills)
**Paso 2:** Empresa observada (select) | Trabajador (select con búsqueda, opcional) | Área/Zona (input)
**Paso 3:** Categoría (select) | Descripción (textarea, mín 20 chars) | Fotos (mín 1, auto-upload) | Fecha límite (datepicker)
**Paso 4:** Resumen readonly + botón "Crear RAC"

---

## 8. CERRAR RAC (/rac/:id/cerrar)

Roles permitidos: ABRIL (siempre) | CONTRATISTA (solo si es su RAC)

Campos:

- Comentario de cierre (textarea, obligatorio)
- Foto de cierre (input file, obligatorio)
  - Al seleccionar → sube automáticamente a SharePoint (SIN botón adicional)
  - Spinner "Subiendo..."
  - Badge verde "Foto subida ✓" al completar
  - Nuevo archivo → resetea fotoCierreUrl → vuelve a subir
  - Botón "Cerrar RAC" habilitado SOLO cuando fotoCierreUrl tiene valor

Backend RacCerrarRequest:

```csharp
public string Comentario { get; set; }
public string FotoCierreUrl { get; set; }  // validar no null/vacío → 400
```

Post-cierre: insertar SsomaRacFoto con Tipo="Cierre", Url=FotoCierreUrl, Orden=0 en misma transacción.

---

## 9. REGULARIZAR RAC (/rac/:id/regularizar)

Roles: ABRIL y CONTRATISTA.

Campos:

- Descripción del levantamiento (textarea, obligatorio)
- Fotos de evidencia (mín 1, auto-upload sin botón)
- Botón "Enviar levantamiento"

---

## 10. PENALIDADES

### Lista

- Tabla desktop: Código | RAC | Empresa | Infracción | Monto | Estado | Fecha | Acción
- Cards mobile con mismos campos

### Modal de penalidad

- Datos readonly
- Textarea de justificación (obligatorio)
- Documento de sustento (input file, obligatorio):
  - Auto-upload al seleccionar, SIN botón adicional
  - Spinner → badge verde al completar
  - Botón "Presentar descargo" deshabilitado hasta tener texto + documentoUrl

Backend PenalidadDescargoRequest:

```csharp
public string Justificacion { get; set; }
public string DocumentoUrl { get; set; }  // validar no null/vacío → 400
```

---

## 11. MOBILE — REGLAS GENERALES

1. Breakpoint mobile: < 768px
2. Tablas → cards siempre en mobile
3. Filtros → acordeón colapsable "🔍 Filtrar"
4. FAB fixed visible en todas las pantallas
5. Touch targets mínimo 44px de altura
6. Modales: 95% viewport con scroll interno
7. Padding horizontal: 16px
8. Stepper compacto: solo puntos sin texto
9. NUNCA `position: absolute` en `:host`

---

## 12. SHAREPOINT — PATRÓN DE SUBIDA

Usar el `SharepointService` existente en el proyecto. Consultar el servicio para método exacto.

**Regla absoluta: subida automática al seleccionar, NUNCA botón adicional.**

```typescript
onFileSelected(event: Event): void {
  const file = (event.target as HTMLInputElement).files?.[0];
  if (!file) return;
  this.subiendo = true;
  this.archivoUrl = null;
  this.cdr.detectChanges();
  this.sharepointService.subirArchivo(file, libraryId, carpeta).subscribe({
    next: (url) => {
      this.archivoUrl = url;
      this.subiendo = false;
      this.cdr.detectChanges();
    },
    error: () => {
      this.subiendo = false;
      this.cdr.detectChanges();
    }
  });
}
```

Verificar nombres de librerías SharePoint con:
`https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/_api/web/lists?$select=Title,Id&$filter=BaseTemplate eq 101`

---

## 13. PATRONES ANGULAR — REGLAS DEL PROYECTO

1. **NUNCA** `position: absolute` en `:host` → usar wrapper `div` interno
2. **SIEMPRE** `ChangeDetectorRef.detectChanges()` después de HTTP responses
3. **No EF Migrations** → cambios de schema directo en pgAdmin con SQL
4. Comparar DTOs frontend/backend lado a lado antes de diagnosticar bugs
5. CSS no funciona en componente nuevo → comparar contra componente que sí funciona
6. CSS global → `src/styles.scss`; `::ng-deep` poco confiable

---

## 14. ORDEN DE IMPLEMENTACIÓN

### Fase única — todo de una vez

- [ ] Aplicar header de PASO al módulo RAC
- [ ] Aplicar design system completo (tokens, cards, badges, tipografía)
- [ ] FAB "Nuevo RAC" flotante en todas las pantallas
- [ ] Cards mobile para lista de observaciones
- [ ] Acciones inline en tabla (PDF + Regularizar + Cerrar + Ver)
- [ ] Filtros colapsables en mobile
- [ ] Acceso rol CONTRATISTA (menú lateral + guards + filtro backend por empresaId)
- [ ] Foto cierre auto-upload (sin botón)
- [ ] Documento descargo auto-upload (sin botón)
- [ ] Dashboard vista CONTRATISTA
- [ ] Dashboard vista ABRIL mejorado
- [ ] Badges y paleta unificada en todo el módulo

---

## 15. INSTRUCCIÓN PARA CLAUDE CODE

Antes de modificar cualquier archivo del módulo RAC:

1. Leer este documento completo
2. Revisar el componente de header de PASO y usarlo sin modificarlo
3. Revisar el componente análogo más cercano antes de crear código nuevo
4. Aplicar los tokens de color de la sección 2.1 — no inventar colores
5. Respetar todas las reglas de la sección 13
6. Subida de archivos SIEMPRE automática al seleccionar — nunca botón separado
7. Verificar nombres de librerías SharePoint con la API antes de hardcodear
