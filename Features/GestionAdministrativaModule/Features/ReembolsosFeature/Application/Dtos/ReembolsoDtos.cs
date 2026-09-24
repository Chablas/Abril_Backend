using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Email;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;

namespace Abril_Backend.Features.GestionAdministrativa.Reembolsos.Application.Dtos
{
    /// <summary>
    /// Un Consolidado del S10 en la bandeja de Tesorería: ya firmado por la jefatura y esperando la
    /// revisión documental, ya confirmado y esperando el pago, o ya pagado. La unidad de esta
    /// pantalla es el CONSOLIDADO y no la planilla —un mismo registro del S10 puede cubrir varias,
    /// de uno o de varios trabajadores— porque lo que Tesorería revisa y desembolsa es el
    /// documento entero: partirlo por planilla dejaba al trabajador con un reembolso a medias y
    /// hacía que el importe declarado en el S10 no cuadrara nunca con la fila.
    ///
    /// Tesorería ve TODA la organización —su recorte es por estado, no por área— así que acá no hay
    /// filtro de visibilidad como en las otras pantallas de salidas.
    /// </summary>
    public class ReembolsoListItemDto
    {
        /// <summary>Id de <c>ga_consolidado_s10</c>.</summary>
        public int Id { get; set; }

        /// <summary>
        /// Código de la rendición grupal, <c>CONS-ÁREA-AAAA-NNN</c>: el nombre del conjunto de planillas
        /// que se consolidaron juntas. Sobrevive al reemplazo del archivo. Null en los consolidados
        /// anteriores a la columna.
        /// </summary>
        public string? Codigo { get; set; }

        /// <summary>Número de reembolso que devolvió el S10: es el nombre que le puso el S10.</summary>
        public string? NumeroReembolso { get; set; }

        /// <summary>
        /// La PLANILLA GRUPAL: el PDF que junta en un solo documento las planillas de gasto de todo
        /// lo que cubre el consolidado. La genera Abril One al adjuntarse el S10, no se sube. Null
        /// en los consolidados anteriores a la columna.
        /// </summary>
        public string? PlanillaGrupalUrl { get; set; }
        public string? PlanillaGrupalFilename { get; set; }
        /// <summary>
        /// Copia de la planilla grupal con la firma de la jefatura. Null mientras no se apruebe, y
        /// en los consolidados aprobados antes de que la grupal se firmara.
        /// </summary>
        public string? PlanillaGrupalFirmadoUrl { get; set; }
        public string? PlanillaGrupalFirmadoFilename { get; set; }


        /// <summary>
        /// Importe con el que el S10 registró las planillas que cubre — el documento entero. Null en
        /// los consolidados subidos antes de que se pidiera el monto.
        /// </summary>
        public decimal? MontoS10 { get; set; }

        /// <summary>
        /// Lo que dice Abril One del documento entero: la suma de las planillas COMPLETAS que
        /// cubre. Es el número contra el que se contrasta <see cref="MontoS10"/>.
        /// </summary>
        public decimal MontoPlanillas { get; set; }

        /// <summary>
        /// Lo que se mueve desde acá: la suma de las salidas del consolidado que llegaron a
        /// Tesorería. Es igual a <see cref="MontoPlanillas"/> salvo que una parte del documento
        /// siga esperando la firma de otra jefatura.
        /// </summary>
        public decimal MontoTotal { get; set; }

        // ── Documentos que Tesorería necesita ver antes de pagar ─────────
        public string PdfUrl { get; set; } = string.Empty;
        public string PdfFilename { get; set; } = string.Empty;
        /// <summary>Copia firmada por la jefatura: es el respaldo del pago.</summary>
        public string? PdfFirmadoUrl { get; set; }
        public string? PdfFirmadoFilename { get; set; }
        public DateTimeOffset? FirmadoAt { get; set; }
        public DateTimeOffset UploadedAt { get; set; }
        /// <summary>Quién lo adjuntó: el consolidador. Null si no se pudo resolver.</summary>
        public string? SubidoPor { get; set; }
        /// <summary>Razón social bajo la que quedó el registro del S10: la del consolidador.</summary>
        public string? RazonSocial { get; set; }

        // ── Qué cubre ────────────────────────────────────────────────────
        /// <summary>
        /// Planillas cubiertas, TODAS —también las que todavía no llegaron a Tesorería—, porque el
        /// documento y su importe son de ese conjunto entero. Ver
        /// <see cref="ReembolsoPlanillaDto.EnBandeja"/>.
        /// </summary>
        public List<ReembolsoPlanillaDto> Rendiciones { get; set; } = new();

