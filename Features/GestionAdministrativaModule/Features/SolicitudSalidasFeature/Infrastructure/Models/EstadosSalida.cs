namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models
{
    /// <summary>
    /// Catálogos de estados de una solicitud de salida. Los ids deben reflejar exactamente
    /// las filas sembradas en las tablas <c>ga_estado_aprobacion</c> y <c>ga_estado_rendicion</c>.
    ///
    /// La lógica de negocio compara/asigna por <b>id</b> (constantes). Los nombres se usan solo
    /// para exponerlos en los DTOs (contrato con el frontend) y para traducir los filtros que
    /// el frontend envía como texto.
    /// </summary>
    public static class EstadosSalida
    {
        public static class Aprobacion
        {
            public const int Pendiente = 1;
            public const int Aprobado  = 2;
            public const int Rechazado = 3;
            /// <summary>El propio solicitante retiró su solicitud mientras estaba Pendiente. Estado terminal.</summary>
            public const int Cancelado = 4;

            public const string NombrePendiente = "Pendiente";
            public const string NombreAprobado  = "Aprobado";
            public const string NombreRechazado = "Rechazado";
            public const string NombreCancelado = "Cancelado";

            /// <summary>id → nombre para exponer en DTOs.</summary>
            public static string Nombre(int id) => id switch
            {
                Pendiente => NombrePendiente,
                Aprobado  => NombreAprobado,
                Rechazado => NombreRechazado,
                Cancelado => NombreCancelado,
                _         => string.Empty,
            };

            /// <summary>nombre (filtro del frontend) → id, o null si no corresponde a ninguno.</summary>
            public static int? IdFromNombre(string? nombre) => nombre?.Trim() switch
            {
                NombrePendiente => Pendiente,
                NombreAprobado  => Aprobado,
                NombreRechazado => Rechazado,
                NombreCancelado => Cancelado,
                _               => null,
            };
        }

        /// <summary>
        /// Estado del REEMBOLSO de una salida ya rendida. Es un eje aparte de
        /// <see cref="Aprobacion"/> (la salida en sí) y de <see cref="Rendicion"/>: solo empieza a
        /// moverse cuando la salida está Rendida Y tiene adjunto el Consolidado del S10, que es lo
        /// que el jefe revisa para dar el visto bueno al gasto.
        ///
        /// Pendiente → Aprobado → Firmado → Pagado, o Rechazado (con observación) hasta que el
        /// trabajador subsane volviendo a subir el Consolidado del S10, que lo devuelve a Pendiente.
        /// Los ids reflejan las filas de <c>ga_estado_reembolso</c>.
        /// </summary>
        public static class Reembolso
        {
            public const int Pendiente = 1;
            public const int Aprobado  = 2;
            public const int Rechazado = 3;
            /// <summary>El jefe ya firmó la planilla de rendición de esta salida.</summary>
            public const int Firmado   = 4;
            /// <summary>Tesorería ya pagó el reembolso. Estado terminal.</summary>
            public const int Pagado    = 5;

            public const string NombrePendiente = "Pendiente";
            public const string NombreAprobado  = "Aprobado";
            public const string NombreRechazado = "Rechazado";
            public const string NombreFirmado   = "Firmado";
            public const string NombrePagado    = "Pagado";

            /// <summary>id → nombre para exponer en DTOs.</summary>
            public static string Nombre(int id) => id switch
            {
                Pendiente => NombrePendiente,
                Aprobado  => NombreAprobado,
                Rechazado => NombreRechazado,
                Firmado   => NombreFirmado,
                Pagado    => NombrePagado,
                _         => string.Empty,
            };

            /// <summary>nombre (filtro del frontend) → id, o null si no corresponde a ninguno.</summary>
            public static int? IdFromNombre(string? nombre) => nombre?.Trim() switch
            {
                NombrePendiente => Pendiente,
                NombreAprobado  => Aprobado,
                NombreRechazado => Rechazado,
                NombreFirmado   => Firmado,
                NombrePagado    => Pagado,
                _               => null,
            };

            /// <summary>
            /// Los dos estados que ve Tesorería: lo que ya firmó la jefatura y lo que ya se pagó.
            /// </summary>
            public static readonly int[] VisiblesParaTesoreria = { Firmado, Pagado };
        }

        /// <summary>
        /// Estado de la PRIMERA revisión de una planilla de rendición — el eje que vive en
        /// <c>ga_rendicion</c> y no en la salida, porque lo que el jefe revisa es el documento y
        /// este cubre todas las salidas que agrupa.
        ///
        /// Es anterior al <see cref="Reembolso"/>: la primera revisión mira tramos, montos y
        /// capturas registrados en Abril One, y solo con su aprobación se habilita cargar el
        /// Consolidado del S10 (RG-35). El reembolso es la SEGUNDA revisión, la que compara el
        /// total del consolidado contra el de la planilla.
        ///
        /// Borrador → EnRevision → Aprobada, u Observada (con comentario del revisor) hasta que el
        /// trabajador corrija capturas y montos y vuelva a generar el PDF, que la devuelve a
        /// Borrador para reenviarla. Los ids reflejan las filas de <c>ga_estado_primera_revision</c>.
        /// </summary>
        public static class PrimeraRevision
        {
            /// <summary>Recién rendida: el PDF ya está, pero el trabajador todavía no la envió.</summary>
            public const int Borrador   = 1;
            /// <summary>Enviada al jefe: espera que apruebe u observe.</summary>
            public const int EnRevision = 2;
            /// <summary>Aprobada: habilita cargar el Consolidado del S10.</summary>
            public const int Aprobada   = 3;
            /// <summary>Observada con comentario: el trabajador tiene que rehacer la rendición.</summary>
            public const int Observada  = 4;

            public const string NombreBorrador   = "Lista para enviar";
            public const string NombreEnRevision = "En primera revisión";
            public const string NombreAprobada   = "Aprobada";
            public const string NombreObservada  = "Observada";

            /// <summary>id → nombre para exponer en DTOs.</summary>
            public static string Nombre(int id) => id switch
            {
                Borrador   => NombreBorrador,
                EnRevision => NombreEnRevision,
                Aprobada   => NombreAprobada,
                Observada  => NombreObservada,
                _          => string.Empty,
            };

            /// <summary>nombre (filtro del frontend) → id, o null si no corresponde a ninguno.</summary>
            public static int? IdFromNombre(string? nombre) => nombre?.Trim() switch
            {
                NombreBorrador   => Borrador,
                NombreEnRevision => EnRevision,
                NombreAprobada   => Aprobada,
                NombreObservada  => Observada,
                _                => null,
            };
        }

        public static class Rendicion
        {
            public const int NoRendido = 1;
            public const int Rendido   = 2;

            public const string NombreNoRendido = "No rendido";
            public const string NombreRendido   = "Rendido";

            /// <summary>id → nombre para exponer en DTOs.</summary>
            public static string Nombre(int id) => id switch
            {
                NoRendido => NombreNoRendido,
                Rendido   => NombreRendido,
                _         => string.Empty,
            };

            /// <summary>nombre (filtro del frontend) → id, o null si no corresponde a ninguno.</summary>
            public static int? IdFromNombre(string? nombre) => nombre?.Trim() switch
            {
                NombreNoRendido => NoRendido,
                NombreRendido   => Rendido,
                _               => null,
            };
        }
    }
}
