namespace Abril_Backend.Features.CursoModule.Application.Dtos
{
    public class IniciarIntentoDto
    {
        public int CursoId { get; set; }
        public decimal? Latitud { get; set; }
        public decimal? Longitud { get; set; }
        public decimal? PrecisionGpsMetros { get; set; }
        public string? DeviceFingerprint { get; set; }
    }

    public class IniciarIntentoResultDto
    {
        public int IntentoId { get; set; }
    }

    public class ResponderSlideDto
    {
        public int SlideId { get; set; }
        public string RespuestaJson { get; set; } = "{}";
        public int? TiempoRespuestaSeg { get; set; }
    }

    public class ResponderSlideResultDto
    {
        public bool? EsCorrecta { get; set; }
        public decimal? PuntajeObtenido { get; set; }
    }

    public class FinalizarIntentoDto
    {
        public bool DeclaracionJuradaAceptada { get; set; }
        public string DeclaracionTexto { get; set; } = string.Empty;
    }

    public class FinalizarIntentoResultDto
    {
        public int IntentoId { get; set; }
        public decimal NotaFinal { get; set; }
        public bool Aprobado { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public CursoIntentoEvidenciaDto Evidencia { get; set; } = new();
    }

    public class CursoIntentoEvidenciaDto
    {
        public string IpAddress { get; set; } = string.Empty;
        public decimal? Latitud { get; set; }
        public decimal? Longitud { get; set; }
        public decimal? PrecisionGpsMetros { get; set; }
        public string UserAgent { get; set; } = string.Empty;
        public string? DeviceFingerprint { get; set; }
        public bool DeclaracionJuradaAceptada { get; set; }
        public string DeclaracionTexto { get; set; } = string.Empty;
        public string? HashSha256 { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SelladoAt { get; set; }
    }

    public class CursoIntentoRespuestaDto
    {
        public int Id { get; set; }
        public int CursoSlideId { get; set; }
        public string RespuestaJson { get; set; } = "{}";
        public bool? EsCorrecta { get; set; }
        public decimal? PuntajeObtenido { get; set; }
        public int? TiempoRespuestaSeg { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>Detalle completo del intento, para auditoría RRHH/SSOMA.</summary>
    public class CursoIntentoDetalleDto
    {
        public int Id { get; set; }
        public int CursoId { get; set; }
        public int UserId { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public decimal? NotaFinal { get; set; }
        public bool? Aprobado { get; set; }
        public string Estado { get; set; } = string.Empty;
        public List<CursoIntentoRespuestaDto> Respuestas { get; set; } = new();
        public CursoIntentoEvidenciaDto? Evidencia { get; set; }
    }

    /// <summary>Un curso visible para el usuario logueado + su progreso real (último intento,
    /// si existe) — fuente de datos del dashboard "Mis cursos" (no confundir con CursoDto, que
    /// es el catálogo/administración sin datos de progreso por usuario).</summary>
    public class MiCursoProgresoDto
    {
        public int CursoId { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string? CategoriaNombre { get; set; }
        public string? ColorTema { get; set; }
        public string? LogoUrl { get; set; }
        public decimal NotaMinimaAprobacion { get; set; }

        /// <summary>"no_iniciado" | "en_progreso" | "aprobado" | "desaprobado".</summary>
        public string Estado { get; set; } = "no_iniciado";
        public int? IntentoId { get; set; }
        public decimal? NotaFinal { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public int TotalSlides { get; set; }
        public int SlidesRespondidas { get; set; }

        /// <summary>Aproximación: suma de TiempoRespuestaSeg de las respuestas del último
        /// intento — no es tiempo de sesión real (eso no se registra hoy), es lo único
        /// medible que ya existe en el modelo.</summary>
        public int SegundosInvertidos { get; set; }
    }

    /// <summary>Fila del historial de evaluaciones (auditoría SUNAFIL) — un intento real de un
    /// trabajador en un curso, con lo mínimo para listar/filtrar; el detalle completo (respuestas
    /// + evidencia con hash) se pide aparte con GET /curso-intento/{intentoId}.</summary>
    public class CursoIntentoHistorialDto
    {
        public int IntentoId { get; set; }
        public int CursoId { get; set; }
        public string CursoTitulo { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string TrabajadorNombre { get; set; } = string.Empty;
        public DateTime FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public decimal? NotaFinal { get; set; }
        public bool? Aprobado { get; set; }

        /// <summary>"en_progreso" | "finalizado".</summary>
        public string Estado { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public string? HashSha256 { get; set; }
        public DateTime? SelladoAt { get; set; }
    }
}
