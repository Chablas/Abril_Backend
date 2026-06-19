# CONTEXT_AC_FULL.md

## MÓDULO ARQUITECTURA COMERCIAL
### Backend .NET 10 + Frontend Angular

---

## 🖥️ BACKEND (.NET 10 - C#)

### MAPA DE ARCHIVOS
📁 Controllers/
└── ArquitecturaComercialController.cs → Endpoints: GET /dashboard, GET /actividades, CRUD

📁 Application/DTOs/ArquitecturaComercial/
├── ArqComercialDashboardDTO.cs → { total, culminadas, enProceso, vencidas, pendientes, avance }
├── ActividadListItemDTO.cs → { id, titulo, estado, fechaInicio, fechaFin, encargado }
├── AcActividadCreateDTO.cs
├── AcActividadUpdateDTO.cs
└── ActividadListResponseDTO.cs

📁 Application/Interfaces/
└── IArquitecturaComercialService.cs

📁 Application/Services/
└── ArquitecturaComercialService.cs → Lógica de negocio

📁 Infrastructure/Models/
└── AcActividad.cs → { Id, Titulo, Estado, FechaInicio, FechaFin, Encargado }

📁 Infrastructure/Interfaces/
└── IArquitecturaComercialRepository.cs

📁 Infrastructure/Repositories/
└── ArquitecturaComercialRepository.cs → Consultas EF Core

text

### ENDPOINTS
GET /api/arquitectura-comercial/dashboard → Estadísticas
GET /api/arquitectura-comercial/actividades → Lista paginada (filtro estado)
POST /api/arquitectura-comercial/actividad → Crear
PUT /api/arquitectura-comercial/actividad → Actualizar
DELETE /api/arquitectura-comercial/actividad/{id} → Eliminar

text

### ESTADOS VÁLIDOS
- Culminadas
- En proceso
- Vencidas
- Pendientes

---

## 🎨 FRONTEND (Angular)

### MAPA DE ARCHIVOS (Basado en tu estructura)
📁 src/app/
├── 📁 dashboard/
│ ├── dashboard.component.ts → Vista principal con gráfica
│ ├── dashboard.component.html → Template con la gráfica
│ ├── dashboard.component.css → Estilos
│ └── dashboard.module.ts → Declara componentes del dashboard
│
├── 📁 actividades/
│ ├── 📁 components/
│ │ ├── lista-actividades/
│ │ │ ├── lista-actividades.component.ts → Lista con filtros
│ │ │ ├── lista-actividades.component.html
│ │ │ └── lista-actividades.component.css
│ │ └── grafica-estado/
│ │ ├── grafica-estado.component.ts → Componente reutilizable de gráfica
│ │ ├── grafica-estado.component.html
│ │ └── grafica-estado.component.css
│ ├── 📁 services/
│ │ ├── actividad.service.ts → CRUD de actividades
│ │ └── estadistica.service.ts → GET /api/arquitectura-comercial/dashboard
│ ├── 📁 models/
│ │ └── actividad.model.ts → Interface Actividad
│ └── actividades.module.ts
│
├── 📁 gantt/
│ └── gantt.component.ts → Diagrama de Gantt
│
├── arquitectura-comercial-module.ts → Módulo raíz
└── arquitectura-comercial-routing-module.ts → Rutas: /dashboard, /actividades, /gantt

text

---

## FRONTEND - ESQUELETO DE ARCHIVOS CLAVE

