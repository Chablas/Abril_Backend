using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Services.Actores.Interfaces;
using Abril_Backend.Shared.Services.Jerarquia;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Shared.Services.Actores.Services
{
    /// <summary>
    /// Implementación de <see cref="IActoresResolver"/>. Ver ahí el orden completo.
    ///
    /// Lo que decide está en dos métodos y en ningún otro lado: <see cref="ResolverGrupo"/> (qué
    /// gana entre lo personalizado y el algoritmo, nodo por nodo) y <see cref="Algoritmo"/> (a quién
    /// señala el sistema en un nodo para un caso). Las cuatro entradas públicas solo arman el CONTEXTO
    /// —de quién se habla, en qué área, con qué obra, quién está dentro del documento— y los llaman;
    /// un documento, además, junta lo que resuelve cada uno de sus trabajadores.
    /// Copiar una de estas reglas afuera es volver a abrir la puerta a que la ficha, la pantalla de
    /// Revisores de Áreas y quien recibe el correo muestren personas distintas.
    /// </summary>
    public class ActoresResolver : IActoresResolver
    {
        private const string EmailDomainCorp = EstructuraAreaLoader.EmailDomainCorp;

        /// <summary>Nombre exacto del área en area_item cuyo area_scope.email es el último recurso.</summary>
        private const string AreaGthNombre = "Gestión del Talento Humano";

        private static readonly IReadOnlyDictionary<int, List<ActorPersona>> SinPersonalizados =
            new Dictionary<int, List<ActorPersona>>();

        private readonly IDbContextFactory<AppDbContext> _factory;

        public ActoresResolver(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        // ══ Entradas ═════════════════════════════════════════════════════════

        public async Task<Dictionary<int, ActoresDeTrabajador>> ResolverTrabajadoresAsync(
            IReadOnlyCollection<int> workerIds, IReadOnlyCollection<int>? actorIds = null)
        {
            var resultado = new Dictionary<int, ActoresDeTrabajador>();

            var ids = workerIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0) return resultado;

            var actores = ActoresPedidos(actorIds);

            using var ctx = _factory.CreateDbContext();

            var fichas   = await CargarFichasAsync(ctx, ids);
            var obraDe   = await ObrasLoader.ObraVigentePorTrabajadorAsync(ctx, ids);
            var contexto = await CargarContextoAsync(
                ctx,
                fichas.Values.Where(f => f.AreaScopeId != null).Select(f => f.AreaScopeId!.Value),
                ids);

            foreach (var ficha in fichas.Values)
            {
                obraDe.TryGetValue(ficha.WorkerId, out var proyecto);
                resultado[ficha.WorkerId] = ResolverFicha(contexto, ficha, proyecto, actores);
            }

            return resultado;
        }

        public async Task<ActoresDeTrabajador> ResolverContextoAsync(ContextoTrabajador contexto)
        {
            using var ctx = _factory.CreateDbContext();

            var workerId = contexto.WorkerId is > 0 ? contexto.WorkerId.Value : 0;
            int? personId = workerId > 0
                ? await ctx.Worker.AsNoTracking()
                    .Where(w => w.Id == workerId)
                    .Select(w => w.PersonId)
                    .FirstOrDefaultAsync()
                : null;

            var cargado = await CargarContextoAsync(
                ctx,
                contexto.AreaScopeId is int nodo ? new[] { nodo } : Array.Empty<int>(),
                workerId > 0 ? new[] { workerId } : Array.Empty<int>());

            var ficha = new Ficha(workerId, personId, contexto.AreaScopeId, contexto.CategoriaId);
            return ResolverFicha(cargado, ficha, contexto.ProyectoId, ActorIds.Todos);
        }

        public async Task<Dictionary<int, List<ActorPersona>>> ResolverDocumentosAsync(
            IReadOnlyDictionary<int, IReadOnlyCollection<int>> workersPorDocumento, int actorId)
        {
            var resultado = new Dictionary<int, List<ActorPersona>>();

            var docs = workersPorDocumento
                .Select(kv => (Id: kv.Key, Workers: kv.Value.Where(id => id > 0).Distinct().ToList()))
                .Where(d => d.Workers.Count > 0)
                .ToList();
            if (docs.Count == 0) return resultado;

            using var ctx = _factory.CreateDbContext();

            var todos    = docs.SelectMany(d => d.Workers).Distinct().ToList();
            var fichas   = await CargarFichasAsync(ctx, todos);
            var obraDe   = await ObrasLoader.ObraVigentePorTrabajadorAsync(ctx, todos);
            var contexto = await CargarContextoAsync(
                ctx,
                fichas.Values.Where(f => f.AreaScopeId != null).Select(f => f.AreaScopeId!.Value),
                todos);

            foreach (var (docId, ids) in docs)
                resultado[docId] = ResolverDocumento(contexto, ids, fichas, obraDe, actorId).Personas;

            return resultado;
        }

        public async Task<Dictionary<(int Nodo, int? Proyecto), Dictionary<int, Dictionary<int, ActorResultado>>>> PrevisualizarAsync(
            IReadOnlyCollection<FilaPrevisualizacion> filas)
        {
            var resultado = new Dictionary<(int Nodo, int? Proyecto), Dictionary<int, Dictionary<int, ActorResultado>>>();
            if (filas.Count == 0) return resultado;

            using var ctx = _factory.CreateDbContext();

            var contexto = await CargarContextoAsync(
                ctx, filas.Select(f => f.Nodo).Distinct(), Array.Empty<int>());

            foreach (var fila in filas)
            {
                var cadena = contexto.Cadena(fila.Nodo);
                var esObra = fila.Proyecto is int p && contexto.Estructura.Obras.Contains(p);

                var porCaso = new Dictionary<int, Dictionary<int, ActorResultado>>();
                foreach (var caso in fila.Casos.Distinct())
                {
                    var sujeto = new Sujeto(
                        cadena, caso, fila.Proyecto, esObra, SinPersonalizados, Excluir: null,
                        Previsualizacion: true);

                    porCaso[caso] = ActorIds.Todos.ToDictionary(a => a, a =>
                    {
                        var r = ResolverActor(contexto, sujeto, a);

                        // Si lo que manda está personalizado EN ESTA fila, se calcula también lo que
                        // quedaría sin eso: es la recomendación que la pantalla muestra al lado.
                        if (r.Origen != ActorOrigen.Area || r.NodoOrigen != fila.Nodo || r.ProyectoOrigen != fila.Proyecto)
                            return r;

                        var sinFila = ResolverActor(
                            contexto, sujeto with { IgnorarFila = (fila.Nodo, fila.Proyecto) }, a);
                        return r.ConSinPersonalizar(sinFila);
                    });
                }

                resultado[(fila.Nodo, fila.Proyecto)] = porCaso;
            }

            return resultado;
        }

        // ══ Armado del contexto de cada pregunta ═════════════════════════════

        /// <summary>Los actores de una ficha (existente o hipotética) con su obra.</summary>
        private static ActoresDeTrabajador ResolverFicha(
            Contexto contexto, Ficha ficha, int? proyecto, IReadOnlyCollection<int> actores)
        {
            var sujeto = SujetoDe(contexto, ficha, proyecto);

            // La 1.ª revisión y la firma del consolidado se deciden sobre un DOCUMENTO, y en el suyo
            // el trabajador está dentro: el algoritmo no lo señala a él para lo suyo (al administrador
            // de obra, que para su gente es el que revisa, su propia planilla la revisa la jefatura del
            // área). Lo personalizado sí vale aunque sea él. Es exactamente lo que hace
            // ResolverDocumento con su planilla, así que la ficha muestra lo que va a pasar.
            var comoDocumento = sujeto with { Excluir = EsDe(new[] { ficha }) };

            return new ActoresDeTrabajador
            {
                WorkerId    = ficha.WorkerId > 0 ? ficha.WorkerId : null,
                AreaScopeId = ficha.AreaScopeId,
                CasoId      = sujeto.CasoId,
                ProyectoId  = proyecto,
                EsObra      = sujeto.EsObra,
                Actores     = actores.ToDictionary(
                    a => a,
                    a => ResolverActor(
                        contexto,
                        a is ActorIds.AprobadorPrimeraRevision or ActorIds.AprobadorConsolidado ? comoDocumento : sujeto,
                        a)),
            };
        }

        /// <summary>
        /// Los que intervienen en un documento que agrupa a <paramref name="ids"/>: los de CADA
        /// trabajador, resueltos igual que en su ficha —lo personalizado para él, lo personalizado de
        /// su área para su caso y su obra, el algoritmo—, y juntos: si todos resuelven lo mismo es eso,
        /// y si no intervienen todos, sin romper el orden de ninguno. Así lo que muestra la ficha de
        /// cada uno es lo que pasa con su documento, aunque lo comparta con otros.
        ///
        /// Lo único que el documento agrega es quién está DENTRO: el algoritmo no señala a nadie de
        /// adentro (usa su respaldo o sube); lo personalizado sí, porque se eligió a mano. El buzón de
        /// GTH entra solo si nadie del documento tiene a otro.
        /// </summary>
        private static ActorResultado ResolverDocumento(
            Contexto contexto, List<int> ids, IReadOnlyDictionary<int, Ficha> fichas,
            IReadOnlyDictionary<int, int?> obraDe, int actorId)
        {
            var grupo   = ids.Select(id => fichas.TryGetValue(id, out var f) ? f : new Ficha(id, null, null, null)).ToList();
            var adentro = EsDe(grupo);

            var resueltos = grupo
                .Select(f =>
                {
                    obraDe.TryGetValue(f.WorkerId, out var obra);
                    return ResolverActor(contexto, SujetoDe(contexto, f, obra) with { Excluir = adentro }, actorId);
                })
                .Where(r => r.Personas.Count > 0)
                .ToList();

            var conAlguien = resueltos.Where(r => r.Origen != ActorOrigen.Gth).ToList();
            var usados     = conAlguien.Count > 0 ? conAlguien : resueltos;
            var origenes   = usados.Select(r => r.Origen).Distinct().ToList();

            return new ActorResultado
            {
                ActorId  = actorId,
                Personas = Combinar(usados.Select(r => r.Personas).ToList()),
                Origen   = origenes.Count == 1 ? origenes[0] : ActorOrigen.Algoritmo,
            };
        }

        /// <summary>
        /// De quién se habla en una ficha: su cadena de áreas, su caso (por su categoría, su obra y si
        /// administra alguna), su obra y lo personalizado para él.
        /// </summary>
        private static Sujeto SujetoDe(Contexto contexto, Ficha ficha, int? proyecto)
        {
            var esObra = proyecto is int p && contexto.Estructura.Obras.Contains(p);
            var esAdministrador = ficha.WorkerId > 0
                && contexto.Estructura.EsAdministradorDeObra(ficha.WorkerId, ficha.PersonId);

            var propios = ficha.WorkerId > 0 && contexto.Individuales.TryGetValue(ficha.WorkerId, out var ind)
                ? ind
                : SinPersonalizados;

            return new Sujeto(
                contexto.Cadena(ficha.AreaScopeId), ActorCasoIds.De(ficha.CategoriaId, esObra, esAdministrador),
                proyecto, esObra, propios, Excluir: null, Previsualizacion: false);
        }

        // ══════════════════════════════════════════════════════════════════════
        // NÚCLEO ÚNICO DE DECISIÓN
        //
        // ResolverActor → ResolverGrupo → Algoritmo. Por acá pasan la ficha del trabajador, la
        // pantalla de Revisores de Áreas, los correos y las guardas de las cuatro bandejas. Si una
        // regla cambia —qué gana, a quién señala el algoritmo, a quién se descarta— cambia acá y
        // todo se mueve junto.
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Un actor para un sujeto: lo personalizado del trabajador, o lo del grupo.</summary>
        private static ActorResultado ResolverActor(Contexto contexto, Sujeto sujeto, int actorId)
        {
            // El jefe notificado solo existe donde la salida la aprueba el residente (staff y
            // administrador de obra). En oficina central y en las jefaturas el que aprueba ya es el jefe.
            if (actorId == ActorIds.JefeNotificado && !ActorCasoIds.TieneJefeNotificado(sujeto.CasoId))
                return new ActorResultado { ActorId = actorId, Aplica = false };

            var grupo = ResolverGrupo(contexto, sujeto, actorId);

            if (sujeto.Personalizados.TryGetValue(actorId, out var propios) && propios.Count > 0)
            {
                return new ActorResultado
                {
                    ActorId         = actorId,
                    Personas        = Tomar(Distintas(propios), ActorIds.EsMultiple(actorId)),
                    Origen          = ActorOrigen.Trabajador,
                    SinPersonalizar = grupo,
                };
            }

            return grupo;
        }

        /// <summary>
        /// Lo que le toca al sujeto sin mirar lo personalizado para él: subiendo por su cadena de
        /// áreas, en cada nodo primero lo personalizado para su obra, después lo personalizado para
        /// el área entera y después el algoritmo — siempre para SU caso—. El primero que deje a
        /// alguien gana. Si nadie, GTH para los actores que deciden algo.
        ///
        /// Lo personalizado vale tal cual, aunque la persona esté dentro del documento: se eligió a
        /// mano y le gana al algoritmo. Solo el algoritmo salta a los de adentro.
        ///
        /// En los actores de uno solo gana el primero activo de la lista y el resto queda de
        /// respaldo; en los de varios cuentan todos los de la lista.
        /// </summary>
        private static ActorResultado ResolverGrupo(Contexto contexto, Sujeto sujeto, int actorId)
        {
            var multiple = ActorIds.EsMultiple(actorId);

            foreach (var nodo in sujeto.Cadena)
            {
                var cargadas = contexto.AsignadasPorNodo[nodo]
                    .Where(a => a.ActorId == actorId && a.CasoId == sujeto.CasoId)
                    .Where(a => sujeto.IgnorarFila is not { } fila
                             || a.AreaScopeId != fila.Nodo || a.ProjectId != fila.Proyecto)
                    .ToList();

                // a) Lo personalizado para la obra en este nodo.
                if (sujeto.ProyectoId != null)
                {
                    var deLaObra = Distintas(PorPrioridad(cargadas.Where(a => a.ProjectId == sujeto.ProyectoId)));
                    if (deLaObra.Count > 0)
                        return DeArea(actorId, Tomar(deLaObra, multiple), nodo, sujeto.ProyectoId);
                }

                // b) Lo personalizado para el área entera en este nodo: vale para todas sus obras.
                var delArea = Distintas(PorPrioridad(cargadas.Where(a => a.ProjectId == null)));
                if (delArea.Count > 0)
                    return DeArea(actorId, Tomar(delArea, multiple), nodo, null);

                // c) El algoritmo del nodo, sin los que están dentro del documento.
                var regla = Algoritmo(contexto, sujeto, nodo, actorId);

                if (regla.Descriptor != null)
                    return new ActorResultado { ActorId = actorId, Descriptor = regla.Descriptor };

                foreach (var tramo in regla.Tramos)
                {
                    var candidatos = tramo.Personas.Select(APersona);
                    var validas = tramo.AdmiteAdentro ? Distintas(candidatos) : Validas(sujeto, candidatos);
                    if (validas.Count > 0)
                        return new ActorResultado
                        {
                            ActorId  = actorId,
                            Personas = Tomar(validas, multiple && tramo.Todos),
                        };
                }
            }

            if (ActorIds.TieneRespaldoGth(actorId) && contexto.Gth != null)
                return new ActorResultado
                {
                    ActorId  = actorId,
                    Personas = new List<ActorPersona> { contexto.Gth },
                    Origen   = ActorOrigen.Gth,
                };

            return new ActorResultado { ActorId = actorId };
        }

        /// <summary>
        /// A quién señala el sistema en <paramref name="nodo"/> para el caso del sujeto, en tramos: se
        /// usa el primer tramo que deje a alguien, y el siguiente es su respaldo. Sin tramos, el nodo
        /// no aporta nada y se sigue subiendo.
        ///
        ///   • En un "Área de Gerencia", para todos los casos y todos los actores: su GERENTE.
        ///   • OFICINA CENTRAL: la jefatura del área (su SUB GERENTE, JEFE o RESIDENTE, por
        ///     precedencia) aprueba la salida, la 1.ª revisión y el consolidado; consolidan todas.
        ///   • STAFF (su obra vigente es una obra):
        ///       – aprobar la salida → el RESIDENTE de la obra (respaldo: la jefatura del área);
        ///       – jefe notificado  → la jefatura del área;
        ///       – 1.ª revisión     → el ADMINISTRADOR DE OBRA (respaldo: la jefatura del área);
        ///       – consolidar       → el administrador de obra y nadie más (sin él, la jefatura);
        ///       – firmar el consolidado → el administrador de obra y DESPUÉS el residente, en ese
        ///         orden (respaldo: la jefatura del área).
        ///   • ADMINISTRADOR DE OBRA: como el staff, salvo que la 1.ª revisión es del RESIDENTE, el
        ///     jefe notificado es nadie (ni siquiera en una gerencia: solo lo que se personalice) y el
        ///     consolidado lo firma él mismo con el residente, aunque esté dentro del documento.
        ///   • JEFE / SUBGERENTE / RESIDENTE: los aprueba y los firma un gerente, así que los nodos
        ///     estándar no aportan nada para esos actores y la búsqueda sube sola hasta la gerencia.
        ///     Los consolida su par: los de su misma categoría en su área —el propio incluido— y, a un
        ///     residente, los de su misma obra.
        /// </summary>
        private static Regla Algoritmo(Contexto contexto, Sujeto sujeto, int nodo, int actorId)
        {
            var jefatura = contexto.Estructura.JefaturaPorNodo[nodo].ToList();
            var esAdministrador = sujeto.CasoId == ActorCasoIds.AdministradorObra;

            // De las salidas del administrador de obra no se avisa a ningún jefe: ni el del área ni,
            // subiendo, el gerente. Solo lo que se personalice.
            if (esAdministrador && actorId == ActorIds.JefeNotificado)
                return Regla.Nada;

            // En una gerencia todo es su gerente: la jefatura de ese nodo solo tiene gerentes.
            if (contexto.Arbol.Gerencias.Contains(nodo))
                return Regla.De(new Tramo(jefatura, Todos: actorId == ActorIds.Consolidador));

            switch (sujeto.CasoId)
            {
                case ActorCasoIds.OficinaCentral:
                    return Regla.De(new Tramo(jefatura, Todos: actorId == ActorIds.Consolidador));

                case ActorCasoIds.Staff:
                case ActorCasoIds.AdministradorObra:
                {
                    // Una fila de la pantalla sin obra no puede nombrar a nadie de la obra: cada
                    // trabajador tiene la suya. Se describe la regla (el jefe notificado sí tiene
                    // nombre: es la jefatura del área).
                    if (sujeto.Previsualizacion && sujeto.ProyectoId == null && actorId != ActorIds.JefeNotificado)
                        return Regla.Describir(DescriptorDeLaObra(sujeto.CasoId, actorId));

                    EstructuraAreaLoader.PersonaDeArea? residente = null, administrador = null;
                    if (sujeto.EsObra && sujeto.ProyectoId is int obra)
                    {
                        contexto.Estructura.ResidentePorProyecto.TryGetValue(obra, out residente);
                        contexto.Estructura.AdministradorPorProyecto.TryGetValue(obra, out administrador);
                    }

                    var jefe = new Tramo(jefatura, Todos: false);

                    return actorId switch
                    {
                        ActorIds.AprobadorSalida          => Regla.De(Tramo.De(residente), jefe),
                        ActorIds.JefeNotificado           => Regla.De(jefe),
                        // Al administrador lo revisa el residente; a su gente, él.
                        ActorIds.AprobadorPrimeraRevision => Regla.De(Tramo.De(esAdministrador ? residente : administrador), jefe),
                        ActorIds.Consolidador             => administrador != null
                                                                 ? Regla.De(Tramo.De(administrador))
                                                                 : Regla.De(new Tramo(jefatura, Todos: true)),
                        // Doble firma: el administrador firma también lo suyo, porque después firma
                        // el residente.
                        ActorIds.AprobadorConsolidado     => Regla.De(
                                                                 Tramo.De(administrador, residente) with { AdmiteAdentro = esAdministrador },
                                                                 jefe),
                        _                                  => Regla.Nada,
                    };
                }

                default:
                {
                    // Jefaturas: aprobar y firmar es de un gerente (se sube hasta la gerencia).
                    if (actorId != ActorIds.Consolidador) return Regla.Nada;

                    if (sujeto.CasoId == ActorCasoIds.Residente && sujeto.Previsualizacion && sujeto.ProyectoId == null)
                        return Regla.Describir("Residentes de su obra");

                    var categoria = sujeto.CasoId switch
                    {
                        ActorCasoIds.Residente  => CategoriaIds.Residente,
                        ActorCasoIds.Subgerente => CategoriaIds.SubGerente,
                        _                       => CategoriaIds.Jefe,
                    };

                    var pares = jefatura
                        .Where(p => p.CategoriaId == categoria)
                        .Where(p => sujeto.CasoId != ActorCasoIds.Residente
                                    || (contexto.Estructura.ObraDeJefatura.TryGetValue(p.WorkerId, out var suObra)
                                            ? suObra
                                            : null) == sujeto.ProyectoId)
                        .ToList();

                    return Regla.De(new Tramo(pares, Todos: true));
                }
            }
        }

        /// <summary>Cómo se nombra, en una fila sin obra, al que el algoritmo saca de la obra.</summary>
        private static string DescriptorDeLaObra(int casoId, int actorId) => actorId switch
        {
            ActorIds.AprobadorSalida      => "Residente de la obra",
            ActorIds.AprobadorPrimeraRevision when casoId == ActorCasoIds.AdministradorObra
                                          => "Residente de la obra",
            ActorIds.AprobadorConsolidado => "Administrador de obra y residente",
            _                             => "Administrador de obra",
        };

        // ── Piezas del núcleo ─────────────────────────────────────────────────

        /// <summary>
        /// Lo que el algoritmo aporta en un nodo: tramos en orden (cada uno respaldo del anterior), o
        /// la descripción de una regla que no se puede resolver a una persona.
        /// </summary>
        private sealed record Regla(IReadOnlyList<Tramo> Tramos, string? Descriptor)
        {
            public static readonly Regla Nada = new(Array.Empty<Tramo>(), null);
            public static Regla De(params Tramo[] tramos) => new(tramos, null);
            public static Regla Describir(string descriptor) => new(Array.Empty<Tramo>(), descriptor);
        }

        /// <summary>
        /// Un grupo de candidatos del algoritmo. <paramref name="Todos"/> = en los actores de varios,
        /// cuentan todos (el administrador y el residente firman los dos); si no, solo el primero.
        /// <paramref name="AdmiteAdentro"/> = vale aunque esté dentro del documento (la firma del
        /// administrador de obra sobre lo suyo); si no, el algoritmo salta a los de adentro.
        /// </summary>
        private sealed record Tramo(
            IReadOnlyList<EstructuraAreaLoader.PersonaDeArea> Personas, bool Todos, bool AdmiteAdentro = false)
        {
            public static Tramo De(params EstructuraAreaLoader.PersonaDeArea?[] personas) =>
                new(personas.Where(p => p != null).Select(p => p!).ToList(), Todos: true);
        }

        /// <summary>
        /// De quién se está hablando: su cadena de áreas, su caso, su obra, lo personalizado para él y
        /// a quiénes no puede señalar el algoritmo por estar dentro del documento.
        /// </summary>
        /// <param name="Previsualizacion">
        /// true = una fila de Revisores de Áreas y no un trabajador: sin obra, lo que depende de la
        /// obra se describe en vez de resolverse.
        /// </param>
        /// <param name="IgnorarFila">
        /// Una fila de Revisores de Áreas cuyo personalizado no cuenta: para mostrar qué quedaría si
        /// se lo quita.
        /// </param>
        private sealed record Sujeto(
            IReadOnlyList<int> Cadena,
            int CasoId,
            int? ProyectoId,
            bool EsObra,
            IReadOnlyDictionary<int, List<ActorPersona>> Personalizados,
            Func<ActorPersona, bool>? Excluir,
            bool Previsualizacion,
            (int Nodo, int? Proyecto)? IgnorarFila = null);

        /// <summary>Candidatos del algoritmo sin repetir persona y sin los que están dentro del documento.</summary>
        private static List<ActorPersona> Validas(Sujeto sujeto, IEnumerable<ActorPersona> candidatos)
            => Distintas(candidatos.Where(p => sujeto.Excluir == null || !sujeto.Excluir(p)));

        private static List<ActorPersona> Distintas(IEnumerable<ActorPersona> personas)
        {
            var vistas = new HashSet<string>();
            return personas.Where(p => vistas.Add(Clave(p))).ToList();
        }

        /// <summary>Todos (actor de varios) o solo el primero.</summary>
        private static List<ActorPersona> Tomar(List<ActorPersona> personas, bool todos)
            => todos ? personas : personas.Take(1).ToList();

        private static ActorResultado DeArea(int actorId, List<ActorPersona> personas, int nodo, int? proyecto) => new()
        {
            ActorId        = actorId,
            Personas       = personas,
            Origen         = ActorOrigen.Area,
            NodoOrigen     = nodo,
            ProyectoOrigen = proyecto,
        };

        private static IEnumerable<ActorPersona> PorPrioridad(IEnumerable<Asignada> filas)
            => filas.OrderBy(a => a.OrdenPrioridad).ThenBy(a => a.Id).Select(a => a.Persona);

        /// <summary>Identidad para comparar: la persona si la tiene, si no la ficha (o el buzón).</summary>
        private static string Clave(ActorPersona p) =>
            p.PersonId != null ? $"p{p.PersonId}" : p.WorkerId != null ? $"w{p.WorkerId}" : $"a{p.AreaScopeId}";

        private static ActorPersona APersona(EstructuraAreaLoader.PersonaDeArea p)
            => new(p.WorkerId, null, p.PersonId, p.Email, p.Nombre, p.CategoriaId);

        /// <summary>
        /// "Está dentro": una de estas fichas o la misma persona con otra ficha (reingreso). El
        /// algoritmo no señala a nadie para un documento donde está incluido.
        /// </summary>
        private static Func<ActorPersona, bool> EsDe(IReadOnlyCollection<Ficha> fichas)
        {
            var workers  = fichas.Where(f => f.WorkerId > 0).Select(f => f.WorkerId).ToHashSet();
            var personas = fichas.Where(f => f.PersonId != null).Select(f => f.PersonId!.Value).ToHashSet();
            return p => (p.WorkerId is int w && workers.Contains(w))
                     || (p.PersonId is int per && personas.Contains(per));
        }

        /// <summary>
        /// Junta lo de varios trabajadores en una sola lista, sin repetir personas y sin romper el orden
        /// de ninguna (en obra, el administrador firma antes que el residente). Entre dos que ninguna
        /// lista ordena va primero el que está más arriba en la suya y, a igual altura, el del
        /// trabajador que viene antes. Si dos listas se contradicen, gana la misma regla.
        /// </summary>
        private static List<ActorPersona> Combinar(IReadOnlyList<List<ActorPersona>> listas)
        {
            var persona   = new Dictionary<string, ActorPersona>();
            var prioridad = new Dictionary<string, (int Altura, int Lista)>();
            var despues   = new Dictionary<string, HashSet<string>>();
            var antes     = new Dictionary<string, int>();

            for (var i = 0; i < listas.Count; i++)
            {
                var lista = Distintas(listas[i]);
                for (var j = 0; j < lista.Count; j++)
                {
                    var clave = Clave(lista[j]);
                    if (persona.TryAdd(clave, lista[j]))
                    {
                        prioridad[clave] = (j, i);
                        despues[clave]   = new HashSet<string>();
                        antes[clave]     = 0;
                    }
                    else if ((j, i).CompareTo(prioridad[clave]) < 0)
                    {
                        prioridad[clave] = (j, i);
                    }

                    if (j > 0 && despues[Clave(lista[j - 1])].Add(clave))
                        antes[clave]++;
                }
            }

            var resultado  = new List<ActorPersona>();
            var pendientes = persona.Keys.ToList();
            while (pendientes.Count > 0)
            {
                var libres  = pendientes.Where(c => antes[c] <= 0).ToList();
                var elegido = (libres.Count > 0 ? libres : pendientes)
                    .OrderBy(c => prioridad[c].Altura)
                    .ThenBy(c => prioridad[c].Lista)
                    .First();

                resultado.Add(persona[elegido]);
                pendientes.Remove(elegido);
                foreach (var siguiente in despues[elegido]) antes[siguiente]--;
            }

            return resultado;
        }

        private static IReadOnlyCollection<int> ActoresPedidos(IReadOnlyCollection<int>? actorIds)
        {
            if (actorIds == null || actorIds.Count == 0) return ActorIds.Todos;
            var pedidos = actorIds.Where(ActorIds.Todos.Contains).Distinct().ToList();
            return pedidos.Count == 0 ? ActorIds.Todos : pedidos;
        }

        // ══ Carga (un número fijo de consultas) ══════════════════════════════

        /// <summary>Lo que el núcleo necesita saber de un trabajador.</summary>
        /// <param name="WorkerId">0 = un trabajador que todavía no existe.</param>
        private sealed record Ficha(int WorkerId, int? PersonId, int? AreaScopeId, int? CategoriaId);

        /// <summary>Una fila viva y activa de <c>area_actor_asignacion</c>, ya con su persona.</summary>
        private sealed record Asignada(
            int AreaScopeId, int? ProjectId, int ActorId, int CasoId, int OrdenPrioridad, int Id,
            ActorPersona Persona);

        /// <summary>Todo lo que usa el núcleo, cargado de una vez.</summary>
        private sealed class Contexto
        {
            public required EstructuraAreaLoader.Arbol Arbol { get; init; }
            public required Dictionary<int, List<int>> CadenaPorNodo { get; init; }
            public required EstructuraAreaLoader.EstructuraArea Estructura { get; init; }
            public required ILookup<int, Asignada> AsignadasPorNodo { get; init; }

            /// <summary>workerId → actorId → lo personalizado para él, en orden.</summary>
            public required Dictionary<int, Dictionary<int, List<ActorPersona>>> Individuales { get; init; }

            public ActorPersona? Gth { get; init; }

            public IReadOnlyList<int> Cadena(int? nodo) =>
                nodo is int n && CadenaPorNodo.TryGetValue(n, out var cadena) ? cadena : Array.Empty<int>();
        }

        private static async Task<Contexto> CargarContextoAsync(
            AppDbContext ctx, IEnumerable<int> nodosDesde, IReadOnlyCollection<int> workerIds)
        {
            var arbol         = await EstructuraAreaLoader.CargarArbolConTiposAsync(ctx);
            var cadenaPorNodo = EstructuraAreaLoader.ConstruirCadenas(nodosDesde, arbol.PadreDe);
            var nodos         = cadenaPorNodo.Values.SelectMany(c => c).Distinct().ToList();

            return new Contexto
            {
                Arbol            = arbol,
                CadenaPorNodo    = cadenaPorNodo,
                Estructura       = await EstructuraAreaLoader.CargarAsync(ctx, nodos),
                AsignadasPorNodo = await CargarAsignadasAsync(ctx, nodos),
                Individuales     = await CargarIndividualesAsync(ctx, workerIds),
                Gth              = await CargarGthAsync(ctx),
            };
        }

        /// <summary>
        /// Ficha de cada trabajador pedido: su persona y, por su puesto, su nodo de área y su
        /// categoría. Un id que no encuentra ficha viva igual se resuelve (sin área): puede tener lo
        /// personalizado y, si no, cae en GTH, como antes.
        /// </summary>
        private static async Task<Dictionary<int, Ficha>> CargarFichasAsync(AppDbContext ctx, List<int> ids)
        {
            var fichas = (await ctx.Worker.AsNoTracking()
                    .Where(w => ids.Contains(w.Id))
                    .Select(w => new
                    {
                        w.Id,
                        w.PersonId,
                        AreaScopeId = w.PuestoCatalogo != null ? w.PuestoCatalogo.AreaDestinoScopeId : null,
                        CategoriaId = w.PuestoCatalogo != null ? (int?)w.PuestoCatalogo.CategoriaId : null,
                    })
                    .ToListAsync())
                .ToDictionary(w => w.Id, w => new Ficha(w.Id, w.PersonId, w.AreaScopeId, w.CategoriaId));

            foreach (var id in ids.Where(id => !fichas.ContainsKey(id)))
                fichas[id] = new Ficha(id, null, null, null);

            return fichas;
        }

        /// <summary>
        /// Lo personalizado por área en los nodos en juego: filas vivas y activas cuyo asignado tiene
        /// correo corporativo. No se mira el estado laboral de la ficha: la designación es explícita.
        /// </summary>
        private static async Task<ILookup<int, Asignada>> CargarAsignadasAsync(AppDbContext ctx, List<int> nodos)
        {
            if (nodos.Count == 0) return Array.Empty<Asignada>().ToLookup(a => a.AreaScopeId);

            var filas = await (
                from a in ctx.AreaActorAsignacion.AsNoTracking()
                where a.State && a.Active && nodos.Contains(a.AreaScopeId)
                join w in ctx.Worker.AsNoTracking() on a.WorkerId equals w.Id
                where w.EmailCorporativo != null
                      && w.EmailCorporativo.Trim().ToLower().EndsWith(EmailDomainCorp)
                select new
                {
                    a.AreaScopeId, a.ProjectId, a.GaActorId, a.GaActorCasoId, a.OrdenPrioridad,
                    a.AreaActorAsignacionId,
                    w.Id, w.PersonId, w.EmailCorporativo,
                    Nombre      = w.Person != null ? w.Person.FullName : null,
                    CategoriaId = w.PuestoCatalogo != null ? (int?)w.PuestoCatalogo.CategoriaId : null,
                }
            ).ToListAsync();

            return filas.ToLookup(
                f => f.AreaScopeId,
                f => new Asignada(
                    f.AreaScopeId, f.ProjectId, f.GaActorId, f.GaActorCasoId, f.OrdenPrioridad, f.AreaActorAsignacionId,
                    new ActorPersona(f.Id, null, f.PersonId, f.EmailCorporativo!.Trim(), f.Nombre, f.CategoriaId)));
        }

        /// <summary>Lo personalizado para cada trabajador pedido, por actor y en orden.</summary>
        private static async Task<Dictionary<int, Dictionary<int, List<ActorPersona>>>> CargarIndividualesAsync(
            AppDbContext ctx, IReadOnlyCollection<int> workerIds)
        {
            var resultado = new Dictionary<int, Dictionary<int, List<ActorPersona>>>();
            if (workerIds.Count == 0) return resultado;

            var ids = workerIds as List<int> ?? workerIds.ToList();

            var filas = await (
                from r in ctx.WorkersActorAsignacion.AsNoTracking()
                where r.State && r.Active && ids.Contains(r.WorkerId)
                join w in ctx.Worker.AsNoTracking() on r.AsignadoId equals w.Id
                where w.EmailCorporativo != null
                      && w.EmailCorporativo.Trim().ToLower().EndsWith(EmailDomainCorp)
                orderby r.OrdenPrioridad, r.WorkersActorAsignacionId
                select new
                {
                    r.WorkerId, r.GaActorId,
                    w.Id, w.PersonId, w.EmailCorporativo,
                    Nombre      = w.Person != null ? w.Person.FullName : null,
                    CategoriaId = w.PuestoCatalogo != null ? (int?)w.PuestoCatalogo.CategoriaId : null,
                }
            ).ToListAsync();

            foreach (var porTrabajador in filas.GroupBy(f => f.WorkerId))
                resultado[porTrabajador.Key] = porTrabajador
                    .GroupBy(f => f.GaActorId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(f => new ActorPersona(
                                f.Id, null, f.PersonId, f.EmailCorporativo!.Trim(), f.Nombre, f.CategoriaId))
                              .ToList());

            return resultado;
        }

        /// <summary>El buzón del área de GTH (area_scope.email): el último recurso.</summary>
        private static async Task<ActorPersona?> CargarGthAsync(AppDbContext ctx)
        {
            var gth = await (
                from s in ctx.AreaScope.AsNoTracking()
                join ai in ctx.AreaItem.AsNoTracking() on s.AreaItemId equals ai.AreaItemId
                where s.State && ai.State
                      && ai.AreaItemName == AreaGthNombre
                      && s.Email != null && s.Email != ""
                orderby s.AreaScopeId
                select new { s.AreaScopeId, s.Email, ai.AreaItemName }
            ).FirstOrDefaultAsync();

            return gth == null
                ? null
                : new ActorPersona(null, gth.AreaScopeId, null, gth.Email!.Trim(), gth.AreaItemName, null);
        }
    }
}
