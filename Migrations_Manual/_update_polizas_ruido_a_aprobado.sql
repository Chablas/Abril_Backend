-- Marca como "Aprobado" SOLO las pólizas SCTR/VidaLey (Enviado/En revision/Parcial)
-- donde el 100% de sus trabajadores YA están Aprobado con vigencia vigente en
-- ss_hab_trabajador (ruido puro: la póliza vieja nunca se cerró pero no hay
-- nada realmente pendiente). NO toca ss_hab_trabajador en absoluto — solo el
-- campo Estado de la póliza. Pólizas con algún trabajador en Falta/Rechazado/
-- Enviado/En revision real quedan intactas, fuera de este UPDATE.

-- 1) Vista previa: pólizas candidatas (100% de sus trabajadores Aprobado+vigente)
WITH poliza_item AS (
    SELECT s.id AS poliza_id, s.tipo, s.anio, s.mes, s.estado,
           (SELECT it.id FROM ss_item_trabajador it
             WHERE it.es_sctr_vidaley = true AND it.activo = true
               AND (CASE WHEN s.tipo = 'VIDA_LEY' THEN it.nombre ILIKE '%Vida%' ELSE it.nombre ILIKE '%SCTR%' END)
             LIMIT 1) AS item_id
    FROM ss_sctr_vidaley s
    WHERE s.estado IN ('Enviado', 'En revision', 'Parcial')
),
worker_status AS (
    SELECT pi.poliza_id, pi.tipo, pi.anio, pi.mes, pi.estado,
           svw.worker_id,
           h.estado AS estado_hab,
           h.vigencia AS vigencia_hab
    FROM poliza_item pi
    JOIN ss_sctr_vidaley_worker svw ON svw.sctr_vidaley_id = pi.poliza_id
    LEFT JOIN ss_hab_trabajador h ON h.worker_id = svw.worker_id AND h.item_id = pi.item_id
)
SELECT poliza_id, tipo, anio, mes, estado AS estado_poliza_actual,
       COUNT(*) AS total_trabajadores,
       COUNT(*) FILTER (WHERE estado_hab = 'Aprobado' AND vigencia_hab >= now()) AS aprobados_vigentes
FROM worker_status
GROUP BY poliza_id, tipo, anio, mes, estado
HAVING COUNT(*) = COUNT(*) FILTER (WHERE estado_hab = 'Aprobado' AND vigencia_hab >= now())
ORDER BY anio DESC, mes DESC;

-- 2) UPDATE real. Ejecutar SOLO después de revisar que la vista previa de
--    arriba tiene sentido (son las pólizas 100% resueltas por otra más nueva).
WITH poliza_item AS (
    SELECT s.id AS poliza_id, s.tipo,
           (SELECT it.id FROM ss_item_trabajador it
             WHERE it.es_sctr_vidaley = true AND it.activo = true
               AND (CASE WHEN s.tipo = 'VIDA_LEY' THEN it.nombre ILIKE '%Vida%' ELSE it.nombre ILIKE '%SCTR%' END)
             LIMIT 1) AS item_id
    FROM ss_sctr_vidaley s
    WHERE s.estado IN ('Enviado', 'En revision', 'Parcial')
),
worker_status AS (
    SELECT pi.poliza_id,
           svw.worker_id,
           h.estado AS estado_hab,
           h.vigencia AS vigencia_hab
    FROM poliza_item pi
    JOIN ss_sctr_vidaley_worker svw ON svw.sctr_vidaley_id = pi.poliza_id
    LEFT JOIN ss_hab_trabajador h ON h.worker_id = svw.worker_id AND h.item_id = pi.item_id
),
polizas_ruido AS (
    SELECT poliza_id
    FROM worker_status
    GROUP BY poliza_id
    HAVING COUNT(*) = COUNT(*) FILTER (WHERE estado_hab = 'Aprobado' AND vigencia_hab >= now())
)
UPDATE ss_sctr_vidaley s
SET estado = 'Aprobado',
    updated_at = now()
WHERE s.id IN (SELECT poliza_id FROM polizas_ruido);
