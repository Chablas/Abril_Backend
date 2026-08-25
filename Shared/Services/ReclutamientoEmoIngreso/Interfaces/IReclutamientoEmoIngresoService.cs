using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;

namespace Abril_Backend.Shared.Services.ReclutamientoEmoIngreso.Interfaces
{
    /// <summary>
    /// El enlace entre el resultado de un EMO de Ingreso y el requerimiento de Reclutamiento que
    /// dejó a esa persona como finalista aprobado. Es lo que decide si el proceso de selección
    /// termina o vuelve atrás.
    ///
    /// Vive en <c>Shared</c> porque lo cruzan dos módulos: la regla es de Reclutamiento
    /// (GestionGthModule) pero quien la dispara es Salud Ocupacional (SsomaModule), que es donde se
    /// registra la aptitud del examen. Ninguno de los dos puede ser el dueño del archivo.
    /// </summary>
    public interface IReclutamientoEmoIngresoService
    {
        /// <summary>
        /// Aplica al requerimiento la aptitud con la que quedó el EMO de Ingreso del trabajador:
        ///
        /// <list type="bullet">
        ///   <item><description><b>Apto</b> / <b>Apto con Restricciones</b> → el proceso CIERRA. Es
        ///   el paso que antes disparaba el solo hecho de agendar la cita.</description></item>
        ///   <item><description><b>No Apto</b> → el seleccionado sale del proceso y el requerimiento
        ///   vuelve a manos de GTH: a <c>EMO_NO_APTO</c> si hay rechazados que retomar, y directo a
        ///   <c>LONG_LIST</c> si no queda ninguno, que es el único trabajo que le quedaría.</description></item>
        ///   <item><description><b>Observado</b> (u otra) → no se toca nada: la aptitud final la
        ///   define la interconsulta y hasta entonces el proceso espera.</description></item>
        /// </list>
        ///
        /// <b>No guarda</b>: deja los cambios en el <paramref name="ctx"/> que se le pasa para que
        /// entren en el mismo <c>SaveChanges</c> que el EMO que los provocó. Y no lanza nunca: si
        /// algo no calza (la persona no viene de un proceso de reclutamiento, el requerimiento ya
        /// avanzó, falta un código del catálogo) no toca nada y devuelve false. Un examen médico ya
        /// registrado no puede fallar por el estado de un requerimiento.
        /// </summary>
        /// <param name="worker">
        /// Ficha del trabajador del EMO, ya cargada por quien llama. Solo se actúa si sigue siendo
        /// de pre-ingreso (<c>FINALISTA_APROBADO</c>): una vez que la persona firma y entra, su
        /// requerimiento es historia y ningún EMO posterior debe moverlo.
        /// </param>
        /// <param name="tipoEmoNombre">
        /// Nombre del tipo de EMO ("Ingreso", "Periódico Anual"…). Solo el de Ingreso mueve el
        /// proceso de selección.
        /// </param>
        /// <param name="aptitud">Aptitud registrada en el EMO.</param>
        /// <param name="userId">Usuario que registró el resultado, para la trazabilidad.</param>
        /// <returns>true si el requerimiento cambió de fase.</returns>
        Task<bool> AplicarAptitudAsync(
            AppDbContext ctx, Worker worker, string? tipoEmoNombre, string? aptitud, int? userId);
    }
}
