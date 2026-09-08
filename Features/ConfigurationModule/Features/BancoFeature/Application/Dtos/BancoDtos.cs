namespace Abril_Backend.Features.ConfigurationModule.Features.BancoFeature.Application.Dtos
{
    /// <summary>Una fila de la tabla de Configuración → Bancos.</summary>
    public class BancoDto
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public int Orden { get; set; }
        public bool Activo { get; set; }

        /// <summary>
        /// Razones sociales del grupo que hoy trabajan con este banco. Es lo que decide si se
        /// puede eliminar: un banco en uso no se borra, se desactiva.
        /// </summary>
        public int RazonesSociales { get; set; }
    }

    /// <summary>Alta o edición de un banco (el <c>codigo</c> solo se define al crearlo).</summary>
    public class BancoUpsertDto
    {
        public string? Codigo { get; set; }
        public string? Nombre { get; set; }
        public int Orden { get; set; }
        public bool Activo { get; set; } = true;
    }
}
