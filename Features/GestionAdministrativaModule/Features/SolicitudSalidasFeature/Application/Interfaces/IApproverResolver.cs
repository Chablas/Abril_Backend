using Abril_Backend.Infrastructure.Models;

namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Interfaces
{
    /// <summary>
    /// Resuelve la jefatura inmediata que debe aprobar/rechazar la salida
    /// de un trabajador, siguiendo las reglas del PowerApps:
    ///   1. Jefe / Sub Gerente            → Gerente del mismo Area
    ///   2. Subarea Legal:
    ///        - Coordinador                → Gerente del Area
    ///        - resto                      → Coordinador de Legal (si no es self) → Gerente del Area
    ///   3. Resto de áreas:
    ///        - Jefe de la misma Subarea (si no es self)
    ///        - Sub Gerente de la misma Subarea (si no es self)
    ///        - Gerente de la misma Area
    /// </summary>
    public interface IApproverResolver
    {
        /// <summary>Devuelve el email del aprobador, o null si no se pudo resolver.</summary>
        Task<string?> ResolveApproverEmailAsync(Worker user);
    }
}
