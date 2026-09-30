using Dapper;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Application.Dtos;
using Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Models;
using UserModel = Abril_Backend.Infrastructure.Models.User;

namespace Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Infrastructure.Repositories
{
    /// <summary>
    /// Un propietario es una persona con al menos una fila vigente en <c>propietario</c>, o con un
    /// usuario vigente que tiene el rol PROPIETARIO (así aparece también el que se creó desde
    /// Seguridad → Usuarios y todavía no tiene propiedades).
    /// </summary>
    public class GestionPropietariosRepository : IGestionPropietariosRepository
    {
        private static readonly int RolPropietarioId = int.Parse(Roles.Propietario);

        /// <summary>DNI en person.document_identity_type_id (mismo valor que Seguridad → Usuarios).</summary>
        private const int TipoDocumentoDni = 1;

        private readonly IDbContextFactory<AppDbContext> _factory;

        public GestionPropietariosRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<PropietarioProyectoDto>> GetProyectos()
        {
            using var ctx = _factory.CreateDbContext();

            return await ctx.Project
                .Where(p => p.State && p.Active && p.ProjectTipoId == ProjectTipoIds.Proyecto)
                .OrderBy(p => p.ProjectDescription)
                .Select(p => new PropietarioProyectoDto
                {
                    ProjectId = p.ProjectId,
                    ProjectDescription = p.ProjectDescription,
                })
                .ToListAsync();
        }

        /// <summary>
        /// Una página de propietarios con sus propiedades, en un solo viaje a la base: el conteo, la
        /// página y las propiedades de esa página son tres sentencias que repiten el mismo CTE.
        /// Los más recientes primero (la última propiedad o el rol que se les dio). La búsqueda es
        /// por palabras en cualquier orden, sin tildes, sobre nombre, DNI y correo.
        /// </summary>
        public async Task<PagedResult<PropietarioListItemDto>> GetPaged(int page, int pageSize, string? search, int? projectId)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > 100 ? 10 : pageSize;

            // Sin búsqueda el patrón es un único '%', que casa con todo (el texto comparado nunca es NULL).
            var patrones = string.IsNullOrWhiteSpace(search)
                ? new[] { "%" }
                : search.Trim().ToLower()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(w => $"%{w}%")
                    .ToArray();

            const string cte = """
                WITH universo AS (
                    SELECT t.person_id, max(t.alta) AS alta
                    FROM (
                        SELECT pr.person_id, pr.created_date_time AS alta
                        FROM propietario pr
                        WHERE pr.state
                        UNION ALL
                        SELECT p.person_id, ur.created_date_time
                        FROM user_role ur
                        JOIN app_user u ON u.user_id = ur.user_id AND u.state
                        JOIN person p   ON p.user_id = u.user_id AND p.state
                        WHERE ur.role_id = @Rol AND ur.state
                    ) t
                    GROUP BY t.person_id
                ),
                filtrado AS (
                    SELECT p.person_id, x.alta
                    FROM universo x
                    JOIN person p        ON p.person_id = x.person_id AND p.state
                    LEFT JOIN app_user u ON u.user_id = p.user_id AND u.state
                    WHERE unaccent(lower(coalesce(p.full_name, '') || ' '
                                         || coalesce(p.document_identity_code, '') || ' '
                                         || coalesce(u.email, '')))
                          LIKE ALL (ARRAY(SELECT unaccent(patron) FROM unnest(@Patrones) AS patron))
                      AND (@ProjectId = 0 OR EXISTS (
                            SELECT 1 FROM propietario pr
                            WHERE pr.person_id = p.person_id AND pr.state AND pr.project_id = @ProjectId))
                ),
                pagina AS (
                    SELECT person_id, alta
                    FROM filtrado
                    ORDER BY alta DESC, person_id DESC
                    LIMIT @Limit OFFSET @Offset
                )
                """;

