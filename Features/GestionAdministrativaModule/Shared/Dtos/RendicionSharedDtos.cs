using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Dtos
{
    // DTOs del ciclo de la rendición que usan varias features del módulo (Gestión de Salidas,
    // Gestión de Rendiciones y Reembolsos), así que viven en el Shared del módulo y no dentro de
    // la carpeta de una de ellas.

    /// <summary>
    /// Una planilla de rendición pendiente de firma, con las salidas de la selección que cuelgan
    /// de ella. El PDF se firma UNA vez por planilla aunque la selección traiga varias salidas
    /// suyas: el documento es uno solo.
    /// </summary>
    public class RendicionPorFirmarDto
    {
        public int RendicionId { get; set; }
        /// <summary>webUrl del PDF original de la planilla (el que se descarga para estampar).</summary>
        public string PdfUrl { get; set; } = string.Empty;
        public string PdfFilename { get; set; } = string.Empty;
        /// <summary>webUrl de la copia ya firmada, si otra firma anterior la genero.</summary>
        public string? PdfFirmadoUrl { get; set; }
        /// <summary>Salidas de la seleccion que cuelgan de esta planilla y estan listas para firmar.</summary>
        public List<int> SolicitudIds { get; set; } = new();
    }

    /// <summary>
    /// Lo que necesitan los correos del reembolso de UNA salida. Sale de una sola consulta para no
    /// volver a la base por cada correo.
    /// </summary>
    public class ReembolsoCorreoInfoDto
    {
        public int SolicitudId { get; set; }
        public int WorkerId { get; set; }
        public string Trabajador { get; set; } = string.Empty;
        /// <summary>
        /// Identificador que ve el trabajador: el código SOL-AAAA-NNNN. Se resuelve igual que en
        /// SolicitudSalidaService para que el mismo pedido no salga con dos identificadores
        /// distintos; las solicitudes anteriores al código conservan su "#N" por trabajador.
        /// </summary>
        public string Codigo { get; set; } = string.Empty;
        /// <summary>Correo del solicitante (app_user.email). Null si no tiene usuario.</summary>
        public string? SolicitanteEmail { get; set; }
        public string? Area { get; set; }
        public DateOnly FechaSalida { get; set; }
        /// <summary>Numero de planilla formateado ("TI: 000123"), o null si no tiene planilla.</summary>
        public string? NumeroPlanilla { get; set; }
        /// <summary>
        /// Planilla a la que pertenece la salida. Es el destino del boton del correo: lo que el
        /// trabajador tiene que hacer despues de una decision (subsanar volviendo a adjuntar el
        /// Consolidado del S10) vive en Mis Rendiciones, no en la salida.
        /// </summary>
        public int? RendicionId { get; set; }
        public int TrayectosCount { get; set; }
        public decimal MontoTotal { get; set; }
        public string EstadoReembolso { get; set; } = string.Empty;
        public string? ObservacionReembolso { get; set; }
        /// <summary>Nombre de quien decidio el reembolso (para mostrarlo en el correo).</summary>
        public string? DecididoPor { get; set; }
    }

    /// <summary>
    /// Lo que necesitan los correos de la decisión de la PRIMERA revisión, por trabajador de la
    /// planilla. Sale de una sola consulta para no volver a la base por cada correo.
    ///
    /// Es por trabajador y no por planilla porque una planilla generada por el revisor desde
    /// Gestión de Salidas puede agrupar a varias personas, y el aviso ("tu rendición fue aprobada")
    /// tiene que llegarle a cada dueño con SUS números, no con el total del documento.
    /// </summary>
    public class PrimeraRevisionCorreoInfoDto
    {
        public int RendicionId { get; set; }
        /// <summary>Código REN-AAAA-NNNN de la planilla (o "#id" en las anteriores al código).</summary>
        public string Codigo { get; set; } = string.Empty;
        /// <summary>Numero de planilla formateado ("TI: 000123"), o null si no tiene.</summary>
        public string? NumeroPlanilla { get; set; }

        public int WorkerId { get; set; }
        public string Trabajador { get; set; } = string.Empty;
        /// <summary>Correo del solicitante (app_user.email). Null si no tiene usuario.</summary>
        public string? SolicitanteEmail { get; set; }
        public string? Area { get; set; }

        /// <summary>Periodo que cubren SUS salidas de la planilla ("Agosto 2026", o un rango).</summary>
        public string? Periodo { get; set; }
        /// <summary>Cuántas salidas suyas entran en la planilla.</summary>
        public int SalidasCount { get; set; }
        /// <summary>Cuántos tramos suman esas salidas — es lo que el revisor mira.</summary>
        public int TramosCount { get; set; }
        /// <summary>Suma de lo rendido en sus salidas, con la misma regla que imprime la planilla.</summary>
        public decimal MontoTotal { get; set; }

        /// <summary>Nombre de quien decidió la primera revisión (para mostrarlo en el correo).</summary>
        public string? DecididoPor { get; set; }
        /// <summary>Comentario con el que se observó. Null al aprobar.</summary>
        public string? Observacion { get; set; }
    }

    /// <summary>Resultado de una accion en bloque sobre el reembolso.</summary>
    public class ReembolsoBulkResultDto
    {
        /// <summary>Cuantas salidas cambiaron de estado.</summary>
        public int Procesadas { get; set; }
        /// <summary>Cuantas planillas distintas se firmaron (solo lo usa Firmar).</summary>
        public int PlanillasFirmadas { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>Nodo del árbol area_scope (lista plana; el frontend arma la jerarquía). </summary>
    public class AreaNodeDto
    {
        public int AreaScopeId { get; set; }
        public int AreaItemId { get; set; }
        public string AreaItemName { get; set; } = string.Empty;
        public int AreaTypeId { get; set; }
        public string AreaTypeName { get; set; } = string.Empty;
        public int? AreaScopeParentId { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class TrabajadorOptionDto
    {
        public int WorkerId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
    }

}
