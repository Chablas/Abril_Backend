using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.OptFeature.Infrastructure.Models;
using Abril_Backend.Features.SsomaModule.PetsFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PetsFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
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
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<PetDetalleDto?> GetDetalleAsync(int id)
    {
        using var ctx = _factory.CreateDbContext();
        var pet = await ctx.SsomaPet.FirstOrDefaultAsync(p => p.Id == id);
        if (pet == null) return null;

        var pasos = await ctx.SsomaPetPaso
            .Where(x => x.PetId == id && x.Activo)
            .OrderBy(x => x.Orden)
            .Select(x => new PetPasoDto
            {
                Id = x.Id,
                ParentId = x.ParentId,
                Tipo = x.Tipo,
                Descripcion = x.Descripcion,
                ImagenUrl = x.ImagenUrl,
                Orden = x.Orden
            })
            .ToListAsync();

        return new PetDetalleDto
        {
            Id = pet.Id,
            Nombre = pet.Nombre,
            Codigo = pet.Codigo,
            SharepointUrl = pet.SharepointUrl,
            Activo = pet.Activo,
            Pasos = pasos
        };
    }

    public async Task<List<PetPasoDto>> GetPasosAsync(int petId)
    {
        using var ctx = _factory.CreateDbContext();
        return await ctx.SsomaPetPaso
            .Where(x => x.PetId == petId && x.Activo)
            .OrderBy(x => x.Orden)
            .Select(x => new PetPasoDto
            {
                Id = x.Id,
                ParentId = x.ParentId,
                Tipo = x.Tipo,
                Descripcion = x.Descripcion,
                ImagenUrl = x.ImagenUrl,
                Orden = x.Orden
            })
            .ToListAsync();
    }

    public async Task<int> CrearAsync(CrearPetRequest request)
    {
        using var ctx = _factory.CreateDbContext();
        var pet = new SsomaPet
        {
            Nombre = request.Nombre,
            Codigo = request.Codigo,
            SharepointUrl = request.SharepointUrl,
            Activo = true,
            CreatedAt = DateTime.UtcNow
        };
        ctx.SsomaPet.Add(pet);
        await ctx.SaveChangesAsync();
        return pet.Id;
    }

    public async Task ActualizarAsync(int id, ActualizarPetRequest request)
    {
        using var ctx = _factory.CreateDbContext();
        var pet = await ctx.SsomaPet.FindAsync(id)
            ?? throw new AbrilException("PETS no encontrado.", 404);

        pet.Nombre = request.Nombre;
        pet.Codigo = request.Codigo;
        pet.SharepointUrl = request.SharepointUrl;
        pet.Activo = request.Activo;
        pet.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    private static readonly HashSet<string> TiposValidos = ["subtitulo", "paso", "letra", "guion"];

    private static string ValidarTipo(string? tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo)) return "paso";
        if (!TiposValidos.Contains(tipo))
            throw new AbrilException($"Tipo de paso inválido: '{tipo}'.", 400);
        return tipo;
    }

    public async Task<int> AgregarPasoAsync(int petId, CrearPetPasoRequest request)
    {
        using var ctx = _factory.CreateDbContext();
        var pet = await ctx.SsomaPet.FindAsync(petId)
            ?? throw new AbrilException("PETS no encontrado.", 404);

        if (request.ParentId.HasValue)
        {
            var padreExiste = await ctx.SsomaPetPaso.AnyAsync(p => p.Id == request.ParentId.Value && p.PetId == petId && p.Activo);
            if (!padreExiste) throw new AbrilException("El subtítulo padre no existe.", 404);
        }

        // "Hermanos": solo los pasos con el MISMO ParentId — cada grupo se ordena
        // independiente, insertar/reordenar dentro de un subtítulo no toca a otro.
        var hermanos = await ctx.SsomaPetPaso
            .Where(p => p.PetId == petId && p.Activo && p.ParentId == request.ParentId)
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

    public async Task ActualizarPasoAsync(int petId, int pasoId, ActualizarPetPasoRequest request)
    {
        using var ctx = _factory.CreateDbContext();
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
        var pasos = await ctx.SsomaPetPaso
            .Where(p => p.PetId == petId && p.Activo && p.ParentId == request.ParentId)
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
        var paso = await ctx.SsomaPetPaso.FirstOrDefaultAsync(p => p.Id == pasoId && p.PetId == petId)
            ?? throw new AbrilException("Paso no encontrado.", 404);

        paso.ImagenUrl = imagenUrl;
        paso.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }
}
