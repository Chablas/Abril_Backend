namespace Abril_Backend.Features.CostsModule.Features.Configuration.PasosFeature.Application.Dtos
{
    /// <summary>Una opción configurable dentro de un paso: la etiqueta del checkbox y su valor.</summary>
    public class CostsPasoOptionDto
    {
        public int ProjectSubContractorStepOptionId { get; set; }
        /// <summary>Clave estable con la que el código consume la opción (no se muestra).</summary>
        public string OptionKey { get; set; } = null!;
        /// <summary>Etiqueta del checkbox.</summary>
        public string OptionDescription { get; set; } = null!;
        public bool Enabled { get; set; }
    }

    /// <summary>Un paso del flujo de adjudicaciones con las opciones que tiene configurables.</summary>
    public class CostsPasoDto
    {
        /// <summary>Id del estado = número del paso (1..9).</summary>
        public int StepNumber { get; set; }
        /// <summary>Descripción del paso tal como está en el catálogo de estados.</summary>
        public string StepDescription { get; set; } = null!;
        public List<CostsPasoOptionDto> Options { get; set; } = new();
    }

    /// <summary>Prender/apagar una opción desde la pantalla de configuración.</summary>
    public class CostsPasoOptionUpdateDto
    {
        public int ProjectSubContractorStepOptionId { get; set; }
        public bool Enabled { get; set; }
    }
}
