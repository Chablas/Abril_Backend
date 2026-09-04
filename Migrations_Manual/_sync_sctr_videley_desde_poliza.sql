-- Sincroniza ss_hab_trabajador.vigencia con la vigencia de la póliza de
-- empresa (ss_sctr_vidaley) ya Aprobada más reciente que cubre a ese
-- trabajador, SOLO para los casos donde h.estado='Aprobado' y h.vigencia
-- está en NULL (nunca se propagó). No se inventa ninguna fecha: se copia
-- la que ya existe y fue aprobada en la póliza real.

-- 1) Vista previa: qué se va a actualizar (worker, item, fecha que se copiaría)
WITH pendientes AS (
    SELECT h.id AS hab_id, h.worker_id, it.nombre AS item, w.id AS wid
    FROM ss_hab_trabajador h
    JOIN workers w ON w.id = h.worker_id
    LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
    JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
    JOIN ss_item_trabajador it ON it.id = h.item_id
    WHERE it.nombre IN ('SCTR', 'Vida ley')
      AND h.estado = 'Aprobado'
      AND h.vigencia IS NULL
      AND lower(trim(w.contrata_casa)) <> 'casa'
      AND we.nombre = 'Activo'
),
mejor_poliza AS (
    SELECT p.hab_id, p.wid, p.item,
        (SELECT s.vigencia
         FROM ss_sctr_vidaley s
         JOIN ss_sctr_vidaley_worker svw ON svw.sctr_vidaley_id = s.id
         WHERE svw.worker_id = p.wid
           AND s.tipo = (CASE WHEN p.item = 'Vida ley' THEN 'VIDA_LEY' ELSE 'SCTR' END)
           AND s.estado = 'Aprobado'
           AND s.vigencia IS NOT NULL
         ORDER BY s.anio DESC, s.mes DESC, s.vigencia DESC
         LIMIT 1) AS vigencia_poliza
    FROM pendientes p
)
SELECT p.document_identity_code AS dni, per.full_name AS nombre, mp.item, mp.vigencia_poliza
FROM mejor_poliza mp
JOIN workers w ON w.id = mp.wid
LEFT JOIN person per ON per.person_id = w.person_id
LEFT JOIN person p ON p.person_id = w.person_id
WHERE mp.vigencia_poliza IS NOT NULL
ORDER BY mp.item, nombre;

-- 2) UPDATE real. Ejecutar SOLO tras revisar la vista previa.
WITH pendientes AS (
    SELECT h.id AS hab_id, h.worker_id, it.nombre AS item, w.id AS wid
    FROM ss_hab_trabajador h
    JOIN workers w ON w.id = h.worker_id
    LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
    JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
    JOIN ss_item_trabajador it ON it.id = h.item_id
    WHERE it.nombre IN ('SCTR', 'Vida ley')
      AND h.estado = 'Aprobado'
      AND h.vigencia IS NULL
      AND lower(trim(w.contrata_casa)) <> 'casa'
      AND we.nombre = 'Activo'
),
mejor_poliza AS (
    SELECT p.hab_id, p.wid, p.item,
        (SELECT s.vigencia
         FROM ss_sctr_vidaley s
         JOIN ss_sctr_vidaley_worker svw ON svw.sctr_vidaley_id = s.id
         WHERE svw.worker_id = p.wid
           AND s.tipo = (CASE WHEN p.item = 'Vida ley' THEN 'VIDA_LEY' ELSE 'SCTR' END)
           AND s.estado = 'Aprobado'
           AND s.vigencia IS NOT NULL
         ORDER BY s.anio DESC, s.mes DESC, s.vigencia DESC
         LIMIT 1) AS vigencia_poliza
    FROM pendientes p
)
UPDATE ss_hab_trabajador h
SET vigencia = mp.vigencia_poliza,
    updated_at = now()
FROM mejor_poliza mp
WHERE h.id = mp.hab_id
  AND mp.vigencia_poliza IS NOT NULL;
