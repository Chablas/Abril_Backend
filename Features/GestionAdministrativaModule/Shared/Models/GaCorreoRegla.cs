using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Models
{
    /// <summary>
    /// Regla de destinatario de un correo del flujo de salidas (<see cref="GaCorreoEvento"/>).
    /// Cada fila es un destinatario al que se le envía ese correo; <see cref="Active"/> lo prende
    /// y lo apaga sin borrarlo. La lista de exclusiones ("nunca se enviará a") se dio de baja en
    /// septiembre de 2026: no había forma de saber por qué alguien estaba excluido y la misma
    /// exclusión se lograba apagando o quitando su fila.
    ///
    /// Según <see cref="TipoId"/> (ver <see cref="GaCorreoTipoDestinatario"/>) se llena UNO solo de:
    /// <list type="bullet">
    ///   <item>TRABAJADOR → <see cref="WorkerId"/> (se resuelve a workers.email_corporativo).</item>
    ///   <item>AREA → <see cref="AreaScopeId"/> (se expande a los email_corporativo de los
    ///     trabajadores del nodo; si <see cref="IncluirDescendientes"/>, también los de sus sub-áreas).</item>
    ///   <item>CORREO → <see cref="Correo"/> (dirección literal; puede ser un grupo de correos opaco).</item>
    /// </list>
    /// </summary>
    [Table("ga_correo_regla")]
    public class GaCorreoRegla
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>Correo al que aplica la regla (ga_correo_evento.id).</summary>
        [Column("evento_id")]
        public int EventoId { get; set; }

        /// <summary>Tipo de destinatario (ga_correo_tipo_destinatario.id).</summary>
        [Column("tipo_id")]
        public int TipoId { get; set; }

        /// <summary>Trabajador (workers.id) cuando el tipo es TRABAJADOR. NULL en otro caso.</summary>
        [Column("worker_id")]
        public int? WorkerId { get; set; }

        /// <summary>Nodo de área (area_scope.area_scope_id) cuando el tipo es AREA. NULL en otro caso.</summary>
        [Column("area_scope_id")]
        public int? AreaScopeId { get; set; }

        /// <summary>Dirección de correo literal cuando el tipo es CORREO. NULL en otro caso.</summary>
        [Column("correo")]
        public string? Correo { get; set; }

        /// <summary>Solo para AREA: si true, incluye también a los trabajadores de las sub-áreas del nodo.</summary>
        [Column("incluir_descendientes")]
        public bool IncluirDescendientes { get; set; } = true;

        /// <summary>Orden de visualización dentro del correo.</summary>
        [Column("orden")]
        public int Orden { get; set; }

        /// <summary>Si false, la regla se ignora (pausar sin borrar).</summary>
        [Column("active")]
        public bool Active { get; set; } = true;

        /// <summary>Soft delete: false = eliminado (se conserva para auditoría).</summary>
        [Column("state")]
        public bool State { get; set; } = true;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
