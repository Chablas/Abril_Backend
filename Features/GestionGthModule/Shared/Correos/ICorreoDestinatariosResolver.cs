namespace Abril_Backend.Features.GestionGthModule.Shared.Correos
{
    /// <summary>
    /// Única fuente de verdad de a quién le llega cada correo de Reclutamiento. Lee la
    /// configuración de la pantalla (correo prendido/apagado, destinatarios prendidos/apagados)
    /// y expande los destinatarios dinámicos al correo que corresponde en ese momento.
    ///
    /// La usan tanto el envío real como la previsualización del modal de nueva solicitud,
    /// justamente para que el aviso que ve el solicitante y el correo que sale no diverjan.
    /// </summary>
    public interface ICorreoDestinatariosResolver
    {
        /// <summary>
        /// Destinatarios efectivos de un correo. Devuelve las listas vacías si el correo está
        /// apagado con su interruptor maestro.
        /// </summary>
        /// <param name="tipoCodigo">Código estable del correo (<see cref="CorreoTipoGth"/>).</param>
        /// <param name="areaScopeId">
        /// <c>puesto.area_destino_scope_id</c> del solicitante, necesario para resolver al gerente de su
        /// área. Si es null (o su área no cuelga de ninguna gerencia con gerente registrado) esa
        /// fila simplemente no aporta a nadie.
        /// </param>
        Task<SolicitudDestinatariosDto> ResolverAsync(string tipoCodigo, int? areaScopeId = null);

        /// <summary>
        /// Lo mismo para varios correos a la vez, en una sola lectura de la configuración y
        /// resolviendo los destinatarios dinámicos una sola vez para todos. La usa quien tiene que
        /// mostrar (o mandar) más de un correo del mismo acto: el aviso del modal de nueva
        /// solicitud, donde qué correos salen depende del tipo de cada vacante.
        /// </summary>
        /// <returns>
        /// Un DTO por cada código pedido, incluidos los que no existen o están apagados: esos
        /// llegan con las listas vacías.
        /// </returns>
        Task<IReadOnlyDictionary<string, SolicitudDestinatariosDto>> ResolverVariosAsync(
            IReadOnlyList<string> tipoCodigos, int? areaScopeId = null);
    }
}
