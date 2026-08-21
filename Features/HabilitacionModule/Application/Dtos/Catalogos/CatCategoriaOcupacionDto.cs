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
        /// <summary>Categoría a la que pertenece el puesto. Obligatoria: es de acá de donde
        /// sale la categoría de un trabajador.</summary>
        public int CategoriaId { get; set; }
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
        /// <summary>Categoría a la que pertenece el puesto. Obligatoria: es de acá de donde
        /// sale la categoría de un trabajador.</summary>
        public int CategoriaId { get; set; }
        public string? CategoriaNombre { get; set; }
        public int Orden { get; set; }
        public bool Activo { get; set; }

        /// <summary>
        /// Fichas de <c>workers</c> que apuntan a este puesto. Es lo que decide si el
        /// puesto se puede eliminar: un puesto en uso solo se puede desactivar.
        /// Solo lo llenan los endpoints de listado (admin); en las respuestas de
        /// alta/edición va en 0.
        /// </summary>
        public int CantidadTrabajadores { get; set; }
    }

    /// <summary>
    /// Fila del detalle "trabajadores de este puesto". Es una ficha de <c>workers</c>, no
    /// una persona: quien reingresó tiene más de una ficha y puede aparecer dos veces si
    /// ambas apuntan al mismo puesto — a propósito, para que la lista cuadre con el
    /// conteo de <see cref="PuestoAdminDto.CantidadTrabajadores"/>, que es el que decide
    /// si el puesto se puede eliminar.
    /// </summary>
    public class PuestoTrabajadorDto
    {
        /// <summary>Id de la ficha en <c>workers</c> (no el de <c>person</c>).</summary>
        public int WorkerId { get; set; }
        public string NombreCompleto { get; set; } = "";
        /// <summary>Correo @abril.pe (<c>workers.email_corporativo</c>); null si no tiene.</summary>
        public string? EmailCorporativo { get; set; }
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
        /// <summary>Categoría del puesto. Obligatoria — se recibe como nullable solo para
        /// poder devolver un 400 legible cuando el formulario no la manda, en vez de dejar
        /// que el binder la convierta en 0.</summary>
        public int? CategoriaId { get; set; }
    }

    public class CatToggleRequest
    {
        public bool Activo { get; set; }
    }

    /// <summary>Eliminación en bloque de puestos desde la selección de la tabla.</summary>
    public class PuestosEliminarRequest
    {
        public List<int> Ids { get; set; } = new();
    }

    /// <summary>
    /// Resultado de la eliminación en bloque. Es tolerante a propósito: si algún puesto
    /// de la selección quedó en uso entre la carga de la pantalla y el envío, se omite
    /// ese y se eliminan los demás en vez de fallar el lote entero.
    /// </summary>
    public class PuestosEliminarResultDto
    {
        public int Eliminados { get; set; }
        /// <summary>Seleccionados que no se eliminaron por tener trabajadores usándolos.</summary>
        public int Omitidos { get; set; }
    }
}
