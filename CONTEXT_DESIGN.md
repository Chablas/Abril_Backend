# CONTEXT_DESIGN.md — Estandarización Visual · Abril Intranet
> Claude Code: lee este archivo COMPLETO antes de tocar cualquier archivo.
> Stack: Angular 21 + Tailwind CSS v4 · archivo de estilos: `src/styles.css`
> REGLA DE ORO: solo cambios visuales. Cero cambios en lógica, servicios, DTOs, rutas o tests.

---

## 0. ANTES DE EMPEZAR — COMMIT DE SEGURIDAD

```bash
git add -A
git commit -m "chore: snapshot pre-design-system — revert point"
```

Confirma el hash del commit antes de continuar.

---

## 1. DIAGNÓSTICO — QUÉ ESTÁ MAL HOY

### Problemas identificados en producción

| Área | Problema |
|------|----------|
| Colores | `#64BC04` hardcodeado en +50 lugares. `#0086A5`, `#E5F7D1` también dispersos. Sin tokens globales. |
| Header shell | `app-header` muestra logo Abril + título. En mobile: hamburger + título. Correcto pero sin mejoras mobile. |
| Page header | `ssoma-page-header` (hero con badge/pills/botones) solo existe en módulos SSOMA. El resto (Salidas, Lecciones, Adjudicaciones) va directo a tabla sin ningún page header. |
| Sidebar desktop | Click en módulo despliega dropdown flotante. Daniel quiere: click directo navega a dashboard del módulo (igual que PASO). Solo módulos sin ruta directa mantienen dropdown. |
| Sidebar mobile | Drawer desde izquierda, `top-17` hardcodeado. Sin fondo oscuro correcto. Sin iconos al lado del label. Márgenes `px-8` desperdician espacio. Sin indicador de ruta activa. |
| Mobile general | Header muestra logo grande en desktop, en mobile solo hamburger. Los filtros de página ocupan toda la altura en stack vertical. Tablas sin scroll horizontal explícito. Padding `p-[20px]` en mobile es excesivo. |
| Iconos | `app-nav-icon` usa SVGs básicos. Migrar a Tabler Icons (ya instalado: clase `ti`). |
| Tipografía | Sin jerarquía definida. Pesos y tamaños inconsistentes entre módulos. |

---

## 2. DESIGN TOKENS — `src/styles.css`

### 2.1 Acción: reemplazar el bloque `@theme { }` existente por este

```css
@import 'tailwindcss';

@theme {
  /* ── Marca Abril ─────────────────────────────────── */
  --color-abril-primary:        #4CAF50;   /* verde principal (acción, CTA) */
  --color-abril-primary-hover:  #43A047;
  --color-abril-primary-light:  #E8F5E9;   /* fondos suaves, hover rows */
  --color-abril-primary-dark:   #2E7D32;   /* textos sobre fondo claro */

  --color-abril-accent:         #00897B;   /* teal — acciones secundarias */
  --color-abril-accent-hover:   #00796B;
  --color-abril-accent-light:   #E0F2F1;

  /* ── Neutros ─────────────────────────────────────── */
  --color-abril-ink:            #0D1B2A;   /* texto principal */
  --color-abril-body:           #374151;   /* texto body */
  --color-abril-muted:          #6B7280;   /* texto secundario */
  --color-abril-placeholder:    #9CA3AF;
  --color-abril-border:         #E5E7EB;
  --color-abril-border-strong:  #D1D5DB;
  --color-abril-surface:        #FFFFFF;
  --color-abril-bg:             #F3F4F6;   /* fondo de página */
  --color-abril-bg-alt:         #F9FAFB;

  /* ── Sidebar ─────────────────────────────────────── */
  --color-abril-sidebar-bg:     #1A2332;   /* fondo sidebar oscuro */
  --color-abril-sidebar-text:   #CBD5E1;   /* texto items */
  --color-abril-sidebar-muted:  #64748B;   /* labels de sección */
  --color-abril-sidebar-active: #4CAF50;   /* indicador activo */
  --color-abril-sidebar-hover:  #243044;   /* hover item */
  --color-abril-sidebar-active-bg: rgba(76,175,80,0.15);

  /* ── Semáforo ────────────────────────────────────── */
  --color-abril-success:        #16A34A;
  --color-abril-success-light:  #DCFCE7;
  --color-abril-success-dark:   #14532D;

  --color-abril-warning:        #D97706;
  --color-abril-warning-light:  #FEF3C7;
  --color-abril-warning-dark:   #92400E;

  --color-abril-danger:         #DC2626;
  --color-abril-danger-light:   #FEE2E2;
  --color-abril-danger-dark:    #991B1B;

  --color-abril-info:           #2563EB;
  --color-abril-info-light:     #DBEAFE;
  --color-abril-info-dark:      #1E40AF;

  /* ── Tipografía ──────────────────────────────────── */
  --font-sans: 'Inter', system-ui, -apple-system, sans-serif;

  /* ── Sombras ─────────────────────────────────────── */
  --shadow-card:       0 1px 3px rgba(0,0,0,0.08), 0 1px 2px rgba(0,0,0,0.04);
  --shadow-card-hover: 0 4px 12px rgba(0,0,0,0.12);
  --shadow-dropdown:   0 8px 24px rgba(0,0,0,0.12);
  --shadow-modal:      0 20px 60px rgba(0,0,0,0.2);

  /* ── Radios ──────────────────────────────────────── */
  --radius-sm:   6px;
  --radius-md:   10px;
  --radius-lg:   14px;
  --radius-xl:   20px;
  --radius-pill: 999px;
}
```

