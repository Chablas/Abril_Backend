using System.ComponentModel.DataAnnotations.Schema;
using Abril_Backend.Features.CostsModule.Shared.Models;
using Abril_Backend.Shared.Models;

namespace Abril_Backend.Infrastructure.Models
{
    /// <summary>
    /// Un TRAMO dentro de un periodo laboral (el "subperiodo"): lapso en el que el trabajador
    /// tuvo la misma razón social, el mismo proyecto, el mismo puesto y la misma clasificación.
    /// Cualquier cambio de esos datos cierra el tramo vigente (<see cref="FechaFin"/>) y abre
    /// otro; nunca se pisa el tramo, que es lo que permite saber qué tenía el trabajador en
    /// cada momento. El vigente es el que tiene <see cref="FechaFin"/> en null.
    ///
    /// <para>Es el subperiodo del plan de <c>PLAN-RAZON-SOCIAL.md</c>: se decidió no crear
    /// una tabla nueva y completar esta, que ya lo era en la práctica
    /// (<c>Migrations_Manual/2026-09-17_worker_vinculaciones_subperiodo.sql</c>). Al abrir un
    /// tramo hay que llenar <see cref="PeriodoLaboral"/>, <see cref="PuestoId"/> y
    /// <see cref="ObraOficinaStaffId"/>; los cambios de puesto hechos desde una edición de la
    /// ficha pasan por <c>WorkerVinculacionHelper.RegistrarCambioDeFichaAsync</c>.</para>
    /// </summary>
    [Table("worker_vinculaciones")]
    public class WorkerVinculacion
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("worker_id")]
        public int WorkerId { get; set; }

        /// <summary>
        /// Periodo laboral al que pertenece el tramo. La FK de la base es compuesta
        /// (periodo + worker), así que solo puede ser un periodo del mismo trabajador.
        /// Null en las filas viejas sin periodo al que pertenecer (fichas sin ningún
        /// periodo, o tramos que empiezan después del último retiro).
        ///
        /// Al abrir un tramo conviene asignar la navegación <see cref="PeriodoLaboral"/> y no
        /// este id: en el alta y el reingreso el periodo se crea en el mismo SaveChanges y
        /// todavía no tiene id.
        /// </summary>
        [Column("workers_periodo_laboral_id")]
        public int? WorkersPeriodoLaboralId { get; set; }

        [ForeignKey(nameof(WorkersPeriodoLaboralId))]
        public WorkersPeriodoLaboral? PeriodoLaboral { get; set; }

        /// <summary>
        /// Puesto durante ESTE tramo. De él salen la categoría y el área del tramo
        /// (<c>puesto.area_destino_scope_id</c>), que no se congelan: cambiarle el área a un
        /// puesto desde Configuración cambia también el área histórica de sus tramos.
        /// En el tramo vigente coincide con <c>workers.puesto_id</c>.
        /// </summary>
        [Column("puesto_id")]
        public int? PuestoId { get; set; }

        [ForeignKey(nameof(PuestoId))]
        public Shared.Models.Puesto? PuestoCatalogo { get; set; }

        [Column("empresa_id")]
        public int? EmpresaId { get; set; }

        [Column("fecha_inicio")]
        public DateOnly FechaInicio { get; set; }

        [Column("fecha_fin")]
        public DateOnly? FechaFin { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("proyecto_id")]
        public int? ProyectoId { get; set; }

        /// <summary>Nombre del puesto congelado al abrir el tramo. Lo reemplaza
        /// <see cref="PuestoId"/>; se sigue escribiendo mientras lo lea Convalidaciones.</summary>
        [Column("puesto")]
        public string? Puesto { get; set; }

        /// <summary>Clasificación de riesgo vigente durante ESTA vinculación (catálogo
        /// workers_obra_oficina_staff). Se guarda por vinculación, no solo en Worker, para
        /// poder saber qué clasificación tenía el trabajador en cada obra/empresa pasada —
        /// necesario para evaluar convalidaciones de EMO con precisión histórica.</summary>
        [Column("obra_oficina_staff_id")]
        public int? ObraOficinaStaffId { get; set; }

        /// <summary>Categoría vigente (campo de LÓGICA, catálogo <c>categoria</c>) durante ESTA
        /// vinculación — congelada al momento del cambio, igual que <see cref="Puesto"/> y
        /// <see cref="ObraOficinaStaffId"/>, para poder reconstruir el historial con precisión
        /// (p.ej. convalidaciones de EMO necesitan saber la categoría de origen vs destino).</summary>
        [Column("categoria_id")]
        public int? CategoriaId { get; set; }

        [ForeignKey(nameof(CategoriaId))]
        public Shared.Models.Categoria? Categoria { get; set; }

        [Column("tipo_vinculacion")]
        public string? TipoVinculacion { get; set; }

        [Column("motivo_retiro")]
        public string? MotivoRetiro { get; set; }

        [Column("registrado_por_id")]
        public int? RegistradoPorId { get; set; }

        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [ForeignKey(nameof(WorkerId))]
        public Worker? Worker { get; set; }

        [ForeignKey(nameof(EmpresaId))]
        public Contributor? Empresa { get; set; }

        [ForeignKey(nameof(ProyectoId))]
        public Project? Proyecto { get; set; }
    }
}