        /// <summary>Trabajadores de las salidas que llegaron a Tesorería, sin repetir.</summary>
        public List<string> Trabajadores { get; set; } = new();
        public int SalidasCount { get; set; }

        /// <summary>"Agosto 2026", o un rango si el consolidado cruza meses.</summary>
        public string Periodo { get; set; } = string.Empty;
        public int PeriodoAnio { get; set; }
        public int PeriodoMes { get; set; }

        /// <summary>
        /// La firma electrónica que Tesorería tiene que ver antes de proceder (RG-24 / RF-TES-02).
        /// Es una lista porque un consolidado compartido por planillas de jefes distintos acumula
        /// las firmas de todos ellos.
        /// </summary>
        public List<FirmaJefaturaDto> Firmas { get; set; } = new();

        /// <summary>
        /// "Firmado", "Proceder con el reembolso", "Pagado" u "Observado" (el más atrasado si el
        /// consolidado trae salidas de varios).
        /// </summary>
        public string EstadoReembolso { get; set; } = EstadosSalida.Reembolso.NombreFirmado;
        public bool ReembolsoMixto { get; set; }

        /// <summary>
        /// Salidas firmadas y sin confirmar: es lo que se marca al Confirmar revisión. Mientras
        /// sea &gt; 0 el consolidado espera a Tesorería y todavía NO se puede pagar (RG-26).
        /// </summary>
        public int PorConfirmarCount { get; set; }
        /// <summary>Salidas ya confirmadas y sin pagar: es lo que se paga al marcar el consolidado.</summary>
        public int PorPagarCount { get; set; }
        /// <summary>
        /// Salidas que la propia Tesorería devolvió y siguen esperando la subsanación (RG-49).
        /// Mientras sea &gt; 0 el consolidado no se toca desde acá: la pelota la tiene el consolidador.
        /// </summary>
        public int ObservadasCount { get; set; }

        // ── Lo que Tesorería observó ─────────────────────────────────────
        /// <summary>Motivo con el que se devolvió el consolidado. Null si no está observado.</summary>
        public string? ObservacionReembolso { get; set; }
        public DateTimeOffset? ObservadoAt { get; set; }
        public string? ObservadoPor { get; set; }

        // ── Trazabilidad de Tesorería ────────────────────────────────────
        public DateTimeOffset? RevisionTesoreriaAt { get; set; }
        public string? RevisionTesoreriaPor { get; set; }
        public DateTimeOffset? PagadoAt { get; set; }
        public string? PagadoPor { get; set; }
    }

    /// <summary>Un jefe que firmó alguna de las planillas del consolidado, con cuándo lo hizo.</summary>
    public class FirmaJefaturaDto
    {
        public string Nombre { get; set; } = string.Empty;
        public DateTimeOffset? FirmadoAt { get; set; }
    }

    /// <summary>
    /// Una planilla cubierta por el consolidado, con lo que Tesorería necesita de ella: su PDF
    /// firmado, quién la firmó y cuánto suma.
    /// </summary>
    public class ReembolsoPlanillaDto
    {
        public int Id { get; set; }
        /// <summary>Código REN-AAAA-NNNN.</summary>
        public string Codigo { get; set; } = string.Empty;

        /// <summary>
        /// false = el consolidado la cubre pero ninguna de sus salidas llegó todavía a Tesorería
        /// (sigue esperando la firma de su jefatura). Se lista igual, con su código y su monto,
        /// porque el importe declarado en el S10 la incluye y sin ella el total no cuadraría.
        /// </summary>
        public bool EnBandeja { get; set; }

        /// <summary>Monto de la planilla COMPLETA: es lo que suma contra el importe del S10.</summary>
        public decimal MontoTotalPlanilla { get; set; }

        // Lo de abajo solo viene en las que ya están en la bandeja.
        public string? NumeroPlanilla { get; set; }
        public string? Periodo { get; set; }
        public List<string> Trabajadores { get; set; } = new();
        public int SalidasCount { get; set; }
        /// <summary>Suma de las salidas de esta planilla que llegaron a Tesorería.</summary>
        public decimal Monto { get; set; }
        public string? EstadoReembolso { get; set; }
        public string? PdfUrl { get; set; }
        public string? PdfFilename { get; set; }
        public string? PdfFirmadoUrl { get; set; }
        public string? PdfFirmadoFilename { get; set; }
        public DateTimeOffset? FirmadoAt { get; set; }
        /// <summary>Jefe que firmó ESTA planilla (y con ella el consolidado).</summary>
        public string? FirmadoPor { get; set; }
    }

