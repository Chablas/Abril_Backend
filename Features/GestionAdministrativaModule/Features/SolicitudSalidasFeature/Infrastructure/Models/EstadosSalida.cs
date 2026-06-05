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

            public const string NombrePendiente = "Pendiente";
            public const string NombreAprobado  = "Aprobado";
            public const string NombreRechazado = "Rechazado";

            /// <summary>id → nombre para exponer en DTOs.</summary>
            public static string Nombre(int id) => id switch
            {
                Pendiente => NombrePendiente,
                Aprobado  => NombreAprobado,
                Rechazado => NombreRechazado,
                _         => string.Empty,
            };

            /// <summary>nombre (filtro del frontend) → id, o null si no corresponde a ninguno.</summary>
            public static int? IdFromNombre(string? nombre) => nombre?.Trim() switch
            {
                NombrePendiente => Pendiente,
                NombreAprobado  => Aprobado,
                NombreRechazado => Rechazado,
                _               => null,
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
