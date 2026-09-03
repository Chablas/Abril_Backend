-- Cierra (marca "Rechazado") pólizas SCTR/VidaLey viejas (Enviado/En revision,
-- de agosto-2026 o antes) SOLO cuando:
--   a) NINGUNO de sus trabajadores tiene una póliza más reciente (setiembre
--      en adelante) del mismo tipo — o sea, no hay nada actual que se pueda
--      ver afectado, y
--   b) el estado REAL actual en ss_hab_trabajador de CADA trabajador de la
--      póliza ya es 'Falta' (no 'Aprobado' ni nada mejor) — o sea, no se
--      pierde ningún dato válido, solo se limpia la bandeja.
-- NO TOCA ss_hab_trabajador en ningún caso. Solo actualiza ss_sctr_vidaley.Estado.

-- 1) Vista previa
WITH polizas_viejas AS (
    SELECT s.id AS poliza_id, s.tipo, s.anio, s.mes, s.estado,
           c.contributor_name AS empresa
    FROM ss_sctr_vidaley s
    LEFT JOIN contributor c ON c.contributor_id = s.empresa_id
    WHERE s.estado IN ('Enviado', 'En revision', 'Parcial')
      AND (s.anio < 2026 OR (s.anio = 2026 AND s.mes <= 8))
),
workers_de_poliza AS (
    SELECT pv.poliza_id, pv.tipo, pv.anio, pv.mes, pv.estado, pv.empresa,
           svw.worker_id
    FROM polizas_viejas pv
    JOIN ss_sctr_vidaley_worker svw ON svw.sctr_vidaley_id = pv.poliza_id
),
tiene_poliza_reciente AS (
    SELECT wp.poliza_id, wp.worker_id,
        EXISTS (
            SELECT 1 FROM ss_sctr_vidaley s2
            JOIN ss_sctr_vidaley_worker svw2 ON svw2.sctr_vidaley_id = s2.id
            WHERE svw2.worker_id = wp.worker_id
              AND s2.tipo = wp.tipo
              AND (s2.anio > 2026 OR (s2.anio = 2026 AND s2.mes > 8))
        ) AS tiene_reciente
    FROM workers_de_poliza wp
),
estado_hab_actual AS (
    SELECT wp.poliza_id, wp.worker_id,
        (SELECT h.estado
         FROM ss_hab_trabajador h
         JOIN ss_item_trabajador it ON it.id = h.item_id
         WHERE h.worker_id = wp.worker_id
           AND (CASE WHEN wp.tipo = 'VIDA_LEY' THEN it.nombre ILIKE '%Vida%' ELSE it.nombre ILIKE '%SCTR%' END)
         LIMIT 1) AS estado_hab
    FROM workers_de_poliza wp
)
SELECT pv.poliza_id, pv.tipo, pv.anio, pv.mes, pv.estado, pv.empresa,
       COUNT(*) AS total_trabajadores,
       COUNT(*) FILTER (WHERE tpr.tiene_reciente) AS con_poliza_reciente,
       COUNT(*) FILTER (WHERE eha.estado_hab IS DISTINCT FROM 'Falta') AS con_estado_no_falta
FROM polizas_viejas pv
JOIN workers_de_poliza wp ON wp.poliza_id = pv.poliza_id
JOIN tiene_poliza_reciente tpr ON tpr.poliza_id = wp.poliza_id AND tpr.worker_id = wp.worker_id
JOIN estado_hab_actual eha ON eha.poliza_id = wp.poliza_id AND eha.worker_id = wp.worker_id
GROUP BY pv.poliza_id, pv.tipo, pv.anio, pv.mes, pv.estado, pv.empresa
HAVING COUNT(*) FILTER (WHERE tpr.tiene_reciente) = 0
   AND COUNT(*) FILTER (WHERE eha.estado_hab IS DISTINCT FROM 'Falta') = 0
ORDER BY pv.anio DESC, pv.mes DESC;

-- 2) UPDATE real. Ejecutar SOLO tras confirmar que la vista previa de arriba
--    coincide con lo esperado.
WITH polizas_viejas AS (
    SELECT s.id AS poliza_id, s.tipo, s.anio, s.mes
    FROM ss_sctr_vidaley s
    WHERE s.estado IN ('Enviado', 'En revision', 'Parcial')
      AND (s.anio < 2026 OR (s.anio = 2026 AND s.mes <= 8))
),
workers_de_poliza AS (
    SELECT pv.poliza_id, pv.tipo, pv.anio, pv.mes, svw.worker_id
    FROM polizas_viejas pv
    JOIN ss_sctr_vidaley_worker svw ON svw.sctr_vidaley_id = pv.poliza_id
),
tiene_poliza_reciente AS (
    SELECT wp.poliza_id, wp.worker_id,
        EXISTS (
            SELECT 1 FROM ss_sctr_vidaley s2
            JOIN ss_sctr_vidaley_worker svw2 ON svw2.sctr_vidaley_id = s2.id
            WHERE svw2.worker_id = wp.worker_id
              AND s2.tipo = wp.tipo
              AND (s2.anio > 2026 OR (s2.anio = 2026 AND s2.mes > 8))
        ) AS tiene_reciente
    FROM workers_de_poliza wp
),
estado_hab_actual AS (
    SELECT wp.poliza_id, wp.worker_id,
        (SELECT h.estado
         FROM ss_hab_trabajador h
         JOIN ss_item_trabajador it ON it.id = h.item_id
         WHERE h.worker_id = wp.worker_id
           AND (CASE WHEN wp.tipo = 'VIDA_LEY' THEN it.nombre ILIKE '%Vida%' ELSE it.nombre ILIKE '%SCTR%' END)
         LIMIT 1) AS estado_hab
    FROM workers_de_poliza wp
),
polizas_cerrables AS (
    SELECT wp.poliza_id
    FROM workers_de_poliza wp
    JOIN tiene_poliza_reciente tpr ON tpr.poliza_id = wp.poliza_id AND tpr.worker_id = wp.worker_id
    JOIN estado_hab_actual eha ON eha.poliza_id = wp.poliza_id AND eha.worker_id = wp.worker_id
    GROUP BY wp.poliza_id
    HAVING COUNT(*) FILTER (WHERE tpr.tiene_reciente) = 0
       AND COUNT(*) FILTER (WHERE eha.estado_hab IS DISTINCT FROM 'Falta') = 0
)
UPDATE ss_sctr_vidaley s
SET estado = 'Rechazado',
    obs_abril = COALESCE(obs_abril, '') || ' [Cerrada automáticamente: póliza vencida sin renovación, sin impacto en meses posteriores]',
    updated_at = now()
WHERE s.id IN (SELECT poliza_id FROM polizas_cerrables);
