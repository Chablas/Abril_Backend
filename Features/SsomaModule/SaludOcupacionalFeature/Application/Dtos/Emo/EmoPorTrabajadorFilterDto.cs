namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Emo
{
    public class EmoPorTrabajadorFilterDto
    {
        public string? Search { get; set; }
        public string? Aptitud { get; set; }
        public string? Estado { get; set; }
        public int? EmpresaId { get; set; }
        public int? ProyectoId { get; set; }

        /// <summary>
        /// Nodo del árbol <c>area_scope</c> por el que se filtra: se incluyen los trabajadores
        /// del nodo y de todos sus descendientes. Reemplaza a los filtros de texto
        /// <see cref="Area"/>/<see cref="Subarea"/>, igual que <c>workers.area_scope_id</c>
        /// reemplazó a <c>workers.area</c>/<c>workers.subarea</c>.
        /// </summary>
        public int? AreaScopeId { get; set; }

        /// <summary>Obsoletos: los reemplaza <see cref="AreaScopeId"/> y ya no se aplican al filtrar.</summary>
        public string? Area { get; set; }
        public string? Subarea { get; set; }
        public DateOnly? FechaEmoDesde { get; set; }
        public DateOnly? FechaEmoHasta { get; set; }
        public bool SinLectura { get; set; }
        public bool SinCertificado { get; set; }
        public bool SinEmoCompleto { get; set; }

        /// <summary>Trabajadores con interconsulta derivada (no cancelada) y sin informe de levantamiento adjunto.</summary>
        public bool SinInterconsulta { get; set; }

        /// <summary>"fechaEmo" | "fechaVencimiento". Cualquier otro valor (o null) ordena por nombre.</summary>
        public string? SortBy { get; set; }
        public bool SortDesc { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}
