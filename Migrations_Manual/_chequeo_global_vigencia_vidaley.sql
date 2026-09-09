-- SOLO LECTURA. Chequeo global (no limitado a las listas curadas anteriores):
-- para CUALQUIER ss_hab_trabajador del ítem Vida Ley cuyo archivo_url
-- coincida EXACTO con el archivo_url de alguna póliza ss_sctr_vidaley
-- (tipo VIDA_LEY) que sí tenga vigencia, compara si la vigencia del
-- trabajador coincide con la de esa póliza. Trae cualquier desfase que se
-- nos haya escapado, en cualquier empresa.

SELECT
    per.document_identity_code AS dni,
    per.full_name AS nombre,
    c.contributor_name AS empresa,
    h.id AS hab_trabajador_id,
    h.archivo_url,
    h.vigencia AS vigencia_actual,
    s.vigencia AS vigencia_poliza,
    s.id AS poliza_id
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN person per ON per.person_id = w.person_id
LEFT JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
LEFT JOIN contributor c ON c.contributor_id = v.empresa_id
JOIN ss_item_trabajador it ON it.id = h.item_id AND it.nombre ILIKE '%Vida%' AND it.es_sctr_vidaley = true
JOIN ss_sctr_vidaley s ON s.tipo = 'VIDA_LEY' AND s.archivo_url = h.archivo_url AND s.vigencia IS NOT NULL
WHERE h.vigencia IS DISTINCT FROM s.vigencia
  AND w.workers_estado_id <> 2 -- 2 = Retirado (ver WorkersEstadoIds.cs); solo no retirados
  AND lower(trim(w.contrata_casa)) = 'casa' -- solo personal Abril, no contratistas
  AND w.obra_oficina_staff_id IN (2, 3) -- Staff u Oficina Central, alcance real del bug
ORDER BY empresa, nombre;
