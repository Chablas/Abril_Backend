namespace Abril_Backend.Features.PlaneamientoBimFeature.Application.Dtos
{
    // ── Lectura ──────────────────────────────────────────────────────────────

    public class CargaDiariaDto
    {
        public DateOnly Fecha { get; set; }
        /// <summary>Categoría de "Evidencias" en esta respuesta ("GENERAL" | "PROCURA") — el resto
        /// del payload (grid, catálogos, bloqueos) no está scoped por categoría, es siempre el mismo.</summary>
        public string Categoria { get; set; } = "GENERAL";
        public bool EsEditable { get; set; }
        public List<TorreDto> Torres { get; set; } = new();
        public List<ActividadCatalogoDto> Actividades { get; set; } = new();
        public List<CausaCatalogoDto> Causas { get; set; } = new();
        public List<CeldaDto> Celdas { get; set; } = new();
        public List<EvidenciaFotoDto> Evidencias { get; set; } = new();
        public List<RestriccionDto> RestriccionesActivas { get; set; } = new();
    }

    public class ActividadCatalogoDto
    {
        public int Id { get; set; }
        public int MacroActividadId { get; set; }
        public string MacroActividadNombre { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public int Orden { get; set; }
    }

    public class CausaCatalogoDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int Orden { get; set; }
    }

    /// <summary>Solo se listan las celdas con registro. Ausente = "sin cargar" (no confundir con 0%).</summary>
    public class CeldaDto
    {
        public int TorreId { get; set; }
        public int ZonaId { get => TorreId; set => TorreId = value; }
        public int NivelId { get; set; }
        public int SectorId { get; set; }
        public int ActividadId { get; set; }
        public decimal PorcentajeAvance { get; set; }
        public bool? Cumplida { get; set; }
        public int? CausaId { get; set; }
        public int? CausaNoCumplimientoId => CausaId;
        public string? CausaNombre { get; set; }
        public string? CausaDetalle { get; set; }
    }

    public class EvidenciaFotoDto
    {
        public int Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public DateTimeOffset CreatedDateTime { get; set; }
    }

    // ── Escritura ────────────────────────────────────────────────────────────

    public class CargaDiariaUpdateDto
    {
        public List<CeldaUpdateDto> Celdas { get; set; } = new();
    }

    /// <summary>Upsert por la tupla natural (torre/zona, nivel, sector, actividad) + fecha de la URL — no viaja Id.
    /// SectorId es el número de sector derivado (1..N para el nivel, ver NivelRangoSectorDto),
    /// no una fila de bim_zona_sector. Cumplida es bool? nullable: true (100%), false (0%), null (neutro / sin evaluar).</summary>
    public class CeldaUpdateDto
    {
        public int TorreId { get; set; }

        public int ZonaId
        {
            get => TorreId;
            set { if (value != 0 && TorreId == 0) TorreId = value; }
        }

        public int NivelId { get; set; }
        public int SectorId { get; set; }
        public int ActividadId { get; set; }
        public bool? Cumplida { get; set; }

        public bool? Hecho
        {
            get => Cumplida;
            set { if (value.HasValue && !Cumplida.HasValue) Cumplida = value; }
        }

        public int? CausaId { get; set; }

        public int? CausaNoCumplimientoId
        {
            get => CausaId;
            set { if (value.HasValue && !CausaId.HasValue) CausaId = value; }
        }

        public string? CausaDetalle { get; set; }
    }

    public class CeldaRegistroDto : CeldaUpdateDto
    {
    }

    /// <summary>Rango válido de SectorId para un nivel, derivado de su TipoEstructura y
    /// de los conteos de la torre — usado solo para validar CeldaUpdateDto.SectorId en
    /// PlaneamientoBimCargaDiariaService, no se expone en ningún endpoint de lectura.</summary>
    public class NivelRangoSectorDto
    {
        public int NivelId { get; set; }
        public string? TipoEstructura { get; set; }
        public int CantidadSectoresSubestructura { get; set; }
        public int CantidadSectoresSuperestructura { get; set; }
    }
}
