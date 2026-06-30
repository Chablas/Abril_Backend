using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Services;

public class CatalogoMaterialesService : ICatalogoMaterialesService
{
    private readonly ICatalogoMaterialesRepository _repo;

    public CatalogoMaterialesService(ICatalogoMaterialesRepository repo)
    {
        _repo = repo;
    }

    public async Task<SeedCatalogoResultDto> SeedCatalogoAsync(SeedCatalogoRequestDto request)
    {
        var resultado = new SeedCatalogoResultDto();
        var tiposCache = new Dictionary<string, int>();

        foreach (var fila in request.Items)
        {
            if (string.IsNullOrWhiteSpace(fila.Recurso) || string.IsNullOrWhiteSpace(fila.NomStd1)
                || string.IsNullOrWhiteSpace(fila.NomStd2) || string.IsNullOrWhiteSpace(fila.TipoMaterial))
            {
                resultado.Advertencias.Add($"Fila incompleta omitida: '{fila.Recurso}'");
                continue;
            }

            // Tipo (normalizado: "Varios"/"VARIOS" colapsan al mismo)
            var tipoNorm = TextoNormalizador.Normalizar(fila.TipoMaterial);
            if (!tiposCache.TryGetValue(tipoNorm, out var tipoId))
            {
                var tipo = await _repo.GetOrCreateTipoAsync(tipoNorm);
                tipoId = tipo.Id;
                tiposCache[tipoNorm] = tipoId;
                resultado.TiposCreados++;
            }

            // Familia = NomStd2 (nivel de agrupación/ratio)
            var familiaNombreNorm = TextoNormalizador.Normalizar(fila.NomStd2);
            var variableBaseNorm = TextoNormalizador.Normalizar(fila.VariableBase);
            var (familia, familiaCreada) = await _repo.GetOrCreateFamiliaAsync(
                fila.NomStd2.Trim(), familiaNombreNorm, tipoId, variableBaseNorm, fila.PerteneceSsoma);

            if (familiaCreada) resultado.FamiliasCreadas++;
            else resultado.FamiliasExistentes++;

            // Item = NomStd1, con talla/dimensión extraídas para no fragmentar la familia
            var itemNombreNorm = TextoNormalizador.Normalizar(fila.NomStd1);
            var (sinTalla, talla) = TextoNormalizador.ExtraerTalla(itemNombreNorm);
            var (sinDimension, dimensionNorm) = TextoNormalizador.ExtraerDimension(sinTalla);
            var noUsar = TextoNormalizador.TieneNoUsar(fila.Recurso) || TextoNormalizador.TieneNoUsar(fila.NomStd1);

            var (item, itemCreado) = await _repo.GetOrCreateItemAsync(
                fila.NomStd1.Trim(), sinDimension, familia.Id, talla, dimensionNorm, noUsar);

            if (itemCreado) resultado.ItemsCreados++;
            else resultado.ItemsExistentes++;

            // Alias: el texto crudo original (Recurso del S10) -> item estandarizado
            var recursoNorm = TextoNormalizador.Normalizar(fila.Recurso);
            var aliasCreado = await _repo.CreateAliasIfNotExistsAsync(
                fila.Recurso.Trim(), recursoNorm, item.Id, "SEED", confianza: 1.0m);

            if (aliasCreado) resultado.AliasCreados++;
        }

        return resultado;
    }
}
