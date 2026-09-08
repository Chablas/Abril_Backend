using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;

namespace Abril_Backend.Features.GestionAdministrativa.Reembolsos.Application.Dtos
{
    /// <summary>
    /// Una planilla en la bandeja de Tesorería: ya firmada por la jefatura y esperando el pago, o
    /// ya pagada. Tesorería ve TODA la organización —su recorte es por estado, no por área— así
    /// que acá no hay filtro de visibilidad como en las otras pantallas de salidas.
    /// </summary>
    public class ReembolsoListItemDto
    {
        public int Id { get; set; }
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
        public ConsolidadoS10Dto? ConsolidadoS10 { get; set; }

        /// <summary>"Firmado" o "Pagado" (o el estado más atrasado si la planilla trae de los dos).</summary>
        public string EstadoReembolso { get; set; } = EstadosSalida.Reembolso.NombreFirmado;
        public bool ReembolsoMixto { get; set; }

        /// <summary>Salidas firmadas y todavía sin pagar: es lo que se paga al marcar la planilla.</summary>
        public int PorPagarCount { get; set; }
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
    }

    public class ReembolsoDetalleDto : ReembolsoListItemDto
    {
        public List<ReembolsoSalidaDto> Salidas { get; set; } = new();
    }

    public class ReembolsoFiltersDto
    {
        public int? WorkerId { get; set; }
        /// <summary>"Firmado" | "Pagado" | null para las dos. Otro valor no aplica a esta bandeja.</summary>
        public string? EstadoReembolso { get; set; }
        public int? PeriodoAnio { get; set; }
        public int? PeriodoMes { get; set; }
        public List<int>? FilterAreaScopeIds { get; set; }
    }

    /// <summary>
    /// Números de las tarjetas, contados sobre el conjunto ya filtrado: lo que Tesorería tiene por
    /// delante y lo que ya cerró.
    /// </summary>
    public class ResumenReembolsosDto
    {
        /// <summary>Planillas con salidas firmadas y sin pagar.</summary>
        public int PorPagar { get; set; }
        /// <summary>Suma a desembolsar de esas planillas.</summary>
        public decimal MontoPorPagar { get; set; }
        /// <summary>Planillas ya completamente pagadas.</summary>
        public int Pagadas { get; set; }

        public static ResumenReembolsosDto De(IEnumerable<ReembolsoListItemDto> planillas)
        {
            var lista = planillas as ICollection<ReembolsoListItemDto> ?? planillas.ToList();
            return new ResumenReembolsosDto
            {
                PorPagar      = lista.Count(x => x.PorPagarCount > 0),
                MontoPorPagar = lista.Where(x => x.PorPagarCount > 0).Sum(x => x.MontoTotal),
                Pagadas       = lista.Count(x => x.PorPagarCount == 0),
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

    /// <summary>Planillas (o salidas sueltas) que Tesorería marca como pagadas.</summary>
    public class PagarDto
    {
        public List<int> RendicionIds { get; set; } = new();
        public List<int> SolicitudIds { get; set; } = new();
    }
}