    /// <summary>Una salida del consolidado, para ver el desglose antes de pagar.</summary>
    public class ReembolsoSalidaDto
    {
        public int Id { get; set; }
        public string? Codigo { get; set; }
        /// <summary>Planilla a la que pertenece, para agrupar las salidas en el detalle.</summary>
        public int RendicionId { get; set; }
        public string Trabajador { get; set; } = string.Empty;
        public string? Area { get; set; }
        public DateOnly FechaSalida { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string? LugarOrigen { get; set; }
        public string? LugarDestino { get; set; }
        public int TrayectosCount { get; set; }
        public decimal Monto { get; set; }
        public string EstadoReembolso { get; set; } = EstadosSalida.Reembolso.NombreFirmado;
        // Los trayectos con sus vouchers ya no viajan acá: se ven en el detalle de la salida (el ojo
        // de la fila), que es el mismo modal de Solicitud de Salidas.
    }

    public class ReembolsoDetalleDto : ReembolsoListItemDto
    {
        /// <summary>Las salidas de todas sus planillas, en orden de planilla y trabajador.</summary>
        public List<ReembolsoSalidaDto> Salidas { get; set; } = new();
    }

    public class ReembolsoFiltersDto
    {
        public int? WorkerId { get; set; }
        /// <summary>
        /// Búsqueda libre de la pantalla (número de reembolso, planilla, código, trabajador o
        /// periodo). Se aplica acá y no en el frontend para que las tarjetas del encabezado cuenten
        /// exactamente lo que muestra la tabla: filtrar del lado del cliente las dejaría contando de más.
        /// </summary>
        public string? Texto { get; set; }
        /// <summary>
        /// "Firmado" | "Proceder con el reembolso" | "Pagado" | "Observado" | null para las cuatro.
        /// Otro valor no aplica a esta bandeja. "Observado" acá significa lo que observó Tesorería:
        /// lo que devolvió la jefatura nunca entra a esta pantalla.
        /// </summary>
        public string? EstadoReembolso { get; set; }
        public int? PeriodoAnio { get; set; }
        public int? PeriodoMes { get; set; }
        public List<int>? FilterAreaScopeIds { get; set; }
    }

    /// <summary>
    /// Números de las tarjetas, contados sobre el conjunto ya filtrado: los tres pasos de
    /// Tesorería en el orden de su flujo.
    /// </summary>
    public class ResumenReembolsosDto
    {
        /// <summary>Consolidados firmados esperando la revisión documental de Tesorería.</summary>
        public int PorRevisar { get; set; }
        /// <summary>Consolidados ya confirmados y listos para pagar.</summary>
        public int PorPagar { get; set; }
        /// <summary>Suma a desembolsar de los consolidados listos para pagar.</summary>
        public decimal MontoPorPagar { get; set; }
        /// <summary>Consolidados que Tesorería devolvió y esperan la subsanación (RG-49).</summary>
        public int Observadas { get; set; }
        /// <summary>Consolidados ya completamente pagados.</summary>
        public int Pagadas { get; set; }

        public static ResumenReembolsosDto De(IEnumerable<ReembolsoListItemDto> consolidados)
        {
            var lista = consolidados as ICollection<ReembolsoListItemDto> ?? consolidados.ToList();

            // Las cuatro situaciones son excluyentes y se evalúan en el orden del flujo: lo
            // observado se saca primero porque un consolidado devuelto no está "por revisar" ni
            // "pagado" aunque sus contadores de Tesorería estén en cero.
            bool Observado(ReembolsoListItemDto x) => x.ObservadasCount > 0;

            return new ResumenReembolsosDto
            {
                PorRevisar    = lista.Count(x => !Observado(x) && x.PorConfirmarCount > 0),
                PorPagar      = lista.Count(x => !Observado(x) && x.PorConfirmarCount == 0 && x.PorPagarCount > 0),
                MontoPorPagar = lista.Where(x => !Observado(x) && x.PorConfirmarCount == 0 && x.PorPagarCount > 0)
                                     .Sum(x => x.MontoTotal),
                Observadas    = lista.Count(Observado),
                Pagadas       = lista.Count(x => !Observado(x) && x.PorConfirmarCount == 0 && x.PorPagarCount == 0),
            };
        }
    }

    public class ReembolsoListResultDto
    {
        public List<ReembolsoListItemDto> Data { get; set; } = new();
        public ResumenReembolsosDto Resumen { get; set; } = new();
    }

