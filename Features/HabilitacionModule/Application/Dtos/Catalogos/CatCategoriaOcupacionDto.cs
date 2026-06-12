namespace Abril_Backend.Features.Habilitacion.Application.Dtos.Catalogos
{
    public class CatCategoriaDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }

    public class CatOcupacionDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }

    public class CatCategoriaAdminDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public int Orden { get; set; }
        public bool Activo { get; set; }
    }

    public class CatOcupacionAdminDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public int Orden { get; set; }
        public bool Activo { get; set; }
    }

    public class CatNombreRequest
    {
        public string Nombre { get; set; } = "";
    }

    public class CatToggleRequest
    {
        public bool Activo { get; set; }
    }
}