### 2.2 Agregar clases utilitarias globales DESPUÉS del bloque @theme

```css
/* ── Cards ──────────────────────────────────────────────────── */
.abril-card {
  background: var(--color-abril-surface);
  border: 1px solid var(--color-abril-border);
  border-radius: var(--radius-lg);
  box-shadow: var(--shadow-card);
  transition: box-shadow 0.2s ease;
}
.abril-card:hover { box-shadow: var(--shadow-card-hover); }

/* ── Botón primario ─────────────────────────────────────────── */
.btn-primary {
  display: inline-flex; align-items: center; gap: 6px;
  padding: 0 16px; height: 38px;
  background: var(--color-abril-primary);
  color: #fff; font-size: 0.875rem; font-weight: 500;
  border-radius: var(--radius-md); border: none; cursor: pointer;
  transition: background 0.15s ease;
}
.btn-primary:hover { background: var(--color-abril-primary-hover); }
.btn-primary:disabled { background: var(--color-abril-border-strong); cursor: not-allowed; }

/* ── Botón ghost ────────────────────────────────────────────── */
.btn-ghost {
  display: inline-flex; align-items: center; gap: 6px;
  padding: 0 14px; height: 38px;
  background: transparent;
  color: var(--color-abril-primary); font-size: 0.875rem; font-weight: 500;
  border-radius: var(--radius-md);
  border: 1.5px solid var(--color-abril-primary);
  cursor: pointer; transition: background 0.15s ease;
}
.btn-ghost:hover { background: var(--color-abril-primary-light); }

/* ── Botón accent ───────────────────────────────────────────── */
.btn-accent {
  display: inline-flex; align-items: center; gap: 6px;
  padding: 0 16px; height: 38px;
  background: var(--color-abril-accent);
  color: #fff; font-size: 0.875rem; font-weight: 500;
  border-radius: var(--radius-md); border: none; cursor: pointer;
  transition: background 0.15s ease;
}
.btn-accent:hover { background: var(--color-abril-accent-hover); }

/* ── Badges de estado ───────────────────────────────────────── */
.badge {
  display: inline-flex; align-items: center;
  padding: 2px 10px; border-radius: var(--radius-pill);
  font-size: 0.72rem; font-weight: 600; white-space: nowrap;
}
.badge-success  { background: var(--color-abril-success-light);  color: var(--color-abril-success-dark); }
.badge-warning  { background: var(--color-abril-warning-light);  color: var(--color-abril-warning-dark); }
.badge-danger   { background: var(--color-abril-danger-light);   color: var(--color-abril-danger-dark); }
.badge-info     { background: var(--color-abril-info-light);     color: var(--color-abril-info-dark); }
.badge-neutral  { background: var(--color-abril-bg);             color: var(--color-abril-muted); }
.badge-primary  { background: var(--color-abril-primary-light);  color: var(--color-abril-primary-dark); }

/* ── Tabla estándar ─────────────────────────────────────────── */
.abril-table { width: 100%; font-size: 0.875rem; border-collapse: collapse; }
.abril-table thead tr {
  background: var(--color-abril-primary-light);
  color: var(--color-abril-primary-dark);
}
.abril-table th {
  text-align: left; padding: 10px 16px;
  font-weight: 600; font-size: 0.8rem;
  text-transform: uppercase; letter-spacing: 0.04em;
  white-space: nowrap;
}
.abril-table td { padding: 10px 16px; border-bottom: 1px solid var(--color-abril-border); color: var(--color-abril-body); }
.abril-table tbody tr { transition: background 0.1s; cursor: pointer; }
.abril-table tbody tr:hover { background: var(--color-abril-bg-alt); }

/* ── Input estándar ─────────────────────────────────────────── */
.abril-input {
  height: 38px; padding: 0 12px; width: 100%;
  border: 1.5px solid var(--color-abril-border-strong);
  border-radius: var(--radius-md);
  font-size: 0.875rem; color: var(--color-abril-ink);
  background: var(--color-abril-surface);
  transition: border-color 0.15s, box-shadow 0.15s;
  outline: none;
}
.abril-input:focus {
  border-color: var(--color-abril-primary);
  box-shadow: 0 0 0 3px rgba(76,175,80,0.15);
}
.abril-input::placeholder { color: var(--color-abril-placeholder); }

/* ── Label ──────────────────────────────────────────────────── */
.abril-label {
  display: block; margin-bottom: 4px;
  font-size: 0.78rem; font-weight: 600;
  color: var(--color-abril-primary-dark);
  text-transform: uppercase; letter-spacing: 0.04em;
}

/* ── Page container (reemplaza p-[20px] en mobile) ──────────── */
.page-container {
  padding: 16px;
}
@media (min-width: 640px) {
  .page-container { padding: 24px; }
}

/* ── Scroll horizontal en tablas mobile ─────────────────────── */
.table-scroll {
  width: 100%; overflow-x: auto;
  -webkit-overflow-scrolling: touch;
  border-radius: var(--radius-lg);
  border: 1px solid var(--color-abril-border);
}

/* ── Filtros responsive ─────────────────────────────────────── */
.filter-row {
  display: flex; flex-wrap: wrap; gap: 12px; align-items: flex-end;
}
@media (max-width: 639px) {
  .filter-row { gap: 10px; }
  .filter-row > * { min-width: calc(50% - 5px); flex: 1 1 calc(50% - 5px); }
  .filter-row > .filter-full { min-width: 100%; flex: 1 1 100%; }
}

/* ── Loader overlay (mantener igual) ───────────────────────── */
/* (no modificar el loader existente) */
```

