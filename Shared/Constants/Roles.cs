namespace Abril_Backend.Shared.Constants
{
    /// <summary>
    /// IDs de los roles de la tabla <c>role</c> (producción), expresados como string
    /// porque así viajan en el claim del JWT y así los consumen <c>[Authorize(Roles = ...)]</c>
    /// y <c>User.IsInRole(...)</c>.
    ///
    /// Se usan IDs y NO nombres a propósito: el nombre de un rol puede editarse desde el
    /// CRUD de roles; si la autorización dependiera del nombre, ese cambio la rompería en
    /// silencio. El ID es estable.
    ///
    /// El JWT emite el ID en <c>ClaimTypes.Role</c> (ver JWTService) — por eso estas
    /// constantes son los IDs. Para mostrar el nombre del rol existe el claim aparte
    /// <c>role_name</c>.
    ///
    /// Espejo del archivo del frontend <c>src/app/core/constants/roles.ts</c>: mantener
    /// ambos alineados.
    ///
    /// Nota: CONTRATISTA (11) y CLINICA (14) también se usan como discriminador de tipo de
    /// sesión vía el claim <c>tipo</c>; para "¿es contratista/clínica?" preferir ese claim.
    /// </summary>
    public static class Roles
    {
        public const string AdministradorSistema              = "1";  // ADMINISTRADOR DEL SISTEMA
        public const string AdministradorUdp                  = "2";  // ADMINISTRADOR DE UDP
        public const string UsuarioUdp                        = "3";  // USUARIO DE UDP
        public const string AdministradorResidentes           = "4";  // ADMINISTRADOR DE RESIDENTES
        public const string Residente                         = "5";  // RESIDENTE
        public const string CostosOficinaCentral              = "6";  // USUARIO DE COSTOS Y PRESUPUESTOS DE OFICINA CENTRAL
        public const string CostosAdministrador               = "7";  // ADMINISTRADOR DE COSTOS Y PRESUPUESTOS
        public const string UsuarioArquitecturaComercial      = "8";  // USUARIO DE ARQUITECTURA COMERCIAL
        public const string AdministradorSsoma                = "9";  // JEFE SSOMA (antes ADMINISTRADOR SSOMA)
        public const string AdministradorAdministracion       = "10"; // ADMINISTRADOR ADMINISTRACION
        public const string Contratista                       = "11"; // CONTRATISTA
        public const string UsuarioDeAbril                    = "12"; // USUARIO DE ABRIL
        public const string Clinica                           = "14"; // CLINICA
        // 15 (ADMINISTRADOR DE GESTIÓN ADMINISTRATIVA) eliminado el 2026-07-14; lo reemplaza el 76.
        public const string ContabilidadFirmante              = "16"; // USUARIO FIRMANTE DE FACTURAS DE CONTABILIDAD
        public const string AdministradorMejoraContinua       = "48"; // ADMINISTRADOR DE MEJORA CONTINUA
        public const string ServicioVigilancia                = "49"; // SERVICIO DE VIGILANCIA
        public const string GestorArquitecturaComercial       = "51"; // GESTOR DE ARQUITECTURA COMERCIAL
        public const string UsuarioRecepcion                  = "52"; // USUARIO DE RECEPCIÓN
        public const string SaludOcupacional                  = "53"; // MÉDICO OCUPACIONAL (antes SALUD OCUPACIONAL)
        public const string VisualizadorEvaluaciones          = "56"; // VISUALIZADOR DE EVALUACIONES
        public const string Evaluador                         = "57"; // EVALUADOR
        public const string AdministradorEvaluaciones         = "58"; // ADMINISTRADOR DE EVALUACIONES
        public const string AdministradorDeObra               = "60"; // ADMINISTRADOR DE OBRA
        public const string CostosOficinaTecnica              = "61"; // USUARIO DE COSTOS Y PRESUPUESTOS DE OFICINA TÉCNICA
        public const string UsuarioVecinos                    = "62"; // USUARIO DE VECINOS
        public const string AdministradorObraVecinos          = "63"; // ADMINISTRADOR DE OBRA DE VECINOS
        public const string ContabilidadUsuario               = "64"; // USUARIO DE CONTABILIDAD
        public const string VisualizadorDashboardResidentes   = "65"; // VISUALIZADOR DE DASHBOARD RESIDENTES
        public const string UsuarioTrabajadores               = "66"; // USUARIO DE TRABAJADORES
        public const string CoordinadorSsoma                  = "70"; // COORDINADOR SSOMA
        public const string AsistentaSocial                   = "71"; // ASISTENTA SOCIAL
        public const string Prevencionista                    = "72"; // PREVENCIONISTA
        public const string ContratistaSupervisorCampo        = "74"; // CONTRATISTA - SUPERVISOR DE CAMPO
        // 76 (ADMINISTRADOR DE SOLICITUD DE SALIDAS) y 78 (USUARIO REVISOR DE SALIDAS) eliminados el
        // 2026-09-29: el acceso a Gestión Administrativa sale de los roles de la función (JEFE, SUB
        // GERENTE, GERENTE, RESIDENTE, ADMINISTRADOR DE OBRA, CONSOLIDADOR). Ver RolesPorFuncion.
        public const string UsuarioGth                        = "77"; // USUARIO DE GTH
        public const string PlaneamientoUdp                   = "80"; // PLANEAMIENTO UDP

        /// <summary>
        /// Las jefaturas de Gestión Administrativa. JEFE y GERENTE los creó producción (dev se alineó
        /// con sus ids); SUB GERENTE lo fija <c>20260929_GaRolesPorFuncion.sql</c>. Aprueban salidas,
        /// revisan planillas, firman consolidados y consolidan lo suyo. Además JEFE elige a los
        /// consolidadores de oficina central de su área (Revisores de Áreas).
        /// </summary>
        public const string Jefe                              = "81"; // JEFE
        public const string Gerente                           = "82"; // GERENTE
        public const string SubGerente                        = "94"; // SUB GERENTE

        /// <summary>
        /// TESORERO. Alcanza con tenerlo: concede sus features como cualquier otro rol. Durante un
        /// tiempo exigió además un puesto de categoría <c>CategoriaIds.Tesorero</c> (46) y esa
        /// condición extra se quitó de <c>AuthRepository.GetAllowedFeaturesAsync</c>.
        /// </summary>
        public const string Tesorero                          = "83"; // TESORERO

        /// <summary>
        /// COORDINADOR ERP. Único rol que entra a "Correcciones S10", la bandeja donde atiende las
        /// solicitudes de corrección del Consolidado del S10 (§10.5 del requerimiento de salidas).
        /// Alcanza con tenerlo, como el resto: no se le exige ningún puesto ni categoría.
        /// </summary>
        public const string CoordinadorErp                    = "84"; // COORDINADOR ERP

        /// <summary>
        /// CONSOLIDADOR. Lo administra el sistema (<c>IRolesPorFuncionService</c>): lo tiene exactamente
        /// quien figura A MANO como consolidador —en Revisores de Áreas o en la ficha de un trabajador—
        /// y no entra ya a Gestión de Rendiciones y Consolidados por otro rol. Se recalcula cada vez
        /// que se guardan actores, así que asignarlo a mano desde Seguridad no dura.
        /// </summary>
        public const string Consolidador                      = "95"; // CONSOLIDADOR

        /// <summary>
        /// COORDINADOR DE ADMINISTRACIÓN DE OBRA: las bandejas de Gestión Administrativa para quien
        /// coordina a los administradores de obra sin ser jefatura (ve las obras por su área,
        /// «Administración de Obra»). Se asigna a mano; ningún código lo compara.
        /// </summary>
        public const string CoordinadorAdministracionObra     = "96"; // COORDINADOR DE ADMINISTRACIÓN DE OBRA

        /// <summary>
        /// Los tres roles que administran el Cronograma de Hitos. Sus IDs los fija el SQL
        /// <c>20260928_ProyectosRolesYHistorialResidente.sql</c> (el sequence les había dado IDs
        /// distintos en cada ambiente). Además crean y editan proyectos y asignan su residente:
        /// ver <see cref="ProyectoRoles"/>.
        /// </summary>
        public const string CoordinadorProyectos              = "91"; // COORDINADOR DE PROYECTOS
        public const string GerenteInmobiliario               = "92"; // GERENTE INMOBILIARIO
        public const string JefeProyectos                     = "93"; // JEFE DE PROYECTOS
    }
}
