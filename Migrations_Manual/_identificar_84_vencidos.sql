-- Identifica los 84 (SCTR + Vida ley) que quedaron "Aprobado" pero con fecha
-- YA VENCIDA, para contratistas activos con vinculación vigente. Marca si su
-- updated_at es de HOY (o sea, se tocaron recién en nuestro UPDATE de
-- sincronización) o si ya estaban vencidos desde antes (dato viejo, sin
-- relación con el sync que acabamos de correr).
SELECT
    it.nombre AS item,
    p.document_identity_code AS dni,
    p.full_name AS nombre,
    c.contributor_name AS empresa,
    pr.project_description AS proyecto,
    h.vigencia,
    h.updated_at,
    CASE WHEN h.updated_at::date = CURRENT_DATE THEN 'TOCADO HOY (por nuestro sync)'
         ELSE 'YA ESTABA ASI DE ANTES' END AS origen
FROM ss_hab_trabajador h
JOIN workers w ON w.id = h.worker_id
LEFT JOIN person p ON p.person_id = w.person_id
LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
JOIN worker_vinculaciones v ON v.worker_id = w.id AND v.fecha_fin IS NULL
LEFT JOIN contributor c ON c.contributor_id = v.empresa_id
LEFT JOIN project pr ON pr.project_id = v.proyecto_id
JOIN ss_item_trabajador it ON it.id = h.item_id
WHERE it.nombre IN ('SCTR', 'Vida ley')
  AND h.estado = 'Aprobado'
  AND h.vigencia IS NOT NULL
  AND h.vigencia < now()
  AND lower(trim(w.contrata_casa)) <> 'casa'
  AND we.nombre = 'Activo'
ORDER BY origen, it.nombre, nombre;