---

## 3. HEADER SHELL — `src/app/shared/components/header/`

### 3.1 Qué cambia en `header.html`

**Desktop (sm+):** reemplazar el fondo `#f2f5f2` y logo por una barra más compacta.
**Mobile:** eliminar márgenes laterales excesivos, reducir altura, mejorar touch targets.

```html
<!-- header.html — REEMPLAZAR COMPLETO -->
<ng-container>

  <!-- ── DESKTOP ──────────────────────────────────────────── -->
  <div class="hidden sm:flex h-[56px] items-center justify-between px-5 bg-white border-b border-abril-border">
    <!-- Título de ruta -->
    <span class="text-[15px] font-semibold text-abril-ink truncate">{{ titulo }}</span>

    <!-- Avatar + dropdown -->
    <div class="relative shrink-0">
      <button (click)="toggleUserMenu($event)"
        class="cursor-pointer rounded-full focus:outline-none
               ring-2 ring-transparent hover:ring-abril-primary/30 transition-all">
        <img *ngIf="userPhotoSrc; else defaultIconD"
          [src]="userPhotoSrc" alt="Foto"
          class="w-9 h-9 rounded-full object-cover" />
        <ng-template #defaultIconD>
          <div class="w-9 h-9 rounded-full bg-abril-primary-light flex items-center justify-center">
            <i class="ti ti-user text-abril-primary-dark text-lg"></i>
          </div>
        </ng-template>
      </button>
      <ng-container *ngTemplateOutlet="userDropdown"></ng-container>
    </div>
  </div>

  <!-- ── MOBILE ───────────────────────────────────────────── -->
  <div class="sm:hidden flex items-center h-[52px] px-3 gap-3 bg-white border-b border-abril-border">
    <!-- Hamburger -->
    <button (click)="menuOpen = !menuOpen"
      class="w-9 h-9 flex items-center justify-center rounded-lg
             text-abril-primary hover:bg-abril-primary-light transition-colors shrink-0">
      <i class="ti text-xl" [class.ti-menu-2]="!menuOpen" [class.ti-x]="menuOpen"></i>
    </button>

    <!-- Título -->
    <span class="flex-1 text-[14px] font-semibold text-abril-ink truncate">{{ titulo }}</span>

    <!-- Avatar -->
    <div class="relative shrink-0">
      <button (click)="toggleUserMenu($event)"
        class="cursor-pointer rounded-full ring-2 ring-transparent
               hover:ring-abril-primary/30 transition-all">
        <img *ngIf="userPhotoSrc; else defaultIconM"
          [src]="userPhotoSrc" alt="Foto"
          class="w-8 h-8 rounded-full object-cover" />
        <ng-template #defaultIconM>
          <div class="w-8 h-8 rounded-full bg-abril-primary-light flex items-center justify-center">
            <i class="ti ti-user text-abril-primary-dark"></i>
          </div>
        </ng-template>
      </button>
      <ng-container *ngTemplateOutlet="userDropdown"></ng-container>
    </div>
  </div>

  <!-- Sidebar mobile (sin cambio funcional) -->
  <app-sidebar-mobile [(menuOpen)]="menuOpen"></app-sidebar-mobile>

</ng-container>

<!-- ── DROPDOWN compartido ────────────────────────────────────── -->
<ng-template #userDropdown>
  <div *ngIf="showUserMenu"
    class="absolute right-0 top-[44px] w-[220px] bg-white rounded-xl z-50 overflow-hidden"
    style="box-shadow: var(--shadow-dropdown); border: 1px solid var(--color-abril-border);">
    <div class="px-4 py-3 border-b border-abril-border">
      <p class="text-sm font-semibold text-abril-ink truncate">{{ userName ?? 'Usuario' }}</p>
      <p *ngIf="userJobTitle" class="text-xs font-medium truncate" style="color:var(--color-abril-primary)">{{ userJobTitle }}</p>
      <p class="text-xs text-abril-muted truncate">{{ userEmail ?? '' }}</p>
    </div>
    <button (click)="logout()"
      class="w-full flex items-center gap-2.5 px-4 py-3 text-sm text-abril-danger
             hover:bg-abril-danger-light transition-colors cursor-pointer">
      <i class="ti ti-logout w-4 h-4 shrink-0"></i>
      Cerrar sesión
    </button>
  </div>
</ng-template>
```

