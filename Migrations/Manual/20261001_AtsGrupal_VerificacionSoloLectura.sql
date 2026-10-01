-- ============================================================================
-- ATS Grupal, firma de Capataz y QR de obra (Samuel, 30/09): ¿qué tiene esta base?
-- ============================================================================
-- SOLO LECTURA. Un SELECT: una fila por objeto que necesita el código de Samuel que entró a
-- master y a demo el 2026-10-01, con `existe` = true / false.
--
-- Son dos grupos:
--   · Los de sus cinco scripts de Migrations_Manual/2026-09-30_ats_*.sql (capataz_cuenta,
--     firma_capataz, grupo_firma_capataz, grupo_firma_capataz_evidencia, grupo_proyecto_qr).
--   · Los que el código mapea y NINGÚN script crea (ATS Grupal y PETAR Grupal): las tablas
--     ss_ats_grupo*, ss_petar_grupo* y las columnas ss_ats.ats_grupo_id y
--     ss_petar.petar_grupo_id. Sin ellas falla hasta el ATS individual (42703 al leer ss_ats).
--
-- Dev (2026-10-01): las 15 en false. En prod deberían estar todas (el código ya está
-- desplegado ahí); en demo, ninguna.
-- ============================================================================

SET client_encoding TO 'UTF8';

SELECT v.orden,
       v.objeto,
       v.origen,
       CASE v.tipo
         WHEN 'T' THEN to_regclass(v.nombre) IS NOT NULL
         WHEN 'C' THEN EXISTS (SELECT 1 FROM information_schema.columns c
                               WHERE c.table_schema = 'public'
                                 AND c.table_name = split_part(v.nombre, '.', 1)
                                 AND c.column_name = split_part(v.nombre, '.', 2))
         WHEN 'R' THEN EXISTS (SELECT 1 FROM role r
                               WHERE upper(r.role_description) = v.nombre AND r.state)
       END AS existe
FROM (VALUES
  ( 1, 'tabla ss_ats_grupo',                          'sin script',                     'T', 'ss_ats_grupo'),
  ( 2, 'tabla ss_ats_grupo_paso_seleccionado',        'sin script',                     'T', 'ss_ats_grupo_paso_seleccionado'),
  ( 3, 'tabla ss_ats_grupo_epp_seleccionado',         'sin script',                     'T', 'ss_ats_grupo_epp_seleccionado'),
  ( 4, 'tabla ss_ats_grupo_herramienta_seleccionada', 'sin script',                     'T', 'ss_ats_grupo_herramienta_seleccionada'),
  ( 5, 'tabla ss_ats_grupo_riesgo_detalle',           'sin script',                     'T', 'ss_ats_grupo_riesgo_detalle'),
  ( 6, 'tabla ss_petar_grupo',                        'sin script',                     'T', 'ss_petar_grupo'),
  ( 7, 'tabla ss_petar_grupo_item_respuesta',         'sin script',                     'T', 'ss_petar_grupo_item_respuesta'),
  ( 8, 'columna ss_ats.ats_grupo_id',                 'sin script',                     'C', 'ss_ats.ats_grupo_id'),
  ( 9, 'columna ss_petar.petar_grupo_id',             'sin script',                     'C', 'ss_petar.petar_grupo_id'),
  (10, 'columnas ss_ats.capataz_*',                   '2026-09-30_ats_firma_capataz',   'C', 'ss_ats.capataz_firma_url'),
  (11, 'columnas ss_ats_grupo.capataz_* (firma)',     '..._ats_grupo_firma_capataz',    'C', 'ss_ats_grupo.capataz_adhesiones_al_firmar'),
  (12, 'columnas ss_ats_grupo.capataz_* (evidencia)', '..._grupo_firma_capataz_evidencia', 'C', 'ss_ats_grupo.capataz_selfie_url'),
  (13, 'tabla ss_ats_proyecto_qr',                    '2026-09-30_ats_grupo_proyecto_qr', 'T', 'ss_ats_proyecto_qr'),
  (14, 'columna ss_ats_autorizacion_permiso.email_personal', '2026-09-30_ats_capataz_cuenta', 'C', 'ss_ats_autorizacion_permiso.email_personal'),
  (15, 'rol CAPATAZ / MAESTRO DE OBRA',               '2026-09-30_ats_capataz_cuenta',  'R', 'CAPATAZ / MAESTRO DE OBRA')
) AS v (orden, objeto, origen, tipo, nombre)
ORDER BY v.orden;