            // solo_propietario: sin otra fila en user_role (con cualquier state: el token de la
            // intranet no mira el state) y sin correo @abril.pe (entra con Microsoft).
            var sql = cte + """
                SELECT count(*)::int FROM filtrado;
                """ + cte + """
                SELECT p.person_id,
                       u.user_id,
                       p.document_identity_code AS dni,
                       p.first_names,
                       p.first_last_name,
                       p.second_last_name,
                       p.full_name,
                       u.email,
                       p.phone_number,
                       CASE WHEN u.user_id IS NULL THEN 'SIN_CUENTA'
                            WHEN NOT EXISTS (SELECT 1 FROM user_role r
                                             WHERE r.user_id = u.user_id AND r.role_id = @Rol AND r.state) THEN 'SIN_ROL'
                            WHEN u.password IS NULL OR u.password = '' THEN 'PENDIENTE'
                            WHEN NOT u.active THEN 'DESACTIVADO'
                            ELSE 'ACTIVO' END AS acceso,
                       (u.user_id IS NOT NULL
                        AND lower(u.email) NOT LIKE '%@abril.pe'
                        AND NOT EXISTS (SELECT 1 FROM user_role o
                                        WHERE o.user_id = u.user_id AND o.role_id <> @Rol)) AS solo_propietario
                FROM pagina pg
                JOIN person p        ON p.person_id = pg.person_id
                LEFT JOIN app_user u ON u.user_id = p.user_id AND u.state
                ORDER BY pg.alta DESC, pg.person_id DESC;
                """ + cte + """
                SELECT pr.propietario_id,
                       pr.person_id,
                       pr.project_id,
                       pj.project_description AS proyecto,
                       pr.torre,
                       pr.departamento
                FROM pagina pg
                JOIN propietario pr ON pr.person_id = pg.person_id AND pr.state
                JOIN project pj     ON pj.project_id = pr.project_id
                ORDER BY pj.project_description, pr.torre NULLS FIRST, pr.departamento;
                """;

            using var ctx = _factory.CreateDbContext();
            var conn = ctx.Database.GetDbConnection();
            await conn.OpenAsync();

            using var multi = await conn.QueryMultipleAsync(sql, new
            {
                Rol = RolPropietarioId,
                Patrones = patrones,
                ProjectId = projectId ?? 0,
                Limit = pageSize,
                Offset = (page - 1) * pageSize,
            });

            var total = await multi.ReadSingleAsync<int>();
            var filas = (await multi.ReadAsync<PropietarioListItemDto>()).ToList();
            var propiedades = (await multi.ReadAsync<PropiedadDto>()).ToLookup(p => p.PersonId);

            foreach (var fila in filas)
                fila.Propiedades = propiedades[fila.PersonId].ToList();

