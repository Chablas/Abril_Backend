namespace Abril_Backend.Features.GestionAdministrativa.MotivosSalida.Application.Dtos
{
    public class GaMotivoSalidaConfigItemDto
    {
        public int Id { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public bool Activo { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class GaMotivoSalidaCreateDto
    {
        public string Descripcion { get; set; } = string.Empty;
    }

    public class GaMotivoSalidaEditDto
    {
        public string Descripcion { get; set; } = string.Empty;
    }
}
