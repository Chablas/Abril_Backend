using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Models
{
    [Table("ss_item_equipo")]
    public class SsItemEquipo
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public bool RequiereVigencia { get; set; } = false;
        public int Orden { get; set; }
        public bool Activo { get; set; } = true;
    }
}
