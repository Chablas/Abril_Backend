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
        /// Candidatos aptos para iniciar onboarding: seleccionados de requerimientos ya CERRADOS que
        /// todavía no tienen un onboarding abierto. Es el desplegable del modal «Nuevo ingreso».
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

        /// <summary>Correo personal al que se le envió (o se le enviará) la carta oferta.</summary>
        public string? Correo { get; set; }

        public string FaseCodigo { get; set; } = string.Empty;
        public string FaseNombre { get; set; } = string.Empty;
        public int FaseOrden { get; set; }

        public string EstadoCodigo { get; set; } = string.Empty;
        public string EstadoNombre { get; set; } = string.Empty;

        /// <summary>Avance del onboarding en % (fase actual sobre el total de fases del catálogo).</summary>
        public int AvancePorcentaje { get; set; }

        // ── Carta oferta ──────────────────────────────────────────────────────
        public string? CartaOfertaNombre { get; set; }
        public string? CartaOfertaUrl { get; set; }

        /// <summary>Fecha de envío de la carta oferta, ya en hora de Perú.</summary>
        public DateTime? CartaOfertaEnviadaEn { get; set; }

        /// <summary>Fecha en que se abrió el onboarding, ya en hora de Perú.</summary>
        public DateTime? IniciadoEn { get; set; }
    }

    /// <summary>
    /// Candidato que terminó reclutamiento y puede pasar a onboarding: una opción del desplegable
    /// del modal «Nuevo ingreso».
    /// </summary>
    public class CandidatoAptoDto
    {
        public int CandidatoId { get; set; }
        public int RequerimientoId { get; set; }
        public int? PersonId { get; set; }

        /// <summary>Nombre + código del requerimiento: es lo que se lee en el desplegable.</summary>
        public string Nombre { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;

        public string? Puesto { get; set; }
        public string? Area { get; set; }
        public string? Empresa { get; set; }
        public string? ProyectoObra { get; set; }

        /// <summary>
        /// Correo personal al que iría la carta oferta. Sale de <c>person.email</c> (lo que GTH
        /// validó al aprobar el formulario) y, si esa ficha no existe todavía, del correo que el
        /// postulante declaró en su formulario. Null = no hay a dónde enviar y el modal lo bloquea.
        /// </summary>
        public string? Correo { get; set; }

        /// <summary>Fecha requerida de ingreso del requerimiento: es la que se propone en el modal.</summary>
        public DateOnly? FechaRequeridaIngreso { get; set; }

        /// <summary>Jefe directo (el solicitante de la vacante), para mostrarlo en el resumen del modal.</summary>
        public string? JefeDirecto { get; set; }
    }

    /// <summary>Datos del modal «Nuevo ingreso» (el JSON del multipart; la carta va como archivo).</summary>
    public class OnboardingCreateDto
    {
        public int CandidatoId { get; set; }

        /// <summary>Fecha de ingreso pactada. Si no viene, se usa la fecha requerida del requerimiento.</summary>
        public DateOnly? FechaIngreso { get; set; }

        /// <summary>
        /// Correo al que enviar la carta oferta. Normalmente no viaja: el backend lo resuelve de la
        /// base de datos. Solo se usa si GTH lo corrigió a mano en el modal.
        /// </summary>
        public string? Correo { get; set; }

        public string? Observacion { get; set; }
    }

    public class OnboardingCreateResultDto
    {
        public int OnboardingId { get; set; }
        public string Message { get; set; } = string.Empty;

        /// <summary>La fila ya lista para insertarse en la tabla sin recargar la bandeja completa.</summary>
        public OnboardingListItemDto? Colaborador { get; set; }
    }

    /// <summary>
    /// Contexto que devuelve el repositorio al validar el inicio de un onboarding: todo lo que el
    /// servicio necesita para subir la carta a SharePoint y armar el correo, resuelto en un solo
    /// roundtrip y ANTES de escribir nada.
    /// </summary>
    public class OnboardingContextoDto
    {
        public int CandidatoId { get; set; }
        public int RequerimientoId { get; set; }
        public int? PersonId { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Puesto { get; set; }
        public string? Area { get; set; }
        public string? Empresa { get; set; }
        public string? ProyectoObra { get; set; }
        public string Correo { get; set; } = string.Empty;
        public DateOnly? FechaIngreso { get; set; }
        public string? JefeDirecto { get; set; }
    }

    /// <summary>Carta oferta ya subida a SharePoint, para persistirla junto con el onboarding.</summary>
    public class CartaOfertaPersistDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string? Url { get; set; }
        public string? ItemId { get; set; }
        public string? DriveId { get; set; }
    }
}
