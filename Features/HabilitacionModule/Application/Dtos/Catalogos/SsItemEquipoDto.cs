namespace Abril_Backend.Features.Habilitacion.Application.Dtos.Catalogos
{
    public class SsItemEquipoDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public bool RequiereVigencia { get; set; }
        public int Orden { get; set; }
        public bool Activo { get; set; }
    }
}
