using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Models
{
    /// <summary>
    /// Configuración por área (nodo area_scope) para el módulo de salidas:
    /// <see cref="CapturasObligatorias"/> (si está en false, los trabajadores del área rinden sin
    /// subir capturas de movilidad). Tabla desacoplada de la matriz base (no se tocan columnas de
    /// area_scope).
    ///
    /// Hasta el 2026-09-25 llevaba también "filtrar por proyecto", una casilla que decidía si el
    /// área se subdividía por obra en Revisores de Áreas. Ya no se marca: la pantalla lo deduce de
    /// dónde trabaja la gente del área y el algoritmo de los actores mira la obra de cada
    /// trabajador. La columna se bota en <c>20260925_GaActoresUnificados_PostDeploy.sql</c>.
    ///
    /// Un área SIN fila acá usa los defaults (capturas obligatorias): por eso un área nueva no
    /// necesita que nadie la registre y la fila solo aparece cuando se cambia algo. Cada nodo es
    /// independiente: la configuración de un área NO se hereda a sus subáreas.
    /// </summary>
    [Table("ga_salidas_area_config")]
    public class GaSalidasAreaConfig
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("area_scope_id")]
        public int AreaScopeId { get; set; }

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
