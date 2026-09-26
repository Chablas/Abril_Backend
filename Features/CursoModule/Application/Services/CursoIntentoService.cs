using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.CursoModule.Application.Dtos;
using Abril_Backend.Features.CursoModule.Application.Interfaces;
using Abril_Backend.Features.CursoModule.Infrastructure.Models;

namespace Abril_Backend.Features.CursoModule.Application.Services
{
    /// <summary>
    /// Lógica de negocio de los intentos de curso: iniciar, corregir respuestas de forma
    /// genérica (sin conocer el tipo de pregunta) y finalizar sellando la evidencia legal
    /// (SUNAFIL) con un hash SHA-256. La corrección NUNCA se hardcodea por tipo de slide.
    /// </summary>
    public class CursoIntentoService : ICursoIntentoService
    {
        private readonly ICursoIntentoRepository _intentoRepo;
        private readonly ICursoRepository _cursoRepo;

        public CursoIntentoService(ICursoIntentoRepository intentoRepo, ICursoRepository cursoRepo)
        {
            _intentoRepo = intentoRepo;
            _cursoRepo = cursoRepo;
        }

        public async Task<IniciarIntentoResultDto> IniciarAsync(int userId, IniciarIntentoDto dto, string ipAddress, string userAgent)
        {
            var curso = await _cursoRepo.GetByIdAsync(dto.CursoId)
                ?? throw new AbrilException("Curso no encontrado.", 404);
            if (!curso.Activo)
                throw new AbrilException("El curso no está activo.", 400);

            var ahora = DateTime.UtcNow;
            var intento = new CursoIntento
            {
                CursoId = dto.CursoId,
                UserId = userId,
                FechaInicio = ahora,
                Estado = "en_progreso",
            };

            var evidencia = new CursoIntentoEvidencia
            {
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Latitud = dto.Latitud,
                Longitud = dto.Longitud,
                PrecisionGpsMetros = dto.PrecisionGpsMetros,
                DeviceFingerprint = dto.DeviceFingerprint,
                DeclaracionJuradaAceptada = false,
                DeclaracionTexto = string.Empty,
                CreatedAt = ahora,
            };

            var creado = await _intentoRepo.CrearIntentoAsync(intento, evidencia);
            return new IniciarIntentoResultDto { IntentoId = creado.Id };
        }

        public async Task<ResponderSlideResultDto> ResponderAsync(int intentoId, ResponderSlideDto dto)
        {
            var intento = await _intentoRepo.GetByIdAsync(intentoId)
                ?? throw new AbrilException("Intento no encontrado.", 404);
            if (intento.Estado != "en_progreso")
                throw new AbrilException("El intento ya fue finalizado.", 400);

            var slide = await _cursoRepo.GetSlideByIdAsync(dto.SlideId)
                ?? throw new AbrilException("Slide no encontrada.", 404);

            bool? esCorrecta = null;
            decimal? puntajeObtenido = null;

            if (slide.EsEvaluable)
            {
                (esCorrecta, puntajeObtenido) = CorregirGenerico(slide, dto.RespuestaJson);
            }

            var respuesta = new CursoIntentoRespuesta
            {
                CursoIntentoId = intentoId,
                CursoSlideId = dto.SlideId,
                RespuestaJson = dto.RespuestaJson,
                EsCorrecta = esCorrecta,
                PuntajeObtenido = puntajeObtenido,
                TiempoRespuestaSeg = dto.TiempoRespuestaSeg,
            };

            await _intentoRepo.GuardarRespuestaAsync(respuesta);

            return new ResponderSlideResultDto { EsCorrecta = esCorrecta, PuntajeObtenido = puntajeObtenido };
        }

        /// <summary>
        /// Corrección genérica: NO conoce el TipoCodigo de la slide, solo su ModoCorreccion.
        /// - "sin_calificar": no se corrige (EsCorrecta/PuntajeObtenido quedan en null).
        /// - "igualdad_exacta": compara RespuestaJson contra ConfiguracionJson["respuestaCorrecta"]
        ///   con igualdad estructural de JSON (JsonNode.DeepEquals), no como texto.
        /// </summary>
        private static (bool esCorrecta, decimal puntajeObtenido) CorregirGenerico(CursoSlide slide, string respuestaJson)
        {
            if (slide.ModoCorreccion != "igualdad_exacta")
                return (false, 0m); // no debería llamarse en este caso, pero por seguridad no se acredita puntaje

            JsonNode? respuestaCorrecta;
            try
            {
                var config = JsonNode.Parse(slide.ConfiguracionJson) as JsonObject;
                respuestaCorrecta = config != null && config.TryGetPropertyValue("respuestaCorrecta", out var rc) ? rc : null;
            }
            catch (JsonException)
            {
                throw new AbrilException("La configuración de la slide no es un JSON válido.", 500);
            }

            if (respuestaCorrecta == null)
                throw new AbrilException("La slide no tiene 'respuestaCorrecta' configurada.", 500);

            JsonNode? respuestaUsuario;
            try
            {
                respuestaUsuario = JsonNode.Parse(string.IsNullOrWhiteSpace(respuestaJson) ? "null" : respuestaJson);
            }
            catch (JsonException)
            {
                throw new AbrilException("La respuesta enviada no es un JSON válido.", 400);
            }

            bool iguales = JsonNode.DeepEquals(respuestaCorrecta, respuestaUsuario);
            return iguales ? (true, slide.Puntaje ?? 0m) : (false, 0m);
        }

