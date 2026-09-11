using Abril_Backend.Features.GestionAdministrativa.RecordatoriosRendicion.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.RecordatoriosRendicion.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.RecordatoriosRendicion.Infrastructure.Repositories
{
    /// <summary>
    /// Las salidas que un trabajador todavía tiene que rendir del mes anterior, y el calendario que
    /// decide cuándo recordárselo.
    ///
    /// Es de solo lectura: el recordatorio no marca nada. Que un correo salga o no se ve en el log
    /// del cron, no en la base — igual que el resto de los correos del módulo.
    /// </summary>
    public class RecordatorioRendicionRepository : IRecordatorioRendicionRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        /// <summary>Lo que se imprime cuando el lugar del trayecto apunta a un proyecto ya borrado.</summary>
        private const string ProyectoSinNombre = "[Sin proyecto]";

        public RecordatorioRendicionRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

        // ── Ventana ──────────────────────────────────────────────────────────

        public async Task<RecordatorioVentanaDto> GetVentanaAsync()
        {
            using var ctx = _factory.CreateDbContext();

            // Trae feriados + el plazo configurado en 2 consultas y responde por cualquier mes.
            var calendario = await CalendarioNoLaborable.CargarAsync(ctx);

            var hoy = MesAnteriorPeru.HoyPeru();
            var (desde, hasta) = MesAnteriorPeru.Rango();

            var limite      = calendario.LimiteDeRendicion(desde.Year, desde.Month);
            var primerHabil = PrimerDiaHabil(hoy, calendario);

            // El cierre se evalúa PRIMERO: con el plazo en 1 día hábil los dos recordatorios caen
            // el mismo día, y ese día lo que corresponde decir es que vence hoy, no que recién se
            // abrió.
            var momento =
                hoy == limite      ? RecordatorioRendicionMomento.Cierre
                : hoy == primerHabil ? RecordatorioRendicionMomento.Apertura
                                     : RecordatorioRendicionMomento.Ninguno;

            return new RecordatorioVentanaDto
            {
                Hoy                  = hoy,
                PeriodoAnio          = desde.Year,
                PeriodoMes           = desde.Month,
                PeriodoDesde         = desde,
                PeriodoHasta         = hasta,
                PrimerDiaHabil       = primerHabil,
                LimiteRendicion      = limite,
                DiasHabilesPlazo     = calendario.DiasHabilesDePlazo,
                DiasHabilesRestantes = DiasHabilesEntre(hoy, limite, calendario),
                Momento              = momento,
            };
        }

        /// <summary>
        /// Primer día hábil del mes de <paramref name="referencia"/>. Si el mes entero fuera no
        /// laborable (caso teórico), devuelve el día 1: nunca una fecha de otro mes.
        /// </summary>
        private static DateOnly PrimerDiaHabil(DateOnly referencia, CalendarioNoLaborable calendario)
        {
            var primero = new DateOnly(referencia.Year, referencia.Month, 1);
            var ultimo  = primero.AddMonths(1).AddDays(-1);

            for (var d = primero; d <= ultimo; d = d.AddDays(1))
                if (!calendario.EsNoLaborable(d)) return d;

            return primero;
        }

        /// <summary>
        /// Días hábiles desde <paramref name="desde"/> hasta <paramref name="hasta"/>, contando
        /// ambos extremos. 0 si el plazo ya pasó. Es el número que el correo le muestra al
        /// trabajador ("te quedan N días hábiles"), así que se cuenta con el mismo calendario que
        /// definió el límite.
        /// </summary>
        private static int DiasHabilesEntre(DateOnly desde, DateOnly hasta, CalendarioNoLaborable calendario)
        {
            if (desde > hasta) return 0;

            var habiles = 0;
            for (var d = desde; d <= hasta; d = d.AddDays(1))
                if (!calendario.EsNoLaborable(d)) habiles++;
            return habiles;
        }

        // ── Pendientes ───────────────────────────────────────────────────────

        public async Task<List<RecordatorioTrabajadorDto>> GetPendientesAsync(DateOnly desde, DateOnly hasta)
        {
            using var ctx = _factory.CreateDbContext();

            // 1. Salidas del periodo aprobadas y sin rendir, con su trabajador y su correo.
            //    Sin email_corporativo no hay a quién recordarle, así que se descartan acá y no
            //    después: no tiene sentido resolver sus trayectos.
            var solicitudes = await (
                from s in ctx.GaSolicitudSalida
                where s.FechaSalida >= desde
                   && s.FechaSalida <= hasta
                   && s.EstadoAprobacionId == EstadosSalida.Aprobacion.Aprobado
                   && s.EstadoRendicionId  == EstadosSalida.Rendicion.NoRendido
                join w in ctx.Worker on s.WorkerId equals w.Id
                where w.EmailCorporativo != null && w.EmailCorporativo != ""
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId into perGroup
                from per in perGroup.DefaultIfEmpty()
                orderby s.FechaSalida, s.Id
                select new
                {
                    s.Id,
                    s.Codigo,
                    s.FechaSalida,
                    WorkerId   = w.Id,
                    Email      = w.EmailCorporativo!,
                    Trabajador = per != null ? per.FullName : null,
                }
            ).ToListAsync();

            if (solicitudes.Count == 0) return new();

            var solicitudIds = solicitudes.Select(s => s.Id).ToList();

            // 2. Trayectos de esas salidas: de acá sale el motivo, el recorrido y —sobre todo— si
            //    la salida da derecho a reembolso, que es lo único que la vuelve rendible.
            var trayectos = await (
                from t  in ctx.GaSolicitudTrayecto
                join m  in ctx.GaMotivoSalida on t.MotivoId equals m.Id into mGroup
                from m  in mGroup.DefaultIfEmpty()
                join lo in ctx.GaLugar on t.LugarOrigenId equals lo.Id into loGroup
                from lo in loGroup.DefaultIfEmpty()
                join po in ctx.Project on lo.ProjectId equals (int?)po.ProjectId into poGroup
                from po in poGroup.DefaultIfEmpty()
                join ld in ctx.GaLugar on t.LugarDestinoId equals ld.Id into ldGroup
                from ld in ldGroup.DefaultIfEmpty()
                join pd in ctx.Project on ld.ProjectId equals (int?)pd.ProjectId into pdGroup
                from pd in pdGroup.DefaultIfEmpty()
                where solicitudIds.Contains(t.SolicitudId)
                orderby t.SolicitudId, t.Orden
                select new
                {
                    t.SolicitudId,
                    t.Orden,
                    t.LugarOrigenId,
                    t.LugarDestinoId,
                    Motivo = m != null ? m.Descripcion : (t.MotivoLibre ?? string.Empty),
                    // Motivo libre = fuera del catálogo: no tiene el flag y por eso no concede nada.
                    EsMotivoDeCatalogo   = m != null,
                    MotivoEsReembolsable = m != null && m.EsReembolsable,
                    LugarOrigen = lo == null ? t.LugarOrigenLibre
                                : lo.Tipo == "proyecto" ? (po != null ? po.ProjectDescription : ProyectoSinNombre)
                                : lo.Nombre,
                    LugarDestino = ld == null ? t.LugarDestinoLibre
                                 : ld.Tipo == "proyecto" ? (pd != null ? pd.ProjectDescription : ProyectoSinNombre)
                                 : ld.Nombre,
                }
            ).ToListAsync();

            // 3. Pares (origen, destino) que anulan el reembolso que concede el motivo (RG-06).
            var excluidos = await ReembolsoTrayectoRule.CargarExcluidosAsync(ctx);

            var trayectosPorSolicitud = trayectos
                .GroupBy(t => t.SolicitudId)
                .ToDictionary(g => g.Key, g => g.OrderBy(t => t.Orden).ToList());

            // 4. Armar el pendiente por trabajador.
            var porTrabajador = new Dictionary<int, RecordatorioTrabajadorDto>();

            foreach (var s in solicitudes)
            {
                if (!trayectosPorSolicitud.TryGetValue(s.Id, out var tramos) || tramos.Count == 0)
                    continue;

                // Basta un trayecto reembolsable: una salida mixta igual generó gasto de movilidad
                // y tiene algo que rendir. Se usa la regla completa (motivo + par origen-destino) y
                // no solo el flag del motivo, para no recordarle a nadie que rinda un recorrido que
                // el catálogo de trayectos ya declaró sin reembolso.
                var esReembolsable = tramos.Any(t => ReembolsoTrayectoRule.Resolver(
                    t.EsMotivoDeCatalogo, t.MotivoEsReembolsable,
                    t.LugarOrigenId, t.LugarDestinoId, excluidos) == true);

                if (!esReembolsable) continue;

                var primero = tramos[0];
                var ultimo  = tramos[^1];

                if (!porTrabajador.TryGetValue(s.WorkerId, out var trabajador))
                {
                    trabajador = new RecordatorioTrabajadorDto
                    {
                        WorkerId = s.WorkerId,
                        Nombre   = s.Trabajador ?? "[Sin nombre]",
                        Email    = s.Email.Trim(),
                    };
                    porTrabajador[s.WorkerId] = trabajador;
                }

                trabajador.Salidas.Add(new RecordatorioSalidaDto
                {
                    SolicitudId    = s.Id,
                    // Las solicitudes anteriores al código no tienen uno: se muestra el id para que
                    // la fila igual sea identificable.
                    Codigo         = string.IsNullOrWhiteSpace(s.Codigo) ? $"#{s.Id}" : s.Codigo!,
                    FechaSalida    = s.FechaSalida,
                    Motivo         = primero.Motivo ?? string.Empty,
                    Origen         = primero.LugarOrigen ?? string.Empty,
                    // El destino es el del ÚLTIMO tramo: es donde termina el recorrido.
                    Destino        = ultimo.LugarDestino ?? string.Empty,
                    TrayectosCount = tramos.Count,
                });
            }

            return porTrabajador.Values
                .OrderBy(t => t.Nombre)
                .ToList();
        }
    }
}
