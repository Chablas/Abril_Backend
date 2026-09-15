using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;

namespace Abril_Backend.Features.GestionAdministrativa.Rendiciones.Application.Dtos
{
    /// <summary>
    /// Una planilla de rendición del trabajador. Es la unidad de esta pantalla: una planilla = un
    /// PDF = un registro en el S10, y puede agrupar varias salidas.
    ///
    /// Los conteos y el monto están acotados a las salidas DEL TRABAJADOR: una planilla generada
    /// por el revisor desde Gestión de Salidas puede mezclar a varias personas, y en "Mis
    /// Rendiciones" contar las de otros sería mentir. Los documentos (planilla, copia firmada,
    /// consolidado), en cambio, son de la planilla entera: el papel es uno solo.
    /// </summary>
    public class RendicionListItemDto
    {
        public int Id { get; set; }

        /// <summary>
        /// Código REN-AAAA-NNNN de la rendición: es lo que el trabajador reconoce y lo que se
        /// conserva cuando una rendición observada se vuelve a generar.
        /// </summary>
        public string Codigo { get; set; } = string.Empty;

        /// <summary>Correlativo impreso en la planilla ("TI: 000123"). Null en las que no lo tienen.</summary>
        public string? NumeroPlanilla { get; set; }

        /// <summary>Cuándo se rindió (es lo que fija el orden por defecto de la tabla).</summary>
        public DateTimeOffset RendidoAt { get; set; }

        /// <summary>Periodo que cubren las salidas propias ("Agosto 2026", o "Jul — Ago 2026" si cruza meses).</summary>
        public string Periodo { get; set; } = string.Empty;

        /// <summary>Año/mes de la salida más antigua de la planilla — es la clave del filtro de periodo.</summary>
        public int PeriodoAnio { get; set; }
        public int PeriodoMes { get; set; }

        /// <summary>Cuántas salidas propias entran en esta planilla.</summary>
        public int SalidasCount { get; set; }

        /// <summary>Suma de lo rendido en las salidas propias de esta planilla.</summary>
        public decimal MontoTotal { get; set; }

        /// <summary>
        /// Monto de la planilla COMPLETA (todas sus salidas, de todos sus trabajadores). Es el
        /// importe que se registró en el S10, así que es contra este —y no contra
        /// <see cref="MontoTotal"/>, que está recortado— que tiene que cuadrar el monto del
        /// Consolidado del S10. Coinciden salvo en las planillas que agrupan a varias personas.
        /// </summary>
        public decimal MontoTotalPlanilla { get; set; }

        // ── Documentos de la planilla ────────────────────────────────────
        public string PdfUrl { get; set; } = string.Empty;
        public string PdfFilename { get; set; } = string.Empty;

        /// <summary>Copia firmada por la jefatura. Null mientras nadie la firme.</summary>
        public string? PdfFirmadoUrl { get; set; }
        public string? PdfFirmadoFilename { get; set; }
        public DateTimeOffset? FirmadoAt { get; set; }

        /// <summary>Consolidado del S10 vigente de la planilla. Null si todavía no se adjuntó.</summary>
        public ConsolidadoS10Dto? ConsolidadoS10 { get; set; }

        // ── Primera revisión ─────────────────────────────────────────────
        // El paso que va ANTES del Consolidado del S10: el jefe revisa tramos, montos y capturas.
        // Es de la PLANILLA, así que no se resume de las salidas como el reembolso.

        /// <summary>"Lista para enviar" | "En primera revisión" | "Aprobada" | "Observada".</summary>
        public string EstadoPrimeraRevision { get; set; } = EstadosSalida.PrimeraRevision.NombreBorrador;

        /// <summary>Cuándo se envió a primera revisión. Null si todavía no se envió.</summary>
        public DateTimeOffset? EnviadaRevisionAt { get; set; }

        /// <summary>Cuándo decidió el jefe la primera revisión. Null si todavía no decidió.</summary>
        public DateTimeOffset? PrimeraRevisionAt { get; set; }

