using Abril_Backend.Features.ConvivirModule.Shared.Dtos;

namespace Abril_Backend.Features.ConvivirModule.Features.InicioFeature.Application.Dtos
{
    /// <summary>Pantalla Inicio de la app, en una sola llamada.</summary>
    public class ConvivirInicioDto
    {
        /// <summary>Para el saludo.</summary>
        public string? Nombres { get; set; }
        /// <summary>Para el selector de propiedad (el propietario puede tener varias).</summary>
        public List<ConvivirPropiedadDto> Propiedades { get; set; } = new();
        /// <summary>La propiedad que se muestra. Null si todavía no tiene propiedades.</summary>
        public int? PropietarioId { get; set; }
        /// <summary>Avance del proyecto de esa propiedad. Null si no tiene propiedades.</summary>
        public ConvivirAvanceDto? Avance { get; set; }
    }
}
