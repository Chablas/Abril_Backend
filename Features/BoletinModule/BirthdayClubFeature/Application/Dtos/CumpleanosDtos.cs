namespace Abril_Backend.Features.BoletinModule.BirthdayClubFeature.Application.Dtos
{
    /// <summary>
    /// Una persona cumpleañera del trimestre. El cumpleaños sale de <c>person.cumpleanos</c>
    /// y, si es null, de <c>workers.fecha_nacimiento</c>. Solo se incluye si el trabajador
    /// tiene un <c>email_personal</c> con dominio @abril.pe.
    /// </summary>
    public class CumpleaneroDto
    {
        public int WorkerId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string? Ocupacion { get; set; }
        public string Email { get; set; } = string.Empty;

        /// <summary>Mes del cumpleaños (1-12).</summary>
        public int Mes { get; set; }

        /// <summary>Día del cumpleaños (1-31).</summary>
        public int Dia { get; set; }

        /// <summary>Foto en data URI base64, o null si Graph no devolvió foto.</summary>
        public string? FotoBase64 { get; set; }
    }

    /// <summary>Cumpleañeros de un trimestre (1-4) listos para pintar en el calendario.</summary>
    public class TrimestreCumpleanosDto
    {
        public int Trimestre { get; set; }
        public List<CumpleaneroDto> Cumpleaneros { get; set; } = new();
    }
}
