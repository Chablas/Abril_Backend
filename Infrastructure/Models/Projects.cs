using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Infrastructure.Models
{
    [Table("projects")]
    public class Projects
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("id_proyecto")]
        public int? IdProyecto { get; set; }

        [Column("nombre")]
        public string? Nombre { get; set; }

        [Column("codigo")]
        public string? Codigo { get; set; }

        [Column("company_id")]
        public int? CompanyId { get; set; }

        [Column("estado")]
        public string? Estado { get; set; }

        [Column("responsable_arq_com")]
        public string? ResponsableArqCom { get; set; }

        [Column("responsable_arq_com_id")]
        public int? ResponsableArqComId { get; set; }

        [Column("email_residente")]
        public string? EmailResidente { get; set; }

        [Column("email_responsable")]
        public string? EmailResponsable { get; set; }

        [Column("email_rrhh")]
        public string? EmailRrhh { get; set; }

        [Column("email_coord_ssoma")]
        public string? EmailCoordSsoma { get; set; }

        [Column("email_coord_admin")]
        public string? EmailCoordAdmin { get; set; }

        [Column("fecha_inicio")]
        public DateOnly? FechaInicio { get; set; }

        [Column("fecha_fin")]
        public DateOnly? FechaFin { get; set; }

        [Column("inicio_obra")]
        public DateOnly? InicioObra { get; set; }

        [Column("fin_obra")]
        public DateOnly? FinObra { get; set; }

        [Column("num_niveles")]
        public int? NumNiveles { get; set; }

        [Column("num_sotanos")]
        public int? NumSotanos { get; set; }

        [Column("pisos")]
        public int? Pisos { get; set; }

        [Column("tiempo_construccion")]
        public int? TiempoConstruccion { get; set; }

        [Column("area_m2")]
        public decimal? AreaM2 { get; set; }

        [Column("area_techada_m2")]
        public decimal? AreaTechadaM2 { get; set; }

        [Column("hh_total_casa")]
        public decimal? HhTotalCasa { get; set; }

        [Column("cant_trabajadores_casa")]
        public int? CantTrabajadoresCasa { get; set; }

        [Column("contador_incidentes")]
        public int? ContadorIncidentes { get; set; }

        [Column("contador_accidentes")]
        public int? ContadorAccidentes { get; set; }

        [Column("contador_rac")]
        public int? ContadorRac { get; set; }

        [Column("tiene_arquitectura_comercial")]
        public bool? TieneArquitecturaComercial { get; set; }

        [Column("activo")]
        public bool Activo { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        public List<ResidentReportIncidence> Incidences { get; set; } = new();
    }
}
