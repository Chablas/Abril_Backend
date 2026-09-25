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

        /// <summary>
        /// Trabajador que no es jefatura, trabaja en una obra y administra alguna obra activa
        /// (<c>project.workers_coord_admin_id</c>). Es un cargo del proyecto, no una categoría. Como el
        /// staff, salvo que su 1.ª revisión la aprueba el residente, de sus salidas no se avisa a
        /// ningún jefe y su consolidado lo firma él mismo junto con el residente.
        /// </summary>
        public const int AdministradorObra = 6;

        /// <summary>Todos, en el orden en que se muestran.</summary>
        public static readonly int[] Todos = { OficinaCentral, Staff, AdministradorObra, Jefe, Residente, Subgerente };

        /// <summary>
        /// El caso de un trabajador: su categoría si es jefatura y, si no, dónde trabaja y si
        /// administra una obra. Ficha sin puesto = sin categoría = trabajador normal.
        /// </summary>
        /// <param name="esObra">true si su obra vigente es una obra (no OFICINA CENTRAL ni ninguna).</param>
        /// <param name="esAdministradorDeObra">true si administra alguna obra activa.</param>
        public static int De(int? categoriaId, bool esObra, bool esAdministradorDeObra = false) => categoriaId switch
        {
            CategoriaIds.Residente  => Residente,
            CategoriaIds.Jefe       => Jefe,
            CategoriaIds.SubGerente => Subgerente,
            CategoriaIds.Gerente
                or CategoriaIds.GerenteGeneral
                or CategoriaIds.GerenteAdministracionFinanzas => Jefe,
            _ => !esObra ? OficinaCentral : esAdministradorDeObra ? AdministradorObra : Staff,
        };

        /// <summary>true = el caso es una jefatura: lo aprueba y lo firma un gerente.</summary>
        public static bool EsJefatura(int casoId) => casoId is Jefe or Residente or Subgerente;

        /// <summary>true = el caso es de un trabajador que no es jefatura.</summary>
        public static bool EsNormal(int casoId) => casoId is OficinaCentral or Staff or AdministradorObra;

        /// <summary>
        /// true = el caso tiene jefe notificado de la salida: el staff y el administrador de obra, cuya
        /// salida aprueba el residente. En el resto el que aprueba ya es el jefe.
        /// </summary>
        public static bool TieneJefeNotificado(int casoId) => casoId is Staff or AdministradorObra;
    }
}
