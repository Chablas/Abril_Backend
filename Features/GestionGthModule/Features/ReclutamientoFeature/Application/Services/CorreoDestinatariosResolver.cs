using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionGthModule.Shared.Correos;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Services
{
    /// <inheritdoc cref="ICorreoDestinatariosResolver"/>
    public class CorreoDestinatariosResolver : ICorreoDestinatariosResolver
    {
        private readonly ICorreoConfigRepository _config;
        private readonly IReclutamientoRepository _reclutamiento;
        private readonly ILogger<CorreoDestinatariosResolver> _logger;

        public CorreoDestinatariosResolver(
            ICorreoConfigRepository config,
            IReclutamientoRepository reclutamiento,
            ILogger<CorreoDestinatariosResolver> logger)
        {
            _config        = config;
            _reclutamiento = reclutamiento;
            _logger        = logger;
        }

        /// <summary>Etiqueta de las filas escritas a mano desde la pantalla de configuración.</summary>
        private const string OrigenAdicional = "Correo configurado";

        public async Task<SolicitudDestinatariosDto> ResolverAsync(string tipoCodigo, int? areaScopeId = null)
        {
            var todos = await ResolverVariosAsync(new[] { tipoCodigo }, areaScopeId);
            return todos[tipoCodigo];
        }

        public async Task<IReadOnlyDictionary<string, SolicitudDestinatariosDto>> ResolverVariosAsync(
            IReadOnlyList<string> tipoCodigos, int? areaScopeId = null)
        {
            var configs = await _config.GetEnvioConfigAsync(tipoCodigos);

            // Los dinámicos se resuelven UNA vez para todos los correos pedidos: son el mismo dato
            // (el Gerente General de hoy, el correo del área de GTH…) y consultarlos correo por
            // correo multiplicaría los roundtrips sin cambiar el resultado. Solo se consulta lo que
            // alguna fila activa necesita, y en el warning van los correos que lo pedían para que
            // el log siga diciendo a cuál se le quedó sin resolver un destinatario.
            var quienPide = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (tipo, config) in configs)
                foreach (var fila in config.Filas.Where(f => !string.IsNullOrWhiteSpace(f.Codigo)))
                {
                    if (!quienPide.TryGetValue(fila.Codigo!, out var tipos))
                        quienPide[fila.Codigo!] = tipos = new List<string>();
                    if (!tipos.Contains(tipo)) tipos.Add(tipo);
                }

            string? Pide(string codigo) =>
                quienPide.TryGetValue(codigo, out var tipos) ? string.Join(", ", tipos) : null;

            var pideGerenteGeneral = Pide(CorreoDestinatarioCodigo.GerenteGeneral);
            var gerenteGeneral = pideGerenteGeneral != null
                ? await ResolverGerenteGeneralAsync(pideGerenteGeneral)
                : null;

            var pideGth = Pide(CorreoDestinatarioCodigo.GthArea);
            var emailGth = pideGth != null
                ? await ResolverAreaAsync(pideGth, "Gestión del Talento Humano", _config.GetEmailAreaGthAsync)
                : null;

            var pideTi = Pide(CorreoDestinatarioCodigo.TiArea);
            var emailTi = pideTi != null
                ? await ResolverAreaAsync(pideTi, "Tecnología de la Información", _config.GetEmailAreaTiAsync)
                : null;

            var pideGerenteArea = Pide(CorreoDestinatarioCodigo.GerenteArea);
            var gerenteArea = pideGerenteArea != null
                ? await ResolverGerenteAreaAsync(pideGerenteArea, areaScopeId)
                : null;

            // Una entrada por código pedido, incluidos los que no existen o están apagados: así el
            // llamador puede leerlos todos sin preguntar si están.
            var resultado = new Dictionary<string, SolicitudDestinatariosDto>(StringComparer.OrdinalIgnoreCase);
            foreach (var tipoCodigo in tipoCodigos)
            {
                if (resultado.ContainsKey(tipoCodigo)) continue;
                var config = configs.TryGetValue(tipoCodigo, out var c) ? c : new CorreoEnvioConfigDto();
                resultado[tipoCodigo] = Armar(tipoCodigo, config, gerenteGeneral, emailGth, emailTi, gerenteArea);
            }

            return resultado;
        }

        /// <summary>
        /// Expande las filas configuradas de UN correo a los buzones concretos que las cumplen hoy,
        /// con los destinatarios dinámicos ya resueltos.
        /// </summary>
        private SolicitudDestinatariosDto Armar(
            string tipoCodigo,
            CorreoEnvioConfigDto config,
            CorreoDestinatarioResueltoDto? gerenteGeneral,
            string? emailGth,
            string? emailTi,
            GerenteAreaDto? gerenteArea)
        {
            // El interruptor del principal automático es independiente del maestro: viaja siempre,
            // incluso cuando el correo está apagado y no hay ninguna fila que resolver.
            var dto = new SolicitudDestinatariosDto
            {
                PrincipalAutomaticoActivo = config.PrincipalAutomaticoActivo,
            };

            // Correo apagado con el interruptor maestro → el repositorio no devuelve ninguna fila.
            var filas = config.Filas;
            if (filas.Count == 0) return dto;

            var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Agregar(List<DestinatarioSolicitudDto> lista, string? email, string? nombre, string origen)
            {
                var e = email?.Trim();
                if (string.IsNullOrWhiteSpace(e) || !vistos.Add(e)) return;
                lista.Add(new DestinatarioSolicitudDto { Email = e, Nombre = nombre, Origen = origen });
            }

            // Los principales primero y las copias al final: así un correo que esté en ambas
            // listas queda solo en "Para" (gana Para sobre CC, mismo criterio que la pantalla).
            foreach (var fila in filas.OrderBy(f => f.EsCopia).ThenBy(f => f.Orden))
            {
                var lista = fila.EsCopia ? dto.Copias : dto.Para;

                if (string.IsNullOrWhiteSpace(fila.Codigo))
                {
                    Agregar(lista, fila.Email, fila.Nombre, fila.Nombre ?? OrigenAdicional);
                    continue;
                }

                switch (fila.Codigo.ToUpperInvariant())
                {
                    case CorreoDestinatarioCodigo.GerenteGeneral:
                        Agregar(lista, gerenteGeneral?.Email, gerenteGeneral?.Nombre,
                            fila.Nombre ?? "Gerente General");
                        break;

                    case CorreoDestinatarioCodigo.GthArea:
                        Agregar(lista, emailGth, null,
                            fila.Nombre ?? "Área de Gestión del Talento Humano");
                        break;

                    case CorreoDestinatarioCodigo.TiArea:
                        Agregar(lista, emailTi, null,
                            fila.Nombre ?? "Área de Tecnología de la Información");
                        break;

                    case CorreoDestinatarioCodigo.GerenteArea:
                        // Guion y no "Gerente de {área}": el área suele llamarse ya "Gerencia de
                        // X", y "Gerente de Gerencia de Proyectos" se lee mal.
                        Agregar(lista, gerenteArea?.Email, gerenteArea?.Nombre,
                            string.IsNullOrWhiteSpace(gerenteArea?.AreaNombre)
                                ? fila.Nombre ?? "Gerente del área"
                                : $"Gerente — {gerenteArea!.AreaNombre}");
                        break;

                    default:
                        _logger.LogWarning(
                            "Correo {Tipo}: el destinatario dinámico {Codigo} no tiene forma de resolverse; se ignora.",
                            tipoCodigo, fila.Codigo);
                        break;
                }
            }

            // Un mismo buzón no puede quedar en Para y en CC a la vez (`vistos` ya lo garantiza,
            // pero el orden de recorrido es lo que decide cuál gana).
            return dto;
        }

        // ── Resolución de cada destinatario dinámico ──────────────────────────
        // Todas son best-effort: un dinámico que hoy no resuelve no puede tumbar el correo
        // completo, así que se registra el warning y el resto de destinatarios sale igual.

        private async Task<CorreoDestinatarioResueltoDto?> ResolverGerenteGeneralAsync(string tipoCodigo)
        {
            try
            {
                var gg = await _config.GetGerenteGeneralAsync();
                if (gg == null)
                    _logger.LogWarning(
                        "Correo {Tipo}: el Gerente General está activo como destinatario pero no hay ningún " +
                        "trabajador ACTIVO con puesto «GERENTE GENERAL» y correo corporativo cargado.",
                        tipoCodigo);
                return gg;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Correo {Tipo}: no se pudo resolver al Gerente General; sale sin él.", tipoCodigo);
                return null;
            }
        }

        /// <summary>
        /// Correo de un área (GTH, TI, …), que sale de <c>area_scope.email</c>. Todas se resuelven
        /// igual y fallan igual, así que comparten el manejo: lo único que cambia es de qué nodo se
        /// lee y cómo se llama el área en el log.
        /// </summary>
        private async Task<string?> ResolverAreaAsync(
            string tipoCodigo, string areaNombre, Func<Task<string?>> leerEmail)
        {
            try
            {
                var email = await leerEmail();
                if (string.IsNullOrWhiteSpace(email))
                    _logger.LogWarning(
                        "Correo {Tipo}: el área de {Area} está activa como destinataria pero no tiene correo configurado.",
                        tipoCodigo, areaNombre);
                return email;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Correo {Tipo}: no se pudo resolver el correo del área de {Area}; sale sin él.",
                    tipoCodigo, areaNombre);
                return null;
            }
        }

        private async Task<GerenteAreaDto?> ResolverGerenteAreaAsync(string tipoCodigo, int? areaScopeId)
        {
            try
            {
                return await _reclutamiento.GetGerenteDeArea(areaScopeId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Correo {Tipo}: no se pudo resolver al gerente del área {AreaScopeId}; sale sin él.",
                    tipoCodigo, areaScopeId);
                return null;
            }
        }
    }
}
