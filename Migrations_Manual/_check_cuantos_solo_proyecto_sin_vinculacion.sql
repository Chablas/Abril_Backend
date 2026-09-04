-- Cuántos workers ACTIVOS (contratistas) tienen asignaciones en
-- ss_hab_worker_proyecto (activas) pero CERO en worker_vinculaciones (nunca,
-- ni siquiera cerrada). Si sale solo 1 (Parque Medina), es un caso aislado;
-- si salen más, es un patrón/bug sistemático.
SELECT w.id AS worker_id, p.document_identity_code AS dni, p.full_name,
       we.nombre AS estado_worker, w.created_at AS worker_created_at,
       COUNT(DISTINCT wp.id) AS proyectos_apoyo_activos
FROM workers w
JOIN person p ON p.person_id = w.person_id
LEFT JOIN workers_estado we ON we.workers_estado_id = w.workers_estado_id
JOIN ss_hab_worker_proyecto wp ON wp.worker_id = w.id AND wp.fecha_fin IS NULL
WHERE lower(trim(w.contrata_casa)) <> 'casa'
  AND NOT EXISTS (SELECT 1 FROM worker_vinculaciones v WHERE v.worker_id = w.id)
GROUP BY w.id, p.document_identity_code, p.full_name, we.nombre, w.created_at
ORDER BY w.created_at;
