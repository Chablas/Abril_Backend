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
        ///   <item><description><b>Apto</b> → <c>EMO_APTO</c>; <b>Apto con Restricciones</b> →
        ///   <c>EMO_APTO_RESTRICCIONES</c>. El proceso NO cierra acá: queda esperando a que GTH lo
        ///   cierre y lo pase a onboarding desde el detalle del requerimiento. Un requerimiento que
        ///   GTH ya cerró no vuelve atrás por reguardar un apto.</description></item>
        ///   <item><description><b>No Apto</b> → el seleccionado sale del proceso y el requerimiento
        ///   vuelve a manos de GTH: a <c>EMO_NO_APTO</c> si hay rechazados que retomar, y directo a
        ///   <c>LONG_LIST</c> si no queda ninguno, que es el único trabajo que le quedaría.</description></item>
        ///   <item><description><b>Observado</b> → <c>EMO_OBSERVADO</c>: el proceso queda a la
        ///   espera del resultado de la interconsulta. Ni cierra ni continúa con otro candidato,
        ///   porque este todavía puede resultar apto.</description></item>
        ///   <item><description>Cualquier otro valor (o el EMO sin calificar) → no se toca
        ///   nada.</description></item>
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

        /// <summary>
        /// Le copia al requerimiento la razón social que se le acaba de elegir a la ficha de
        /// pre-ingreso desde el modal "Programar EMO con clínica".
        ///
        /// La razón social se asigna en un solo punto del proceso —la programación del EMO de
        /// ingreso—, así que todo requerimiento llega ahí sin ninguna y esa elección es de las dos:
        /// la ficha (<c>workers.contributor_id</c>, que lo escribe quien llama) y el requerimiento,
        /// que es de donde la leen la carta oferta y el onboarding.
        ///
        /// Solo escribe si el requerimiento no tiene ninguna: reprogramar el EMO no cambia la razón
        /// social con la que el proceso ya quedó.
        ///
        /// <b>No guarda</b> ni lanza, por el mismo motivo que
        /// <see cref="AplicarAptitudAsync"/>: los cambios entran en el <c>SaveChanges</c> de quien
        /// llama y una cita médica no puede caerse por el estado de un requerimiento.
        /// </summary>
        /// <returns>true si el requerimiento se actualizó.</returns>
        Task<bool> SincronizarRazonSocialAsync(
            AppDbContext ctx, Worker worker, int contributorId, int? userId);

        /// <summary>
        /// ¿La vacante de la que sale esta ficha de pre-ingreso es un <b>REEMPLAZO</b>? Es lo único
        /// que levanta el tope de 20 trabajadores por razón social al asignarle una desde el EMO.
        ///
        /// <para>El motivo es de negocio y no técnico: quien reemplaza y quien se va conviven un
        /// mes —el que entra se empalma con el que sale—, así que durante ese tiempo la razón social
        /// tiene 21. El exceso lo cierra la baja del reemplazado en Habilitación, que devuelve la
        /// cuenta a 20 (ver <c>HabTrabajadorRepository.BajaAsync</c>). Cortar por el tope acá
        /// dejaría el proceso trabado justo en el caso en que el tope ya está previsto que se
        /// pase.</para>
        ///
        /// <para>Vale igual para un reemplazo por ingreso directo FFT: el tipo de requerimiento y el
        /// flujo son dos cosas distintas.</para>
        ///
        /// <para>false para cualquier ficha que no sea de pre-ingreso o que no venga de un
        /// requerimiento: sin vacante de reemplazo detrás, el tope es el de siempre.</para>
        /// </summary>
        Task<bool> EsReemplazoAsync(AppDbContext ctx, Worker worker);
    }
}