### 3.2 `header.css` — agregar solo esto
```css
/* Nada — todo via Tailwind + tokens globales */
```

---

## 4. SIDEBAR DESKTOP — `src/app/shared/components/sidebar/`

### 4.1 Cambios de comportamiento
- **Módulos con ruta directa** (habilitacion, control-acceso, clinica): navegan directamente → **sin cambio** (ya lo hace `onModuleClick`).
- **Módulos CON subitems** (los que abren dropdown hoy): **CAMBIO** → navegan a `module.baseRoute` directamente. El dropdown se elimina del sidebar. El usuario elige subsección dentro de la página destino (igual que PASO usa `ssoma-page-header` con tabs).
- El sidebar queda como lista de íconos verticales, sin flyouts.

### 4.2 `sidebar.html` — REEMPLAZAR COMPLETO

```html
<div class="flex flex-col h-screen py-4 px-2 gap-1"
     style="background: var(--color-abril-sidebar-bg); width: 88px;">

  <!-- Módulos visibles -->
  <ng-container *ngFor="let module of visibleModules; trackBy: trackByModuleKey">
    <button
      #moduleItem
      type="button"
      (click)="onModuleClick(module)"
      class="relative flex flex-col items-center justify-center gap-1 w-full
             py-2 px-1 rounded-xl cursor-pointer border-0 outline-none
             transition-all duration-150 group"
      [style.background]="isActiveModule(module.baseRoute)
        ? 'var(--color-abril-sidebar-active-bg)' : 'transparent'"
    >
      <!-- Indicador activo izquierda -->
      <span
        class="absolute left-0 top-1/2 -translate-y-1/2 w-[3px] h-6 rounded-r-full transition-all duration-200"
        [style.background]="isActiveModule(module.baseRoute)
          ? 'var(--color-abril-sidebar-active)' : 'transparent'"
      ></span>

      <!-- Ícono Tabler -->
      <i class="ti text-2xl transition-colors duration-150"
         [class]="'ti-' + module.iconKey"
         [style.color]="isActiveModule(module.baseRoute)
           ? 'var(--color-abril-sidebar-active)'
           : 'var(--color-abril-sidebar-text)'"
      ></i>

      <!-- Badge de alertas (programaciones rechazadas) -->
      <ng-container *ngIf="module.key === 'clinica' && (alertaSvc.rechazados$ | async) as count">
        <span *ngIf="count > 0"
          class="absolute top-1.5 right-1.5 min-w-[16px] h-4 px-1
                 rounded-full text-[9px] font-bold text-white flex items-center justify-center"
          style="background: var(--color-abril-danger);">
          {{ count }}
        </span>
      </ng-container>

      <!-- Label -->
      <span class="text-[10px] font-medium leading-tight text-center transition-colors duration-150"
            [style.color]="isActiveModule(module.baseRoute)
              ? 'var(--color-abril-sidebar-active)'
              : 'var(--color-abril-sidebar-text)'">
        {{ module.label }}
      </span>
    </button>
  </ng-container>

  <!-- Spacer -->
  <div class="flex-1"></div>

  <!-- Botón "Más" overflow -->
  <button
    *ngIf="overflowModules.length > 0"
    type="button"
    (click)="toggleOverflow()"
    class="flex flex-col items-center justify-center gap-1 w-full
           py-2 px-1 rounded-xl cursor-pointer border-0 outline-none transition-all"
    [style.background]="overflowOpen ? 'var(--color-abril-sidebar-active-bg)' : 'transparent'"
  >
    <i class="ti ti-dots text-2xl"
       [style.color]="overflowOpen
         ? 'var(--color-abril-sidebar-active)'
         : 'var(--color-abril-sidebar-text)'"></i>
    <span class="text-[10px] font-medium" style="color: var(--color-abril-sidebar-muted)">Más</span>
  </button>

</div>

<!-- Backdrop overflow -->
<div
  class="fixed inset-0 z-40 bg-black transition-opacity duration-300"
  [ngClass]="overflowOpen ? 'opacity-40 pointer-events-auto' : 'opacity-0 pointer-events-none'"
  (click)="closeOverflow()"
></div>

<!-- Panel overflow (módulos extra) -->
<div
  class="fixed top-4 bottom-4 left-[96px] w-64 z-50 rounded-xl overflow-hidden
         flex flex-col transition-all duration-300"
  style="background: var(--color-abril-sidebar-bg); box-shadow: var(--shadow-modal);"
  [ngClass]="overflowOpen ? 'translate-x-0 opacity-100' : '-translate-x-3 opacity-0 pointer-events-none'"
>
  <div class="flex items-center justify-between px-4 py-3"
       style="border-bottom: 1px solid rgba(255,255,255,0.08);">
    <span class="text-sm font-semibold" style="color: var(--color-abril-sidebar-text)">Módulos</span>
    <button class="p-1 rounded-lg transition-colors hover:bg-white/10 border-0 cursor-pointer" (click)="closeOverflow()">
      <i class="ti ti-x text-base" style="color: var(--color-abril-sidebar-muted)"></i>
    </button>
  </div>
  <div class="flex-1 overflow-y-auto p-2 flex flex-col gap-1">
    <button
      *ngFor="let module of overflowModules; trackBy: trackByModuleKey"
      type="button"
      (click)="onModuleClick(module); closeOverflow()"
      class="flex items-center gap-3 px-3 py-2.5 rounded-lg cursor-pointer border-0 outline-none
             transition-colors text-left w-full"
      [style.background]="isActiveModule(module.baseRoute)
        ? 'var(--color-abril-sidebar-active-bg)' : 'transparent'"
      [class.hover:bg-white/5]="!isActiveModule(module.baseRoute)"
    >
      <i class="ti text-xl shrink-0"
         [class]="'ti-' + module.iconKey"
         [style.color]="isActiveModule(module.baseRoute)
           ? 'var(--color-abril-sidebar-active)'
           : 'var(--color-abril-sidebar-text)'"></i>
      <span class="text-sm"
            [style.color]="isActiveModule(module.baseRoute)
              ? 'var(--color-abril-sidebar-active)'
              : 'var(--color-abril-sidebar-text)'">
        {{ module.label }}
      </span>
    </button>
  </div>
</div>
```

