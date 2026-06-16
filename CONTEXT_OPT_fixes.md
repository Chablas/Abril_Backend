# CONTEXT_OPT_fixes.md — Fixes módulo OPT frontend
# Fecha: 2026-06-15

---

## FIX 1 — Backend: columna se_obtuvo_compromiso

En `OptModels.cs`, clase `SsomaOpt`, agregar `[Column]` explícito:

```csharp
// ANTES
public bool SeObtuvoCCompromiso { get; set; }

// DESPUÉS
[Column("se_obtuvo_compromiso")]
public bool SeObtuvoCCompromiso { get; set; }
```

Compilar y pushear.

---

## SQL — INSERT 36 PETs en pgAdmin

```sql
INSERT INTO ssoma_pet (nombre, codigo, sharepoint_url) VALUES
('Trabajos en Altura', 'SSO-PETS-01', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-01 Trabajos en Altura.docx'),
('Trabajos en Caliente', 'SSO-PETS-02', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-02 Trabajos en Caliente.docx'),
('Montaje, uso y desmontaje de andamios', 'SSO-PETS-03', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-03 Montaje, uso y desmontaje de andamios.docx'),
('Espacios Confinados', 'SSO-PETS-04', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-04 Espacios Confinados.docx'),
('Trabajo Eléctricos', 'SSO-PETS-05', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-05 Trabajo Electricos.docx'),
('Elevación vertical de materiales con equipos eléctrico', 'SSO-PETS-06', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-06 Elevación vertical de materiales con equipos eléctrico.docx'),
('Vaciado de concreto', 'SSO-PETS-07', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-07 Vaciado de concreto.docx'),
('Orden y Limpieza', 'SSO-PETS-08', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-08 Orden y Limpieza JA.docx'),
('Excavaciones', 'SSO-PETS-09', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-09 Excavaciones.docx'),
('Instalación de línea de vida', 'SSO-PETS-10', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-10 Instalacion de linea de vida.docx'),
('Trabajos con pintura', 'SSO-PETS-11', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-11 Trabajos con pintura.docx'),
('Encofrado y desencofrado', 'SSO-PETS-12', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-12 Encofrado y desencofrado.docx'),
('Izaje de carga', 'SSO-PETS-13', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-13 Izaje de carga.docx'),
('Colocación de Prelosas', 'SSO-PETS-14', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-14 Colocación de Prelosas JA.docx'),
('Carpintería', 'SSO-PETS-15', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-15 Carpinteria.docx'),
('Control Vehicular, peatonal y ciclovía', 'SSO-PETS-16', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-16 Control Vehicular, peatonal y ciclovia.docx'),
('Tarrajeo', 'SSO-PETS-17', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-17 Tarrajeo (FALTA).docx'),
('Izaje de materiales', 'SSO-PETS-18', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-18 Izaje de materiales.docx'),
('Limpieza de oficinas, sala de venta y vecinos', 'SSO-PETS-19', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-19 Limpieza de oficinas sala de venta vecinos.docx'),
('Enchape', 'SSO-PETS-20', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-20 Enchape.docx'),
('Limpieza de Oficina Central y Departamento Piloto', 'SSO-PETS-21', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-21 Limpieza de oficina Central y Departamento Piloto.docx'),
('Habilitado y Corte de acero', 'SSO-PETS-22', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-22 Habilitado y Corte de acero.docx'),
('Colocación de Acero de Refuerzo', 'SSO-PETS-23', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-23 Colocación de Acero de Refuerzo.docx'),
('Izaje de materiales con grúa telescópica', 'SSO-PETS-24', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-24 Izaje de materiales con grua telescopica.docx'),
('Manipulación manual de carga', 'SSO-PETS-25', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-25 Manipulacion manual de carga.docx'),
('Montaje y desmontaje de protecciones colectivas', 'SSO-PETS-26', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-26 Montaje y desmontaje de protecciones colectivas JA - copia.docx'),
('Operación y Gestión de Servicio de Seguridad de Vigilancia', 'SSO-PETS-27', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-27 Operacion y Gestión de Servicio de Seguridad de Vigilancia.docx'),
('Acarreo de Vidrios', 'SSO-PETS-28', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-28 Acarreo de Vidrios.docx'),
('Pintura en interiores de departamentos', 'SSO-PETS-29', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-29 Pintura en interiores de departamentos.docx'),
('Movimiento de material con maquinaria', 'SSO-PETS-30', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-30 Movimiento de material con maquinaria.docx'),
('Habilitación de instalaciones provisionales', 'SSO-PETS-31', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-31 Habilitación de instalaciones provisionales.docx'),
('Retiro de materiales y estructuras', 'SSO-PETS-32', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-32 Retiro de materiales, estructuras.docx'),
('Retiro de cerco metálico', 'SSO-PETS-33', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-33 Retiro de cerco metálico.docx'),
('Levantamiento Topográfico', 'SSO-PETS-34', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-34 Levantamiento Topógrafico (En proceso).docx'),
('Acero', 'SSO-PETS-35', 'https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/PETSAbril2026/SSO-PETS-35 Acero.docx');
```

