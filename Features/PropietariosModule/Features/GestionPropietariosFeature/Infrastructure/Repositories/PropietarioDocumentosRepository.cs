using Dapper;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Application.Dtos;
using Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;

namespace Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Infrastructure.Repositories
{
    /// <summary>
    /// Documentos de las propiedades de un propietario (modal «Documentos» de la pantalla
    /// Propietarios). Cada documento cuelga de una fila de <c>propietario</c>: si se quita la
    /// propiedad, sus documentos dejan de verse (en la intranet y en la app) sin borrarse.
    /// </summary>
    public class PropietarioDocumentosRepository : IPropietarioDocumentosRepository
    {
        /// <summary>
        /// El modal completo: tipos, propiedades y documentos. Las fechas salen en hora de Perú
        /// (<c>AT TIME ZONE</c>), sin depender del timezone de la sesión (dev y prod difieren).
        /// </summary>
        private const string SqlModal = """
            SELECT t.propietario_documento_tipo_id AS tipo_id,
                   t.nombre
            FROM propietario_documento_tipo t
            WHERE t.state AND t.active
            ORDER BY t.orden, t.nombre;

            SELECT pr.propietario_id,
                   pj.project_description AS proyecto,
                   pr.torre,
                   pr.departamento
            FROM propietario pr
            JOIN project pj ON pj.project_id = pr.project_id
            WHERE pr.person_id = @PersonId AND pr.state
            ORDER BY pj.project_description, pr.torre NULLS FIRST, pr.departamento, pr.propietario_id;

            SELECT d.propietario_documento_id      AS documento_id,
                   d.propietario_id,
                   d.propietario_documento_tipo_id AS tipo_id,
                   t.nombre                        AS tipo,
                   d.nombre,
                   d.archivo_nombre,
                   d.archivo_tamano_bytes          AS tamano_bytes,
                   d.created_date_time AT TIME ZONE 'America/Lima' AS subido_el,
                   d.leido_date_time   AT TIME ZONE 'America/Lima' AS leido_el
            FROM propietario_documento d
            JOIN propietario pr               ON pr.propietario_id = d.propietario_id AND pr.state
            JOIN propietario_documento_tipo t ON t.propietario_documento_tipo_id = d.propietario_documento_tipo_id
            WHERE pr.person_id = @PersonId AND d.state
            ORDER BY d.created_date_time DESC, d.propietario_documento_id DESC;
            """;

        private readonly IDbContextFactory<AppDbContext> _factory;

        public PropietarioDocumentosRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<PropietarioDocumentosDto> GetDocumentos(int personId)
        {
            using var ctx = _factory.CreateDbContext();
            var conn = ctx.Database.GetDbConnection();
            await conn.OpenAsync();

            using var multi = await conn.QueryMultipleAsync(SqlModal, new { PersonId = personId });
            return await LeerModal(multi);
        }

        public async Task<PropietarioDocumentosSubidaDto> GetContextoSubida(int personId, int[] propietarioIds, int[] tipoIds)
        {
            const string sql = """
                SELECT p.document_identity_code AS dni,
                       p.full_name
                FROM person p
                WHERE p.person_id = @PersonId AND p.state;

                SELECT pr.propietario_id,
                       pj.project_description AS proyecto,
                       pr.torre,
                       pr.departamento
                FROM propietario pr
                JOIN project pj ON pj.project_id = pr.project_id
                WHERE pr.person_id = @PersonId AND pr.state AND pr.propietario_id = ANY(@PropietarioIds);

                SELECT t.propietario_documento_tipo_id
                FROM propietario_documento_tipo t
                WHERE t.state AND t.active AND t.propietario_documento_tipo_id = ANY(@TipoIds);

                SELECT f.link_url
                FROM propietario_documento_folder f
                WHERE f.state AND f.active
                ORDER BY f.propietario_documento_folder_id
                LIMIT 1;
                """;

            using var ctx = _factory.CreateDbContext();
            var conn = ctx.Database.GetDbConnection();
            await conn.OpenAsync();

            using var multi = await conn.QueryMultipleAsync(sql, new
            {
                PersonId = personId,
                PropietarioIds = propietarioIds,
                TipoIds = tipoIds,
            });

            var persona = await multi.ReadSingleOrDefaultAsync<PersonaFila>();
            return new PropietarioDocumentosSubidaDto
            {
                Dni = persona?.Dni,
                FullName = persona?.FullName,
                Propiedades = (await multi.ReadAsync<PropiedadDocumentosDto>()).ToList(),
                TiposVigentes = (await multi.ReadAsync<int>()).ToHashSet(),
                LinkCarpeta = await multi.ReadSingleOrDefaultAsync<string?>(),
            };
        }

