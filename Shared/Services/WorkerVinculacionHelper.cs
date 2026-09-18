using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Shared.Services
{
    /// <summary>
    /// Los tramos de <c>worker_vinculaciones</c> (ver <see cref="WorkerVinculacion"/>) que se
    /// parten fuera de "Cambiar obra / puesto de trabajo".
    ///
    /// Vive acá por lo mismo que <see cref="WorkersPeriodoLaboralHelper"/>: la edición de la
    /// ficha existe en SSOMA y en Habilitación, y las dos tienen que partir el tramo igual.
    /// Tampoco llama a SaveChanges: muta el contexto de quien llama.
    /// </summary>
    public static class WorkerVinculacionHelper
    {
        /// <summary>
        /// El tramo vigente de la ficha, o null si no tiene ninguno abierto. Mismo orden que
        /// el resto del backend: <c>created_at DESC, id DESC</c>.
        /// </summary>
        public static Task<WorkerVinculacion?> VigenteAsync(AppDbContext ctx, int workerId) =>
            ctx.WorkerVinculacion
                .Where(v => v.WorkerId == workerId && v.FechaFin == null)
                .OrderByDescending(v => v.CreatedAt)
                .ThenByDescending(v => v.Id)
                .FirstOrDefaultAsync();

        /// <summary>
        /// Deja en el historial un cambio de puesto o de clasificación (Obra / Staff / Oficina
        /// Central) hecho desde una EDICIÓN de la ficha. Sin esto la ficha cambiaba y el tramo
        /// vigente seguía diciendo el puesto anterior, así que no había forma de saber qué
        /// puesto (ni qué área, que sale del puesto) tuvo el trabajador en cada momento.
        ///
        /// Se llama después de asignarle a la ficha los valores nuevos y antes del
        /// SaveChanges, pasando los que tenía antes:
        /// <list type="bullet">
        ///   <item>Si no cambió nada, no hace nada.</item>
        ///   <item>Si la ficha no tiene tramo abierto, tampoco: una ficha de pre-ingreso no
        ///   tiene vinculación a propósito (es lo que la aísla del resto del sistema) y un
        ///   retirado no tiene tramo vigente que partir.</item>
        ///   <item>Llenar un dato que estaba vacío, o corregir un tramo que empezó hoy, se
        ///   escribe sobre el tramo vigente: no hay un "antes" que conservar.</item>
        ///   <item>Cualquier otro cambio cierra el tramo vigente hoy y abre uno nuevo, con la
        ///   misma razón social, el mismo proyecto y el mismo periodo laboral.</item>
        /// </list>
        /// </summary>
        public static async Task RegistrarCambioDeFichaAsync(
            AppDbContext ctx, Worker ficha, int? puestoAnteriorId, int? obraOficinaStaffAnteriorId,
            DateTimeOffset ahora)
        {
            var cambioPuesto = ficha.PuestoId != puestoAnteriorId;
            var cambioClasificacion = ficha.ObraOficinaStaffId != obraOficinaStaffAnteriorId;
            if (!cambioPuesto && !cambioClasificacion) return;

            var vigente = await VigenteAsync(ctx, ficha.Id);
            if (vigente == null) return;

            // El nombre y la categoría se siguen congelando en el tramo porque Convalidaciones
            // los lee para reconstruir el puesto de origen de un EMO.
            var puesto = ficha.PuestoId == null
                ? null
                : await ctx.Puesto
                    .Where(p => p.PuestoId == ficha.PuestoId.Value)
                    .Select(p => new { p.Nombre, p.CategoriaId })
                    .FirstOrDefaultAsync();

            // Hoy en hora de Perú y no del servidor: en prod corre en UTC y un cambio hecho de
            // noche abriría el tramo al día siguiente.
            var hoy = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));

            var soloRellena = (!cambioPuesto || puestoAnteriorId == null)
                && (!cambioClasificacion || obraOficinaStaffAnteriorId == null);

            if (soloRellena || vigente.FechaInicio >= hoy)
            {
                if (cambioPuesto)
                {
                    vigente.PuestoId = ficha.PuestoId;
                    vigente.Puesto = puesto?.Nombre;
                    vigente.CategoriaId = puesto?.CategoriaId;
                }
                if (cambioClasificacion)
                    vigente.ObraOficinaStaffId = ficha.ObraOficinaStaffId;
                vigente.UpdatedAt = ahora;
                return;
            }

            // Un tramo viejo que todavía no tiene periodo (lo creó el backend anterior y el
            // script aún no lo completó) toma el vigente de la ficha.
            var periodoId = vigente.WorkersPeriodoLaboralId
                ?? (await WorkersPeriodoLaboralHelper.VigenteAsync(ctx, ficha.Id))?.WorkersPeriodoLaboralId;

            vigente.FechaFin = hoy;
            vigente.UpdatedAt = ahora;

            ctx.WorkerVinculacion.Add(new WorkerVinculacion
            {
                WorkerId = ficha.Id,
                WorkersPeriodoLaboralId = periodoId,
                EmpresaId = vigente.EmpresaId,
                ProyectoId = vigente.ProyectoId,
                TipoVinculacion = vigente.TipoVinculacion,
                PuestoId = ficha.PuestoId,
                Puesto = puesto?.Nombre,
                CategoriaId = puesto?.CategoriaId,
                ObraOficinaStaffId = ficha.ObraOficinaStaffId,
                FechaInicio = hoy,
                CreatedAt = ahora,
            });
        }
    }
}
