using Abril_Backend.Shared.Services.Revisores.Interfaces;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// A quién le toca firmar AHORA un documento que firman varios.
    ///
    /// Las firmas del Consolidado del S10 son en CADENA y no simultáneas: en una obra firma primero
    /// el administrador y después el residente, y el segundo no se entera hasta que el primero
    /// firmó. Es la misma idea que <c>RutaAprobacion.LeTocaAhora</c> en las aprobaciones de GTH,
    /// donde el reemplazo le aparece a GTH recién cuando el gerente del área lo aprobó.
    ///
    /// Vive acá —y no dentro de un repositorio— porque lo miran tres cosas que tienen que decir lo
    /// mismo: el correo que sale al adjuntar el consolidado, el que sale solo cuando alguien firma,
    /// y la previsualización que la pantalla muestra antes de confirmar. Quien decide el orden es
    /// <see cref="AprobadorDocumento.Orden"/>, que ya viene resuelto del algoritmo o de
    /// <c>area_revisores_rendicion</c>.
    /// </summary>
    public static class FirmaEnTurno
    {
        /// <summary>
        /// Los aprobadores a los que hoy les toca: el primero que todavía no firmó (y cualquier
        /// otro que comparta su lugar en el orden). Vacío si ya firmaron todos.
        ///
        /// Un aprobador sin ficha —el fallback de GTH, que es un área— no se puede contrastar
        /// contra las firmas puestas: cuenta siempre como pendiente, y como en ese caso es el único
        /// del documento, el resultado es él mismo.
        /// </summary>
        /// <param name="yaFirmaron"><c>workers.id</c> de los que ya estamparon su firma.</param>
        public static List<AprobadorDocumento> De(
            IEnumerable<AprobadorDocumento> aprobadores, IReadOnlySet<int> yaFirmaron)
        {
            var pendientes = aprobadores
                .Where(a => a.Persona.WorkerId == null || !yaFirmaron.Contains(a.Persona.WorkerId.Value))
                .OrderBy(a => a.Orden)
                .ToList();

            if (pendientes.Count == 0) return new List<AprobadorDocumento>();

            var turno = pendientes[0].Orden;
            return pendientes.Where(a => a.Orden == turno).ToList();
        }
    }
}
