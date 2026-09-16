namespace Abril_Backend.Features.LearningModule.Application.Dtos
{
    // ─────────────────────────── Display (login / inicio) ───────────────────────────

    /// <summary>Video o manual tal como lo consume el frontend para mostrarlo.</summary>
    public class LearningVideoDto
    {
        public string Titulo { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? Img { get; set; }
    }

    /// <summary>Grupo/área con sus videos y manuales, para renderizar agrupado.</summary>
    public class LearningCategoryDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public List<LearningVideoDto> Videos { get; set; } = new();
    }

    // ─────────────────────────────── Admin (CRUD) ───────────────────────────────

    public class LearningVideoAdminDto
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? Img { get; set; }
        public int Orden { get; set; }
        public bool Activo { get; set; }
        /// <summary>Nombre del archivo subido a SharePoint. Null = es un enlace.</summary>
        public string? ArchivoNombre { get; set; }
    }

    public class LearningCategoryAdminDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int Orden { get; set; }
        public int SurfaceId { get; set; }
        public string SurfaceCode { get; set; } = string.Empty;
        public string SurfaceNombre { get; set; } = string.Empty;
        public bool EsPublicoInterno { get; set; }
        public bool Activo { get; set; }
        public List<int> RoleIds { get; set; } = new();
        public List<LearningVideoAdminDto> Videos { get; set; } = new();
    }

    public class LearningSurfaceDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
    }

    public class LearningRoleOptionDto
    {
        public int Id { get; set; }
        public string Descripcion { get; set; } = string.Empty;
    }

    /// <summary>Todo lo que la página de administración necesita en una sola petición.</summary>
    public class LearningAdminDataDto
    {
        public List<LearningCategoryAdminDto> Categorias { get; set; } = new();
        public List<LearningSurfaceDto> Superficies { get; set; } = new();
        public List<LearningRoleOptionDto> Roles { get; set; } = new();
    }

    // ─────────────────────────── Create / Edit payloads ───────────────────────────

    public class LearningCategoryCreateDto
    {
        public string Nombre { get; set; } = string.Empty;
        public int SurfaceId { get; set; }
        public int Orden { get; set; }
        public bool EsPublicoInterno { get; set; }
        public List<int> RoleIds { get; set; } = new();
    }

    public class LearningCategoryEditDto
    {
        public string Nombre { get; set; } = string.Empty;
        public int SurfaceId { get; set; }
        public int Orden { get; set; }
        public bool EsPublicoInterno { get; set; }
        public List<int> RoleIds { get; set; } = new();
    }

    /// <summary>
    /// Alta de un video o manual. Llega como el campo <c>data</c> (JSON) de un multipart; si
    /// <see cref="EsArchivo"/> es true el archivo viene en el campo <c>archivo</c> y <see cref="Url"/>
    /// se ignora.
    /// </summary>
    public class LearningVideoCreateDto
    {
        public int CategoriaId { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? Img { get; set; }
        public int Orden { get; set; }
        public bool EsArchivo { get; set; }
    }

    /// <summary>
    /// Edición de un video o manual (campo <c>data</c> de un multipart). Con <see cref="EsArchivo"/>
    /// en true y sin <c>archivo</c> nuevo se conserva el archivo que ya tenía; en false pasa a ser el
    /// enlace de <see cref="Url"/>.
    /// </summary>
    public class LearningVideoEditDto
    {
        public string Titulo { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string? Img { get; set; }
        public int Orden { get; set; }
        public bool EsArchivo { get; set; }
    }

    // ─────────────────────────── Archivo en SharePoint ───────────────────────────

    /// <summary>Dónde quedó un archivo (video o manual) subido a SharePoint.</summary>
    public class LearningVideoArchivoDto
    {
        /// <summary>
        /// Lo que se guarda como url: el link del reproductor de Stream si es un video, o el del
        /// archivo si es un PDF o una imagen.
        /// </summary>
        public string Url { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string DriveId { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Lo que hay que saber antes de subir un archivo: si el grupo es del login (que no admite
    /// archivos) y el link de la carpeta de <c>learning_video_folder</c>.
    /// </summary>
    public class LearningVideoDestinoDto
    {
        public bool EsLogin { get; set; }
        public string? FolderLink { get; set; }
    }
}
