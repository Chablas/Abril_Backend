namespace Abril_Backend.Features.CostsModule.Shared.Constants
{
    /// <summary>
    /// Claves de las opciones por paso de la sección "Pasos" (Configuración de Costos).
    /// Tienen que coincidir con project_sub_contractor_step_option.option_key: la fila es
    /// el dato (etiqueta + valor), esta constante es cómo el código la pide.
    /// </summary>
    public static class CostsStepOptionKeys
    {
        /// <summary>
        /// Paso 4 — permite volver a generar el contrato completo cuando la adjudicación
        /// YA pasó del paso 4. No reabre el envío del correo al SC: solo el archivo.
        /// </summary>
        public const string Paso4PermitirRegenerarPaquete = "paso4.permitir-regenerar-paquete";
    }
}
