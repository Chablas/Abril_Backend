using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionGthModule.Shared.Correos;
using Abril_Backend.Infrastructure.Interfaces;
using Layout = Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Shared.OnboardingEmailLayout;

namespace Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Services
{
    /// <summary>
    /// Onboarding de nuevos colaboradores: la fase que sigue a Reclutamiento.
    ///
    /// El colaborador entra solo, en cuanto su proceso de reclutamiento cierra: llega con su ficha
    /// maestra, su fecha de ingreso y su file digital ya resueltos por la carta oferta, y lo que
    /// queda es recorrer el checklist. La única actividad con operación real hoy es el aviso al
    /// responsable de obra; el resto de las fases se irán habilitando una por una.
    /// </summary>
    public class OnboardingService : IOnboardingService
    {
        private readonly IOnboardingRepository _repo;
        private readonly ICorreoDestinatariosResolver _destinatarios;
        private readonly IEmailService _email;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OnboardingService> _logger;

        public OnboardingService(
            IOnboardingRepository repo,
            ICorreoDestinatariosResolver destinatarios,
            IEmailService email,
            IConfiguration configuration,
            ILogger<OnboardingService> logger)
        {
            _repo          = repo;
            _destinatarios = destinatarios;
            _email         = email;
            _configuration = configuration;
            _logger        = logger;
        }

        public Task<BandejaOnboardingDto> GetBandeja() => _repo.GetBandeja();

        public async Task<OnboardingAccionResultDto> Avanzar(int onboardingId, int? userId)
        {
            var colaborador = await _repo.Avanzar(onboardingId, userId);
            return new OnboardingAccionResultDto
            {
                Colaborador = colaborador,
                Message     = $"Onboarding avanzado a la fase «{colaborador.FaseNombre}».",
            };
        }

        // ── Aviso al responsable de obra ───────────────────────────────────────

        /// <summary>
        /// Manda el aviso al coordinador administrativo de la obra y recién ahí marca la actividad
        /// como cumplida.
        ///
        /// A diferencia del resto de los correos del módulo —que salen best-effort detrás de una
        /// acción que ya se registró—, este ES la acción: si el correo no sale, no hay nada que dar
        /// por hecho, así que el error se propaga y la actividad queda pendiente para reintentar.
        /// </summary>
        public async Task<OnboardingAccionResultDto> EnviarAvisoObra(int onboardingId, int? userId)
        {
            var ctx = await _repo.GetAvisoObraContexto(onboardingId);

            if (!ctx.Aplica)
                throw new AbrilException(
                    ctx.MotivoNoAplica ?? "Este ingreso no lleva aviso al responsable de obra.", 409);

            // El coordinador administrativo del proyecto es el destinatario principal; la
            // Configuración de Onboarding puede sumarle principales y copias, y también apagarlo a
            // él (interruptor del principal automático) o apagar el correo entero.
            var configurados = await _destinatarios.ResolverAsync(CorreoTipoGth.AvisoObra);
            var (para, copias) = CorreoDestinatariosCombinador.Combinar(ctx.CoordAdminEmail, configurados);

            if (para.Count == 0)
                throw new AbrilException(
                    "Este correo no tiene destinatarios activos: revisa Onboarding → Configuración.", 409);

            var destino = string.IsNullOrWhiteSpace(ctx.ProyectoObra) ? "la obra" : ctx.ProyectoObra;

            await _email.SendAsync(
                to:      para,
                subject: $"[Onboarding] Nuevo ingreso a {destino} — {ctx.Nombre}",
                body:    ConstruirCuerpoAvisoObra(ctx),
                isHtml:  true,
                cc:      copias.Count > 0 ? copias : null);

            // El buzón que se guarda es el principal al que realmente salió: es el que la pantalla
            // muestra después, y el coordinador administrativo del proyecto puede cambiar.
            var colaborador = await _repo.MarcarAvisoObraEnviado(onboardingId, para[0], userId);

            _logger.LogInformation(
                "Aviso al responsable de obra enviado (onboarding {OnboardingId}, {Destinatarios} destinatarios)",
                onboardingId, para.Count);

            return new OnboardingAccionResultDto
            {
                Colaborador = colaborador,
                Message     = $"Aviso enviado a {para[0]}.",
            };
        }

        /// <summary>
        /// Cuerpo del aviso: quién entra, a qué puesto, a qué obra y cuándo. No lleva botón — el
        /// coordinador administrativo no tiene nada que registrar en el sistema, lo que necesita es
        /// la anticipación para prever espacio y condiciones de ingreso.
        /// </summary>
        private string ConstruirCuerpoAvisoObra(AvisoObraContextoDto ctx)
        {
            var l = Layout.Desde(_configuration);

            var datos = new List<Layout.Fila>
            {
                new("req-candidato", "Colaborador", Layout.Esc(ctx.Nombre)),
                new("req-puesto", "Puesto", OGuion(ctx.Puesto)),
                new("req-proyecto", "Destino", OGuion(ctx.ProyectoObra)),
                new("req-fecha", "Fecha de ingreso",
                    ctx.FechaIngreso.HasValue
                        ? Layout.Esc(ctx.FechaIngreso.Value.ToString("dd/MM/yyyy"))
                        : "Por confirmar"),
                new("req-area", "Área", OGuion(ctx.Area)),
            };

            if (!string.IsNullOrWhiteSpace(ctx.Empresa))
                datos.Add(new("onb-empresa", "Razón social", Layout.Esc(ctx.Empresa)));
            if (!string.IsNullOrWhiteSpace(ctx.JefeDirecto))
                datos.Add(new("req-solicitante", "Jefe directo", Layout.Esc(ctx.JefeDirecto)));

            return l.Documento(
                new Layout.Cabecera(
                    "req-proyecto", "Nuevo Ingreso",
                    "Prevé espacio y condiciones de ingreso para:"),
                l.Tarjeta(datos));
        }

        /// <summary>Guion cuando el dato no está: nunca se deja una fila de la tarjeta en blanco.</summary>
        private static string OGuion(string? valor) =>
            string.IsNullOrWhiteSpace(valor) ? "—" : Layout.Esc(valor);
    }
}