### 4.3 `sidebar.ts` — cambiar `onModuleClick`

Reemplazar el método `onModuleClick` por este (sin tocar nada más del archivo):

```typescript
onModuleClick(module: NavModule): void {
  // Navega directamente al baseRoute de cada módulo.
  // Los módulos con ruta especial mantienen su lógica.
  if (module.key === 'habilitacion') {
    if (this.authService.isContratista()) {
      this.router.navigate(['/habilitacion/dashboard-contratista']);
    } else {
      this.router.navigate(['/habilitacion/gestion']);
    }
  } else {
    this.router.navigate([module.baseRoute]);
  }
  // Cerrar cualquier menú abierto
  this.activeMenu = null;
  this.activeGroup = null;
}
```

> NOTA: si algún `module.baseRoute` no tiene ruta definida en el router, el usuario verá 404. Verificar en `NavigationService` que todos los baseRoutes tengan ruta. No crear rutas nuevas — solo verificar.

### 4.4 `sidebar.css` — REEMPLAZAR COMPLETO

```css
:host {
  display: block;
  height: 100vh;
  flex-shrink: 0;
}
```

---

## 5. SIDEBAR MOBILE — `src/app/shared/components/sidebar-mobile/`

### 5.1 `sidebar-mobile.html` — REEMPLAZAR COMPLETO

