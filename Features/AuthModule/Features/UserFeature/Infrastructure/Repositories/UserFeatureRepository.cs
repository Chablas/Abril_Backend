using Dapper;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.AuthModule.Shared.Dtos;
using Abril_Backend.Features.AuthModule.UserFeature.Application.Dtos;
using Abril_Backend.Features.AuthModule.UserFeature.Application.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Constants;
using UserModel = Abril_Backend.Infrastructure.Models.User;

namespace Abril_Backend.Features.AuthModule.UserFeature.Infrastructure.Repositories
{
    public class UserFeatureRepository : IUserFeatureRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public UserFeatureRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        /// <summary>
        /// Página de la tabla de Usuarios. <paramref name="categoriaId"/> filtra por la
        /// categoría del trabajador, a la que se llega por
        /// <c>person → workers.puesto_id → puesto.categoria_id</c> (la ficha ya no guarda la
        /// categoría). Un usuario sin ficha viva de trabajador — contratistas, colaboradores
        /// externos — queda fuera en cuanto se elige una categoría, que es lo correcto: no
        /// tiene ninguna.
        /// </summary>
        public async Task<PagedResult<UserListItemDto>> GetPaged(int page, int pageSize, string? search = null, int? categoriaId = null)
        {
            page = page < 1 ? 1 : page;
            using var ctx = _factory.CreateDbContext();

            var hasSearch = !string.IsNullOrWhiteSpace(search);
            // Búsqueda por palabras en cualquier orden: cada palabra debe aparecer en el
            // nombre, DNI o correo (misma semántica que SearchInput.matches del frontend).
            // Así "jairo diaz" encuentra a "DIAZ BUIZA JAIRO ELIU".
            //
            // Sin búsqueda el patrón es un único '%', que casa con todo: así el WHERE queda
            // uno solo para los dos casos en vez de duplicar la consulta entera. El texto
            // contra el que se compara nunca es NULL (app_user.email es NOT NULL y el resto
            // va con COALESCE), así que ese '%' no descarta ninguna fila.
            var likePatterns = hasSearch
                ? search!.Trim().ToLower()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(w => $"%{w}%")
                    .ToArray()
                : new[] { "%" };

            // 0 = sin filtro de categoría. Se usa un centinela y no NULL porque el parámetro
            // viaja en SQL crudo, donde un NULL suelto no tiene tipo inferible para Postgres;
            // categoria_id es una identity que arranca en 1, así que el 0 nunca colisiona.
            var categoriaFilter = categoriaId ?? 0;
            var hasFilters = hasSearch || categoriaFilter != 0;

            // Sin ningún filtro el total es la cantidad de usuarios vivos, sin pagar los
            // joins de la consulta filtrada.
            var totalRecords = hasFilters
                ? (await ctx.Database.SqlQuery<int>($"""
                    SELECT COUNT(DISTINCT u.user_id)::int AS "Value"
                    FROM app_user u
                    LEFT JOIN person p ON p.user_id = u.user_id AND p.state = true
                    WHERE u.state = true
                      AND LOWER(COALESCE(p.full_name, '') || ' ' || COALESCE(p.document_identity_code, '') || ' ' || u.email)
                          LIKE ALL ({likePatterns})
                      AND ({categoriaFilter} = 0 OR EXISTS (
                            SELECT 1
                            FROM workers w
                            JOIN puesto pu ON pu.puesto_id = w.puesto_id
                            WHERE w.person_id = p.person_id
                              AND w.state = true
                              AND pu.categoria_id = {categoriaFilter}))
                    """).ToListAsync()).FirstOrDefault()
                : await ctx.User.CountAsync(u => u.State);

            var baseRows = await ctx.Database.SqlQuery<UserBaseRow>($"""
                SELECT DISTINCT ON (u.user_id)
                    u.user_id,
                    u.email,
                    u.active,
                    CASE WHEN cu.contractor_user_id IS NOT NULL THEN 'CONTRATISTA'
                         WHEN p.person_id IS NOT NULL THEN 'PERSONA'
                         ELSE 'COLABORADOR' END AS user_type,
                    p.full_name AS display_name,
                    p.document_identity_code,
                    p.first_names,
                    p.first_last_name,
                    p.second_last_name,
                    p.phone_number
                FROM app_user u
                LEFT JOIN person p ON p.user_id = u.user_id AND p.state = true
                LEFT JOIN contractor_user cu ON cu.user_id = u.user_id AND cu.state = true
                WHERE u.state = true
                  AND LOWER(COALESCE(p.full_name, '') || ' ' || COALESCE(p.document_identity_code, '') || ' ' || u.email)
                      LIKE ALL ({likePatterns})
                  AND ({categoriaFilter} = 0 OR EXISTS (
                        SELECT 1
                        FROM workers w
                        JOIN puesto pu ON pu.puesto_id = w.puesto_id
                        WHERE w.person_id = p.person_id
                          AND w.state = true
                          AND pu.categoria_id = {categoriaFilter}))
                ORDER BY u.user_id DESC
                LIMIT {pageSize} OFFSET {(page - 1) * pageSize}
                """).ToListAsync();

            var userIds = baseRows.Select(r => r.UserId).ToList();

            var rolesData = await ctx.UserRole
                .Where(ur => userIds.Contains(ur.UserId) && ur.State)
                .Join(ctx.Role.Where(r => r.State),
                    ur => ur.RoleId,
                    r => r.RoleId,
                    (ur, r) => new { ur.UserId, r.RoleId, r.RoleDescription })
                .ToListAsync();

            var rolesMap = rolesData
                .GroupBy(r => r.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(r => new RoleItemDto { RoleId = r.RoleId, RoleDescription = r.RoleDescription }).ToList());

            var data = baseRows.Select(r => new UserListItemDto
            {
                UserId = r.UserId,
                Email = r.Email,
                Active = r.Active,
                UserType = r.UserType,
                DisplayName = r.DisplayName,
                DocumentIdentityCode = r.DocumentIdentityCode,
                FirstNames = r.FirstNames,
                FirstLastName = r.FirstLastName,
                SecondLastName = r.SecondLastName,
                PhoneNumber = r.PhoneNumber,
                Roles = rolesMap.TryGetValue(r.UserId, out var roles) ? roles : new()
            }).ToList();

            return new PagedResult<UserListItemDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data = data
            };
        }

        /// <summary>
        /// Categorías para el desplegable del filtro: solo las que tienen al menos un usuario
        /// del sistema detrás, para no ofrecer opciones que dejarían la tabla vacía. Salen del
        /// mismo camino que usa el filtro (<c>workers.puesto_id → puesto.categoria_id</c>), así
        /// que la lista de opciones y el filtro no pueden contradecirse.
        ///
        /// A propósito NO se filtra por <c>puesto.active</c> ni <c>categoria.active</c>: si un
        /// trabajador con usuario tiene ese puesto, esa categoría es real y hay que poder filtrarla.
        /// </summary>
        public async Task<List<UserCategoriaOptionDto>> GetCategoriaOptions()
        {
            using var ctx = _factory.CreateDbContext();

            return await ctx.Database.SqlQuery<UserCategoriaOptionDto>($"""
                SELECT DISTINCT c.categoria_id, c.nombre
                FROM app_user u
                JOIN person p ON p.user_id = u.user_id AND p.state = true
                JOIN workers w ON w.person_id = p.person_id AND w.state = true
                JOIN puesto pu ON pu.puesto_id = w.puesto_id
                JOIN categoria c ON c.categoria_id = pu.categoria_id
                WHERE u.state = true
                ORDER BY c.nombre
                """).ToListAsync();
        }

        /// <summary>
        /// Cabecera, roles y funcionalidades del usuario en un solo viaje a la base. El acceso se
        /// lee igual que al iniciar sesión (<c>AuthRepository.GetAllowedFeaturesAsync</c>): solo
        /// user_role y roles vivos. Las funcionalidades llegan como una fila por funcionalidad y
        /// rol que la da, y se juntan acá para que cada una salga una vez con todos sus roles.
        /// </summary>
        public async Task<UserDetailDto?> GetDetail(int userId)
        {
            using var ctx = _factory.CreateDbContext();

            const string sql = """
                SELECT u.user_id,
                       u.email,
                       u.active,
                       p.full_name AS display_name,
                       p.document_identity_code,
                       CASE WHEN EXISTS (SELECT 1 FROM contractor_user cu
                                         WHERE cu.user_id = u.user_id AND cu.state) THEN 'CONTRATISTA'
                            WHEN p.person_id IS NOT NULL THEN 'PERSONA'
                            ELSE 'COLABORADOR' END AS user_type
                FROM app_user u
                LEFT JOIN LATERAL (
                    SELECT pe.person_id, pe.full_name, pe.document_identity_code
                    FROM person pe
                    WHERE pe.user_id = u.user_id AND pe.state
                    ORDER BY pe.person_id DESC
                    LIMIT 1
                ) p ON true
                WHERE u.user_id = @userId AND u.state;

                SELECT r.role_id,
                       r.role_description,
                       (SELECT COUNT(*)::int
                          FROM user_role x
                          JOIN app_user xu ON xu.user_id = x.user_id AND xu.state
                         WHERE x.role_id = r.role_id AND x.state) AS users_count,
                       (SELECT COUNT(*)::int
                          FROM role_feature x
                         WHERE x.role_id = r.role_id) AS features_count
                FROM user_role ur
                JOIN role r ON r.role_id = ur.role_id AND r.state
                WHERE ur.user_id = @userId
                  AND ur.state
                ORDER BY r.role_description;

                SELECT f.feature_id,
                       f.feature_key,
                       f.module_id,
                       m.module_name,
                       r.role_id,
                       r.role_description
                FROM user_role ur
                JOIN role r          ON r.role_id     = ur.role_id AND r.state
                JOIN role_feature rf ON rf.role_id    = r.role_id
                JOIN feature f       ON f.feature_id  = rf.feature_id
                LEFT JOIN module m   ON m.module_id   = f.module_id
                WHERE ur.user_id = @userId
                  AND ur.state
                ORDER BY m.module_name NULLS LAST, f.feature_key, r.role_description;
                """;

            var conn = ctx.Database.GetDbConnection();
            await conn.OpenAsync();
            using var multi = await conn.QueryMultipleAsync(sql, new { userId });

            var detail = await multi.ReadSingleOrDefaultAsync<UserDetailDto>();
            if (detail == null) return null;

            detail.Roles    = (await multi.ReadAsync<AccessRoleDto>()).ToList();
            detail.Features = (await multi.ReadAsync<FeatureRoleRow>())
                .GroupBy(r => r.FeatureId)
                .Select(g => new AccessFeatureDto
                {
                    FeatureId  = g.Key,
                    FeatureKey = g.First().FeatureKey,
                    ModuleId   = g.First().ModuleId,
                    ModuleName = g.First().ModuleName,
                    ViaRoles   = g.Select(r => new RoleRefDto { RoleId = r.RoleId, RoleDescription = r.RoleDescription }).ToList(),
                })
                .ToList();

            return detail;
        }

        public async Task<List<AbrilWorkerOptionDto>> GetAbrilWorkersWithoutUser()
        {
            using var ctx = _factory.CreateDbContext();

            // Trabajadores de Abril = existen en workers con email_corporativo @abril.pe.
            // "Sin usuario" = la person vinculada no tiene un app_user activo (user_id NULL
            // o el user apuntado está dado de baja). Un mismo person puede tener varias filas
            // en workers, por eso se agrupa por person_id.
            return await ctx.Worker
                .Where(w => w.PersonId != null
                         && w.EmailCorporativo != null
                         && w.EmailCorporativo.ToLower().EndsWith("@abril.pe")
                         && w.Person != null
                         && w.Person.State
                         && !ctx.User.Any(u => u.UserId == w.Person!.UserId && u.State))
                .GroupBy(w => new
                {
                    w.PersonId,
                    w.Person!.FullName,
                    w.Person.DocumentIdentityCode
                })
                .Select(g => new AbrilWorkerOptionDto
                {
                    PersonId = g.Key.PersonId!.Value,
                    FullName = g.Key.FullName ?? string.Empty,
                    DocumentIdentityCode = g.Key.DocumentIdentityCode,
                    EmailCorporativo = g.Min(w => w.EmailCorporativo)!,
                    // Si alguna de sus filas en workers es "Staff", se considera Staff
                    // (usado por el front para preseleccionar el rol EVALUADOR).
                    ObraOficinaStaffId = g.Any(w => w.ObraOficinaStaffId == ObraOficinaStaffIds.Staff)
                        ? ObraOficinaStaffIds.Staff
                        : g.Max(w => w.ObraOficinaStaffId)
                })
                .OrderBy(o => o.FullName)
                .ToListAsync();
        }

        public async Task CreateAbrilWorkerUser(AbrilWorkerUserCreateDto dto, int createdUserId)
        {
            using var ctx = _factory.CreateDbContext();

            var strategy = ctx.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await ctx.Database.BeginTransactionAsync();
                try
                {
                    var person = await ctx.Person.FirstOrDefaultAsync(p => p.PersonId == dto.PersonId && p.State)
                        ?? throw new AbrilException("Persona no encontrada.", 404);

                    // El correo corporativo @abril.pe vive en workers.email_corporativo.
                    var worker = await ctx.Worker
                        .FirstOrDefaultAsync(w => w.PersonId == dto.PersonId
                                               && w.EmailCorporativo != null
                                               && w.EmailCorporativo.ToLower().EndsWith("@abril.pe"))
                        ?? throw new AbrilException("El trabajador de Abril no tiene un correo @abril.pe registrado.", 400);

                    var userExists = await ctx.User.AnyAsync(u => u.UserId == person.UserId && u.State);
                    if (userExists)
                        throw new AbrilException("El trabajador ya tiene un usuario registrado.");

                    // Los trabajadores de Abril ingresan vía Microsoft SSO: el usuario nace
                    // activo y con el correo confirmado, sin contraseña (igual que el alta por login).
                    var user = new UserModel
                    {
                        Email = worker.EmailCorporativo!,
                        Password = null,
                        Active = true,
                        State = true,
                        EmailConfirmed = true,
                        CreatedDateTime = DateTime.UtcNow,
                        CreatedUserId = createdUserId
                    };
                    ctx.User.Add(user);
                    await ctx.SaveChangesAsync();

                    person.UserId = user.UserId;
                    person.UpdatedDateTime = DateTime.UtcNow;
                    person.UpdatedUserId = createdUserId;
                    await ctx.SaveChangesAsync();

                    foreach (var roleId in dto.RoleIds.Distinct())
                    {
                        ctx.UserRole.Add(new UserRole
                        {
                            UserId = user.UserId,
                            RoleId = roleId,
                            Active = true,
                            State = true,
                            CreatedDateTime = DateTime.UtcNow,
                            CreatedUserId = createdUserId
                        });
                    }
                    await ctx.SaveChangesAsync();

                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task CreateAbrilManualUser(string email, string? fullName, List<int> roleIds, int createdUserId)
        {
            using var ctx = _factory.CreateDbContext();

            var strategy = ctx.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await ctx.Database.BeginTransactionAsync();
                try
                {
                    // Índice único uq_app_user_email: se valida sin importar el state para
                    // evitar chocar con la restricción (incluye usuarios dados de baja).
                    var exists = await ctx.User.AnyAsync(u => u.Email.ToLower() == email.ToLower());
                    if (exists)
                        throw new AbrilException("Ya existe un usuario con ese correo.", 409);

                    // Trabajador de Abril: ingresa vía Microsoft SSO, nace activo y con el
                    // correo confirmado, sin contraseña (igual que el alta por login).
                    var user = new UserModel
                    {
                        Email = email,
                        Password = null,
                        Active = true,
                        State = true,
                        EmailConfirmed = true,
                        CreatedDateTime = DateTime.UtcNow,
                        CreatedUserId = createdUserId
                    };
                    ctx.User.Add(user);
                    await ctx.SaveChangesAsync();

                    // Person mínima (sin DNI) para que el usuario muestre nombre en el listado
                    // y quede vinculado desde ya; mismo criterio que el alta por login SSO.
                    var person = new Person
                    {
                        UserId = user.UserId,
                        DocumentIdentityTypeId = null,
                        DocumentIdentityCode = null,
                        FullName = (string.IsNullOrWhiteSpace(fullName) ? email : fullName).ToUpper(),
                        Active = true,
                        State = true,
                        CreatedDateTime = DateTime.UtcNow,
                        CreatedUserId = createdUserId
                    };
                    ctx.Person.Add(person);
                    await ctx.SaveChangesAsync();

                    foreach (var roleId in roleIds.Distinct())
                    {
                        ctx.UserRole.Add(new UserRole
                        {
                            UserId = user.UserId,
                            RoleId = roleId,
                            Active = true,
                            State = true,
                            CreatedDateTime = DateTime.UtcNow,
                            CreatedUserId = createdUserId
                        });
                    }
                    await ctx.SaveChangesAsync();

                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task<UserModel> Create(UserFeatureCreateDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            UserModel? result = null;

            var strategy = ctx.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await ctx.Database.BeginTransactionAsync();
                try
                {
                    var person = await ctx.Person
                        .FirstOrDefaultAsync(p => p.DocumentIdentityCode == dto.DocumentIdentityCode && p.State);

                    if (person != null)
                    {
                        var userExists = await ctx.User.AnyAsync(u => u.UserId == person.UserId && u.State);
                        if (userExists)
                            throw new AbrilException("La persona ya tiene un usuario registrado.");
                    }
                    else
                    {
                        person = new Person
                        {
                            DocumentIdentityCode = dto.DocumentIdentityCode,
                            DocumentIdentityTypeId = 1,
                            FirstNames = dto.FirstNames,
                            FirstLastName = dto.FirstLastName,
                            SecondLastName = dto.SecondLastName,
                            FullName = $"{dto.FirstNames} {dto.FirstLastName} {dto.SecondLastName}",
                            PhoneNumber = dto.PhoneNumber,
                            Active = true,
                            State = true,
                            CreatedDateTime = DateTime.UtcNow,
                            CreatedUserId = dto.CreatedUserId
                        };
                        ctx.Person.Add(person);
                        await ctx.SaveChangesAsync();
                    }

                    var user = new UserModel
                    {
                        Email = dto.Email,
                        Active = false,
                        State = true,
                        EmailConfirmed = false,
                        CreatedDateTime = DateTime.UtcNow,
                        CreatedUserId = dto.CreatedUserId
                    };
                    ctx.User.Add(user);
                    await ctx.SaveChangesAsync();

                    person.UserId = user.UserId;
                    await ctx.SaveChangesAsync();

                    foreach (var roleId in dto.RoleIds)
                    {
                        ctx.UserRole.Add(new UserRole
                        {
                            UserId = user.UserId,
                            RoleId = roleId,
                            Active = true,
                            State = true,
                            CreatedDateTime = DateTime.UtcNow,
                            CreatedUserId = dto.CreatedUserId
                        });
                    }
                    await ctx.SaveChangesAsync();
                    await transaction.CommitAsync();
                    result = user;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });

            return result!;
        }

        public async Task Update(int userId, UserFeatureUpdateDto dto, int updatedUserId)
        {
            using var ctx = _factory.CreateDbContext();

            var strategy = ctx.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await ctx.Database.BeginTransactionAsync();
                try
                {
                    var user = await ctx.User.FirstOrDefaultAsync(u => u.UserId == userId && u.State)
                        ?? throw new AbrilException("Usuario no encontrado.", 404);

                    user.Email = dto.Email;
                    user.UpdatedDateTime = DateTime.UtcNow;
                    user.UpdatedUserId = updatedUserId;

                    var person = await ctx.Person.FirstOrDefaultAsync(p => p.UserId == userId && p.State);
                    if (person != null)
                    {
                        if (!string.IsNullOrWhiteSpace(dto.FirstNames))
                            person.FirstNames = dto.FirstNames;
                        if (!string.IsNullOrWhiteSpace(dto.FirstLastName))
                            person.FirstLastName = dto.FirstLastName;
                        if (!string.IsNullOrWhiteSpace(dto.SecondLastName))
                            person.SecondLastName = dto.SecondLastName;

                        var fullName = string.Join(" ", new[] { person.FirstNames, person.FirstLastName, person.SecondLastName }
                            .Where(n => !string.IsNullOrWhiteSpace(n)));
                        if (!string.IsNullOrWhiteSpace(fullName))
                            person.FullName = fullName;

                        person.PhoneNumber = dto.PhoneNumber;
                        person.UpdatedDateTime = DateTime.UtcNow;
                        person.UpdatedUserId = updatedUserId;
                    }

                    await ctx.SaveChangesAsync();

                    await ctx.Database.ExecuteSqlAsync($"DELETE FROM user_role WHERE user_id = {userId}");

                    foreach (var roleId in dto.RoleIds)
                    {
                        ctx.UserRole.Add(new UserRole
                        {
                            UserId = userId,
                            RoleId = roleId,
                            Active = true,
                            State = true,
                            CreatedDateTime = DateTime.UtcNow,
                            CreatedUserId = updatedUserId
                        });
                    }

                    await ctx.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (AbrilException)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task ToggleActive(int userId, int updatedUserId)
        {
            using var ctx = _factory.CreateDbContext();
            var user = await ctx.User.FirstOrDefaultAsync(u => u.UserId == userId && u.State)
                ?? throw new AbrilException("Usuario no encontrado.", 404);

            user.Active = !user.Active;
            user.UpdatedDateTime = DateTime.UtcNow;
            user.UpdatedUserId = updatedUserId;
            await ctx.SaveChangesAsync();
        }

        public async Task Delete(int userId, int updatedUserId)
        {
            using var ctx = _factory.CreateDbContext();
            var user = await ctx.User.FirstOrDefaultAsync(u => u.UserId == userId && u.State)
                ?? throw new AbrilException("Usuario no encontrado.", 404);

            user.State = false;
            user.UpdatedDateTime = DateTime.UtcNow;
            user.UpdatedUserId = updatedUserId;
            await ctx.SaveChangesAsync();
        }
    }

    /// <summary>Fila del detalle de usuario: una funcionalidad y uno de los roles que se la dan.</summary>
    internal class FeatureRoleRow
    {
        public int FeatureId { get; set; }
        public string FeatureKey { get; set; } = null!;
        public int? ModuleId { get; set; }
        public string? ModuleName { get; set; }
        public int RoleId { get; set; }
        public string RoleDescription { get; set; } = null!;
    }

    internal class UserBaseRow
    {
        public int UserId { get; set; }
        public string Email { get; set; } = null!;
        public bool Active { get; set; }
        public string UserType { get; set; } = null!;
        public string? DisplayName { get; set; }
        public string? DocumentIdentityCode { get; set; }
        public string? FirstNames { get; set; }
        public string? FirstLastName { get; set; }
        public string? SecondLastName { get; set; }
        public int? PhoneNumber { get; set; }
    }
}
