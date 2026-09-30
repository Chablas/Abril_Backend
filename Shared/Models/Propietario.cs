using Abril_Backend.Infrastructure.Models;

namespace Abril_Backend.Shared.Models
{
    /// <summary>
    /// Un inmueble de un propietario de Abril: la persona, el proyecto, la torre y el departamento.
    /// Una persona con dos departamentos tiene dos filas, y un departamento con dos dueños también.
    /// La cuenta con la que entra a la app Convivir Abril es la de <c>person.user_id</c>, con el rol
    /// PROPIETARIO (<see cref="Constants.Roles.Propietario"/>).
    ///
    /// La escribe el módulo Propietarios de la intranet y la lee la app (ConvivirModule). El avance
    /// que ve el propietario sale del cronograma de hitos del proyecto (owner_milestone), que es por
    /// proyecto y no por torre.
    /// </summary>
    public class Propietario
    {
        public int PropietarioId { get; set; }

        public int PersonId { get; set; }
        public Person? Person { get; set; }

        public int ProjectId { get; set; }
        public Project? Project { get; set; }

        /// <summary>
        /// Texto, igual que <c>torre_nombre</c> en RAC/ATS/Inspección: el catálogo
        /// <c>project_torre</c> se reemplaza entero al editarlo, así que no admite una FK. Null en
        /// un edificio de una sola torre. Se guarda sin la palabra «Torre» («A», «1»).
        /// </summary>
        public string? Torre { get; set; }

        /// <summary>Número del departamento, sin «Dpto.» («803», «1201-B»).</summary>
        public string Departamento { get; set; } = string.Empty;

        public DateTime CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;
    }
}
