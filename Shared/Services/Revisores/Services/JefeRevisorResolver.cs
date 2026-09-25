using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Services.Actores.Interfaces;
using Abril_Backend.Shared.Services.Revisores.Interfaces;

namespace Abril_Backend.Shared.Services.Revisores.Services
{
    /// <summary>
    /// Fachada de <see cref="IActoresResolver"/> con la forma que ya consumían las pantallas: acá no
    /// se decide nada, solo se pregunta por el actor que corresponde y se traduce la respuesta.
    ///
    ///   • <see cref="ResolveManyAsync"/>              → <c>ActorIds.AprobadorSalida</c>;
    ///   • <see cref="ResolveJefeNotificadoManyAsync"/> → <c>ActorIds.JefeNotificado</c>;
    ///   • <see cref="ResolveAprobadoresDeDocumentosAsync"/> → <c>ActorIds.AprobadorPrimeraRevision</c>
    ///     o <c>ActorIds.AprobadorConsolidado</c>, según el paso.
    ///
    /// Hasta el 2026-09-25 esta clase TENÍA el algoritmo (Ranking + Elegir para la salida,
    /// AprobadoresDeLaCadena para la planilla) y el de los consolidadores vivía aparte; los tres
    /// pasaron al mismo núcleo para que la ficha del trabajador, Revisores de Áreas y los correos
    /// digan siempre lo mismo.
    /// </summary>
    public class JefeRevisorResolver : IJefeRevisorResolver
    {
        private readonly IActoresResolver _actores;

        public JefeRevisorResolver(IActoresResolver actores)
        {
            _actores = actores;
        }

        public async Task<JefeRevisorResolution?> ResolveAsync(int workerId)
        {
            var resueltos = await ResolveManyAsync(new[] { workerId });
            return resueltos.TryGetValue(workerId, out var jefe) ? jefe : null;
        }

        public Task<Dictionary<int, JefeRevisorResolution>> ResolveManyAsync(IReadOnlyCollection<int> workerIds)
            => UnoPorTrabajadorAsync(workerIds, ActorIds.AprobadorSalida);

        public async Task<JefeRevisorResolution?> ResolveJefeNotificadoAsync(int workerId)
        {
            var resueltos = await ResolveJefeNotificadoManyAsync(new[] { workerId });
            return resueltos.TryGetValue(workerId, out var jefe) ? jefe : null;
        }

        public Task<Dictionary<int, JefeRevisorResolution>> ResolveJefeNotificadoManyAsync(
            IReadOnlyCollection<int> workerIds)
            => UnoPorTrabajadorAsync(workerIds, ActorIds.JefeNotificado);

        public async Task<List<AprobadorDocumento>> ResolveAprobadoresDeDocumentoAsync(
            IReadOnlyCollection<int> workerIds, PasoAprobacion paso)
        {
            var uno = new Dictionary<int, IReadOnlyCollection<int>> { [0] = workerIds };
            var resueltos = await ResolveAprobadoresDeDocumentosAsync(uno, paso);
            return resueltos.GetValueOrDefault(0) ?? new List<AprobadorDocumento>();
        }

        public async Task<Dictionary<int, List<AprobadorDocumento>>> ResolveAprobadoresDeDocumentosAsync(
            IReadOnlyDictionary<int, IReadOnlyCollection<int>> workersPorDocumento,
            PasoAprobacion paso)
        {
            var actorId = paso == PasoAprobacion.PrimeraRevision
                ? ActorIds.AprobadorPrimeraRevision
                : ActorIds.AprobadorConsolidado;

            var resueltos = await _actores.ResolverDocumentosAsync(workersPorDocumento, actorId);

            return resueltos.ToDictionary(
                kv => kv.Key,
                kv => kv.Value
                    .Select((p, i) => new AprobadorDocumento(Traducir(p, OrigenDe(p)), i + 1))
                    .ToList());
        }

        /// <summary>El primero de un actor de uno solo, por trabajador. Los que no resuelven no aparecen.</summary>
        private async Task<Dictionary<int, JefeRevisorResolution>> UnoPorTrabajadorAsync(
            IReadOnlyCollection<int> workerIds, int actorId)
        {
            var resultado = new Dictionary<int, JefeRevisorResolution>();

            foreach (var (workerId, actores) in await _actores.ResolverTrabajadoresAsync(workerIds, new[] { actorId }))
            {
                if (!actores.Actores.TryGetValue(actorId, out var r) || !r.Aplica) continue;

                var persona = r.Personas.FirstOrDefault();
                if (persona != null) resultado[workerId] = Traducir(persona, Origen(r.Origen));
            }

            return resultado;
        }

        private static JefeRevisorResolution Traducir(ActorPersona p, RevisorOrigen origen)
            => new(p.WorkerId, p.AreaScopeId, p.Email, p.Nombre, p.PersonId, origen, p.CategoriaId);

        private static RevisorOrigen Origen(ActorOrigen origen) => origen switch
        {
            ActorOrigen.Gth       => RevisorOrigen.Gth,
            ActorOrigen.Algoritmo => RevisorOrigen.Algoritmo,
            _                     => RevisorOrigen.Personalizado,
        };

        /// <summary>En un documento la persona no trae su origen: el buzón de área es GTH, el resto no importa.</summary>
        private static RevisorOrigen OrigenDe(ActorPersona p)
            => p.AreaScopeId != null ? RevisorOrigen.Gth : RevisorOrigen.Algoritmo;
    }
}
