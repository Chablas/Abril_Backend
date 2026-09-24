using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Quién es «Tesorería» para los correos: los que hoy tienen el rol TESORERO. Es el
    /// destinatario principal de todos los avisos que le llegan a Tesorería —el consolidado
    /// firmado y la observación subsanada, que salen de Consolidados, y la revisión confirmada,
    /// que sale de Reembolsos—, y por eso vive acá y no en cada repositorio: resuelto por separado,
    /// dos correos «a Tesorería» podían terminar llegándole a gente distinta.
    ///
    /// El requisito es el mismo que abre la bandeja de Reembolsos: el rol y nada más. No se mira
    /// el puesto (la categoría Tesorero del <c>puesto_id</c> del trabajador): si se pidiera, el
    /// aviso dejaría fuera a gente que sí entra a la bandeja. El camino user_role → person →
    /// workers es el mismo con el que <see cref="CorreoSalidaRecipientResolver"/> resuelve un
    /// destinatario de tipo ROL.
    /// </summary>
    public static class CorreosTesoreriaLoader
    {
        /// <summary>Correos corporativos de quien tiene el rol TESORERO. Vacío si no lo tiene nadie.</summary>
        public static Task<List<string>> LoadAsync(AppDbContext ctx)
        {
            var rolTesorero = int.Parse(Roles.Tesorero);
            return (
                from w   in ctx.Worker
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId
                join ur  in ctx.UserRole on per.UserId equals (int?)ur.UserId
                where ur.RoleId == rolTesorero && ur.State && ur.Active
                   && w.EmailCorporativo != null && w.EmailCorporativo != ""
                select w.EmailCorporativo!
            ).Distinct().ToListAsync();
        }
    }
}
