using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Models
{
    /// <summary>
    /// Catálogo de las SECCIONES en las que se reparten los correos configurables dentro de la
    /// pantalla de Configuración que los administra:
    ///
    /// <list type="bullet">
    ///   <item>CORREOS — los del flujo, que dispara una acción de alguien (crear la solicitud,
    ///     aprobarla, firmar la planilla, pagar…).</item>
    ///   <item>RECORDATORIOS — los que dispara el cron del plazo de rendición: nadie los provoca,
    ///     salen porque llegó el día.</item>
    /// </list>
    ///
    /// Es ortogonal a <see cref="GaCorreoPantalla"/>: la pantalla dice DÓNDE se administra el
    /// correo y el grupo dice EN QUÉ SECCIÓN de esa pantalla aparece. Los dos recordatorios se
    /// administran en Solicitud de Salidas —igual que el aviso al revisor y la confirmación al
    /// solicitante— pero en su propia sección, porque no son parte del ciclo de una solicitud.
    /// </summary>
    [Table("ga_correo_grupo")]
    public class GaCorreoGrupo
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>Clave estable de la sección (CORREOS, RECORDATORIOS). Ver <c>CorreoGrupoCodigos</c>.</summary>
        [Column("codigo")]
        public string Codigo { get; set; } = string.Empty;

        /// <summary>Nombre de la sección, como se muestra en la tira de pestañas.</summary>
        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        /// <summary>Orden de visualización.</summary>
        [Column("orden")]
        public int Orden { get; set; }

        /// <summary>false = la sección ya no se dibuja.</summary>
        [Column("active")]
        public bool Active { get; set; } = true;

        /// <summary>Soft delete: false = eliminada (se conserva para auditoría).</summary>
        [Column("state")]
        public bool State { get; set; } = true;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
