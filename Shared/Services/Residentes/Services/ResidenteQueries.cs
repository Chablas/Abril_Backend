using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;

namespace Abril_Backend.Shared.Services.Residentes.Services
{
    /// <summary>
    /// La regla del residente como consultas componibles: se meten dentro de otra consulta del mismo
    /// contexto (subconsulta, sin N+1). Es su única definición; <see cref="ResidenteProyectoResolver"/>
    /// y los repositorios que necesitan la regla dentro de su propia consulta usan estas.
    /// </summary>
    public static class ResidenteQueries
    {
        private static readonly int RolResidenteId = int.Parse(Roles.Residente);

        /// <summary>Proyectos cuyo residente es la persona del usuario. Worker lleva el filtro global
        /// de <c>state</c>: una ficha de baja no cuenta.</summary>
        public static IQueryable<int> ProyectosDelResidente(this AppDbContext ctx, int userId) =>
            from p in ctx.Project
            join w in ctx.Worker on p.ResidenteWorkersId equals (int?)w.Id
            join pe in ctx.Person on w.PersonId equals (int?)pe.PersonId
            where pe.UserId == userId
            select p.ProjectId;

        /// <summary>
        /// Universo de obras con residente (PLAN-RESIDENTES.md §3.2-3): proyecto visible, de un tipo
        /// que es obra, en ciclo ACTIVO y con un residente cuya persona tiene usuario vigente con el
        /// rol RESIDENTE. Una obra nueva entra sola al ponerle residente. Reemplaza a la tabla
        /// antigua <c>project_resident</c>, que se llenaba con SQL a mano.
        /// </summary>
        public static IQueryable<int> ObrasConResidente(this AppDbContext ctx) =>
            from p in ctx.Project
            where p.Active && p.State
               && p.Tipo!.EsObra
               && p.ProjectCicloVidaId == ProjectCicloVidaIds.Activo
            join w in ctx.Worker on p.ResidenteWorkersId equals (int?)w.Id
            join pe in ctx.Person on w.PersonId equals (int?)pe.PersonId
            join u in ctx.User on pe.UserId equals (int?)u.UserId
            where u.State
               && ctx.UserRole.Any(ur => ur.UserId == u.UserId && ur.RoleId == RolResidenteId && ur.State)
            select p.ProjectId;
    }
}
