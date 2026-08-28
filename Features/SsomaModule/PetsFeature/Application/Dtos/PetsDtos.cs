namespace Abril_Backend.Features.SsomaModule.PetsFeature.Application.Dtos;

public class PetListItemDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Codigo { get; set; }
    public bool Activo { get; set; }
    public int TotalPasos { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PetPasoDto
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string Tipo { get; set; } = "paso";
    public string Descripcion { get; set; } = string.Empty;
    public string? ImagenUrl { get; set; }
    public int Orden { get; set; }
}

public class PetDetalleDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Codigo { get; set; }
    public string? SharepointUrl { get; set; }
    public bool Activo { get; set; }
    public List<PetPasoDto> Pasos { get; set; } = [];
}

public class CrearPetRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string? Codigo { get; set; }
    public string? SharepointUrl { get; set; }
}

public class ActualizarPetRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string? Codigo { get; set; }
    public string? SharepointUrl { get; set; }
    public bool Activo { get; set; } = true;
}

public class CrearPetPasoRequest
{
    public string Descripcion { get; set; } = string.Empty;

    // null = nivel superior de "Procedimiento". Si se indica, el nuevo paso se crea
    // como hijo de ese subtítulo (a cualquier nivel de anidamiento).
    public int? ParentId { get; set; }

    // subtitulo | paso | letra | guion
    public string Tipo { get; set; } = "paso";

    // 1-based, posición ENTRE LOS HERMANOS del mismo ParentId. Null o fuera de rango
    // = se agrega al final de ese grupo. Todo lo que esté en esa posición o después
    // se corre +1 — así insertar "entre el 3 y el 4" no requiere renumerar a mano.
    public int? Posicion { get; set; }
}

public class ActualizarPetPasoRequest
{
    public string Descripcion { get; set; } = string.Empty;
    public string Tipo { get; set; } = "paso";
}

public class ReordenarPasosRequest
{
    // null = reordena los del nivel superior. Si se indica, reordena solo los
    // hijos de ese subtítulo — cada grupo de hermanos se reordena por separado.
    public int? ParentId { get; set; }

    // Ids de los pasos ACTIVOS de ese grupo de hermanos, en el nuevo orden deseado.
    public List<int> PasoIds { get; set; } = [];
}