---

## FIXES FRONTEND — opt-nuevo

### FIX 2 — Proyecto: combobox global reutilizable

Buscar cómo está implementado el selector de proyecto en `features/habilitacion/` o
`features/ssoma/gestion/rac/` — usar exactamente el mismo componente/servicio.
El proyecto debe:
- Ordenarse alfabéticamente
- Ser un combobox con búsqueda (no select nativo)
- Reutilizar `ProjectService` o el servicio que ya existe

### FIX 3 — Observador: buscador de workers

El observador (nombre + cargo) debe usar el mismo buscador de trabajadores
que ya existe en `features/habilitacion/pages/trabajadores/`.
- Buscar por nombre o DNI (mínimo 3 caracteres → llamar API)
- Al seleccionar: autocompletar `observador_nombre` con `person.fullName`
  y `observador_cargo` con `worker.ocupacion`
- Mismo componente que se usa para buscar trabajadores observados

### FIX 4 — Buscador trabajadores observados

Reutilizar el mismo componente buscador de trabajadores que ya existe en la app.
Buscar en `features/habilitacion/` el componente o servicio de búsqueda de workers.
El endpoint que usan actualmente es el mismo que debe usar OPT.
- Buscar por nombre o DNI
- Mostrar: nombre completo + DNI + empresa + ocupación
- Permitir agregar múltiples trabajadores (lista con chip + botón eliminar)

### FIX 5 — Visor PET inline

Usar el mismo patrón de visor de documentos SharePoint que ya existe en:
`features/habilitacion/` (aprobación de trabajadores/empresas).
- Al seleccionar un PET del dropdown → mostrar botón "Ver PET"
- Abrir el documento usando el mismo componente visor que ya existe en la app
- El `sharepoint_url` viene en el catálogo desde `GET /api/v1/ssoma-opt/catalogos`

### FIX 6 — Etiquetas S/I/NA → texto completo

En el paso de pasos observados, cambiar las etiquetas:
```
S → Seguro ✓
I → Inseguro ✗  
NA → No aplica —
```
Mostrar texto completo en desktop, icono + inicial en mobile (< 480px).

### FIX 7 — Eliminar niveles N1/N2/N3

Simplificar el wizard de pasos — eliminar la selección de nivel.
Solo descripción + resultado (Seguro/Inseguro/No aplica).
Numeración automática simple: 1, 2, 3, 4...

### FIX 8 — Validación paso 1

El botón "Siguiente" del paso 1 solo se activa cuando están completos:
- proyectoId (required)
- fecha (required)
- tipoObservacion (required)
- cuentaConPet (toggle — siempre tiene valor)
- seInformaTrabajador (toggle — siempre tiene valor)
- area (required)
- observadorNombre (required — desde buscador worker)

### FIX 9 — Validación paso 2

El botón "Siguiente" del paso 2 solo se activa cuando hay al menos 1 trabajador agregado.

---

## INSTRUCCIÓN PARA CLAUDE CODE

```
Lee CONTEXT_OPT_fixes.md y aplica todos los fixes en orden.

FIX 1 (backend): En OptModels.cs agregar [Column("se_obtuvo_compromiso")] 
sobre SeObtuvoCCompromiso. Compilar y pushear.

FIX 2-9 (frontend): En opt-nuevo.component:
- FIX 2: Proyecto — buscar cómo está implementado el combobox de proyecto 
  en rac-nuevo o paso-lista y reutilizar exactamente ese patrón
- FIX 3 y 4: Observador y trabajadores — buscar el servicio/componente de 
  búsqueda de workers en features/habilitacion/pages/trabajadores/ y reutilizarlo.
  Al seleccionar observador: autocompletar nombre desde person.fullName 
  y cargo desde worker.ocupacion
- FIX 5: Visor PET — buscar el componente visor de SharePoint docs que ya 
  existe en habilitacion (aprobación trabajadores/empresas) y reutilizarlo
- FIX 6: S/I/NA → "Seguro / Inseguro / No aplica" texto completo
- FIX 7: Eliminar niveles N1/N2/N3, solo numeración 1,2,3 automática
- FIX 8: Validar paso 1 — siguiente deshabilitado hasta completar campos required
- FIX 9: Validar paso 2 — siguiente deshabilitado hasta tener ≥1 trabajador

IMPORTANTE: Antes de implementar FIX 2,3,4,5 — leer primero los archivos 
existentes en habilitacion y rac-nuevo para reutilizar exactamente lo que ya 
funciona. No reinventar.
```
