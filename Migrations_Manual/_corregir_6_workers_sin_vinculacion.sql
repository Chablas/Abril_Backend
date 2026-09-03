-- Corrige a los 6 trabajadores restantes del mismo lote de migración
-- histórica (2026-05-23) que quedaron sin vinculación principal, usando
-- la empresa/proyecto ya confirmados (de ss_hab_worker_proyecto, o elegidos
-- por el usuario cuando tenían varios).

INSERT INTO worker_vinculaciones (worker_id, empresa_id, proyecto_id, categoria_id, fecha_inicio, created_at)
VALUES
    (12169, 635, 7,  (SELECT categoria_id FROM puesto WHERE puesto_id = 221), CURRENT_DATE, now()), -- ESPIRITU RAYME, KAURÍ
    (12359, 574, 8,  (SELECT categoria_id FROM puesto WHERE puesto_id = 4),   CURRENT_DATE, now()), -- OLAECHEA TORRES, CEDRO 33
    (12621, 590, 7,  (SELECT categoria_id FROM puesto WHERE puesto_id = 297), CURRENT_DATE, now()), -- TORRES CABEZUDO, KAURÍ
    (12757, 588, 8,  (SELECT categoria_id FROM puesto WHERE puesto_id = 172), CURRENT_DATE, now()), -- CABRERA VIENA, CEDRO 33
    (13148, 609, 8,  (SELECT categoria_id FROM puesto WHERE puesto_id = 5),   CURRENT_DATE, now()), -- CAREAJANO PUTPANA, CEDRO 33
    (13976, 591, 11, (SELECT categoria_id FROM puesto WHERE puesto_id = 5),   CURRENT_DATE, now()); -- VELA OCHAVANO, MÁXIMO ABRIL

-- Sincronizar SCTR/Vida ley de estos 6 si quedaron "Aprobado" sin fecha
-- (mismo criterio que el sync masivo: copiar la vigencia de la póliza de
-- su propia empresa ya aprobada más reciente).
UPDATE ss_hab_trabajador h
SET vigencia = (
        SELECT s.vigencia
        FROM ss_sctr_vidaley s
        JOIN ss_sctr_vidaley_worker svw ON svw.sctr_vidaley_id = s.id
        JOIN ss_item_trabajador it2 ON it2.id = h.item_id
        WHERE svw.worker_id = h.worker_id
          AND s.tipo = (CASE WHEN it2.nombre ILIKE '%Vida%' THEN 'VIDA_LEY' ELSE 'SCTR' END)
          AND s.estado = 'Aprobado'
          AND s.vigencia IS NOT NULL
        ORDER BY s.anio DESC, s.mes DESC, s.vigencia DESC
        LIMIT 1
    ),
    updated_at = now()
WHERE h.worker_id IN (12169, 12359, 12621, 12757, 13148, 13976)
  AND h.item_id IN (SELECT id FROM ss_item_trabajador WHERE nombre IN ('SCTR', 'Vida ley'))
  AND h.estado = 'Aprobado'
  AND h.vigencia IS NULL
  AND EXISTS (
      SELECT 1 FROM ss_sctr_vidaley s
      JOIN ss_sctr_vidaley_worker svw ON svw.sctr_vidaley_id = s.id
      JOIN ss_item_trabajador it2 ON it2.id = h.item_id
      WHERE svw.worker_id = h.worker_id
        AND s.tipo = (CASE WHEN it2.nombre ILIKE '%Vida%' THEN 'VIDA_LEY' ELSE 'SCTR' END)
        AND s.estado = 'Aprobado'
        AND s.vigencia IS NOT NULL
  );

-- Vista de verificación post-corrección
SELECT h.worker_id, it.nombre AS item, h.estado, h.vigencia
FROM ss_hab_trabajador h
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE h.worker_id IN (12169, 12359, 12621, 12757, 13148, 13976)
  AND it.nombre IN ('SCTR', 'Vida ley')
ORDER BY h.worker_id, it.nombre;
