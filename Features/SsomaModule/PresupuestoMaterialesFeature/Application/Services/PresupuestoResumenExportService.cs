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

    /// <summary>Families de Barandas FRP que sí entran a la partida "Barandas de Seguridad FRP" del
    /// Resumen — solo 21mm y 25mm (id 475/476); la família genérica "BARRA FRP" (id 262) queda
    /// fuera aunque esté activa en Catálogo, por indicación explícita.</summary>
    private static readonly HashSet<int> FamiliasBarandasFrp = [475, 476];

    /// <summary>Família "Examenes medicos" del Catálogo — se calcula por ratio de Horas-Hombre igual
    /// que cualquier otra família de Materiales, pero en el Resumen agregado sale como su propia
    /// partida en vez de caer en el balde genérico "MATERIALES".</summary>
    private const int FamiliaExamenesMedicos = 328;

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

    /// <summary>Agrupa el Desagregado en partidas legibles para Costos, PERO reconciliando 100% con
    /// el total real del presupuesto — versión anterior de este método hardcodeaba unas pocas
    /// familias a mano (Rodapié/Ducto para "EPC") y se comía en silencio TODO lo demás que estuviera
    /// activo en esos Tipos (Puntales, Formatos, Conos, Drizas, Señalética... — en un caso real esto
    /// dejaba fuera S/373 mil de S/732 mil, más de la mitad). Ahora agrupa por el Tipo real del
    /// Catálogo — cualquier família que se active después entra sola a su partida sin tocar código.
    /// Los materiales inactivos (botiquín suelto, alcohol, etc.) siguen sin listarse: su Tipo nunca
    /// aparece acá porque no queda ninguna família activa que lo alimente.</summary>
    public async Task<PresupuestoResumenRecursosDto?> ObtenerResumenAgregadoAsync(int projectId)
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

        var familiasActivas = detalle.Tipos.SelectMany(t => t.Familias).Where(f => f.Activo).ToList();

        void AgregarLinea(string descripcion, string? unidad, decimal cantidad, decimal mo, decimal mat, decimal costo, decimal? pctRecuperacion = null)
        {
            if (costo == 0 && cantidad == 0) return;
            resumen.Lineas.Add(new RecursoResumenLineaDto
            {
                Grupo = "",
                Descripcion = descripcion,
                Unidad = unidad,
                Cantidad = cantidad,
                Mo = mo,
                Mat = mat,
                CostoDirecto = costo,
                PctRecuperacion = pctRecuperacion,
            });
        }

        // ── Materiales recuperables — se revenden al terminar la obra, así que el costo real
        // para el proyecto es solo la porción que NO se recupera. Hoy: Barandas FRP (85% se
        // vende). Cuando se dé de alta el Equipo de Monitoreo de Gases en Catálogo (también se
        // va a vender), agregar su FamiliaId acá con el mismo % para que salga igual en el Excel.
        const decimal PctRecuperacionFrp = 0.85m;

        // ── Barandas de Seguridad FRP — 21mm y 25mm van como 2 líneas separadas,
        // igual que en el modelo referencial (Horizontal/Vertical) ─────────────
        var etiquetasBarandas = new Dictionary<int, string> { [476] = "21mm", [475] = "25mm" };
        foreach (var familiaId in FamiliasBarandasFrp)
        {
            var f = familiasActivas.FirstOrDefault(x => x.FamiliaId == familiaId);
            if (f is null) continue;
            AgregarLinea($"Barandas de Seguridad con FRP {etiquetasBarandas[familiaId]}", "ml", f.CantidadEfectiva,
                0, f.PrecioEfectivo, f.TotalEfectivo, PctRecuperacionFrp);
        }

        // ── Personal por rol (sumado en todos los hitos donde participe) ────────
        var personal = await _personalHitoRepo.ObtenerPorProyectoAsync(projectId);
        void AgregarPersonalPorRoles(string descripcion, params string[] roles)
        {
            var filas = personal.Where(p => roles.Contains(p.Rol)).ToList();
            var cantidad = filas.Sum(p => p.Cantidad * p.Semanas);
            var costo = filas.Sum(p => p.Total);
            AgregarLinea(descripcion, "pers-sem", cantidad, cantidad == 0 ? 0 : costo / cantidad, 0, costo);
        }
        // Vígia hace el mismo trabajo que Paletero en la práctica — se cargan juntos aunque en
        // Personal por hito estén como roles separados (VIGIA-OFICIAL/VIGIA-PEON vs PALETERO).
        AgregarPersonalPorRoles("Personal de Seguridad - Paleteros", "PALETERO", "VIGIA-OFICIAL", "VIGIA-PEON");
        AgregarPersonalPorRoles("Personal de Seguridad - Paleteros Montacarga", "PALETERO-MONTACARGA");

        // ── Monitores: una línea por hito/etapa del cronograma, tal como esté armado (puede ser
        // más de 2 etapas) — junta Oficial+Peón de ese hito en una sola línea "Monitores - <Hito>".
        var monitores = personal.Where(p => p.Rol is "MONITOR-OFICIAL" or "MONITOR-PEON");
        foreach (var grupo in monitores.GroupBy(p => p.HitoDescripcion))
        {
            var cantidad = grupo.Sum(p => p.Cantidad * p.Semanas);
            var costo = grupo.Sum(p => p.Total);
            AgregarLinea($"Personal de Seguridad - Monitores - {grupo.Key}", "pers-sem",
                cantidad, cantidad == 0 ? 0 : costo / cantidad, 0, costo);
        }

        // ── EPI Obrero / EPI Staff ───────────────────────────────────────────────
        // "EPI - Obrero" es TODO el Tipo EPP del Catálogo puesto a cargo del obrero: la porción
        // obrero de las 9 famílias compartidas con Staff (descuento de EpiStaffCalculoService) MÁS
        // el resto de EPP (zapato/pantalón/polo/guantes normales, etc.) que es 100% del obrero, sin
        // descuento — antes solo se contaban las 9 famílias del descuento y el resto de EPP (la
        // mayor parte del costo real) no aparecía en ningún lado del Resumen.
        var epiStaff = await _epiStaffCalculoService.CalcularAsync(projectId);
        var costoObrero = epiStaff?.Lineas.Where(l => l.RequiereDescuento).Sum(l => l.CostoObrero) ?? 0;
        var cantidadObrero = epiStaff?.Lineas.Where(l => l.RequiereDescuento).Sum(l => l.CantidadObrero) ?? 0;
        var costoStaff = epiStaff?.Lineas.Sum(l => l.CostoStaff) ?? 0;
        var cantidadStaff = epiStaff?.Lineas.Sum(l => l.CantidadStaff) ?? 0;

        var restoEpp = familiasActivas.Where(f => f.NombreTipo == "EPP" && !FamiliasEpiManejadasAparte.Contains(f.FamiliaId));
        costoObrero += restoEpp.Sum(f => f.TotalEfectivo);
        cantidadObrero += restoEpp.Sum(f => f.CantidadEfectiva);

        AgregarLinea("Equipos de Protección Individual (EPI) - Obrero", "und", cantidadObrero,
            0, cantidadObrero == 0 ? 0 : costoObrero / cantidadObrero, costoObrero);
        AgregarLinea("Equipos de Protección Individual (EPI) - Staff", "und", cantidadStaff,
            0, cantidadStaff == 0 ? 0 : costoStaff / cantidadStaff, costoStaff);

        // ── EPC: TODO el Tipo EPC del Catálogo (menos Barandas FRP, que van en sus 2 líneas
        // propias arriba) + Kits (Botiquín/Estación de Emergencia) ─────────────
        var kits = await _kitRepo.ObtenerGuardadosPorProyectoAsync(projectId);
        var costoKits = kits.Sum(k => k.Total);
        var costoEpc = familiasActivas
            .Where(f => f.NombreTipo == "EPC" && !FamiliasBarandasFrp.Contains(f.FamiliaId))
            .Sum(f => f.TotalEfectivo) + costoKits;
        AgregarLinea("Equipos de Protección Colectiva (EPC)", "glb", 1, 0, costoEpc, costoEpc);

        // ── Varios Seguridad: Servicios y equipos (Servicios Fijos) únicamente ──────────
        var servicios = await _servicioFijoRepo.ObtenerPorProyectoAsync(projectId);
        var costoServicios = servicios.Sum(s => s.Total);
        AgregarLinea("Varios Seguridad", "glb", 1, 0, costoServicios, costoServicios);

        // ── Costo fijo manual: cada uno es su propia partida ────────────────────
        var costoFijo = await _costoFijoManualService.ObtenerPorProyectoAsync(projectId);
        AgregarLinea("Malla Anticaída de Protección Edificio", "glb", 1, 0, costoFijo.MallaAnticaida, costoFijo.MallaAnticaida);
        AgregarLinea("Encapsulado en Fachada de Edificio", "glb", 1, 0, costoFijo.Encapsulado, costoFijo.Encapsulado);
        AgregarLinea("Malla Anillo Fenólico", "glb", 1, 0, costoFijo.MallaAnilloFenolico, costoFijo.MallaAnilloFenolico);

        // ── Exámenes Médicos: família propia, calculada por ratio de Horas-Hombre ───
        var examenes = familiasActivas.FirstOrDefault(f => f.FamiliaId == FamiliaExamenesMedicos);
        if (examenes is not null)
            AgregarLinea("Exámenes Médicos para el Personal Obrero", "und",
                examenes.CantidadEfectiva, 0, examenes.PrecioEfectivo, examenes.TotalEfectivo);

        for (var i = 0; i < resumen.Lineas.Count; i++)
            resumen.Lineas[i].Item = (i + 1).ToString("D2");

        return resumen;
    }

    public async Task<byte[]?> ExportarExcelAsync(int projectId)
    {
        var resumen = await ObtenerResumenAsync(projectId);
        if (resumen is null) return null;
        var resumenAgregado = await ObtenerResumenAgregadoAsync(projectId);

        using var workbook = new XLWorkbook();
        var wsResumen = workbook.Worksheets.Add("RESUMEN");
        PresupuestoResumenExcelBuilder.Build(wsResumen, resumenAgregado!, "RESUMEN SSOMA");

        var wsDesagregado = workbook.Worksheets.Add("DESAGREGADO DE RECURSOS");
        PresupuestoResumenExcelBuilder.Build(wsDesagregado, resumen);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}
