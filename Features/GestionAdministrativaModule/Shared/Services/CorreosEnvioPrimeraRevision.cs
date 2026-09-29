using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Shared.Services.Revisores.Interfaces;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// A quién le llegan los dos correos de enviar una planilla a la PRIMERA revisión: el aviso a la
    /// jefatura (<see cref="CorreoEventoCodigos.RendicionPrimeraRevision"/>) y el acuse al propio
    /// solicitante (<see cref="CorreoEventoCodigos.RendicionEnviada"/>).
    ///
    /// Los dispara «Rendir» en Solicitud de Salidas —que ya envía la planilla— y «Enviar a revisión»
    /// en Mis Rendiciones —la que se vuelve a generar tras una observación, o la que rindió el
    /// revisor—. Vive en el Shared del módulo para que las dos confirmaciones nombren exactamente a
    /// los mismos destinatarios.
    ///
    /// Sale de las MISMAS llamadas que hace el envío (<c>RendicionService.EnviarAPrimeraRevision</c>):
    /// quien revisa la planilla lo decide <see cref="IJefeRevisorResolver"/> por PASO
    /// (<see cref="PasoAprobacion.PrimeraRevision"/>) y no por salida —en un área filtrada por
    /// proyecto la salida la aprueba el residente y la planilla la revisa el administrador de obra—,
    /// y el acuse va al correo de usuario del trabajador. Sin correo del revisor el envío se corta
    /// antes de mandar nada, así que acá tampoco se anuncia ningún correo.
    ///
    /// Se pregunta por el trabajador que envía, que es el documento que va a nacer al rendir. Una
    /// planilla que el revisor generó desde Gestión de Salidas puede agrupar a varios y entonces el
    /// envío resuelve sobre el conjunto: el preview es de la pantalla, no de una planilla concreta.
    /// </summary>
    public static class CorreosEnvioPrimeraRevision
    {
        /// <param name="solicitanteEmail">Correo de usuario del trabajador (app_user.email).</param>
        /// <returns>Los correos que saldrían hoy; vacío si ninguno le llega a nadie.</returns>
        public static async Task<List<CorreoAvisoPreviewDto>> ResolverAsync(
            ICorreoSalidaRecipientResolver correoResolver,
            IJefeRevisorResolver revisorResolver,
            int workerId,
            string? solicitanteEmail)
        {
            var avisos = new List<CorreoAvisoPreviewDto>();

            var revisores = await revisorResolver.ResolveAprobadoresDeDocumentoAsync(
                new[] { workerId }, PasoAprobacion.PrimeraRevision);

            var correosRevisores = revisores
                .Select(a => a.Persona.Email?.Trim() ?? string.Empty)
                .Where(e => e.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (correosRevisores.Count == 0) return avisos;

            await AgregarAsync(avisos, correoResolver,
                "A la jefatura", CorreoEventoCodigos.RendicionPrimeraRevision, correosRevisores);

            if (!string.IsNullOrWhiteSpace(solicitanteEmail))
                await AgregarAsync(avisos, correoResolver,
                    "Al solicitante", CorreoEventoCodigos.RendicionEnviada, new[] { solicitanteEmail! });

            return avisos;
        }

        /// <summary>
        /// Agrega un correo solo si hoy se enviaría: la pantalla distingue "no sale ningún correo" de
        /// "sale a estas direcciones".
        /// </summary>
        private static async Task AgregarAsync(
            List<CorreoAvisoPreviewDto> avisos, ICorreoSalidaRecipientResolver correoResolver,
            string etiqueta, string eventoCodigo, IReadOnlyCollection<string> principal)
        {
            var envio = await correoResolver.ResolveEnvioAsync(eventoCodigo, principal);
            if (!envio.Enviar || envio.Para.Count == 0) return;

            avisos.Add(new CorreoAvisoPreviewDto
            {
                Etiqueta = etiqueta,
                Para     = envio.Para,
                Copia    = envio.Copia,
            });
        }
    }
}
