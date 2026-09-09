namespace Abril_Backend.Features.GestionAdministrativa.Shared.Dtos
{
    /// <summary>
    /// Un correo que una acción va a disparar, con sus destinatarios REALES ya resueltos con
    /// Configuración → Correos. La pantalla los imprime en la confirmación para que quien decide
    /// vea a quién le va a llegar antes de apretar el botón.
    ///
    /// Se devuelve una LISTA de estos y no un objeto suelto porque una acción puede disparar más
    /// de un correo a públicos distintos: aprobar el reembolso avisa al solicitante Y a Tesorería.
    /// Lista vacía = esa acción hoy no manda ningún correo (apagado o sin destinatarios), y la
    /// confirmación lo dice en vez de prometer un envío que no va a pasar.
    ///
    /// Sale del MISMO resolver que hace el envío, así que la confirmación no puede quedar
    /// desalineada de la configuración. No replicar ese cálculo en el frontend.
    /// </summary>
    public class CorreoAvisoPreviewDto
    {
        /// <summary>
        /// Qué correo es, para que dos avisos de la misma acción no se lean como uno solo
        /// ("Al solicitante", "A Tesorería"). Se imprime delante de las direcciones.
        /// </summary>
        public string Etiqueta { get; set; } = "";

        public List<string> Para { get; set; } = new();
        public List<string> Copia { get; set; } = new();
    }
}
