using Abril_Backend.Features.ConvivirModule.Shared.Dtos;

namespace Abril_Backend.Features.ConvivirModule.Features.MiProyectoFeature.Application.Dtos
{
    /// <summary>Pantalla Mi Proyecto de la app: el cronograma por hitos de la propiedad elegida.</summary>
    public class ConvivirMiProyectoDto
    {
        /// <summary>Null si todavía no tiene propiedades.</summary>
        public ConvivirPropiedadDto? Propiedad { get; set; }
        public ConvivirAvanceDto? Avance { get; set; }
        /// <summary>Los hitos que están en el cronograma vigente, en orden.</summary>
        public List<ConvivirHitoDto> Hitos { get; set; } = new();
    }
}
