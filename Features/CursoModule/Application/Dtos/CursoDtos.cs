namespace Abril_Backend.Features.CursoModule.Application.Dtos
{
    public class CursoDto
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public string? CategoriaNombre { get; set; }
        public string? RolDestino { get; set; }
        public decimal NotaMinimaAprobacion { get; set; }
        public bool Activo { get; set; }
        public string? ColorTema { get; set; }
    }

    public class CursoUpsertDto
    {
        public string Titulo { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public string? CategoriaNombre { get; set; }
        public string? RolDestino { get; set; }
        public decimal NotaMinimaAprobacion { get; set; }
        public bool Activo { get; set; } = true;
        public string? ColorTema { get; set; }
    }

    public class CursoSlideUpsertDto
    {
        public int Orden { get; set; }
        public string TipoCodigo { get; set; } = string.Empty;
        public bool EsEvaluable { get; set; }
        public decimal? Puntaje { get; set; }
        public string? ModoCorreccion { get; set; }
        public string ConfiguracionJson { get; set; } = "{}";
    }

    /// <summary>
    /// Slide de curso tal como se entrega al frontend: NUNCA incluye la clave "respuestaCorrecta"
    /// (ni ninguna otra pista de solución) dentro de ConfiguracionJson — se limpia en el controller
    /// antes de mapear a este DTO.
    /// </summary>
    public class CursoSlideDto
    {
        public int Id { get; set; }
        public int CursoId { get; set; }
        public int Orden { get; set; }
        public string TipoCodigo { get; set; } = string.Empty;
        public bool EsEvaluable { get; set; }
        public decimal? Puntaje { get; set; }
        public string? ModoCorreccion { get; set; }
        public string ConfiguracionJson { get; set; } = "{}";
    }
}
