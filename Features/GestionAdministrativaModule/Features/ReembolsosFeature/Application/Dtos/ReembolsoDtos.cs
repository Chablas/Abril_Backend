using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;

namespace Abril_Backend.Features.GestionAdministrativa.Reembolsos.Application.Dtos
{
    /// <summary>
    /// Una planilla en la bandeja de Tesorería: ya firmada por la jefatura y esperando la revisión
    /// documental, ya confirmada y esperando el pago, o ya pagada. Tesorería ve TODA la
    /// organización —su recorte es por estado, no por área— así que acá no hay filtro de
    /// visibilidad como en las otras pantallas de salidas.
    /// </summary>
    public class ReembolsoListItemDto
    {
        public int Id { get; set; }
        /// <summary>Código REN-AAAA-NNNN — es como el trabajador la nombra en los correos.</summary>
        public string Codigo { get; set; } = string.Empty;
        public string? NumeroPlanilla { get; set; }
        public DateTimeOffset RendidoAt { get; set; }

        public string Periodo { get; set; } = string.Empty;
        public int PeriodoAnio { get; set; }
        public int PeriodoMes { get; set; }

        public List<string> Trabajadores { get; set; } = new();
        public int SalidasCount { get; set; }
        /// <summary>Lo que hay que reembolsar por esta planilla.</summary>
        public decimal MontoTotal { get; set; }

        // ── Documentos que Tesorería necesita ver antes de pagar ─────────
        public string PdfUrl { get; set; } = string.Empty;
        public string PdfFilename { get; set; } = string.Empty;
        /// <summary>Copia firmada por la jefatura: es el respaldo del pago.</summary>
        public string? PdfFirmadoUrl { get; set; }
        public string? PdfFirmadoFilename { get; set; }
        public DateTimeOffset? FirmadoAt { get; set; }
        /// <summary>
        /// Nombre del jefe que firmó. Junto con <see cref="FirmadoAt"/> es la "firma electrónica"
        /// que Tesorería tiene que ver antes de proceder (RG-24 / RF-TES-02).
        /// </summary>
        public string? FirmadoPor { get; set; }
        public ConsolidadoS10Dto? ConsolidadoS10 { get; set; }

        /// <summary>"Firmado", "Proceder con el reembolso" o "Pagado" (el más atrasado si la planilla trae de varios).</summary>
        public string EstadoReembolso { get; set; } = EstadosSalida.Reembolso.NombreFirmado;
        public bool ReembolsoMixto { get; set; }

        /// <summary>
        /// Salidas firmadas y sin confirmar: es lo que se marca al Confirmar revisión. Mientras
        /// sea &gt; 0 la planilla espera a Tesorería y todavía NO se puede pagar (RG-26).
        /// </summary>
        public int PorConfirmarCount { get; set; }
        /// <summary>Salidas ya confirmadas y sin pagar: es lo que se paga al marcar la planilla.</summary>
        public int PorPagarCount { get; set; }

        // ── Trazabilidad de Tesorería ────────────────────────────────────
        public DateTimeOffset? RevisionTesoreriaAt { get; set; }
        public string? RevisionTesoreriaPor { get; set; }
        public DateTimeOffset? PagadoAt { get; set; }
        public string? PagadoPor { get; set; }
    }

    /// <summary>Una captura de movilidad (el voucher) de un tramo, para verla antes de pagar.</summary>
    public class ReembolsoCapturaDto
    {
        public int Id { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
        public decimal Monto { get; set; }
    }

    /// <summary>Documento adjunto de un tramo (los motivos que exigen sustento documental).</summary>
    public class ReembolsoAdjuntoDto
    {
        public string Url { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
    }

    /// <summary>
    /// Un tramo de una salida rendida, con lo que el requerimiento le pide mostrar a Tesorería:
    /// fecha, motivo, origen, destino, horario, monto y los adjuntos asociados (RF-TES-05,
    /// RG-32). El monto sale de la misma regla que imprime la columna IMPORTE de la planilla.
    /// </summary>
    public class ReembolsoTramoDto
    {
        public int Id { get; set; }
        public int Orden { get; set; }
        /// <summary>Null en los motivos que no piden horario.</summary>
        public string? HoraSalida { get; set; }
        public string? HoraRetorno { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string? LugarOrigen { get; set; }
        public string? LugarDestino { get; set; }
        public decimal Monto { get; set; }
        /// <summary>
        /// true si el monto salió del catálogo <c>ga_trayecto</c> (tarifario de TI) y no de
        /// capturas: sin ese aviso, un tramo con importe y sin voucher parece un sustento perdido.
        /// </summary>
        public bool MontoDeCatalogo { get; set; }
        public List<ReembolsoCapturaDto> Capturas { get; set; } = new();
        public List<ReembolsoAdjuntoDto> Adjuntos { get; set; } = new();
    }

