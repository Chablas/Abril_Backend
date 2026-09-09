namespace Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Application.Dtos
{
    // ─── Materiales (catálogo único: Tambor Retráctil, Freno de Cuerda, etc.) ─────

    public class ActivoRotativoMaterialDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public int Orden { get; set; }
        public bool Activo { get; set; }
        public int TotalActivos { get; set; }

        // Control S10: null si no está vinculado a un ítem de Presupuesto Materiales.
        public int? PresupuestoItemId { get; set; }
        public string? PresupuestoItemNombre { get; set; }
        public decimal? CantidadCompradaS10 { get; set; }
        public int CantidadRegistrada { get; set; }
    }

    public class ActivoRotativoMaterialUpsertDto
    {
        public string Nombre { get; set; } = null!;
        public int Orden { get; set; } = 0;
        public int? PresupuestoItemId { get; set; }
    }

    // Para el buscador de ítems del catálogo de Presupuesto Materiales, sin
    // depender del feature "ssoma.gestion.presupuesto-materiales".
    public class PresupuestoItemBuscarDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string? NombreFamilia { get; set; }
    }

    // Coordinador SSOMA / Prevencionista de Abril (staff propio, no de contratista)
    // para el selector de "Responsable / contacto" de un activo.
    public class ResponsableSsomaDto
    {
        public string Nombre { get; set; } = null!;
        public string? Email { get; set; }
    }

    // ─── Activos ─────────────────────────────────────────────────────────────────

    public class ActivoRotativoListDto
    {
        public int Id { get; set; }
        public int MaterialId { get; set; }
        public string MaterialNombre { get; set; } = null!;
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
        public int MaterialId { get; set; }
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
