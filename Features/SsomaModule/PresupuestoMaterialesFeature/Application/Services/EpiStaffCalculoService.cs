using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Services;

/// <summary>Calcula cuánto del EPI compartido con obrero (Casco/Orejera/Arnés/Lentes/Barbiquejo/
/// Guantes) corresponde a Staff, y descuenta esa porción del total ya calculado por el motor
/// normal de Materiales — para que el Desagregado de Recursos no duplique el costo entre la línea
/// de Staff y la de Obrero. Camisa/Blusa/Zapato de Staff no necesitan descuento: son families
/// exclusivas de Staff en el Catálogo, el 100% de lo consumido ya es suyo.</summary>
public class EpiStaffCalculoService : IEpiStaffCalculoService
{
    // Ids de família del Catálogo — hardcodeados (mismo patrón que FAMILIA_ID_MARCELINOS en el
    // frontend o PrecioVigilanciaPorTurno en VigilanciaHitoRepository). Si el día de mañana el
    // Catálogo reorganiza estas families, hay que actualizar estas constantes.
    private const int FamiliaCasco = 404;
    private const int FamiliaOrejera = 377;
    private const int FamiliaArnes = 383;
    private const int FamiliaLentes = 410;
    private const int FamiliaBarbiquejo = 376;
    private const int FamiliaGuantes = 433;
    private const int FamiliaCamisa = 365;
    private const int FamiliaBlusa = 415;
    private const int FamiliaZapatoStaff = 392;

    private readonly IEpiStaffRepository _epiRepo;
    private readonly IPresupuestoRepository _presupuestoRepo;
    private readonly IRatioDriverService _ratioDriverService;

    public EpiStaffCalculoService(
        IEpiStaffRepository epiRepo,
        IPresupuestoRepository presupuestoRepo,
        IRatioDriverService ratioDriverService)
    {
        _epiRepo = epiRepo;
        _presupuestoRepo = presupuestoRepo;
        _ratioDriverService = ratioDriverService;
    }

    public Task<EpiStaffConfigDto> ObtenerConfigAsync() => _epiRepo.ObtenerConfigAsync();
    public Task ActualizarConfigAsync(ActualizarEpiStaffConfigDto dto) => _epiRepo.ActualizarConfigAsync(dto);

