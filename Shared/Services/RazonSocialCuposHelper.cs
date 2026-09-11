using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Shared.Services
{
    /// <summary>
    /// Las razones sociales activas del grupo con sus cupos disponibles: el desplegable que aparece
    /// cada vez que hay que decidir bajo cuál de las empresas de Abril entra una persona.
    ///
    /// Vive acá y no en un repositorio porque lo preguntan dos módulos —Salud Ocupacional
    /// (SsomaModule), al programarle el EMO de ingreso al finalista, que es el único punto del
    /// sistema donde se asigna la razón social de un ingreso; y Configuración
    /// (ConfigurationModule), que en Razones Sociales muestra ese mismo conjunto de trabajadores—,
    /// y las dos pantallas tienen que contar exactamente lo mismo. Con la cuenta duplicada bastaría
    /// con tocar una para que la otra ofreciera cupos que ya no existen.
    ///
    /// <para><b>El tope no siempre corta.</b> Una vacante de tipo REEMPLAZO puede pasarse de 20: el
    /// que entra y el que sale conviven un mes y la baja del reemplazado devuelve la cuenta a su
    /// sitio. La excepción se decide en el EMO, que es quien sabe de qué vacante sale la ficha (ver
    /// <c>IReclutamientoEmoIngresoService.EsReemplazoAsync</c>); acá la cuenta es siempre la misma y
    /// los cupos que informa son siempre la verdad.</para>
    /// </summary>
    public static class RazonSocialCuposHelper
    {
        /// <summary>
        /// Tope de trabajadores por razón social para el cálculo de cupos. Cuentan los de Staff,
        /// Oficina Central y Personal Externo
        /// (<see cref="ObraOficinaStaffIds.ConsumenCupoRazonSocial"/>); el personal de Obra y los
        /// practicantes no consumen cupo.
        /// </summary>
        public const int TopeCupos = 20;

        /// <summary>
        /// Razones sociales operativas del grupo (<c>contributor.operativo = true</c>) ordenadas por
        /// nombre, cada una con lo que le queda de su tope. Dos roundtrips fijos: el catálogo y la
        /// ocupación agrupada, sin N+1 aunque haya una docena de empresas.
        /// </summary>
        public static async Task<List<RazonSocialCupoDto>> ListarAsync(AppDbContext ctx)
        {
            var razones = await ctx.Contributor
                .Where(c => c.State && c.Active && c.Operativo)
                .OrderBy(c => c.ContributorName)
                .Select(c => new { c.ContributorId, c.ContributorName })
                .ToListAsync();

            var ocupadosPorRazon = await OcupadosPorRazonSocialAsync(ctx);

            return razones.Select(c => new RazonSocialCupoDto
            {
                Id     = c.ContributorId,
                Nombre = c.ContributorName,
                CuposDisponibles = Math.Max(0,
                    TopeCupos - ocupadosPorRazon.GetValueOrDefault(c.ContributorId)),
            }).ToList();
        }

        /// <summary>
        /// Fichas que consumen cupo, cada una ya emparejada con la razón social que le toca HOY: la
        /// definición de "ocupa un cupo" en un solo sitio, para que contar todas las razones
        /// sociales (<see cref="ListarAsync"/>), contar una sola
        /// (<see cref="CuposDisponiblesAsync"/>) y listar quiénes son (Configuración → Razones
        /// Sociales, vía <see cref="FichasDe"/>) no puedan dar respuestas distintas.
        ///
        /// <para><b>De dónde sale la razón social.</b> De la vinculación abierta
        /// (<c>worker_vinculaciones.fecha_fin IS NULL</c>), NO de <c>workers.contributor_id</c>.
        /// Son dos fuentes distintas y no coinciden: la ficha solo la escribe el pre-ingreso de
        /// GTH, mientras que el alta de trabajador de SSOMA y todos los cambios de empresa de
        /// Habilitación escriben únicamente la vinculación. O sea que <c>contributor_id</c> se
        /// desactualiza con cada alta y con cada cambio de empresa, y el desfase crece solo: al
        /// 2026-09-08 había 31 de 238 fichas del universo de cupos contadas en la empresa
        /// equivocada (o en ninguna, por tenerlo en null) y cuatro razones sociales llenas que la
        /// pantalla ofrecía con cupo libre.</para>
        ///
        /// <para>El <c>??</c> a <c>workers.contributor_id</c> es la red para las fichas sin ninguna
        /// vinculación —hoy 9 en prod, todas de una carga masiva—: sin él desaparecerían de la
        /// cuenta, que es justo el bug que este helper arregla.</para>
        ///
        /// <para>La subconsulta es correlacionada a propósito (un LATERAL, no un N+1) y se apoya en
        /// el índice parcial <c>idx_wvin_activa (worker_id) WHERE fecha_fin IS NULL</c>.</para>
        ///
        /// <para><b>Quiénes consumen.</b> Los trabajadores no retirados de Staff, Oficina Central o
        /// Personal Externo. El personal de Obra NO consume el tope (el tope de 20 es de planilla de
        /// escritorio, y contando obreros toda razón social con un proyecto en curso quedaba en 0
        /// cupos). Los practicantes tampoco consumen. Las fichas de pre-ingreso (finalistas
        /// aprobados) quedan fuera por estado: reservan cupo recién cuando GTH les aprueba la carta
        /// oferta firmada y pasan a ACTIVO — ver <c>CartaOfertaRepository.Aprobar</c>.</para>
        ///
        /// <para>El practicante se detecta por <c>categoria_maestra_id</c>, no por el texto libre
        /// <c>workers.categoria</c>: ese campo guarda el nivel del puesto (Operario, Arquitecto…) y
        /// se desincroniza — había practicantes con "Arquitecto" contando cupo y empleados que
        /// habían sido practicantes y seguían con el texto viejo sin contar. Los que no tienen
        /// categoría maestra sí consumen (no son practicantes).</para>
        /// </summary>
        public static IQueryable<FichaQueOcupaCupo> OcupanCupo(AppDbContext ctx) =>
            RazonSocialVigente(ctx, ctx.Worker
                    .Where(w => WorkersEstadoIds.NoRetirados.Contains(w.WorkersEstadoId)
                             && ObraOficinaStaffIds.ConsumenCupoRazonSocial.Contains(w.ObraOficinaStaffId ?? 0)
                             && w.CategoriaMaestraId != CategoriaMaestraIds.PracticantePrePro))
                .Where(f => f.ContributorId != null)
                .Select(f => new FichaQueOcupaCupo { WorkerId = f.WorkerId, ContributorId = f.ContributorId });

        /// <summary>
        /// La razón social VIGENTE de cada ficha de <paramref name="fichas"/>, sin mirar si consume
        /// cupo: la de la vinculación abierta y, si no tiene ninguna, la de la ficha. Es la regla de
        /// <see cref="OcupanCupo"/> (ver ahí por qué es esa y no <c>workers.contributor_id</c>) en un
        /// solo sitio, para quien necesita la razón social de trabajadores que no entran en el
        /// universo de cupos: el Consolidado del S10 exige que las planillas que agrupa sean de una
        /// misma razón social, y ahí cuenta también el personal de Obra.
        ///
        /// <para>A diferencia de <see cref="OcupanCupo"/>, no descarta las fichas sin razón social:
        /// vuelven con <see cref="FichaRazonSocial.ContributorId"/> en null.</para>
        /// </summary>
        public static IQueryable<FichaRazonSocial> RazonSocialVigente(AppDbContext ctx, IQueryable<Worker> fichas) =>
            fichas.Select(w => new FichaRazonSocial
            {
                WorkerId      = w.Id,
                ContributorId = ctx.WorkerVinculacion
                                   .Where(v => v.WorkerId == w.Id && v.FechaFin == null)
                                   .OrderByDescending(v => v.CreatedAt)
                                   .ThenByDescending(v => v.Id)
                                   .Select(v => v.EmpresaId)
                                   .FirstOrDefault()
                                ?? w.ContributorId,
            });

        /// <summary>
        /// Cuántas fichas ocupa hoy cada razón social. Lo comparten el desplegable de cupos
        /// (<see cref="ListarAsync"/>) y el chip de cantidad de la bandeja de Configuración →
        /// Razones Sociales, que son la misma cuenta vista de dos formas.
        ///
        /// <para>Un roundtrip que trae solo la columna de la razón social ya resuelta y agrupa en
        /// memoria, en vez de un <c>GROUP BY</c> en la base. Es a propósito: la clave de
        /// agrupación es una subconsulta correlacionada, y agrupar por una subconsulta es de los
        /// casos que EF a veces no sabe traducir — y cuando no puede, no avisa al compilar sino que
        /// revienta al abrir la pantalla. Lo que se trae es un <c>int</c> por ficha del universo de
        /// cupos (238 al 2026-09-08, y son los de escritorio, no la planilla entera), así que el
        /// ahorro de contar en la base no compensa el riesgo.</para>
        /// </summary>
        public static async Task<Dictionary<int, int>> OcupadosPorRazonSocialAsync(AppDbContext ctx)
        {
            var razonesOcupadas = await OcupanCupo(ctx)
                .Select(f => f.ContributorId!.Value)
                .ToListAsync();

            return razonesOcupadas
                .GroupBy(id => id)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        /// <summary>Cuántas fichas ocupa hoy UNA razón social. Un roundtrip (un COUNT).</summary>
        public static Task<int> OcupadosAsync(AppDbContext ctx, int contributorId) =>
            OcupanCupo(ctx).CountAsync(f => f.ContributorId == contributorId);

        /// <summary>
        /// Las fichas de UNA razón social —las mismas que le cuentan cupo—, como
        /// <c>IQueryable&lt;Worker&gt;</c> para que quien las pida las proyecte a su gusto sin
        /// arrastrar la definición. Va en una sola consulta: el <c>Contains</c> sobre el
        /// <c>IQueryable</c> de ids se traduce a un <c>IN (subconsulta)</c>, no a dos viajes.
        /// </summary>
        public static IQueryable<Worker> FichasDe(AppDbContext ctx, int contributorId)
        {
            var ids = OcupanCupo(ctx)
                .Where(f => f.ContributorId == contributorId)
                .Select(f => f.WorkerId);

            return ctx.Worker.Where(w => ids.Contains(w.Id));
        }

        /// <summary>
        /// Lo que le queda del tope a UNA razón social, con la misma cuenta que
        /// <see cref="ListarAsync"/>. Un roundtrip (un COUNT), pensado para revalidar en el
        /// servidor lo que la pantalla ya muestra: en una razón social llena no entra nadie más, y
        /// el desplegable que lo avisa no es el que manda.
        /// </summary>
        public static async Task<int> CuposDisponiblesAsync(AppDbContext ctx, int contributorId) =>
            Math.Max(0, TopeCupos - await OcupadosAsync(ctx, contributorId));

        /// <summary>
        /// ¿Es una razón social del grupo a la que se puede asignar gente hoy? Es la revalidación de
        /// lo que ofrece <see cref="ListarAsync"/>: lo que no está en la lista tampoco se acepta,
        /// venga de donde venga el id. No mira cupos — para eso está
        /// <see cref="CuposDisponiblesAsync"/>.
        /// </summary>
        public static Task<bool> EsValidaAsync(AppDbContext ctx, int contributorId) =>
            ctx.Contributor.AnyAsync(c => c.ContributorId == contributorId
                                       && c.State && c.Active && c.Operativo);

        /// <summary>
        /// El texto de "esta razón social está llena", en el helper y no en la pantalla porque
        /// también lo usa la revalidación del servidor: el aviso del modal y el error del guardado
        /// tienen que decir lo mismo.
        /// </summary>
        public static string MensajeSinCupos(string? nombre = null) =>
            (string.IsNullOrWhiteSpace(nombre) ? "La razón social seleccionada" : nombre)
            + $" ya llegó al tope de {TopeCupos} trabajadores: elige otra.";
    }

    /// <summary>
    /// Una ficha con su razón social vigente ya resuelta (ver
    /// <see cref="RazonSocialCuposHelper.RazonSocialVigente"/>). A diferencia de
    /// <see cref="FichaQueOcupaCupo"/>, puede venir sin razón social.
    /// </summary>
    public class FichaRazonSocial
    {
        public int WorkerId { get; set; }

        /// <summary>La razón social vigente, o null si la ficha no tiene ninguna.</summary>
        public int? ContributorId { get; set; }
    }

    /// <summary>
    /// Una ficha que consume cupo, con la razón social que le toca hoy ya resuelta. Se proyecta a
    /// ids y no al <c>Worker</c> entero a propósito: los cuatro consumidores del conteo solo
    /// necesitan agrupar, y el único que quiere las fichas las vuelve a pedir por
    /// <see cref="RazonSocialCuposHelper.FichasDe"/> en la misma consulta.
    /// </summary>
    public class FichaQueOcupaCupo
    {
        public int WorkerId { get; set; }

        /// <summary>
        /// La razón social vigente: la de la vinculación abierta, o la de la ficha si no tiene
        /// ninguna. Nullable por la proyección, pero <see cref="RazonSocialCuposHelper.OcupanCupo"/>
        /// ya descartó las filas donde quedaría en null.
        /// </summary>
        public int? ContributorId { get; set; }
    }

    /// <summary>Opción del desplegable "Razón social activa", con sus cupos disponibles.</summary>
    public class RazonSocialCupoDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;

        /// <summary>
        /// Cupos disponibles = tope (<see cref="RazonSocialCuposHelper.TopeCupos"/>) − trabajadores
        /// vigentes de la razón social en la base maestra que son de Staff, Oficina Central o
        /// Personal Externo (el personal de Obra y los practicantes no consumen cupo). Nunca
        /// negativo (se muestra 0).
        /// </summary>
        public int CuposDisponibles { get; set; }
    }
}
