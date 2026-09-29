using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Models;

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

        /// <summary>Las obras del residente en el módulo de Residentes (incidencias, IVTs, cuaderno de
        /// obra): las suyas, visibles y sin excluir de RESIDENTES. Es donde ve, sube y responde.</summary>
        public static IQueryable<Project> ObrasDelResidenteEnResidentes(this AppDbContext ctx, int userId)
        {
            var delUsuario = ctx.ProyectosDelResidente(userId);
            return ctx.VisiblesEnResidentes().Where(p => delUsuario.Contains(p.ProjectId));
        }

        /// <summary>Las obras del módulo de Residentes, para filtros y combos: el universo de obras con
        /// residente, visibles y sin excluir de RESIDENTES.</summary>
        public static IQueryable<Project> ObrasEnResidentes(this AppDbContext ctx)
        {
            var obras = ctx.ObrasConResidente();
            return ctx.VisiblesEnResidentes().Where(p => obras.Contains(p.ProjectId));
        }

        /// <summary>Lo que muestra el módulo de Residentes: proyectos visibles que no se excluyeron de
        /// RESIDENTES en el filtro por funcionalidad (<c>proyecto_filtro</c>).</summary>
        private static IQueryable<Project> VisiblesEnResidentes(this AppDbContext ctx) =>
            ctx.Project.Where(p => p.Active
                && !ctx.ProyectoFiltro.Any(f => f.ProjectId == p.ProjectId
                    && f.FuncionalidadId == ProyectoFiltroFuncionalidades.Residentes && !f.Active));
    }
}
