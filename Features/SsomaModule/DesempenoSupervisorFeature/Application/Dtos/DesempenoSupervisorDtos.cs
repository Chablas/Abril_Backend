namespace Abril_Backend.Features.SsomaModule.DesempenoSupervisorFeature.Application.Dtos;

public record DesempenoSupervisorDto(
    int SupervisorId,
    string SupervisorNombre,
    int ProyectoId,
    string ProyectoNombre,
    int Mes,
    int Anio,
    int MetaRacs,
    int MetaOpt,
    int MetaInspecciones,
    int MetaCharlas,
    int ActualRacs,
    int ActualOpt,
    int ActualInspecciones,
    int ActualCharlas,
    decimal PctRacs,
    decimal PctOpt,
    decimal PctInspecciones,
    decimal PctCharlas,
    decimal PctGeneral
);
