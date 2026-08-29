namespace Abril_Backend.Features.PlaneamientoBimFeature.Application.Dtos
{
    // ── Lectura ──────────────────────────────────────────────────────────────

    public class ConfiguracionInicialDto
    {
        public List<TorreDto> Torres { get; set; } = new();
        public List<FaseDto> Fases { get; set; } = new();
        public int? ResponsableId { get; set; }
        public string? ResponsableNombre { get; set; }
        public decimal? MetaPpc { get; set; }
    }

    public class TorreDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int Orden { get; set; }
        /// <summary>Cantidad de sectores (1..N, derivados) para los niveles de
        /// esta torre con TipoEstructura = SUBESTRUCTURA.</summary>
        public int CantidadSectoresSubestructura { get; set; }
        /// <summary>Análogo a <see cref="CantidadSectoresSubestructura"/> para
        /// niveles con TipoEstructura = SUPERESTRUCTURA.</summary>
        public int CantidadSectoresSuperestructura { get; set; }
        public List<NivelDto> Niveles { get; set; } = new();
    }

    public class NivelDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int Orden { get; set; }
        public string? TipoEstructura { get; set; }
    }

    public class ResponsableBimLookupDto
    {
        public int Id { get; set; }
        public string ApellidoNombre { get; set; } = string.Empty;
    }

    /// <summary>Id de la fila bim_proyecto_fase (no del catálogo bim_fase). Las 5 fases siempre existen; solo se editan sus fechas.</summary>
    public class FaseDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public DateOnly? FechaInicio { get; set; }
        public DateOnly? FechaFinMeta { get; set; }
    }

    // ── Escritura ────────────────────────────────────────────────────────────

    public class ConfiguracionInicialUpdateDto
    {
        public List<TorreUpdateDto> Torres { get; set; } = new();
        public List<FaseUpdateDto> Fases { get; set; } = new();
        public int? ResponsableId { get; set; }
        public string? ResponsableNombre { get; set; }
        public decimal? MetaPpc { get; set; }
    }

    /// <summary>Id null/0 = torre nueva a crear; Id existente = actualizar esa fila.</summary>
    public class TorreUpdateDto
    {
        public int? Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int Orden { get; set; }
        public int CantidadSectoresSubestructura { get; set; }
        public int CantidadSectoresSuperestructura { get; set; }
        public List<NivelUpdateDto> Niveles { get; set; } = new();
    }

    public class NivelUpdateDto
    {
        public int? Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int Orden { get; set; }
        public string? TipoEstructura { get; set; }
    }

    /// <summary>Id obligatorio: debe ser una de las 5 filas bim_proyecto_fase ya creadas para el proyecto. No se crean ni eliminan fases desde acá.</summary>
    public class FaseUpdateDto
    {
        public int Id { get; set; }
        public DateOnly? FechaInicio { get; set; }
        public DateOnly? FechaFinMeta { get; set; }
    }
}
