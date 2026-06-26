namespace Abril_Backend.Features.AccountingModule.Features.Configuration.InvoiceFolderFeature.Application.Dtos
{
    /// <summary>Fila de la tabla de carpetas de facturas configuradas.</summary>
    public class InvoiceFolderDto
    {
        public int InvoiceFolderId { get; set; }
        public string Name { get; set; } = null!;
        public string LinkUrl { get; set; } = null!;
        public string DriveId { get; set; } = null!;
        public string FolderId { get; set; } = null!;
        public string? FolderName { get; set; }
        public string? WebUrl { get; set; }
        public bool Active { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
    }

    public class InvoiceFolderCreateDto
    {
        /// <summary>Nombre identificativo de la carpeta (p. ej. "Facturas urgentes").</summary>
        public string Name { get; set; } = null!;
        public string LinkUrl { get; set; } = null!;
        /// <summary>Carpeta seleccionada en el navegador (debe estar dentro del drive del link).</summary>
        public string DriveId { get; set; } = null!;
        public string FolderId { get; set; } = null!;
    }

    public class InvoiceFolderUpdateDto
    {
        public int InvoiceFolderId { get; set; }
        public string Name { get; set; } = null!;
        public string LinkUrl { get; set; } = null!;
        public string DriveId { get; set; } = null!;
        public string FolderId { get; set; } = null!;
        public bool Active { get; set; }
    }

    // ── Navegador de carpetas ────────────────────────────────────────────────
    public class FolderItemDto
    {
        public string Id { get; set; } = null!;
        public string Name { get; set; } = null!;
    }

    public class ResolveLinkRequestDto
    {
        public string LinkUrl { get; set; } = null!;
    }

    public class FolderBrowseDto
    {
        public string DriveId { get; set; } = null!;
        public string FolderId { get; set; } = null!;
        public string? FolderName { get; set; }
        public List<FolderItemDto> Folders { get; set; } = new();
    }

    public class InvoiceFolderFilterDto
    {
        public int Page { get; set; } = 1;
    }

    /// <summary>Opción para el desplegable "Carpeta a guardar" del formulario de factura.</summary>
    public class InvoiceFolderOptionDto
    {
        public int InvoiceFolderId { get; set; }
        public string Name { get; set; } = null!;
    }
}