        public async Task<FinalizarIntentoResultDto> FinalizarAsync(int intentoId, FinalizarIntentoDto dto)
        {
            if (!dto.DeclaracionJuradaAceptada)
                throw new AbrilException("Debe aceptar la declaración jurada para finalizar el curso.", 400);

            var intento = await _intentoRepo.GetByIdAsync(intentoId)
                ?? throw new AbrilException("Intento no encontrado.", 404);
            if (intento.Estado != "en_progreso")
                throw new AbrilException("El intento ya fue finalizado.", 400);

            var curso = await _cursoRepo.GetByIdAsync(intento.CursoId)
                ?? throw new AbrilException("Curso no encontrado.", 404);

            var evidencia = await _intentoRepo.GetEvidenciaAsync(intentoId)
                ?? throw new AbrilException("Evidencia del intento no encontrada.", 404);

            var respuestas = await _intentoRepo.GetRespuestasAsync(intentoId);
            var slides = await _cursoRepo.GetSlidesOrdenadasAsync(intento.CursoId);
            // ContarParaNota=false ("solo práctica"): ResponderAsync igual corrige y devuelve
            // acierto/error (ver EsEvaluable ahí), pero su puntaje no entra a la nota final.
            var slidesEvaluables = slides.Where(s => s.EsEvaluable && s.ContarParaNota).ToList();

            decimal puntajeTotal = slidesEvaluables.Sum(s => s.Puntaje ?? 0m);
            decimal puntajeObtenido = respuestas
                .Where(r => slidesEvaluables.Any(s => s.Id == r.CursoSlideId))
                .Sum(r => r.PuntajeObtenido ?? 0m);

            // Escala 0-100 (EvaluacionesModule no define una escala propia reutilizable).
            decimal notaFinal = puntajeTotal > 0 ? Math.Round(puntajeObtenido / puntajeTotal * 100m, 2) : 0m;
            bool aprobado = notaFinal >= curso.NotaMinimaAprobacion;

            var ahora = DateTime.UtcNow;
            intento.FechaFin = ahora;
            intento.NotaFinal = notaFinal;
            intento.Aprobado = aprobado;
            intento.Estado = "finalizado";

            evidencia.DeclaracionJuradaAceptada = dto.DeclaracionJuradaAceptada;
            evidencia.DeclaracionTexto = dto.DeclaracionTexto;
            evidencia.SelladoAt = ahora;
            evidencia.HashSha256 = CalcularHash(respuestas, notaFinal, aprobado, intento.FechaInicio, ahora, intento.UserId);

            await _intentoRepo.FinalizarAsync(intento, evidencia);

            return new FinalizarIntentoResultDto
            {
                IntentoId = intento.Id,
                NotaFinal = notaFinal,
                Aprobado = aprobado,
                FechaInicio = intento.FechaInicio,
                FechaFin = ahora,
                Evidencia = MapEvidencia(evidencia),
            };
        }

        /// <summary>Concatenación determinística: respuestas ordenadas por Id + NotaFinal + Aprobado + FechaInicio + FechaFin + UserId.</summary>
        private static string CalcularHash(List<CursoIntentoRespuesta> respuestas, decimal notaFinal, bool aprobado, DateTime fechaInicio, DateTime fechaFin, int userId)
        {
            var sb = new StringBuilder();
            foreach (var r in respuestas.OrderBy(r => r.Id))
            {
                sb.Append(r.Id).Append('|')
                  .Append(r.CursoSlideId).Append('|')
                  .Append(r.RespuestaJson).Append('|')
                  .Append(r.EsCorrecta).Append('|')
                  .Append(r.PuntajeObtenido).Append(';');
            }
            sb.Append(notaFinal).Append('|')
              .Append(aprobado).Append('|')
              .Append(fechaInicio.ToString("O")).Append('|')
              .Append(fechaFin.ToString("O")).Append('|')
              .Append(userId);

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        public async Task<CursoIntentoDetalleDto> GetDetalleAsync(int intentoId)
        {
            var intento = await _intentoRepo.GetByIdAsync(intentoId)
                ?? throw new AbrilException("Intento no encontrado.", 404);
            var respuestas = await _intentoRepo.GetRespuestasAsync(intentoId);
            var evidencia = await _intentoRepo.GetEvidenciaAsync(intentoId);

            return new CursoIntentoDetalleDto
            {
                Id = intento.Id,
                CursoId = intento.CursoId,
                UserId = intento.UserId,
                FechaInicio = intento.FechaInicio,
                FechaFin = intento.FechaFin,
                NotaFinal = intento.NotaFinal,
                Aprobado = intento.Aprobado,
                Estado = intento.Estado,
                Respuestas = respuestas.Select(r => new CursoIntentoRespuestaDto
                {
                    Id = r.Id,
                    CursoSlideId = r.CursoSlideId,
                    RespuestaJson = r.RespuestaJson,
                    EsCorrecta = r.EsCorrecta,
                    PuntajeObtenido = r.PuntajeObtenido,
                    TiempoRespuestaSeg = r.TiempoRespuestaSeg,
                    CreatedAt = r.CreatedAt,
                }).ToList(),
                Evidencia = evidencia == null ? null : MapEvidencia(evidencia),
            };
        }

        private static CursoIntentoEvidenciaDto MapEvidencia(CursoIntentoEvidencia e) => new()
        {
            IpAddress = e.IpAddress,
            Latitud = e.Latitud,
            Longitud = e.Longitud,
            PrecisionGpsMetros = e.PrecisionGpsMetros,
            UserAgent = e.UserAgent,
            DeviceFingerprint = e.DeviceFingerprint,
            DeclaracionJuradaAceptada = e.DeclaracionJuradaAceptada,
            DeclaracionTexto = e.DeclaracionTexto,
            HashSha256 = e.HashSha256,
            CreatedAt = e.CreatedAt,
            SelladoAt = e.SelladoAt,
        };
    }
}
