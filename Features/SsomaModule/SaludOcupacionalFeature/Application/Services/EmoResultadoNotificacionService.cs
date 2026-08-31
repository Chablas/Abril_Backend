using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Configuracion;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Shared;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Services
{
    /// <inheritdoc cref="IEmoResultadoNotificacionService"/>
    public class EmoResultadoNotificacionService : IEmoResultadoNotificacionService
    {
        /// <summary>
        /// Las únicas aptitudes que avisan. "Observado" queda fuera a propósito: significa que la
        /// clínica derivó a interconsulta y la aptitud real se define después — mandar un correo
        /// ahí sería anunciar un resultado que todavía no existe. "Pendiente" tampoco es resultado.
        /// </summary>
        private static readonly HashSet<string> AptitudesQueAvisan = new(StringComparer.OrdinalIgnoreCase)
        {
            EmoResultadoEmailTemplate.AptitudApto,
            EmoResultadoEmailTemplate.AptitudAptoRestricciones,
            EmoResultadoEmailTemplate.AptitudNoApto,
        };

        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IEmoDestinatariosResolver _destinatarios;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmoResultadoNotificacionService> _logger;

        public EmoResultadoNotificacionService(
            IDbContextFactory<AppDbContext> factory,
            IEmoDestinatariosResolver destinatarios,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<EmoResultadoNotificacionService> logger)
        {
            _factory       = factory;
            _destinatarios = destinatarios;
            _emailService  = emailService;
            _configuration = configuration;
            _logger        = logger;
        }

        public async Task NotificarAsync(int emoId)
        {
            try
            {
                using var ctx = _factory.CreateDbContext();

                // Casi todo lo que necesita el correo en una consulta: la plantilla no vuelve a la
                // base. Los left join son obligatorios, no un lujo — un EMO puede no traer clínica
                // ni tipo, y una ficha de pre-ingreso puede no traer puesto.
                var datos = await (
                    from e in ctx.WorkerEmo.AsNoTracking()
                    where e.Id == emoId
                    join w in ctx.Worker.AsNoTracking() on e.WorkerId equals w.Id
                    from p in ctx.Person.AsNoTracking()
                        .Where(x => x.PersonId == w.PersonId).DefaultIfEmpty()
                    from pu in ctx.Puesto.AsNoTracking()
                        .Where(x => x.PuestoId == w.PuestoId).DefaultIfEmpty()
                    from t in ctx.SsEmoTipo.AsNoTracking()
                        .Where(x => x.Id == e.TipoEmoId).DefaultIfEmpty()
                    from cl in ctx.SsClinica.AsNoTracking()
                        .Where(x => x.Id == e.ClinicaId).DefaultIfEmpty()
                    select new
                    {
                        e.WorkerId,
                        e.Aptitud,
                        e.FechaEmo,
                        e.FechaVencimiento,
                        e.ClinicaId,
                        w.WorkersEstadoId,
                        Trabajador = p != null ? p.FullName : null,
                        Dni        = p != null ? p.DocumentIdentityCode : null,
                        Puesto     = pu != null ? pu.Nombre : null,
                        TipoEmo    = t != null ? t.Nombre : null,
                        Clinica    = cl != null ? cl.Nombre : null,
                    })
                    .FirstOrDefaultAsync();

                if (datos == null)
                {
                    _logger.LogWarning("Correo de resultado de EMO: el EMO {EmoId} no existe.", emoId);
                    return;
                }

                var aptitud = datos.Aptitud?.Trim() ?? string.Empty;
                if (!AptitudesQueAvisan.Contains(aptitud))
                {
                    _logger.LogInformation(
                        "Correo de resultado de EMO {EmoId}: la aptitud \"{Aptitud}\" no es un veredicto cerrado, no se avisa.",
                        emoId, string.IsNullOrEmpty(aptitud) ? "—" : aptitud);
                    return;
                }

                var destinatarios = await _destinatarios.ResolverAsync(
                    EmoCorreoEventoCodigo.Resultado, datos.WorkerId, datos.ClinicaId);

                var to = destinatarios.Para.Select(d => d.Email).ToList();
                var cc = destinatarios.Copias.Select(d => d.Email).ToList();

                // Sin destinatarios principales activos no se envía nada (ni las copias): es la
                // forma de silenciar el correo desde la pantalla de Configuración de EMOs.
                if (to.Count == 0)
                {
                    _logger.LogWarning(
                        "Correo de resultado de EMO {EmoId}: sin destinatarios principales activos, no se envía.",
                        emoId);
                    return;
                }

                // El proyecto sale de la vinculación vigente, que las fichas de pre-ingreso todavía
                // no tienen: sin vinculación la consulta no devuelve fila y el correo sale sin esa
                // línea, que es exactamente lo correcto para un postulante.
                var proyecto = await (
                    from v in ctx.WorkerVinculacion.AsNoTracking()
                    where v.WorkerId == datos.WorkerId && v.FechaFin == null
                    orderby v.CreatedAt descending, v.Id descending
                    join pr in ctx.Project.AsNoTracking() on v.ProyectoId equals pr.ProjectId
                    select pr.ProjectDescription)
                    .FirstOrDefaultAsync();

                // Las restricciones solo se listan cuando son las que explican la aptitud; en un
                // Apto a secas no debería haber ninguna, y en un No Apto no aportan nada.
                var restricciones = new List<string>();
                if (string.Equals(aptitud, EmoResultadoEmailTemplate.AptitudAptoRestricciones,
                                  StringComparison.OrdinalIgnoreCase))
                {
                    // La descripción libre manda sobre el catálogo: es lo que escribió la clínica
                    // para este examen. El coalesce va en memoria y no en el SELECT para que la
                    // consulta sea un join plano, sin subconsultas que traducir.
                    var filas = await (
                        from r in ctx.SsEmoRestriccion.AsNoTracking()
                        where r.EmoId == emoId && r.Vigente
                        from tipo in ctx.SsRestriccionTipo.AsNoTracking()
                            .Where(x => x.Id == r.RestriccionTipoId).DefaultIfEmpty()
                        select new
                        {
                            r.DescripcionLibre,
                            Catalogo = tipo != null ? tipo.Descripcion : null,
                        })
                        .ToListAsync();

                    restricciones = filas
                        .Select(f => string.IsNullOrWhiteSpace(f.DescripcionLibre)
                            ? f.Catalogo
                            : f.DescripcionLibre)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => x!.Trim())
                        .ToList();
                }

                var correo = new EmoResultadoCorreoDatos
                {
                    Trabajador       = datos.Trabajador ?? $"Trabajador #{datos.WorkerId}",
                    Dni              = datos.Dni,
                    Puesto           = datos.Puesto,
                    TipoEmo          = datos.TipoEmo,
                    FechaEmo         = datos.FechaEmo,
                    FechaVencimiento = datos.FechaVencimiento,
                    Clinica          = datos.Clinica,
                    Proyecto         = proyecto,
                    Aptitud          = aptitud,
                    Restricciones    = restricciones,
                    EsPostulante     = WorkersEstadoIds.PreIngreso.Contains(datos.WorkersEstadoId),
                };

                var layout = SaludOcupacionalEmailLayout.Desde(_configuration);

                await _emailService.SendAsync(
                    to: to,
                    subject: EmoResultadoEmailTemplate.Asunto(correo),
                    body: EmoResultadoEmailTemplate.Construir(layout, correo),
                    isHtml: true,
                    cc: cc.Count > 0 ? cc : null,
                    sender: SaludOcupacionalEmailConstants.SenderKey);
            }
            catch (Exception ex)
            {
                // Best-effort: el examen ya está guardado y no se deshace por un correo.
                _logger.LogError(ex, "Correo de resultado de EMO {EmoId}: no se pudo enviar.", emoId);
            }
        }
    }
}
