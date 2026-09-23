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
}
