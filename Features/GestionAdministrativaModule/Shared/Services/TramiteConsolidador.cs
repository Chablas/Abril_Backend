namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Quién SIGUE el trámite de una planilla grupal ya preparada y de su consolidado: subir el S10,
    /// avisar a la jefatura, pedir la corrección al ERP y reemplazarlo.
    ///
    /// Varios consolidadores pueden EMPEZAR —cualquiera que sea consolidador de todos los
    /// trabajadores prepara una planilla grupal—, pero la planilla que uno agrupó la sigue solo él
    /// hasta el final (pedido del 2026-09-25): dos consolidadores trabajando el mismo documento se
    /// pisaban el S10. La única salida es que quien la agrupó ya no pueda consolidar por esa gente
    /// (se fue, cambió de área o le quitaron el papel): entonces la retoma cualquier consolidador,
    /// para que el documento no quede trabado.
    ///
    /// Es una regla sin base de datos: quien la usa ya tiene resuelto a quién puede consolidar cada
    /// uno (<c>IConsolidadorResolver.FiltrarQuePuedenConsolidarAsync</c>, una sola llamada para el
    /// usuario y los dueños de toda una tabla).
    /// </summary>
    public static class TramiteConsolidador
    {
        /// <param name="userId">El usuario que quiere hacer el trámite.</param>
        /// <param name="duenoUserId">
        /// Quien agrupó: <c>ga_planilla_grupal.preparada_por_id</c> antes del S10 y
        /// <c>ga_consolidado_s10.uploaded_by_id</c> después. Null = nadie (documento viejo).
        /// </param>
        /// <param name="trabajadores">Los trabajadores de todas las planillas que cubre el documento.</param>
        /// <param name="habilitadoUsuario">Los trabajadores que el usuario puede consolidar.</param>
        /// <param name="habilitadoDueno">Los que puede consolidar quien agrupó.</param>
        public static bool PuedeSeguir(
            int userId,
            int? duenoUserId,
            IReadOnlyCollection<int> trabajadores,
            IReadOnlySet<int> habilitadoUsuario,
            IReadOnlySet<int>? habilitadoDueno)
        {
            if (trabajadores.Count == 0 || !trabajadores.All(habilitadoUsuario.Contains)) return false;
            if (duenoUserId == null || duenoUserId == userId) return true;

            // Quien lo agrupó ya no puede seguirlo: lo retoma cualquier consolidador.
            return habilitadoDueno == null || !trabajadores.All(habilitadoDueno.Contains);
        }

        /// <summary>
        /// true si quien agrupó sigue pudiendo consolidar por todos: entonces nadie más continúa el
        /// trámite. Es la mitad de <see cref="PuedeSeguir"/> que no depende de quién pregunta.
        /// </summary>
        public static bool DuenoSigue(
            IReadOnlyCollection<int> trabajadores, IReadOnlySet<int>? habilitadoDueno)
            => habilitadoDueno != null && trabajadores.Count > 0 && trabajadores.All(habilitadoDueno.Contains);
    }
}