        /// <summary>Comentario del jefe al observar: es lo que hay que corregir.</summary>
        public string? PrimeraRevisionObservacion { get; set; }

        /// <summary>True si se puede enviar a primera revisión (está "Lista para enviar").</summary>
        public bool PuedeEnviarPrimeraRevision { get; set; }

        /// <summary>
        /// True si está observada: el trabajador tiene que corregir las capturas y los montos de sus
        /// salidas y volver a generar la planilla.
        /// </summary>
        public bool PuedeSubsanar { get; set; }

        // ── Reembolso ────────────────────────────────────────────────────
        /// <summary>
        /// Estado del reembolso de la planilla, resumido a partir de las salidas propias: gana el
        /// que más atención pide (Observado > Pendiente > Aprobado > Firmado > Pagado), porque
        /// mientras una salida siga atrás la planilla no está cerrada.
        /// </summary>
        public string EstadoReembolso { get; set; } = EstadosSalida.Reembolso.NombrePendiente;

        /// <summary>True si las salidas propias no están todas en el mismo estado de reembolso.</summary>
        public bool ReembolsoMixto { get; set; }

        /// <summary>
        /// Comentario con el que se observó el reembolso: es lo que hay que subsanar. Null si no
        /// está observado.
        /// </summary>
        public string? ObservacionReembolso { get; set; }

        /// <summary>
        /// Quién escribió esa observación: "Jefatura" o "Tesorería" (RG-49). Vacío si no hay
        /// observación. La pantalla lo usa para rotular la caja roja — el trabajador que ya vio su
        /// planilla firmada tiene que saber que quien la devolvió fue Tesorería y no su jefe.
        /// </summary>
        public string ObservacionReembolsoOrigen { get; set; } = string.Empty;

        /// <summary>Última vez que se le avisó al revisor por esta planilla. Null si nunca.</summary>
        public DateTimeOffset? RevisorNotificadoAt { get; set; }

        /// <summary>
        /// True cuando se puede adjuntar o reemplazar el Consolidado del S10: la primera revisión
        /// tiene que estar APROBADA (RG-35) y el reembolso seguir abierto (Pendiente u Observado)
        /// — después de aprobado no tendría a quién avisarle ni qué reabrir.
        /// </summary>
        public bool PuedeAdjuntarConsolidado { get; set; }

        /// <summary>
        /// True cuando el Consolidado del S10 de la planilla es compartido con otras planillas que
        /// siguen con el reembolso por decidir (lo adjuntó un consolidador para varias a la vez).
        /// Un documento compartido se reemplaza entero, así que desde esta pantalla —que es de una
        /// planilla— no se puede: el botón queda apagado y el reemplazo se hace en Gestión de
        /// Rendiciones.
        /// </summary>
        public bool ConsolidadoCompartido { get; set; }

        /// <summary>True cuando ya hay consolidado adjunto y el reembolso sigue abierto.</summary>
        public bool PuedeNotificarRevisor { get; set; }

        // ── Corrección con el Coordinador ERP ────────────────────────────
        // El camino alternativo cuando la jefatura observa el reembolso y el arreglo tiene que
        // hacerse DENTRO del S10, donde el trabajador no tiene permiso (§10.5).

        /// <summary>
        /// La solicitud de corrección viva de esta planilla. Null en el caso normal: la mayoría de
        /// las planillas nunca pasa por el ERP. Cuando está, su estado dice de quién es la pelota
        /// (esperando al ERP, o ya atendida y esperando la recarga del consolidado).
        /// </summary>
        public CorreccionS10Dto? CorreccionS10 { get; set; }

        /// <summary>
        /// True cuando el trabajador puede PEDIRLE la corrección al Coordinador ERP: el reembolso
        /// tiene que estar observado, con el Consolidado del S10 adjunto (es el documento que hay
        /// que corregir) y sin otra corrección ya en curso.
        ///
        /// Es un camino ALTERNATIVO, no obligatorio: si el trabajador puede arreglar el S10 él
        /// mismo, vuelve a adjuntar el consolidado y listo. Pedir el paso por el ERP para algo que
        /// resuelve solo sería fricción.
        /// </summary>
        public bool PuedeSolicitarCorreccion { get; set; }
    }

