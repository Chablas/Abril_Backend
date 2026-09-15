using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Helpers;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;
using ClosedXML.Excel;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Services;

/// <summary>Arma el "Desagregado de Recursos" SSOMA que pide Costos/Presupuesto: junta las 5 fuentes
/// de costo del presupuesto vigente de un proyecto en un único formato de fila (mismo que usa Costos
/// en su presupuesto general de obra), con Personal→MO y el resto (Materiales/Vigilancia/Servicios
/// fijos/Kits)→MAT. No incluye la fila padre de obra general (P.C./"OBRAS PRELIMINARES...") — eso
/// vive en el presupuesto general de Costos, fuera del alcance de este sistema.</summary>
public class PresupuestoResumenExportService : IPresupuestoResumenExportService
{
    /// <summary>Families de EPI que se manejan aparte (descuento Staff/Obrero, o 100% Staff) —
    /// se excluyen del volcado genérico de Materiales para no listarlas dos veces.</summary>
    private static readonly HashSet<int> FamiliasEpiManejadasAparte = [404, 377, 383, 410, 376, 433, 365, 415, 392];

    /// <summary>Families de Materiales que en el export se etiquetan "EPC" en vez de
    /// "MATERIALES" — Rodapié (triplay 8mm) y Ducto (fenólico 18mm + listón) del Cálculo técnico.
    /// Solo cambia la etiqueta para el Excel; el cálculo interno de la família sigue igual.</summary>
    private static readonly HashSet<int> FamiliasEpc = [292, 211, 253];

    private readonly IPresupuestoRepository _presupuestoRepo;
    private readonly IPersonalHitoRepository _personalHitoRepo;
    private readonly IVigilanciaHitoRepository _vigilanciaHitoRepo;
    private readonly IServicioFijoRepository _servicioFijoRepo;
    private readonly IKitRepository _kitRepo;
    private readonly IEpiStaffCalculoService _epiStaffCalculoService;
    private readonly ICostoFijoManualService _costoFijoManualService;

    public PresupuestoResumenExportService(
        IPresupuestoRepository presupuestoRepo,
        IPersonalHitoRepository personalHitoRepo,
        IVigilanciaHitoRepository vigilanciaHitoRepo,
        IServicioFijoRepository servicioFijoRepo,
        IKitRepository kitRepo,
        IEpiStaffCalculoService epiStaffCalculoService,
        ICostoFijoManualService costoFijoManualService)
    {
        _presupuestoRepo = presupuestoRepo;
        _personalHitoRepo = personalHitoRepo;
        _vigilanciaHitoRepo = vigilanciaHitoRepo;
        _servicioFijoRepo = servicioFijoRepo;
        _kitRepo = kitRepo;
        _epiStaffCalculoService = epiStaffCalculoService;
        _costoFijoManualService = costoFijoManualService;
    }

