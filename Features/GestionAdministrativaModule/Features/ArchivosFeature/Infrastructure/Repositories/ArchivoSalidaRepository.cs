using Abril_Backend.Features.GestionAdministrativa.Archivos.Application;
using Abril_Backend.Features.GestionAdministrativa.Archivos.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Archivos.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Archivos.Infrastructure.Repositories
{
    public class ArchivoSalidaRepository : IArchivoSalidaRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ArchivoSalidaRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

        /// <summary>
        /// Una sola consulta. Los archivos del módulo viven en cinco lugares —la planilla y su copia
        /// firmada (<c>ga_rendicion</c>), los cuatro documentos del consolidado
        /// (<c>ga_consolidado_s10</c>), la planilla grupal preparada que todavía espera su S10
        /// (<c>ga_planilla_grupal</c>) y los adjuntos de los trayectos (la tabla nueva y la columna
        /// vieja de <c>ga_solicitud_trayecto</c>)— y cada rama trae a los trabajadores dueños: los de
        /// las salidas de esa planilla, de las planillas que cubre el consolidado o la planilla grupal
        /// (o de su salida, en los consolidados viejos) o de la salida del trayecto. El LEFT JOIN deja
        /// la fila aunque no haya salidas, para que "existe" no dependa de tener dueño.
        ///
        /// Se lee SQL crudo: si alguna de estas columnas se renombra o se bota, hay que tocarla acá.
        /// </summary>
        public async Task<AccesoArchivoSalida> GetAcceso(string url, int userId, int[] roleIds)
        {
            using var ctx = _factory.CreateDbContext();

            var resultado = await ctx.Database.SqlQuery<int>($"""
                WITH duenos AS (
                    SELECT s.worker_id
                    FROM ga_rendicion r
                    LEFT JOIN ga_solicitud_salida s ON s.rendicion_id = r.id
                    WHERE r.pdf_url = {url} OR r.pdf_firmado_url = {url}
                    UNION ALL
                    SELECT s.worker_id
                    FROM ga_consolidado_s10 c
                    LEFT JOIN ga_consolidado_s10_rendicion v ON v.consolidado_s10_id = c.id
                    LEFT JOIN ga_solicitud_salida s ON s.rendicion_id = v.rendicion_id
                    WHERE {url} IN (c.pdf_url, c.pdf_firmado_url, c.planilla_grupal_url, c.planilla_grupal_firmado_url)
                    UNION ALL
                    SELECT s.worker_id
                    FROM ga_consolidado_s10 c
                    JOIN ga_solicitud_salida s ON s.id = c.solicitud_id
                    WHERE {url} IN (c.pdf_url, c.pdf_firmado_url, c.planilla_grupal_url, c.planilla_grupal_firmado_url)
                    UNION ALL
                    SELECT s.worker_id
                    FROM ga_planilla_grupal g
                    LEFT JOIN ga_planilla_grupal_rendicion v ON v.planilla_grupal_id = g.id
                    LEFT JOIN ga_solicitud_salida s ON s.rendicion_id = v.rendicion_id
                    WHERE g.pdf_url = {url}
                    UNION ALL
                    SELECT s.worker_id
                    FROM ga_solicitud_trayecto_adjunto a
                    JOIN ga_solicitud_trayecto t ON t.id = a.trayecto_id
                    JOIN ga_solicitud_salida s ON s.id = t.solicitud_id
                    WHERE a.adjunto_url = {url}
                    UNION ALL
                    SELECT s.worker_id
                    FROM ga_solicitud_trayecto t
                    JOIN ga_solicitud_salida s ON s.id = t.solicitud_id
                    WHERE t.adjunto_url = {url}
                )
                SELECT CASE
                    WHEN NOT EXISTS (SELECT 1 FROM duenos) THEN 0
                    WHEN EXISTS (
                            SELECT 1
                            FROM role_feature rf
                            JOIN feature f ON f.feature_id = rf.feature_id
                            WHERE rf.role_id = ANY({roleIds})
                              AND f.feature_key = ANY({PantallasSalidas.DeRevision}))
                      OR EXISTS (
                            SELECT 1
                            FROM duenos d
                            JOIN workers w ON w.id = d.worker_id
                            JOIN person p ON p.person_id = w.person_id
                            WHERE p.user_id = {userId})
                    THEN 2
                    ELSE 1
                END AS "Value"
                """)
                .ToListAsync();

            return (AccesoArchivoSalida)resultado.FirstOrDefault();
        }
    }
}
