namespace Abril_Backend.Features.SsomaModule.CumplimientoSsomaFeature.Application.Dtos
{
    // ─── Catálogo de actividades ─────────────────────────────────────────────────

    public class CumplimientoActividadDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string? Descripcion { get; set; }
        public string RolResponsable { get; set; } = null!;
        public string Frecuencia { get; set; } = null!;
        public int Orden { get; set; }
        public bool Activo { get; set; }
    }

    public class CumplimientoActividadUpsertDto
    {
        public string Nombre { get; set; } = null!;
        public string? Descripcion { get; set; }
        public string RolResponsable { get; set; } = "ambos";
        public string Frecuencia { get; set; } = "diaria";
        public int Orden { get; set; } = 0;
    }

    // ─── Cumplimiento por proyecto/periodo ───────────────────────────────────────

    // Estado de una actividad en el periodo vigente para un proyecto (el registro
    // se crea recién cuando alguien la marca — antes de eso, "pendiente" es solo
    // la ausencia de fila, no algo que haya que sembrar por cron).
    public class CumplimientoItemDto
    {
        public int ActividadId { get; set; }
        public string Nombre { get; set; } = null!;
        public string? Descripcion { get; set; }
        public string RolResponsable { get; set; } = null!;
        public string Frecuencia { get; set; } = null!;
        public DateOnly Periodo { get; set; }
        public bool Cumplido { get; set; }
        public DateTimeOffset? FechaCumplimiento { get; set; }
        public string? CumplidoPor { get; set; }
        public string? Observacion { get; set; }
    }

    public class CumplimientoResumenDto
    {
        public int ProyectoId { get; set; }
        public List<CumplimientoItemDto> Actividades { get; set; } = new();
    }

    public class CumplimientoMarcarDto
    {
        public bool Cumplido { get; set; }
        public string? Observacion { get; set; }
    }
}
