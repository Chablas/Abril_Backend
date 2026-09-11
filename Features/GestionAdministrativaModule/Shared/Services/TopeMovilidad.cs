using Abril_Backend.Features.GestionAdministrativa.Shared.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Tope de movilidad (S/ 45 por defecto, configurable en
    /// <c>ga_rendicion_config.limite_diario_movilidad</c>). El mismo número manda en dos momentos
    /// distintos del flujo, y conviene no confundirlos:
    ///
    /// <list type="bullet">
    ///   <item><b>Al subir capturas</b> — es el máximo de UN TRAYECTO. Varios trayectos que
    ///   individualmente no lo pasan sí pueden sumar más de S/ 45 entre todos: eso está permitido
    ///   y no se corta acá.</item>
    ///   <item><b>Al imprimir la planilla</b> — es el máximo de UN DÍA. Lo que un día no aguanta
    ///   se imputa al día siguiente (ver <see cref="ImputacionMovilidadPlanilla"/>), que es lo que
    ///   pide RG-42 del requerimiento funcional: trasladar el exceso, no impedirlo.</item>
    /// </list>
    ///
    /// Antes el tope se controlaba por FECHA DE SALIDA sumando todas las salidas del día, y guardar
    /// se bloqueaba al pasarse. Eso contradecía RG-42: el trabajador no puede rendir menos de lo
    /// que realmente gastó, lo que corresponde es repartirlo. Por eso la validación bajó al
    /// trayecto y el reparto por día subió a la planilla.
    ///
    /// El importe de cada trayecto se resuelve con la MISMA precedencia que imprime la planilla
    /// (<see cref="ImporteRendidoLoader"/>): la suma de sus capturas si tiene alguna con monto, y
    /// si no el tarifario de <c>ga_trayecto</c> cuando el trabajador es de TI. Acá está repetida
    /// —y no reusada— porque el control necesita simular el lote ANTES de escribirlo, sobre montos
    /// que todavía no están en la base.
    /// </summary>
    public static class TopeMovilidad
    {
        /// <summary>
        /// Tope con el que se responde si <c>ga_rendicion_config</c> está vacía (base sin sembrar).
        /// Es el valor acordado con GTH, el mismo que trae la columna por defecto.
        /// </summary>
        public const decimal PorDefecto = 45m;

        /// <summary>
        /// Deja el tope dentro del rango que valida la base (CHECK) y cubre la fila ausente. Una
        /// config torcida por SQL a mano no puede abrir el tope de par en par ni dejarlo en cero.
        /// </summary>
        public static decimal Acotar(decimal? limite) => Math.Clamp(
            limite ?? PorDefecto,
            GaRendicionConfig.LimiteDiarioMinimo,
            GaRendicionConfig.LimiteDiarioMaximo);

        // Nadie lee el tope "suelto": los repositorios que ya consultan la solicitud lo traen en su
        // propia consulta y llaman directo a Acotar, y quien va a imputar fechas lo recibe dentro
        // de CalendarioNoLaborable.LimiteMovilidad, que sale de la misma fila de config.

        /// <summary>
        /// Un trayecto de la solicitud que se está editando, con lo que hoy tiene cargado. Guarda
        /// los montos captura por captura (y no su suma) porque el lote puede corregir una captura
        /// puntual: para saber cuánto quedaría hay que reemplazar ese monto, no sumarle encima.
        /// </summary>
        public sealed class TrayectoConMontos
        {
            public int TrayectoId { get; init; }

            /// <summary>Posición del trayecto en la solicitud. Solo para nombrarlo en el error.</summary>
            public int Orden { get; init; }

            /// <summary>capturaId → monto guardado. Las eliminadas no están (filtro global de state).</summary>
            public Dictionary<int, decimal> Capturas { get; init; } = new();

            /// <summary>
            /// Monto del tarifario <c>ga_trayecto</c> para ese par origen-destino, solo si el
            /// trabajador es de TI. Cuenta únicamente cuando el trayecto queda sin capturas: en
            /// cuanto hay una, manda la captura.
            /// </summary>
            public decimal? MontoCatalogo { get; init; }

            /// <summary>
            /// Lo que este trayecto costaría si el lote entrara: sus capturas con los montos
            /// corregidos que traiga <paramref name="montosEditados"/>, más
            /// <paramref name="montosNuevos"/>, y el tarifario solo si así queda en cero.
            /// </summary>
            public decimal Importe(
                IReadOnlyDictionary<int, decimal>? montosEditados = null,
                decimal montosNuevos = 0m)
            {
                var suma = montosNuevos;
                foreach (var (capturaId, monto) in Capturas)
                    suma += montosEditados != null && montosEditados.TryGetValue(capturaId, out var editado)
                        ? editado
                        : monto;

                return suma > 0m ? suma : (MontoCatalogo ?? 0m);
            }

            /// <summary>
            /// Lo que el trayecto cuesta tal como está guardado hoy. Es contra esto que se mide si
            /// un lote EMPEORA un trayecto que ya venía excedido.
            /// </summary>
            public decimal ImporteGuardado => Importe();
        }

        /// <summary>
        /// Los trayectos de la solicitud que se está editando y el tope con el que se los compara.
        /// Ya no hace falta mirar las otras salidas del día: el tope es de cada trayecto.
        /// </summary>
        public sealed class ContextoCapturas
        {
            /// <summary>Tope por trayecto, ya acotado.</summary>
            public decimal Limite { get; init; }

            public List<TrayectoConMontos> Trayectos { get; init; } = new();
        }

        /// <summary>
        /// Trae los trayectos de <paramref name="solicitudId"/> con los montos de sus capturas
        /// vivas, para poder simular el lote encima antes de escribirlo.
        /// </summary>
        /// <param name="limiteConfigurado">
        /// Tope leído por el llamador (normalmente en la misma consulta con la que ya buscó la
        /// solicitud). Null = no hay fila de config y se usa <see cref="PorDefecto"/>.
        /// </param>
        /// <param name="catalogo">
        /// Tarifario ya cargado, si el llamador lo tenía a mano (el detalle de la salida lo arma
        /// igual para pintar el monto de catálogo). Null = se carga acá, y solo si hace falta.
        /// </param>
        public static async Task<ContextoCapturas> CargarAsync(
            AppDbContext ctx, int solicitudId, string? subarea,
            decimal? limiteConfigurado,
            IReadOnlyDictionary<(int, int), decimal>? catalogo = null)
        {
            var trayectos = await ctx.GaSolicitudTrayecto
                .Where(t => t.SolicitudId == solicitudId)
                .OrderBy(t => t.Orden)
                .Select(t => new { t.Id, t.Orden, t.LugarOrigenId, t.LugarDestinoId })
                .ToListAsync();

            if (trayectos.Count == 0)
                return new ContextoCapturas { Limite = Acotar(limiteConfigurado) };

            // Las capturas van en su propio viaje —y no como subconsulta del proyectado— para
            // quedarse con la forma que usa el resto del módulo. Las eliminadas no vienen: el
            // filtro global de GaSolicitudCaptura las saca de toda lectura.
            var trayectoIds = trayectos.Select(t => t.Id).ToList();
            var capturas = await ctx.GaSolicitudCaptura
                .Where(c => trayectoIds.Contains(c.TrayectoId))
                .Select(c => new { c.Id, c.TrayectoId, c.Monto })
                .ToListAsync();

            var capturasPorTrayecto = capturas
                .GroupBy(c => c.TrayectoId)
                .ToDictionary(g => g.Key, g => g.ToDictionary(c => c.Id, c => c.Monto));

            // El tarifario se carga solo para TI, y solo si hay algún trayecto con los dos lugares
            // del catálogo. Se carga aunque el trayecto ya tenga capturas: el lote podría dejarlo
            // en cero y ahí el tarifario vuelve a mandar.
            var tarifario = catalogo
                ?? (ImporteRendidoLoader.EsTi(subarea)
                    && trayectos.Any(t => t.LugarOrigenId.HasValue && t.LugarDestinoId.HasValue)
                        ? await ImporteRendidoLoader.CargarCatalogoAsync(ctx)
                        : new Dictionary<(int, int), decimal>());

            var lista = new List<TrayectoConMontos>(trayectos.Count);
            foreach (var t in trayectos)
            {
                decimal? montoCatalogo = null;
                if (t.LugarOrigenId.HasValue && t.LugarDestinoId.HasValue
                    && tarifario.TryGetValue((t.LugarOrigenId.Value, t.LugarDestinoId.Value), out var monto))
                    montoCatalogo = monto;

                lista.Add(new TrayectoConMontos
                {
                    TrayectoId    = t.Id,
                    Orden         = t.Orden,
                    Capturas      = capturasPorTrayecto.GetValueOrDefault(t.Id) ?? new(),
                    MontoCatalogo = montoCatalogo,
                });
            }

            return new ContextoCapturas
            {
                Limite    = Acotar(limiteConfigurado),
                Trayectos = lista,
            };
        }
    }
}
