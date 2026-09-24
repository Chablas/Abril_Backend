namespace Abril_Backend.Shared.Constants
{
    /// <summary>
    /// IDs del catálogo <c>puesto</c> sobre los que hay lógica de negocio que no se puede
    /// resolver por categoría (ver <see cref="CategoriaIds"/>) porque el puesto no tiene una
    /// categoría propia — comparte una genérica con puestos de otras áreas.
    /// </summary>
    public static class PuestoIds
    {
        /// <summary>
        /// Jefe de Seguridad y Salud en el Trabajo ("Jefe SSOMA"). A diferencia de
        /// Coordinador SSOMA (categoria_id 41) y Prevencionista (categoria_id 35), este
        /// puesto no tiene categoría propia: su categoría es la genérica "JEFE" (17), que
        /// también usan jefaturas de cualquier otra área (Costos, RRHH, etc.), así que no
        /// sirve para identificarlo. Solo existe un puesto con este nombre en el catálogo,
        /// así que se referencia directo por su id — tan estable como una categoría, y
        /// evita depender de un rol de sistema (antes role_id 9) que en la práctica estaba
        /// asignado a ~50 cuentas de todas las áreas y no reflejaba quién es realmente el
        /// Jefe SSOMA.
        /// </summary>
        public const int JefeSsoma = 189;

        /// <summary>
        /// Puestos de staff evaluables en la Evaluación 360° de Staff: el Residente
        /// (categoria_id CategoriaIds.Residente) evalúa a todo el staff de SU proyecto
        /// cuyo puesto esté en esta lista. Ids fijados por Comité/GTH — no completar
        /// ni recortar sin que venga de ahí. Clave = puesto_id, valor = nombre de
        /// referencia (documentación; el nombre real y editable vive en `puesto`).
        /// </summary>
        public static readonly Dictionary<int, string> StaffEvaluablePuestoIds = new()
        {
            { 281, "Prevencionista de Riesgos" },
            { 28, "Arquitecto de Calidad" },
            { 161, "Ingeniero de Oficina Técnica" },
            { 158, "Ingeniero de Calidad" },
            { 9, "Administrador de Obra" },
            { 57, "Asistente de Producción" },
            { 119, "Coordinador SSOMA" },
            { 163, "Ingeniero de Producción" },
            { 162, "Ingeniero de Planeamiento BIM" },
            { 31, "Arquitecto de Producción" },
            { 55, "Asistente de Oficina Técnica" },
            { 43, "Asistente de Calidad" },
            { 103, "Coordinador Administrativo de Obra" },
            { 169, "Ingeniero Practicante" },
            { 27, "Arquitecto Coordinador de Obra" },
            { 301, "Supervisor de Instalaciones" },
        };
    }
}
