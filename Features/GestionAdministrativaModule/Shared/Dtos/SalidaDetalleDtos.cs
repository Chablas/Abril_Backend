namespace Abril_Backend.Features.GestionAdministrativa.Shared.Dtos
{
    // El detalle de UNA solicitud de salida: cabecera, trayectos con sus capturas y adjuntos, y los
    // documentos de su planilla. Vive en el Shared del módulo porque lo muestran cuatro pantallas
    // con el mismo modal: Solicitud de Salidas (el trabajador, que además edita sus capturas) y
    // Gestión de Rendiciones, Consolidados y Reembolsos (quien revisa la salida de otro, en
    // consulta). Lo arma SalidaDetalleLoader.

    public class SolicitudSalidaCapturaDto
    {
        public int Id { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public DateTimeOffset UploadedAt { get; set; }
    }

    /// <summary>Un documento adjunto (prueba) de un trayecto, para mostrar en el detalle.</summary>
    public class TrayectoAdjuntoDto
    {
        public string Url { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
    }

    public class TrayectoDetalleDto
    {
        public int Id { get; set; }
        public int Orden { get; set; }
        /// <summary>Null en trayectos de motivos que no piden horario.</summary>
        public TimeOnly? HoraSalida { get; set; }
        public TimeOnly? HoraRetorno { get; set; }
        public string Motivo { get; set; } = string.Empty;
        /// <summary>Detalle escrito por el trabajador cuando el motivo lo exige. Null si no aplica.</summary>
        public string? MotivoAdicional { get; set; }
        public string? LugarOrigen { get; set; }
        public string? LugarDestino { get; set; }
        /// <summary>Documentos adjuntos del trayecto (motivos con requiere_adjunto). Vacío si no tiene.</summary>
        public List<TrayectoAdjuntoDto> Adjuntos { get; set; } = new();
        public List<SolicitudSalidaCapturaDto> Capturas { get; set; } = new();
        /// <summary>
        /// Monto del catálogo <c>ga_trayecto</c> que matchea (origen, destino) — solo poblado
        /// cuando el trabajador pertenece a "Tecnología de la Información" y el par de lugares
        /// está registrado y activo. Null en cualquier otro caso.
        /// </summary>
        public decimal? MontoCatalogo { get; set; }
        /// <summary>
        /// Monto final a usar para este trayecto:
        ///   - Si hay capturas → suma de montos de capturas.
        ///   - Si no hay capturas pero existe <see cref="MontoCatalogo"/> → ese valor.
        ///   - Si no hay ni capturas ni catálogo → 0.
        /// </summary>
        public decimal MontoTotal { get; set; }
        /// <summary>
        /// Si el trayecto genera reembolso de movilidad: lo concede el motivo del catálogo
        /// (Configuración → Motivos) y el par (origen, destino) puede anularlo, nunca al revés
        /// (ver <c>ReembolsoTrayectoRule</c>). Null con motivo libre: no está en el catálogo, así
        /// que no tiene el flag configurado y el detalle no muestra el pill.
        /// </summary>
        public bool? EsReembolsable { get; set; }
    }

    /// <summary>PDF de la planilla de rendición (SharePoint) asociado a la solicitud. Null si aún no se rindió.</summary>
    public class SolicitudSalidaRendicionDto
    {
        public int Id { get; set; }
        public string PdfUrl { get; set; } = string.Empty;
        public string PdfFilename { get; set; } = string.Empty;
        public DateTimeOffset RendidoAt { get; set; }
    }

    public class SolicitudSalidaDetalleDto
    {
        public int Id { get; set; }
        /// <summary>Código SOL-AAAA-NNNN. Null solo en solicitudes anteriores a la columna.</summary>
        public string? Codigo { get; set; }

        /// <summary>
        /// Nombre del trabajador dueño de la salida. Lo muestran las pantallas que revisan salidas
        /// de otros; en Solicitud de Salidas es el propio usuario y no se imprime.
        /// </summary>
        public string? Trabajador { get; set; }

        public DateOnly FechaSalida { get; set; }
        public string EstadoAprobacion { get; set; } = string.Empty;
        public string EstadoRendicion { get; set; } = "No rendido";
        public DateTimeOffset CreatedAt { get; set; }
        public string? MotivoRechazo { get; set; }
        /// <summary>PDF de la planilla de rendición. Null si la solicitud aún no fue rendida.</summary>
        public SolicitudSalidaRendicionDto? Rendicion { get; set; }
        /// <summary>PDF Consolidado del S10 vigente (propio de la salida o heredado de su planilla). Null si no hay.</summary>
        public ConsolidadoS10Dto? ConsolidadoS10 { get; set; }

        /// <summary>
        /// Tope en soles de CADA trayecto (<c>ga_rendicion_config</c>). Varios trayectos pueden
        /// sumar más que esto entre todos: lo que un día no aguanta se reparte al imprimir la
        /// planilla, no se bloquea al cargarlo (ver <c>TopeMovilidad</c>). El modal de capturas lo
        /// usa para pintar el tope de cada trayecto mientras el trabajador escribe.
        /// </summary>
        public decimal LimiteMovilidadTrayecto { get; set; }

        /// <summary>
        /// True si la salida está lista para rendirse. Misma definición que
        /// <c>SolicitudSalidaListItemDto.AptaParaRendir</c> —aprobada, no rendida, con todos sus
        /// trayectos cubiertos, con motivo reembolsable y dentro del plazo—: el botón "Rendir" del
        /// modal de detalle y el de la columna de acciones tienen que aparecer o faltar juntos, y
        /// ninguno de los dos puede ofrecer algo que después el rendir vaya a rechazar.
        ///
        /// Solo lo calcula el detalle del propio trabajador: en las pantallas de consulta nadie
        /// rinde por otro, así que ahí viaja siempre en false.
        /// </summary>
        public bool AptaParaRendir { get; set; }

        /// <summary>
        /// El recorrido del reembolso de esta salida —de la solicitud al pago—, para el pipeline del
        /// modal de detalle. Lo arma <c>ReembolsoPipelineBuilder</c> con lo que este loader ya trajo:
        /// no cuesta ni un viaje más a la base.
        /// </summary>
        public ReembolsoPipelineDto Pipeline { get; set; } = new();

        public List<TrayectoDetalleDto> Trayectos { get; set; } = new();
    }
}