    public async Task<PresupuestoResumenRecursosDto?> ObtenerResumenAsync(int projectId)
    {
        var presupuestoId = await _presupuestoRepo.ObtenerUltimoPresupuestoIdAsync(projectId);
        if (presupuestoId is null) return null;

        var detalle = await _presupuestoRepo.ObtenerDetalleAsync(presupuestoId.Value);
        if (detalle is null) return null;

        var resumen = new PresupuestoResumenRecursosDto
        {
            PresupuestoId = presupuestoId.Value,
            ProjectId = detalle.ProjectId,
            ProjectDescription = detalle.ProjectDescription,
            Version = detalle.Version,
            Estado = detalle.Estado,
        };

        // Lista plana, numerada en una sola secuencia — igual que el Desagregado de Recursos real
        // (cada partida es su propia fila, sin agrupar bajo un encabezado por fuente).

        // ── Materiales por ratio (menos las families de EPI que se arman aparte más abajo,
        // con el descuento Staff/Obrero ya aplicado) ─────────────────────────────
        // f.Activo: la línea puede venir de una versión de presupuesto vieja, generada antes de
        // que la família se desactivara en Catálogo (ss_presupuesto_detalle no se reescribe solo
        // porque el catálogo cambió después) — el export solo debe reflejar el catálogo actual.
        var materiales = detalle.Tipos.SelectMany(t => t.Familias)
            .Where(f => f.Activo && !FamiliasEpiManejadasAparte.Contains(f.FamiliaId));
        foreach (var f in materiales)
        {
            resumen.Lineas.Add(new RecursoResumenLineaDto
            {
                Grupo = FamiliasEpc.Contains(f.FamiliaId) ? "EPC" : "MATERIALES",
                Descripcion = f.NombreFamilia,
                Cantidad = f.CantidadEfectiva,
                Mat = f.PrecioEfectivo,
                CostoDirecto = f.TotalEfectivo,
            });
        }

        // ── EPI de Staff (Casco/Orejera/Arnés/Lentes/Barbiquejo/Guantes descontados de la
        // porción de Obrero; Camisa/Blusa/Zapato 100% Staff) ────────────────────
        var epiStaff = await _epiStaffCalculoService.CalcularAsync(projectId);
        if (epiStaff is not null)
        {
            foreach (var linea in epiStaff.Lineas)
            {
                if (linea.RequiereDescuento)
                {
                    resumen.Lineas.Add(new RecursoResumenLineaDto
                    {
                        Grupo = "EPI OBRERO",
                        Descripcion = linea.Nombre,
                        Cantidad = linea.CantidadObrero,
                        Mat = linea.PrecioUnitarioObrero,
                        CostoDirecto = linea.CostoObrero,
                    });
                }
                resumen.Lineas.Add(new RecursoResumenLineaDto
                {
                    Grupo = "EPI STAFF",
                    Descripcion = linea.Nombre,
                    Cantidad = linea.CantidadStaff,
                    Mat = linea.PrecioUnitarioStaff,
                    CostoDirecto = linea.CostoStaff,
                });
            }
        }

        // ── Dotación de personal por hito (Personal→MO) ─────────────────────────
        var personal = await _personalHitoRepo.ObtenerPorProyectoAsync(projectId);
        foreach (var p in personal)
        {
            // Cantidad combinada (personas × semanas) para que Cantidad × PU cierre exacto con el
            // Total ya calculado, aunque el origen real sea cantidad×tarifa×semanas — igual que en
            // el modelo, donde Cantidad(meses) × PU(tarifa mensual) = Costo Directo.
            var cantidadCombinada = p.Cantidad * p.Semanas;
            resumen.Lineas.Add(new RecursoResumenLineaDto
            {
                Grupo = "PERSONAL",
                Descripcion = $"{p.Rol} — {p.HitoDescripcion}",
                Unidad = "pers-sem",
                Cantidad = cantidadCombinada,
                Mo = cantidadCombinada == 0 ? 0 : p.Total / cantidadCombinada,
                CostoDirecto = p.Total,
            });
        }

        // ── Vigilancia por hito (Vigilancia→MAT) ────────────────────────────────
        var vigilancia = await _vigilanciaHitoRepo.ObtenerPorProyectoAsync(projectId);
        foreach (var v in vigilancia)
        {
            var cantidadCombinada = v.CantidadPuntos * v.Semanas;
            resumen.Lineas.Add(new RecursoResumenLineaDto
            {
                Grupo = "VIGILANCIA",
                Descripcion = $"Vigilancia — {v.HitoDescripcion}",
                Unidad = "pto-sem",
                Cantidad = cantidadCombinada,
                Mat = cantidadCombinada == 0 ? 0 : v.Total / cantidadCombinada,
                CostoDirecto = v.Total,
            });
        }

        // ── Servicios fijos (Servicios→MAT) ─────────────────────────────────────
        var servicios = await _servicioFijoRepo.ObtenerPorProyectoAsync(projectId);
        foreach (var s in servicios)
        {
            resumen.Lineas.Add(new RecursoResumenLineaDto
            {
                Grupo = "SERVICIOS FIJOS",
                Descripcion = s.Descripcion is { Length: > 0 } d ? $"{s.NombreFamilia} — {d}" : s.NombreFamilia,
                Unidad = s.UnidadMedida,
                Cantidad = s.Metrado,
                Mat = s.PrecioUnitario,
                CostoDirecto = s.Total,
            });
        }

        // ── Kits (Botiquín/Estación de Emergencia→MAT) — se etiquetan "EPC" en el export
        // (siguen siendo "Kits" en el resto de la app, esto es solo para el Excel) ──────
        var kits = await _kitRepo.ObtenerGuardadosPorProyectoAsync(projectId);
        foreach (var k in kits)
        {
            resumen.Lineas.Add(new RecursoResumenLineaDto
            {
                Grupo = "EPC",
                Descripcion = k.NombreKit,
                Unidad = "kit",
                Cantidad = k.CantidadKits,
                Mat = k.CantidadKits == 0 ? 0 : k.Total / k.CantidadKits,
                CostoDirecto = k.Total,
            });
        }

        // ── Costo fijo manual (Malla Anticaída/Encapsulado/Malla Anillo Fenólico→MAT) ───
        var costoFijo = await _costoFijoManualService.ObtenerPorProyectoAsync(projectId);
        void AgregarCostoFijo(string nombre, decimal monto)
        {
            if (monto == 0) return;
            resumen.Lineas.Add(new RecursoResumenLineaDto
            {
                Grupo = "COSTO FIJO MANUAL",
                Descripcion = nombre,
                Unidad = "glb",
                Cantidad = 1,
                Mat = monto,
                CostoDirecto = monto,
            });
        }
        AgregarCostoFijo("Malla anticaída de protección", costoFijo.MallaAnticaida);
        AgregarCostoFijo("Encapsulado en fachada", costoFijo.Encapsulado);
        AgregarCostoFijo("Malla anillo fenólico", costoFijo.MallaAnilloFenolico);

        // Numeración única y continua sobre TODO el listado, igual que la columna "Item" del
        // modelo (01, 02, 03... sin reiniciar por fuente).
        for (var i = 0; i < resumen.Lineas.Count; i++)
            resumen.Lineas[i].Item = (i + 1).ToString("D2");

        return resumen;
    }

    public async Task<byte[]?> ExportarExcelAsync(int projectId)
    {
        var resumen = await ObtenerResumenAsync(projectId);
        if (resumen is null) return null;

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("DESAGREGADO DE RECURSOS");
        PresupuestoResumenExcelBuilder.Build(ws, resumen);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}
