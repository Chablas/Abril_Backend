using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Infrastructure.Models
{
    [Table("companies")]
    public class Empresa
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("id_razon_social")]
        public int? IdRazonSocial { get; set; }

        [Column("razon_social")]
        public string? RazonSocial { get; set; }

        [Column("ruc")]
        public string? Ruc { get; set; }

        [Column("direccion")]
        public string? Direccion { get; set; }

        [Column("partida_registral")]
        public string? PartidaRegistral { get; set; }

        [Column("tipo_actividad")]
        public string? TipoActividad { get; set; }

        [Column("activa")]
        public bool? Activa { get; set; }

        [Column("created_at")]
        public DateTimeOffset? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
