using Abril_Backend.Features.ConvivirModule.Features.NotificacionesFeature.Application.Dtos;
using Abril_Backend.Features.ConvivirModule.Features.NotificacionesFeature.Application.Interfaces;
using Abril_Backend.Features.ConvivirModule.Features.NotificacionesFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.ConvivirModule.Shared.Dtos;

namespace Abril_Backend.Features.ConvivirModule.Features.NotificacionesFeature.Application.Services
{
    /// <summary>
    /// La campana de la app (RF-05 y sección 11 del documento funcional): junta los avisos del
    /// propietario, de todas sus propiedades, y cada uno lleva a su contenido. Hoy: hito cumplido
    /// (Mi Proyecto) y documento nuevo (Mis documentos). El texto se arma acá y no en la app, para
    /// que sea el mismo cuando lleguen las notificaciones al teléfono.
    /// </summary>
    public class ConvivirNotificacionesService : IConvivirNotificacionesService
    {
        /// <summary>Tope de la lista: los avisos más viejos ya no se muestran (siguen en la base).</summary>
        private const int Limite = 50;

        /// <summary>Perú no tiene horario de verano: siempre UTC-5.</summary>
        private static readonly TimeSpan HoraPeru = TimeSpan.FromHours(-5);

        private readonly IConvivirNotificacionesRepository _repo;

        public ConvivirNotificacionesService(IConvivirNotificacionesRepository repo)
        {
            _repo = repo;
        }

        public async Task<ConvivirNotificacionesDto> GetNotificaciones(int userId)
        {
            var filas = await _repo.GetNotificaciones(userId, Limite);

            return new ConvivirNotificacionesDto
            {
                Notificaciones = filas.Notificaciones.Select(Armar).ToList(),
                NoLeidas = filas.NoLeidas,
                VariasPropiedades = filas.VariasPropiedades,
            };
        }

        public Task MarcarLeida(int userId, int notificacionId) => _repo.MarcarLeida(userId, notificacionId);

        public Task MarcarTodasLeidas(int userId) => _repo.MarcarTodasLeidas(userId);

        private static ConvivirNotificacionDto Armar(ConvivirNotificacionFila fila)
        {
            var (titulo, detalle) = fila.Tipo switch
            {
                ConvivirNotificacionTipo.Hito => ("Nuevo avance de tu obra", $"Se cumplió el hito «{fila.Contenido}»."),
                ConvivirNotificacionTipo.Documento => ("Nuevo documento", DetalleDocumento(fila)),
                _ => ("Aviso", fila.Contenido),
            };

            return new ConvivirNotificacionDto
            {
                NotificacionId = fila.NotificacionId,
                Tipo           = fila.Tipo,
                Titulo         = titulo,
                Detalle        = detalle,
                Fecha          = new DateTimeOffset(fila.Fecha, HoraPeru),
                Leida          = fila.Leida,
                PropietarioId  = fila.PropietarioId,
                Proyecto       = fila.Proyecto,
                Torre          = fila.Torre,
                Departamento   = fila.Departamento,
            };
        }

        /// <summary>«Minuta de compraventa» (Minuta) ya está en Mis documentos. Sin el tipo si el nombre ya lo dice.</summary>
        private static string DetalleDocumento(ConvivirNotificacionFila fila) =>
            string.IsNullOrWhiteSpace(fila.ContenidoTipo)
            || string.Equals(fila.Contenido.Trim(), fila.ContenidoTipo.Trim(), StringComparison.OrdinalIgnoreCase)
                ? $"«{fila.Contenido}» ya está en Mis documentos."
                : $"«{fila.Contenido}» ({fila.ContenidoTipo}) ya está en Mis documentos.";
    }
}
