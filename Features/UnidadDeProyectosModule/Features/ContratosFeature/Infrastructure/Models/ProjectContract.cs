namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Infrastructure.Models {
    /// <summary>Contrato de locación de servicios con un consultor/proyectista de diseño (Unidad de
    /// Proyectos) — distinto de los contratos de subcontratistas de construcción de Adjudicaciones
    /// (Costos), aunque reutiliza sus mismas tablas de Contractor/Contributor y WorkSpecialty.</summary>
    public class ProjectContract {
        public int ProjectContractId {get; set;}
        public int ProjectId {get; set;}
        /// <summary>FK a Contractor (Shared/Models) — mismo contratista que usa Adjudicaciones,
        /// puede ser persona natural (RUC 10) o jurídica (RUC 20).</summary>
        public int ContractorId {get; set;}
        /// <summary>FK a WorkSpecialty (Costos/Configuration) — especialidad de diseño (Instalaciones
        /// Eléctricas, Sanitarias, Estructuras, Arquitectura, etc.). Determina qué plantilla .docx
        /// se usa: Arquitectura/Estructuras tiene la suya, el resto comparte la genérica.</summary>
        public int WorkSpecialtyId {get; set;}
        public int ProjectContractStatusId {get; set;} = 1;

        /// <summary>Número secuencial del contrato (sin abreviatura de proyecto ni año — esos se
        /// componen al generar el documento, igual que ContractNumber en ProjectSubContractor).</summary>
        public int? ContractNumber {get; set;}
        /// <summary>Título corto del servicio para la plantilla ({{TIPO_SERVICIO}}), ej.
        /// "Instalaciones Eléctricas".</summary>
        public string? ServiceDescription {get; set;}
        public decimal Amount {get; set;}
        public int CurrencyId {get; set;}
        public string? ContractorEmail {get; set;}

        public DateOnly? SigningDate {get; set;}
        public DateOnly? StartDate {get; set;}
        public DateOnly? EndDate {get; set;}
        public int? TermDays {get; set;}

        /// <summary>Texto libre pegado por quien arma el contrato (cláusula Cuarta: alcance de
        /// servicios por especialidad) — no se genera, se copia de la Propuesta/Anexo 01 aprobada.
        /// Placeholder {{DETALLE_SERVICIOS}} en la plantilla.</summary>
        public string? DetalleServicios {get; set;}

        /// <summary>Nombre de la carpeta de este contrato en SharePoint ("CONTRATO N° X") — se
        /// asigna la primera vez que se sube un documento y se reutiliza (mismo mecanismo que
        /// AdjudicacionFolderName), dentro de {CarpetaConfigurada}/{Especialidad}/{RUC - Razón Social}/.</summary>
        public string? FolderName {get; set;}

        // Último .docx generado (paso 3) y subido a SharePoint.
        public string? ContractFileUrl {get; set;}
        public string? ContractOriginalFileName {get; set;}
        public string? ContractStorageItemId {get; set;}

        // Paso 4 (Envío al contratista) — true cuando el envío se omitió porque ya se había
        // mandado por correo fuera del sistema (mismo mecanismo que ScNotificationSkipped en
        // ProjectSubContractor). El contrato avanza al paso 5 sin que el sistema envíe nada.
        public bool ContractorNotificationSkipped {get; set;}

        // Paso 5 (Llegada a Oficina Central)
        public bool? ArrivedWithObservations {get; set;}
        public string? ArrivalObservation {get; set;}

        // Paso 6 (Procesos de firma) — estado persistido de cada checkbox. Firmantes acordados
        // para Contratos (distintos de los 3 de Adjudicaciones): Jefe de Proyectos, Gerente
        // Inmobiliario, Gerente General.
        public bool Step6SignedJefeProyectos {get; set;}
        public bool Step6SignedGerenteInmobiliario {get; set;}
        public bool Step6SignedGerenteGeneral {get; set;}

        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
        public bool State {get; set;}
    }
}
