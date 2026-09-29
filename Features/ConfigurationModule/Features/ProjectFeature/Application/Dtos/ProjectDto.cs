namespace Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Dtos
{
    public class ProjectDto
    {
        public int ProjectId { get; set; }
        public string ProjectDescription { get; set; } = null!;
        public string? Codigo { get; set; }
        public string? Abbreviation { get; set; }
        public string? LevelDescription { get; set; }

        /// <summary>Qué es: proyecto de verdad, FFT, Oficina Central, área interna o prueba (catálogo project_tipo, ProjectTipoIds).</summary>
        public int ProjectTipoId { get; set; }
        public string ProjectTipoCodigo { get; set; } = string.Empty;
        public string ProjectTipoNombre { get; set; } = string.Empty;

        /// <summary>
        /// Ciclo de vida: activo, finalizado o inactivo (catálogo project_ciclo_vida,
        /// ProjectCicloVidaIds). No confundir con <see cref="Active"/>.
        /// </summary>
        public int ProjectCicloVidaId { get; set; }
        public string ProjectCicloVidaCodigo { get; set; } = string.Empty;
        public string ProjectCicloVidaNombre { get; set; } = string.Empty;

        // Contribuyente
        public int? ContributorId { get; set; }
        public string? ContributorRuc { get; set; }
        public string? ContributorName { get; set; }
        public string? ContributorAddress { get; set; }
        public string? ContributorDistrict { get; set; }
        public string? ContributorProvince { get; set; }
        public string? ContributorDepartment { get; set; }
        public string? ContributorLegalEntityRegistryNumber { get; set; }

        // Ubicación del proyecto
        public string? ProjectDistrict { get; set; }
        public string? ProjectProvince { get; set; }
        public string? ProjectDepartment { get; set; }
        public string? ProjectLocation { get; set; }

        // Responsable
        public string? ResponsableArqCom { get; set; }
        public int? ResponsableArqComId { get; set; }
        public string? ResponsableUdp { get; set; }
        public int? ResponsableUdpId { get; set; }
        public string? ResponsablePlaneamientoBim { get; set; }
        public int? ResponsablePlaneamientoBimId { get; set; }

        // Coordinador administrativo (FK a workers; el correo se resuelve en vivo)
        public int? WorkersCoordAdminId { get; set; }
        /// <summary>Nombre del coordinador administrativo, para pintarlo en el modal sin buscarlo en la lista.</summary>
        public string? CoordAdminNombre { get; set; }
        public string? CoordAdminEmail { get; set; }

        // Residente (FK a workers; el correo se resuelve en vivo). Nombre y correo van resueltos
        // para que el modal los muestre sin pedir la lista de trabajadores.
        public int? ResidenteWorkersId { get; set; }
        public string? ResidenteNombre { get; set; }
        public string? ResidenteEmail { get; set; }

        // Correos de aviso (texto)
        public string? EmailResponsable { get; set; }
        public string? EmailRrhh { get; set; }
        public string? EmailCoordSsoma { get; set; }

        // Fechas
        public DateOnly? FechaInicio { get; set; }
        public DateOnly? FechaFin { get; set; }
        public DateOnly? InicioObra { get; set; }
        public DateOnly? FinObra { get; set; }

        // Métricas físicas
        public string? NumNiveles { get; set; }
        public string? NumSotanos { get; set; }
        public string? Pisos { get; set; }
        public int? TiempoConstruccion { get; set; }
        public decimal? AreaM2 { get; set; }
        public decimal? AreaTechadaM2 { get; set; }
        public decimal? HhTotalCasa { get; set; }
        public string? CantTrabajadoresCasa { get; set; }

        // Flags
        public bool? TieneArquitecturaComercial { get; set; }
        public bool TieneUnidadDeProyectos { get; set; }

        // Geolocalización (geofencing de Tareo — Arquitectura Comercial)
        public decimal? Lat { get; set; }
        public decimal? Lng { get; set; }
        public decimal RadioGeofenceMetros { get; set; }

        /// <summary>Columna de sistema: si el proyecto aparece en filtros y desplegables.</summary>
        public bool Active { get; set; }
    }
}
