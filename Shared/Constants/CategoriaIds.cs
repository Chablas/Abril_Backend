namespace Abril_Backend.Shared.Constants
{
    /// <summary>
    /// IDs del catálogo <c>categoria</c> (<c>workers.categoria_id</c>) sobre los que hay
    /// lógica de negocio. Los ids son idénticos en dev y prod (verificado el 2026-08-13:
    /// las 41 categorías coinciden id ↔ nombre en ambas), así que se pueden usar como
    /// constantes — mismo criterio que <see cref="CategoriaMaestraIds"/>.
    ///
    /// Solo están acá las categorías que el código realmente compara. El resto del
    /// catálogo (OPERARIO, PEÓN, ARQUITECTO…) es data pura: se muestra y se filtra desde
    /// la UI, pero ninguna regla depende de ellas, así que no tienen constante.
    ///
    /// Antes cada feature comparaba contra el NOMBRE (<c>c.Nombre == "GERENTE"</c>). Eso
    /// ataba la lógica a un texto que se puede editar desde Configuración → Categorías y
    /// Puestos: renombrar una categoría apagaba en silencio la funcionalidad que la
    /// buscaba. Comparando por id, renombrar es inofensivo.
    ///
    /// La categoría es el campo de LÓGICA; el puesto (<c>workers.puesto_id</c>) es solo
    /// presentación y nunca debe usarse para decidir nada.
    /// </summary>
    public static class CategoriaIds
    {
        /// <summary>
        /// Habilitación la usa para el ítem Vida Ley (los practicantes no lo llevan).
        /// Ojo: no es el único eje de "es practicante" — Reclutamiento usa
        /// <see cref="CategoriaMaestraIds.PracticantePrePro"/>, que es el vínculo laboral
        /// de la Data Maestra de GTH. Los dos criterios conviven a propósito por ahora.
        /// </summary>
        public const int Practicante = 4;

        /// <summary>Lecciones Aprendidas (alcance por proyecto) y Desempeño de Supervisores.</summary>
        public const int Residente = 8;

        /// <summary>
        /// Gestión de Salidas (ve su gerencia completa y puede aprobar sus propias
        /// solicitudes) y Revisores de Áreas.
        /// No incluye a <see cref="GerenteGeneral"/>, <see cref="GerenteAdministracionFinanzas"/>
        /// ni SUB GERENTE: son categorías distintas y hoy quedan fuera de esas reglas.
        /// </summary>
        public const int Gerente = 11;

        /// <summary>
        /// Revisores de Áreas (ve su propia área), aprobador de salidas y jefatura de
        /// Lecciones Aprendidas.
        /// </summary>
        public const int Jefe = 17;

        /// <summary>Aprobador de salidas (segundo en el walk-up, después del Jefe).</summary>
        public const int SubGerente = 29;

        /// <summary>
        /// Revisores de Áreas: ve su propia área. No incluye a
        /// <see cref="CoordinadorSsoma"/>, que es una categoría aparte.
        /// </summary>
        public const int Coordinador = 22;

        /// <summary>Destinatario del Flash Report de accidentes.</summary>
        public const int Medico = 26;

        /// <summary>Charlas: quién cuenta como staff supervisor.</summary>
        public const int Supervisor = 37;

        /// <summary>Destinatario del Flash Report de accidentes.</summary>
        public const int GerenteGeneral = 39;

        /// <summary>Destinatario del Flash Report de accidentes.</summary>
        public const int GerenteAdministracionFinanzas = 40;

        /// <summary>Desempeño de Supervisores: puede ocultar/mostrar tarjetas y empresas.</summary>
        public const int CoordinadorSsoma = 41;

        /// <summary>
        /// Categorías cuyo trabajador puede ver su propia área en Revisores de Áreas.
        /// </summary>
        public static readonly int[] ConVistaDeSuArea = { Jefe, Coordinador, Gerente };

        /// <summary>
        /// Categorías que pueden aprobar la salida de un trabajador regular, en el orden
        /// en que las busca el walk-up por el árbol de áreas (ApproverResolver, regla C).
        /// El índice define la prioridad, así que el orden importa.
        /// </summary>
        public static readonly int[] AprobadoresWalkUp = { Jefe, SubGerente, Coordinador };

        /// <summary>
        /// Categorías que cuentan como "jefatura" en la configuración de recordatorios de
        /// Lecciones Aprendidas (quién aparece y puede activarse en esa sección).
        /// Independiente de quién PUEDE aprobar una lección, que es solo el jefe asignado
        /// a mano en <c>workers.worker_lesson_jefe_id</c>.
        /// </summary>
        public static readonly int[] Jefaturas = { Jefe, Coordinador, Residente };

        /// <summary>
        /// Categorías que reciben el Flash Report de accidentes por sí mismas (además de
        /// las que entran por área/subárea/jefatura).
        /// </summary>
        public static readonly int[] DestinatariosFlashReport =
            { Medico, GerenteGeneral, GerenteAdministracionFinanzas };
    }
}
