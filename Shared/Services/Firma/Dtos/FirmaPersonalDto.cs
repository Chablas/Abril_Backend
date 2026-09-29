namespace Abril_Backend.Shared.Services.Firma.Dtos
{
    /// <summary>
    /// Una firma ya registrada por la persona. La imagen se entrega como data URL para mostrarla
    /// directo en un <c>&lt;img src&gt;</c>.
    /// </summary>
    public class FirmaPersonalDto
    {
        /// <summary>DIBUJO | IMAGEN (<see cref="Abril_Backend.Shared.Models.FirmaTipo"/>).</summary>
        public string Tipo { get; set; } = null!;

        /// <summary>data:image/png;base64,… para usar directamente en un &lt;img src&gt;.</summary>
        public string ImageDataUrl { get; set; } = null!;

        public DateTime? UpdatedDateTime { get; set; }
    }

    /// <summary>Un tipo de firma del catálogo y si hoy se ofrece al firmar.</summary>
    public class FirmaTipoDto
    {
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;

        /// <summary>
        /// Los checkboxes de Consolidados → Configuración → Firmas. Quien firma un consolidado tiene
        /// que tener registrada una firma de alguno de los tipos activos.
        /// </summary>
        public bool Activo { get; set; }
    }

    /// <summary>
    /// Todo lo que una pantalla necesita para pedir (o no pedir) una firma: qué tipos se ofrecen y
    /// cuáles tiene ya registrados el usuario.
    ///
    /// Va junto en una sola respuesta a propósito: el modal que salta al aprobar un consolidado
    /// necesita las dos cosas a la vez para decidir si muestra el lienzo, el selector de imagen o
    /// ambos, y pedirlas por separado serían dos roundtrips para dibujar una pantalla.
    /// </summary>
    public class FirmaPersonalEstadoDto
    {
        /// <summary>Catálogo completo, en orden de visualización, con su bandera de habilitado.</summary>
        public List<FirmaTipoDto> Tipos { get; set; } = new();

        /// <summary>Lo que el usuario ya registró (vacío si todavía no registró ninguna).</summary>
        public List<FirmaPersonalDto> Firmas { get; set; } = new();
    }

    /// <summary>Datos para guardar una firma.</summary>
    public class FirmaPersonalSaveDto
    {
        /// <summary>
        /// DIBUJO | IMAGEN. Sin valor se asume DIBUJO, que es lo único que existía antes y lo único
        /// que sigue mandando Contabilidad → Firma.
        /// </summary>
        public string? Tipo { get; set; }

        /// <summary>
        /// data:image/png;base64,… del canvas, o el data URL del archivo que el usuario subió
        /// (PNG/JPG/WEBP: el backend lo normaliza a PNG).
        /// </summary>
        public string ImageBase64 { get; set; } = null!;
    }

    /// <summary>Los dos checkboxes de Consolidados → Configuración → Firmas.</summary>
    public class FirmaTiposSaveDto
    {
        public List<FirmaTipoActivoDto> Tipos { get; set; } = new();
    }

    /// <summary>Un tipo y si queda habilitado.</summary>
    public class FirmaTipoActivoDto
    {
        public string Codigo { get; set; } = null!;
        public bool Activo { get; set; }
    }
}
