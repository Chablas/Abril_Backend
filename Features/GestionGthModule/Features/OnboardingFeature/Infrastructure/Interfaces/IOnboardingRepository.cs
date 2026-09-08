using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Dtos;

namespace Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Interfaces
{
    public interface IOnboardingRepository
    {
        /// <summary>
        /// Todo lo de la pantalla de Onboarding en una sola petición: resumen, embudo de fases y
        /// tabla de colaboradores. De paso le abre el onboarding a todo el que ya terminó
        /// reclutamiento y todavía no lo tenía: entrar dejó de ser una decisión de GTH.
        /// </summary>
        Task<BandejaOnboardingDto> GetBandeja();

        /// <summary>
        /// Avanza el onboarding a la fase siguiente del catálogo, validando lo que esa fase exige.
        /// </summary>
        Task<OnboardingListItemDto> Avanzar(int onboardingId, int? userId);

        /// <summary>
        /// Datos del aviso al responsable de obra: el ingreso y el coordinador administrativo del
        /// proyecto destino, más si ese aviso aplica y si ya salió.
        /// </summary>
        Task<AvisoObraContextoDto> GetAvisoObraContexto(int onboardingId);

        /// <summary>
        /// Deja registrado que el aviso al responsable de obra ya salió (y a qué buzón): es lo único
        /// que marca esa actividad del checklist como cumplida. Devuelve la fila ya actualizada.
        /// </summary>
        Task<OnboardingListItemDto> MarcarAvisoObraEnviado(int onboardingId, string email, int? userId);

        /// <summary>
        /// Una fila de la bandeja, con la MISMA proyección que la tabla. La usan las acciones que
        /// viven en otras features (el correo de bienvenida) para devolver la fila actualizada sin
        /// recalcular el avance por su cuenta — que es como la tabla y el detalle terminarían
        /// mostrando dos números distintos.
        /// </summary>
        Task<OnboardingListItemDto> GetItem(int onboardingId);
    }
}
