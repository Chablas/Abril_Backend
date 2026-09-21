using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Models
{
    /// <summary>
    /// Configuración por área (nodo area_scope) para el módulo de salidas. Lleva dos flags:
    /// <see cref="FiltraPorProyecto"/> (si está activo, el área se "subdivide por proyecto" y sus
    /// revisores se asignan por proyecto — area_revisores.project_id — en vez de a nivel de área) y
    /// <see cref="CapturasObligatorias"/> (si está en false, los trabajadores del área rinden sin
    /// subir capturas de movilidad). Tabla desacoplada de la matriz base (no se tocan columnas de
    /// area_scope).
    ///
    /// Un área SIN fila acá usa los defaults (no filtra por proyecto, capturas obligatorias): por
    /// eso un área nueva no necesita que nadie la registre y la fila solo aparece cuando se cambia
    /// algo. Cada nodo es independiente: la configuración de un área NO se hereda a sus subáreas.
    /// </summary>
    [Table("ga_salidas_area_config")]
    public class GaSalidasAreaConfig
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("area_scope_id")]
        public int AreaScopeId { get; set; }

        /// <summary>Si true, el área se filtra por proyecto (se muestran subfilas por proyecto). Default false.</summary>
        [Column("filtra_por_proyecto")]
        public bool FiltraPorProyecto { get; set; }

        /// <summary>
        /// Solo tiene sentido con <see cref="FiltraPorProyecto"/> en true, y por eso la pantalla lo
        /// ofrece únicamente en esas áreas.
        ///
        /// En un área filtrada por proyecto conviven dos revisores: el de la OBRA (el residente, o
        /// el que se haya asignado a ese proyecto) y el del ÁREA entera. Por defecto el de la obra
        /// aprueba las salidas y el del área firma el consolidado y la planilla grupal —son
        /// documentos del área, no de una obra—. Con este flag en true la firma también baja a la
        /// obra: firma el mismo revisor por proyecto que aprueba las salidas.
        ///
        /// No trae lista propia: lee los revisores por proyecto que ya existen en Revisores de
        /// Áreas. Si el documento mezcla obras no hay un revisor de obra único y se cae al del área.
        /// </summary>
        [Column("firma_consolidado_por_proyecto")]
        public bool FirmaConsolidadoPorProyecto { get; set; }

        /// <summary>
        /// Si true (default), los trabajadores del área deben subir una captura de movilidad por
        /// cada trayecto antes de poder rendir la salida. En false, la salida se puede rendir de
        /// frente sin capturas. Se configura en Gestión Administrativa → Configuración → Capturas.
        /// </summary>
        [Column("capturas_obligatorias")]
        public bool CapturasObligatorias { get; set; } = true;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [Column("active")]
        public bool Active { get; set; } = true;

        /// <summary>Soft delete: false = eliminado (se conserva para auditoría).</summary>
        [Column("state")]
        public bool State { get; set; } = true;
    }
}
