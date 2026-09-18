namespace Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Models
{
    // Categoría del EPP (Cabeza, Manos, Ojos, Altura, etc.)
    public class SsEppCategoria
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public int Orden { get; set; }
        public bool Activo { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        public ICollection<SsEppFamilia> Familias { get; set; } = new List<SsEppFamilia>();
    }

    // Familia de EPP (ej. "Barbiquejo", "Casco de seguridad") — agrupa las variantes/ítems
    // concretos que comparten el mismo propósito (Barbiquejo 2 puntas, Barbiquejo 4 puntas).
    public class SsEppFamilia
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public int CategoriaId { get; set; }
        public int Orden { get; set; }
        public bool Activo { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        public SsEppCategoria? Categoria { get; set; }
        public ICollection<SsEppItem> Items { get; set; } = new List<SsEppItem>();
    }

    // Ítem/variante de EPP dentro de una familia (ej. "Barbiquejo 2 puntas"), con ficha
    // técnica propia. NombreTecnico es el nombre normativo/SSOMA; NombreComercial es como lo
    // maneja Logística con el proveedor. La imagen identifica visualmente la variante.
    public class SsEppItem
    {
        public int Id { get; set; }
        public string NombreTecnico { get; set; } = null!;
        public string NombreComercial { get; set; } = null!;
        public int FamiliaId { get; set; }
        public string? Descripcion { get; set; }
        public string? ImagenUrl { get; set; }
        public string? FichaTecnicaUrl { get; set; }
        public string? FichaTecnicaNombreArchivo { get; set; }
        public bool Activo { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public int? CreatedById { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public int? UpdatedById { get; set; }

        public SsEppFamilia? Familia { get; set; }
        public ICollection<SsEppModelo> Modelos { get; set; } = new List<SsEppModelo>();
    }

    // Modelo/marca autorizada para un ítem de EPP (varios por ítem: distintos proveedores/marcas).
    public class SsEppModelo
    {
        public int Id { get; set; }
        public int EppItemId { get; set; }
        public string Marca { get; set; } = null!;
        public string Modelo { get; set; } = null!;
        public string? CodigoReferencia { get; set; }
        public string? ImagenUrl { get; set; }
        public bool Activo { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public int? CreatedById { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }
        public int? UpdatedById { get; set; }

        public SsEppItem? EppItem { get; set; }
    }

    // Bitácora de auditoría: quién y cuándo creó/editó/desactivó un ítem o modelo.
    public class SsEppAuditoria
    {
        public int Id { get; set; }
        /// <summary>"Item" | "Modelo"</summary>
        public string EntidadTipo { get; set; } = null!;
        public int EntidadId { get; set; }
        /// <summary>"Creado" | "Editado" | "Activado" | "Desactivado"</summary>
        public string Accion { get; set; } = null!;
        public string? Detalle { get; set; }
        public int? UsuarioId { get; set; }
        public DateTimeOffset Fecha { get; set; }
    }
}
