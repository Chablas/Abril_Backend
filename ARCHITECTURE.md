# Arquitectura del proyecto — Backend

## Estructura general

El proyecto usa **arquitectura por features**. Cada funcionalidad es un módulo
independiente. Las carpetas raíz `Controllers/`, `Application/` e `Infrastructure/`
están **deprecadas** y no deben usarse para código nuevo.

## Organización de carpetas

```
Abril-Backend/
├── Features/
│ ├── CostsModule/
│ │ ├── Features/
│ │ │ ├── AdjudicacionesFeature/
│ │ │ │ ├── Application/
│ │ │ │ │ ├── Dtos/
│ │ │ │ │ ├── Interfaces/
│ │ │ │ │ └── Services/
│ │ │ │ ├── Infrastructure/
│ │ │ │ │ ├── Interfaces/
│ │ │ │ │ ├── Models/
│ │ │ │ │ └── Repositories/
│ │ │ │ └── Presentation/
│ │ │ └── ConfigurationFeature/
│ │ └── Shared/ ← archivos compartidos solo dentro del módulo
│ │ └── Models/
│ ├── ContractorsModule/
│ └── MicrosoftAuth/
├── Shared/ ← servicios reutilizables globales
│ ├── Models/
│ └── Services/
│ ├── Email/
│ ├── SharePoint/
│ └── Sunat/
├── Infrastructure/ ← DEPRECADO, no usar
├── Controllers/ ← DEPRECADO, no usar
└── Application/ ← DEPRECADO, no usar

## ¿Qué es una feature?
Una feature corresponde a una funcionalidad específica que aparece como ítem
en el sidebar de la aplicación frontend. Cada entrada del menú lateral
corresponde exactamente a una feature y tiene su propia carpeta dentro del
módulo al que pertenece.
Ejemplos:
- "Adjudicaciones" → `AdjudicacionesFeature/`
- "Homologación de Contratistas" → `ContractorRegistrationFeature/`
- "Gestión de Contratistas" → `ContractorManagementFeature/`
No se considera feature a lógica interna de una funcionalidad (como validaciones,
helpers o modelos secundarios): esos viven dentro de la carpeta de su feature.
## Reglas
### ✅ Código nuevo
- Cada feature va dentro de `Features/{Modulo}/Features/{NombreFeature}/`
- Si el módulo es simple y no agrupa sub-features, puede vivir directamente
  en `Features/{NombreFeature}/`
- El namespace debe coincidir exactamente con la ruta de la carpeta:
  - Ruta: `Features/CostsModule/Features/AdjudicacionesFeature/Application/Services/`
  - Namespace: `Abril_Backend.Features.CostsModule.Features.AdjudicacionesFeature.Application.Services`
- Cada módulo se registra en su propio archivo `{Modulo}Module.cs`
- Dentro de cada feature hay **estructura libre**, aunque se recomienda
  mantener la separación `Application / Infrastructure / Presentation`
### ❌ No hacer
- No agregar controllers, servicios ni repositorios en las carpetas raíz
  `Controllers/`, `Application/` o `Infrastructure/` — están deprecadas
- No poner lógica de negocio en los controllers
- No compartir archivos entre features o módulos salvo que 2 o más los usen.
  En ese caso: si los usan features del mismo módulo → mover a
  `Features/{Modulo}/Shared/`. Si los usan módulos distintos → mover a `Shared/`
## `Shared/` global
Contiene servicios reutilizables por cualquier módulo:
| Carpeta | Descripción |
|---|---|
| `Shared/Services/Email/` | Envío de correos delegados vía Graph API |
| `Shared/Services/SharePoint/` | Subida de archivos a SharePoint/OneDrive |
| `Shared/Services/Sunat/` | Consulta de RUC a Sunat |
| `Shared/Models/` | Modelos de BD compartidos entre módulos |
Para agregar un nuevo servicio compartido, crear la carpeta en
`Shared/Services/{NombreServicio}/` con subcarpetas `Interfaces/` y `Services/`.
## `Shared/` por módulo
Para archivos compartidos **solo dentro de un módulo**, usar
`Features/{Modulo}/Shared/` en vez del Shared global.
## Registro de dependencias
Cada módulo expone un método de extensión y se registra en `Program.cs`:
```csharp
// Features/CostsModule/CostsModule.cs
public static IServiceCollection AddCostsModule(this IServiceCollection services)
{
services.AddScoped<IAlgunaInterface, AlgunaImplementacion>();
return services;
}

// Program.cs
builder.Services.AddCostsModule();
builder.Services.AddContractorsModule();