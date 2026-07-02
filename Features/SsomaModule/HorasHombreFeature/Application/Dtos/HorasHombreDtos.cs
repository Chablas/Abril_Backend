namespace Abril_Backend.Features.SsomaModule.HorasHombreFeature.Application.Dtos
{
    public class HorasHombreFilterDto
    {
        public int? ProyectoId { get; set; }
        public int? EmpresaId { get; set; }
        public DateOnly? FechaDesde { get; set; }
        public DateOnly? FechaHasta { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class HorasHombreDiaDto
    {
        public DateOnly Fecha { get; set; }
        public int ProyectoId { get; set; }
        public string ProyectoNombre { get; set; } = string.Empty;
        public int PersonasCasa { get; set; }
        public int PersonasContratista { get; set; }
        public int TotalPersonas { get; set; }
        public long HorasHombre { get; set; }
    }

    public class HorasHombrePorEmpresaDto
    {
        public int EmpresaId { get; set; }
        public string EmpresaNombre { get; set; } = string.Empty;
        public long HorasHombre { get; set; }
        public long TotalPersonasDia { get; set; }
    }

    public class HorasHombreSerieDiaDto
    {
        public DateOnly Fecha { get; set; }
        public long HorasHombreCasa { get; set; }
        public long HorasHombreContratista { get; set; }
        public long HorasHombreTotal { get; set; }
    }

    public class HorasHombreProyectoResumenDto
    {
        public int ProyectoId { get; set; }
        public string ProyectoNombre { get; set; } = string.Empty;
        public long HorasHombre { get; set; }
    }

    public class HorasHombreDashboardDto
    {
        public int? ProyectoId { get; set; }
        public int? Mes { get; set; }
        public int? Anio { get; set; }
        public long TotalHorasHombre { get; set; }
        public long TotalHorasHombreCasa { get; set; }
        public long TotalHorasHombreContratista { get; set; }
        public int DiasRegistrados { get; set; }
        public double PromedioPersonasPorDia { get; set; }
        public DateOnly? UltimaFechaRegistrada { get; set; }
        public List<HorasHombreSerieDiaDto> SerieDiaria { get; set; } = new();
        public List<HorasHombrePorEmpresaDto> PorEmpresa { get; set; } = new();
        public List<HorasHombreProyectoResumenDto> PorProyecto { get; set; } = new();
    }
}
