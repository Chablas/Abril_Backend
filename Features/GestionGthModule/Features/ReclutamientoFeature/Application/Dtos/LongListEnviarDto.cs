namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos
{
    /// <summary>
    /// Metadatos (campo <c>data</c>, JSON) del envío de la long list. Los archivos (CV e
    /// informe) viajan aparte en el multipart y se enlazan por <see cref="LongListCandidatoMetaDto.CvKey"/>
    /// / <see cref="LongListCandidatoMetaDto.InformeKey"/> (nombre del campo del form file).
    /// </summary>
    public class LongListEnviarMetaDto
    {
        public List<LongListCandidatoMetaDto> Candidatos { get; set; } = new();
    }

    /// <summary>Metadatos de un candidato de la long list (sin los binarios de los archivos).</summary>
    public class LongListCandidatoMetaDto
    {
        /// <summary>Nombre y apellido del candidato (lo captura/corrige GTH).</summary>
        public string? Nombre { get; set; }

        /// <summary>Fuente de reclutamiento (nombre del canal), solo para mostrar en el correo.</summary>
        public string? FuenteNombre { get; set; }

        /// <summary>Comentario interno de GTH sobre el candidato.</summary>
        public string? Comentario { get; set; }

        /// <summary>Nombre del campo del multipart con el CV de este candidato (ej. "cv_0").</summary>
        public string? CvKey { get; set; }

        /// <summary>Nombre del campo del multipart con el informe (opcional) de este candidato.</summary>
        public string? InformeKey { get; set; }
    }

    /// <summary>
    /// Candidato de la long list ya resuelto con los binarios de sus archivos (lo arma el
    /// controller a partir del multipart y lo consume el servicio para el correo).
    /// </summary>
    public class LongListCandidatoArchivoDto
    {
        public string? Nombre { get; set; }
        public string? FuenteNombre { get; set; }
        public string? Comentario { get; set; }

        public string CvFileName { get; set; } = string.Empty;
        public string CvContentType { get; set; } = "application/octet-stream";
        public byte[] CvContent { get; set; } = Array.Empty<byte>();

        /// <summary>Informe adjunto (opcional).</summary>
        public string? InformeFileName { get; set; }
        public string? InformeContentType { get; set; }
        public byte[]? InformeContent { get; set; }
    }

    /// <summary>
    /// Cabecera del requerimiento necesaria para construir el correo de la long list.
    /// La devuelve el repositorio junto con la transición de estado (1 roundtrip).
    /// </summary>
    public class LongListEnvioContextoDto
    {
        public string EstadoCodigo { get; set; } = string.Empty;
        public string EstadoNombre { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public string Puesto { get; set; } = string.Empty;
        public string? Area { get; set; }
        public string? ProyectoObra { get; set; }
        public DateOnly FechaRequeridaIngreso { get; set; }
        /// <summary>SLA del tipo de proceso asignado (null si aún no se clasificó).</summary>
        public int? SlaDias { get; set; }
    }
}
