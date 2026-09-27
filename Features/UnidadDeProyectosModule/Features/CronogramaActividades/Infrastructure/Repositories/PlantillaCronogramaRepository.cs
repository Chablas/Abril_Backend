using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Infrastructure.Repositories
{
    public class PlantillaCronogramaRepository : IPlantillaCronogramaRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public PlantillaCronogramaRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<PlantillaItemDto>> GetByTipoAsync(string tipoCronograma)
        {
            using var ctx = _factory.CreateDbContext();

            return await ctx.CronogramaTemplateItems
                .Where(i => i.TipoCronograma == tipoCronograma && i.State && i.Active)
                .OrderBy(i => i.Orden)
                .Select(i => new PlantillaItemDto
                {
                    Id = i.Id,
                    TipoCronograma = i.TipoCronograma,
                    Codigo = i.Codigo,
                    Nombre = i.Nombre,
                    Nivel = i.Nivel,
                    EsPadre = i.EsPadre,
                    ParentCodigo = i.ParentCodigo,
                    PredecesoraCodigo = i.PredecesoraCodigo,
                    Orden = i.Orden
                })
                .ToListAsync();
        }

        public async Task<PlantillaItemDto> CrearItemAsync(CrearPlantillaItemRequest request, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var existeCodigo = await ctx.CronogramaTemplateItems
                .AnyAsync(i => i.TipoCronograma == request.TipoCronograma && i.Codigo == request.Codigo && i.State && i.Active);
            if (existeCodigo)
                throw new AbrilException($"Ya existe un ítem con el código '{request.Codigo}' en la plantilla de {request.TipoCronograma}.", 409);

            var item = new CronogramaTemplateItem
            {
                TipoCronograma = request.TipoCronograma,
                Codigo = request.Codigo,
                Nombre = request.Nombre,
                Nivel = request.Nivel,
                EsPadre = request.EsPadre,
                ParentCodigo = request.ParentCodigo,
                PredecesoraCodigo = request.PredecesoraCodigo,
                Orden = request.Orden,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId,
                Active = true,
                State = true
            };

            ctx.CronogramaTemplateItems.Add(item);
            await ctx.SaveChangesAsync();

            return MapToDto(item);
        }

        public async Task<PlantillaItemDto> EditarItemAsync(int id, EditarPlantillaItemRequest request, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var item = await ctx.CronogramaTemplateItems
                .FirstOrDefaultAsync(i => i.Id == id && i.State && i.Active)
                ?? throw new AbrilException("El ítem de plantilla no existe.", 404);

            var existeCodigo = await ctx.CronogramaTemplateItems
                .AnyAsync(i => i.Id != id && i.TipoCronograma == item.TipoCronograma && i.Codigo == request.Codigo && i.State && i.Active);
            if (existeCodigo)
                throw new AbrilException($"Ya existe un ítem con el código '{request.Codigo}' en la plantilla de {item.TipoCronograma}.", 409);

            item.Codigo = request.Codigo;
            item.Nombre = request.Nombre;
            item.Nivel = request.Nivel;
            item.EsPadre = request.EsPadre;
            item.ParentCodigo = request.ParentCodigo;
            item.PredecesoraCodigo = request.PredecesoraCodigo;
            item.Orden = request.Orden;
            item.UpdatedDateTime = DateTime.UtcNow;
            item.UpdatedUserId = userId;

            await ctx.SaveChangesAsync();

            return MapToDto(item);
        }

        public async Task EliminarItemAsync(int id, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var item = await ctx.CronogramaTemplateItems
                .FirstOrDefaultAsync(i => i.Id == id && i.State && i.Active)
                ?? throw new AbrilException("El ítem de plantilla no existe.", 404);

            item.Active = false;
            item.State = false;
            item.UpdatedDateTime = DateTime.UtcNow;
            item.UpdatedUserId = userId;

            await ctx.SaveChangesAsync();
        }

        private static PlantillaItemDto MapToDto(CronogramaTemplateItem item) => new()
        {
            Id = item.Id,
            TipoCronograma = item.TipoCronograma,
            Codigo = item.Codigo,
            Nombre = item.Nombre,
            Nivel = item.Nivel,
            EsPadre = item.EsPadre,
            ParentCodigo = item.ParentCodigo,
            PredecesoraCodigo = item.PredecesoraCodigo,
            Orden = item.Orden
        };
    }
}
