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
    SELECT 6, 'A mano: plantilla al final de 20260930_PropietarioDocumentos.sql',
           'Carpeta de SharePoint de los documentos (filas vigentes)', '1',
           CASE WHEN to_regclass('public.propietario_documento_folder') IS NULL THEN '(sin tabla)'
                ELSE (xpath('/row/n/text()', query_to_xml(
                        'SELECT count(*) AS n FROM propietario_documento_folder WHERE state AND active',
                        false, true, '')))[1]::text
           END
)
SELECT script, que, esperado, ahora, ahora = esperado AS ok
FROM x
ORDER BY orden;
