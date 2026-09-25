namespace Abril_Backend.Shared.Constants
{
    /// <summary>
    /// IDs del catálogo <c>ga_actor_caso</c>: qué TIPO de trabajador es alguien a efectos de quiénes
    /// son sus actores (<see cref="ActorIds"/>). Se insertan con id explícito en
    /// <c>Migrations/Manual/20260925_GaActoresUnificados.sql</c>.
    ///
    /// Lo personalizado por área se guarda por caso: poner a alguien como aprobador de los
    /// trabajadores de oficina de un área no lo convierte en aprobador de su jefe.
    /// </summary>
    public static class ActorCasoIds
    {
        /// <summary>Trabajador que no es jefatura y cuya obra vigente es OFICINA CENTRAL (o ninguna).</summary>
        public const int OficinaCentral = 1;

        /// <summary>Trabajador que no es jefatura y cuya obra vigente es una obra.</summary>
        public const int Staff = 2;

        /// <summary>
        /// Categoría JEFE. Las categorías de gerencia también caen acá: a los gerentes se los resuelve
        /// como a una jefatura (los aprueba y los consolida un gerente, que en su gerencia es él mismo).
        /// </summary>
        public const int Jefe = 3;

        /// <summary>Categoría RESIDENTE.</summary>
        public const int Residente = 4;

        /// <summary>Categoría SUB GERENTE.</summary>
        public const int Subgerente = 5;

        /// <summary>Los cinco, en el orden en que se muestran.</summary>
        public static readonly int[] Todos = { OficinaCentral, Staff, Jefe, Residente, Subgerente };

        /// <summary>
        /// El caso de un trabajador: su categoría si es jefatura y, si no, dónde trabaja.
        /// Ficha sin puesto = sin categoría = trabajador normal.
        /// </summary>
        /// <param name="esObra">true si su obra vigente es una obra (no OFICINA CENTRAL ni ninguna).</param>
        public static int De(int? categoriaId, bool esObra) => categoriaId switch
        {
            CategoriaIds.Residente  => Residente,
            CategoriaIds.Jefe       => Jefe,
            CategoriaIds.SubGerente => Subgerente,
            CategoriaIds.Gerente
                or CategoriaIds.GerenteGeneral
                or CategoriaIds.GerenteAdministracionFinanzas => Jefe,
            _ => esObra ? Staff : OficinaCentral,
        };

        /// <summary>true = el caso es una jefatura: lo aprueba y lo firma un gerente.</summary>
        public static bool EsJefatura(int casoId) => casoId is Jefe or Residente or Subgerente;

        /// <summary>true = el caso es de un trabajador normal (oficina o staff).</summary>
        public static bool EsNormal(int casoId) => casoId is OficinaCentral or Staff;
    }
}
