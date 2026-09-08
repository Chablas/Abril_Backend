namespace Abril_Backend.Features.ConfigurationModule.Features.BancoFeature.Infrastructure.Models
{
    /// <summary>
    /// Catálogo de bancos (tabla <c>banco</c>). Cada razón social del grupo trabaja con uno
    /// (<c>contributor.banco_id</c>), y de ahí sale el banco que el formulario de bienvenida le
    /// muestra al nuevo colaborador cuando le pregunta si quiere su cuenta sueldo.
    ///
    /// <c>codigo</c> es la clave estable (BCP, BBVA, SCOTIABANK, BANBIF); <c>nombre</c> es lo que
    /// se ve en pantalla y en el correo, y por eso se puede corregir sin romper nada.
    /// </summary>
    public class Banco
    {
        public int BancoId { get; set; }
        public string Codigo { get; set; } = null!;
        public string Nombre { get; set; } = null!;

        /// <summary>Posición en los desplegables. A igual orden, manda el nombre.</summary>
        public int Orden { get; set; }

        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }

        /// <summary>false = no aparece en los desplegables, pero las razones sociales que ya lo tienen lo conservan.</summary>
        public bool Active { get; set; } = true;

        /// <summary>Soft delete: false = eliminado (se conserva para auditoría).</summary>
        public bool State { get; set; } = true;
    }
}