    /// <summary>Una salida dentro de la planilla, para el detalle.</summary>
    public class RendicionSalidaDto
    {
        public int Id { get; set; }
        public string? Codigo { get; set; }
        public DateOnly FechaSalida { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string? LugarOrigen { get; set; }
        public string? LugarDestino { get; set; }
        public int TrayectosCount { get; set; }
        public decimal Monto { get; set; }
        public string EstadoReembolso { get; set; } = EstadosSalida.Reembolso.NombrePendiente;
        public string? ObservacionReembolso { get; set; }
    }

    /// <summary>La planilla con el desglose de sus salidas propias.</summary>
    public class RendicionDetalleDto : RendicionListItemDto
    {
        public List<RendicionSalidaDto> Salidas { get; set; } = new();
    }

    public class RendicionFiltersDto
    {
        /// <summary>
        /// "Lista para enviar" | "En primera revisión" | "Aprobada" | "Observada" | null para todas.
        /// </summary>
        public string? EstadoPrimeraRevision { get; set; }

        /// <summary>"Pendiente" | "Aprobado" | "Observado" | "Firmado" | "Pagado" | null para todos.</summary>
        public string? EstadoReembolso { get; set; }

        /// <summary>true = solo con consolidado adjunto; false = solo sin él; null = todas.</summary>
        public bool? ConConsolidado { get; set; }

        /// <summary>Periodo (mes de la salida más antigua de la planilla). Los dos o ninguno.</summary>
        public int? PeriodoAnio { get; set; }
        public int? PeriodoMes { get; set; }
    }

    /// <summary>
    /// Números de las tarjetas del encabezado. Se cuentan sobre el MISMO conjunto que muestra la
    /// tabla (con los filtros ya aplicados), así que acompañan a la búsqueda; por eso viajan en la
    /// respuesta del listado y no en <c>filter-data</c>. Son las tres cosas que le pueden faltar al
    /// trabajador, en el orden del flujo.
    /// </summary>
    public class ResumenRendicionesDto
    {
        /// <summary>Planillas rendidas que todavía no se enviaron a primera revisión.</summary>
        public int PorEnviar { get; set; }
        /// <summary>
        /// Aprobadas en primera revisión y sin el Consolidado del S10 adjunto: el paso habilitado.
        /// Las que no pasaron la primera revisión no cuentan — ahí el S10 ni se puede cargar.
        /// </summary>
        public int SinConsolidado { get; set; }
        /// <summary>Con consolidado y reembolso abierto, pero sin avisarle todavía al revisor.</summary>
        public int PorAvisar { get; set; }
        /// <summary>
        /// Lo que espera al trabajador: la primera revisión observada (rehacer la rendición) o el
        /// reembolso observado (volver a adjuntar el consolidado). Van juntas porque para él son la
        /// misma cosa —algo suyo volvió con observaciones— y la fila dice cuál de las dos es.
        /// </summary>
        public int Observadas { get; set; }
        /// <summary>
        /// Con una corrección del S10 en curso: la pelota está en el Coordinador ERP y al
        /// trabajador no le toca nada hasta que confirme. Va aparte de Observadas justamente por
        /// eso — verlas juntas haría pensar que hay algo que hacer.
        /// </summary>
        public int EnErp { get; set; }

