namespace Abril_Backend.Features.SsomaModule.InspeccionCruzadaProgramacionFeature.Application.Dtos
{
    public class ProyectoSimpleInspeccionCruzadaDto
    {
        public int ProyectoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }

    public class AnilloDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public List<MiembroAnilloDto> Miembros { get; set; } = new();
    }

    /// <summary>Un proyecto dentro de un anillo, con su posición en la cola circular.</summary>
    public class MiembroAnilloDto
    {
        public int Id { get; set; }
        public int ProyectoId { get; set; }
        public string ProyectoNombre { get; set; } = string.Empty;
        public int Orden { get; set; }
        public bool Activo { get; set; }
    }

    public class CrearAnilloDto
    {
        public string Nombre { get; set; } = string.Empty;
    }

    public class AgregarMiembroDto
    {
        public int ProyectoId { get; set; }
    }

    public class ReordenarItemDto
    {
        public int Id { get; set; }
        public int Orden { get; set; }
    }

    public class ReordenarDto
    {
        public List<ReordenarItemDto> Items { get; set; } = new();
    }

    public class ActivoDto
    {
        public bool Activo { get; set; }
    }

    /// <summary>Una pareja ya generada (o reasignada a mano) del mes.</summary>
    public class ProgramacionInspeccionCruzadaDto
    {
        public int Id { get; set; }
        public int Anio { get; set; }
        public int Mes { get; set; }
        public int AnilloId { get; set; }
        public string AnilloNombre { get; set; } = string.Empty;
        public int ProyectoInspectorId { get; set; }
        public string ProyectoInspectorNombre { get; set; } = string.Empty;
        public int ProyectoInspeccionadoId { get; set; }
        public string ProyectoInspeccionadoNombre { get; set; } = string.Empty;
        public bool EsManual { get; set; }
        public string? MotivoCambio { get; set; }
    }

    public class ReasignarProgramacionDto
    {
        public int ProyectoInspectorId { get; set; }
        public int ProyectoInspeccionadoId { get; set; }
        public string? Motivo { get; set; }
    }
}
