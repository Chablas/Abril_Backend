namespace Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Application.Dtos
{
    // ─── Categorías ──────────────────────────────────────────────────────────────

    public class ActivoRotativoCategoriaDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public int Orden { get; set; }
        public bool Activo { get; set; }
        public int TotalActivos { get; set; }
    }

    public class ActivoRotativoCategoriaUpsertDto
    {
        public string Nombre { get; set; } = null!;
        public int Orden { get; set; } = 0;
    }

    // ─── Activos ─────────────────────────────────────────────────────────────────

    public class ActivoRotativoListDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public int CategoriaId { get; set; }
        public string CategoriaNombre { get; set; } = null!;
        public string? Codigo { get; set; }
        public string Estado { get; set; } = null!;
        public int? ProyectoActualId { get; set; }
        public string? ProyectoActualNombre { get; set; }
        public string? ResponsableNombre { get; set; }
        public string? ResponsableTelefono { get; set; }
        public bool Activo { get; set; }
    }

    public class ActivoRotativoDetalleDto : ActivoRotativoListDto
    {
        public string? Observaciones { get; set; }
        public List<ActivoRotativoMovimientoDto> Historial { get; set; } = new();
    }

    public class ActivoRotativoMovimientoDto
    {
        public int Id { get; set; }
        public string? ProyectoOrigenNombre { get; set; }
        public string? ProyectoDestinoNombre { get; set; }
        public DateTimeOffset FechaMovimiento { get; set; }
        public string? MovidoPor { get; set; }
        public string? Observacion { get; set; }
    }

    public class ActivoRotativoUpsertDto
    {
        public string Nombre { get; set; } = null!;
        public int CategoriaId { get; set; }
        public string? Codigo { get; set; }
        public string Estado { get; set; } = "disponible";
        public int? ProyectoActualId { get; set; }
        public string? ResponsableNombre { get; set; }
        public string? ResponsableTelefono { get; set; }
        public string? Observaciones { get; set; }
    }

    // Traspaso: null = pasa a almacén central
    public class ActivoRotativoMoverDto
    {
        public int? NuevoProyectoId { get; set; }
        public string? Observacion { get; set; }
    }
}
