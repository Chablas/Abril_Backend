using Dapper;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Features.ConvivirModule.Features.DocumentosFeature.Application.Dtos;
using Abril_Backend.Features.ConvivirModule.Features.DocumentosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.ConvivirModule.Shared.Dtos;
using Abril_Backend.Infrastructure.Data;

namespace Abril_Backend.Features.ConvivirModule.Features.DocumentosFeature.Infrastructure.Repositories
{
    public class ConvivirDocumentosRepository : IConvivirDocumentosRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ConvivirDocumentosRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        /// <summary>
        /// Dos sentencias en un viaje. «seleccion» es la misma regla que
        /// ConvivirPropiedadesRepository: primero la pedida y después en el orden de la lista.
        /// </summary>
        public async Task<(ConvivirPropiedadDto? Propiedad, List<ConvivirDocumentoFila> Documentos)> GetDocumentos(
            int userId,
            int? propietarioId)
        {
            const string cte = """
                WITH seleccion AS (
                    SELECT pr.propietario_id,
                           pr.project_id,
                           pj.project_description AS proyecto,
                           pr.torre,
                           pr.departamento
                    FROM propietario pr
                    JOIN person p   ON p.person_id = pr.person_id AND p.state
                    JOIN project pj ON pj.project_id = pr.project_id
                    WHERE pr.state AND p.user_id = @UserId
                    ORDER BY (pr.propietario_id = @PropietarioId) DESC,
                             pj.project_description, pr.torre NULLS FIRST, pr.departamento, pr.propietario_id
                    LIMIT 1
                )
                """;

            var sql = cte + """
                SELECT propietario_id, project_id, proyecto, torre, departamento
                FROM seleccion;
                """ + cte + """
                SELECT d.propietario_documento_id AS documento_id,
                       t.nombre                   AS tipo,
                       t.orden                    AS tipo_orden,
                       d.nombre,
                       d.archivo_nombre,
                       d.archivo_content_type     AS content_type,
                       d.archivo_tamano_bytes     AS tamano_bytes,
                       d.created_date_time AT TIME ZONE 'America/Lima' AS subido_el,
                       d.leido_date_time IS NULL  AS nuevo
                FROM propietario_documento d
                JOIN seleccion s                  ON s.propietario_id = d.propietario_id
                JOIN propietario_documento_tipo t ON t.propietario_documento_tipo_id = d.propietario_documento_tipo_id
                WHERE d.state
                ORDER BY d.created_date_time DESC, d.propietario_documento_id DESC;
                """;

            using var ctx = _factory.CreateDbContext();
            var conn = ctx.Database.GetDbConnection();
            await conn.OpenAsync();

            using var multi = await conn.QueryMultipleAsync(sql, new
            {
                UserId = userId,
                PropietarioId = propietarioId ?? 0,
            });

            var propiedad = await multi.ReadSingleOrDefaultAsync<ConvivirPropiedadDto>();
            var documentos = (await multi.ReadAsync<ConvivirDocumentoFila>()).ToList();
            return (propiedad, documentos);
        }

        public async Task<ConvivirDocumentoArchivoFila?> GetArchivo(int userId, int documentoId)
        {
            const string sql = """
                SELECT d.archivo_nombre,
                       d.archivo_content_type,
                       d.archivo_drive_id,
                       d.archivo_item_id
                FROM propietario_documento d
                JOIN propietario pr ON pr.propietario_id = d.propietario_id AND pr.state
                JOIN person p       ON p.person_id = pr.person_id AND p.state
                WHERE d.propietario_documento_id = @DocumentoId AND d.state
                  AND p.user_id = @UserId;
                """;

            using var ctx = _factory.CreateDbContext();
            var conn = ctx.Database.GetDbConnection();

            return await conn.QuerySingleOrDefaultAsync<ConvivirDocumentoArchivoFila>(sql, new
            {
                UserId = userId,
                DocumentoId = documentoId,
            });
        }

        public async Task MarcarLeido(int documentoId)
        {
            // No toca updated_*: abrirlo no es una edición. Su aviso de la campana también queda
            // leído: ya vio el documento.
            const string sql = """
                UPDATE propietario_documento
                SET leido_date_time = now()
                WHERE propietario_documento_id = @DocumentoId AND leido_date_time IS NULL;

                UPDATE propietario_notificacion
                SET leida_date_time = now()
                WHERE propietario_documento_id = @DocumentoId AND state AND leida_date_time IS NULL;
                """;

            using var ctx = _factory.CreateDbContext();
            var conn = ctx.Database.GetDbConnection();
            await conn.ExecuteAsync(sql, new { DocumentoId = documentoId });
        }
    }
}
