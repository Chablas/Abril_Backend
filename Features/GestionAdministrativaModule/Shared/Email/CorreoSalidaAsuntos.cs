using Abril_Backend.Features.GestionAdministrativa.Shared.Services;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Email
{
    /// <summary>
    /// Los asuntos de los correos de Gestión Administrativa, en un solo lugar: los arma el envío, y
    /// la Configuración de cada pantalla los muestra bajo el nombre de cada correo para buscarlos en
    /// Enviados (<see cref="Plantillas"/>).
    /// </summary>
    public static class CorreoSalidaAsuntos
    {
        // ── Solicitud de Salidas ─────────────────────────────────────────────

        public static string Revisor(string? codigo, string? solicitante, DateOnly fechaSalida) =>
            Revisor(codigo, solicitante, Fecha(fechaSalida));

        public static string RevisorJefeArea(string? codigo, string? solicitante, DateOnly fechaSalida) =>
            RevisorJefeArea(codigo, solicitante, Fecha(fechaSalida));

        public static string Confirmacion(string? codigo, DateOnly fechaSalida) =>
            Confirmacion(codigo, Fecha(fechaSalida));

        /// <param name="periodo">El mes que se rinde, en palabras («septiembre 2026»).</param>
        public static string RecordatorioApertura(string? periodo) =>
            $"Ya puedes rendir tus movilidades de {periodo}";

        /// <param name="periodo">El mes que se rinde, en palabras («septiembre 2026»).</param>
        public static string RecordatorioCierre(string? periodo) =>
            $"Hoy vence el plazo para rendir tus movilidades de {periodo}";

        // ── Gestión de Salidas ───────────────────────────────────────────────

        public static string Aprobada(string? codigo, DateOnly fechaSalida) =>
            Aprobada(codigo, Fecha(fechaSalida));

        public static string Rechazada(string? codigo, DateOnly fechaSalida) =>
            Rechazada(codigo, Fecha(fechaSalida));

        // ── Mis Rendiciones ──────────────────────────────────────────────────

        public static string RendicionPorRevisar(string? codigo, string? trabajador) =>
            $"Rendición por revisar - {codigo} - {trabajador}";

        public static string RendicionEnviada(string? codigo) =>
            $"Tu rendición {codigo} está en revisión";

        // ── Gestión de Rendiciones ───────────────────────────────────────────

        public static string RendicionPrimeraAprobada(string? codigo) =>
            $"Rendición {codigo} APROBADA en primera revisión";

        public static string RendicionPrimeraObservada(string? codigo) =>
            $"Rendición {codigo} OBSERVADA en primera revisión";

        /// <summary>Al consolidador, cuando se aprueba una sola rendición.</summary>
        public static string DisponibleParaConsolidar(string? codigo, string? trabajador) =>
            $"Rendición disponible para consolidar - {codigo} - {trabajador}";

        /// <summary>Al consolidador, cuando se aprueban varias a la vez: un solo correo con todas.</summary>
        public static string DisponiblesParaConsolidar(int cantidad) =>
            DisponiblesParaConsolidar($"{cantidad}");

        public static string RendicionEnPlanillaGrupal(string? codigo, string? planillaGrupal) =>
            $"Rendición {codigo} incluida en la planilla grupal {planillaGrupal}";

        /// <param name="consolidado">Código CONS-…; los anteriores al código salen como «un consolidado».</param>
        public static string RendicionConsolidada(string? codigo, string? consolidado) =>
            $"Rendición {codigo} incluida en "
            + (string.IsNullOrWhiteSpace(consolidado) ? "un consolidado" : $"el consolidado {consolidado}");

        // ── Consolidados ─────────────────────────────────────────────────────

        /// <summary>Al primero que firma.</summary>
        public static string ConsolidadoPorRevisar(string? numeroReembolso) =>
            $"Consolidado del S10{NumeroS10(numeroReembolso)} por revisar";

        /// <summary>A los que firman después: el asunto dice que ya hubo una firma antes.</summary>
        public static string FaltaTuFirma(string? numeroReembolso) =>
            $"Falta tu firma - Consolidado del S10{NumeroS10(numeroReembolso)}";

        public static string ConsolidadoAprobado(string? numeroReembolso) =>
            $"Consolidado del S10{NumeroS10(numeroReembolso)} APROBADO";

        public static string ConsolidadoObservado(string? numeroReembolso) =>
            $"Consolidado del S10{NumeroS10(numeroReembolso)} OBSERVADO";

        /// <summary>Los consolidados anteriores al código se nombran por su número de reembolso.</summary>
        public static string TesoreriaReembolso(string? codigo, string? numeroReembolso) =>
            "Consolidado pendiente de revisión"
            + (!string.IsNullOrWhiteSpace(codigo) ? $" - {codigo}"
               : !string.IsNullOrWhiteSpace(numeroReembolso) ? $" - N.° {numeroReembolso}"
               : string.Empty);

        public static string TesoreriaSubsanada(string? codigo, string? numeroReembolso) =>
            "La observación fue subsanada y el consolidado volvió a Tesorería"
            + ReembolsoEmailTemplates.NombreEnAsunto(codigo, numeroReembolso);

        public static string CorreccionS10Solicitada(string? codigo, string? numeroReembolso) =>
            $"Corrección del S10 solicitada{ReembolsoEmailTemplates.NombreEnAsunto(codigo, numeroReembolso)}";

        // ── Reembolsos ───────────────────────────────────────────────────────

        public static string TesoreriaPorPagar(string? codigo, string? numeroReembolso) =>
            "El consolidado está listo para programación de pago"
            + ReembolsoEmailTemplates.NombreEnAsunto(codigo, numeroReembolso);

        public static string ReembolsoPagadoConsolidador(string? codigo, string? numeroReembolso) =>
            "El consolidado fue pagado" + ReembolsoEmailTemplates.NombreEnAsunto(codigo, numeroReembolso);

        /// <summary>Al colaborador, con una sola rendición pagada.</summary>
        public static string ReembolsoPagado(string? codigoRendicion) =>
            $"Reembolso realizado - rendición {codigoRendicion}";

        /// <summary>Al colaborador, con varias: un solo correo por persona en cada pago.</summary>
        public static string ReembolsoPagadoVarias(int cantidad) =>
            ReembolsoPagadoVarias($"{cantidad}");

        public static string ReembolsoObservadoTesoreria(string? codigo, string? numeroReembolso) =>
            $"Reembolso observado por Tesorería{ReembolsoEmailTemplates.NombreEnAsunto(codigo, numeroReembolso)}";

        // ── Correcciones S10 ─────────────────────────────────────────────────

        public static string CorreccionS10Atendida(string? codigo, string? numeroReembolso) =>
            $"Corrección del S10 atendida{ReembolsoEmailTemplates.NombreEnAsunto(codigo, numeroReembolso)}";

        // ── Para la Configuración ────────────────────────────────────────────

        private const string Codigo = "{código}";
        private const string Solicitante = "{solicitante}";
        private const string FechaDeSalida = "{fecha de salida}";
        private const string Trabajador = "{trabajador}";
        private const string Mes = "{mes}";
        private const string Cantidad = "{cantidad}";
        private const string PlanillaGrupal = "{planilla grupal}";
        private const string Consolidado = "{consolidado}";
        private const string NumeroReembolso = "{número}";

        /// <summary>
        /// Los asuntos de un correo como los muestra la Configuración: lo que cambia en cada envío va
        /// entre llaves («{código}»). Más de uno cuando el asunto cambia según el caso (el primero que
        /// firma o los siguientes, una rendición o varias). Vacío = ese correo no lo tiene acá.
        /// </summary>
        public static IReadOnlyList<string> Plantillas(string codigo) => codigo switch
        {
            CorreoEventoCodigos.Revisor => new[] { Revisor(Codigo, Solicitante, FechaDeSalida) },
            CorreoEventoCodigos.RevisorJefeArea => new[] { RevisorJefeArea(Codigo, Solicitante, FechaDeSalida) },
            CorreoEventoCodigos.Confirmacion => new[] { Confirmacion(Codigo, FechaDeSalida) },
            CorreoEventoCodigos.RecordatorioRendicionApertura => new[] { RecordatorioApertura(Mes) },
            CorreoEventoCodigos.RecordatorioRendicionCierre => new[] { RecordatorioCierre(Mes) },

            CorreoEventoCodigos.Aprobada => new[] { Aprobada(Codigo, FechaDeSalida) },
            CorreoEventoCodigos.Rechazada => new[] { Rechazada(Codigo, FechaDeSalida) },

            CorreoEventoCodigos.RendicionPrimeraRevision => new[] { RendicionPorRevisar(Codigo, Trabajador) },
            CorreoEventoCodigos.RendicionEnviada => new[] { RendicionEnviada(Codigo) },

            CorreoEventoCodigos.RendicionPrimeraAprobada => new[] { RendicionPrimeraAprobada(Codigo) },
            CorreoEventoCodigos.RendicionPrimeraObservada => new[] { RendicionPrimeraObservada(Codigo) },
            CorreoEventoCodigos.RendicionPrimeraAprobadaConsolidador =>
                new[] { DisponibleParaConsolidar(Codigo, Trabajador), DisponiblesParaConsolidar(Cantidad) },
            CorreoEventoCodigos.RendicionIncluidaPlanillaGrupal => new[] { RendicionEnPlanillaGrupal(Codigo, PlanillaGrupal) },
            CorreoEventoCodigos.RendicionIncluidaConsolidado => new[] { RendicionConsolidada(Codigo, Consolidado) },

            CorreoEventoCodigos.S10Revisor =>
                new[] { ConsolidadoPorRevisar(NumeroReembolso), FaltaTuFirma(NumeroReembolso) },
            CorreoEventoCodigos.ReembolsoAprobado => new[] { ConsolidadoAprobado(NumeroReembolso) },
            CorreoEventoCodigos.ReembolsoObservado => new[] { ConsolidadoObservado(NumeroReembolso) },
            CorreoEventoCodigos.TesoreriaReembolso => new[] { TesoreriaReembolso(Consolidado, null) },
            CorreoEventoCodigos.TesoreriaSubsanada => new[] { TesoreriaSubsanada(Consolidado, null) },
            CorreoEventoCodigos.CorreccionS10Solicitada => new[] { CorreccionS10Solicitada(Consolidado, null) },

            CorreoEventoCodigos.TesoreriaPorPagar => new[] { TesoreriaPorPagar(Consolidado, null) },
            CorreoEventoCodigos.ReembolsoPagadoConsolidador => new[] { ReembolsoPagadoConsolidador(Consolidado, null) },
            CorreoEventoCodigos.ReembolsoPagado => new[] { ReembolsoPagado(Codigo), ReembolsoPagadoVarias(Cantidad) },
            CorreoEventoCodigos.ReembolsoObservadoTesoreria => new[] { ReembolsoObservadoTesoreria(Consolidado, null) },

            CorreoEventoCodigos.CorreccionS10Atendida => new[] { CorreccionS10Atendida(Consolidado, null) },

            _ => Array.Empty<string>(),
        };

        // ── Las que reciben el texto ya armado (la fecha, la cantidad) ───────

        private static string Revisor(string? codigo, string? solicitante, string fecha) =>
            $"Solicitud de salida {codigo} - {solicitante} - {fecha}";

        private static string RevisorJefeArea(string? codigo, string? solicitante, string fecha) =>
            $"Salida registrada en tu área - {codigo} - {solicitante} - {fecha}";

        private static string Confirmacion(string? codigo, string fecha) =>
            $"Tu solicitud de salida {codigo} está en revisión - {fecha}";

        private static string Aprobada(string? codigo, string fecha) =>
            $"Solicitud de salida {codigo} APROBADA - {fecha}";

        private static string Rechazada(string? codigo, string fecha) =>
            $"Solicitud de salida {codigo} RECHAZADA - {fecha}";

        private static string DisponiblesParaConsolidar(string cantidad) =>
            $"{cantidad} rendiciones disponibles para consolidar";

        private static string ReembolsoPagadoVarias(string cantidad) =>
            $"Reembolso realizado - {cantidad} rendiciones";

        /// <summary>" N.° 00123", o nada si el consolidado no tiene número de reembolso.</summary>
        private static string NumeroS10(string? numeroReembolso) =>
            string.IsNullOrWhiteSpace(numeroReembolso) ? string.Empty : $" N.° {numeroReembolso}";

        // Como lo escribía el envío ($"{fecha:dd/MM/yyyy}"): con la cultura del servidor.
        private static string Fecha(DateOnly fecha) => fecha.ToString("dd/MM/yyyy");
    }
}
