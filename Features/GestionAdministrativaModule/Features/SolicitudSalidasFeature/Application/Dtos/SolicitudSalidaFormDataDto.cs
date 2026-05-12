namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Dtos
{
    public class SolicitudSalidaFormDataDto
    {
        public List<HoraOpcionDto> Horas { get; set; } = new();
        public List<MotivoSalidaDto> Motivos { get; set; } = new();
        public List<LugarSalidaDto> Lugares { get; set; } = new();
    }

    public class HoraOpcionDto
    {
        public int Id { get; set; }
        public string Etiqueta { get; set; } = string.Empty;
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
