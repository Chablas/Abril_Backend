using Abril_Backend.Features.GestionGthModule.Shared.Correos;
using System.Text.RegularExpressions;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Services
{
    /// <inheritdoc cref="ICorreoConfigService"/>
    public class CorreoConfigService : ICorreoConfigService
    {
        private readonly ICorreoConfigRepository _repo;

        public CorreoConfigService(ICorreoConfigRepository repo)
        {
            _repo = repo;
        }

        /// <summary>
        /// Qué correos administra cada pantalla de configuración, en el orden del flujo. El reparto
        /// no es cosmético: es lo que impide que desde una pantalla se toque la configuración de la
        /// otra. La clave es el slug de la URL (<c>api/v1/gestion-gth/{modulo}/configuracion</c>).
        ///   • solicitud-personal → los correos del flujo del solicitante.
        ///   • aprobaciones       → los que dispara una decisión de esa pantalla (los avisos a GTH
        ///                            y a TI).
        ///   • reclutamiento      → los que salen desde la bandeja de GTH, de punta a punta del
        ///                            proceso: desde la long list hasta la carta oferta que lo cierra.
        ///
        /// Onboarding ya no tiene pantalla propia: sus dos únicos correos eran los de la carta
        /// oferta, que pasó a ser el último paso de Reclutamiento. Cuando el onboarding tenga
        /// correos suyos, vuelve como una clave más de este diccionario.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string[]> CorreosPorPantalla =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["solicitud-personal"] = new[]
                {
                    CorreoTipoGth.AprobacionGg,
                    // Aviso al gerente del área de las vacantes NUEVAS: sale junto con el de arriba
                    // y con las mismas vacantes, pero solo para que se entere — no las aprueba él.
                    CorreoTipoGth.AvisoGerenteArea,
                    // Los reemplazos no suben a Gerencia General: los aprueban el gerente del área
                    // y GTH, en ese orden. Este es el primero de los dos y sale en el mismo momento
                    // que el de arriba (al registrar la solicitud), así que se configura acá; el de
                    // GTH lo dispara la firma del área y se configura en Aprobaciones.
                    CorreoTipoGth.AprobacionReemplazo,
                    // Ingreso directo FFT: sale al registrar la solicitud, en lugar del correo de
                    // aprobación (a un FFT no lo aprueba nadie), así que se configura acá y no en
                    // Aprobaciones.
                    CorreoTipoGth.FftSolicitudGg,
                    CorreoTipoGth.LongListDecision,
                    CorreoTipoGth.FinalistaDecision,
                },
                // Los correos que dispara una decisión de la pantalla «Aprobaciones», no el
                // solicitante: se configuran acá, que es donde esas decisiones se toman.
                // SOLICITUD, TI_VACANTES y FFT_APROBACION_GG salen de la del Gerente General;
                // APROBACION_REEMPLAZO_GTH, de la firma del gerente del área (le abre el turno a
                // GTH) y REEMPLAZO_APROBADO, de la de GTH, que es la que completa el reemplazo.
                ["aprobaciones"] = new[]
                {
                    CorreoTipoGth.Solicitud,
                    CorreoTipoGth.AprobacionReemplazoGth,
                    CorreoTipoGth.ReemplazoAprobado,
                    CorreoTipoGth.Ti,
                    CorreoTipoGth.FftAprobacionGg,
                },
                // Todos los correos que salen desde la bandeja de GTH, en el orden del proceso.
                // AGRADECIMIENTO también lo dispara el rechazo de un finalista desde Solicitud de
                // Personal, pero es el MISMO correo al candidato: se configura una sola vez y acá,
                // que es de donde sale en el resto de los casos.
                ["reclutamiento"] = new[]
                {
                    CorreoTipoGth.LongList,
                    CorreoTipoGth.FormularioEnvio,
                    CorreoTipoGth.FormularioCompletado,
                    CorreoTipoGth.FormularioCorreccion,
                    // El candidato FFT no pasa por entrevistas ni por decisión de finalista: al
                    // aprobarle el formulario, GTH avisa con este correo que pasa a su EMO. Va acá
                    // porque esa aprobación se hace en esta pantalla.
                    CorreoTipoGth.FftEmo,
                    CorreoTipoGth.Entrevista,
                    CorreoTipoGth.EntrevistaRespuesta,
                    // Misma respuesta del candidato, otro destinatario: al solicitante le avisa
                    // cuándo y dónde es la entrevista a la que tiene que ir. Sale desde el
                    // endpoint público de la respuesta, igual que el de arriba.
                    CorreoTipoGth.EntrevistaConfirmadaSolicitante,
                    CorreoTipoGth.FinalistaEnvio,
                    // Retomar a un rechazado se hace desde esta pantalla, así que su aviso al
                    // solicitante se configura acá.
                    CorreoTipoGth.CandidatoRetomado,
                    CorreoTipoGth.Agradecimiento,
                    // La ida y la vuelta de la carta oferta, que es el último paso del proceso: el
                    // primero se la manda al candidato (al enviarla o al reenviarle el enlace) y el
                    // segundo le avisa a GTH cuando él la firma, para que la apruebe y cierre. Se
                    // configuran acá desde que la carta dejó de ser el primer paso del onboarding.
                    CorreoTipoGth.CartaOferta,
                    CorreoTipoGth.CartaOfertaFirmada,
                },
            };

        private static readonly Regex EmailRegex =
            new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        public Task<CorreoConfigDto> GetConfig(string pantalla) =>
            _repo.GetConfigAsync(CorreosDeLaPantalla(pantalla));

        public Task<int> CrearAdicional(string pantalla, CorreoAdicionalCreateDto dto, int? userId)
        {
            var tipoCodigo = ResolverTipo(pantalla, dto?.EventoCodigo);
            var email      = ValidarEmail(dto?.Email);
            return _repo.CreateAdicionalAsync(tipoCodigo, email, dto?.Nombre, dto?.EsCopia ?? false, userId);
        }

        public Task ActualizarAdicional(string pantalla, int destinatarioId, CorreoAdicionalUpdateDto dto, int? userId)
        {
            var email = ValidarEmail(dto?.Email);
            return _repo.UpdateAdicionalAsync(
                destinatarioId, email, dto?.Nombre, dto?.EsCopia ?? false, CorreosDeLaPantalla(pantalla), userId);
        }

        public Task SetDestinatarioActive(string pantalla, int destinatarioId, bool active, int? userId) =>
            _repo.SetDestinatarioActiveAsync(destinatarioId, active, CorreosDeLaPantalla(pantalla), userId);

        public Task SetCorreoActive(string pantalla, string tipoSlug, bool active, int? userId) =>
            _repo.SetTipoActiveAsync(ResolverTipo(pantalla, tipoSlug), active, userId);

        public Task SetPrincipalAutomaticoActive(string pantalla, string tipoSlug, bool active, int? userId) =>
            _repo.SetPrincipalAutomaticoActiveAsync(ResolverTipo(pantalla, tipoSlug), active, userId);

        public Task EliminarAdicional(string pantalla, int destinatarioId, int? userId) =>
            _repo.DeleteAdicionalAsync(destinatarioId, CorreosDeLaPantalla(pantalla), userId);

        /// <summary>Correos de la pantalla indicada; 404 si el módulo no tiene configuración de correos.</summary>
        private static string[] CorreosDeLaPantalla(string? pantalla)
        {
            if (!string.IsNullOrWhiteSpace(pantalla) &&
                CorreosPorPantalla.TryGetValue(pantalla.Trim(), out var correos))
                return correos;

            throw new AbrilException("Ese módulo no tiene configuración de correos.", 404);
        }

        /// <summary>
        /// Acepta tanto el código estable (APROBACION_GG) como el slug de la URL
        /// (aprobacion-gg) y valida que sea uno de los correos de esa pantalla, para que un
        /// id ajeno no pueda tocar la configuración de otra feature.
        /// </summary>
        private static string ResolverTipo(string? pantalla, string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                throw new AbrilException("Falta indicar de qué correo se trata.", 400);

            var codigo = CorreoTipoGth.FromSlug(valor) ?? valor.Trim().ToUpperInvariant();
            if (!CorreosDeLaPantalla(pantalla).Contains(codigo, StringComparer.OrdinalIgnoreCase))
                throw new AbrilException("Ese correo no se configura desde esta pantalla.", 400);

            return codigo;
        }

        private static string ValidarEmail(string? email)
        {
            var e = email?.Trim().ToLowerInvariant() ?? string.Empty;
            if (e.Length == 0)
                throw new AbrilException("El correo es obligatorio.", 400);
            if (!EmailRegex.IsMatch(e))
                throw new AbrilException($"«{e}» no tiene un formato de correo válido.", 400);
            return e;
        }
    }
}
