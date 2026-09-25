namespace Abril_Backend.Features.Habilitacion.Application.Dtos.Catalogos
{
    /// <summary>
    /// Los cinco actores de un trabajador tal como los muestra su ficha: para cada uno, lo que le
    /// toca por su área (lo personalizado en Revisores de Áreas o el algoritmo) y lo personalizado en
    /// su propia ficha, si lo tiene. La ficha no decide nada: muestra lo uno o lo otro según el
    /// checkbox "Personalizado".
    /// </summary>
    public class ActoresTrabajadorDto
    {
        /// <summary><c>ActorCasoIds</c>: qué tipo de trabajador es (oficina central, staff, jefe…).</summary>
        public int CasoId { get; set; }
        public string CasoNombre { get; set; } = string.Empty;

        /// <summary>Los cinco, en el orden del flujo.</summary>
        public List<ActorTrabajadorDto> Actores { get; set; } = new();
    }

    public class ActorTrabajadorDto
    {
        /// <summary><c>ActorIds</c>.</summary>
        public int ActorId { get; set; }
        public string Nombre { get; set; } = string.Empty;

        /// <summary>true = admite varias personas (consolidadores, aprobadores del consolidado).</summary>
        public bool Multiple { get; set; }

        /// <summary>false = el actor no existe para este trabajador (el jefe notificado fuera del staff).</summary>
        public bool Aplica { get; set; }

        /// <summary>Lo que le toca sin lo personalizado de su ficha, en orden.</summary>
        public List<ActorPersonaTrabajadorDto> Grupo { get; set; } = new();

        /// <summary>
        /// De dónde sale <see cref="Grupo"/>: "Area" (personalizado en Revisores de Áreas),
        /// "Algoritmo" o "Gth" (último recurso).
        /// </summary>
        public string GrupoOrigen { get; set; } = "Algoritmo";

        /// <summary>Lo personalizado en la ficha, en orden. Vacía = no tiene.</summary>
        public List<ActorPersonaTrabajadorDto> Personalizados { get; set; } = new();
    }

    public class ActorPersonaTrabajadorDto
    {
        /// <summary>Null en el último recurso (GTH), que es un buzón de área.</summary>
        public int? WorkerId { get; set; }
        public int? PersonId { get; set; }
        public string? Nombre { get; set; }
        public string? Email { get; set; }
    }
}
