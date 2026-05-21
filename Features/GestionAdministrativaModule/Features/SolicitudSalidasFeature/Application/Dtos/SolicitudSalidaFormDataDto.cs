namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Dtos
{
    public class SolicitudSalidaFormDataDto
    {
        public List<MotivoSalidaDto> Motivos { get; set; } = new();
        public List<LugarSalidaDto> Lugares { get; set; } = new();

        /// <summary>Email de la jefatura que recibirá el email de aprobación. Null si no se pudo resolver.</summary>
        public string? AprobadorEmail { get; set; }
    }

    public class MotivoSalidaDto
    {
        public int Id { get; set; }
        public string Descripcion { get; set; } = string.Empty;
    }

    public class LugarSalidaDto
    {
        public int Id { get; set; }
        public string NombreDisplay { get; set; } = string.Empty;
        public bool EsLibre { get; set; }
    }
}