    public class ReembolsoFilterDataDto
    {
        public List<TrabajadorOptionDto> Trabajadores { get; set; } = new();
        public List<AreaNodeDto> AreaTree { get; set; } = new();
        public List<PeriodoReembolsoOptionDto> Periodos { get; set; } = new();
    }

    public class PeriodoReembolsoOptionDto
    {
        public int Anio { get; set; }
        public int Mes { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>
    /// Consolidados del S10 sobre los que actúa Tesorería. Lo usan sus tres acciones —confirmar la
    /// revisión, observar y pagar— porque las tres operan sobre la misma selección, y las tres
    /// alcanzan al documento entero: el servidor resuelve sus salidas y recorta por estado.
    /// </summary>
    public class ReembolsoSeleccionDto
    {
        public List<int> ConsolidadoIds { get; set; } = new();
    }

    /// <summary>
    /// Lo mismo, más el motivo obligatorio con el que Tesorería devuelve el consolidado (RG-49).
    /// El motivo es lo único que el consolidador va a leer para saber qué corregir, así que sin él
    /// la acción se rechaza con 400.
    /// </summary>
    public class ReembolsoObservacionDto : ReembolsoSeleccionDto
    {
        public string? Observacion { get; set; }
    }

    /// <summary>
    /// Lo que necesita el aviso a Tesorería de que confirmó la revisión: los consolidados que
    /// quedaron listos para pagar —uno por correo— y su destinatario principal, el rol TESORERO.
    /// Van juntos porque se resuelven con el mismo contexto.
    ///
    /// Los demás destinatarios NO salen de acá: son los de Reembolsos → Configuración → Correos y
    /// los agrega el resolver al enviar. Este DTO solo trae el principal.
    /// </summary>
    public class ReembolsoPorPagarCorreoInfoDto
    {
        public List<ConsolidadoPorPagarCorreoDatos> Consolidados { get; set; } = new();
        /// <summary>Correos corporativos de quien tiene el rol TESORERO. Vacío si no lo tiene nadie.</summary>
        public List<string> Destinatarios { get; set; } = new();
    }

    // ── Seguimiento (11.4 del requerimiento) ─────────────────────────────────
    // La segunda vista de Tesorería: no es una bandeja de trabajo sino la consulta de lo ya
    // abonado, con filtro por colaborador. Por eso solo mira lo Pagado y se agrupa por persona
    // y no por documento: a Tesorería le interesa a quién le pagó cuánto.

    /// <summary>Una rendición pagada dentro del seguimiento de un colaborador.</summary>
    public class SeguimientoRendicionDto
    {
        public int RendicionId { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string? NumeroPlanilla { get; set; }
        public string Periodo { get; set; } = string.Empty;
        public int PeriodoAnio { get; set; }
        public int PeriodoMes { get; set; }
        /// <summary>Número de reembolso del Consolidado del S10. Null en las planillas viejas.</summary>
        public string? NumeroReembolso { get; set; }
        public int SalidasCount { get; set; }
        /// <summary>Lo abonado a ESTE colaborador por esta planilla.</summary>
        public decimal MontoAbonado { get; set; }
        public string Estado { get; set; } = EstadosSalida.Reembolso.NombrePagado;
        /// <summary>Última actualización: el pago más reciente de sus salidas en la planilla.</summary>
        public DateTimeOffset? ActualizadoAt { get; set; }
        public string? PagadoPor { get; set; }
        public string? PdfFirmadoUrl { get; set; }
        public string? ConsolidadoS10Url { get; set; }
    }

    /// <summary>Un colaborador con lo que Tesorería ya le abonó.</summary>
    public class SeguimientoColaboradorDto
    {
        public int WorkerId { get; set; }
        public string Trabajador { get; set; } = string.Empty;
        public string? Area { get; set; }
        public decimal TotalAbonado { get; set; }
        public int RendicionesPagadas { get; set; }
        public DateTimeOffset? UltimoPagoAt { get; set; }
        public List<SeguimientoRendicionDto> Rendiciones { get; set; } = new();
    }

    /// <summary>
    /// El seguimiento completo: los colaboradores con pagos (ya filtrados) y los totales de ese
    /// mismo conjunto, para que la pantalla no tenga que sumarlos por su cuenta.
    /// </summary>
    public class ReembolsoSeguimientoDto
    {
        public List<SeguimientoColaboradorDto> Colaboradores { get; set; } = new();
        public decimal TotalAbonado { get; set; }
        public int RendicionesPagadas { get; set; }
        public int ColaboradoresCount { get; set; }
    }
}