```html
<!-- Backdrop -->
<div
  *ngIf="menuOpen"
  class="fixed inset-0 z-40 bg-black/50 backdrop-blur-sm"
  (click)="close()"
></div>

<!-- Drawer -->
<div
  class="fixed top-0 left-0 bottom-0 z-50 flex flex-col w-[280px]
         transition-transform duration-300 ease-out"
  style="background: var(--color-abril-sidebar-bg);
         box-shadow: 4px 0 24px rgba(0,0,0,0.3);"
  [class.-translate-x-full]="!menuOpen"
>

  <!-- Header del drawer -->
  <div class="flex items-center justify-between px-4 h-[52px] shrink-0"
       style="border-bottom: 1px solid rgba(255,255,255,0.08);">
    <span class="text-xs font-semibold tracking-widest uppercase"
          style="color: var(--color-abril-sidebar-muted)">Menú</span>
    <button (click)="close()"
      class="w-8 h-8 flex items-center justify-center rounded-lg
             border-0 cursor-pointer transition-colors hover:bg-white/10">
      <i class="ti ti-x text-lg" style="color: var(--color-abril-sidebar-text)"></i>
    </button>
  </div>

  <!-- Navegación -->
  <nav class="flex-1 overflow-y-auto py-3 px-2 flex flex-col gap-0.5">
    <ng-container *ngFor="let module of navService.getModules(); trackBy: trackByModuleKey">

      <!-- Item de módulo -->
      <button
        type="button"
        (click)="navigateTo(module)"
        class="flex items-center gap-3 w-full px-3 py-2.5 rounded-xl
               border-0 cursor-pointer text-left outline-none
               transition-colors hover:bg-white/8"
      >
        <div class="w-8 h-8 flex items-center justify-center rounded-lg shrink-0"
             [style.background]="'rgba(76,175,80,0.15)'">
          <i class="ti text-lg" [class]="'ti-' + module.iconKey"
             style="color: var(--color-abril-sidebar-active)"></i>
        </div>
        <span class="text-sm font-medium flex-1"
              style="color: var(--color-abril-sidebar-text)">{{ module.label }}</span>
        <i *ngIf="module.items?.length || module.groups?.length"
           class="ti ti-chevron-right text-sm shrink-0"
           style="color: var(--color-abril-sidebar-muted)"></i>
      </button>

    </ng-container>
  </nav>

  <!-- Footer del drawer -->
  <div class="px-4 py-3 shrink-0" style="border-top: 1px solid rgba(255,255,255,0.08);">
    <span class="text-[10px]" style="color: var(--color-abril-sidebar-muted)">
      Abril · SSOMA 2026
    </span>
  </div>
</div>
```

### 5.2 `sidebar-mobile.ts` — agregar método `navigateTo`

Agregar en la clase (sin tocar nada más):

```typescript
import { Router } from '@angular/router';
// Agregar Router al constructor:
constructor(public navService: NavigationService, private router: Router) {}

navigateTo(module: NavModule): void {
  this.router.navigate([module.baseRoute]);
  this.close();
}
```

---

## 6. LAYOUT — `src/app/shared/components/layout/layout.html`

Cambio: sidebar ahora tiene ancho fijo integrado. Ajustar solo padding/fondo:

```html
<div class="flex h-screen" style="background: var(--color-abril-sidebar-bg);">
  <app-sidebar class="hidden sm:block shrink-0"></app-sidebar>
  <div class="flex-1 min-w-0 flex flex-col"
       style="background: var(--color-abril-bg); border-radius: 16px 0 0 16px; overflow: hidden;"
       [class.rounded-none]="isFullPage()">
    <main class="flex-1 flex flex-col min-h-0 overflow-auto"
          [class.p-0]="isFullPage()"
          [style.padding]="isFullPage() ? '0' : null">
      <ng-container *ngIf="isFullPage(); else normalLayout">
        <app-header style="position:fixed; top:0; right:0; z-index:999;
                           background:transparent; width:auto;"></app-header>
        <router-outlet></router-outlet>
      </ng-container>
      <ng-template #normalLayout>
        <app-header></app-header>
        <div class="page-content flex-1 min-h-0 overflow-y-auto">
          <router-outlet></router-outlet>
        </div>
      </ng-template>
    </main>
  </div>
</div>
```

---

## 7. ICONOS — `NavigationService` / `nav.model`

Los `iconKey` en `NavigationService` deben ser nombres de Tabler Icons válidos (sin el prefijo `ti-`). Verificar que existan en https://tabler.io/icons.

### Mapa de íconos recomendados por módulo

| module.key | iconKey sugerido |
|---|---|
| habilitacion | `users-group` |
| control-acceso | `shield-check` |
| gestion-administrativa | `briefcase` |
| mejora-continua | `trending-up` |
| proyectos | `building-estate` |
| contratistas | `file-certificate` |
| costos | `coins` |
| arquitectura-comercial | `building` |
| clinica / salud | `heart-rate-monitor` |
| ssoma-gestion | `shield` |
| evaluaciones | `clipboard-check` |

