namespace Abril_Backend.Features.SsomaModule.PetsFeature.Application.Dtos;

// "Indice" es la posición ORIGINAL del párrafo en el documento Word (no cambia si
// se filtra o recorta la lista) — se usa como identificador estable para que
// "ParentIndice" pueda referenciar a otro elemento de la misma respuesta, incluso
// después de que el usuario borre o edite filas en la vista previa.
public class ImportPasoPreviewDto
{
    public int Indice { get; set; }
    public int? ParentIndice { get; set; }
    public string Tipo { get; set; } = "paso"; // subtitulo | paso | letra | guion
    public string Texto { get; set; } = string.Empty;
    public string? ImagenBase64 { get; set; }
}

public class PetsImportPreviewDto
{
    public bool SeccionEncontrada { get; set; }
    public List<ImportPasoPreviewDto> Pasos { get; set; } = [];

    // Todos los párrafos con contenido del documento, en orden, con el mismo tipo/
    // jerarquía ya detectados — respaldo cuando no se encuentra el título de la
    // sección automáticamente, o el usuario prefiere elegir el rango a mano.
    public List<ImportParrafoDto> TodosLosParrafos { get; set; } = [];
}

public class ImportParrafoDto
{
    public int Indice { get; set; }
    public int? ParentIndice { get; set; }
    public string Tipo { get; set; } = "paso";
    public string Texto { get; set; } = string.Empty;
    public string? ImagenBase64 { get; set; }
}

public class ImportPasoConfirmDto
{
    public int Indice { get; set; }
    public int? ParentIndice { get; set; }
    public string Tipo { get; set; } = "paso";
    public string Texto { get; set; } = string.Empty;
    public string? ImagenBase64 { get; set; }
}

public class ConfirmarImportacionRequest
{
    public List<ImportPasoConfirmDto> Pasos { get; set; } = [];
}
