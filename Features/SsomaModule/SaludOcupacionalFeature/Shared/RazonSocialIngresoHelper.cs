using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Services;
using Abril_Backend.Shared.Services.ReclutamientoEmoIngreso.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Shared
{
    /// <summary>
    /// Asignarle a una ficha de pre-ingreso la razón social con la que va a entrar. Se elige en el
    /// EMO de Ingreso, y el EMO de Ingreso tiene dos entradas: programarlo con clínica
    /// (<c>ProgramacionEmoRepository</c>) y registrar su resultado sin haberlo programado
    /// (<c>EmoRepository</c>, cuando el examen se hizo por fuera). Las dos aplican esta misma regla.
    ///
    /// <para>Hasta el 2026-09-24 solo la programación la pedía, así que un EMO cargado directo
    /// dejaba a la ficha sin razón social hasta la carta oferta, y la aprobación la hacía entrar
    /// sin ninguna (caso REQ-2026-0014): su carta salió sin empresa y su inducción no se podía
    /// programar.</para>
    ///
    /// Muta el contexto y no guarda: los cambios entran en el <c>SaveChanges</c> de quien llama.
    /// </summary>
    public static class RazonSocialIngresoHelper
    {
        public static async Task AsignarAsync(
            AppDbContext ctx, Worker worker, int elegida, IReclutamientoEmoIngresoService reclutamiento, int? userId)
        {
            // Se revalida contra la MISMA lista que se le ofrecio: lo que no esta ahi tampoco se
            // acepta, venga de donde venga el id.
            if (!await RazonSocialCuposHelper.EsValidaAsync(ctx, elegida))
                throw new AbrilException("La razón social seleccionada no es válida.", 400);

            // Y con cupo libre: elegirla es ASIGNARSELA a la ficha, asi que una razon social llena
            // metería un trabajador mas por encima del tope. El modal ya lo avisa y no deja guardar,
            // pero el tope se cuenta de nuevo acá: entre que se abrio el modal y este momento otro
            // pudo ocupar el ultimo cupo. Vale tambien cuando es la que la ficha ya traia: la de
            // pre-ingreso no ocupa cupo, asi que una razon social que se lleno despues de
            // asignarsela tampoco tiene lugar para ella.
            //
            // Salvo que la vacante sea un REEMPLAZO: ahi el tope se pasa a proposito, porque el que
            // entra y el que sale conviven un mes. Ver IReclutamientoEmoIngresoService
            // .EsReemplazoAsync. Se pregunta solo cuando la razon social esta llena, que es el unico
            // momento en que la respuesta cambia algo.
            if (await RazonSocialCuposHelper.CuposDisponiblesAsync(ctx, elegida) == 0
                && !await reclutamiento.EsReemplazoAsync(ctx, worker))
            {
                var nombre = await ctx.Contributor
                    .Where(c => c.ContributorId == elegida)
                    .Select(c => c.ContributorName)
                    .FirstOrDefaultAsync();
                throw new AbrilException(RazonSocialCuposHelper.MensajeSinCupos(nombre), 400);
            }

            if (worker.ContributorId != elegida)
            {
                worker.ContributorId = elegida;
                worker.UpdatedAt     = DateTimeOffset.UtcNow;
            }

            // Y se le baja al requerimiento del que salio la ficha, si viene de uno: la carta oferta
            // la lee de la ficha y el onboarding del requerimiento, asi que quedarse con la vieja en
            // uno de los dos haria que la persona firme con una empresa y entre a otra.
            await reclutamiento.SincronizarRazonSocialAsync(ctx, worker, elegida, userId);
        }
    }
}
