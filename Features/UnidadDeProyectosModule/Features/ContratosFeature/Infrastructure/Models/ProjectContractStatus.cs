namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Infrastructure.Models {
    /// <summary>Catálogo de los 9 pasos del flujo de contratos de Unidad de Proyectos — paralelo a
    /// ProjectSubContractorStatus (Adjudicaciones), con un solo ajuste de negocio: el paso 8
    /// notifica al correo de Unidad de Proyectos en vez de a Staff de Obra.
    /// 1. Cotización/comparativo · 2. Datos del contrato · 3. Generación de documentos + VoBo ·
    /// 4. Envío al contratista · 5. Llegada a Of. Central · 6. Procesos de firma ·
    /// 7. Contrato firmado escaneado · 8. Notificación a Unidad de Proyectos · 9. Cierre.</summary>
    public class ProjectContractStatus {
        public int ProjectContractStatusId {get; set;}
        public string ProjectContractStatusDescription {get; set;} = null!;
    }
}