        /// <summary>Cuenta las cuatro bandejas sobre las planillas recibidas (el conjunto ya filtrado).</summary>
        public static ResumenRendicionesDto De(IEnumerable<RendicionListItemDto> rendiciones)
        {
            var lista = rendiciones as ICollection<RendicionListItemDto> ?? rendiciones.ToList();
            return new ResumenRendicionesDto
            {
                PorEnviar      = lista.Count(x => x.PuedeEnviarPrimeraRevision),
                SinConsolidado = lista.Count(x => x.ConsolidadoS10 == null
                                              && x.EstadoPrimeraRevision == EstadosSalida.PrimeraRevision.NombreAprobada),
                PorAvisar      = lista.Count(x => x.PuedeNotificarRevisor && x.RevisorNotificadoAt == null),
                Observadas     = lista.Count(x => x.PuedeSubsanar
                                              || x.EstadoReembolso == EstadosSalida.Reembolso.NombreObservado),
                EnErp          = lista.Count(x => x.CorreccionS10?.EsperandoErp == true),
            };
        }
    }

    /// <summary>
    /// Cuerpo de "Solicitar corrección al ERP". Un solo campo, que es el «MOTIVO *» del
    /// requerimiento: viaja en el cuerpo y no en la query porque es texto libre y largo.
    /// </summary>
    public class SolicitarCorreccionS10Dto
    {
        /// <summary>Qué corrección se necesita en el S10. Obligatorio (RG-21 / CA-17).</summary>
        public string Motivo { get; set; } = string.Empty;
    }

    /// <summary>Respuesta del listado: las planillas y las tarjetas de ese mismo conjunto.</summary>
    public class RendicionListResultDto
    {
        public List<RendicionListItemDto> Data { get; set; } = new();
        public ResumenRendicionesDto Resumen { get; set; } = new();
    }

    /// <summary>Un periodo ofrecido por el filtro (mes con al menos una planilla del trabajador).</summary>
    public class PeriodoOptionDto
    {
        public int Anio { get; set; }
        public int Mes { get; set; }
        /// <summary>"Agosto 2026" — ya capitalizado, la pantalla lo imprime tal cual.</summary>
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>
    /// Datos de arranque de "Mis Rendiciones": lo que NO cambia al mover los filtros. Por eso las
    /// opciones del filtro de periodo viajan junto a los destinatarios de los correos que dispara
    /// la pantalla, que son los mismos para toda ella (está acotada a un solo trabajador) y no se
    /// vuelven a pedir con cada búsqueda.
    /// </summary>
    public class RendicionFilterDataDto
    {
        public List<PeriodoOptionDto> Periodos { get; set; } = new();

        /// <summary>
        /// A quién le llega el aviso de la primera revisión (Configuración → Correos → «1.ª
        /// revisión al revisor»). Lo dispara "Enviar a revisión".
        /// </summary>
        public CorreoDestinatariosDto CorreoPrimeraRevision { get; set; } = new();

        /// <summary>
        /// A quién le llega el aviso de que ya se adjuntó el Consolidado del S10 (Configuración →
        /// Correos → «S10 al revisor»). Lo dispara "Avisar al revisor".
        /// </summary>
        public CorreoDestinatariosDto CorreoS10Revisor { get; set; } = new();

        /// <summary>
        /// A quién le llega la solicitud de corrección del S10. A diferencia de los otros dos, su
        /// destinatario principal se resuelve por ROL (COORDINADOR ERP) y no por el organigrama del
        /// trabajador: el responsable ERP es uno para toda la organización.
        /// </summary>
        public CorreoDestinatariosDto CorreoCorreccionS10 { get; set; } = new();
    }

    /// <summary>
    /// El trabajador dueño de las salidas propias de una planilla, con lo que necesitan los correos
    /// de la primera revisión. Se resuelve en una consulta: el correo no vuelve a la base.
    /// </summary>
    public class RendicionSolicitanteDto
    {
        /// <summary>Ficha del trabajador (<c>workers.id</c>) — con esto se resuelve su jefe.</summary>
        public int WorkerId { get; set; }
        public string Trabajador { get; set; } = "Trabajador";
        /// <summary>Correo del usuario del trabajador. Null si su persona no tiene usuario.</summary>
        public string? Email { get; set; }
        /// <summary>Nombre del área a la que entra por su puesto. Null si no se resuelve.</summary>
        public string? Area { get; set; }
    }
}
