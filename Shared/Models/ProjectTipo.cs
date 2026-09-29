using System.ComponentModel.DataAnnotations;

namespace Abril_Backend.Shared.Models
{
    /// <summary>
    /// project_tipo — qué es una fila de <c>project</c>. Proyecto de verdad es solo PROYECTO (un
    /// edificio que se vende al público); el resto son filas que no se pueden borrar porque otras
    /// pantallas dependen de ellas: FFT (edificaciones personales del gerente general), la Oficina
    /// Central, áreas de la empresa registradas como proyecto (Post Venta, Arquitectura Comercial,
    /// Eventos) y el proyecto de prueba (Torre Abril).
    ///
    /// Los ids son fijos (los inserta <c>20260928_ProyectosTipoYCicloVida.sql</c>): ver
    /// <see cref="Constants.ProjectTipoIds"/>.
    /// </summary>
    public class ProjectTipo
    {
        [Key]
        public int ProjectTipoId { get; set; }

        /// <summary>PROYECTO, FFT, OFICINA_CENTRAL, AREA_INTERNA o PRUEBA.</summary>
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        /// <summary>Qué significa, para mostrarlo junto al desplegable.</summary>
        public string? Descripcion { get; set; }

        /// <summary>
        /// Los proyectos de este tipo se tratan como obra (se construyen): PROYECTO y FFT, y PRUEBA
        /// para poder probar ahí lo mismo que en una obra. La Oficina Central y las áreas internas,
        /// no. Preferir esta columna a enumerar ids: un tipo nuevo no obliga a tocar el código.
        /// </summary>
        public bool EsObra { get; set; }

        public int Orden { get; set; }

        public DateTime CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;
    }
}
