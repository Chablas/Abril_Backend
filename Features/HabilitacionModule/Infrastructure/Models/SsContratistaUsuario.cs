using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Models
{
    [Table("ss_contratista_usuario")]
    public class SsContratistaUsuario
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("contractor_id")]
        public int ContractorId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("rol_id")]
        public int RolId { get; set; }

        [Column("scope")]
        public string Scope { get; set; } = "TODOS";

        [Column("activo")]
        public bool Activo { get; set; } = true;

        [Column("creado_en")]
        public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

        [Column("system_role_id")]
        public int? SystemRoleId { get; set; }

        [Column("creado_por")]
        public int? CreadoPor { get; set; }

        [Column("modulos")]
        public string Modulos { get; set; } = "AMBOS";

        [ForeignKey(nameof(RolId))]
        public SsContratistaRol? Rol { get; set; }

        public ICollection<SsContratistaUsuarioProyecto> Proyectos { get; set; } = new List<SsContratistaUsuarioProyecto>();
    }
}
