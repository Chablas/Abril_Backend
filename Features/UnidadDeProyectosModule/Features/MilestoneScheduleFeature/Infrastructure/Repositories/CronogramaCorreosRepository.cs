using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Constants;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Infrastructure.Repositories
{
    /// <summary>
    /// Correos del Cronograma de Hitos. La pantalla guarda al tocar cada control, así que cada
    /// escritura toca una sola fila (nada de reemplazar la lista entera: pisaría lo que otro acabara
    /// de cambiar en otra fila), y las que cambian la lista devuelven el correo actualizado en el
    /// mismo viaje, sin volver a pedir la pantalla completa.
    ///
    /// Un rol se expande a los correos corporativos de quienes lo tienen el día que sale el correo
    /// (user_role → person → workers), igual que en la configuración de correos de Salidas.
    /// </summary>
    public class CronogramaCorreosRepository : ICronogramaCorreosRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public CronogramaCorreosRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        // ── Piezas de SQL ────────────────────────────────────────────────────

        /// <summary>El correo con el código de su sección. Termina en el WHERE: se le suma el filtro.</summary>
        private const string SqlCorreo = """
            SELECT c.milestone_schedule_correo_id AS id,
                   g.codigo                       AS grupo_codigo,
                   c.codigo,
                   c.nombre,
                   c.descripcion,
                   c.active,
                   c.principal_nombre,
                   c.principal_active
            FROM milestone_schedule_correo c
            JOIN milestone_schedule_correo_grupo g
                 ON g.milestone_schedule_correo_grupo_id = c.milestone_schedule_correo_grupo_id
            WHERE c.state
            """;

        /// <summary>
        /// Los destinatarios con lo que hace falta para mostrarlos: el nombre y el correo del
        /// trabajador (el correo, solo si su ficha sigue vigente), el nombre del rol y a cuántos
        /// correos alcanza hoy. Termina en el WHERE: se le suma el filtro y el orden.
        /// </summary>
        private const string SqlDestinatarios = """
            SELECT d.milestone_schedule_correo_destinatario_id AS id,
                   d.milestone_schedule_correo_id              AS correo_id,
                   t.codigo                                    AS tipo_codigo,
                   r.codigo                                    AS recepcion_codigo,
                   d.worker_id,
                   d.role_id,
                   d.correo,
                   d.active,
                   p.full_name                                 AS worker_nombre,
                   CASE WHEN w.state THEN nullif(trim(w.email_corporativo), '') END AS worker_email,
                   ro.role_description                         AS rol_nombre,
                   CASE WHEN d.role_id IS NOT NULL THEN
                       (SELECT count(DISTINCT lower(trim(wr.email_corporativo)))
                        FROM user_role ur
                        JOIN person pr  ON pr.user_id = ur.user_id AND pr.state
                        JOIN workers wr ON wr.person_id = pr.person_id AND wr.state
                        WHERE ur.role_id = d.role_id AND ur.state AND ur.active
                          AND coalesce(trim(wr.email_corporativo), '') <> '')::int
                   END AS rol_miembros
            FROM milestone_schedule_correo_destinatario d
            JOIN milestone_schedule_correo_destinatario_tipo t
                 ON t.milestone_schedule_correo_destinatario_tipo_id = d.milestone_schedule_correo_destinatario_tipo_id
            JOIN milestone_schedule_correo_recepcion r
                 ON r.milestone_schedule_correo_recepcion_id = d.milestone_schedule_correo_recepcion_id
            LEFT JOIN workers w ON w.id = d.worker_id
            LEFT JOIN person p  ON p.person_id = w.person_id
            LEFT JOIN role ro   ON ro.role_id = d.role_id
            WHERE d.state
            """;

        /// <summary>Primero los Para, después los CC y los CCO; dentro de cada uno, en el orden en que se agregaron.</summary>
        private const string OrdenDestinatarios =
            "\nORDER BY d.milestone_schedule_correo_id, r.orden, d.orden, d.milestone_schedule_correo_destinatario_id;\n";

        /// <summary>El correo al que pertenece el destinatario @Id (aunque ya esté dado de baja).</summary>
        private const string CorreoDelDestinatario =
            "(SELECT x.milestone_schedule_correo_id FROM milestone_schedule_correo_destinatario x "
            + "WHERE x.milestone_schedule_correo_destinatario_id = @Id)";

        private const string SqlCorreoPorCodigo = SqlCorreo + "\n  AND c.codigo = @Codigo;\n";
        private const string SqlDestinatariosPorCodigo =
            SqlDestinatarios
            + "\n  AND d.milestone_schedule_correo_id = (SELECT x.milestone_schedule_correo_id "
            + "FROM milestone_schedule_correo x WHERE x.codigo = @Codigo AND x.state)"
            + OrdenDestinatarios;

        private const string SqlCorreoPorDestinatario =
            SqlCorreo + "\n  AND c.milestone_schedule_correo_id = " + CorreoDelDestinatario + ";\n";
        private const string SqlDestinatariosPorDestinatario =
            SqlDestinatarios + "\n  AND d.milestone_schedule_correo_id = " + CorreoDelDestinatario + OrdenDestinatarios;

        /// <summary>
        /// El tipo, la recepción y el destino, validados en la misma sentencia que escribe: el
        /// trabajador y el rol tienen que seguir vigentes.
        /// </summary>
        private const string SqlValidacion = """
                   (SELECT t.milestone_schedule_correo_destinatario_tipo_id
                    FROM milestone_schedule_correo_destinatario_tipo t
                    WHERE t.codigo = @TipoCodigo AND t.state AND t.active) AS tipo_id,
                   (SELECT r.milestone_schedule_correo_recepcion_id
                    FROM milestone_schedule_correo_recepcion r
                    WHERE r.codigo = @RecepcionCodigo AND r.state AND r.active) AS recepcion_id,
                   (@WorkerId IS NULL OR EXISTS (SELECT 1 FROM workers w WHERE w.id = @WorkerId AND w.state))
                   AND (@RoleId IS NULL OR EXISTS (SELECT 1 FROM role ro WHERE ro.role_id = @RoleId AND ro.state))
                       AS destino_existe
            """;

        // ── Lectura ──────────────────────────────────────────────────────────

        /// <summary>Siete sentencias en un viaje: secciones, correos, destinatarios y las cuatro listas del modal.</summary>
        public async Task<CronogramaConfiguracionDto> GetConfiguracionAsync()
        {
            const string sql = """
                SELECT g.codigo, g.nombre
                FROM milestone_schedule_correo_grupo g
                WHERE g.state AND g.active
                ORDER BY g.orden, g.milestone_schedule_correo_grupo_id;
                """
                + "\n" + SqlCorreo + "\nORDER BY c.orden, c.milestone_schedule_correo_id;\n"
                + SqlDestinatarios + OrdenDestinatarios
                + """
                SELECT t.worker_id, t.full_name, t.email
                FROM (
                    SELECT DISTINCT ON (lower(trim(w.email_corporativo)))
                           w.id AS worker_id,
                           p.full_name,
                           trim(w.email_corporativo) AS email
                    FROM workers w
                    JOIN person p ON p.person_id = w.person_id AND p.state
                    WHERE w.state AND w.email_corporativo ILIKE '%@abril.pe%'
                    ORDER BY lower(trim(w.email_corporativo)), w.id DESC
                ) t
                ORDER BY t.full_name;

                SELECT r.role_id,
                       r.role_description AS nombre,
                       (SELECT count(DISTINCT lower(trim(wr.email_corporativo)))
                        FROM user_role ur
                        JOIN person pr  ON pr.user_id = ur.user_id AND pr.state
                        JOIN workers wr ON wr.person_id = pr.person_id AND wr.state
                        WHERE ur.role_id = r.role_id AND ur.state AND ur.active
                          AND coalesce(trim(wr.email_corporativo), '') <> '')::int AS miembros
                FROM role r
                WHERE r.state AND r.active
                ORDER BY r.role_description;

                SELECT t.codigo, t.nombre
                FROM milestone_schedule_correo_destinatario_tipo t
                WHERE t.state AND t.active
                ORDER BY t.orden;

                SELECT r.codigo, r.nombre
                FROM milestone_schedule_correo_recepcion r
                WHERE r.state AND r.active
                ORDER BY r.orden;
                """;

            using var ctx = _factory.CreateDbContext();
            var conn = ctx.Database.GetDbConnection();
            await conn.OpenAsync();

            using var multi = await conn.QueryMultipleAsync(sql);

            var grupos = (await multi.ReadAsync<GrupoFila>()).ToList();
            var correos = (await multi.ReadAsync<CorreoFila>()).ToList();
            var destinatarios = (await multi.ReadAsync<DestinatarioFila>()).ToLookup(d => d.CorreoId);

            return new CronogramaConfiguracionDto
            {
                Grupos = grupos.Select(g => new CronogramaCorreoGrupoDto
                {
                    Codigo = g.Codigo,
                    Nombre = g.Nombre,
                    Correos = correos
                        .Where(c => c.GrupoCodigo == g.Codigo)
                        .Select(c => ACorreo(c, destinatarios[c.Id]))
                        .ToList(),
                }).ToList(),
                Trabajadores = (await multi.ReadAsync<CronogramaCorreoTrabajadorOpcionDto>()).ToList(),
                Roles = (await multi.ReadAsync<CronogramaCorreoRolOpcionDto>()).ToList(),
                Tipos = (await multi.ReadAsync<CronogramaCorreoOpcionDto>()).ToList(),
                Recepciones = (await multi.ReadAsync<CronogramaCorreoOpcionDto>()).ToList(),
            };
        }

        // ── Interruptores ────────────────────────────────────────────────────

        public Task<bool> SetCorreoActiveAsync(string codigo, bool active, int userId) =>
            EjecutarAsync("""
                UPDATE milestone_schedule_correo
                SET active = @Active, updated_date_time = now(), updated_user_id = @UserId
                WHERE codigo = @Codigo AND state;
                """, new { Codigo = codigo, Active = active, UserId = userId });

        public Task<bool> SetPrincipalActiveAsync(string codigo, bool active, int userId) =>
            EjecutarAsync("""
                UPDATE milestone_schedule_correo
                SET principal_active = @Active, updated_date_time = now(), updated_user_id = @UserId
                WHERE codigo = @Codigo AND state AND principal_nombre IS NOT NULL;
                """, new { Codigo = codigo, Active = active, UserId = userId });

        public Task<bool> SetDestinatarioActiveAsync(int id, bool active, int userId) =>
            EjecutarAsync("""
                UPDATE milestone_schedule_correo_destinatario
                SET active = @Active, updated_date_time = now(), updated_user_id = @UserId
                WHERE milestone_schedule_correo_destinatario_id = @Id AND state;
                """, new { Id = id, Active = active, UserId = userId });

        // ── Alta, edición y baja de destinatarios ────────────────────────────

        public async Task<CronogramaDestinatarioGuardadoDto> CrearDestinatarioAsync(
            string codigo, string tipoCodigo, string recepcionCodigo, int? workerId, int? roleId, string? correo, int userId)
        {
            // Va al final de su correo: el orden solo separa a los que reciben igual.
            const string sql = """
                WITH v AS (
                    SELECT (SELECT c.milestone_schedule_correo_id FROM milestone_schedule_correo c
                            WHERE c.codigo = @Codigo AND c.state) AS correo_id,
                """ + SqlValidacion + """

                ),
                nuevo AS (
                    INSERT INTO milestone_schedule_correo_destinatario
                        (milestone_schedule_correo_id, milestone_schedule_correo_destinatario_tipo_id,
                         milestone_schedule_correo_recepcion_id, worker_id, role_id, correo, orden, created_user_id)
                    SELECT v.correo_id, v.tipo_id, v.recepcion_id, @WorkerId, @RoleId, @Correo,
                           coalesce((SELECT max(d.orden) FROM milestone_schedule_correo_destinatario d
                                     WHERE d.milestone_schedule_correo_id = v.correo_id AND d.state), 0) + 1,
                           @UserId
                    FROM v
                    WHERE v.correo_id IS NOT NULL AND v.tipo_id IS NOT NULL AND v.recepcion_id IS NOT NULL
                      AND v.destino_existe
                    RETURNING milestone_schedule_correo_destinatario_id
                )
                SELECT v.correo_id IS NOT NULL                              AS encontrado,
                       v.destino_existe,
                       (v.tipo_id IS NOT NULL AND v.recepcion_id IS NOT NULL) AS catalogo_ok,
                       EXISTS (SELECT 1 FROM nuevo)                         AS guardado
                FROM v;
                """ + "\n" + SqlCorreoPorCodigo + SqlDestinatariosPorCodigo;

            return await GuardarDestinatarioAsync(sql, new
            {
                Codigo = codigo,
                TipoCodigo = tipoCodigo,
                RecepcionCodigo = recepcionCodigo,
                WorkerId = workerId,
                RoleId = roleId,
                Correo = correo,
                UserId = userId,
            });
        }

        public async Task<CronogramaDestinatarioGuardadoDto> ActualizarDestinatarioAsync(
            int id, string tipoCodigo, string recepcionCodigo, int? workerId, int? roleId, string? correo, int userId)
        {
            // Sin fila en «v» (no existe o está dada de baja), la última sentencia no devuelve nada.
            const string sql = """
                WITH v AS (
                    SELECT d.milestone_schedule_correo_destinatario_id AS id,
                """ + SqlValidacion + """

                    FROM milestone_schedule_correo_destinatario d
                    WHERE d.milestone_schedule_correo_destinatario_id = @Id AND d.state
                ),
                cambio AS (
                    UPDATE milestone_schedule_correo_destinatario d
                    SET milestone_schedule_correo_destinatario_tipo_id = v.tipo_id,
                        milestone_schedule_correo_recepcion_id         = v.recepcion_id,
                        worker_id         = @WorkerId,
                        role_id           = @RoleId,
                        correo            = @Correo,
                        updated_date_time = now(),
                        updated_user_id   = @UserId
                    FROM v
                    WHERE d.milestone_schedule_correo_destinatario_id = v.id
                      AND v.tipo_id IS NOT NULL AND v.recepcion_id IS NOT NULL AND v.destino_existe
                    RETURNING d.milestone_schedule_correo_destinatario_id
                )
                SELECT true                                                 AS encontrado,
                       v.destino_existe,
                       (v.tipo_id IS NOT NULL AND v.recepcion_id IS NOT NULL) AS catalogo_ok,
                       EXISTS (SELECT 1 FROM cambio)                        AS guardado
                FROM v;
                """ + "\n" + SqlCorreoPorDestinatario + SqlDestinatariosPorDestinatario;

            return await GuardarDestinatarioAsync(sql, new
            {
                Id = id,
                TipoCodigo = tipoCodigo,
                RecepcionCodigo = recepcionCodigo,
                WorkerId = workerId,
                RoleId = roleId,
                Correo = correo,
                UserId = userId,
            });
        }

        public async Task<CronogramaCorreoDto?> EliminarDestinatarioAsync(int id, int userId)
        {
            // Baja lógica: la fila queda para saber a quién le llegaba antes.
            const string sql = """
                UPDATE milestone_schedule_correo_destinatario
                SET state = false, updated_date_time = now(), updated_user_id = @UserId
                WHERE milestone_schedule_correo_destinatario_id = @Id AND state
                RETURNING milestone_schedule_correo_destinatario_id;
                """ + "\n" + SqlCorreoPorDestinatario + SqlDestinatariosPorDestinatario;

            using var ctx = _factory.CreateDbContext();
            var conn = ctx.Database.GetDbConnection();
            await conn.OpenAsync();

            using var multi = await conn.QueryMultipleAsync(sql, new { Id = id, UserId = userId });

            var eliminados = (await multi.ReadAsync<int>()).Count();
            var correo = await multi.ReadSingleOrDefaultAsync<CorreoFila>();
            var destinatarios = (await multi.ReadAsync<DestinatarioFila>()).ToList();

            return eliminados == 0 || correo == null ? null : ACorreo(correo, destinatarios);
        }

        // ── Envío ────────────────────────────────────────────────────────────

        /// <summary>
        /// Dos sentencias: los interruptores y las direcciones de los destinatarios prendidos. Un rol
        /// se expande a los que lo tienen hoy; un trabajador cuya ficha ya no está vigente no suma.
        /// </summary>
        public async Task<CronogramaCorreoListaEnvio?> GetListaEnvioAsync(string codigo)
        {
            const string sql = """
                SELECT c.active, c.principal_active
                FROM milestone_schedule_correo c
                WHERE c.codigo = @Codigo AND c.state;

                SELECT r.codigo AS recepcion, trim(e.email) AS email
                FROM milestone_schedule_correo c
                JOIN milestone_schedule_correo_destinatario d
                     ON d.milestone_schedule_correo_id = c.milestone_schedule_correo_id AND d.state AND d.active
                JOIN milestone_schedule_correo_recepcion r
                     ON r.milestone_schedule_correo_recepcion_id = d.milestone_schedule_correo_recepcion_id
                JOIN LATERAL (
                    SELECT w.email_corporativo AS email
                    FROM workers w
                    WHERE w.id = d.worker_id AND w.state
                    UNION ALL
                    SELECT wr.email_corporativo
                    FROM user_role ur
                    JOIN person pr  ON pr.user_id = ur.user_id AND pr.state
                    JOIN workers wr ON wr.person_id = pr.person_id AND wr.state
                    WHERE ur.role_id = d.role_id AND ur.state AND ur.active
                    UNION ALL
                    SELECT d.correo
                    WHERE d.correo IS NOT NULL
                ) e ON coalesce(trim(e.email), '') <> ''
                WHERE c.codigo = @Codigo AND c.state
                ORDER BY r.orden, d.orden, d.milestone_schedule_correo_destinatario_id;
                """;

            using var ctx = _factory.CreateDbContext();
            var conn = ctx.Database.GetDbConnection();
            await conn.OpenAsync();

            using var multi = await conn.QueryMultipleAsync(sql, new { Codigo = codigo });

            var interruptores = await multi.ReadSingleOrDefaultAsync<InterruptoresFila>();
            var filas = (await multi.ReadAsync<EnvioFila>()).ToList();

            return interruptores == null
                ? null
                : CronogramaCorreoListaEnvio.Desde(
                    interruptores.Active,
                    interruptores.PrincipalActive,
                    filas.Select(f => (f.Recepcion, f.Email)));
        }

        /// <summary>
        /// Mismo criterio que <c>LessonReminderRepository.GetHolidayDatesAsync</c>, que es el que usa
        /// el cron: feriados vivos y activos; uno que se repite cada año cae en ese mes y día de
        /// <paramref name="anio"/>.
        /// </summary>
        public async Task<HashSet<DateOnly>> GetFeriadosAsync(int anio, int mes)
        {
            using var ctx = _factory.CreateDbContext();

            var feriados = await ctx.Holiday
                .Where(h => h.State && h.Active)
                .Select(h => new { h.HolidayDate, h.RecurringYearly })
                .ToListAsync();

            var resultado = new HashSet<DateOnly>();
            foreach (var h in feriados)
            {
                if (h.HolidayDate.Month != mes) continue;
                if (h.RecurringYearly)
                    resultado.Add(new DateOnly(anio, mes, Math.Min(h.HolidayDate.Day, DateTime.DaysInMonth(anio, mes))));
                else if (h.HolidayDate.Year == anio)
                    resultado.Add(h.HolidayDate);
            }
            return resultado;
        }

        // ── Ayudantes ────────────────────────────────────────────────────────

        private async Task<bool> EjecutarAsync(string sql, object parametros)
        {
            using var ctx = _factory.CreateDbContext();
            var conn = ctx.Database.GetDbConnection();
            return await conn.ExecuteAsync(sql, parametros) > 0;
        }

        /// <summary>
        /// Alta y edición: el resultado de la escritura, el correo y su lista. Un destinatario que ya
        /// está en el correo choca con un índice único parcial: sale como 409.
        /// </summary>
        private async Task<CronogramaDestinatarioGuardadoDto> GuardarDestinatarioAsync(string sql, object parametros)
        {
            using var ctx = _factory.CreateDbContext();
            var conn = ctx.Database.GetDbConnection();
            await conn.OpenAsync();

            try
            {
                using var multi = await conn.QueryMultipleAsync(sql, parametros);

                var resultado = await multi.ReadSingleOrDefaultAsync<GuardadoFila>();
                var correo = await multi.ReadSingleOrDefaultAsync<CorreoFila>();
                var destinatarios = (await multi.ReadAsync<DestinatarioFila>()).ToList();

                return new CronogramaDestinatarioGuardadoDto
                {
                    Encontrado = resultado?.Encontrado ?? false,
                    DestinoExiste = resultado?.DestinoExiste ?? false,
                    CatalogoOk = resultado?.CatalogoOk ?? false,
                    Guardado = resultado?.Guardado ?? false,
                    Correo = correo == null ? null : ACorreo(correo, destinatarios),
                };
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                throw new AbrilException("Ese destinatario ya está en la lista de este correo.", 409);
            }
        }

        private static CronogramaCorreoDto ACorreo(CorreoFila fila, IEnumerable<DestinatarioFila> destinatarios) => new()
        {
            Codigo = fila.Codigo,
            Nombre = fila.Nombre,
            Descripcion = fila.Descripcion,
            Asuntos = CronogramaHitosAsuntos.Plantillas(fila.Codigo).ToList(),
            Active = fila.Active,
            PrincipalNombre = fila.PrincipalNombre,
            PrincipalActive = fila.PrincipalActive,
            Destinatarios = destinatarios.Select(ADestinatario).ToList(),
        };

        /// <summary>Qué se muestra de cada destinatario según a qué apunta.</summary>
        private static CronogramaCorreoDestinatarioDto ADestinatario(DestinatarioFila fila)
        {
            var dto = new CronogramaCorreoDestinatarioDto
            {
                Id = fila.Id,
                TipoCodigo = fila.TipoCodigo,
                RecepcionCodigo = fila.RecepcionCodigo,
                WorkerId = fila.WorkerId,
                RoleId = fila.RoleId,
                Active = fila.Active,
            };

            switch (fila.TipoCodigo)
            {
                case CronogramaCorreoDestinatarioTipos.Trabajador:
                    dto.Nombre = fila.WorkerNombre ?? "[Trabajador no encontrado]";
                    dto.Email = fila.WorkerEmail;
                    dto.SinCorreo = string.IsNullOrWhiteSpace(fila.WorkerEmail);
                    break;

                case CronogramaCorreoDestinatarioTipos.Rol:
                    dto.Nombre = fila.RolNombre ?? "[Rol no encontrado]";
                    dto.Miembros = fila.RolMiembros ?? 0;
                    dto.SinCorreo = dto.Miembros == 0;
                    break;

                default:
                    dto.Nombre = fila.Correo ?? string.Empty;
                    dto.Email = fila.Correo;
                    dto.SinCorreo = string.IsNullOrWhiteSpace(fila.Correo);
                    break;
            }

            return dto;
        }

        private sealed class GrupoFila
        {
            public string Codigo { get; set; } = string.Empty;
            public string Nombre { get; set; } = string.Empty;
        }

        private sealed class CorreoFila
        {
            public int Id { get; set; }
            public string GrupoCodigo { get; set; } = string.Empty;
            public string Codigo { get; set; } = string.Empty;
            public string Nombre { get; set; } = string.Empty;
            public string? Descripcion { get; set; }
            public bool Active { get; set; }
            public string? PrincipalNombre { get; set; }
            public bool PrincipalActive { get; set; }
        }

        private sealed class DestinatarioFila
        {
            public int Id { get; set; }
            public int CorreoId { get; set; }
            public string TipoCodigo { get; set; } = string.Empty;
            public string RecepcionCodigo { get; set; } = string.Empty;
            public int? WorkerId { get; set; }
            public int? RoleId { get; set; }
            public string? Correo { get; set; }
            public bool Active { get; set; }
            public string? WorkerNombre { get; set; }
            public string? WorkerEmail { get; set; }
            public string? RolNombre { get; set; }
            public int? RolMiembros { get; set; }
        }

        private sealed class GuardadoFila
        {
            public bool Encontrado { get; set; }
            public bool DestinoExiste { get; set; }
            public bool CatalogoOk { get; set; }
            public bool Guardado { get; set; }
        }

        private sealed class InterruptoresFila
        {
            public bool Active { get; set; }
            public bool PrincipalActive { get; set; }
        }

        private sealed class EnvioFila
        {
            public string Recepcion { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
        }
    }
}
