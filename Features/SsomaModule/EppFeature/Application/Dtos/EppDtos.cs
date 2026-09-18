namespace Abril_Backend.Features.SsomaModule.EppFeature.Application.Dtos
{
    // ─── Categorías ─────────────────────────────────────────────────────────────

    public class EppCategoriaDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public int Orden { get; set; }
        public bool Activo { get; set; }
        public int TotalItems { get; set; }
    }

    public class EppCategoriaUpsertDto
    {
        public string Nombre { get; set; } = null!;
        public int Orden { get; set; } = 0;
    }

    // ─── Familias ───────────────────────────────────────────────────────────────

    public class EppFamiliaDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public int CategoriaId { get; set; }
        public string CategoriaNombre { get; set; } = null!;
        public int Orden { get; set; }
        public bool Activo { get; set; }
        public int TotalItems { get; set; }
    }

    public class EppFamiliaUpsertDto
    {
        public string Nombre { get; set; } = null!;
        public int CategoriaId { get; set; }
        public int Orden { get; set; } = 0;
    }

    // ─── Ítems ──────────────────────────────────────────────────────────────────

    public class EppModeloDto
    {
        public int Id { get; set; }
        public string Marca { get; set; } = null!;
        public string Modelo { get; set; } = null!;
        public string? CodigoReferencia { get; set; }
        public string? ImagenUrl { get; set; }
        public bool Activo { get; set; }
    }

    public class EppItemListDto
    {
        public int Id { get; set; }
        public string NombreTecnico { get; set; } = null!;
        public string NombreComercial { get; set; } = null!;
        public int FamiliaId { get; set; }
        public string FamiliaNombre { get; set; } = null!;
        public int CategoriaId { get; set; }
        public string CategoriaNombre { get; set; } = null!;
        public string? ImagenUrl { get; set; }
        public string? FichaTecnicaUrl { get; set; }
        public string? FichaTecnicaNombreArchivo { get; set; }
        public bool Activo { get; set; }
        public int TotalModelos { get; set; }
        public List<EppModeloDto> Modelos { get; set; } = new();
    }

    public class EppItemDetalleDto : EppItemListDto
    {
        public string? Descripcion { get; set; }
        public List<EppAuditoriaDto> Auditoria { get; set; } = new();
    }

    public class EppItemUpsertDto
    {
        public string NombreTecnico { get; set; } = null!;
        public string NombreComercial { get; set; } = null!;
        public int FamiliaId { get; set; }
        public string? Descripcion { get; set; }
    }

    public class EppModeloUpsertDto
    {
        public string Marca { get; set; } = null!;
        public string Modelo { get; set; } = null!;
        public string? CodigoReferencia { get; set; }
    }

    // ─── Auditoría ──────────────────────────────────────────────────────────────

    public class EppAuditoriaDto
    {
        public string EntidadTipo { get; set; } = null!;
        public string Accion { get; set; } = null!;
        public string? Detalle { get; set; }
        public string? UsuarioNombre { get; set; }
        public DateTimeOffset Fecha { get; set; }
    }
}
