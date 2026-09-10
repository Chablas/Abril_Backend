namespace Abril_Backend.Shared.Services;

/// <summary>Correos de los responsables de un proyecto, para notificaciones cross-módulo.</summary>
public class ProyectoResponsablesDto
{
    public string? ResidenteEmail { get; set; }
    public string? CoordSsomaEmail { get; set; }
    /// <summary>
    /// No es por proyecto: se resuelve como el único Worker activo con puesto
    /// "GERENTE INMOBILIARIO" (rol único a nivel de toda la compañía).
    /// </summary>
    public string? GerenteInmobiliarioEmail { get; set; }
}

/// <summary>
/// Resuelve los responsables de un proyecto (Residente, Coordinador SSOMA, Gerente
/// Inmobiliario) a partir de <c>Project</c> y el catálogo de puestos — mismo criterio que ya
/// usaba (duplicado) InspeccionRepository.ResolverDestinatariosCierreAsync. Se centraliza acá
/// para que Penalidades y cualquier otro módulo que necesite notificar a estos roles no
/// reimplemente el lookup.
/// </summary>
public interface IProyectoResponsablesResolver
{
    Task<ProyectoResponsablesDto> ResolverAsync(int proyectoId);
}
