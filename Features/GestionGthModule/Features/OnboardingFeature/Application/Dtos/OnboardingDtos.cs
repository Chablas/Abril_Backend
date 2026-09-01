namespace Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Dtos
{
    /// <summary>
    /// Todo lo que necesita la pantalla de Onboarding al entrar, en una sola petición: tarjetas de
    /// resumen, embudo de fases, tabla de colaboradores ingresados y los candidatos que ya pueden
    /// entrar al proceso (el desplegable del modal «Nuevo ingreso»).
    /// </summary>
    public class BandejaOnboardingDto
    {
        public ResumenOnboardingDto Resumen { get; set; } = new();

        /// <summary>Fases del catálogo en orden, con cuántos colaboradores hay parados en cada una.</summary>
        public List<FaseOnboardingDto> Fases { get; set; } = new();

        public List<OnboardingListItemDto> Colaboradores { get; set; } = new();

        /// <summary>
        /// Candidatos aptos para iniciar onboarding: seleccionados de requerimientos ya CERRADOS —o
        /// sea, con su carta oferta firmada y aprobada— que todavía no tienen un onboarding abierto.
        /// Es el desplegable del modal «Nuevo ingreso».
        /// </summary>
        public List<CandidatoAptoDto> CandidatosAptos { get; set; } = new();
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

        /// <summary>Cuántos candidatos hay esperando que GTH les abra el onboarding.</summary>
        public int CandidatosPorIngresar { get; set; }
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
    }

    /// <summary>Resultado de avanzar de fase: la fila ya actualizada.</summary>
    public class OnboardingAccionResultDto
    {
        public string Message { get; set; } = string.Empty;

        /// <summary>La fila ya actualizada, para refrescar tabla y modal sin recargar la bandeja.</summary>
        public OnboardingListItemDto? Colaborador { get; set; }
    }

    /// <summary>
    /// Candidato que terminó reclutamiento y puede pasar a onboarding: una opción del desplegable
    /// del modal «Nuevo ingreso».
    /// </summary>
    public class CandidatoAptoDto
    {
        public int CandidatoId { get; set; }
        public int RequerimientoId { get; set; }

        /// <summary>
        /// Ficha del candidato en la base maestra. Sale de su carta oferta, que no se pudo enviar sin
        /// ella, así que en la práctica siempre viene llena.
        /// </summary>
        public int? PersonId { get; set; }

        /// <summary>Nombre del colaborador (el de su ficha de la base maestra).</summary>
        public string Nombre { get; set; } = string.Empty;

        /// <summary>Código del requerimiento: es lo que distingue dos procesos de la misma persona.</summary>
        public string Codigo { get; set; } = string.Empty;

        public string? Puesto { get; set; }
        public string? Area { get; set; }
        public string? Empresa { get; set; }
        public string? ProyectoObra { get; set; }

        /// <summary>Correo personal del colaborador (de su ficha de la base maestra).</summary>
        public string? Correo { get; set; }

        /// <summary>Jefe directo (el solicitante de la vacante), para mostrarlo en el resumen del modal.</summary>
        public string? JefeDirecto { get; set; }

        /// <summary>
        /// Fecha de ingreso pactada en su carta oferta. Es lo que prellena el modal: GTH la puede
        /// ajustar si el ingreso se movió entre la firma y la apertura del onboarding.
        /// </summary>
        public DateOnly? FechaIngreso { get; set; }

        /// <summary>
        /// Carpeta del file digital que abrió su carta oferta, para mostrarla en el modal. El
        /// onboarding la hereda tal cual: es el mismo expediente.
        /// </summary>
        public string? FileDigitalCarpeta { get; set; }
    }

    /// <summary>Datos del modal «Nuevo ingreso».</summary>
    public class OnboardingCreateDto
    {
        public int CandidatoId { get; set; }

        /// <summary>
        /// Fecha de ingreso. Si no viene, se usa la que quedó pactada en la carta oferta.
        /// </summary>
        public DateOnly? FechaIngreso { get; set; }

        public string? Observacion { get; set; }
    }

    public class OnboardingCreateResultDto
    {
        public int OnboardingId { get; set; }
        public string Message { get; set; } = string.Empty;

        /// <summary>La fila ya lista para insertarse en la tabla sin recargar la bandeja completa.</summary>
        public OnboardingListItemDto? Colaborador { get; set; }
    }
}
