namespace Abril_Backend.Features.Habilitacion.Application.Dtos
{
    public class RetiroAutomaticoResultDto
    {
        public int TotalRetirados { get; set; }
        public int TotalAvisados { get; set; }
        public List<string> Detalles { get; set; } = [];
        /// <summary>True cuando corrió en modo "solo aviso" (RetiroAutomatico:SoloAviso=true en
        /// config): se enviaron los correos pero NO se ejecutó ningún retiro real.</summary>
        public bool SoloAviso { get; set; }
    }
}