### 1. Dashboard Component (dashboard.component.ts)
```typescript
import { Component, OnInit } from '@angular/core';
import { EstadisticaService } from '../actividades/services/estadistica.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})
export class DashboardComponent implements OnInit {
  estadisticas: any = {
    total: 0,
    culminadas: 0,
    enProceso: 0,
    vencidas: 0,
    pendientes: 0,
    avance: 0
  };
  loading = false;

  constructor(
    private estadisticaService: EstadisticaService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.cargarEstadisticas();
  }

  cargarEstadisticas(): void {
    this.loading = true;
    this.estadisticaService.getDashboard().subscribe({
      next: (data) => {
        this.estadisticas = data;
        this.loading = false;
      },
      error: (error) => {
        console.error('Error al cargar estadísticas', error);
        this.loading = false;
      }
    });
  }

  filtrarPorEstado(estado: string): void {
    this.router.navigate(['/actividades'], { 
      queryParams: { estado: estado.toLowerCase().replace(' ', '-') }
    });
  }
}
2. Dashboard Template (dashboard.component.html)
html
<div class="dashboard-container" *ngIf="!loading">
  <h2>Distribución por Estado</h2>
  <p><strong>{{ estadisticas.total }}</strong> actividades</p>

  <div class="stats-grid">
    <!-- Culminadas -->
    <div class="stat-card culminated" (click)="filtrarPorEstado('Culminadas')">
      <span class="stat-value">{{ estadisticas.culminadas }}</span>
      <span class="stat-label">Culminadas</span>
      <span class="stat-percentage">{{ estadisticas.avance }}%</span>
    </div>

    <!-- En proceso -->
    <div class="stat-card in-progress" (click)="filtrarPorEstado('En proceso')">
      <span class="stat-value">{{ estadisticas.enProceso }}</span>
      <span class="stat-label">En proceso</span>
      <span class="stat-percentage">{{ (estadisticas.enProceso / estadisticas.total * 100) | number:'1.0-0' }}%</span>
    </div>

    <!-- Vencidas -->
    <div class="stat-card overdue" (click)="filtrarPorEstado('Vencidas')">
      <span class="stat-value">{{ estadisticas.vencidas }}</span>
      <span class="stat-label">Vencidas</span>
      <span class="stat-percentage">{{ (estadisticas.vencidas / estadisticas.total * 100) | number:'1.0-0' }}%</span>
    </div>

    <!-- Pendientes -->
    <div class="stat-card pending" (click)="filtrarPorEstado('Pendientes')">
      <span class="stat-value">{{ estadisticas.pendientes }}</span>
      <span class="stat-label">Pendientes</span>
      <span class="stat-percentage">{{ (estadisticas.pendientes / estadisticas.total * 100) | number:'1.0-0' }}%</span>
    </div>
  </div>

  <!-- Barra de avance -->
  <div class="progress-container">
    <div class="progress-bar" [style.width.%]="estadisticas.avance">
      {{ estadisticas.avance }}%
    </div>
  </div>
</div>
3. Estadistica Service (estadistica.service.ts)
typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class EstadisticaService {
  private apiUrl = environment.apiUrl + '/api/arquitectura-comercial';

  constructor(private http: HttpClient) {}

  getDashboard(): Observable<any> {
    return this.http.get(`${this.apiUrl}/dashboard`);
  }

  getActividades(estado?: string, page: number = 1, pageSize: number = 10): Observable<any> {
    let url = `${this.apiUrl}/actividades?page=${page}&pageSize=${pageSize}`;
    if (estado) {
      url += `&estado=${estado}`;
    }
    return this.http.get(url);
  }

  crearActividad(actividad: any): Observable<any> {
    return this.http.post(`${this.apiUrl}/actividad`, actividad);
  }

  actualizarActividad(actividad: any): Observable<any> {
    return this.http.put(`${this.apiUrl}/actividad`, actividad);
  }

  eliminarActividad(id: number): Observable<any> {
    return this.http.delete(`${this.apiUrl}/actividad/${id}`);
  }
}
4. Actividad Model (actividad.model.ts)
typescript
export interface Actividad {
  id: number;
  titulo: string;
  estado: 'Culminadas' | 'En proceso' | 'Vencidas' | 'Pendientes';
  fechaInicio: Date;
  fechaFin: Date;
  encargado?: string;
  categoria?: string;
  progreso: number;
}

export interface DashboardEstadisticas {
  total: number;
  culminadas: number;
  enProceso: number;
  vencidas: number;
  pendientes: number;
  avance: number;
  distribucion: {
    [key: string]: number;
  };
}

export interface ActividadListResponse {
  items: Actividad[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
5. Lista Actividades Component (lista-actividades.component.ts)
typescript
import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { EstadisticaService } from '../../services/estadistica.service';
import { Actividad, ActividadListResponse } from '../../models/actividad.model';

@Component({
  selector: 'app-lista-actividades',
  templateUrl: './lista-actividades.component.html',
  styleUrls: ['./lista-actividades.component.css']
})
export class ListaActividadesComponent implements OnInit {
  actividades: Actividad[] = [];
  totalCount = 0;
  page = 1;
  pageSize = 10;
  totalPages = 0;
  estadoFiltro: string | null = null;
  loading = false;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private estadisticaService: EstadisticaService
  ) {}

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      this.estadoFiltro = params['estado'] || null;
      this.page = 1;
      this.cargarActividades();
    });
  }

  cargarActividades(): void {
    this.loading = true;
    this.estadisticaService.getActividades(
      this.estadoFiltro || undefined,
      this.page,
      this.pageSize
    ).subscribe({
      next: (response: ActividadListResponse) => {
        this.actividades = response.items;
        this.totalCount = response.totalCount;
        this.totalPages = response.totalPages;
        this.loading = false;
      },
      error: (error) => {
        console.error('Error al cargar actividades', error);
        this.loading = false;
      }
    });
  }

  cambiarPagina(page: number): void {
    if (page >= 1 && page <= this.totalPages) {
      this.page = page;
      this.cargarActividades();
    }
  }

  getEstadoClass(estado: string): string {
    return estado.toLowerCase().replace(' ', '-');
  }
}
6. Enrutamiento (arquitectura-comercial-routing-module.ts)
typescript
import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { DashboardComponent } from './dashboard/dashboard.component';
import { ListaActividadesComponent } from './actividades/components/lista-actividades/lista-actividades.component';
import { GanttComponent } from './gantt/gantt.component';

const routes: Routes = [
  { path: 'dashboard', component: DashboardComponent },
  { path: 'actividades', component: ListaActividadesComponent },
  { path: 'gantt', component: GanttComponent },
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class ArquitecturaComercialRoutingModule { }
COMUNICACIÓN BACKEND ↔ FRONTEND
Flujo de Datos
text
1. Frontend (DashboardComponent) 
   → llama a EstadisticaService.getDashboard()

2. EstadisticaService 
   → hace GET a /api/arquitectura-comercial/dashboard

3. Backend (ArquitecturaComercialController) 
   → recibe la petición
   → llama a ArquitecturaComercialService.GetDashboardAsync()

4. Service 
   → consulta a Repository
   → calcula estadísticas
   → devuelve ArqComercialDashboardDTO

5. Frontend recibe el DTO 
   → lo muestra en la gráfica