    public async Task<EpiStaffCalculoDto?> CalcularAsync(int projectId)
    {
        var presupuestoId = await _presupuestoRepo.ObtenerUltimoPresupuestoIdAsync(projectId);
        if (presupuestoId is null) return null;

        var detalle = await _presupuestoRepo.ObtenerDetalleAsync(presupuestoId.Value);
        if (detalle is null) return null;

        var config = await _epiRepo.ObtenerConfigAsync();
        var area = await _epiRepo.ObtenerAreaTechadaAsync(projectId);
        var meses = await _epiRepo.ObtenerMesesProyectoAsync(projectId);
        var recomendados = await _ratioDriverService.ObtenerRecomendadosAsync();
        var staffHeadcount = area * (recomendados.StaffCasco?.RatioRecomendado ?? 0);

        // f.Activo: no arrastrar líneas de families ya desactivadas en Catálogo pero que quedaron
        // guardadas en una versión vieja del presupuesto (ver mismo comentario en
        // PresupuestoResumenExportService).
        var familias = detalle.Tipos.SelectMany(t => t.Familias)
            .Where(f => f.Activo)
            .ToDictionary(f => f.FamiliaId);

        var resultado = new EpiStaffCalculoDto
        {
            ProjectId = projectId,
            StaffHeadcountAplicado = staffHeadcount,
            MesesProyecto = meses,
            Config = config,
        };

        // Casco/Orejera: el precio de la família (PrecioEfectivo) mezcla ciegamente todos los
        // colores/marcas — Staff necesita el precio real de la variante premium (BLANCO/INGENIER,
        // 3M), Obrero el de las variantes normales, cada uno por separado.
        var (precioCascoStaff, precioCascoObrero) = await _epiRepo.ObtenerPreciosCascoAsync();
        var (precioOrejeraStaff, precioOrejeraObrero) = await _epiRepo.ObtenerPreciosOrejeraAsync();

        AgregarLinea(resultado, familias, FamiliaCasco, "Casco (blanco/ingeniero)", staffHeadcount,
            requiereDescuento: true, precioStaffOverride: precioCascoStaff, precioObreroOverride: precioCascoObrero);
        AgregarLinea(resultado, familias, FamiliaOrejera, "Orejera 3M", staffHeadcount,
            requiereDescuento: true, precioStaffOverride: precioOrejeraStaff, precioObreroOverride: precioOrejeraObrero);

        // El resto comparte el mismo SKU/precio entre Staff y Obrero — se descuenta la porción de
        // Staff del total ya calculado por el motor normal de Materiales (CantidadEfectiva ya
        // viene de Ratios × drivers).
        AgregarLinea(resultado, familias, FamiliaArnes, "Arnés de seguridad",
            staffHeadcount * config.ArnesPorStaff, requiereDescuento: true);
        AgregarLinea(resultado, familias, FamiliaLentes, "Lentes de seguridad",
            config.RotacionLentesMeses > 0 ? staffHeadcount * meses / config.RotacionLentesMeses : 0, requiereDescuento: true);
        AgregarLinea(resultado, familias, FamiliaBarbiquejo, "Barbiquejo",
            config.RotacionBarbiquejoMeses > 0 ? staffHeadcount * meses / config.RotacionBarbiquejoMeses : 0, requiereDescuento: true);
        AgregarLinea(resultado, familias, FamiliaGuantes, "Guantes anticorte",
            config.RotacionGuantesMeses > 0 ? staffHeadcount * meses / config.RotacionGuantesMeses : 0, requiereDescuento: true);

        // Exclusivas de Staff en el Catálogo: el 100% de lo consumido ya es suyo, nada que
        // descontarle a Obrero.
        AgregarLinea(resultado, familias, FamiliaCamisa, "Camisa", null, requiereDescuento: false);
        AgregarLinea(resultado, familias, FamiliaBlusa, "Blusa manga larga", null, requiereDescuento: false);
        AgregarLinea(resultado, familias, FamiliaZapatoStaff, "Zapato de seguridad staff", null, requiereDescuento: false);

        return resultado;
    }

    /// <summary>cantidadStaff null = sin descuento (toda la família es de Staff). Con descuento,
    /// la cantidad de Staff nunca supera lo realmente consumido — así Obrero nunca queda negativo.
    /// precioStaffOverride/precioObreroOverride: solo Casco/Orejera los usan (precio real de la
    /// variante premium vs. la normal) — el resto usa el mismo PrecioEfectivo de la família para
    /// ambos lados, porque comparten SKU.</summary>
    private static void AgregarLinea(
        EpiStaffCalculoDto resultado, Dictionary<int, PresupuestoLineaDto> familias,
        int familiaId, string nombre, decimal? cantidadStaff, bool requiereDescuento,
        decimal? precioStaffOverride = null, decimal? precioObreroOverride = null)
    {
        familias.TryGetValue(familiaId, out var f);
        var cantidadTotal = f?.CantidadEfectiva ?? 0;
        var precioFamilia = f?.PrecioEfectivo ?? 0;
        var precioStaff = precioStaffOverride ?? precioFamilia;
        var precioObrero = precioObreroOverride ?? precioFamilia;

        var staff = requiereDescuento ? Math.Min(cantidadStaff ?? 0, cantidadTotal) : cantidadTotal;
        var obrero = requiereDescuento ? Math.Max(0, cantidadTotal - staff) : 0;

        resultado.Lineas.Add(new EpiStaffLineaDto
        {
            Nombre = nombre,
            FamiliaId = familiaId,
            RequiereDescuento = requiereDescuento,
            CantidadTotalConsumida = cantidadTotal,
            CantidadStaff = staff,
            CantidadObrero = obrero,
            PrecioUnitarioStaff = precioStaff,
            PrecioUnitarioObrero = precioObrero,
            CostoStaff = Math.Round(staff * precioStaff, 2),
            CostoObrero = Math.Round(obrero * precioObrero, 2),
        });
    }
}