            return new PagedResult<PropietarioListItemDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalRecords = total,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                Data = filas,
            };
        }

        public async Task<PropietarioPersonaDto?> GetPersonaPorDni(string dni)
        {
            using var ctx = _factory.CreateDbContext();

            // Sin filtro de state: el DNI es único en toda la tabla.
            var persona = await ctx.Person
                .Where(p => p.DocumentIdentityCode == dni)
                .Select(p => new
                {
                    p.PersonId,
                    p.State,
                    p.UserId,
                    p.FirstNames,
                    p.FirstLastName,
                    p.SecondLastName,
                    p.Email,
                    p.PhoneNumber,
                })
                .FirstOrDefaultAsync();

            if (persona == null)
                return null;

            if (!persona.State)
                throw new AbrilException("El DNI pertenece a una persona dada de baja en el sistema.", 409);

            var usuario = persona.UserId == null
                ? null
                : await ctx.User
                    .Where(u => u.UserId == persona.UserId && u.State)
                    .Select(u => new { u.UserId, u.Email })
                    .FirstOrDefaultAsync();

            return new PropietarioPersonaDto
            {
                Fuente = "SISTEMA",
                PersonId = persona.PersonId,
                FirstNames = persona.FirstNames,
                FirstLastName = persona.FirstLastName,
                SecondLastName = persona.SecondLastName,
                Email = usuario?.Email ?? persona.Email,
                PhoneNumber = persona.PhoneNumber,
                TieneUsuario = usuario != null,
                YaEsPropietario = await EsPropietario(ctx, persona.PersonId, usuario?.UserId),
            };
        }

        public async Task<PropietarioGuardadoRepoDto> Crear(PropietarioCreateDto dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();
            PropietarioGuardadoRepoDto? resultado = null;

            var strategy = ctx.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await ctx.Database.BeginTransactionAsync();
                var ahora = DateTime.UtcNow;

                // Sin filtro de state: el DNI es único en toda la tabla (person_document_identity_code_key).
                var person = await ctx.Person.FirstOrDefaultAsync(p => p.DocumentIdentityCode == dto.Dni);

                if (person != null && !person.State)
                    throw new AbrilException("El DNI pertenece a una persona dada de baja en el sistema.", 409);

                if (person == null)
                {
                    person = new Person
                    {
                        DocumentIdentityTypeId = TipoDocumentoDni,
                        DocumentIdentityCode = dto.Dni,
                        FirstNames = dto.FirstNames,
                        FirstLastName = dto.FirstLastName,
                        SecondLastName = dto.SecondLastName,
                        FullName = NombreCompleto(dto.FirstNames, dto.FirstLastName, dto.SecondLastName),
                        Email = dto.Email,
                        PhoneNumber = dto.PhoneNumber,
                        Active = true,
                        State = true,
                        CreatedDateTime = ahora,
                        CreatedUserId = userId,
                    };
                    ctx.Person.Add(person);
                    await ctx.SaveChangesAsync();
                }
                else
                {
                    var usuarioVigente = await UsuarioVigente(ctx, person);
                    if (await EsPropietario(ctx, person.PersonId, usuarioVigente?.UserId))
                        throw new AbrilException("Ya está en la lista de propietarios.", 409);

                    // La persona ya existe (GTH, Seguridad): sus nombres no se pisan. Solo se
                    // completan si no los tiene (el alta por Microsoft deja solo full_name).
                    if (string.IsNullOrWhiteSpace(person.FirstNames))
                    {
                        person.FirstNames = dto.FirstNames;
                        person.FirstLastName = dto.FirstLastName;
                        person.SecondLastName = dto.SecondLastName;
                        person.FullName = NombreCompleto(dto.FirstNames, dto.FirstLastName, dto.SecondLastName);
                    }
                    person.Email ??= dto.Email;
                    person.PhoneNumber = dto.PhoneNumber ?? person.PhoneNumber;
                    person.UpdatedDateTime = ahora;
                    person.UpdatedUserId = userId;
                }

                var (usuario, enviarInvitacion) = await AsegurarCuenta(ctx, person, dto.Email!, userId, ahora);
                await AsegurarRolPropietario(ctx, usuario.UserId, userId, ahora);
                await ValidarProyectos(ctx, dto.Propiedades);

                foreach (var propiedad in dto.Propiedades)
                {
                    ctx.Propietario.Add(new Propietario
                    {
                        PersonId = person.PersonId,
                        ProjectId = propiedad.ProjectId,
                        Torre = propiedad.Torre,
                        Departamento = propiedad.Departamento,
                        CreatedDateTime = ahora,
                        CreatedUserId = userId,
                    });
                }

                await ctx.SaveChangesAsync();
                await transaction.CommitAsync();

                resultado = new PropietarioGuardadoRepoDto
                {
                    PersonId = person.PersonId,
                    UserId = usuario.UserId,
                    EnviarInvitacion = enviarInvitacion,
                };
            });

            return resultado!;
        }

        public async Task<PropietarioGuardadoRepoDto> Actualizar(int personId, PropietarioUpdateDto dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();
            PropietarioGuardadoRepoDto? resultado = null;

            var strategy = ctx.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await ctx.Database.BeginTransactionAsync();
                var ahora = DateTime.UtcNow;

                var person = await ctx.Person.FirstOrDefaultAsync(p => p.PersonId == personId && p.State)
                    ?? throw new AbrilException("Propietario no encontrado.", 404);

                var usuario = await UsuarioVigente(ctx, person);
                if (!await EsPropietario(ctx, personId, usuario?.UserId))
                    throw new AbrilException("Propietario no encontrado.", 404);

                person.FirstNames = dto.FirstNames;
                person.FirstLastName = dto.FirstLastName;
                person.SecondLastName = dto.SecondLastName;
                person.FullName = NombreCompleto(dto.FirstNames, dto.FirstLastName, dto.SecondLastName);
                person.PhoneNumber = dto.PhoneNumber;
                person.UpdatedDateTime = ahora;
                person.UpdatedUserId = userId;

                bool enviarInvitacion;
                if (usuario == null)
                {
                    // Le eliminaron el usuario desde Seguridad: se le crea uno nuevo con este correo.
                    (usuario, enviarInvitacion) = await AsegurarCuenta(ctx, person, dto.Email!, userId, ahora);
                }
                else if (!string.Equals(usuario.Email, dto.Email, StringComparison.OrdinalIgnoreCase))
                {
                    if (!await EsSoloPropietario(ctx, usuario))
                        throw new AbrilException(
                            "Este usuario también entra a la intranet: su correo se cambia desde Seguridad → Usuarios.", 409);

                    await ValidarCorreoLibre(ctx, dto.Email!, salvoUserId: usuario.UserId);
                    usuario.Email = dto.Email!;
                    usuario.UpdatedDateTime = ahora;
                    usuario.UpdatedUserId = userId;

                    // Si todavía no creó su contraseña, la invitación anterior fue al correo equivocado.
                    enviarInvitacion = string.IsNullOrEmpty(usuario.Password);
                }
                else
                {
                    enviarInvitacion = false;
                }

                await AsegurarRolPropietario(ctx, usuario.UserId, userId, ahora);
                await ValidarProyectos(ctx, dto.Propiedades);

                var existentes = await ctx.Propietario
                    .Where(pr => pr.PersonId == personId && pr.State)
                    .ToListAsync();

                var quedan = dto.Propiedades
                    .Where(p => p.PropietarioId != null)
                    .Select(p => p.PropietarioId!.Value)
                    .ToHashSet();

                foreach (var fila in existentes.Where(f => !quedan.Contains(f.PropietarioId)))
                {
                    fila.State = false;
                    fila.UpdatedDateTime = ahora;
                    fila.UpdatedUserId = userId;
                }

                foreach (var propiedad in dto.Propiedades.Where(p => p.PropietarioId != null))
                {
                    var fila = existentes.FirstOrDefault(f => f.PropietarioId == propiedad.PropietarioId)
                        ?? throw new AbrilException("Una de las propiedades ya no existe. Vuelve a abrir el propietario.", 409);

                    if (fila.ProjectId == propiedad.ProjectId
                        && fila.Torre == propiedad.Torre
                        && fila.Departamento == propiedad.Departamento)
                        continue;

                    fila.ProjectId = propiedad.ProjectId;
                    fila.Torre = propiedad.Torre;
                    fila.Departamento = propiedad.Departamento;
                    fila.UpdatedDateTime = ahora;
                    fila.UpdatedUserId = userId;
                }

                // Bajas y cambios antes que las altas: el índice único (parcial, WHERE state) se
                // valida por sentencia, y una propiedad quitada y vuelta a agregar en el mismo
                // guardado chocaría con su propia fila si EF insertara primero.
                await ctx.SaveChangesAsync();

                foreach (var propiedad in dto.Propiedades.Where(p => p.PropietarioId == null))
                {
                    ctx.Propietario.Add(new Propietario
                    {
                        PersonId = personId,
                        ProjectId = propiedad.ProjectId,
                        Torre = propiedad.Torre,
                        Departamento = propiedad.Departamento,
                        CreatedDateTime = ahora,
                        CreatedUserId = userId,
                    });
                }

                await ctx.SaveChangesAsync();
                await transaction.CommitAsync();

                resultado = new PropietarioGuardadoRepoDto
                {
                    PersonId = personId,
                    UserId = usuario.UserId,
                    EnviarInvitacion = enviarInvitacion,
                };
            });

            return resultado!;
        }

        public async Task<PropietarioCuentaDto?> GetCuenta(int personId)
        {
            using var ctx = _factory.CreateDbContext();

            var person = await ctx.Person.FirstOrDefaultAsync(p => p.PersonId == personId && p.State);
            if (person == null)
                return null;

            var usuario = await UsuarioVigente(ctx, person);
            if (usuario == null || !await EsPropietario(ctx, personId, usuario.UserId))
                return null;

            return new PropietarioCuentaDto
            {
                UserId = usuario.UserId,
                TienePassword = !string.IsNullOrEmpty(usuario.Password),
            };
        }

        public async Task Eliminar(int personId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var strategy = ctx.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await ctx.Database.BeginTransactionAsync();
                var ahora = DateTime.UtcNow;

                var person = await ctx.Person.FirstOrDefaultAsync(p => p.PersonId == personId && p.State)
                    ?? throw new AbrilException("Propietario no encontrado.", 404);

                var usuario = await UsuarioVigente(ctx, person);
                if (!await EsPropietario(ctx, personId, usuario?.UserId))
                    throw new AbrilException("Propietario no encontrado.", 404);

                await ctx.Propietario
                    .Where(pr => pr.PersonId == personId && pr.State)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.State, false)
                        .SetProperty(x => x.UpdatedDateTime, ahora)
                        .SetProperty(x => x.UpdatedUserId, userId));

                // DELETE y no state = false, igual que Seguridad → Usuarios: el login arma el token
                // con todas las filas de user_role. El refresh de la app lo saca en ≤ 2 minutos.
                // El usuario se queda: puede tener otros roles.
                if (usuario != null)
                {
                    await ctx.UserRole
                        .Where(ur => ur.UserId == usuario.UserId && ur.RoleId == RolPropietarioId)
                        .ExecuteDeleteAsync();
                }

                await transaction.CommitAsync();
            });
        }

        private static async Task<bool> EsPropietario(AppDbContext ctx, int personId, int? usuarioId) =>
            await ctx.Propietario.AnyAsync(pr => pr.PersonId == personId && pr.State)
            || (usuarioId != null && await ctx.UserRole.AnyAsync(ur =>
                ur.UserId == usuarioId && ur.RoleId == RolPropietarioId && ur.State));

        private static Task<UserModel?> UsuarioVigente(AppDbContext ctx, Person person) =>
            person.UserId == null
                ? Task.FromResult<UserModel?>(null)
                : ctx.User.FirstOrDefaultAsync(u => u.UserId == person.UserId && u.State);

        /// <summary>
        /// Solo entra a la app: ningún otro rol (con cualquier state, porque el token de la intranet
        /// no lo mira) y un correo que no es @abril.pe (esos entran con Microsoft).
        /// </summary>
        private static async Task<bool> EsSoloPropietario(AppDbContext ctx, UserModel usuario) =>
            !usuario.Email.EndsWith("@abril.pe", StringComparison.OrdinalIgnoreCase)
            && !await ctx.UserRole.AnyAsync(ur => ur.UserId == usuario.UserId && ur.RoleId != RolPropietarioId);

        /// <summary>
        /// El usuario vigente de la persona, o uno nuevo con el correo del formulario. El nuevo nace
        /// inactivo y sin contraseña, como el alta de Seguridad → Usuarios: crear la contraseña con
        /// el enlace de la invitación lo activa. Se invita solo si la cuenta no tiene contraseña.
        /// </summary>
        private static async Task<(UserModel Usuario, bool EnviarInvitacion)> AsegurarCuenta(
            AppDbContext ctx, Person person, string email, int userId, DateTime ahora)
        {
            var usuario = await UsuarioVigente(ctx, person);
            if (usuario != null)
                return (usuario, string.IsNullOrEmpty(usuario.Password));

            await ValidarCorreoLibre(ctx, email, salvoUserId: null);

            usuario = new UserModel
            {
                Email = email,
                Password = null,
                Active = false,
                State = true,
                EmailConfirmed = false,
                CreatedDateTime = ahora,
                CreatedUserId = userId,
            };
            ctx.User.Add(usuario);
            await ctx.SaveChangesAsync();

            person.UserId = usuario.UserId;
            person.UpdatedDateTime = ahora;
            person.UpdatedUserId = userId;

            return (usuario, true);
        }

        /// <summary>
        /// Busca la fila con cualquier state porque hay un índice único (user_id, role_id): si
        /// existe dada de baja, se revive en vez de insertar otra.
        /// </summary>
        private static async Task AsegurarRolPropietario(AppDbContext ctx, int usuarioId, int userId, DateTime ahora)
        {
            var fila = await ctx.UserRole.FirstOrDefaultAsync(ur => ur.UserId == usuarioId && ur.RoleId == RolPropietarioId);

            if (fila == null)
            {
                ctx.UserRole.Add(new UserRole
                {
                    UserId = usuarioId,
                    RoleId = RolPropietarioId,
                    Active = true,
                    State = true,
                    CreatedDateTime = ahora,
                    CreatedUserId = userId,
                });
            }
            else if (!fila.State || !fila.Active)
            {
                fila.State = true;
                fila.Active = true;
                fila.UpdatedDateTime = ahora;
                fila.UpdatedUserId = userId;
            }
        }

        /// <summary>
        /// Proyectos que se venden (tipo PROYECTO) y no eliminados. No exige active: una propiedad
        /// de un proyecto que dejó de verse en los desplegables tiene que poder seguir guardándose.
        /// </summary>
        private static async Task ValidarProyectos(AppDbContext ctx, List<PropiedadGuardarDto> propiedades)
        {
            var ids = propiedades.Select(p => p.ProjectId).Distinct().ToList();
            var validos = await ctx.Project
                .CountAsync(p => ids.Contains(p.ProjectId) && p.State && p.ProjectTipoId == ProjectTipoIds.Proyecto);

            if (validos != ids.Count)
                throw new AbrilException("Uno de los proyectos elegidos no existe o no es un proyecto a la venta.", 400);
        }

        /// <summary>
        /// El correo es único solo entre los usuarios vigentes (índice <c>uq_app_user_email ... WHERE
        /// state</c>), sin distinguir mayúsculas: mismo criterio que Seguridad → Usuarios.
        /// </summary>
        private static async Task ValidarCorreoLibre(AppDbContext ctx, string email, int? salvoUserId)
        {
            var normalizado = email.Trim().ToLower();

            var enUso = await ctx.User.AnyAsync(u =>
                u.State
                && u.Email != null
                && u.Email.ToLower() == normalizado
                && (salvoUserId == null || u.UserId != salvoUserId));

            if (enUso)
                throw new AbrilException("Ya existe un usuario con ese correo.", 409);
        }

        private static string NombreCompleto(params string?[] partes) =>
            string.Join(" ", partes.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
}