> ACCIÓN: abrir `NavigationService`, revisar los `iconKey` actuales y reemplazarlos por los de la tabla de arriba. Si algún módulo no aparece en la tabla, buscar en tabler.io un ícono apropiado.

---

## 8. MÓDULOS EXISTENTES — MIGRACIÓN DE COLORES

### 8.1 Regla general
Buscar y reemplazar hardcodes en TODOS los HTMLs de módulos:

| Viejo | Nuevo (clase Tailwind o CSS var) |
|---|---|
| `bg-[#64BC04]` | `btn-primary` o `style="background:var(--color-abril-primary)"` |
| `text-[#64BC04]` | `style="color:var(--color-abril-primary)"` |
| `bg-[#E5F7D1]` | `style="background:var(--color-abril-primary-light)"` |
| `text-[#64BC04]` en thead | `style="color:var(--color-abril-primary-dark)"` |
| `bg-[#0086A5]` | `btn-accent` o `style="background:var(--color-abril-accent)"` |
| `text-[#0086A5]` | `style="color:var(--color-abril-accent)"` |
| `hover:bg-[#57a803]` | eliminar (ya está en `.btn-primary:hover`) |
| `border-[#D6DEE5]` | `style="border-color:var(--color-abril-border)"` |
| `focus:border-[#64BC04]` | ya cubierto por `.abril-input:focus` |

### 8.2 Archivos a migrar (prioridad)
1. `gestion-salidas.html` — usa `#64BC04`, `#0086A5`, `#E5F7D1`, `#D6DEE5`
2. Todos los HTMLs en `mejora-continua/`
3. Todos los HTMLs en `gestion-administrativa/`
4. `ssoma-page-header.component.css` — reemplazar colores hardcodeados
5. `sidebar.html` ya migrado (sección 4)
6. `header.html` ya migrado (sección 3)

> IMPORTANTE: hacer los reemplazos de a un módulo por sesión. No hacer todos juntos.

---

## 9. MOBILE — PROBLEMAS ESPECÍFICOS Y SOLUCIONES

### 9.1 Padding excesivo en pages

En todos los componentes de página que usen `p-[20px]` o `p-4` como wrapper externo:
```html
<!-- ANTES -->
<div class="p-[20px]">

<!-- DESPUÉS -->
<div class="page-container">
```

### 9.2 Filtros en mobile

Los filtros con `flex-wrap` y `w-[220px]` fijos se rompen en mobile. Usar:
```html
<!-- ANTES -->
<div class="flex flex-wrap gap-4 items-end">
  <div class="w-[220px]">...</div>

<!-- DESPUÉS -->
<div class="filter-row">
  <div>...</div>  <!-- sin width fijo -->
```

### 9.3 Tablas en mobile

Envolver toda tabla en:
```html
<div class="table-scroll">
  <table class="abril-table">...</table>
</div>
```

### 9.4 Sidebar mobile — `top-17` hardcodeado

El drawer ya no usa `top-17`. El nuevo sidebar-mobile arranca desde `top-0` (cubre toda la pantalla incluido el header shell). El backdrop también es `inset-0`. El header shell mobile tiene `h-[52px]` — el drawer lo cubre y tiene su propio header interno con botón cerrar.

---

## 10. SSOMA PAGE HEADER — `ssoma-page-header.component.css`

No tocar el HTML ni el TS. Solo actualizar el CSS para usar los tokens:

