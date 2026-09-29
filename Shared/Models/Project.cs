using Abril_Backend.Features.CostsModule.Shared.Models;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Constants;

namespace Abril_Backend.Shared.Models {
    public class Project {
        // Identidad
        public int ProjectId {get; set;}
        public string ProjectDescription {get; set;}
        public string? Codigo {get; set;}
        public string? Abbreviation {get; set;}
        public string? LevelDescription {get; set;}

        /// <summary>
        /// Qué es la fila: proyecto de verdad (edificio que se vende al público), FFT, la Oficina
        /// Central, un área interna o el proyecto de prueba. Ver <see cref="ProjectTipoIds"/> y, para
        /// «¿se trata como obra?», <see cref="ProjectTipo.EsObra"/>. Navegación en <see cref="Tipo"/>.
        /// </summary>
        public int ProjectTipoId {get; set;} = ProjectTipoIds.Proyecto;
        public ProjectTipo? Tipo {get; set;}

        /// <summary>
        /// Ciclo de vida: activo, finalizado o inactivo (<see cref="ProjectCicloVidaIds"/>). Es la
        /// única fuente: reemplazó a las columnas estado, activo y operativo. No confundir con
        /// <see cref="Active"/>, que es de sistema (si aparece en filtros y desplegables).
        /// </summary>
        public int ProjectCicloVidaId {get; set;} = ProjectCicloVidaIds.Activo;
        public ProjectCicloVida? CicloVida {get; set;}

        // Contribuyente / razón social
        public int? ContributorId {get; set;}
        public Contributor? Contributor {get; set;}

        // Ubicación
        public string? ProjectDistrict {get; set;}
        public string? ProjectProvince {get; set;}
        public string? ProjectDepartment {get; set;}
        public string? ProjectLocation {get; set;}

        // Responsables
        public string? ResponsableArqCom {get; set;}
        public int? ResponsableArqComId {get; set;}
        public string? ResponsableUdp {get; set;}
        public int? ResponsableUdpId {get; set;}
        public string? ResponsablePlaneamientoBim {get; set;}
        public int? ResponsablePlaneamientoBimId {get; set;}

        // Planeamiento BIM: meta de PPC pactada, fija por proyecto
        public decimal? MetaPpc {get; set;}

        // Residente: referencia al trabajador. Su correo se lee de
        // workers.email_corporativo al enviar, así sigue siempre al dato maestro.
        public int? ResidenteWorkersId {get; set;}

        /// <summary>
        /// Coordinador administrativo del proyecto: referencia al trabajador, igual que
        /// <see cref="ResidenteWorkersId"/>. Su correo se lee de
        /// <c>workers.email_corporativo</c> al enviar — no hay copia del texto que se
        /// desactualice cuando la persona cambia de correo o se retira.
        ///
        /// Reemplazó a la columna <c>email_coord_admin</c> (texto suelto), eliminada.
        /// Para leer el correo usar la navegación <see cref="CoordAdmin"/>.
        /// </summary>
        public int? WorkersCoordAdminId {get; set;}

        /// <summary>
        /// Ficha del coordinador administrativo. Es la fuente del correo:
        /// <c>CoordAdmin?.EmailCorporativo</c>. Traerla en la misma consulta
        /// (proyección o <c>Include</c>) para no caer en N+1.
        /// </summary>
        public Worker? CoordAdmin {get; set;}

        // Emails del proyecto
        /// <summary>
        /// DEPRECADO — reemplazado por <see cref="ResidenteWorkersId"/>. Se conserva
        /// solo como histórico (convención del proyecto: no se borran campos); ningún
        /// código lo lee. Para el residente usar la FK.
        /// </summary>
        public string? EmailResidente {get; set;}
        public string? EmailResponsable {get; set;}
        public string? EmailRrhh {get; set;}
        public string? EmailCoordSsoma {get; set;}
        public string? StaffEmail {get; set;}

        // Fechas del proyecto
        public DateOnly? FechaInicio {get; set;}
        public DateOnly? FechaFin {get; set;}
        public DateOnly? InicioObra {get; set;}
        public DateOnly? FinObra {get; set;}

        // Métricas físicas
        public string? NumNiveles {get; set;}
        public string? NumSotanos {get; set;}
        public string? Pisos {get; set;}
        public int? TiempoConstruccion {get; set;}
        public decimal? AreaM2 {get; set;}
        public decimal? AreaTechadaM2 {get; set;}
        public decimal? HhTotalCasa {get; set;}
        public string? CantTrabajadoresCasa {get; set;}
        /// <summary>HH_REAL | HH_PROYECTADO | HH_CALCULADO_MEDIANA</summary>
        public string? HhFuente {get; set;}

        // Contadores
        public int ContadorIncidentes {get; set;}
        public int ContadorAccidentes {get; set;}
        public int ContadorRac {get; set;}
        public int ContadorPenalidad {get; set;}

        // Flags
        public bool TieneArquitecturaComercial {get; set;}
        public bool TieneUnidadDeProyectos {get; set;}

        // Foto
        public string? FotoUrl {get; set;}

        /// <summary>
        /// Logo del proyecto (no confundir con <see cref="FotoUrl"/>, la foto de obra de BIM/SharePoint).
        /// Se imprime junto al logo ABRIL en el encabezado del PDF de Control de Licencias.
        /// </summary>
        public string? LogoUrl {get; set;}

        // Geolocalización (geofencing de Tareos)
        public decimal? Lat {get; set;}
        public decimal? Lng {get; set;}
        public decimal RadioGeofenceMetros {get; set;} = 300;

        // Auditoría
        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
        public bool State {get; set;}

        // Navegaciones
        public List<ResidentReportIncidence> Incidences { get; set; }
    }
}