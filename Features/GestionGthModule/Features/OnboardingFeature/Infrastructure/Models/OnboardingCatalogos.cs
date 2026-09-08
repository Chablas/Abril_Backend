namespace Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Infrastructure.Models
{
    /// <summary>
    /// Estado del formulario de bienvenida (tabla <c>gth_onboarding_formulario_estado</c>):
    /// ENVIADO mientras el colaborador no lo manda, COMPLETADO cuando lo manda. No hay aprobación
    /// ni rechazo como en el del postulante: acá no se está evaluando a nadie, se está recogiendo
    /// la información de alguien que ya entró.
    /// </summary>
    public class GthOnboardingFormularioEstado
    {
        public int GthOnboardingFormularioEstadoId { get; set; }
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public int Orden { get; set; }
        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;
    }

    /// <summary>
    /// Dónde trabaja el colaborador (tabla <c>gth_onboarding_ubicacion</c>): Oficina Principal,
    /// Proyectos Abril o Salas de Ventas. Es una respuesta del formulario, no el proyecto destino
    /// de la vacante: el colaborador confirma lo que le dijo el correo de bienvenida.
    /// </summary>
    public class GthOnboardingUbicacion
    {
        public int GthOnboardingUbicacionId { get; set; }
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public int Orden { get; set; }
        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;
    }

    /// <summary>
    /// Situación del certificado de renta de 5ta categoría (tabla <c>gth_renta_quinta</c>): ya lo
    /// entregó, no le aplica, o está esperando que su antiguo empleador se lo dé.
    /// </summary>
    public class GthRentaQuinta
    {
        public int GthRentaQuintaId { get; set; }
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public int Orden { get; set; }
        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;
    }

    /// <summary>
    /// Talla de calzado (tabla <c>talla_calzado</c>, 35 a 44). Aparte de <see cref="Talla"/>, que
    /// es la de la camisa: son dos escalas distintas y juntarlas dejaría un desplegable de botas
    /// ofreciendo "L".
    /// </summary>
    public class TallaCalzado
    {
        public int TallaCalzadoId { get; set; }
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public int Orden { get; set; }
        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;
    }

    /// <summary>
    /// Talla de blusa/camisa (tabla <c>talla</c>, XS…XXL). Existía desde la data maestra —
    /// <c>person.talla_id</c> apunta acá— pero no estaba mapeada en el contexto porque nadie la
    /// leía todavía.
    /// </summary>
    public class Talla
    {
        public int TallaId { get; set; }
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;
        public int Orden { get; set; }
        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;
    }
}
