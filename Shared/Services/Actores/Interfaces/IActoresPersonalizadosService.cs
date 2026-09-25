namespace Abril_Backend.Shared.Services.Actores.Interfaces
{
    /// <summary>
    /// Lado de ESCRITURA de lo personalizado por trabajador (<c>workers_actor_asignacion</c>): los
    /// actores elegidos a mano en la ficha del trabajador, que le ganan a todo lo demás. Reemplaza al
    /// "Jefe personalizado" (<c>workers_revisores</c>), que solo cubría a quien aprueba la salida.
    ///
    /// La LECTURA —quién le toca al trabajador y de dónde sale— es de <see cref="IActoresResolver"/>.
    ///
    /// Servicio compartido: lo usan Habilitación (el formulario de trabajadores y su catálogo de
    /// personas) y SSOMA · Salud Ocupacional (que guarda el alta y la edición del trabajador).
    /// </summary>
    public interface IActoresPersonalizadosService
    {
        /// <summary>
        /// Deja al trabajador exactamente con lo personalizado indicado: por cada actor, la lista en
        /// orden (vacía o ausente = sin personalizar, vuelve a mandar su área). Las filas que sobran se
        /// dan de baja (soft delete) y las que faltan se crean. Los actores de uno solo admiten una
        /// persona; los de varios, las que vengan.
        /// </summary>
        Task SetAsync(int workerId, IReadOnlyDictionary<int, IReadOnlyList<int>> porActor);

        /// <summary>
        /// Personas que se pueden elegir: las fichas con correo corporativo @abril.pe, tengan o no
        /// usuario del sistema. Ordenadas por nombre. El propio trabajador entra: elegirse a sí mismo
        /// es una decisión explícita de quien edita la ficha.
        /// </summary>
        Task<List<ActorCandidatoDto>> GetCandidatosAsync();
    }

    /// <summary>
    /// Lo personalizado para un actor tal como lo manda un formulario: el actor y las personas en
    /// orden (lista vacía = sin personalizar).
    /// </summary>
    public class ActorPersonalizadoInputDto
    {
        /// <summary><c>ActorIds</c>.</summary>
        public int ActorId { get; set; }

        /// <summary>workers.id de las personas elegidas, en orden. Una sola en los actores de uno solo.</summary>
        public List<int> WorkerIds { get; set; } = new();

        /// <summary>La lista del formulario como la espera <see cref="IActoresPersonalizadosService.SetAsync"/>.</summary>
        public static IReadOnlyDictionary<int, IReadOnlyList<int>> PorActor(IEnumerable<ActorPersonalizadoInputDto>? filas)
            => (filas ?? Enumerable.Empty<ActorPersonalizadoInputDto>())
                .GroupBy(f => f.ActorId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<int>)g.SelectMany(f => f.WorkerIds ?? new List<int>()).ToList());
    }

    /// <summary>Opción de los desplegables de personas.</summary>
    public class ActorCandidatoDto
    {
        public int WorkerId { get; set; }

        /// <summary>Una persona puede tener varias fichas en <c>workers</c> por reingreso.</summary>
        public int? PersonId { get; set; }

        public string? FullName { get; set; }
        public string? Email { get; set; }
    }
}
