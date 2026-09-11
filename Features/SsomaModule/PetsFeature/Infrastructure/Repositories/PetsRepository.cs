using System.Text.Json;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Infrastructure.Models;
using Abril_Backend.Features.SsomaModule.OptFeature.Infrastructure.Models;
using Abril_Backend.Features.SsomaModule.PetsFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PetsFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.PetsFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Models;
using Abril_Backend.Features.CostsModule.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.SsomaModule.PetsFeature.Infrastructure.Repositories;

public class PetsRepository : IPetsRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public PetsRepository(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<PetListItemDto>> GetListAsync()
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsomaPet
            .OrderBy(p => p.Nombre)
            .Select(p => new PetListItemDto
            {
                Id = p.Id,
                Nombre = p.Nombre,
                Codigo = p.Codigo,
                Activo = p.Activo,
                TotalPasos = p.Pasos.Count(x => x.Activo),
                CreatedAt = p.CreatedAt,
                EstadoRevision = p.EstadoRevision,
                VersionVigente = p.VersionVigente,
                RevisionPendiente = p.RevisionPendiente,
                RevisionPendienteMotivo = p.RevisionPendienteMotivo,
                Origen = p.Origen,
                ContributorId = p.ContributorId,
                ContributorNombre = ctx.Set<Contributor>()
                    .Where(c => c.ContributorId == p.ContributorId)
                    .Select(c => c.ContributorNombreComercial ?? c.ContributorName)
                    .FirstOrDefault(),
                ProyectoId = p.ProyectoId,
                ProyectoNombre = ctx.Set<Project>()
                    .Where(pr => pr.ProjectId == p.ProyectoId)
                    .Select(pr => pr.ProjectDescription)
                    .FirstOrDefault()
            })
            .ToListAsync();
    }


    // Cualquier edición de contenido (paso, imagen, catálogo, texto, firma, anexo,
    // datos generales) vuelve a poner el PETS en "borrador", aunque ya hubiera una
    // versión aprobada — así el estado nunca miente sobre si lo que se ve/edita
    // coincide con la última versión oficial. No hace nada si ya estaba en borrador
    // (evita un UPDATE de más en el caso común). Se llama siempre ANTES de cualquier
    // validación que pueda tirar excepción — si la operación falla, el ctx se
    // descarta sin SaveChanges y este cambio nunca se persiste, que es lo correcto.
    private static async Task MarcarBorradorAsync(AppDbContext ctx, int petId)
    {
        var pet = await ctx.SsomaPet.FindAsync(petId);
        if (pet != null && pet.EstadoRevision == "aprobado")
            pet.EstadoRevision = "borrador";
    }

    // Secciones narrativas: un solo bloque de texto cada una (no árbol).
    private static readonly string[] SeccionesTextoKeys =
        ["introduccion", "alcance", "objetivo", "definiciones", "restricciones"];

    private static readonly string[] RolesFirma = ["elaborado", "revisado", "aprobado"];

    private static PetFirmaDto MapFirma(string rol, SsomaPetFirma? f) => new()
    {
        Rol = rol,
        Nombre = f?.Nombre,
        Cargo = f?.Cargo,
        Fecha = f?.Fecha,
        FirmaUrl = f?.FirmaUrl
    };

    private static PetPasoDto MapPaso(SsomaPetPaso x, Dictionary<int, List<PetImagenDto>> imagenesPorPaso)
    {
        var imagenes = imagenesPorPaso.GetValueOrDefault(x.Id) ?? [];
        return new PetPasoDto
        {
            Id = x.Id,
            ParentId = x.ParentId,
            Tipo = x.Tipo,
            Descripcion = x.Descripcion,
            // Compatibilidad: si todavía no tiene fila en la tabla nueva pero sí en la
            // columna vieja (pasos importados antes de este cambio), se sigue mostrando.
            ImagenUrl = imagenes.Count > 0 ? imagenes[0].Url : x.ImagenUrl,
            Imagenes = imagenes,
            Categoria = x.Categoria,
            Orden = x.Orden
        };
    }

    private async Task<Dictionary<int, List<PetImagenDto>>> CargarImagenesPorPasoAsync(AppDbContext ctx, IEnumerable<int> pasoIds)
    {
        var ids = pasoIds.ToList();
        if (ids.Count == 0) return [];

        var imagenes = await ctx.SsomaPetPasoImagen
            .Where(x => ids.Contains(x.PasoId) && x.Activo)
            .OrderBy(x => x.Orden)
            .ToListAsync();

        return imagenes.GroupBy(x => x.PasoId)
            .ToDictionary(g => g.Key, g => g.Select(i => new PetImagenDto { Id = i.Id, Url = i.Url }).ToList());
    }

    private static PetItemSeleccionadoDto MapSeleccion(SsomaPetItemSeleccionado x) => new()
    {
        Id = x.Id,
        Grupo = x.Grupo,
        Tipo = x.Tipo,
        CatalogoItemId = x.CatalogoItemId,
        Descripcion = x.CatalogoItem?.Descripcion ?? x.DescripcionPersonalizada ?? string.Empty,
        EsPersonalizado = x.CatalogoItemId == null,
        Orden = x.Orden
    };

    public async Task<PetDetalleDto?> GetDetalleAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var pet = await ctx.SsomaPet.FirstOrDefaultAsync(p => p.Id == id);
        if (pet == null) return null;

        var todosPasos = await ctx.SsomaPetPaso
            .Where(x => x.PetId == id && x.Activo)
            .OrderBy(x => x.Orden)
            .ToListAsync();

        var imagenesPorPaso = await CargarImagenesPorPasoAsync(ctx, todosPasos.Select(x => x.Id));
        var pasosPorSeccion = todosPasos.GroupBy(x => x.Seccion)
            .ToDictionary(g => g.Key, g => g.Select(x => MapPaso(x, imagenesPorPaso)).ToList());

        var textosPorSeccion = await ctx.SsomaPetSeccionTexto
            .Where(x => x.PetId == id)
            .ToDictionaryAsync(x => x.Seccion, x => x.Contenido);

        var seleccionadosEntidades = await ctx.SsomaPetItemSeleccionado
            .Include(x => x.CatalogoItem)
            .Where(x => x.PetId == id && x.Activo)
            .OrderBy(x => x.Orden)
            .ToListAsync();
        var seleccionados = seleccionadosEntidades.Select(MapSeleccion).ToList();

        var anexos = await ctx.SsomaPetAnexo
            .Where(x => x.PetId == id && x.Activo)
            .OrderBy(x => x.Orden)
            .Select(x => new PetAnexoDto { Id = x.Id, Nombre = x.Nombre, ArchivoUrl = x.ArchivoUrl, Orden = x.Orden })
            .ToListAsync();

        var firmasPorRol = await ctx.SsomaPetFirma
            .Where(x => x.PetId == id)
            .ToDictionaryAsync(x => x.Rol);

        var contributorNombre = pet.ContributorId.HasValue
            ? await ctx.Set<Contributor>()
                .Where(c => c.ContributorId == pet.ContributorId.Value)
                .Select(c => c.ContributorNombreComercial ?? c.ContributorName)
                .FirstOrDefaultAsync()
            : null;
        var proyectoNombre = pet.ProyectoId.HasValue
            ? await ctx.Set<Project>()
                .Where(pr => pr.ProjectId == pet.ProyectoId.Value)
                .Select(pr => pr.ProjectDescription)
                .FirstOrDefaultAsync()
            : null;

        return new PetDetalleDto
        {
            Id = pet.Id,
            Nombre = pet.Nombre,
            Codigo = pet.Codigo,
            SharepointUrl = pet.SharepointUrl,
            Activo = pet.Activo,
            EstadoRevision = pet.EstadoRevision,
            VersionVigente = pet.VersionVigente,
            RevisionPendiente = pet.RevisionPendiente,
            RevisionPendienteMotivo = pet.RevisionPendienteMotivo,
            Origen = pet.Origen,
            ContributorId = pet.ContributorId,
            ContributorNombre = contributorNombre,
            ProyectoId = pet.ProyectoId,
            ProyectoNombre = proyectoNombre,
            Pasos = pasosPorSeccion.GetValueOrDefault("procedimiento") ?? [],
            Responsabilidades = pasosPorSeccion.GetValueOrDefault("responsabilidades") ?? [],
            SeccionesTexto = SeccionesTextoKeys.ToDictionary(s => s, s => textosPorSeccion.GetValueOrDefault(s) ?? ""),
            MarcoLegal = seleccionados.Where(x => x.Grupo == "marco_legal").ToList(),
            Epp = seleccionados.Where(x => x.Grupo == "epp").ToList(),
            Recursos = seleccionados.Where(x => x.Grupo == "recurso").ToList(),
            Anexos = anexos,
            Firmas = RolesFirma.ToDictionary(r => r, r => MapFirma(r, firmasPorRol.GetValueOrDefault(r)))
        };
    }

    public async Task<List<PetPasoDto>> GetPasosAsync(int petId)
    {
        using var ctx = _factory.CreateDbContext();
        var pasos = await ctx.SsomaPetPaso
            .Where(x => x.PetId == petId && x.Activo && x.Seccion == "procedimiento")
            .OrderBy(x => x.Orden)
            .ToListAsync();

        var imagenesPorPaso = await CargarImagenesPorPasoAsync(ctx, pasos.Select(x => x.Id));
        return pasos.Select(x => MapPaso(x, imagenesPorPaso)).ToList();
    }

    // Un PETS de contratista siempre tiene empresa dueña (obligatoria); el proyecto
    // en cambio puede quedar sin asignar TEMPORALMENTE (ej. justo después de
    // "Duplicar", que crea la copia sin proyecto a propósito para forzar que se
    // reasigne a la obra nueva) — mientras no tenga proyecto, simplemente no
    // aparece en ningún selector de OPT/Accidentes (esos filtran por proyecto
    // exacto), así que no hay riesgo de que se use sin estar bien ubicado.
    // El de Abril, en cambio, se fuerza a quedar sin proyecto/empresa (es el
    // catálogo global de siempre), aunque el request traiga algo por error.
    private static (string Origen, int? ContributorId, int? ProyectoId) NormalizarOrigen(
        string? origen, int? contributorId, int? proyectoId)
    {
        var esContratista = string.Equals(origen, "Contratista", StringComparison.OrdinalIgnoreCase);
        if (!esContratista) return ("Abril", null, null);

        if (!contributorId.HasValue)
            throw new AbrilException("Selecciona la empresa contratista dueña del PETS.", 400);

        return ("Contratista", contributorId, proyectoId);
    }

    public async Task<int> CrearAsync(CrearPetRequest request)
    {
        var (origen, contributorId, proyectoId) = NormalizarOrigen(request.Origen, request.ContributorId, request.ProyectoId);

        using var ctx = _factory.CreateDbContext();
        var pet = new SsomaPet
        {
            Nombre = request.Nombre,
            Codigo = request.Codigo,
            SharepointUrl = request.SharepointUrl,
            Origen = origen,
            ContributorId = contributorId,
            ProyectoId = proyectoId,
            Activo = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.SsomaPet.Add(pet);
        await ctx.SaveChangesAsync();
        return pet.Id;
    }

    public async Task ActualizarAsync(int id, ActualizarPetRequest request)
    {
        var (origen, contributorId, proyectoId) = NormalizarOrigen(request.Origen, request.ContributorId, request.ProyectoId);

        using var ctx = _factory.CreateDbContext();
        var pet = await ctx.SsomaPet.FindAsync(id)
            ?? throw new AbrilException("PETS no encontrado.", 404);

        if (pet.EstadoRevision == "aprobado") pet.EstadoRevision = "borrador";
        pet.Nombre = request.Nombre;
        pet.Codigo = request.Codigo;
        pet.SharepointUrl = request.SharepointUrl;
        pet.Activo = request.Activo;
        pet.Origen = origen;
        pet.ContributorId = contributorId;
        pet.ProyectoId = proyectoId;
        pet.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    private static readonly HashSet<string> TiposValidos = ["subtitulo", "paso", "letra", "guion"];

    // Solo estas dos secciones usan el árbol de pasos — el resto (Introducción,
    // Alcance, Objetivo, Definiciones, Restricciones) son bloques de texto único.
    private static readonly HashSet<string> SeccionesValidas = ["procedimiento", "responsabilidades"];

    private static string ValidarTipo(string? tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo)) return "paso";
        if (!TiposValidos.Contains(tipo))
            throw new AbrilException($"Tipo de paso inválido: '{tipo}'.", 400);
        return tipo;
    }

    private static string ValidarSeccion(string? seccion)
    {
        if (string.IsNullOrWhiteSpace(seccion)) return "procedimiento";
        if (!SeccionesValidas.Contains(seccion))
            throw new AbrilException($"Sección inválida: '{seccion}'.", 400);
        return seccion;
    }

    public async Task<int> AgregarPasoAsync(int petId, CrearPetPasoRequest request)
    {
        using var ctx = _factory.CreateDbContext();
        var pet = await ctx.SsomaPet.FindAsync(petId)
            ?? throw new AbrilException("PETS no encontrado.", 404);
        if (pet.EstadoRevision == "aprobado") pet.EstadoRevision = "borrador";

        var seccion = ValidarSeccion(request.Seccion);

        if (request.ParentId.HasValue)
        {
            var padreExiste = await ctx.SsomaPetPaso.AnyAsync(p => p.Id == request.ParentId.Value && p.PetId == petId && p.Seccion == seccion && p.Activo);
            if (!padreExiste) throw new AbrilException("El subtítulo padre no existe.", 404);
        }

        // "Hermanos": solo los pasos con el MISMO ParentId dentro de la MISMA sección —
        // cada grupo se ordena independiente, insertar/reordenar dentro de un subtítulo
        // no toca a otro, ni a otra sección.
        var hermanos = await ctx.SsomaPetPaso
            .Where(p => p.PetId == petId && p.Activo && p.Seccion == seccion && p.ParentId == request.ParentId)
            .OrderBy(p => p.Orden)
            .ToListAsync();

        var maxPosicion = hermanos.Count + 1;
        var posicion = request.Posicion.HasValue && request.Posicion.Value >= 1 && request.Posicion.Value <= maxPosicion
            ? request.Posicion.Value
            : maxPosicion;

        foreach (var p in hermanos.Where(p => p.Orden >= posicion))
        {
            p.Orden += 1;
            p.UpdatedAt = DateTime.UtcNow;
        }

        var nuevo = new SsomaPetPaso
        {
            PetId = petId,
            Seccion = seccion,
            ParentId = request.ParentId,
            Tipo = ValidarTipo(request.Tipo),
            Descripcion = request.Descripcion,
            Orden = posicion,
            Activo = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.SsomaPetPaso.Add(nuevo);
        await ctx.SaveChangesAsync();
        return nuevo.Id;
    }

    public async Task<Dictionary<int, int>> AgregarPasosBulkAsync(int petId, string seccionRaw, List<ImportPasoConfirmDto> pasos)
    {
        using var ctx = _factory.CreateDbContext();
        var pet = await ctx.SsomaPet.FindAsync(petId) ?? throw new AbrilException("PETS no encontrado.", 404);
        if (pet.EstadoRevision == "aprobado") pet.EstadoRevision = "borrador";
        var seccion = ValidarSeccion(seccionRaw);

        var idPorIndice = new Dictionary<int, int>();
        // Próximo "Orden" disponible por padre real (0 = raíz), cacheado en memoria durante
        // todo el batch: solo se consulta la base la PRIMERA vez que aparece cada padre: el
        // resto de hermanos del mismo padre solo incrementan el contador en memoria. Antes
        // cada paso releía la lista completa de hermanos existentes, así que insertar 200
        // pasos hermanos hacía ~200 consultas cada vez más grandes (cuadrático).
        var ordenSiguientePorPadre = new Dictionary<int, int>();

        async Task<int> SiguienteOrden(int? parentIdReal)
        {
            var key = parentIdReal ?? 0;
            if (!ordenSiguientePorPadre.TryGetValue(key, out var siguiente))
            {
                var maxActual = await ctx.SsomaPetPaso
                    .Where(p => p.PetId == petId && p.Activo && p.Seccion == seccion && p.ParentId == parentIdReal)
                    .MaxAsync(p => (int?)p.Orden) ?? 0;
                siguiente = maxActual + 1;
            }
            ordenSiguientePorPadre[key] = siguiente + 1;
            return siguiente;
        }

        // Se procesa nivel por nivel (BFS del árbol reconstruido en el preview): un paso
        // solo puede insertarse una vez que su padre (si tiene) ya tiene un id real de base
        // de datos, así que cada vuelta procesa todos los que YA están listos y hace un solo
        // SaveChanges para ese nivel completo.
        var pendientes = new List<ImportPasoConfirmDto>(pasos);
        var vueltasRestantes = pendientes.Count + 5; // corte de seguridad — no debería hacer falta.
        while (pendientes.Count > 0 && vueltasRestantes-- > 0)
        {
            var listos = pendientes.Where(p => p.ParentIndice is null || idPorIndice.ContainsKey(p.ParentIndice.Value)).ToList();
            if (listos.Count == 0) break; // padre nunca resuelto (dato inconsistente del preview) — se descarta el resto, igual que antes fallaba solo esa fila.

            var nuevos = new List<(int Indice, SsomaPetPaso Entidad)>();
            foreach (var item in listos)
            {
                int? parentIdReal = item.ParentIndice.HasValue ? idPorIndice[item.ParentIndice.Value] : null;
                var orden = await SiguienteOrden(parentIdReal);
                var entidad = new SsomaPetPaso
                {
                    PetId = petId,
                    Seccion = seccion,
                    ParentId = parentIdReal,
                    Tipo = ValidarTipo(item.Tipo),
                    Descripcion = item.Texto,
                    Categoria = item.Categoria != null && CategoriasValidas.Contains(item.Categoria) ? item.Categoria : null,
                    Orden = orden,
                    Activo = true,
                    CreatedAt = DateTime.UtcNow
                };
                ctx.SsomaPetPaso.Add(entidad);
                nuevos.Add((item.Indice, entidad));
            }

            await ctx.SaveChangesAsync();
            foreach (var (indice, entidad) in nuevos)
                idPorIndice[indice] = entidad.Id;

            pendientes = pendientes.Where(p => !idPorIndice.ContainsKey(p.Indice)).ToList();
        }

        return idPorIndice;
    }

    public async Task ActualizarPasoAsync(int petId, int pasoId, ActualizarPetPasoRequest request)
    {
        using var ctx = _factory.CreateDbContext();
        await MarcarBorradorAsync(ctx, petId);
        var paso = await ctx.SsomaPetPaso.FirstOrDefaultAsync(p => p.Id == pasoId && p.PetId == petId)
            ?? throw new AbrilException("Paso no encontrado.", 404);

        paso.Descripcion = request.Descripcion;
        paso.Tipo = ValidarTipo(request.Tipo);
        paso.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    public async Task EliminarPasoAsync(int petId, int pasoId)
    {
        using var ctx = _factory.CreateDbContext();
        await MarcarBorradorAsync(ctx, petId);
        var paso = await ctx.SsomaPetPaso.FirstOrDefaultAsync(p => p.Id == pasoId && p.PetId == petId)
            ?? throw new AbrilException("Paso no encontrado.", 404);

        var tieneHijos = await ctx.SsomaPetPaso.AnyAsync(p => p.ParentId == pasoId && p.Activo);
        if (tieneHijos)
            throw new AbrilException("Este subtítulo tiene pasos dentro — elimínalos primero.", 400);

        paso.Activo = false;
        paso.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    public async Task ReordenarPasosAsync(int petId, ReordenarPasosRequest request)
    {
        using var ctx = _factory.CreateDbContext();
        await MarcarBorradorAsync(ctx, petId);
        var seccion = ValidarSeccion(request.Seccion);
        var pasos = await ctx.SsomaPetPaso
            .Where(p => p.PetId == petId && p.Activo && p.Seccion == seccion && p.ParentId == request.ParentId)
            .ToListAsync();

        var porId = pasos.ToDictionary(p => p.Id);
        for (int i = 0; i < request.PasoIds.Count; i++)
        {
            if (porId.TryGetValue(request.PasoIds[i], out var paso))
            {
                paso.Orden = i + 1;
                paso.UpdatedAt = DateTime.UtcNow;
            }
        }
        await ctx.SaveChangesAsync();
    }

    public async Task SetImagenPasoAsync(int petId, int pasoId, string? imagenUrl)
    {
        using var ctx = _factory.CreateDbContext();
        await MarcarBorradorAsync(ctx, petId);
        var paso = await ctx.SsomaPetPaso.FirstOrDefaultAsync(p => p.Id == pasoId && p.PetId == petId)
            ?? throw new AbrilException("Paso no encontrado.", 404);

        paso.ImagenUrl = imagenUrl;
        paso.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    public async Task<int> AgregarImagenPasoAsync(int petId, int pasoId, string url)
    {
        using var ctx = _factory.CreateDbContext();
        await MarcarBorradorAsync(ctx, petId);
        var existe = await ctx.SsomaPetPaso.AnyAsync(p => p.Id == pasoId && p.PetId == petId);
        if (!existe) throw new AbrilException("Paso no encontrado.", 404);

        var maxOrden = await ctx.SsomaPetPasoImagen
            .Where(x => x.PasoId == pasoId && x.Activo)
            .Select(x => (int?)x.Orden).MaxAsync() ?? 0;

        var nueva = new SsomaPetPasoImagen
        {
            PasoId = pasoId,
            Url = url,
            Orden = maxOrden + 1,
            Activo = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.SsomaPetPasoImagen.Add(nueva);
        await ctx.SaveChangesAsync();
        return nueva.Id;
    }

    public async Task EliminarImagenPasoAsync(int petId, int pasoId, int imagenId)
    {
        using var ctx = _factory.CreateDbContext();
        await MarcarBorradorAsync(ctx, petId);
        var existe = await ctx.SsomaPetPaso.AnyAsync(p => p.Id == pasoId && p.PetId == petId);
        if (!existe) throw new AbrilException("Paso no encontrado.", 404);

        var imagen = await ctx.SsomaPetPasoImagen.FirstOrDefaultAsync(x => x.Id == imagenId && x.PasoId == pasoId)
            ?? throw new AbrilException("Imagen no encontrada.", 404);

        imagen.Activo = false;
        await ctx.SaveChangesAsync();
    }

    // Catálogo fijo de categorías válidas — texto libre no, porque el color/ícono en
    // pantalla y PDF dependen de reconocer el valor exacto.
    private static readonly HashSet<string> CategoriasValidas = ["medio_ambiente"];

    public async Task ActualizarCategoriaPasoAsync(int petId, int pasoId, string? categoria)
    {
        if (categoria != null && !CategoriasValidas.Contains(categoria))
            throw new AbrilException($"Categoría inválida: '{categoria}'.", 400);

        using var ctx = _factory.CreateDbContext();
        await MarcarBorradorAsync(ctx, petId);
        var paso = await ctx.SsomaPetPaso.FirstOrDefaultAsync(p => p.Id == pasoId && p.PetId == petId)
            ?? throw new AbrilException("Paso no encontrado.", 404);

        paso.Categoria = categoria;
        paso.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    // Usado al reimportar un Word corregido: borra (desactiva) todo lo que había
    // en esa sección antes de insertar los pasos nuevos, en vez de acumularlos.
    public async Task DesactivarSeccionAsync(int petId, string seccion)
    {
        seccion = ValidarSeccion(seccion);
        using var ctx = _factory.CreateDbContext();
        await MarcarBorradorAsync(ctx, petId);
        var pasos = await ctx.SsomaPetPaso
            .Where(p => p.PetId == petId && p.Seccion == seccion && p.Activo)
            .ToListAsync();

        foreach (var p in pasos)
        {
            p.Activo = false;
            p.UpdatedAt = DateTime.UtcNow;
        }
        await ctx.SaveChangesAsync();
    }

    // ── Secciones de texto único (Introducción / Alcance / Objetivo / Definiciones / Restricciones) ──

    private static string ValidarSeccionTexto(string seccion)
    {
        if (!SeccionesTextoKeys.Contains(seccion))
            throw new AbrilException($"Sección de texto inválida: '{seccion}'.", 400);
        return seccion;
    }

    public async Task UpsertSeccionTextoAsync(int petId, string seccion, string contenido)
    {
        seccion = ValidarSeccionTexto(seccion);
        using var ctx = _factory.CreateDbContext();
        var pet = await ctx.SsomaPet.FindAsync(petId)
            ?? throw new AbrilException("PETS no encontrado.", 404);
        if (pet.EstadoRevision == "aprobado") pet.EstadoRevision = "borrador";

        var fila = await ctx.SsomaPetSeccionTexto.FirstOrDefaultAsync(x => x.PetId == petId && x.Seccion == seccion);
        if (fila == null)
        {
            ctx.SsomaPetSeccionTexto.Add(new SsomaPetSeccionTexto
            {
                PetId = petId,
                Seccion = seccion,
                Contenido = contenido,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            fila.Contenido = contenido;
            fila.UpdatedAt = DateTime.UtcNow;
        }
        await ctx.SaveChangesAsync();
    }

    // ── Catálogo (Marco Legal / EPP / Recursos) ──────────────────────────────────

    private static readonly HashSet<string> GruposValidos = ["marco_legal", "epp", "recurso"];
    private static readonly Dictionary<string, HashSet<string>> TiposPorGrupo = new()
    {
        ["epp"] = ["basico", "especifico", "emergencia"],
        ["recurso"] = ["equipo", "herramienta", "material"]
    };

    private static void ValidarGrupoTipo(string grupo, string? tipo)
    {
        if (!GruposValidos.Contains(grupo))
            throw new AbrilException($"Grupo de catálogo inválido: '{grupo}'.", 400);

        if (TiposPorGrupo.TryGetValue(grupo, out var tiposValidos))
        {
            if (string.IsNullOrWhiteSpace(tipo) || !tiposValidos.Contains(tipo))
                throw new AbrilException($"Tipo inválido para el grupo '{grupo}'.", 400);
        }
        else if (!string.IsNullOrWhiteSpace(tipo))
        {
            throw new AbrilException($"El grupo '{grupo}' no admite tipo.", 400);
        }
    }

    public async Task<List<CatalogoItemDto>> GetCatalogoAsync(string grupo, string? tipo)
    {
        ValidarGrupoTipo(grupo, tipo);
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsomaCatalogoItem
            .Where(x => x.Grupo == grupo && x.Tipo == tipo && x.Activo)
            .OrderBy(x => x.Orden).ThenBy(x => x.Descripcion)
            .Select(x => new CatalogoItemDto { Id = x.Id, Grupo = x.Grupo, Tipo = x.Tipo, Descripcion = x.Descripcion, Activo = x.Activo, Orden = x.Orden })
            .ToListAsync();
    }

    public async Task<int> CrearCatalogoItemAsync(CrearCatalogoItemRequest request)
    {
        ValidarGrupoTipo(request.Grupo, request.Tipo);
        using var ctx = _factory.CreateDbContext();
        var item = new SsomaCatalogoItem
        {
            Grupo = request.Grupo,
            Tipo = request.Tipo,
            Descripcion = request.Descripcion,
            Activo = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.SsomaCatalogoItem.Add(item);
        await ctx.SaveChangesAsync();
        return item.Id;
    }

    public async Task DesactivarCatalogoItemAsync(int catalogoItemId)
    {
        using var ctx = _factory.CreateDbContext();
        var item = await ctx.SsomaCatalogoItem.FindAsync(catalogoItemId)
            ?? throw new AbrilException("Ítem de catálogo no encontrado.", 404);

        item.Activo = false;
        item.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    public async Task<int> SeleccionarCatalogoItemAsync(int petId, SeleccionarItemCatalogoRequest request)
    {
        ValidarGrupoTipo(request.Grupo, request.Tipo);
        using var ctx = _factory.CreateDbContext();
        var pet = await ctx.SsomaPet.FindAsync(petId)
            ?? throw new AbrilException("PETS no encontrado.", 404);
        if (pet.EstadoRevision == "aprobado") pet.EstadoRevision = "borrador";

        var catalogoItem = await ctx.SsomaCatalogoItem.FirstOrDefaultAsync(x => x.Id == request.CatalogoItemId && x.Activo)
            ?? throw new AbrilException("Ítem de catálogo no encontrado.", 404);

        var yaSeleccionado = await ctx.SsomaPetItemSeleccionado
            .AnyAsync(x => x.PetId == petId && x.CatalogoItemId == request.CatalogoItemId && x.Activo);
        if (yaSeleccionado) throw new AbrilException("Este ítem ya está seleccionado.", 400);

        var maxOrden = await ctx.SsomaPetItemSeleccionado
            .Where(x => x.PetId == petId && x.Grupo == request.Grupo && x.Activo)
            .Select(x => (int?)x.Orden).MaxAsync() ?? 0;

        var nuevo = new SsomaPetItemSeleccionado
        {
            PetId = petId,
            Grupo = request.Grupo,
            Tipo = catalogoItem.Tipo,
            CatalogoItemId = catalogoItem.Id,
            Orden = maxOrden + 1,
            Activo = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.SsomaPetItemSeleccionado.Add(nuevo);
        await ctx.SaveChangesAsync();
        return nuevo.Id;
    }

    public async Task<int> AgregarItemPersonalizadoAsync(int petId, AgregarItemPersonalizadoRequest request)
    {
        ValidarGrupoTipo(request.Grupo, request.Tipo);
        using var ctx = _factory.CreateDbContext();
        var pet = await ctx.SsomaPet.FindAsync(petId)
            ?? throw new AbrilException("PETS no encontrado.", 404);
        if (pet.EstadoRevision == "aprobado") pet.EstadoRevision = "borrador";

        // Import de Word reimportado varias veces sin este chequeo triplicaba Marco
        // Legal/EPP/Recursos — a diferencia de Procedimiento/Responsabilidades, estos
        // ítems personalizados no tienen un "Reemplazar" explícito, así que si ya existe
        // uno activo con la misma descripción en este PETS (mismo grupo/tipo), se
        // reutiliza en vez de duplicar.
        var yaExiste = await ctx.SsomaPetItemSeleccionado
            .Where(x => x.PetId == petId && x.Grupo == request.Grupo && x.Tipo == request.Tipo && x.Activo
                && x.CatalogoItemId == null && x.DescripcionPersonalizada != null)
            .Select(x => new { x.Id, x.DescripcionPersonalizada })
            .ToListAsync();
        var existente = yaExiste.FirstOrDefault(x => string.Equals(x.DescripcionPersonalizada, request.Descripcion, StringComparison.OrdinalIgnoreCase));
        if (existente != null) return existente.Id;

        int? catalogoItemId = null;
        if (request.AgregarAlCatalogoGlobal)
        {
            var catalogoItem = new SsomaCatalogoItem
            {
                Grupo = request.Grupo,
                Tipo = request.Tipo,
                Descripcion = request.Descripcion,
                Activo = true,
                CreatedAt = DateTime.UtcNow
            };
            ctx.SsomaCatalogoItem.Add(catalogoItem);
            await ctx.SaveChangesAsync();
            catalogoItemId = catalogoItem.Id;
        }

        var maxOrden = await ctx.SsomaPetItemSeleccionado
            .Where(x => x.PetId == petId && x.Grupo == request.Grupo && x.Activo)
            .Select(x => (int?)x.Orden).MaxAsync() ?? 0;

        var nuevo = new SsomaPetItemSeleccionado
        {
            PetId = petId,
            Grupo = request.Grupo,
            Tipo = request.Tipo,
            CatalogoItemId = catalogoItemId,
            DescripcionPersonalizada = catalogoItemId == null ? request.Descripcion : null,
            Orden = maxOrden + 1,
            Activo = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.SsomaPetItemSeleccionado.Add(nuevo);
        await ctx.SaveChangesAsync();
        return nuevo.Id;
    }

    public async Task EliminarSeleccionAsync(int petId, int seleccionId)
    {
        using var ctx = _factory.CreateDbContext();
        await MarcarBorradorAsync(ctx, petId);
        var seleccion = await ctx.SsomaPetItemSeleccionado.FirstOrDefaultAsync(x => x.Id == seleccionId && x.PetId == petId)
            ?? throw new AbrilException("Selección no encontrada.", 404);

        seleccion.Activo = false;
        await ctx.SaveChangesAsync();
    }

    public async Task DesactivarSeleccionesGrupoAsync(int petId, string grupo)
    {
        using var ctx = _factory.CreateDbContext();
        await MarcarBorradorAsync(ctx, petId);
        var selecciones = await ctx.SsomaPetItemSeleccionado
            .Where(x => x.PetId == petId && x.Grupo == grupo && x.Activo)
            .ToListAsync();

        foreach (var s in selecciones)
            s.Activo = false;
        await ctx.SaveChangesAsync();
    }

    // ── Anexos ────────────────────────────────────────────────────────────────

    public async Task<int> AgregarAnexoAsync(int petId, string nombre, string archivoUrl)
    {
        using var ctx = _factory.CreateDbContext();
        var pet = await ctx.SsomaPet.FindAsync(petId)
            ?? throw new AbrilException("PETS no encontrado.", 404);
        if (pet.EstadoRevision == "aprobado") pet.EstadoRevision = "borrador";

        var maxOrden = await ctx.SsomaPetAnexo
            .Where(x => x.PetId == petId && x.Activo)
            .Select(x => (int?)x.Orden).MaxAsync() ?? 0;

        var nuevo = new SsomaPetAnexo
        {
            PetId = petId,
            Nombre = nombre,
            ArchivoUrl = archivoUrl,
            Orden = maxOrden + 1,
            Activo = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.SsomaPetAnexo.Add(nuevo);
        await ctx.SaveChangesAsync();
        return nuevo.Id;
    }

    public async Task EliminarAnexoAsync(int petId, int anexoId)
    {
        using var ctx = _factory.CreateDbContext();
        await MarcarBorradorAsync(ctx, petId);
        var anexo = await ctx.SsomaPetAnexo.FirstOrDefaultAsync(x => x.Id == anexoId && x.PetId == petId)
            ?? throw new AbrilException("Anexo no encontrado.", 404);

        anexo.Activo = false;
        await ctx.SaveChangesAsync();
    }

    // ── Firmas (Elaborado por / Revisado por / Aprobado por) ────────────────────

    private static string ValidarRolFirma(string rol)
    {
        if (!RolesFirma.Contains(rol))
            throw new AbrilException($"Rol de firma inválido: '{rol}'.", 400);
        return rol;
    }

    public async Task UpsertFirmaAsync(int petId, string rol, string? nombre, string? cargo, DateOnly? fecha)
    {
        rol = ValidarRolFirma(rol);
        using var ctx = _factory.CreateDbContext();
        var pet = await ctx.SsomaPet.FindAsync(petId)
            ?? throw new AbrilException("PETS no encontrado.", 404);
        if (pet.EstadoRevision == "aprobado") pet.EstadoRevision = "borrador";

        var fila = await ctx.SsomaPetFirma.FirstOrDefaultAsync(x => x.PetId == petId && x.Rol == rol);
        if (fila == null)
        {
            ctx.SsomaPetFirma.Add(new SsomaPetFirma
            {
                PetId = petId,
                Rol = rol,
                Nombre = nombre,
                Cargo = cargo,
                Fecha = fecha,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            fila.Nombre = nombre;
            fila.Cargo = cargo;
            fila.Fecha = fecha;
            fila.UpdatedAt = DateTime.UtcNow;
        }
        await ctx.SaveChangesAsync();
    }

    // ── Versionado y aprobación ──────────────────────────────────────────────────

    // El snapshot se toma leyendo GetDetalleAsync (mismo shape que ve la pantalla en
    // vivo) y se serializa entero como JSON — una versión aprobada nunca se edita,
    // así que no hace falta duplicar cada tabla de pasos/catálogo con un VersionId,
    // alcanza con guardar la "foto" completa tal cual quedó en ese momento.
    public async Task<PetVersionDto> AprobarVersionAsync(int petId, string motivo, int? aprobadoPorId, string aprobadoPorNombre)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new AbrilException("El motivo es obligatorio para aprobar una versión.", 400);

        var detalle = await GetDetalleAsync(petId) ?? throw new AbrilException("PETS no encontrado.", 404);
        var snapshot = JsonSerializer.Serialize(detalle);

        using var ctx = _factory.CreateDbContext();
        var pet = await ctx.SsomaPet.FindAsync(petId) ?? throw new AbrilException("PETS no encontrado.", 404);

        var numeroVersion = pet.VersionVigente + 1;
        var version = new SsomaPetVersion
        {
            PetId = petId,
            NumeroVersion = numeroVersion,
            SnapshotJson = snapshot,
            Motivo = motivo.Trim(),
            AprobadoPorId = aprobadoPorId,
            AprobadoPorNombre = aprobadoPorNombre,
            CreatedAt = DateTime.UtcNow
        };
        ctx.SsomaPetVersion.Add(version);

        pet.VersionVigente = numeroVersion;
        pet.EstadoRevision = "aprobado";
        pet.UpdatedAt = DateTime.UtcNow;
        // Aprobar una versión ES la revisión que el accidente/incidente pedía.
        pet.RevisionPendiente = false;
        pet.RevisionPendienteMotivo = null;

        // Cierra en automático el entregable "Evidencia de modificación de PETS"
        // (ss_entregable_tipo.id = 16) de cualquier accidente/incidente que tenía
        // este PETS asociado: la aprobación de la versión ES la evidencia, nadie
        // tiene que subir un archivo aparte para levantar ese entregable puntual.
        const int TipoEvidenciaModificacionPets = 16;
        var accidenteIds = await ctx.Set<SsomaAccidenteIncidente>()
            .Where(a => a.PetId == petId)
            .Select(a => a.Id)
            .ToListAsync();
        if (accidenteIds.Count > 0)
        {
            var entregablesPendientes = await ctx.Set<SsomaEntregable>()
                .Where(e => accidenteIds.Contains(e.AccidenteIncidenteId)
                    && e.TipoId == TipoEvidenciaModificacionPets
                    && e.Estado != "Aprobado")
                .ToListAsync();
            foreach (var ent in entregablesPendientes)
            {
                ent.Estado = "Aprobado";
                ent.Observacion = $"Auto-aprobado: PETS versión {numeroVersion} publicada el {DateTime.UtcNow:dd/MM/yyyy}.";
                ent.UpdatedAt = DateTime.UtcNow;
            }
        }

        await ctx.SaveChangesAsync();

        return new PetVersionDto
        {
            NumeroVersion = numeroVersion,
            Motivo = version.Motivo,
            AprobadoPorNombre = aprobadoPorNombre,
            CreatedAt = version.CreatedAt
        };
    }

    public async Task<List<PetVersionDto>> GetVersionesAsync(int petId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsomaPetVersion
            .Where(v => v.PetId == petId)
            .OrderByDescending(v => v.NumeroVersion)
            .Select(v => new PetVersionDto
            {
                NumeroVersion = v.NumeroVersion,
                Motivo = v.Motivo,
                AprobadoPorNombre = v.AprobadoPorNombre,
                CreatedAt = v.CreatedAt
            })
            .ToListAsync();
    }

    // Usado para regenerar el PDF "oficial" de una versión pasada — nunca se lee para
    // editar, la deserialización es de solo lectura.
    public async Task<PetDetalleDto?> GetVersionSnapshotAsync(int petId, int numeroVersion)
    {
        using var ctx = _factory.CreateDbContext();
        var version = await ctx.SsomaPetVersion.FirstOrDefaultAsync(v => v.PetId == petId && v.NumeroVersion == numeroVersion);
        if (version == null) return null;
        return JsonSerializer.Deserialize<PetDetalleDto>(version.SnapshotJson);
    }

    public async Task SetFirmaUrlAsync(int petId, string rol, string firmaUrl)
    {
        rol = ValidarRolFirma(rol);
        using var ctx = _factory.CreateDbContext();
        var pet = await ctx.SsomaPet.FindAsync(petId)
            ?? throw new AbrilException("PETS no encontrado.", 404);
        if (pet.EstadoRevision == "aprobado") pet.EstadoRevision = "borrador";

        var fila = await ctx.SsomaPetFirma.FirstOrDefaultAsync(x => x.PetId == petId && x.Rol == rol);
        if (fila == null)
        {
            ctx.SsomaPetFirma.Add(new SsomaPetFirma { PetId = petId, Rol = rol, FirmaUrl = firmaUrl, UpdatedAt = DateTime.UtcNow });
        }
        else
        {
            fila.FirmaUrl = firmaUrl;
            fila.UpdatedAt = DateTime.UtcNow;
        }
        await ctx.SaveChangesAsync();
    }
}
