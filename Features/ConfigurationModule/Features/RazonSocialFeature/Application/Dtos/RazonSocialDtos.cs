namespace Abril_Backend.Features.ConfigurationModule.Features.RazonSocialFeature.Application.Dtos
{
    /// <summary>Una fila de la tabla de Configuración → Razones Sociales.</summary>
    public class RazonSocialDto
    {
        public int Id { get; set; }
        public string? Nombre { get; set; }
        public string? Ruc { get; set; }
        public string? Direccion { get; set; }
        public string? PartidaRegistral { get; set; }
        public string? TipoActividad { get; set; }
        public bool Activo { get; set; }

        /// <summary>true = es una empresa del grupo Abril (no un contratista ni un proveedor).</summary>
        public bool EsAbril { get; set; }

        /// <summary>
        /// Banco con el que trabaja. Solo lo tienen las del grupo: es el que el formulario de
        /// bienvenida le nombra al nuevo colaborador al preguntarle si quiere su cuenta sueldo.
        /// </summary>
        public int? BancoId { get; set; }
        public string? BancoNombre { get; set; }
    }

    /// <summary>
    /// Todo lo que la pantalla necesita al entrar, en una sola petición: la tabla completa
    /// (activas e inactivas, el filtro es de pantalla) y el catálogo de bancos que llena el
    /// desplegable de los modales.
    /// </summary>
    public class RazonSocialBandejaDto
    {
        public List<RazonSocialDto> RazonesSociales { get; set; } = new();
        public List<BancoOpcionDto> Bancos { get; set; } = new();
    }

    /// <summary>Una opción del desplegable «Banco».</summary>
    public class BancoOpcionDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }

    /// <summary>Alta de una razón social. Los datos de identidad salen de la consulta a SUNAT.</summary>
    public class RazonSocialCreateDto
    {
        public string Ruc { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public string Direccion { get; set; } = null!;
        public string TipoActividad { get; set; } = null!;
        public string Distrito { get; set; } = null!;
        public string Provincia { get; set; } = null!;
        public string Departamento { get; set; } = null!;
        public string? PartidaRegistral { get; set; }

        public bool EsAbril { get; set; }

        /// <summary>Solo se guarda cuando <see cref="EsAbril"/> es true (lo exige un CHECK en la base).</summary>
        public int? BancoId { get; set; }
    }

    /// <summary>
    /// Edición de una razón social. Solo viaja lo que se puede corregir: el RUC, el nombre y la
    /// partida registral vienen de SUNAT y quedan en solo lectura — cambiarlos sería otra empresa,
    /// no una corrección.
    /// </summary>
    public class RazonSocialUpdateDto
    {
        public string? Direccion { get; set; }
        public string? TipoActividad { get; set; }
        public bool Activo { get; set; } = true;
        public bool EsAbril { get; set; }
        public int? BancoId { get; set; }
    }
}