```css
/* ssoma-page-header.component.css — REEMPLAZAR COMPLETO */

.ssoma-hero {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 16px 20px;
  background: var(--color-abril-sidebar-bg);
  border-radius: 0 0 var(--radius-lg) var(--radius-lg);
  flex-wrap: wrap;
}

.ssoma-hero__left {
  display: flex;
  align-items: center;
  gap: 14px;
  flex-shrink: 0;
}

.ssoma-hero__badge {
  display: inline-flex;
  align-items: center;
  padding: 4px 12px;
  border-radius: var(--radius-pill);
  background: var(--color-abril-primary);
  color: #fff;
  font-size: 0.72rem;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  white-space: nowrap;
}

.ssoma-hero__vline {
  width: 1px;
  height: 36px;
  background: rgba(255,255,255,0.12);
  flex-shrink: 0;
}

.ssoma-hero__title {
  font-size: 1.15rem;
  font-weight: 700;
  color: #fff;
  margin: 0;
  line-height: 1.2;
}

.ssoma-hero__sub {
  font-size: 0.78rem;
  color: var(--color-abril-sidebar-muted);
  margin: 2px 0 0;
}

.ssoma-hero__center {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  flex: 1;
}

.ssoma-hero__pill {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 4px 12px;
  border-radius: var(--radius-pill);
  background: rgba(255,255,255,0.08);
  color: var(--color-abril-sidebar-text);
  font-size: 0.78rem;
  font-weight: 500;
  border: 1px solid rgba(255,255,255,0.1);
}

.ssoma-hero__pill--warn {
  background: rgba(217,119,6,0.2);
  color: #FCD34D;
  border-color: rgba(217,119,6,0.3);
}

.ssoma-hero__right {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-left: auto;
}

.ssoma-hero__btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 0 16px;
  height: 36px;
  border-radius: var(--radius-md);
  font-size: 0.85rem;
  font-weight: 500;
  cursor: pointer;
  border: none;
  transition: all 0.15s ease;
}

.ssoma-hero__btn--primary {
  background: var(--color-abril-primary);
  color: #fff;
}
.ssoma-hero__btn--primary:hover {
  background: var(--color-abril-primary-hover);
}

.ssoma-hero__btn--ghost {
  background: rgba(255,255,255,0.08);
  color: var(--color-abril-sidebar-text);
  border: 1px solid rgba(255,255,255,0.15);
}
.ssoma-hero__btn--ghost:hover {
  background: rgba(255,255,255,0.14);
}

.ssoma-hero__avatar-gap {
  width: 44px; /* espacio para el avatar flotante */
  flex-shrink: 0;
}

/* Mobile */
@media (max-width: 639px) {
  .ssoma-hero {
    padding: 12px 14px;
    gap: 10px;
    border-radius: 0;
  }
  .ssoma-hero__vline { display: none; }
  .ssoma-hero__center { display: none; } /* pills se ocultan en mobile, solo header */
  .ssoma-hero__right { margin-left: 0; }
  .ssoma-hero__avatar-gap { width: 40px; }
  .ssoma-hero__btn span,
  .ssoma-hero__btn { font-size: 0.8rem; padding: 0 12px; height: 34px; }
}
```

---

## 11. PLAN DE SESIONES PARA CLAUDE CODE

### Sesión 1 — Tokens + Layout base (SOLO ESTILOS GLOBALES)
**Archivos a tocar:** `src/styles.css`, `layout.html`
**Verificar:** compilar `ng serve`, revisar que la app carga sin errores.
**Tiempo estimado:** ~8k tokens

### Sesión 2 — Header shell + Sidebar desktop
**Archivos a tocar:** `header.html`, `header.css`, `sidebar.html`, `sidebar.css`, `sidebar.ts` (solo método `onModuleClick`)
**Verificar:** navegar a todos los módulos, confirmar que el sidebar navega directo sin dropdown.
**Tiempo estimado:** ~10k tokens

### Sesión 3 — Sidebar mobile + NavigationService iconos
**Archivos a tocar:** `sidebar-mobile.html`, `sidebar-mobile.ts`, `NavigationService` (solo `iconKey`)
**Verificar:** en mobile, abrir drawer, navegar a un módulo, cerrar.
**Tiempo estimado:** ~8k tokens

### Sesión 4 — ssoma-page-header CSS + módulo Gestión de Salidas
**Archivos a tocar:** `ssoma-page-header.component.css`, `gestion-salidas.html`
**Verificar:** header SSOMA se ve con la nueva paleta, tabla tiene scroll en mobile.
**Tiempo estimado:** ~8k tokens

### Sesión 5 en adelante — Un módulo por sesión
Migrar colores hardcodeados siguiendo la tabla de la sección 8.1.
Orden sugerido: mejora-continua → gestion-administrativa → costos → proyectos → evaluaciones.

---

## 12. REGLAS ANTI-ROTURA

1. **NO** tocar archivos `.ts` de lógica (services, DTOs, resolvers, guards).
2. **NO** modificar rutas en `app.routes.ts` ni en ningún módulo de routing.
3. **NO** cambiar nombres de clases CSS que sean referenciadas en `.ts` (ej: `isFullPage()` busca clases específicas, no tocar).
4. **NO** cambiar la estructura HTML de `router-outlet` ni de `app-sidebar-mobile`.
5. **SÍ** verificar `ng build` sin errores antes de dar tarea por terminada.
6. **SÍ** confirmar en browser que en mobile (375px) el layout no desborda.
7. El color `#64BC04` **NO debe aparecer en ningún archivo HTML después de la migración**.
8. Tabler Icons: verificar que el CDN o el paquete npm esté incluido en el proyecto antes de usar clases `ti-*`.

---

## 13. VERIFICAR TABLER ICONS

Antes de la Sesión 2, ejecutar en FRONTEND:
```bash
grep -r "tabler" index.html src/styles.css package.json
```
Si no está instalado:
```bash
npm install @tabler/icons-webfont
```
Y agregar en `styles.css` o `angular.json`:
```css
@import '@tabler/icons-webfont/dist/tabler-icons.min.css';
```
