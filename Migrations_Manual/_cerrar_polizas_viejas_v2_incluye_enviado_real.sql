-- Extiende el cierre anterior: ahora incluye también los casos con documento
-- real (Enviado/En revision genuino), no solo los abandonados en Falta.
-- Regla: cierra pólizas SCTR/VidaLey de agosto-2026 o antes (Enviado/En
-- revision/Parcial) SOLO si NINGUNO de sus trabajadores tiene una póliza más
-- reciente (setiembre+) del mismo tipo. Para esos trabajadores, si su estado
-- actual en ss_hab_trabajador sigue "abierto" (Enviado/En revision/Falta),
-- se alinea a "Rechazado". Si ya quedó "Aprobado" (por el sync previo), NO
-- se toca — no se degrada nada que ya esté resuelto.

-- 1) Vista previa: pólizas a cerrar + detalle de qué le pasaría a cada worker
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
poliza_cerrable AS (
    SELECT wp.poliza_id
    FROM workers_de_poliza wp
    JOIN tiene_poliza_reciente tpr ON tpr.poliza_id = wp.poliza_id AND tpr.worker_id = wp.worker_id
    GROUP BY wp.poliza_id
    HAVING COUNT(*) FILTER (WHERE tpr.tiene_reciente) = 0
)
SELECT pv.poliza_id, pv.tipo, pv.anio, pv.mes, pv.empresa,
       p.document_identity_code AS dni, per.full_name AS nombre,
       h.id AS hab_id, h.estado AS estado_hab_actual,
       CASE WHEN h.estado IN ('Enviado','En revision','Falta') THEN 'SE MARCA RECHAZADO'
            WHEN h.estado = 'Aprobado' THEN 'NO SE TOCA (ya aprobado)'
            ELSE 'NO SE TOCA (' || COALESCE(h.estado,'sin fila') || ')' END AS accion
FROM polizas_viejas pv
JOIN poliza_cerrable pc ON pc.poliza_id = pv.poliza_id
JOIN ss_sctr_vidaley_worker svw ON svw.sctr_vidaley_id = pv.poliza_id
JOIN workers w ON w.id = svw.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
LEFT JOIN person per ON per.person_id = w.person_id
LEFT JOIN ss_hab_trabajador h ON h.worker_id = w.id
    AND h.item_id = (SELECT it.id FROM ss_item_trabajador it
                      WHERE (CASE WHEN pv.tipo = 'VIDA_LEY' THEN it.nombre ILIKE '%Vida%' ELSE it.nombre ILIKE '%SCTR%' END)
                      LIMIT 1)
ORDER BY pv.anio DESC, pv.mes DESC, pv.poliza_id, nombre;

-- 2a) UPDATE real #1: cerrar las pólizas. Ejecutar SOLO tras revisar la vista
--     previa de arriba. (Postgres no ejecuta un CTE de escritura si no se
--     referencia en la sentencia final, así que van como dos UPDATE separados
--     en vez de encadenados en un solo WITH.)
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
poliza_cerrable AS (
    SELECT wp.poliza_id
    FROM workers_de_poliza wp
    JOIN tiene_poliza_reciente tpr ON tpr.poliza_id = wp.poliza_id AND tpr.worker_id = wp.worker_id
    GROUP BY wp.poliza_id
    HAVING COUNT(*) FILTER (WHERE tpr.tiene_reciente) = 0
)
UPDATE ss_sctr_vidaley s
SET estado = 'Rechazado',
    obs_abril = COALESCE(obs_abril, '') || ' [Cerrada automáticamente: período vencido, sin vinculación a meses posteriores]',
    updated_at = now()
WHERE s.id IN (SELECT poliza_id FROM poliza_cerrable);

-- 2b) UPDATE real #2: alinear a "Rechazado" SOLO a los trabajadores que
--     seguían Enviado/En revision/Falta en esas mismas pólizas (no toca a
--     los que el sync anterior ya dejó "Aprobado"). Ejecutar después del 2a.
WITH polizas_viejas AS (
    SELECT s.id AS poliza_id, s.tipo, s.anio, s.mes
    FROM ss_sctr_vidaley s
    WHERE s.estado = 'Rechazado'
      AND s.obs_abril ILIKE '%Cerrada automáticamente%'
      AND (s.anio < 2026 OR (s.anio = 2026 AND s.mes <= 8))
),
workers_a_rechazar AS (
    SELECT DISTINCT pv.tipo, svw.worker_id
    FROM polizas_viejas pv
    JOIN ss_sctr_vidaley_worker svw ON svw.sctr_vidaley_id = pv.poliza_id
)
UPDATE ss_hab_trabajador h
SET estado = 'Rechazado',
    obs_abril = COALESCE(h.obs_abril, '') || ' [Rechazado automáticamente: póliza vencida sin renovación]',
    updated_at = now()
FROM workers_a_rechazar war
WHERE h.worker_id = war.worker_id
  AND h.item_id = (SELECT it.id FROM ss_item_trabajador it
                    WHERE (CASE WHEN war.tipo = 'VIDA_LEY' THEN it.nombre ILIKE '%Vida%' ELSE it.nombre ILIKE '%SCTR%' END)
                    LIMIT 1)
  AND h.estado IN ('Enviado', 'En revision', 'Falta');
