namespace Abril_Backend.Features.GestionAdministrativa.CorreosSalida.Application.Dtos
{
    /// <summary>
    /// Carga inicial de la pantalla "Correos" (1 sola petición): los correos configurables con
    /// sus reglas de inclusión/exclusión + las opciones para los desplegables (trabajadores,
    /// áreas) y el catálogo de tipos de destinatario.
    /// </summary>
    public class CorreoConfigInicialDto
    {
        public List<CorreoEventoDto> Eventos { get; set; } = new();
        public List<CorreoTipoDto> Tipos { get; set; } = new();
        public List<CorreoWorkerOptionDto> Trabajadores { get; set; } = new();
        public List<CorreoAreaOptionDto> Areas { get; set; } = new();
    }

    /// <summary>Un correo configurable (ga_correo_evento) con sus dos listas de reglas.</summary>
    public class CorreoEventoDto
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public int Orden { get; set; }
        /// <summary>"Se enviará a" (reglas con es_exclusion = false).</summary>
        public List<CorreoReglaDto> Incluir { get; set; } = new();
        /// <summary>"Nunca se enviará a" (reglas con es_exclusion = true).</summary>
        public List<CorreoReglaDto> Excluir { get; set; } = new();
    }

    /// <summary>Una regla ya guardada (fila viva de ga_correo_regla).</summary>
    public class CorreoReglaDto
    {
        public int Id { get; set; }
        /// <summary>TRABAJADOR / AREA / CORREO.</summary>
        public string TipoCodigo { get; set; } = string.Empty;
        public int? WorkerId { get; set; }
        public int? AreaScopeId { get; set; }
        public string? Correo { get; set; }
        public bool IncluirDescendientes { get; set; } = true;
        public bool Active { get; set; } = true;
    }

    public class CorreoTipoDto
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
    }

    public class CorreoWorkerOptionDto
    {
        public int WorkerId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
    }

    public class CorreoAreaOptionDto
    {
        public int AreaScopeId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        /// <summary>Correo de grupo del área (area_scope.email), informativo. Puede ser null.</summary>
        public string? Email { get; set; }
    }

    // ── Update (PUT /{eventoCodigo}) ─────────────────────────────────────────

    /// <summary>Reemplaza el conjunto completo de reglas de un correo.</summary>
    public class CorreoReglasUpdateDto
    {
        public List<CorreoReglaInputDto> Incluir { get; set; } = new();
        public List<CorreoReglaInputDto> Excluir { get; set; } = new();
    }

    public class CorreoReglaInputDto
    {
        /// <summary>TRABAJADOR / AREA / CORREO.</summary>
        public string TipoCodigo { get; set; } = string.Empty;
        public int? WorkerId { get; set; }
        public int? AreaScopeId { get; set; }
        public string? Correo { get; set; }
        public bool IncluirDescendientes { get; set; } = true;
        public bool Active { get; set; } = true;
    }
}
