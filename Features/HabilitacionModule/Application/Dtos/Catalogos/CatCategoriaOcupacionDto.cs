namespace Abril_Backend.Features.Habilitacion.Application.Dtos.Catalogos
{
    /// <summary>Ítem del desplegable de categorías (el campo de lógica).</summary>
    public class CatCategoriaDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }

    /// <summary>
    /// Ítem del desplegable de puestos (el campo de presentación). Lleva su
    /// <see cref="CategoriaId"/> para que el formulario pueda filtrar los puestos
    /// por la categoría elegida sin volver al servidor.
    /// </summary>
    public class PuestoDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int? CategoriaId { get; set; }
    }

    public class CatCategoriaAdminDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public int Orden { get; set; }
        public bool Activo { get; set; }
    }

    public class PuestoAdminDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public int? CategoriaId { get; set; }
        public string? CategoriaNombre { get; set; }
        public int Orden { get; set; }
        public bool Activo { get; set; }
    }

    /// <summary>
    /// Carga inicial de la pantalla de Configuración → Categorías y Puestos: las dos
    /// listas en una sola respuesta. La pantalla necesita ambas de entrada (los puestos
    /// muestran y eligen su categoría), así que se sirven juntas en vez de con dos GET.
    /// </summary>
    public class CatalogosAdminDto
    {
        public List<CatCategoriaAdminDto> Categorias { get; set; } = new();
        public List<PuestoAdminDto> Puestos { get; set; } = new();
    }

    public class CatNombreRequest
    {
        public string Nombre { get; set; } = "";
    }

    /// <summary>Alta/edición de un puesto: nombre + la categoría a la que pertenece.</summary>
    public class PuestoUpsertRequest
    {
        public string Nombre { get; set; } = "";
        public int? CategoriaId { get; set; }
    }

    public class CatToggleRequest
    {
        public bool Activo { get; set; }
    }
}
