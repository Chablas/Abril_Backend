using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Infrastructure.Models
{
    [Table("workers")]
    public class Worker
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("id_trabajador")]
        public int? IdTrabajador { get; set; }

        [Column("apellido_nombre")]
        public string? ApellidoNombre { get; set; }

        [Column("dni")]
        public string? Dni { get; set; }

        [Column("ruc")]
        public string? Ruc { get; set; }

        [Column("celular")]
        public string? Celular { get; set; }

        [Column("email_personal")]
        public string? EmailPersonal { get; set; }

        [Column("email_corporativo")]
        public string? EmailCorporativo { get; set; }

        [Column("fecha_nacimiento")]
        public DateOnly? FechaNacimiento { get; set; }

        [Column("fecha_ingreso")]
        public DateOnly? FechaIngreso { get; set; }

        [Column("fecha_retiro")]
        public DateOnly? FechaRetiro { get; set; }

        [Column("categoria")]
        public string? Categoria { get; set; }

        [Column("ocupacion")]
        public string? Ocupacion { get; set; }

        [Column("area")]
        public string? Area { get; set; }

        [Column("subarea")]
        public string? Subarea { get; set; }

        [Column("contrata_casa")]
        public string? ContrataCasa { get; set; }

        [Column("obra_oficina")]
        public string? ObraOficina { get; set; }

        [Column("jefatura")]
        public string? Jefatura { get; set; }

        [Column("estado")]
        public string? Estado { get; set; }

        [Column("habilitado_obra")]
        public bool? HabilitadoObra { get; set; }

        [Column("sctr")]
        public bool? Sctr { get; set; }

        [Column("condicion_medica")]
        public string? CondicionMedica { get; set; }

        [Column("procedencia")]
        public string? Procedencia { get; set; }

        [Column("notas")]
        public string? Notas { get; set; }

        [Column("puntos_infraccion")]
        public int? PuntosInfraccion { get; set; }

        [Column("created_at")]
        public DateTimeOffset? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
