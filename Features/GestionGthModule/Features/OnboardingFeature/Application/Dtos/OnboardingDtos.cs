namespace Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Dtos
{
    /// <summary>
    /// Todo lo que necesita la pantalla de Onboarding al entrar, en una sola petición: tarjetas de
    /// resumen, embudo de fases y tabla de colaboradores.
    ///
    /// Ya no viajan «candidatos aptos»: el que termina reclutamiento entra solo a la lista (ver
    /// <c>MaterializarPendientes</c>), así que no hay nada que elegir en un desplegable.
    /// </summary>
    public class BandejaOnboardingDto
    {
        public ResumenOnboardingDto Resumen { get; set; } = new();

        /// <summary>Fases del catálogo en orden, con cuántos colaboradores hay parados en cada una.</summary>
        public List<FaseOnboardingDto> Fases { get; set; } = new();

        public List<OnboardingListItemDto> Colaboradores { get; set; } = new();
    }

    public class ResumenOnboardingDto
    {
        /// <summary>Colaboradores con fecha de ingreso dentro del mes en curso.</summary>
        public int IngresosDelMes { get; set; }

        /// <summary>Onboardings abiertos (todo lo que no está COMPLETO).</summary>
        public int EnProceso { get; set; }

        /// <summary>Onboardings terminados (estado COMPLETO).</summary>
        public int Completos { get; set; }

        /// <summary>Onboardings iniciados en los últimos 7 días.</summary>
        public int ColaboradoresNuevos { get; set; }
    }

    public class FaseOnboardingDto
    {
        public int FaseId { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public int Orden { get; set; }

        /// <summary>Colaboradores parados en esta fase.</summary>
        public int Total { get; set; }

        /// <summary>
        /// Checklist operativo de la fase (catálogo, igual para todos los colaboradores). Viaja con
        /// la bandeja porque es lo que dibuja el modal de detalle y de donde salen sus contadores de
        /// avance: pedirlo aparte al abrir cada detalle sería una petición extra por fila.
        /// </summary>
        public List<ActividadOnboardingDto> Actividades { get; set; } = new();
    }

    /// <summary>
    /// Una actividad obligatoria del checklist (espejo de <c>gth_onboarding_actividad</c>). El
    /// avance del onboarding se mide en actividades, no en fases.
    /// </summary>
    public class ActividadOnboardingDto
    {
        public int ActividadId { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public int Orden { get; set; }

        /// <summary>true = la cumple el sistema solo, sin acción de GTH (avisos preventivos).</summary>
        public bool Automatica { get; set; }
    }

    /// <summary>Una fila de la tabla «Colaboradores ingresados».</summary>
    public class OnboardingListItemDto
    {
        public int OnboardingId { get; set; }
        public int CandidatoId { get; set; }
        public int? PersonId { get; set; }

        /// <summary>Código del requerimiento que originó la contratación (REQ-AAAA-NNNN).</summary>
        public string Codigo { get; set; } = string.Empty;

        public string Nombre { get; set; } = string.Empty;
        public string? Puesto { get; set; }
        public string? Area { get; set; }

        /// <summary>Razón social con la que se contrata (la que GTH asignó al requerimiento).</summary>
        public string? Empresa { get; set; }

        /// <summary>Proyecto / obra destino de la vacante.</summary>
        public string? ProyectoObra { get; set; }

        public DateOnly? FechaIngreso { get; set; }

        /// <summary>Quien pidió la vacante: es el jefe directo del nuevo colaborador.</summary>
        public string? JefeDirecto { get; set; }

        /// <summary>Correo personal del colaborador (el de su ficha de la base maestra).</summary>
        public string? Correo { get; set; }

        public string FaseCodigo { get; set; } = string.Empty;
        public string FaseNombre { get; set; } = string.Empty;
        public int FaseOrden { get; set; }

        public string EstadoCodigo { get; set; } = string.Empty;
        public string EstadoNombre { get; set; } = string.Empty;

        /// <summary>
        /// Avance del onboarding en % — se mide en ACTIVIDADES del checklist, no en fases
        /// (RF-ONB-27): una fase puede tener una sola actividad y otra ocho.
        /// </summary>
        public int AvancePorcentaje { get; set; }

        /// <summary>
        /// Códigos de las actividades del checklist ya cumplidas por este colaborador. Es el único
        /// origen del avance y de los checks del detalle: la pantalla no vuelve a deducir nada.
        /// </summary>
        public List<string> ActividadesHechas { get; set; } = new();

        /// <summary>Carpeta de SharePoint donde vive el file digital del colaborador.</summary>
        public string? FileDigitalCarpeta { get; set; }

        /// <summary>Observación interna que dejó GTH al abrir el onboarding.</summary>
        public string? Observacion { get; set; }

        /// <summary>Fecha en que se abrió el onboarding, ya en hora de Perú.</summary>
        public DateTime? IniciadoEn { get; set; }

        // ── Aviso al responsable de obra (fase «Correo de bienvenida») ────────

        /// <summary>
        /// false cuando este ingreso no lleva ese aviso: a Oficina Central no hay obra que avisarle,
        /// y un proyecto sin coordinador administrativo no tiene a quién escribirle.
        /// </summary>
        public bool AvisoObraAplica { get; set; }

        /// <summary>Por qué no aplica, para decirlo en la pantalla. null cuando sí aplica.</summary>
        public string? AvisoObraMotivoNoAplica { get; set; }

        /// <summary>Nombre del coordinador administrativo del proyecto (el destinatario).</summary>
        public string? AvisoObraDestinatario { get; set; }

        /// <summary>
        /// Buzón del aviso: el que quedó registrado si ya salió, o el del coordinador administrativo
        /// de hoy si todavía no.
        /// </summary>
        public string? AvisoObraEmail { get; set; }

        /// <summary>Cuándo salió el aviso, en hora de Perú. null = todavía no.</summary>
        public DateTime? AvisoObraEnviadoEn { get; set; }

        // ── Correo de bienvenida y formulario del colaborador ─────────────────

        /// <summary>Cuándo salió el correo de bienvenida, en hora de Perú. null = todavía no.</summary>
        public DateTime? BienvenidaEnviadaEn { get; set; }

        /// <summary>
        /// Buzón al que salió (o al que saldría): el correo personal de su ficha maestra. null
        /// cuando esa ficha no tiene correo, que es lo único que impide mandar la bienvenida.
        /// </summary>
        public string? BienvenidaEmail { get; set; }

        /// <summary>Hasta cuándo tiene el colaborador para completar su formulario.</summary>
        public DateOnly? FormularioFechaLimite { get; set; }

        /// <summary>Cuándo envió su formulario, en hora de Perú. null = todavía no lo mandó.</summary>
        public DateTime? FormularioCompletadoEn { get; set; }
    }

    /// <summary>
    /// Todo lo que el correo al responsable de obra necesita, resuelto en una sola consulta: los
    /// datos del ingreso y el coordinador administrativo del proyecto destino.
    /// </summary>
    public class AvisoObraContextoDto
    {
        public int OnboardingId { get; set; }

        /// <summary>Código del requerimiento que originó la contratación.</summary>
        public string Codigo { get; set; } = string.Empty;

        public string Nombre { get; set; } = string.Empty;
        public string? Puesto { get; set; }
        public string? Area { get; set; }
        public string? Empresa { get; set; }
        public string? ProyectoObra { get; set; }
        public string? JefeDirecto { get; set; }
        public DateOnly? FechaIngreso { get; set; }

        public string? CoordAdminNombre { get; set; }
        public string? CoordAdminEmail { get; set; }

        /// <summary>false = este ingreso no lleva aviso (ver <see cref="MotivoNoAplica"/>).</summary>
        public bool Aplica { get; set; }
        public string? MotivoNoAplica { get; set; }

        /// <summary>Cuándo salió (UTC), si ya salió.</summary>
        public DateTimeOffset? EnviadoEn { get; set; }
    }

    /// <summary>Resultado de avanzar de fase: la fila ya actualizada.</summary>
    public class OnboardingAccionResultDto
    {
        public string Message { get; set; } = string.Empty;

        /// <summary>La fila ya actualizada, para refrescar tabla y modal sin recargar la bandeja.</summary>
        public OnboardingListItemDto? Colaborador { get; set; }
    }

}
