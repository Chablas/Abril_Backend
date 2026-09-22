namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Emo
{
    public class ReconciliacionCertAptitudResultDto
    {
        public int TotalEvaluados { get; set; }
        public int TotalCorregidos { get; set; }
        public int Errores { get; set; }
        public List<string> Detalles { get; set; } = new();
    }
}
