namespace Abril_Backend.Features.GestionAdministrativa.Shared.Models
{
    /// <summary>
    /// La PLANILLA GRUPAL («PLANILLA DE REEMBOLSO» en el papel): los trayectos de varias planillas de
    /// rendición en una sola tabla, con la columna RENDICIÓN, la cabecera del consolidador y el
    /// código de la rendición grupal impreso.
    ///
    /// Desde el 2026-09-24 la PREPARA el consolidador en Gestión de Rendiciones antes de subir el
    /// Consolidado del S10 —es el papel con el que registra las planillas en el S10—, así que existe
    /// antes que el consolidado y por eso vive en su propia tabla. Hasta entonces la armaba Abril One
    /// al subirse el S10 y solo existía dentro de <see cref="GaConsolidadoS10"/>.
    ///
    /// El código <c>CONS-ÁREA-AAAA-NNN</c> nace acá. Al subir el S10 el consolidado lo hereda y
    /// copia el archivo en <c>ga_consolidado_s10.planilla_grupal_*</c>, que es de donde lo leen
    /// Consolidados, Correcciones S10 y Reembolsos y al lado de donde queda su copia firmada: esta
    /// fila es siempre la original, sin firmas.
    ///
    /// Qué planillas cubre vive en <see cref="GaPlanillaGrupalRendicion"/>. Una vez preparada NO se
    /// rehace ni se reemplaza —es lo que se registró en el S10—, y sus rendiciones no pueden entrar
    /// en otra.
    /// </summary>
    public class GaPlanillaGrupal
    {
        public int Id { get; set; }

        /// <summary>
        /// <c>CONS-&lt;ÁREA&gt;-AAAA-NNN</c>, el mismo correlativo por área y año que el consolidado
        /// (ver <c>CodigoRendicionGrupal</c>). Único entre las vigentes.
        /// </summary>
        public string Codigo { get; set; } = string.Empty;

        /// <summary>Año del correlativo (hora de Perú).</summary>
        public int Anio { get; set; }

        /// <summary>Correlativo dentro del área y el año.</summary>
        public int Numero { get; set; }

        /// <summary>
        /// Área de la rendición grupal: la del consolidador que la prepara (su ficha vigente →
        /// puesto → área de destino). Da la sigla del código y el «ÁREA» impreso. Null si no se pudo
        /// resolver.
        /// </summary>
        public int? AreaScopeId { get; set; }

        /// <summary>webUrl del PDF en SharePoint (la misma carpeta que las planillas de rendición).</summary>
        public string PdfUrl { get; set; } = string.Empty;
        public string? PdfItemId { get; set; }
        public string? PdfDriveId { get; set; }
        public string PdfFilename { get; set; } = string.Empty;

        /// <summary>FK a <c>app_user.user_id</c> del consolidador que la preparó.</summary>
        public int PreparadaPorId { get; set; }
        public DateTimeOffset PreparadaAt { get; set; }

        /// <summary>Soft delete: false = dada de baja (se conserva para auditoría).</summary>
        public bool State { get; set; } = true;
    }
}
