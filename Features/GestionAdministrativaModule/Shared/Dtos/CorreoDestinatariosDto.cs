namespace Abril_Backend.Features.GestionAdministrativa.Shared.Dtos
{
    /// <summary>
    /// Destinatarios REALES de un correo del flujo, ya resueltos con la configuración de
    /// Configuración → Correos: el interruptor del correo, el del destinatario principal y los
    /// destinatarios agregados a mano. Salen del MISMO cálculo que hace el envío, así que la
    /// pantalla no promete un correo a alguien que la configuración dejó fuera.
    ///
    /// <see cref="Para"/> vacío = ese correo hoy no le llega a nadie.
    ///
    /// Vive en el Shared del módulo porque lo usan Mis Rendiciones (los avisos que dispara el
    /// trabajador) y Gestión de Rendiciones (el aviso de la decisión del reembolso).
    /// </summary>
    public class CorreoDestinatariosDto
    {
        public List<string> Para { get; set; } = new();
        public List<string> Copia { get; set; } = new();
    }
}