        public async Task<PropietarioDocumentosDto> InsertarYListar(
            int personId,
            List<PropietarioDocumentoInsertDto> filas,
            int userId)
        {
            // Un solo INSERT para todo el lote (unnest de arreglos paralelos). El RETURNING hace que
            // la sentencia tenga resultado propio y el lector pase limpio al modal.
            const string sql = """
                INSERT INTO propietario_documento
                    (propietario_id, propietario_documento_tipo_id, nombre, archivo_nombre,
                     archivo_content_type, archivo_tamano_bytes, archivo_url, archivo_drive_id,
                     archivo_item_id, created_user_id)
                SELECT x.propietario_id, x.tipo_id, x.nombre, x.archivo_nombre,
                       x.content_type, x.tamano_bytes, x.url, x.drive_id,
                       x.item_id, @UserId
                FROM unnest(@PropietarioIds, @TipoIds, @Nombres, @ArchivoNombres,
                            @ContentTypes, @Tamanos, @Urls, @DriveIds, @ItemIds)
                     AS x (propietario_id, tipo_id, nombre, archivo_nombre,
                           content_type, tamano_bytes, url, drive_id, item_id)
                RETURNING propietario_documento_id;
                """ + SqlModal;

            using var ctx = _factory.CreateDbContext();
            var conn = ctx.Database.GetDbConnection();
            await conn.OpenAsync();

            using var multi = await conn.QueryMultipleAsync(sql, new
            {
                PersonId = personId,
                UserId = userId,
                PropietarioIds = filas.Select(f => f.PropietarioId).ToArray(),
                TipoIds = filas.Select(f => f.TipoId).ToArray(),
                Nombres = filas.Select(f => f.Nombre).ToArray(),
                ArchivoNombres = filas.Select(f => f.ArchivoNombre).ToArray(),
                ContentTypes = filas.Select(f => f.ContentType).ToArray(),
                Tamanos = filas.Select(f => f.TamanoBytes).ToArray(),
                Urls = filas.Select(f => f.Url).ToArray(),
                DriveIds = filas.Select(f => f.DriveId).ToArray(),
                ItemIds = filas.Select(f => f.ItemId).ToArray(),
            });

            await multi.ReadAsync<int>();
            return await LeerModal(multi);
        }

        public async Task<PropietarioDocumentosDto?> EliminarYListar(int personId, int documentoId, int userId)
        {
            const string sql = """
                UPDATE propietario_documento d
                SET state = false,
                    updated_date_time = now(),
                    updated_user_id = @UserId
                FROM propietario pr
                WHERE pr.propietario_id = d.propietario_id
                  AND pr.person_id = @PersonId AND pr.state
                  AND d.propietario_documento_id = @DocumentoId AND d.state
                RETURNING d.propietario_documento_id;
                """ + SqlModal;

            using var ctx = _factory.CreateDbContext();
            var conn = ctx.Database.GetDbConnection();
            await conn.OpenAsync();

            using var multi = await conn.QueryMultipleAsync(sql, new
            {
                PersonId = personId,
                DocumentoId = documentoId,
                UserId = userId,
            });

            var eliminados = (await multi.ReadAsync<int>()).Count();
            var modal = await LeerModal(multi);
            return eliminados == 0 ? null : modal;
        }

        public async Task<PropietarioDocumentoArchivoRefDto?> GetArchivo(int personId, int documentoId)
        {
            const string sql = """
                SELECT d.archivo_nombre,
                       d.archivo_content_type,
                       d.archivo_drive_id,
                       d.archivo_item_id
                FROM propietario_documento d
                JOIN propietario pr ON pr.propietario_id = d.propietario_id AND pr.state
                WHERE d.propietario_documento_id = @DocumentoId AND d.state
                  AND pr.person_id = @PersonId;
                """;

            using var ctx = _factory.CreateDbContext();
            var conn = ctx.Database.GetDbConnection();

            return await conn.QuerySingleOrDefaultAsync<PropietarioDocumentoArchivoRefDto>(sql, new
            {
                PersonId = personId,
                DocumentoId = documentoId,
            });
        }

        /// <summary>Lee las tres sentencias de <see cref="SqlModal"/> y reparte los documentos por propiedad.</summary>
        private static async Task<PropietarioDocumentosDto> LeerModal(SqlMapper.GridReader multi)
        {
            var modal = new PropietarioDocumentosDto
            {
                Tipos = (await multi.ReadAsync<PropietarioDocumentoTipoDto>()).ToList(),
                Propiedades = (await multi.ReadAsync<PropiedadDocumentosDto>()).ToList(),
            };

            var porPropiedad = (await multi.ReadAsync<PropietarioDocumentoDto>()).ToLookup(d => d.PropietarioId);
            foreach (var propiedad in modal.Propiedades)
                propiedad.Documentos = porPropiedad[propiedad.PropietarioId].ToList();

            return modal;
        }

        private sealed class PersonaFila
        {
            public string? Dni { get; set; }
            public string? FullName { get; set; }
        }
    }
}
