-- ============================================================================
-- Convivir Abril: qué SQL faltan en esta base (SOLO LECTURA, un solo SELECT)
-- ============================================================================
-- Una fila por cosa que deja cada script de Convivir. ok = false → falta correr el de la columna
-- «script». No falla si todavía no existe una tabla: esa fila sale con ok = false.
-- Los scripts están en esta misma carpeta y se corren en este orden, antes del deploy del backend.
-- ============================================================================

WITH x AS (
    SELECT 1 AS orden,
           '20260929_ConvivirRolPropietario.sql' AS script,
           'Rol 97 = PROPIETARIO' AS que,
           'PROPIETARIO' AS esperado,
           coalesce((SELECT role_description FROM role WHERE role_id = 97), '(no existe)') AS ahora
    UNION ALL
    SELECT 2, '20260929_Propietarios.sql', 'Tabla propietario', 'sí',
           CASE WHEN to_regclass('public.propietario') IS NULL THEN 'no' ELSE 'sí' END
    UNION ALL
    SELECT 3, '20260929_Propietarios.sql', 'Funcionalidad propietarios.gestion para el rol 1', 'sí',
           CASE WHEN EXISTS (SELECT 1
                             FROM feature f
                             JOIN role_feature rf ON rf.feature_id = f.feature_id
                             WHERE f.feature_key = 'propietarios.gestion' AND rf.role_id = 1)
                THEN 'sí' ELSE 'no' END
    UNION ALL
    SELECT 4, '20260929_AppUserEmailUnicoSoloVigentes.sql', 'Correo único solo entre usuarios vigentes', 'sí',
           CASE WHEN EXISTS (SELECT 1 FROM pg_indexes
                             WHERE schemaname = 'public' AND indexname = 'uq_app_user_email'
                               AND indexdef LIKE '%WHERE state%')
                THEN 'sí' ELSE 'no' END
    UNION ALL
    SELECT 5, '20260930_PropietarioDocumentos.sql', 'Tablas de documentos (documento, tipo y carpeta)', 'sí',
           CASE WHEN to_regclass('public.propietario_documento') IS NOT NULL
                 AND to_regclass('public.propietario_documento_tipo') IS NOT NULL
                 AND to_regclass('public.propietario_documento_folder') IS NOT NULL
                THEN 'sí' ELSE 'no' END
    UNION ALL
    -- query_to_xml deja consultar la tabla solo si existe (el CASE no la toca si falta).
    -- La base no sabe en qué ambiente está: la fila dice cuál de las dos carpetas quedó vigente.
    SELECT 6, '20260930_PropietarioDocumentoFolder_Prod.sql (prod) o _DevDemo.sql (dev y demo)',
           'Carpeta de SharePoint vigente (prod: Documentos de Propietarios; dev y demo: Desarrollo / App Convivir - Documentos de Propietarios)',
           'prod en prod; dev y demo en dev y demo',
           CASE WHEN to_regclass('public.propietario_documento_folder') IS NULL THEN '(sin tabla)'
                -- Un agregado para que siempre haya una fila: sin filas, xpath falla (documento vacío).
                ELSE (xpath('/row/n/text()', query_to_xml(
                        $q$SELECT coalesce(max(CASE link_url
                                    WHEN 'https://abrilinmob.sharepoint.com/sites/bibliotecanm/Documentos%20de%20Propietarios/Forms/AllItems.aspx'
                                        THEN 'prod'
                                    WHEN 'https://abrilinmob.sharepoint.com/sites/bibliotecanm/Desarrollo/Forms/AllItems.aspx?id=%2Fsites%2Fbibliotecanm%2FDesarrollo%2FApp%20Convivir%20%2D%20Documentos%20de%20Propietarios&viewid=087a516f%2Da398%2D433d%2Db208%2Df4aa51591a4d'
                                        THEN 'dev y demo'
                                    ELSE 'otra' END), '(ninguna)') AS n
                           FROM propietario_documento_folder
                           WHERE state AND active$q$,
                        false, true, '')))[1]::text
           END
    UNION ALL
    SELECT 7, '20260930_PropietarioNotificaciones.sql', 'Tablas de la campana (notificación y tipo) con los tipos HITO y DOCUMENTO', 'sí',
           CASE WHEN to_regclass('public.propietario_notificacion') IS NULL
                  OR to_regclass('public.propietario_notificacion_tipo') IS NULL THEN 'no'
                -- Mismo truco que la fila 6: la tabla de tipos solo se consulta si existe.
                ELSE (xpath('/row/n/text()', query_to_xml(
                        $q$SELECT CASE WHEN count(*) = 2 THEN 'sí' ELSE 'faltan tipos' END AS n
                           FROM propietario_notificacion_tipo
                           WHERE state AND codigo IN ('HITO', 'DOCUMENTO')$q$,
                        false, true, '')))[1]::text
           END
)
SELECT script, que, esperado, ahora,
       CASE WHEN orden = 6 THEN ahora IN ('prod', 'dev y demo') ELSE ahora = esperado END AS ok
FROM x
ORDER BY orden;
