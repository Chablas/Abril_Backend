namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Dtos
{
    /// <summary>Una captura nueva del lote: a qué trayecto entra, con qué imagen y por cuánto.</summary>
    public class CapturaNuevaInput
    {
        public int TrayectoId { get; set; }
        public decimal Monto { get; set; }
        public IFormFile File { get; set; } = default!;
    }

    /// <summary>
    /// Un cambio sobre una captura que ya estaba subida: su monto y, solo si el trabajador eligió
    /// otra imagen, el archivo que la reemplaza. Sin <see cref="File"/> se guarda nada más el monto.
    /// </summary>
    public class CapturaEdicionInput
    {
        public int CapturaId { get; set; }
        public decimal Monto { get; set; }
        public IFormFile? File { get; set; }
    }

    /// <summary>
    /// Todo lo que el modal de capturas guarda de una vez: lo nuevo de cualquiera de los trayectos
    /// de la salida y lo corregido sobre lo que ya estaba. Va junto porque en la pantalla es un
    /// solo botón "Guardar", y porque así la carpeta de SharePoint se resuelve una sola vez para
    /// todo el lote en vez de una por fila.
    /// </summary>
    public class GuardarCapturasInput
    {
        public List<CapturaNuevaInput> Nuevas { get; set; } = new();
        public List<CapturaEdicionInput> Ediciones { get; set; } = new();
    }
}
