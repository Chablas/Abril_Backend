-- Corrige los casos detectados por _check_certaptitud_falta_con_emo_apto.sql: recalcula
-- ss_hab_trabajador (item 4, Certificado de Aptitud) a "Aprobado" + vigencia del EMO cuando
-- el último EMO activo del trabajador ya está Apto/Apto con Restricciones y sin interconsulta
-- pendiente, pero el ítem había quedado congelado en "Falta".
-- Ejecutar SIEMPRE el diagnóstico (_check_certaptitud_falta_con_emo_apto.sql) antes, revisar
-- la lista, y recién entonces correr este UPDATE.
WITH candidatos AS (
    SELECT h.id AS hab_id, e.id AS emo_id,
           COALESCE(e.fecha_vencimiento_calculada, e.fecha_vencimiento) AS vigencia_emo
    FROM ss_hab_trabajador h
    JOIN ss_item_trabajador it ON it.id = h.item_id
    JOIN LATERAL (
        SELECT *
        FROM worker_emos we
        WHERE we.worker_id = h.worker_id AND we.activo
        ORDER BY we.fecha_emo DESC
        LIMIT 1
    ) e ON true
    WHERE it.id = 4
      AND h.estado = 'Falta'
      AND e.aptitud IN ('Apto', 'Apto con Restricciones')
      AND NOT (e.requiere_interconsulta = true AND e.interconsulta_resuelta = false)
      AND NOT EXISTS (
          SELECT 1 FROM ss_interconsultas si
          WHERE si.emo_id = e.id AND si.estado = 'Pendiente'
      )
)
UPDATE ss_hab_trabajador h
SET estado = 'Aprobado',
    vigencia = c.vigencia_emo,
    updated_at = now()
FROM candidatos c
WHERE h.id = c.hab_id;
