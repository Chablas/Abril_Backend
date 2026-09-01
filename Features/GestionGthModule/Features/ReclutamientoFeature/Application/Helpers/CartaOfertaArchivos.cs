namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Helpers
{
    /// <summary>
    /// Cómo se llaman los dos archivos de la carta oferta dentro del file del colaborador.
    ///
    /// Los dos llevan nombre ESTABLE —sin el sello de tiempo que usa el resto del file— porque de
    /// cada uno hay exactamente uno por carta y a los dos se los vuelve a escribir: el .docx cuando
    /// GTH regenera la carta, y el PDF cuando el colaborador abre su enlace por primera vez y hay
    /// que estamparle la fecha de conformidad. Con nombre estable esa segunda escritura reemplaza al
    /// archivo anterior; con sello de tiempo dejaría dos versiones del mismo documento en la carpeta
    /// y GTH tendría que adivinar cuál es la buena.
    ///
    /// Vive aparte de <see cref="CartaOfertaPlantilla"/> porque lo usan las dos features del flujo:
    /// el envío (que crea los archivos) y la página pública de firma (que rehace el PDF).
    /// </summary>
    public static class CartaOfertaArchivos
    {
        /// <summary>El Word de trabajo, el que GTH revisa y corrige antes de enviar.</summary>
        public static string Docx(string codigoRequerimiento) =>
            $"carta_oferta_{codigoRequerimiento}.docx";

        /// <summary>El PDF que se le manda al colaborador y que él firma.</summary>
        public static string Pdf(string codigoRequerimiento) =>
            $"carta_oferta_{codigoRequerimiento}.pdf";
    }
}
