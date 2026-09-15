namespace Abril_Backend.Features.SsomaModule.ChecklistFeature.Application.Dtos
{
    public class ChecklistPlantillaListDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string? Descripcion { get; set; }
        public string TipoActivacion { get; set; } = null!;
        public string? EventoActivacion { get; set; }
        public bool EsObligatorio { get; set; }
        public int Orden { get; set; }
        public bool Activo { get; set; }
        public int TotalItems { get; set; }
        public int? PartidaId { get; set; }
        public string? PartidaNombre { get; set; }
    }

    public class ChecklistPlantillaDetalleDto : ChecklistPlantillaListDto
    {
        public List<ChecklistPlantillaItemDto> Items { get; set; } = new();
    }

    public class ChecklistPlantillaItemDto
    {
        public int Id { get; set; }
        public string Descripcion { get; set; } = null!;
        public int Orden { get; set; }
        public bool TieneAdjuntoRef { get; set; }
        public bool Activo { get; set; }
        public List<ChecklistItemImagenDto> ImagenesReferencia { get; set; } = new();
    }

    // Foto de referencia ("cómo debe quedar") de un ítem — galería sin límite
    public class ChecklistItemImagenDto
    {
        public int Id { get; set; }
        public string Url { get; set; } = null!;
        public int Orden { get; set; }
    }

    // Para crear/editar plantilla
    public class ChecklistPlantillaUpsertDto
    {
        public string Nombre { get; set; } = null!;
        public string? Descripcion { get; set; }
        public string TipoActivacion { get; set; } = "manual";
        public string? EventoActivacion { get; set; }
        public bool EsObligatorio { get; set; } = false;
        public int Orden { get; set; } = 0;
        public int? PartidaId { get; set; }
    }

    // Para agregar un item nuevo a una plantilla existente
    public class ChecklistPlantillaItemCreateDto
    {
        public string Descripcion { get; set; } = null!;
        public bool TieneAdjuntoRef { get; set; } = false;
    }

    // Para editar un item existente de la plantilla
    public class ChecklistPlantillaItemEditDto
    {
        public string Descripcion { get; set; } = null!;
        public bool TieneAdjuntoRef { get; set; }
        public bool Activo { get; set; }
    }

    // Para reordenar un item (mover arriba/abajo)
    public class ChecklistItemOrdenDto
    {
        public int Orden { get; set; }
    }

    // ─── Partidas (etapas constructivas: Muro Anclado, Excavación, etc.) ────────

    public class ChecklistPartidaDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string? Descripcion { get; set; }
        public int Orden { get; set; }
        public bool Activo { get; set; }
        public int TotalPlantillas { get; set; }
    }

    public class ChecklistPartidaUpsertDto
    {
        public string Nombre { get; set; } = null!;
        public string? Descripcion { get; set; }
        public int Orden { get; set; } = 0;
    }
}
