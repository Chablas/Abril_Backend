using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Services;

public class EstandarizacionService : IEstandarizacionService
{
    // Umbral para auto-estandarizar sin revisión humana
    private const decimal UMBRAL_AUTO = 0.80m;
    // Umbral mínimo para enviar a revisión (debajo = sin match)
    private const decimal UMBRAL_REVISION = 0.55m;

    private readonly IConsumoRepository _consumoRepo;
    private readonly IEstandarizacionRepository _estandarizacionRepo;

    public EstandarizacionService(IConsumoRepository consumoRepo, IEstandarizacionRepository estandarizacionRepo)
    {
        _consumoRepo = consumoRepo;
        _estandarizacionRepo = estandarizacionRepo;
    }

    public async Task<EstandarizacionLoteResultDto> EstandarizarCargaAsync(int cargaId)
    {
        var lineas = await _consumoRepo.ObtenerLineasSinEstandarizarAsync(cargaId);
        var resultado = new EstandarizacionLoteResultDto { TotalProcesadas = lineas.Count };
        var detalles = new List<EstandarizacionLineaDto>();

        int autoResueltas = 0, enRevision = 0, sinMatch = 0;

        foreach (var linea in lineas)
        {
            var textoNorm = TextoNormalizador.Normalizar(linea.RecursoCrudo);
            var detalle = await ProcesarLineaAsync(linea.Id, linea.RecursoCrudo, textoNorm);
            detalles.Add(detalle);

            switch (detalle.Resultado)
            {
                case "AUTO_ALIAS":
                case "AUTO_EXACTO":
                case "AUTO_FUZZY":
                    autoResueltas++;
                    break;
                case "REVISION":
                    enRevision++;
                    break;
                default:
                    sinMatch++;
                    break;
            }
        }

        resultado.AutoResueltas = autoResueltas;
        resultado.EnRevision = enRevision;
        resultado.SinMatch = sinMatch;
        resultado.Detalles = detalles;

        await _consumoRepo.ActualizarContadoresCargaAsync(cargaId, autoResueltas, enRevision);
        return resultado;
    }

    private async Task<EstandarizacionLineaDto> ProcesarLineaAsync(long lineaId, string recursoCrudo, string textoNorm)
    {
        // Etapa 1: Alias exacto (O(1) — diccionario de aprendizaje)
        var match = await _estandarizacionRepo.BuscarPorAliasExactoAsync(textoNorm);
        if (match != null)
        {
            await _consumoRepo.ActualizarLineaEstandarizadaAsync(lineaId, match.ItemId, match.PerteneceSsoma, "ALIAS", 1.0m, null);
            return ToDetalle(lineaId, recursoCrudo, "AUTO_ALIAS", match);
        }

        // Etapa 2: Nombre normalizado exacto en catálogo
        match = await _estandarizacionRepo.BuscarPorNombreExactoAsync(textoNorm);
        if (match != null)
        {
            await _consumoRepo.ActualizarLineaEstandarizadaAsync(lineaId, match.ItemId, match.PerteneceSsoma, "EXACTO", 1.0m, null);
            // Aprender: guardar alias para próxima vez O(1)
            await _estandarizacionRepo.CrearAliasAsync(recursoCrudo, textoNorm, match.ItemId, "FUZZY_CONFIRMADO", 1.0m);
            return ToDetalle(lineaId, recursoCrudo, "AUTO_EXACTO", match);
        }

        // Etapa 3: Intentar sin talla ni dimensión (expansión de búsqueda)
        var (sinTalla, _) = TextoNormalizador.ExtraerTalla(textoNorm);
        var (sinDim, _) = TextoNormalizador.ExtraerDimension(sinTalla);
        if (sinDim != textoNorm)
        {
            match = await _estandarizacionRepo.BuscarPorNombreExactoAsync(sinDim);
            if (match != null)
            {
                await _consumoRepo.ActualizarLineaEstandarizadaAsync(lineaId, match.ItemId, match.PerteneceSsoma, "EXACTO_SIN_TALLA", 0.95m, null);
                await _estandarizacionRepo.CrearAliasAsync(recursoCrudo, textoNorm, match.ItemId, "FUZZY_CONFIRMADO", 0.95m);
                return ToDetalle(lineaId, recursoCrudo, "AUTO_EXACTO", match, 0.95m);
            }
        }

        // Etapa 4: Trigram con umbral alto → auto-estandariza
        var candidatos = await _estandarizacionRepo.BuscarPorTrigramAsync(textoNorm, UMBRAL_REVISION);
        if (candidatos.Count > 0)
        {
            var mejor = candidatos[0];
            if (mejor.Score >= UMBRAL_AUTO)
            {
                // Score alto: auto-estandarizar y aprender
                await _consumoRepo.ActualizarLineaEstandarizadaAsync(lineaId, mejor.ItemId, mejor.PerteneceSsoma, "FUZZY", mejor.Score, null);
                await _estandarizacionRepo.CrearAliasAsync(recursoCrudo, textoNorm, mejor.ItemId, "FUZZY_CONFIRMADO", mejor.Score);
                return ToDetalle(lineaId, recursoCrudo, "AUTO_FUZZY", mejor);
            }
            else
            {
                // Score medio: enviar a revisión humana (se guarda el mejor match como sugerencia)
                await _consumoRepo.ActualizarLineaEstandarizadaAsync(lineaId, mejor.ItemId, mejor.PerteneceSsoma, "FUZZY", mejor.Score, "PENDIENTE");
                return ToDetalle(lineaId, recursoCrudo, "REVISION", mejor);
            }
        }

        // Etapa 5: Sin match
        return new EstandarizacionLineaDto
        {
            LineaId = lineaId,
            RecursoCrudo = recursoCrudo,
            Resultado = "SIN_MATCH"
        };
    }

    private static EstandarizacionLineaDto ToDetalle(long lineaId, string recursoCrudo, string resultado, MatchResult match, decimal? scoreOverride = null) =>
        new()
        {
            LineaId = lineaId,
            RecursoCrudo = recursoCrudo,
            Resultado = resultado,
            ItemId = match.ItemId,
            NombreItem = match.NombreItem,
            NombreFamilia = match.NombreFamilia,
            Score = scoreOverride ?? match.Score
        };
}