    /// <summary>Una salida de la planilla, para ver el desglose antes de pagar.</summary>
    public class ReembolsoSalidaDto
    {
        public int Id { get; set; }
        public string? Codigo { get; set; }
        public string Trabajador { get; set; } = string.Empty;
        public string? Area { get; set; }
        public DateOnly FechaSalida { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string? LugarOrigen { get; set; }
        public string? LugarDestino { get; set; }
        public int TrayectosCount { get; set; }
        public decimal Monto { get; set; }
        public string EstadoReembolso { get; set; } = EstadosSalida.Reembolso.NombreFirmado;
        /// <summary>Los tramos que componen la salida, con sus vouchers.</summary>
        public List<ReembolsoTramoDto> Tramos { get; set; } = new();
    }

    public class ReembolsoDetalleDto : ReembolsoListItemDto
    {
        public List<ReembolsoSalidaDto> Salidas { get; set; } = new();
    }

    public class ReembolsoFiltersDto
    {
        public int? WorkerId { get; set; }
        /// <summary>
        /// Búsqueda libre de la pantalla (planilla, código, trabajador, guía o periodo). Se aplica
        /// acá y no en el frontend para que las tarjetas del encabezado cuenten exactamente lo que
        /// muestra la tabla: filtrar del lado del cliente las dejaría contando de más.
        /// </summary>
        public string? Texto { get; set; }
        /// <summary>
        /// "Firmado" | "Proceder con el reembolso" | "Pagado" | null para las tres. Otro valor no
        /// aplica a esta bandeja.
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
        /// <summary>Planillas firmadas esperando la revisión documental de Tesorería.</summary>
        public int PorRevisar { get; set; }
        /// <summary>Planillas ya confirmadas y listas para pagar.</summary>
        public int PorPagar { get; set; }
        /// <summary>Suma a desembolsar de las planillas listas para pagar.</summary>
        public decimal MontoPorPagar { get; set; }
        /// <summary>Planillas ya completamente pagadas.</summary>
        public int Pagadas { get; set; }

        public static ResumenReembolsosDto De(IEnumerable<ReembolsoListItemDto> planillas)
        {
            var lista = planillas as ICollection<ReembolsoListItemDto> ?? planillas.ToList();
            return new ResumenReembolsosDto
            {
                PorRevisar    = lista.Count(x => x.PorConfirmarCount > 0),
                PorPagar      = lista.Count(x => x.PorConfirmarCount == 0 && x.PorPagarCount > 0),
                MontoPorPagar = lista.Where(x => x.PorConfirmarCount == 0 && x.PorPagarCount > 0)
                                     .Sum(x => x.MontoTotal),
                Pagadas       = lista.Count(x => x.PorConfirmarCount == 0 && x.PorPagarCount == 0),
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
    /// Planillas (o salidas sueltas) sobre las que actúa Tesorería. Lo usan sus dos acciones
    /// —confirmar la revisión y pagar— porque las dos operan sobre la misma selección.
    /// </summary>
    public class ReembolsoSeleccionDto
    {
        public List<int> RendicionIds { get; set; } = new();
        public List<int> SolicitudIds { get; set; } = new();
    }

    // ── Seguimiento (11.4 del requerimiento) ─────────────────────────────────
    // La segunda vista de Tesorería: no es una bandeja de trabajo sino la consulta de lo ya
    // abonado, con filtro por colaborador. Por eso solo mira lo Pagado y se agrupa por persona
    // y no por planilla: una planilla puede cubrir a varios y a Tesorería le interesa a quién
    // le pagó cuánto.

    /// <summary>Una rendición pagada dentro del seguimiento de un colaborador.</summary>
    public class SeguimientoRendicionDto
    {
        public int RendicionId { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string? NumeroPlanilla { get; set; }
        public string Periodo { get; set; } = string.Empty;
        public int PeriodoAnio { get; set; }
        public int PeriodoMes { get; set; }
        /// <summary>Número de guía del Consolidado del S10. Null en las planillas viejas.</summary>
        public string? NumeroGuia { get; set; }
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
